using System.ComponentModel.DataAnnotations;

namespace ZuMarket.Models;

public class ListingReport
{
    public int Id { get; set; }
    public int ListingId { get; set; }
    public Listing? Listing { get; set; }
    [Required]
    public string ReporterId { get; set; } = string.Empty;
    public ApplicationUser? Reporter { get; set; }
    public ReportReason Reason { get; set; }
    [StringLength(1000)]
    public string? Details { get; set; }
    public ReportStatus Status { get; set; } = ReportStatus.Pending;
    [StringLength(1000)]
    public string? ResolutionNotes { get; set; }
    public string? ResolvedById { get; set; }
    public ApplicationUser? ResolvedBy { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAtUtc { get; set; }
}
