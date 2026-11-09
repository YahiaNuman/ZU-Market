using System.Net.Mail;
using ZuMarket.Models;

namespace ZuMarket.Services;

public interface IUniversityEmailPolicy
{
    bool IsConfigured { get; }
    string DisplayDomains { get; }
    bool IsAllowed(string? email);
    bool CanAutoApprove(ApplicationUser user);
}

public sealed class UniversityEmailPolicy : IUniversityEmailPolicy
{
    private readonly string[] _domains;

    public UniversityEmailPolicy(IConfiguration configuration)
    {
        _domains = (configuration["Registration:AllowedEmailDomain"] ?? string.Empty)
            .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(domain => domain.TrimStart('@').ToLowerInvariant())
            .Where(domain => domain.Length > 0)
            .ToArray();
    }

    public bool IsConfigured => _domains.Length > 0;
    public string DisplayDomains => string.Join(", ", _domains.Select(domain => $"@{domain}"));

    public bool IsAllowed(string? email)
    {
        var value = email?.Trim();
        return MailAddress.TryCreate(value, out var address)
            && address.Address == value
            && _domains.Contains(address.Host.ToLowerInvariant());
    }

    // Only Pending accounts qualify, so an administrator's rejection is never overturned.
    public bool CanAutoApprove(ApplicationUser user) =>
        user.VerificationStatus == VerificationStatus.Pending && user.EmailConfirmed && !user.IsDisabled && IsAllowed(user.Email);
}
