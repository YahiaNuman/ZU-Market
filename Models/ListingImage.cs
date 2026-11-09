using System.ComponentModel.DataAnnotations;

namespace ZuMarket.Models;

public class ListingImage
{
    public int Id { get; set; }
    public int ListingId { get; set; }
    public Listing? Listing { get; set; }
    [Required, StringLength(100)]
    public string StorageFileName { get; set; } = string.Empty;
    [Required, StringLength(255)]
    public string OriginalFileName { get; set; } = string.Empty;
    [Required, StringLength(100)]
    public string ContentType { get; set; } = string.Empty;
    public long Length { get; set; }
    [Range(0, 9)]
    public int SortOrder { get; set; }
}
