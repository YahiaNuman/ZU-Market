using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ZuMarket.Models;
using ZuMarket.Services;

namespace ZuMarket.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IAiListingReviewProvider _aiReview;
    private readonly IReportSummaryService _reportSummaries;
    public AdminController(ApplicationDbContext db, UserManager<ApplicationUser> users, IAiListingReviewProvider aiReview, IReportSummaryService reportSummaries) { _db = db; _users = users; _aiReview = aiReview; _reportSummaries = reportSummaries; }

    public async Task<IActionResult> Index()
    {
        var availability = await _aiReview.CheckAvailabilityAsync(HttpContext.RequestAborted);
        return View(new AdminDashboardViewModel
        {
            Users = await _users.Users.CountAsync(),
            PendingVerifications = await _db.VerificationRequests.CountAsync(x => x.Status == VerificationStatus.Pending),
            PendingListings = await _db.Listings.CountAsync(x => x.ModerationStatus == ListingModerationStatus.Pending),
            PendingReports = await _db.ListingReports.CountAsync(x => x.Status == ReportStatus.Pending),
            PublishedListings = await _db.Listings.CountAsync(x => x.ModerationStatus == ListingModerationStatus.Approved),
            AiProviderAvailability = availability,
            AiProviderEnabled = _aiReview.IsEnabled,
            AiApiKeyConfigured = _aiReview.HasApiKey,
            AiRequiredSettingsConfigured = _aiReview.HasRequiredSettings,
            AiConfidenceThreshold = _aiReview.ConfidenceThreshold
        });
    }

    public async Task<IActionResult> Verifications() => View(await _db.VerificationRequests.AsNoTracking().Include(x => x.User).OrderBy(x => x.Status).ThenBy(x => x.SubmittedAtUtc).ToListAsync());
    public async Task<IActionResult> VerificationDetails(int id)
    {
        var item = await _db.VerificationRequests.AsNoTracking().Include(x => x.User).Include(x => x.ReviewedBy).FirstOrDefaultAsync(x => x.Id == id);
        return item is null ? NotFound() : View(item);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ReviewVerification(int id, bool approve, string? notes)
    {
        var request = await _db.VerificationRequests.Include(x => x.User).FirstOrDefaultAsync(x => x.Id == id && x.Status == VerificationStatus.Pending);
        if (request is null) return NotFound();
        var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        request.Status = approve ? VerificationStatus.Approved : VerificationStatus.Rejected;
        request.User!.VerificationStatus = request.Status; request.ReviewNotes = notes?.Trim(); request.ReviewedById = adminId; request.ReviewedAtUtc = DateTime.UtcNow;
        _db.ModerationActions.Add(new ModerationAction { AdministratorId = adminId, TargetUserId = request.UserId, ActionType = approve ? ModerationActionType.VerificationApproved : ModerationActionType.VerificationRejected, Notes = notes?.Trim() });
        await _db.SaveChangesAsync(); TempData["Success"] = approve ? "VerificationApproved" : "VerificationRejected";
        return RedirectToAction(nameof(Verifications));
    }

    public async Task<IActionResult> Listings(ListingModerationStatus? status)
    {
        var query = _db.Listings.AsNoTracking().Include(x => x.Category).Include(x => x.Seller).Include(x => x.Images).Include(x => x.AiReviews).AsQueryable();
        if (status.HasValue) query = status == ListingModerationStatus.Pending
            ? query.Where(x => x.ModerationStatus == status || x.ReviewState == ListingReviewState.NeedsManualReview)
            : query.Where(x => x.ModerationStatus == status);
        else query = query.Where(x => x.ModerationStatus == ListingModerationStatus.Pending);
        return View(await query.OrderBy(x => x.CreatedAtUtc).ToListAsync());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ReviewListing(int id, ListingModerationStatus status, string? notes)
    {
        if (status is not (ListingModerationStatus.Approved or ListingModerationStatus.Rejected or ListingModerationStatus.Hidden or ListingModerationStatus.Removed)) return BadRequest();
        var listing = await _db.Listings.FindAsync(id); if (listing is null) return NotFound();
        var previousReviewState = listing.ReviewState;
        listing.ModerationStatus = status; listing.ModerationNotes = notes?.Trim(); listing.UpdatedAtUtc = DateTime.UtcNow;
        if (status == ListingModerationStatus.Approved) listing.ReviewState = ListingReviewState.ManuallyApproved;
        if (status == ListingModerationStatus.Rejected) listing.ReviewState = ListingReviewState.ManuallyRejected;
        if (status == ListingModerationStatus.Removed) listing.AvailabilityStatus = ListingAvailabilityStatus.Removed;
        var action = previousReviewState == ListingReviewState.AiApproved && status != ListingModerationStatus.Approved
            ? ModerationActionType.AiOverrideRejected
            : previousReviewState == ListingReviewState.NeedsManualReview && status == ListingModerationStatus.Approved
                ? ModerationActionType.AiOverrideApproved
                : status switch { ListingModerationStatus.Approved => ModerationActionType.Approved, ListingModerationStatus.Rejected => ModerationActionType.Rejected, ListingModerationStatus.Hidden => ModerationActionType.Hidden, _ => ModerationActionType.Removed };
        _db.ModerationActions.Add(new ModerationAction { AdministratorId = User.FindFirstValue(ClaimTypes.NameIdentifier)!, ListingId = id, ActionType = action, Notes = notes?.Trim() });
        await _db.SaveChangesAsync(); TempData["Success"] = "ModerationSaved";
        return RedirectToAction(nameof(Listings));
    }

    public async Task<IActionResult> Reports()
    {
        var reports = await _db.ListingReports.AsNoTracking().Include(x => x.Listing).Include(x => x.Reporter).OrderBy(x => x.Status).ThenByDescending(x => x.CreatedAtUtc).ToListAsync();
        var listingIds = reports.Where(x => x.Status == ReportStatus.Pending).Select(x => x.ListingId).Distinct().ToList();
        var summaries = await _db.ListingReportSummaries.AsNoTracking().Where(x => listingIds.Contains(x.ListingId)).ToListAsync();
        return View(new AdminReportsViewModel
        {
            Reports = reports,
            LatestSummaries = summaries.GroupBy(x => x.ListingId).ToDictionary(x => x.Key, x => x.OrderByDescending(y => y.Id).First()),
            AiAvailable = _reportSummaries.IsAvailable
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SummarizeReports(int listingId)
    {
        var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var summary = await _reportSummaries.SummarizeAsync(listingId, adminId, CultureInfo.CurrentUICulture.TwoLetterISOLanguageName, HttpContext.RequestAborted);
        if (summary is null) TempData["Error"] = "AiSummaryFailed";
        return Redirect($"{Url.Action(nameof(Reports))}#listing-{listingId}");
    }

    public async Task<IActionResult> History() => View(await _db.ModerationActions.AsNoTracking()
        .Include(x => x.Administrator).Include(x => x.Listing).Include(x => x.TargetUser)
        .OrderByDescending(x => x.CreatedAtUtc).Take(200).ToListAsync());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ResolveReport(int id, bool dismiss, string? notes)
    {
        var report = await _db.ListingReports.FindAsync(id); if (report is null) return NotFound();
        var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        report.Status = dismiss ? ReportStatus.Dismissed : ReportStatus.Resolved; report.ResolutionNotes = notes?.Trim(); report.ResolvedById = adminId; report.ResolvedAtUtc = DateTime.UtcNow;
        _db.ModerationActions.Add(new ModerationAction { AdministratorId = adminId, ListingId = report.ListingId, ActionType = dismiss ? ModerationActionType.ReportDismissed : ModerationActionType.ReportResolved, Notes = notes?.Trim() });
        await _db.SaveChangesAsync(); TempData["Success"] = "ReportResolved";
        return RedirectToAction(nameof(Reports));
    }

    public async Task<IActionResult> Users()
    {
        ViewBag.AdminIds = (await _users.GetUsersInRoleAsync("Admin")).Select(x => x.Id).ToHashSet();
        return View(await _users.Users.AsNoTracking().OrderByDescending(x => x.CreatedAtUtc).ToListAsync());
    }

    public async Task<IActionResult> Categories() => View(await _db.Categories.AsNoTracking().OrderBy(x => x.SortOrder).ToListAsync());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> MoveCategory(int id, bool up)
    {
        var categories = await _db.Categories.OrderBy(x => x.SortOrder).ToListAsync();
        var index = categories.FindIndex(x => x.Id == id);
        var target = up ? index - 1 : index + 1;
        if (index < 0 || target < 0 || target >= categories.Count) return RedirectToAction(nameof(Categories));
        (categories[index].SortOrder, categories[target].SortOrder) = (categories[target].SortOrder, categories[index].SortOrder);
        await _db.SaveChangesAsync();
        TempData["Success"] = "CategoryOrderUpdated";
        return RedirectToAction(nameof(Categories));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleUser(string id)
    {
        var currentId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        if (id == currentId) { TempData["Error"] = "CannotDisableSelf"; return RedirectToAction(nameof(Users)); }
        var user = await _users.FindByIdAsync(id); if (user is null) return NotFound();
        user.IsDisabled = !user.IsDisabled;
        if (user.IsDisabled) await _users.UpdateSecurityStampAsync(user);
        await _users.UpdateAsync(user);
        _db.ModerationActions.Add(new ModerationAction { AdministratorId = currentId, TargetUserId = id, ActionType = user.IsDisabled ? ModerationActionType.UserDisabled : ModerationActionType.UserEnabled });
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Users));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleAdmin(string id)
    {
        var currentId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        if (id == currentId) { TempData["Error"] = "CannotChangeOwnRole"; return RedirectToAction(nameof(Users)); }
        var user = await _users.FindByIdAsync(id); if (user is null) return NotFound();
        var isAdmin = await _users.IsInRoleAsync(user, "Admin");
        if (!isAdmin && user.IsDisabled) { TempData["Error"] = "CannotGrantDisabledUser"; return RedirectToAction(nameof(Users)); }
        IdentityResult result = isAdmin ? await _users.RemoveFromRoleAsync(user, "Admin") : await _users.AddToRoleAsync(user, "Admin");
        if (!result.Succeeded) TempData["Error"] = "RoleChangeFailed";
        else
        {
            _db.ModerationActions.Add(new ModerationAction { AdministratorId = currentId, TargetUserId = id, ActionType = isAdmin ? ModerationActionType.AdminRevoked : ModerationActionType.AdminGranted });
            await _db.SaveChangesAsync();
            TempData["Success"] = "RoleChanged";
        }
        return RedirectToAction(nameof(Users));
    }
}
