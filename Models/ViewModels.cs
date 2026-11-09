using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace ZuMarket.Models;

public class RegisterViewModel
{
    [Required, StringLength(100, MinimumLength = 3)] public string FullName { get; set; } = string.Empty;
    [Required, EmailAddress] public string Email { get; set; } = string.Empty;
    [Required, DataType(DataType.Password), StringLength(100, MinimumLength = 8)] public string Password { get; set; } = string.Empty;
    [Required, DataType(DataType.Password), Compare(nameof(Password))] public string ConfirmPassword { get; set; } = string.Empty;
    public bool AcceptTerms { get; set; }
}

public class LoginViewModel
{
    [Required, EmailAddress] public string Email { get; set; } = string.Empty;
    [Required, DataType(DataType.Password)] public string Password { get; set; } = string.Empty;
    public bool RememberMe { get; set; }
    public string? ReturnUrl { get; set; }
}

public class ForgotPasswordViewModel { [Required, EmailAddress] public string Email { get; set; } = string.Empty; }

public class ResetPasswordViewModel
{
    [Required, EmailAddress] public string Email { get; set; } = string.Empty;
    [Required] public string Token { get; set; } = string.Empty;
    [Required, DataType(DataType.Password), StringLength(100, MinimumLength = 8)] public string Password { get; set; } = string.Empty;
    [Required, DataType(DataType.Password), Compare(nameof(Password))] public string ConfirmPassword { get; set; } = string.Empty;
}

public class ProfileViewModel
{
    [Required, StringLength(100, MinimumLength = 3)] public string FullName { get; set; } = string.Empty;
    [StringLength(120, MinimumLength = 2)] public string? College { get; set; }
    [Phone] public string? PhoneNumber { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? UniversityId { get; set; }
    public VerificationStatus VerificationStatus { get; set; }
}

public class ChangePasswordViewModel
{
    [Required, DataType(DataType.Password)] public string CurrentPassword { get; set; } = string.Empty;
    [Required, DataType(DataType.Password), StringLength(100, MinimumLength = 8)] public string NewPassword { get; set; } = string.Empty;
    [Required, DataType(DataType.Password), Compare(nameof(NewPassword))] public string ConfirmPassword { get; set; } = string.Empty;
}

public class ListingFormViewModel
{
    public int? Id { get; set; }
    [Required, StringLength(120, MinimumLength = 3)] public string Title { get; set; } = string.Empty;
    [Required, StringLength(3000, MinimumLength = 10)] public string Description { get; set; } = string.Empty;
    [Required(ErrorMessage = "CategoryRequired"), Range(4, 18, ErrorMessage = "CategoryRequired")] public int? CategoryId { get; set; }
    [Range(typeof(decimal), "0", "1000000")] public decimal? Price { get; set; }
    public bool IsFree { get; set; }
    [Required(ErrorMessage = "ConditionRequired"), EnumDataType(typeof(ListingCondition))] public ListingCondition? Condition { get; set; }
    public List<IFormFile> Photos { get; set; } = new();
}

public class ListingFilterViewModel
{
    public string? Query { get; set; }
    public int? CategoryId { get; set; }
    public ListingCondition? Condition { get; set; }
    public ListingAvailabilityStatus? Availability { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public string Sort { get; set; } = "newest";
    public int Page { get; set; } = 1;
    public int TotalPages { get; set; }
    public IReadOnlyList<Listing> Items { get; set; } = Array.Empty<Listing>();
    public IReadOnlyList<Category> Categories { get; set; } = Array.Empty<Category>();
}

public class HomeViewModel
{
    public IReadOnlyList<Category> Categories { get; set; } = Array.Empty<Category>();
    public IReadOnlyList<Listing> RecentListings { get; set; } = Array.Empty<Listing>();
}

public class AdminReportsViewModel
{
    public IReadOnlyList<ListingReport> Reports { get; set; } = Array.Empty<ListingReport>();
    public IReadOnlyDictionary<int, ListingReportSummary> LatestSummaries { get; set; } = new Dictionary<int, ListingReportSummary>();
    public bool AiAvailable { get; set; }
}

public class MessageViewModel { [Required, StringLength(2000, MinimumLength = 1)] public string Body { get; set; } = string.Empty; }
public class ReportViewModel
{
    public int ListingId { get; set; }
    [EnumDataType(typeof(ReportReason))] public ReportReason Reason { get; set; }
    [StringLength(1000)] public string? Details { get; set; }
}

public class AdminDashboardViewModel
{
    public int Users { get; set; }
    public int PendingVerifications { get; set; }
    public int PendingListings { get; set; }
    public int PendingReports { get; set; }
    public int PublishedListings { get; set; }
    public AiProviderAvailability AiProviderAvailability { get; set; }
    public bool AiProviderEnabled { get; set; }
    public bool AiApiKeyConfigured { get; set; }
    public bool AiRequiredSettingsConfigured { get; set; }
    public decimal AiConfidenceThreshold { get; set; }
}

public class RegisterConfirmationViewModel
{
    public string Email { get; set; } = string.Empty;
    public string? DevelopmentConfirmationUrl { get; set; }
}

public class DashboardViewModel
{
    public ApplicationUser User { get; set; } = new();
    public IReadOnlyList<Listing> Listings { get; set; } = Array.Empty<Listing>();
    public int Favorites { get; set; }
    public int Conversations { get; set; }
}

public class ConversationSummaryViewModel
{
    public int Id { get; set; }
    public int ListingId { get; set; }
    public string ListingTitle { get; set; } = string.Empty;
    public string OtherParticipantName { get; set; } = string.Empty;
    public string? LatestMessage { get; set; }
    public DateTime LastMessageAtUtc { get; set; }
    public int UnreadCount { get; set; }
}
