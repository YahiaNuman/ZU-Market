using System.ComponentModel.DataAnnotations;

namespace ZuMarket.Models;

public class Listing : IValidatableObject
{
    public const int MaxTranslatedTitleLength = 400;
    public const int MaxTranslatedDescriptionLength = 6000;

    public int Id { get; set; }
    [Required, StringLength(120, MinimumLength = 3)]
    public string Title { get; set; } = string.Empty;
    [Required, StringLength(3000, MinimumLength = 10)]
    public string Description { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public Category? Category { get; set; }
    [Range(typeof(decimal), "0", "1000000")]
    public decimal Price { get; set; }
    public bool IsFree { get; set; }
    public ListingCondition Condition { get; set; }
    public ListingModerationStatus ModerationStatus { get; set; } = ListingModerationStatus.Pending;
    public ListingReviewState ReviewState { get; set; }
    public ListingAvailabilityStatus AvailabilityStatus { get; set; } = ListingAvailabilityStatus.Available;
    [StringLength(1000)]
    public string? ModerationNotes { get; set; }
    [Required]
    public string SellerId { get; set; } = string.Empty;
    public ApplicationUser? Seller { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public ICollection<ListingImage> Images { get; set; } = new List<ListingImage>();
    public ICollection<AiListingReview> AiReviews { get; set; } = new List<AiListingReview>();
    [StringLength(2)]
    public string? SourceLanguage { get; set; }
    [StringLength(MaxTranslatedTitleLength)]
    public string? TranslatedTitle { get; set; }
    [StringLength(MaxTranslatedDescriptionLength)]
    public string? TranslatedDescription { get; set; }
    public int TranslationAttempts { get; set; }
    public DateTime? TranslationNextAttemptUtc { get; set; }

    public bool ShowsTranslation(string language) =>
        TranslatedTitle is not null && TranslatedDescription is not null && SourceLanguage is not null && SourceLanguage != language;

    public string DisplayTitle(string language) => ShowsTranslation(language) ? TranslatedTitle! : Title;

    public void ClearTranslation()
    {
        SourceLanguage = null;
        TranslatedTitle = null;
        TranslatedDescription = null;
        TranslationAttempts = 0;
        TranslationNextAttemptUtc = null;
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (IsFree && Price != 0)
            yield return new ValidationResult("Free listings must have a zero price.", new[] { nameof(Price) });
    }
}
