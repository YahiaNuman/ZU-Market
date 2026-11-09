using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ZuMarket.Models;

namespace ZuMarket.Services;

public sealed class AiReviewOptions
{
    public const string SectionName = "AiReview";

    public decimal ConfidenceThreshold { get; set; } = 0.90m;
    public List<string> PostingRules { get; set; } = new();
}

public sealed class OpenAiOptions
{
    public const string SectionName = "OpenAI";

    public bool Enabled { get; set; }
    public string? ApiKey { get; set; }
    public string Endpoint { get; set; } = "https://api.openai.com/v1/moderations";
    public string Model { get; set; } = "omni-moderation-latest";
    public int TimeoutSeconds { get; set; } = 45;
}

public sealed record AiReviewImage(string ContentType, byte[] Content);
public sealed record AiReviewInput(int ListingId, string Title, string Description, string Category, string Condition, decimal Price, bool IsFree, IReadOnlyList<AiReviewImage> Images, IReadOnlyList<string> Rules);
public sealed record AiRuleCheck(string Rule, bool Passed, string Evidence);
public sealed record AiProviderReview(AiReviewDecision Decision, decimal Confidence, IReadOnlyList<AiRuleCheck> RuleChecks, string Explanation, string Provider, string Model);
public interface IAiListingReviewProvider
{
    bool IsEnabled { get; }
    bool HasApiKey { get; }
    bool HasRequiredSettings { get; }
    bool IsConfigured { get; }
    decimal ConfidenceThreshold { get; }
    Task<AiProviderAvailability> CheckAvailabilityAsync(CancellationToken cancellationToken = default);
    Task<AiProviderReview> ReviewAsync(AiReviewInput input, CancellationToken cancellationToken = default);
}

public interface IListingReviewOrchestrator
{
    Task ReviewAsync(int listingId, CancellationToken cancellationToken = default);
}

public sealed class OpenAiListingReviewProvider : IAiListingReviewProvider
{
    private readonly HttpClient _http;
    private readonly OpenAiOptions _providerOptions;
    private readonly AiReviewOptions _reviewOptions;
    private readonly ILogger<OpenAiListingReviewProvider> _logger;

    public OpenAiListingReviewProvider(
        HttpClient http,
        IOptions<OpenAiOptions> providerOptions,
        IOptions<AiReviewOptions> reviewOptions,
        ILogger<OpenAiListingReviewProvider> logger)
    {
        _http = http;
        _providerOptions = providerOptions.Value;
        _reviewOptions = reviewOptions.Value;
        _logger = logger;
        _http.Timeout = TimeSpan.FromSeconds(Math.Clamp(_providerOptions.TimeoutSeconds, 5, 120));
    }

    public bool IsEnabled => _providerOptions.Enabled;
    public bool HasApiKey => !string.IsNullOrWhiteSpace(_providerOptions.ApiKey);
    public bool HasRequiredSettings =>
        !string.IsNullOrWhiteSpace(_providerOptions.Model) &&
        Uri.TryCreate(_providerOptions.Endpoint, UriKind.Absolute, out var endpoint) &&
        endpoint.Scheme == Uri.UriSchemeHttps;
    public bool IsConfigured => IsEnabled && HasApiKey && HasRequiredSettings;
    public decimal ConfidenceThreshold => Math.Clamp(_reviewOptions.ConfidenceThreshold, 0.50m, 1m);

    public async Task<AiProviderAvailability> CheckAvailabilityAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Checking OpenAI availability. Enabled: {Enabled}; API key configured: {ApiKeyConfigured}; required settings configured: {RequiredSettingsConfigured}",
            IsEnabled,
            HasApiKey,
            HasRequiredSettings);

        if (!IsEnabled) return AiProviderAvailability.Disabled;
        if (!HasApiKey) return AiProviderAvailability.MissingApiKey;
        if (!HasRequiredSettings) return AiProviderAvailability.InvalidConfiguration;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, _providerOptions.Endpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _providerOptions.ApiKey);
            request.Content = new StringContent(JsonSerializer.Serialize(new
            {
                model = _providerOptions.Model,
                input = "Harmless synthetic ZuMarket provider readiness check."
            }), Encoding.UTF8, "application/json");
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(_providerOptions.TimeoutSeconds, 5, 15)));
            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            _logger.LogInformation("OpenAI availability check returned HTTP {StatusCode}", (int)response.StatusCode);
            if (response.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
                return AiProviderAvailability.AuthenticationFailed;
            if (!response.IsSuccessStatusCode) return AiProviderAvailability.ProviderError;
            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: timeout.Token);
            if (!document.RootElement.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array || results.GetArrayLength() != 1)
                return AiProviderAvailability.InvalidResponse;
            var result = results[0];
            return result.TryGetProperty("flagged", out var flagged) && flagged.ValueKind is JsonValueKind.True or JsonValueKind.False &&
                result.TryGetProperty("categories", out var categories) && categories.ValueKind == JsonValueKind.Object &&
                result.TryGetProperty("category_scores", out var scores) && scores.ValueKind == JsonValueKind.Object
                ? AiProviderAvailability.Ready
                : AiProviderAvailability.InvalidResponse;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("OpenAI availability check timed out");
            return AiProviderAvailability.ConnectionFailed;
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning("OpenAI availability check could not connect. Error type: {ErrorType}", exception.GetType().Name);
            return AiProviderAvailability.ConnectionFailed;
        }
        catch (JsonException exception)
        {
            _logger.LogWarning("OpenAI availability check returned invalid JSON. Error type: {ErrorType}", exception.GetType().Name);
            return AiProviderAvailability.InvalidResponse;
        }
        catch (Exception exception)
        {
            _logger.LogWarning("OpenAI availability check failed safely. Error type: {ErrorType}", exception.GetType().Name);
            return AiProviderAvailability.ConnectionFailed;
        }
    }

    public async Task<AiProviderReview> ReviewAsync(AiReviewInput input, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured) return Unavailable("The AI review provider is not configured.");

        var moderationInput = new List<object>
        {
            new { type = "text", text = $"Title: {input.Title}\nDescription: {input.Description}" }
        };
        moderationInput.AddRange(input.Images.Select(image => (object)new
        {
            type = "image_url",
            image_url = new { url = $"data:{image.ContentType};base64,{Convert.ToBase64String(image.Content)}" }
        }));
        var requestBody = new { model = _providerOptions.Model, input = moderationInput };

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, _providerOptions.Endpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _providerOptions.ApiKey);
            request.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            _logger.LogInformation("OpenAI listing review returned HTTP {StatusCode}", (int)response.StatusCode);
            if (!response.IsSuccessStatusCode) return Unavailable($"The AI provider returned HTTP {(int)response.StatusCode}.");
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            if (!document.RootElement.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array || results.GetArrayLength() != 1)
                return Invalid("The moderation service returned an invalid result.");
            var moderation = results[0];
            if (!moderation.TryGetProperty("flagged", out var flagged) || flagged.ValueKind is not (JsonValueKind.True or JsonValueKind.False) ||
                !moderation.TryGetProperty("categories", out var categories) || categories.ValueKind != JsonValueKind.Object ||
                !moderation.TryGetProperty("category_scores", out var scores) || scores.ValueKind != JsonValueKind.Object)
                return Invalid("The moderation service returned an incomplete result.");
            var isFlagged = flagged.GetBoolean();
            var categoryFlags = categories.EnumerateObject().Where(x => x.Value.ValueKind == JsonValueKind.True).Select(x => x.Name).ToArray();
            var maxScore = scores.EnumerateObject().Where(x => x.Value.ValueKind == JsonValueKind.Number).Select(x => x.Value.GetDecimal()).DefaultIfEmpty(0m).Max();
            var explanation = isFlagged
                ? $"OpenAI moderation flagged content in: {string.Join(", ", categoryFlags)}. Admin review is required."
                : "OpenAI moderation did not flag the listing text or images.";
            return new AiProviderReview(isFlagged ? AiReviewDecision.NeedsManualReview : AiReviewDecision.Approved, 1m - maxScore, Array.Empty<AiRuleCheck>(), explanation, "OpenAI", _providerOptions.Model);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("OpenAI listing review timed out");
            return Unavailable("The AI provider timed out.");
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning("OpenAI listing review could not connect. Error type: {ErrorType}", exception.GetType().Name);
            return Unavailable("The AI provider could not be reached.");
        }
        catch (JsonException exception)
        {
            _logger.LogWarning("OpenAI listing review returned invalid JSON. Error type: {ErrorType}", exception.GetType().Name);
            return Invalid("The AI provider returned invalid JSON.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning("OpenAI listing review failed safely. Error type: {ErrorType}", exception.GetType().Name);
            return Unavailable("The AI provider could not complete the review. The listing remains pending for manual review.");
        }
    }

    private AiProviderReview Unavailable(string explanation) => new(AiReviewDecision.ProviderUnavailable, 0, Array.Empty<AiRuleCheck>(), explanation, "OpenAI", _providerOptions.Model);
    private AiProviderReview Invalid(string explanation) => new(AiReviewDecision.InvalidResponse, 0, Array.Empty<AiRuleCheck>(), explanation, "OpenAI", _providerOptions.Model);

}

public sealed class ListingReviewOrchestrator : IListingReviewOrchestrator
{
    private readonly ApplicationDbContext _db;
    private readonly IPrivateFileStorage _files;
    private readonly IAiListingReviewProvider _provider;
    private readonly AiReviewOptions _options;

    public ListingReviewOrchestrator(ApplicationDbContext db, IPrivateFileStorage files, IAiListingReviewProvider provider, IOptions<AiReviewOptions> options)
    {
        _db = db;
        _files = files;
        _provider = provider;
        _options = options.Value;
    }

    public async Task ReviewAsync(int listingId, CancellationToken cancellationToken = default)
    {
        var listing = await _db.Listings.Include(x => x.Category).Include(x => x.Images).FirstOrDefaultAsync(x => x.Id == listingId, cancellationToken);
        if (listing is null || listing.ModerationStatus != ListingModerationStatus.Pending) return;

        var localChecks = ValidateLocally(listing);
        AiProviderReview result;
        if (_options.PostingRules.Count == 0)
        {
            result = new AiProviderReview(AiReviewDecision.NeedsManualReview, 0, localChecks, "No explicit posting rules are configured. The listing remains pending for manual review.", "LocalRules", string.Empty);
        }
        else if (localChecks.Any(x => !x.Passed))
        {
            result = new AiProviderReview(AiReviewDecision.NeedsManualReview, 0, localChecks, "The listing requires manual review because one or more deterministic checks failed.", "LocalRules", string.Empty);
        }
        else if (!_provider.IsConfigured)
        {
            result = new AiProviderReview(AiReviewDecision.ProviderUnavailable, 0, localChecks, "The AI review provider is not configured. The listing remains pending for manual review.", "OpenAI", string.Empty);
        }
        else
        {
            var images = new List<AiReviewImage>();
            foreach (var image in listing.Images.OrderBy(x => x.SortOrder))
            {
                var stored = await _files.OpenAsync(image.StorageFileName, "listings", cancellationToken);
                if (stored is null) continue;
                await using var stream = stored.Value.Stream;
                using var memory = new MemoryStream();
                await stream.CopyToAsync(memory, cancellationToken);
                images.Add(new AiReviewImage(stored.Value.ContentType, memory.ToArray()));
            }
            var input = new AiReviewInput(listing.Id, listing.Title, listing.Description, listing.Category!.Key, listing.Condition.ToString(), listing.Price, listing.IsFree, images, _options.PostingRules);
            result = images.Count == 0
                ? new AiProviderReview(AiReviewDecision.NeedsManualReview, 0, localChecks, "Listing images could not be loaded. The listing remains pending for manual review.", "OpenAI", string.Empty)
                : await _provider.ReviewAsync(input, cancellationToken);
        }

        result = result with { RuleChecks = localChecks };
        var canApprove = result.Decision == AiReviewDecision.Approved && result.Confidence >= _provider.ConfidenceThreshold && localChecks.All(x => x.Passed);
        listing.ReviewState = canApprove ? ListingReviewState.AiApproved : ListingReviewState.NeedsManualReview;
        if (canApprove) listing.ModerationStatus = ListingModerationStatus.Approved;
        listing.UpdatedAtUtc = DateTime.UtcNow;
        _db.AiListingReviews.Add(new AiListingReview
        {
            ListingId = listing.Id,
            Decision = result.Decision,
            Confidence = result.Confidence,
            RuleChecksJson = JsonSerializer.Serialize(result.RuleChecks),
            Explanation = result.Explanation[..Math.Min(result.Explanation.Length, 1000)],
            Provider = string.IsNullOrWhiteSpace(result.Provider) ? "NotConfigured" : result.Provider,
            Model = string.IsNullOrWhiteSpace(result.Model) ? "NotConfigured" : result.Model,
            FinalDecisionWasAi = canApprove
        });
        await _db.SaveChangesAsync(cancellationToken);
    }

    private List<AiRuleCheck> ValidateLocally(Listing listing)
    {
        var checks = new List<AiRuleCheck>();
        foreach (var rule in _options.PostingRules)
        {
            var passed = rule switch
            {
                "RequiredFields" => !string.IsNullOrWhiteSpace(listing.Title) && !string.IsNullOrWhiteSpace(listing.Description),
                "AllowedCategory" => listing.CategoryId is >= 4 and <= 18,
                "ValidPrice" => listing.Price >= 0 && (!listing.IsFree || listing.Price == 0),
                "ListingImageRequired" => listing.Images.Count > 0,
                _ => false
            };
            checks.Add(new AiRuleCheck(rule, passed, passed ? "Deterministic validation passed or requires AI inspection." : "Deterministic validation failed."));
        }
        return checks;
    }
}
