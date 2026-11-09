using System.Diagnostics;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ZuMarket.Models;

namespace ZuMarket.Controllers;

public class HomeController : Controller
{
    private readonly ApplicationDbContext _db;
    public HomeController(ApplicationDbContext db) { _db = db; }

    public async Task<IActionResult> Index()
    {
        var categories = await _db.Categories.AsNoTracking().OrderBy(x => x.SortOrder).ToListAsync();
        var publicListings = _db.Listings.AsNoTracking()
            .Where(x => x.ModerationStatus == ListingModerationStatus.Approved && x.AvailabilityStatus == ListingAvailabilityStatus.Available);

        var model = new HomeViewModel
        {
            Categories = categories,
            RecentListings = await publicListings.Include(x => x.Category).Include(x => x.Images)
                .OrderByDescending(x => x.CreatedAtUtc).Take(24).ToListAsync()
        };
        return View(model);
    }

    public IActionResult Privacy() => View();
    public IActionResult Terms() => View();
    public IActionResult HttpStatus(int code) => code == 404 ? View("NotFound") : View("Error", new ErrorViewModel { RequestId = HttpContext.TraceIdentifier });

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult SetLanguage(string culture, string returnUrl)
    {
        if (culture is not ("ar" or "en")) culture = "en";
        Response.Cookies.Append(CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
            new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1), IsEssential = true, SameSite = SameSiteMode.Lax });
        return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl : "/");
    }
}
