using System.Data.Common;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ZuMarket.Models;

namespace ZuMarket.Services;

public class IdentitySetupService : IHostedService
{
    private readonly IServiceProvider _services;
    private readonly IConfiguration _configuration;
    private readonly ILogger<IdentitySetupService> _logger;
    private readonly IWebHostEnvironment _environment;

    public IdentitySetupService(IServiceProvider services, IConfiguration configuration, ILogger<IdentitySetupService> logger, IWebHostEnvironment environment)
    {
        _services = services;
        _configuration = configuration;
        _logger = logger;
        _environment = environment;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _services.CreateScope();
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            foreach (var role in new[] { "User", "Admin" })
            {
                if (await roles.RoleExistsAsync(role)) continue;
                var roleResult = await roles.CreateAsync(new IdentityRole(role));
                if (!roleResult.Succeeded)
                {
                    _logger.LogError("Identity role creation failed for {Role}: {Errors}", role, string.Join(", ", roleResult.Errors.Select(x => x.Description)));
                    return;
                }
            }

            if (!_environment.IsDevelopment()) return;

            var email = _configuration["InitialAdmin:Email"];
            var password = _configuration["InitialAdmin:Password"];
            var fullName = _configuration["InitialAdmin:FullName"];
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(fullName)) return;

            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var admin = await users.FindByEmailAsync(email);

            var created = false;
            if (admin is null)
            {
                admin = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true,
                    FullName = fullName,
                    VerificationStatus = VerificationStatus.Approved
                };
                var result = await users.CreateAsync(admin, password);
                if (!result.Succeeded)
                {
                    _logger.LogError("Initial administrator creation failed: {Errors}", string.Join(", ", result.Errors.Select(x => x.Description)));
                    return;
                }
                created = true;
                _logger.LogInformation("Initial administrator account {Email} was created.", email);
            }

            if (!await users.IsInRoleAsync(admin, "Admin"))
            {
                var roleResult = await users.AddToRoleAsync(admin, "Admin");
                if (!roleResult.Succeeded)
                {
                    _logger.LogError("Initial administrator role assignment failed: {Errors}", string.Join(", ", roleResult.Errors.Select(x => x.Description)));
                    return;
                }
            }

            if (string.Equals(admin.Email, "abd@admin.com", StringComparison.OrdinalIgnoreCase) && (admin.UniversityId is not null || admin.College is not null))
            {
                admin.UniversityId = null;
                admin.College = null;
                await users.UpdateAsync(admin);
            }

            var resetExistingPassword = _configuration.GetValue<bool>("InitialAdmin:ResetExistingPassword");
            var passwordResetKey = _configuration["InitialAdmin:PasswordResetKey"];
            if (!resetExistingPassword)
            {
                if (!created) _logger.LogInformation("Initial administrator account {Email} already exists; its password was not changed.", email);
                return;
            }

            if (string.IsNullOrWhiteSpace(passwordResetKey) || passwordResetKey.Length < 32)
            {
                _logger.LogError("InitialAdmin:PasswordResetKey must contain at least 32 characters when password reset is enabled.");
                return;
            }

            const string loginProvider = "InitialAdminBootstrap";
            const string tokenName = "PasswordResetKey";
            var appliedResetKey = await users.GetAuthenticationTokenAsync(admin, loginProvider, tokenName);
            if (appliedResetKey == passwordResetKey) return;

            if (!created)
            {
                var resetToken = await users.GeneratePasswordResetTokenAsync(admin);
                var resetResult = await users.ResetPasswordAsync(admin, resetToken, password);
                if (!resetResult.Succeeded)
                {
                    _logger.LogError("Initial administrator password reset failed: {Errors}", string.Join(", ", resetResult.Errors.Select(x => x.Description)));
                    return;
                }
                _logger.LogInformation("The configured one-time password reset was applied to administrator account {Email}.", email);
            }

            var tokenResult = await users.SetAuthenticationTokenAsync(admin, loginProvider, tokenName, passwordResetKey);
            if (!tokenResult.Succeeded)
                _logger.LogError("Initial administrator password reset marker could not be stored: {Errors}", string.Join(", ", tokenResult.Errors.Select(x => x.Description)));
        }
        catch (Exception exception) when (exception is InvalidOperationException or DbException or DbUpdateException)
        {
            _logger.LogWarning(exception, "Identity initialization is waiting for the database migration.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
