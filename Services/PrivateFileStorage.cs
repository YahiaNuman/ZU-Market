namespace ZuMarket.Services;

public record StoredFile(string FileName, string OriginalFileName, string ContentType, long Length);

public interface IPrivateFileStorage
{
    Task<StoredFile> SaveAsync(IFormFile file, string scope, CancellationToken cancellationToken = default);
    Task<(Stream Stream, string ContentType)?> OpenAsync(string fileName, string scope, CancellationToken cancellationToken = default);
    Task DeleteAsync(string fileName, string scope);
}

public class PrivateFileStorage : IPrivateFileStorage
{
    private const long MaxFileSize = 5 * 1024 * 1024;
    private readonly string _root;
    private static readonly Dictionary<string, (string Extension, byte[][] Signatures)> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = (".jpg", new[] { new byte[] { 0xFF, 0xD8, 0xFF } }),
        ["image/png"] = (".png", new[] { new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A } }),
        ["image/webp"] = (".webp", new[] { new byte[] { 0x52, 0x49, 0x46, 0x46 } })
    };

    public PrivateFileStorage(IWebHostEnvironment environment)
    {
        _root = Path.Combine(environment.ContentRootPath, "App_Data", "private");
    }

    public async Task<StoredFile> SaveAsync(IFormFile file, string scope, CancellationToken cancellationToken = default)
    {
        var extension = await ValidateImageAsync(file, cancellationToken);
        await using var input = file.OpenReadStream();

        var safeScope = scope.All(char.IsLetterOrDigit) ? scope : throw new InvalidOperationException("Invalid storage scope.");
        var directory = Path.Combine(_root, safeScope);
        Directory.CreateDirectory(directory);
        var fileName = $"{Guid.NewGuid():N}{extension}";
        var path = Path.Combine(directory, fileName);
        await using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true);
        await input.CopyToAsync(output, cancellationToken);
        return new StoredFile(fileName, Path.GetFileName(file.FileName), file.ContentType, file.Length);
    }

    public static async Task<string> ValidateImageAsync(IFormFile file, CancellationToken cancellationToken = default)
    {
        if (file.Length is <= 0 or > MaxFileSize || !Allowed.TryGetValue(file.ContentType, out var format))
            throw new InvalidOperationException("Invalid image file.");

        await using var input = file.OpenReadStream();
        var header = new byte[12];
        var read = await input.ReadAsync(header.AsMemory(0, header.Length), cancellationToken);
        if (!format.Signatures.Any(signature => read >= signature.Length && header.AsSpan(0, signature.Length).SequenceEqual(signature)))
            throw new InvalidOperationException("Invalid image content.");

        if (file.ContentType.Equals("image/webp", StringComparison.OrdinalIgnoreCase) &&
            (read < 12 || !header.AsSpan(8, 4).SequenceEqual(new byte[] { 0x57, 0x45, 0x42, 0x50 })))
            throw new InvalidOperationException("Invalid WebP image.");

        return format.Extension;
    }

    public Task<(Stream Stream, string ContentType)?> OpenAsync(string fileName, string scope, CancellationToken cancellationToken = default)
    {
        if (fileName != Path.GetFileName(fileName) || !scope.All(char.IsLetterOrDigit))
            return Task.FromResult<(Stream, string)?>(null);
        var path = Path.Combine(_root, scope, fileName);
        if (!File.Exists(path)) return Task.FromResult<(Stream, string)?>(null);
        var contentType = Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".jpg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => "application/octet-stream"
        };
        return Task.FromResult<(Stream, string)?>(new(File.OpenRead(path), contentType));
    }

    public Task DeleteAsync(string fileName, string scope)
    {
        if (fileName == Path.GetFileName(fileName) && scope.All(char.IsLetterOrDigit))
        {
            var path = Path.Combine(_root, scope, fileName);
            if (File.Exists(path)) File.Delete(path);
        }
        return Task.CompletedTask;
    }
}
