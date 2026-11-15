using System.Text.RegularExpressions;

namespace AuraCafé.Web.Services;

public class ProductImageStore(IWebHostEnvironment environment, IConfiguration configuration)
{
    public const int MaximumBytes = 5 * 1024 * 1024;
    private string DirectoryPath => Path.GetFullPath(configuration["ProductImages:Directory"]
        ?? Path.Combine(environment.ContentRootPath, "App_Data", "product-images"));

    public async Task<string> SaveAsync(IFormFile file, CancellationToken cancellationToken)
    {
        if (file.Length is <= 0 or > MaximumBytes)
            throw new ArgumentException("Choose a JPG, PNG or WebP image no larger than 5 MB.");
        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, cancellationToken);
        var bytes = buffer.ToArray();
        var format = Detect(bytes);
        if (format is null || !string.Equals(file.ContentType, format.Value.Type, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("The selected file must be a JPG, PNG or WebP image. SVG and other file types are not supported.");
        Directory.CreateDirectory(DirectoryPath);
        var name = Guid.NewGuid().ToString("N") + format.Value.Extension;
        await using var destination = new FileStream(Path.Combine(DirectoryPath, name), FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await destination.WriteAsync(bytes, cancellationToken);
        return "/product-images/" + name;
    }

    public (string Path, string Type)? Find(string name)
    {
        if (!Regex.IsMatch(name, @"\A[a-f0-9]{32}\.(jpg|png|webp)\z", RegexOptions.CultureInvariant)) return null;
        var path = Path.Combine(DirectoryPath, name);
        if (!File.Exists(path)) return null;
        var type = Path.GetExtension(name) switch { ".jpg" => "image/jpeg", ".png" => "image/png", _ => "image/webp" };
        return (path, type);
    }

    private static (string Extension, string Type)? Detect(byte[] bytes)
    {
        if (bytes.Length >= 24 && bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })
            && bytes.AsSpan(12, 4).SequenceEqual("IHDR"u8)) return (".png", "image/png");
        if (bytes.Length >= 4 && bytes[0] == 255 && bytes[1] == 216 && bytes[^2] == 255 && bytes[^1] == 217) return (".jpg", "image/jpeg");
        if (bytes.Length >= 12 && bytes.AsSpan(0, 4).SequenceEqual("RIFF"u8) && bytes.AsSpan(8, 4).SequenceEqual("WEBP"u8)) return (".webp", "image/webp");
        return null;
    }
}
