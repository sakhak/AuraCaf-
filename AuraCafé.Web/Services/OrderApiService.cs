
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AuraCafé.Web.Models;

namespace AuraCafé.Web.Services;

public class OrderApiService
{
    private readonly HttpClient _http;

    public OrderApiService(HttpClient http)
    {
        _http = http;
    }

    public async Task<PosOrderResponseDto> CreateAsync(
        PosCreateOrderDto order)
    {
        using var response =
            await _http.PostAsJsonAsync("api/orders", order);

        await EnsureSuccessAsync(response);

        return await response.Content
            .ReadFromJsonAsync<PosOrderResponseDto>()
            ?? throw new HttpRequestException(
                "The API returned an empty order.");
    }

    public async Task<List<PosOrderResponseDto>> GetMyOrdersAsync()
    {
        return await _http
            .GetFromJsonAsync<List<PosOrderResponseDto>>(
                "api/orders/my") ?? new();
    }

    public async Task<PosOrderResponseDto?> GetByIdAsync(int id)
    {
        using var response = await _http.GetAsync($"api/orders/{id}");
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<PosOrderResponseDto>();
    }

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
            return;

        var message = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized =>
                "Your login session has expired.",

            HttpStatusCode.Forbidden =>
                "You do not have permission to create orders.",

            HttpStatusCode.Conflict =>
                "Check that your cashier account is active " +
                "and you have clocked in.",

            HttpStatusCode.BadRequest =>
                "Invalid order. Check products and quantities.",

            _ => "Unable to create the order."
        };

        if (response.StatusCode is HttpStatusCode.BadRequest
            or HttpStatusCode.Conflict)
        {
            try
            {
                using var json = JsonDocument.Parse(
                    await response.Content.ReadAsStringAsync());

                if (json.RootElement.TryGetProperty(
                    "message", out var property))
                {
                    message = property.GetString() ?? message;
                }
            }
            catch (JsonException)
            {
                // Keep the safe default message.
            }
        }

        throw new HttpRequestException(
            message, null, response.StatusCode);
    }
}
