
using System.Net.Http.Json;
using AuraCafé.Web.Models;

namespace AuraCafé.Web.Services;

public class PosProductApiService
{
    private readonly HttpClient _http;

    public PosProductApiService(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<PosProductDto>> GetAvailableAsync()
    {
        var products =
            await _http.GetFromJsonAsync<List<PosProductDto>>(
                "api/products");

        return (products ?? new())
            .Where(p => p.IsAvailable && p.IsActive)
            .OrderBy(p => p.Name)
            .ToList();
    }

    public async Task<List<PosCategoryDto>> GetCategoriesAsync()
    {
        return await _http.GetFromJsonAsync<List<PosCategoryDto>>(
            "api/categories") ?? new();
    }
}
