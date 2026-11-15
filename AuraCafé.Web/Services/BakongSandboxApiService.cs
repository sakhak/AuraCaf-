using AuraCafé.Web.Models;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace AuraCafé.Web.Services;
public class BakongSandboxApiService(HttpClient http)
{
    public async Task<BakongSandboxIntentDto?> GetAsync(int orderId)
    {
        using var response = await http.GetAsync($"api/payments/bakong-sandbox/orders/{orderId}");
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        await EnsureAsync(response);
        return await response.Content.ReadFromJsonAsync<BakongSandboxIntentDto>();
    }
    public Task<BakongSandboxIntentDto> StartAsync(int orderId) => PostAsync<BakongSandboxIntentDto>($"api/payments/bakong-sandbox/orders/{orderId}");
    public Task<BakongSandboxIntentDto> CheckAsync(int orderId) => PostAsync<BakongSandboxIntentDto>($"api/payments/bakong-sandbox/orders/{orderId}/check");
    public Task<BakongSandboxConnectionDto> ConnectionAsync() => PostAsync<BakongSandboxConnectionDto>("api/payments/bakong-sandbox/connection");
    private async Task<T> PostAsync<T>(string path)
    {
        using var response = await http.PostAsync(path, null);
        await EnsureAsync(response);
        return await response.Content.ReadFromJsonAsync<T>() ?? throw new HttpRequestException("The sandbox response was empty.");
    }
    private static async Task EnsureAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;
        var message = "The sandbox request could not be confirmed. No real payment has been recorded.";
        if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict or HttpStatusCode.ServiceUnavailable)
        {
            try
            {
                using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                if (body.RootElement.TryGetProperty("message", out var text)) message = text.GetString() ?? message;
            }
            catch (JsonException) { }
        }
        throw new HttpRequestException(message, null, response.StatusCode);
    }
}
