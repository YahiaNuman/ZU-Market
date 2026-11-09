namespace ZuMarket.Models;

public class Favorite
{
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }
    public int ListingId { get; set; }
    public Listing? Listing { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
