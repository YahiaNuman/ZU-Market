using System.ComponentModel.DataAnnotations;

namespace ZuMarket.Models;

public class Category
{
    public int Id { get; set; }
    [Required, StringLength(40)]
    public string Key { get; set; } = string.Empty;
    [Required, StringLength(40)]
    public string Slug { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public ICollection<Listing> Listings { get; set; } = new List<Listing>();
}
