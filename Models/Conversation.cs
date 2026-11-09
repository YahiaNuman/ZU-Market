using System.ComponentModel.DataAnnotations;

namespace ZuMarket.Models;

public class Conversation : IValidatableObject
{
    public int Id { get; set; }
    public int ListingId { get; set; }
    public Listing? Listing { get; set; }
    [Required]
    public string BuyerId { get; set; } = string.Empty;
    public ApplicationUser? Buyer { get; set; }
    [Required]
    public string SellerId { get; set; } = string.Empty;
    public ApplicationUser? Seller { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime LastMessageAtUtc { get; set; } = DateTime.UtcNow;
    public ICollection<Message> Messages { get; set; } = new List<Message>();

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (BuyerId == SellerId)
            yield return new ValidationResult("A user cannot start a conversation with themselves.", new[] { nameof(BuyerId) });
    }
}
