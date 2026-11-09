using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ZuMarket.Models;
using ZuMarket.Services;

namespace ZuMarket.Controllers;

[Authorize]
public class VerificationController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IPrivateFileStorage _files;
    private readonly IUniversityEmailPolicy _universityEmail;
    public VerificationController(ApplicationDbContext db, UserManager<ApplicationUser> users, IPrivateFileStorage files, IUniversityEmailPolicy universityEmail) { _db = db; _users = users; _files = files; _universityEmail = universityEmail; }

    public async Task<IActionResult> Index()
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var user = (await _users.GetUserAsync(User))!;
        if (_universityEmail.CanAutoApprove(user))
        {
            user.VerificationStatus = VerificationStatus.Approved;
            await _users.UpdateAsync(user);
        }
        ViewBag.User = user;
        return View(await _db.VerificationRequests.AsNoTracking().Where(x => x.UserId == id).OrderByDescending(x => x.SubmittedAtUtc).FirstOrDefaultAsync());
    }

    public async Task<IActionResult> Document(int id)
    {
        var request = await _db.VerificationRequests.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (request is null) return NotFound();
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (request.UserId != userId && !User.IsInRole("Admin")) return Forbid();
        var file = await _files.OpenAsync(request.StorageFileName, "verification");
        return file is null ? NotFound() : File(file.Value.Stream, file.Value.ContentType);
    }
}
