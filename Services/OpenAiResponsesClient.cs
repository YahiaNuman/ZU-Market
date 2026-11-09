using System.Net.Http.Headers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace ZuMarket.Services;

public sealed class AiAssistOptions
{
    public const string SectionName = "AiAssist";

    public bool Enabled { get; set; }
    public string Endpoint { get; set; } = "https://api.openai.com/v1/responses";
    public string DraftModel { get; set; } = "gpt-6-luna";
    public string TranslationModel { get; set; } = "gpt-6-luna";
    public string SummaryModel { get; set; } = "gpt-6-luna";
    public int TimeoutSeconds { get; set; } = 45;
    public int DraftDailyLimit { get; set; } = 20;
}

public abstract record AiInput;
public sealed record AiTextInput(string Text) : AiInput;
public sealed record AiImageInput(string ContentType, byte[] Content) : AiInput;
public sealed record AiStructuredRequest(string Feature, string Model, string Instructions, IReadOnlyList<AiInput> Input, string SchemaName, object Schema, int MaxOutputTokens);

public interface IAiResponsesClient
{
    bool IsConfigured { get; }
    Task<T?> CompleteAsync<T>(AiStructuredRequest request, CancellationToken cancellationToken = default) where T : class;
}

public static class AiSchema
{
    public static object Text { get; } = new Dictionary<string, object> { ["type"] = "string" };
    public static object Boolean { get; } = new Dictionary<string, object> { ["type"] = "boolean" };
    public static object OneOf(params string[] values) => new Dictionary<string, object> { ["type"] = "string", ["enum"] = values };
    public static object Object(params (string Name, object Schema)[] properties) => new Dictionary<string, object>
    {
        ["type"] = "object",
        ["properties"] = properties.ToDictionary(x => x.Name, x => x.Schema),
        ["required"] = properties.Select(x => x.Name).ToArray(),
        ["additionalProperties"] = false
    };
}

public static class AiPrompt
{
    private static readonly JsonSerializerOptions Options = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    // User-written text is sent as JSON so quoting keeps it from breaking out of its data block.
    public static string AsData(object value) => JsonSerializer.Serialize(value, Options);
    public static string LanguageName(string twoLetterLanguage) => twoLetterLanguage == "ar" ? "Arabic" : "English";
}

public sealed class OpenAiResponsesClient : IAiResponsesClient
{
    private static readonly JsonSerializerOptions ResultOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower, PropertyNameCaseInsensitive = true };
    private readonly HttpClient _http;
    private readonly AiAssistOptions _options;
    private readonly string? _apiKey;
    private readonly ILogger<OpenAiResponsesClient> _logger;

    public OpenAiResponsesClient(HttpClient http, IOptions<AiAssistOptions> options, IOptions<OpenAiOptions> openAi, ILogger<OpenAiResponsesClient> logger)
    {
        _http = http;
        _options = options.Value;
        _apiKey = openAi.Value.ApiKey;
        _logger = logger;
        _http.Timeout = TimeSpan.FromSeconds(Math.Clamp(_options.TimeoutSeconds, 5, 120));
    }

    public bool IsConfigured =>
        _options.Enabled &&
        !string.IsNullOrWhiteSpace(_apiKey) &&
        Uri.TryCreate(_options.Endpoint, UriKind.Absolute, out var endpoint) &&
        (endpoint.Scheme == Uri.UriSchemeHttps || endpoint.IsLoopback);

    public async Task<T?> CompleteAsync<T>(AiStructuredRequest request, CancellationToken cancellationToken = default) where T : class
    {
        if (!IsConfigured) return null;

        var body = new
        {
            model = request.Model,
            store = false,
            reasoning = new { effort = "low" },
            max_output_tokens = request.MaxOutputTokens,
            instructions = request.Instructions,
            input = new[] { new { role = "user", content = request.Input.Select(ToContent).ToArray() } },
            text = new { format = new { type = "json_schema", name = request.SchemaName, strict = true, schema = request.Schema } }
        };

        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Post, _options.Endpoint);
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            message.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
            using var response = await _http.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("AI request for {Feature} returned HTTP {StatusCode}", request.Feature, (int)response.StatusCode);
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var text = ExtractText(document.RootElement);
            if (text is null)
            {
                _logger.LogWarning("AI request for {Feature} returned no usable output", request.Feature);
                return null;
            }
            return JsonSerializer.Deserialize<T>(text, ResultOptions);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("AI request for {Feature} timed out", request.Feature);
            return null;
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException)
        {
            _logger.LogWarning("AI request for {Feature} failed. Error type: {ErrorType}", request.Feature, exception.GetType().Name);
            return null;
        }
    }

    private static object ToContent(AiInput input) => input switch
    {
        AiTextInput text => new { type = "input_text", text = text.Text },
        AiImageInput image => new { type = "input_image", image_url = $"data:{image.ContentType};base64,{Convert.ToBase64String(image.Content)}" },
        _ => throw new ArgumentOutOfRangeException(nameof(input))
    };

    // Refusals and truncated responses do not follow the schema, so they count as no result.
    private static string? ExtractText(JsonElement root)
    {
        if (!root.TryGetProperty("status", out var status) || status.GetString() != "completed") return null;
        if (!root.TryGetProperty("output", out var output) || output.ValueKind != JsonValueKind.Array) return null;
        foreach (var item in output.EnumerateArray())
        {
            if (!item.TryGetProperty("type", out var type) || type.GetString() != "message") continue;
            if (!item.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array) continue;
            foreach (var part in content.EnumerateArray())
            {
                if (part.TryGetProperty("type", out var partType) && partType.GetString() == "output_text" && part.TryGetProperty("text", out var text))
                    return text.GetString();
            }
        }
        return null;
    }
}
