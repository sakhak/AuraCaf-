using System.Net;
using System.Net.Http.Json;
using AuraCafé.Web.Models;

namespace AuraCafé.Web.Services;

public class ProductApiService
{
    private readonly HttpClient _httpClient;

    public ProductApiService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<ProductDto>> GetAllAsync()
    {
        return await _httpClient
            .GetFromJsonAsync<List<ProductDto>>("api/products")
            ?? [];
    }

    public async Task<ProductPageDto> SearchAsync(string? search, int? categoryId, bool? available, bool? active, int page)
    {
        var url = $"api/products/manage?page={page}&pageSize=12";
        if (!string.IsNullOrWhiteSpace(search)) url += "&search=" + Uri.EscapeDataString(search);
        if (categoryId.HasValue) url += $"&categoryId={categoryId}";
        if (available.HasValue) url += $"&available={available.Value.ToString().ToLowerInvariant()}";
        if (active.HasValue) url += $"&active={active.Value.ToString().ToLowerInvariant()}";
        return await _httpClient.GetFromJsonAsync<ProductPageDto>(url) ?? new();
    }

    public async Task<ProductDto?> GetByIdAsync(int id)
    {
        using var response = await _httpClient.GetAsync($"api/products/{id}");

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<ProductDto>();
    }

    public async Task<List<CategoryDto>> GetCategoriesAsync()
    {
        return await _httpClient
            .GetFromJsonAsync<List<CategoryDto>>("api/categories")
            ?? [];
    }

    public async Task<CategoryPageDto> SearchCategoriesAsync(string? search, int page)
    {
        return await _httpClient.GetFromJsonAsync<CategoryPageDto>($"api/categories/manage?page={page}&search={Uri.EscapeDataString(search ?? "")}") ?? new();
    }

    public async Task<CategoryDto?> GetCategoryAsync(int id)
    {
        using var response = await _httpClient.GetAsync($"api/categories/{id}");
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<CategoryDto>();
    }

    public async Task UpdateCategoryAsync(int id, string name)
    {
        using var response = await _httpClient.PutAsJsonAsync($"api/categories/{id}", new { Name = name.Trim() });
        await EnsureSuccessAsync(response);
    }

    public async Task DeleteCategoryAsync(int id)
    {
        using var response = await _httpClient.DeleteAsync($"api/categories/{id}");
        await EnsureSuccessAsync(response);
    }

    public async Task CreateCategoryAsync(string name)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            "api/categories", new { Name = name.Trim() });

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            throw new InvalidOperationException("A category with this name already exists. Choose another name.");
        }

        await EnsureSuccessAsync(response);
    }

    public async Task CreateAsync(ProductFormDto product)
    {
        var request = new
        {
            product.Name,
            product.Description,
            product.PriceCents,
            product.CategoryId,
            product.ImageUrl,
            product.IsActive,
            product.IsAvailable
        };

        using var response = await _httpClient.PostAsJsonAsync(
            "api/products", request);

        await EnsureSuccessAsync(response);
    }

    public async Task UpdateAsync(int id, ProductFormDto product)
    {
        using var response = await _httpClient.PutAsJsonAsync(
            $"api/products/{id}", product);

        await EnsureSuccessAsync(response);
    }

    public async Task DeleteAsync(int id)
    {
        using var response = await _httpClient.DeleteAsync(
            $"api/products/{id}");

        await EnsureSuccessAsync(response);
    }

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = "The product request failed. Check your entries and permissions.";
        try
        {
            using var json = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            if (json.RootElement.TryGetProperty("message", out var message)) body = message.GetString() ?? body;
        }
        catch (System.Text.Json.JsonException) { }

        throw new HttpRequestException(
            body, null, response.StatusCode);
    }
}
