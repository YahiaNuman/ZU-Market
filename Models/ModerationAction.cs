using System.ComponentModel.DataAnnotations;

namespace ZuMarket.Models;

public class ModerationAction
{
    public int Id { get; set; }
    public ModerationActionType ActionType { get; set; }
    [Required]
    public string AdministratorId { get; set; } = string.Empty;
    public ApplicationUser? Administrator { get; set; }
    public int? ListingId { get; set; }
    public Listing? Listing { get; set; }
    public string? TargetUserId { get; set; }
    public ApplicationUser? TargetUser { get; set; }
    [StringLength(1000)]
    public string? Notes { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
