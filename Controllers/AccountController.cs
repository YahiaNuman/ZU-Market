using System.Globalization;
using System.Net;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Localization;
using ZuMarket.Models;
using ZuMarket.Services;

namespace ZuMarket.Controllers;

public class AccountController : Controller
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly SignInManager<ApplicationUser> _signIn;
    private readonly ApplicationDbContext _db;
    private readonly IUniversityEmailPolicy _universityEmail;
    private readonly IAppEmailSender _email;
    private readonly IMemoryCache _cache;
    private readonly IWebHostEnvironment _environment;
    private readonly IStringLocalizer<SharedResource> _text;

    public AccountController(UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn, ApplicationDbContext db, IUniversityEmailPolicy universityEmail, IAppEmailSender email, IMemoryCache cache, IWebHostEnvironment environment, IStringLocalizer<SharedResource> text)
    {
        _users = users;
        _signIn = signIn;
        _db = db;
        _universityEmail = universityEmail;
        _email = email;
        _cache = cache;
        _environment = environment;
        _text = text;
    }

    [HttpGet]
    public IActionResult Register() => View(new RegisterViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!model.AcceptTerms) ModelState.AddModelError(nameof(model.AcceptTerms), _text["TermsRequired"]);
        if (!_universityEmail.IsConfigured) ModelState.AddModelError(string.Empty, _text["RegistrationClosed"]);
        else if (!_universityEmail.IsAllowed(model.Email)) ModelState.AddModelError(nameof(model.Email), _text["EmailDomainNotAllowed"]);
        if (!_email.IsConfigured && !_environment.IsDevelopment()) ModelState.AddModelError(string.Empty, _text["EmailDeliveryUnavailable"]);
        if (!ModelState.IsValid) return View(model);

        var user = new ApplicationUser
        {
            UserName = model.Email.Trim(), Email = model.Email.Trim(),
            FullName = model.FullName.Trim(),
            VerificationStatus = VerificationStatus.Pending
        };
        var result = await _users.CreateAsync(user, model.Password);
        if (result.Succeeded)
        {
            await _users.AddToRoleAsync(user, "User");
            if (!await SendConfirmationAsync(user)) TempData["Error"] = "EmailSendFailed";
            return RedirectToAction(nameof(RegisterConfirmation), new { email = user.Email });
        }
        foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error.Description);
        return View(model);
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null) => View(new LoginViewModel { ReturnUrl = returnUrl });

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        var user = await _users.FindByEmailAsync(model.Email.Trim());
        if (user is null || user.IsDisabled)
        {
            ModelState.AddModelError(string.Empty, _text["InvalidLogin"]);
            return View(model);
        }
        var result = await _signIn.PasswordSignInAsync(user, model.Password, model.RememberMe, true);
        if (result.Succeeded)
        {
            user.LastLoginAtUtc = DateTime.UtcNow;
            if (_universityEmail.CanAutoApprove(user)) user.VerificationStatus = VerificationStatus.Approved;
            await _users.UpdateAsync(user);
            return LocalRedirect(Url.IsLocalUrl(model.ReturnUrl) ? model.ReturnUrl! : Url.Action("Dashboard")!);
        }
        if (result.IsNotAllowed && !user.EmailConfirmed)
        {
            TempData["Error"] = "EmailNotConfirmed";
            return RedirectToAction(nameof(RegisterConfirmation), new { email = user.Email });
        }
        var errorKey = result.IsLockedOut ? "AccountLocked" : "InvalidLogin";
        ModelState.AddModelError(string.Empty, _text[errorKey]);
        return View(model);
    }

    [HttpGet]
    public IActionResult RegisterConfirmation(string email)
    {
        return View(new RegisterConfirmationViewModel
        {
            Email = email,
            DevelopmentConfirmationUrl = _environment.IsDevelopment() ? TempData["DevelopmentConfirmationUrl"] as string : null
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ResendConfirmation(string email)
    {
        var address = email?.Trim() ?? string.Empty;
        var user = address.Length == 0 ? null : await _users.FindByEmailAsync(address);
        if (user is { EmailConfirmed: false } && _universityEmail.IsAllowed(user.Email) && TryStartCooldown("confirmation", user.Id))
            await SendConfirmationAsync(user);
        TempData["Success"] = "ConfirmationResent";
        return RedirectToAction(nameof(RegisterConfirmation), new { email = address });
    }

    [HttpGet]
    public async Task<IActionResult> ConfirmEmail(string email, string token)
    {
        var user = await _users.FindByEmailAsync(email);
        if (user is null) return NotFound();
        try
        {
            var decoded = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(token));
            var result = await _users.ConfirmEmailAsync(user, decoded);
            if (result.Succeeded && _universityEmail.CanAutoApprove(user))
            {
                user.VerificationStatus = VerificationStatus.Approved;
                await _users.UpdateAsync(user);
            }
            TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded ? "EmailConfirmed" : "EmailConfirmationFailed";
        }
        catch (FormatException)
        {
            TempData["Error"] = "EmailConfirmationFailed";
        }
        return RedirectToAction(nameof(Login));
    }

    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signIn.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public IActionResult ForgotPassword() => View(new ForgotPasswordViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        var user = await _users.FindByEmailAsync(model.Email.Trim());
        if (user is not null)
        {
            var token = await _users.GeneratePasswordResetTokenAsync(user);
            var encoded = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
            var url = Url.Action(nameof(ResetPassword), "Account", new { email = user.Email, token = encoded }, Request.Scheme);
            if (_environment.IsDevelopment()) TempData["DevelopmentResetUrl"] = url;
            if (_email.IsConfigured && TryStartCooldown("reset", user.Id))
                await _email.SendAsync(user.Email!, _text["ResetEmailSubject"], BuildEmail(_text["ResetEmailIntro"], _text["ResetEmailAction"], url!));
        }
        else if (_environment.IsDevelopment())
        {
            TempData["DevelopmentResetUnavailable"] = true;
        }
        TempData["Success"] = "PasswordResetRequested";
        return RedirectToAction(nameof(ForgotPassword));
    }

    [HttpGet]
    public IActionResult ResetPassword(string email, string token) => View(new ResetPasswordViewModel { Email = email, Token = token });

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        var user = await _users.FindByEmailAsync(model.Email);
        if (user is null) return RedirectToAction(nameof(Login));
        var token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(model.Token));
        var result = await _users.ResetPasswordAsync(user, token, model.Password);
        if (result.Succeeded)
        {
            TempData["Success"] = "PasswordResetComplete";
            return RedirectToAction(nameof(Login));
        }
        foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error.Description);
        return View(model);
    }

    [Authorize]
    public async Task<IActionResult> Dashboard()
    {
        var user = (await _users.GetUserAsync(User))!;
        return View(new DashboardViewModel
        {
            User = user,
            Listings = await _db.Listings.AsNoTracking().Include(x => x.Images).Where(x => x.SellerId == user.Id).OrderByDescending(x => x.CreatedAtUtc).Take(5).ToListAsync(),
            Favorites = await _db.Favorites.CountAsync(x => x.UserId == user.Id),
            Conversations = await _db.Conversations.CountAsync(x => x.BuyerId == user.Id || x.SellerId == user.Id)
        });
    }

    [Authorize, HttpGet]
    public async Task<IActionResult> Profile()
    {
        var user = (await _users.GetUserAsync(User))!;
        return View(new ProfileViewModel { FullName = user.FullName, College = user.College, PhoneNumber = user.PhoneNumber, Email = user.Email!, UniversityId = user.UniversityId, VerificationStatus = user.VerificationStatus });
    }

    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(ProfileViewModel model)
    {
        ModelState.Remove(nameof(model.Email));
        ModelState.Remove(nameof(model.UniversityId));
        var user = (await _users.GetUserAsync(User))!;
        if (ModelState.IsValid)
        {
            user.FullName = model.FullName.Trim(); user.College = string.IsNullOrWhiteSpace(model.College) ? null : model.College.Trim();
            user.PhoneNumber = string.IsNullOrWhiteSpace(model.PhoneNumber) ? null : model.PhoneNumber.Trim();
            var result = await _users.UpdateAsync(user);
            if (result.Succeeded) TempData["Success"] = "ProfileUpdated";
            else foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error.Description);
        }
        model.Email = user.Email!; model.UniversityId = user.UniversityId; model.VerificationStatus = user.VerificationStatus;
        return View(model);
    }

    [Authorize, HttpGet]
    public IActionResult Settings() => View(new ChangePasswordViewModel());

    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Settings(ChangePasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        var user = (await _users.GetUserAsync(User))!;
        var result = await _users.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
        if (result.Succeeded)
        {
            await _signIn.RefreshSignInAsync(user); TempData["Success"] = "PasswordChanged";
            return RedirectToAction(nameof(Settings));
        }
        foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error.Description);
        return View(model);
    }

    public IActionResult AccessDenied() => View();

    private async Task<bool> SendConfirmationAsync(ApplicationUser user)
    {
        var token = await _users.GenerateEmailConfirmationTokenAsync(user);
        var encoded = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        var url = Url.Action(nameof(ConfirmEmail), "Account", new { email = user.Email, token = encoded }, Request.Scheme)!;
        if (_environment.IsDevelopment()) TempData["DevelopmentConfirmationUrl"] = url;
        if (!_email.IsConfigured) return _environment.IsDevelopment();
        return await _email.SendAsync(user.Email!, _text["ConfirmationEmailSubject"], BuildEmail(_text["ConfirmationEmailIntro"], _text["ConfirmationEmailAction"], url));
    }

    private bool TryStartCooldown(string purpose, string userId)
    {
        var key = $"email-cooldown:{purpose}:{userId}";
        if (_cache.TryGetValue(key, out _)) return false;
        _cache.Set(key, true, TimeSpan.FromSeconds(60));
        return true;
    }

    private string BuildEmail(string intro, string action, string url)
    {
        var direction = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar" ? "rtl" : "ltr";
        return $"""
            <div dir="{direction}" style="font-family:Arial,sans-serif;font-size:16px;line-height:1.6">
              <p>{WebUtility.HtmlEncode(intro)}</p>
              <p><a href="{WebUtility.HtmlEncode(url)}">{WebUtility.HtmlEncode(action)}</a></p>
              <p style="color:#666;font-size:14px">{WebUtility.HtmlEncode(_text["EmailIgnoreNote"])}</p>
            </div>
            """;
    }
}
