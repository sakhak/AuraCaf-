using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AuraCafé.Web.Models;

namespace AuraCafé.Web.Services;

public class InventoryApiService(HttpClient http)
{
    public Task<InventorySummaryDto?> SummaryAsync() => GetAsync<InventorySummaryDto>("api/inventory/summary");
    public Task<IngredientDto?> IngredientAsync(int id) => GetAsync<IngredientDto>($"api/inventory/ingredients/{id}");
    public Task<ProductRecipeDto?> RecipeAsync(int id) => GetAsync<ProductRecipeDto>($"api/inventory/products/{id}/recipe");
    public Task<MovementPageDto?> MovementsAsync(int? ingredientId, int page) => GetAsync<MovementPageDto>($"api/inventory/movements?page={page}" + (ingredientId.HasValue ? $"&ingredientId={ingredientId.Value}" : ""));
    public Task<IngredientPageDto?> ListAsync(string? search, bool? active, bool lowOnly, int page)
    {
        var url = $"api/inventory/ingredients?page={page}&lowOnly={lowOnly.ToString().ToLowerInvariant()}&search={Uri.EscapeDataString(search ?? "")}";
        if (active.HasValue) url += "&active=" + active.Value.ToString().ToLowerInvariant();
        return GetAsync<IngredientPageDto>(url);
    }
    public async Task SaveIngredientAsync(int? id, CreateIngredientDto request)
    {
        using var response = id.HasValue
            ? await http.PutAsJsonAsync($"api/inventory/ingredients/{id.Value}", new { request.Name, request.MinimumQuantity, request.IsActive })
            : await http.PostAsJsonAsync("api/inventory/ingredients", request);
        await EnsureSuccessAsync(response);
    }
    public async Task DeactivateAsync(int id)
    {
        using var response = await http.DeleteAsync($"api/inventory/ingredients/{id}");
        await EnsureSuccessAsync(response);
    }
    public async Task<StockMovementDto> MoveAsync(int id, CreateMovementDto request)
    {
        using var response = await http.PostAsJsonAsync($"api/inventory/ingredients/{id}/movements", request);
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<StockMovementDto>() ?? throw new HttpRequestException("The stock transaction result was empty. Check the ledger before retrying.");
    }
    public async Task SaveRecipeAsync(int id, SaveRecipeDto request)
    {
        using var response = await http.PutAsJsonAsync($"api/inventory/products/{id}/recipe", request);
        await EnsureSuccessAsync(response);
    }
    private async Task<T?> GetAsync<T>(string url)
    {
        using var response = await http.GetAsync(url);
        if (response.StatusCode == HttpStatusCode.NotFound) return default;
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<T>();
    }
    private static async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;
        var message = "Inventory request failed. Check your Admin session and API connection.";
        if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict)
        {
            try
            {
                using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                if (json.RootElement.TryGetProperty("message", out var value)) message = value.GetString() ?? message;
                else if (json.RootElement.TryGetProperty("errors", out _)) message = "Invalid inventory input. Use whole quantities and check all required fields.";
            }
            catch (JsonException) { }
        }
        throw new HttpRequestException(message, null, response.StatusCode);
    }
}
