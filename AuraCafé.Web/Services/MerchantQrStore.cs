using System.Text.Json;

namespace AuraCafé.Web.Services;

public sealed record MerchantQr(string ImageUrl, DateTime UpdatedAtUtc, string UpdatedBy);

// The merchant image is public payment information. Only Admin may change the pointer.
public sealed class MerchantQrStore(ProductImageStore images, IWebHostEnvironment environment, IConfiguration configuration)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private string SettingsPath => Path.Combine(configuration["MerchantQr:Directory"]
        ?? Path.Combine(environment.ContentRootPath, "App_Data", "merchant-qr"), "current.json");

    public async Task<MerchantQr?> GetAsync()
    {
        await gate.WaitAsync();
        try
        {
            if (!File.Exists(SettingsPath)) return null;
            var qr = JsonSerializer.Deserialize<MerchantQr>(await File.ReadAllTextAsync(SettingsPath));
            if (qr is null || !qr.ImageUrl.StartsWith("/product-images/", StringComparison.Ordinal)
                || images.Find(qr.ImageUrl["/product-images/".Length..]) is null) return null;
            return qr;
        }
        catch (JsonException) { return null; }
        catch (IOException) { return null; }
        finally { gate.Release(); }
    }

    public async Task SaveAsync(IFormFile file, string updatedBy, CancellationToken cancellationToken)
    {
        var imageUrl = await images.SaveAsync(file, cancellationToken);
        var qr = new MerchantQr(imageUrl, DateTime.UtcNow, updatedBy);
        await gate.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            var temporary = SettingsPath + ".tmp";
            await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(qr), cancellationToken);
            File.Move(temporary, SettingsPath, overwrite: true);
        }
        finally { gate.Release(); }
    }
}
