using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace ZuMarket.Models;

public class ApplicationUser : IdentityUser
{
    [Required, StringLength(100, MinimumLength = 3)]
    public string FullName { get; set; } = string.Empty;
    [StringLength(40, MinimumLength = 3)]
    public string? UniversityId { get; set; }
    [StringLength(120, MinimumLength = 2)]
    public string? College { get; set; }
    public VerificationStatus VerificationStatus { get; set; } = VerificationStatus.Pending;
    public bool IsDisabled { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAtUtc { get; set; }
    public bool CanUseMarketplace => VerificationStatus == VerificationStatus.Approved && !IsDisabled;
}
