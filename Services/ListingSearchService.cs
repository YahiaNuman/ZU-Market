using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using ZuMarket.Models;

namespace ZuMarket.Services;

public interface IListingSearchService
{
    Task<ListingFilterViewModel> SearchAsync(ListingFilterViewModel filter, CancellationToken cancellationToken = default);
}

public class ListingSearchService : IListingSearchService
{
    private readonly ApplicationDbContext _db;
    private const int PageSize = 12;

    public ListingSearchService(ApplicationDbContext db) { _db = db; }

    public async Task<ListingFilterViewModel> SearchAsync(ListingFilterViewModel filter, CancellationToken cancellationToken = default)
    {
        var query = _db.Listings.AsNoTracking()
            .Include(x => x.Category).Include(x => x.Images).Include(x => x.Seller)
            .Where(x => x.ModerationStatus == ListingModerationStatus.Approved && x.AvailabilityStatus != ListingAvailabilityStatus.Removed);

        if (!string.IsNullOrWhiteSpace(filter.Query))
        {
            var normalized = NormalizeArabic(filter.Query);
            if (normalized.Contains("مجاني") || normalized.Contains("خيري")) filter.MaxPrice = 0;
            var price = Regex.Match(normalized, @"(?:اقل|تحت|دون)\s*(?:من)?\s*(\d+(?:\.\d+)?)");
            if (price.Success && decimal.TryParse(price.Groups[1].Value, out var max)) filter.MaxPrice = max;
            var stopWords = new HashSet<string> { "بدي", "غرض", "عن", "مجاني", "خيري", "اقل", "تحت", "دون", "من", "دينار" };
            var terms = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(x => x.Length > 1 && !stopWords.Contains(x) && !decimal.TryParse(x, out _)).Take(6).ToArray();
            foreach (var term in terms)
            {
                var pattern = ToLikePattern(term);
                query = query.Where(x => EF.Functions.Like(x.Title, pattern) || EF.Functions.Like(x.Description, pattern)
                    || EF.Functions.Like(x.TranslatedTitle!, pattern) || EF.Functions.Like(x.TranslatedDescription!, pattern));
            }
        }

        if (filter.CategoryId.HasValue) query = query.Where(x => x.CategoryId == filter.CategoryId);
        if (filter.Condition.HasValue) query = query.Where(x => x.Condition == filter.Condition);
        if (filter.Availability.HasValue) query = query.Where(x => x.AvailabilityStatus == filter.Availability);
        if (filter.MinPrice.HasValue) query = query.Where(x => x.Price >= filter.MinPrice);
        if (filter.MaxPrice.HasValue) query = query.Where(x => x.Price <= filter.MaxPrice);
        query = filter.Sort switch
        {
            "price-asc" => query.OrderBy(x => x.Price),
            "price-desc" => query.OrderByDescending(x => x.Price),
            _ => query.OrderByDescending(x => x.CreatedAtUtc)
        };

        var count = await query.CountAsync(cancellationToken);
        filter.Page = Math.Max(1, filter.Page);
        filter.TotalPages = Math.Max(1, (int)Math.Ceiling(count / (double)PageSize));
        filter.Page = Math.Min(filter.Page, filter.TotalPages);
        filter.Items = await query.Skip((filter.Page - 1) * PageSize).Take(PageSize).ToListAsync(cancellationToken);
        filter.Categories = await _db.Categories.AsNoTracking().OrderBy(x => x.SortOrder).ToListAsync(cancellationToken);
        return filter;
    }

    // Search terms are normalized but stored text is not, so each normalized letter must match its stored spellings.
    private static string ToLikePattern(string term)
    {
        var pattern = new StringBuilder("%");
        foreach (var character in term)
        {
            pattern.Append(character switch
            {
                'ا' => "[اأإآ]",
                'ي' => "[يى]",
                'ه' => "[هة]",
                '%' or '_' or '[' => $"[{character}]",
                _ => character.ToString()
            });
        }
        return pattern.Append('%').ToString();
    }

    private static string NormalizeArabic(string value)
    {
        var normalized = value.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var result = new StringBuilder();
        foreach (var character in normalized)
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark) result.Append(character);
        return result.ToString().Replace('\u0623', '\u0627').Replace('\u0625', '\u0627').Replace('\u0622', '\u0627').Replace('\u0649', '\u064A').Replace('\u0629', '\u0647');
    }
}


