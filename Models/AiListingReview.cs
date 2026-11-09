using System.ComponentModel.DataAnnotations;

namespace ZuMarket.Models;

public class AiListingReview
{
    public int Id { get; set; }
    public int ListingId { get; set; }
    public Listing? Listing { get; set; }
    public AiReviewDecision Decision { get; set; }
    public decimal Confidence { get; set; }
    [Required]
    public string RuleChecksJson { get; set; } = "[]";
    [Required, StringLength(1000)]
    public string Explanation { get; set; } = string.Empty;
    [Required, StringLength(80)]
    public string Provider { get; set; } = string.Empty;
    [Required, StringLength(120)]
    public string Model { get; set; } = string.Empty;
    public bool FinalDecisionWasAi { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
