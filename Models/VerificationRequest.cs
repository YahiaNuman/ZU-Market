using System.ComponentModel.DataAnnotations;

namespace ZuMarket.Models;

public class VerificationRequest
{
    public int Id { get; set; }
    [Required]
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }
    [Required, StringLength(100)]
    public string StorageFileName { get; set; } = string.Empty;
    [Required, StringLength(255)]
    public string OriginalFileName { get; set; } = string.Empty;
    [Required, StringLength(100)]
    public string ContentType { get; set; } = string.Empty;
    public long Length { get; set; }
    public VerificationStatus Status { get; set; } = VerificationStatus.Pending;
    public DateTime SubmittedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedAtUtc { get; set; }
    public string? ReviewedById { get; set; }
    public ApplicationUser? ReviewedBy { get; set; }
    [StringLength(1000)]
    public string? ReviewNotes { get; set; }
}
