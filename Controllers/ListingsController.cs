using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using ZuMarket.Models;
using ZuMarket.Services;

namespace ZuMarket.Controllers;

public class ListingsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IPrivateFileStorage _files;
    private readonly IListingSearchService _search;
    private readonly IStringLocalizer<SharedResource> _text;
    private readonly IListingReviewOrchestrator _review;
    private readonly IListingDraftService _drafts;
    private const int MaxDraftPhotos = 3;

    public ListingsController(ApplicationDbContext db, UserManager<ApplicationUser> users, IPrivateFileStorage files, IListingSearchService search, IStringLocalizer<SharedResource> text, IListingReviewOrchestrator review, IListingDraftService drafts)
    {
        _db = db; _users = users; _files = files; _search = search; _text = text; _review = review; _drafts = drafts;
    }

    public async Task<IActionResult> Index(ListingFilterViewModel filter) => View(await _search.SearchAsync(filter));

    public async Task<IActionResult> Details(int id)
    {
        var listing = await _db.Listings.AsNoTracking().Include(x => x.Category).Include(x => x.Images).Include(x => x.Seller).FirstOrDefaultAsync(x => x.Id == id);
        if (listing is null) return NotFound();
        var currentId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var canPreview = currentId == listing.SellerId || User.IsInRole("Admin");
        if (listing.ModerationStatus != ListingModerationStatus.Approved && !canPreview) return NotFound();
        ViewBag.IsFavorite = currentId is not null && await _db.Favorites.AnyAsync(x => x.UserId == currentId && x.ListingId == id);
        ViewBag.ShowTranslation = listing.ShowsTranslation(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName) && !User.IsInRole("Admin");
        return View(listing);
    }

    [Authorize, HttpPost, ValidateAntiForgeryToken, EnableRateLimiting(ListingDraftRateLimit.PolicyName)]
    public async Task<IActionResult> Draft(List<IFormFile> photos)
    {
        if (!await CanUseMarketplace()) return StatusCode(StatusCodes.Status403Forbidden);
        if (!_drafts.IsAvailable) return StatusCode(StatusCodes.Status503ServiceUnavailable);
        if (photos.Count is < 1 or > MaxDraftPhotos) return BadRequest();

        var images = new List<AiImageInput>();
        foreach (var photo in photos)
        {
            try { await PrivateFileStorage.ValidateImageAsync(photo, HttpContext.RequestAborted); }
            catch (InvalidOperationException) { return BadRequest(); }
            await using var input = photo.OpenReadStream();
            using var memory = new MemoryStream();
            await input.CopyToAsync(memory, HttpContext.RequestAborted);
            images.Add(new AiImageInput(photo.ContentType, memory.ToArray()));
        }

        var outcome = await _drafts.CreateAsync(images, CultureInfo.CurrentUICulture.TwoLetterISOLanguageName, HttpContext.RequestAborted);
        return outcome.Status switch
        {
            ListingDraftStatus.Ready => Json(new { title = outcome.Draft!.Title, description = outcome.Draft.Description, categoryId = outcome.Draft.CategoryId, condition = (int)outcome.Draft.Condition }),
            ListingDraftStatus.NotRecognized => UnprocessableEntity(),
            _ => StatusCode(StatusCodes.Status502BadGateway)
        };
    }

    [Authorize, HttpGet]
    public async Task<IActionResult> Create()
    {
        if (!await CanUseMarketplace()) return RedirectToAction("Index", "Verification");
        ViewBag.Categories = await _db.Categories.AsNoTracking().OrderBy(x => x.SortOrder).ToListAsync();
        return View(new ListingFormViewModel());
    }

    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ListingFormViewModel model)
    {
        var user = await _users.GetUserAsync(User);
        if (user is null || !user.CanUseMarketplace) return Forbid();
        if (model.IsFree) { model.Price = 0; }
        else if (model.Price is null) ModelState.AddModelError(nameof(model.Price), _text["PriceRequired"]);
        if (model.Photos.Count is < 1 or > 6) ModelState.AddModelError(nameof(model.Photos), _text["PhotoCountInvalid"]);
        if (!ModelState.IsValid)
        {
            ViewBag.Categories = await _db.Categories.AsNoTracking().OrderBy(x => x.SortOrder).ToListAsync();
            return View(model);
        }

        var listing = new Listing
        {
            Title = model.Title.Trim(), Description = model.Description.Trim(), CategoryId = model.CategoryId!.Value,
            Price = model.Price!.Value, IsFree = model.IsFree, Condition = model.Condition!.Value, SellerId = user.Id,
            ModerationStatus = ListingModerationStatus.Pending
        };
        _db.Listings.Add(listing);
        try
        {
            for (var i = 0; i < model.Photos.Count; i++)
            {
                var stored = await _files.SaveAsync(model.Photos[i], "listings");
                listing.Images.Add(new ListingImage { StorageFileName = stored.FileName, OriginalFileName = stored.OriginalFileName, ContentType = stored.ContentType, Length = stored.Length, SortOrder = i });
            }
            await _db.SaveChangesAsync();
            await _review.ReviewAsync(listing.Id);
        }
        catch (InvalidOperationException)
        {
            ModelState.AddModelError(nameof(model.Photos), _text["InvalidImage"]);
            ViewBag.Categories = await _db.Categories.AsNoTracking().OrderBy(x => x.SortOrder).ToListAsync();
            return View(model);
        }
        TempData["Success"] = "ListingSubmitted";
        return RedirectToAction(nameof(My));
    }

    [Authorize, HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var listing = await _db.Listings.Include(x => x.Images).FirstOrDefaultAsync(x => x.Id == id && x.SellerId == userId && x.AvailabilityStatus != ListingAvailabilityStatus.Removed);
        if (listing is null) return NotFound();
        ViewBag.Categories = await _db.Categories.AsNoTracking().OrderBy(x => x.SortOrder).ToListAsync();
        ViewBag.ExistingImages = listing.Images.OrderBy(x => x.SortOrder).ToList();
        return View(new ListingFormViewModel { Id = listing.Id, Title = listing.Title, Description = listing.Description, CategoryId = listing.CategoryId, Price = listing.Price, IsFree = listing.IsFree, Condition = listing.Condition });
    }

    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ListingFormViewModel model)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var listing = await _db.Listings.Include(x => x.Images).FirstOrDefaultAsync(x => x.Id == id && x.SellerId == userId && x.AvailabilityStatus != ListingAvailabilityStatus.Removed);
        if (listing is null) return NotFound();
        if (model.IsFree) { model.Price = 0; }
        else if (model.Price is null) ModelState.AddModelError(nameof(model.Price), _text["PriceRequired"]);
        if (listing.Images.Count + model.Photos.Count > 6) ModelState.AddModelError(nameof(model.Photos), _text["PhotoCountInvalid"]);
        if (!ModelState.IsValid)
        {
            ViewBag.Categories = await _db.Categories.AsNoTracking().OrderBy(x => x.SortOrder).ToListAsync();
            ViewBag.ExistingImages = listing.Images.OrderBy(x => x.SortOrder).ToList();
            return View(model);
        }
        var title = model.Title.Trim();
        var description = model.Description.Trim();
        if (title != listing.Title || description != listing.Description) listing.ClearTranslation();
        listing.Title = title; listing.Description = description; listing.CategoryId = model.CategoryId!.Value;
        listing.Price = model.Price!.Value; listing.IsFree = model.IsFree; listing.Condition = model.Condition!.Value;
        listing.ModerationStatus = ListingModerationStatus.Pending; listing.ModerationNotes = null; listing.UpdatedAtUtc = DateTime.UtcNow;
        listing.ReviewState = ListingReviewState.PendingReview;
        try
        {
            foreach (var photo in model.Photos)
            {
                var stored = await _files.SaveAsync(photo, "listings");
                listing.Images.Add(new ListingImage { StorageFileName = stored.FileName, OriginalFileName = stored.OriginalFileName, ContentType = stored.ContentType, Length = stored.Length, SortOrder = listing.Images.Count });
            }
            await _db.SaveChangesAsync();
            await _review.ReviewAsync(listing.Id);
        }
        catch (InvalidOperationException)
        {
            ModelState.AddModelError(nameof(model.Photos), _text["InvalidImage"]);
            ViewBag.Categories = await _db.Categories.AsNoTracking().OrderBy(x => x.SortOrder).ToListAsync();
            ViewBag.ExistingImages = listing.Images.OrderBy(x => x.SortOrder).ToList();
            return View(model);
        }
        TempData["Success"] = "ListingUpdated";
        return RedirectToAction(nameof(My));
    }

    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveImage(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var image = await _db.ListingImages.Include(x => x.Listing).FirstOrDefaultAsync(x => x.Id == id && x.Listing!.SellerId == userId);
        if (image is null) return NotFound();
        if (await _db.ListingImages.CountAsync(x => x.ListingId == image.ListingId) <= 1)
        {
            TempData["Error"] = "ListingNeedsPhoto";
            return RedirectToAction(nameof(Edit), new { id = image.ListingId });
        }
        _db.ListingImages.Remove(image); await _db.SaveChangesAsync(); await _files.DeleteAsync(image.StorageFileName, "listings");
        return RedirectToAction(nameof(Edit), new { id = image.ListingId });
    }

    [Authorize]
    public async Task<IActionResult> My()
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return View(await _db.Listings.AsNoTracking().Include(x => x.Category).Include(x => x.Images).Where(x => x.SellerId == id).OrderByDescending(x => x.CreatedAtUtc).ToListAsync());
    }

    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkSold(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var listing = await _db.Listings.FirstOrDefaultAsync(x => x.Id == id && x.SellerId == userId);
        if (listing is null) return NotFound();
        listing.AvailabilityStatus = ListingAvailabilityStatus.Sold; listing.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(); TempData["Success"] = "ListingMarkedSold";
        return RedirectToAction(nameof(My));
    }

    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAvailable(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var listing = await _db.Listings.FirstOrDefaultAsync(x => x.Id == id && x.SellerId == userId && x.AvailabilityStatus == ListingAvailabilityStatus.Sold);
        if (listing is null) return NotFound();
        listing.AvailabilityStatus = ListingAvailabilityStatus.Available;
        listing.ModerationStatus = ListingModerationStatus.Pending;
        listing.ReviewState = ListingReviewState.PendingReview;
        listing.ModerationNotes = null;
        listing.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await _review.ReviewAsync(listing.Id);
        TempData["Success"] = "ListingResubmitted";
        return RedirectToAction(nameof(My));
    }

    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Hide(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var listing = await _db.Listings.FirstOrDefaultAsync(x => x.Id == id && x.SellerId == userId && x.AvailabilityStatus != ListingAvailabilityStatus.Removed && x.ModerationStatus != ListingModerationStatus.Hidden);
        if (listing is null) return NotFound();
        listing.ModerationStatus = ListingModerationStatus.Hidden;
        listing.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        TempData["Success"] = "ListingHidden";
        return RedirectToAction(nameof(My));
    }

    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitForReview(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var listing = await _db.Listings.FirstOrDefaultAsync(x => x.Id == id && x.SellerId == userId && x.AvailabilityStatus != ListingAvailabilityStatus.Removed && (x.ModerationStatus == ListingModerationStatus.Hidden || x.ModerationStatus == ListingModerationStatus.Rejected));
        if (listing is null) return NotFound();
        listing.ModerationStatus = ListingModerationStatus.Pending;
        listing.ReviewState = ListingReviewState.PendingReview;
        listing.ModerationNotes = null;
        listing.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await _review.ReviewAsync(listing.Id);
        TempData["Success"] = "ListingResubmitted";
        return RedirectToAction(nameof(My));
    }

    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var listing = await _db.Listings.FirstOrDefaultAsync(x => x.Id == id && x.SellerId == userId);
        if (listing is null) return NotFound();
        listing.AvailabilityStatus = ListingAvailabilityStatus.Removed; listing.ModerationStatus = ListingModerationStatus.Removed;
        await _db.SaveChangesAsync(); TempData["Success"] = "ListingRemoved";
        return RedirectToAction(nameof(My));
    }

    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleFavorite(int id, string? returnUrl)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var listingExists = await _db.Listings.AnyAsync(x => x.Id == id && x.ModerationStatus == ListingModerationStatus.Approved && x.SellerId != userId);
        if (!listingExists) return NotFound();
        var favorite = await _db.Favorites.FindAsync(userId, id);
        if (favorite is null) _db.Favorites.Add(new Favorite { UserId = userId, ListingId = id }); else _db.Favorites.Remove(favorite);
        await _db.SaveChangesAsync();
        return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl : Url.Action(nameof(Details), new { id })!);
    }

    [Authorize]
    public async Task<IActionResult> Favorites()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        return View(await _db.Favorites.AsNoTracking().Include(x => x.Listing)!.ThenInclude(x => x!.Images).Include(x => x.Listing)!.ThenInclude(x => x!.Category)
            .Where(x => x.UserId == userId && x.Listing!.ModerationStatus == ListingModerationStatus.Approved).OrderByDescending(x => x.CreatedAtUtc).ToListAsync());
    }

    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Report(ReportViewModel model)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        if (!ModelState.IsValid || !await _db.Listings.AnyAsync(x => x.Id == model.ListingId && x.ModerationStatus == ListingModerationStatus.Approved && x.SellerId != userId)) return NotFound();
        if (!await _db.ListingReports.AnyAsync(x => x.ListingId == model.ListingId && x.ReporterId == userId && x.Status == ReportStatus.Pending))
        {
            _db.ListingReports.Add(new ListingReport { ListingId = model.ListingId, ReporterId = userId, Reason = model.Reason, Details = model.Details?.Trim() });
            await _db.SaveChangesAsync();
        }
        TempData["Success"] = "ReportSubmitted";
        return RedirectToAction(nameof(Details), new { id = model.ListingId });
    }

    public async Task<IActionResult> Image(int id)
    {
        var image = await _db.ListingImages.AsNoTracking().Include(x => x.Listing).FirstOrDefaultAsync(x => x.Id == id);
        if (image is null) return NotFound();
        var currentId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (image.Listing!.ModerationStatus != ListingModerationStatus.Approved && image.Listing.SellerId != currentId && !User.IsInRole("Admin")) return NotFound();
        var stored = await _files.OpenAsync(image.StorageFileName, "listings");
        return stored is null ? NotFound() : File(stored.Value.Stream, stored.Value.ContentType);
    }

    private async Task<bool> CanUseMarketplace()
    {
        var user = await _users.GetUserAsync(User);
        return user?.CanUseMarketplace == true;
    }
}
