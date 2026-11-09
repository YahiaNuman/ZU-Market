using System.ComponentModel.DataAnnotations;

namespace ZuMarket.Models;

public class ListingReportSummary
{
    public int Id { get; set; }
    public int ListingId { get; set; }
    public Listing? Listing { get; set; }
    [Required, StringLength(1500)]
    public string Summary { get; set; } = string.Empty;
    public ReportSuggestedAction SuggestedAction { get; set; }
    [Required, StringLength(600)]
    public string Reason { get; set; } = string.Empty;
    public int ReportCount { get; set; }
    public int LatestReportId { get; set; }
    [Required, StringLength(120)]
    public string Model { get; set; } = string.Empty;
    [Required]
    public string CreatedById { get; set; } = string.Empty;
    public ApplicationUser? CreatedBy { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public bool IsCurrentFor(IReadOnlyCollection<ListingReport> pendingReports) =>
        pendingReports.Count == ReportCount && pendingReports.Max(x => x.Id) == LatestReportId;
}
