
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AuraCafé.Web.Models;

namespace AuraCafé.Web.Services;

public class PaymentApiService
{
    private readonly HttpClient _http;

    public PaymentApiService(HttpClient http)
    {
        _http = http;
    }

    public async Task<PaymentDto> CreateCashAsync(
        CashPaymentRequestDto request)
    {
        using var response = await _http.PostAsJsonAsync(
            "api/payments/cash", request);

        await EnsureSuccessAsync(response);

        return await response.Content
            .ReadFromJsonAsync<PaymentDto>()
            ?? throw new HttpRequestException(
                "The payment response was empty.");
    }

    public async Task<PaymentDto?> GetByOrderIdAsync(
        int orderId)
    {
        using var response = await _http.GetAsync(
            $"api/payments/order/{orderId}");

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        await EnsureSuccessAsync(response);

        return await response.Content
            .ReadFromJsonAsync<PaymentDto>();
    }

    public async Task<List<PaymentDto>> GetAllAsync()
    {
        return await _http.GetFromJsonAsync<List<PaymentDto>>(
            "api/payments") ?? new();
    }

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
            return;

        var message = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized =>
                "Your session has expired.",

            HttpStatusCode.Forbidden =>
                "You do not have permission.",

            HttpStatusCode.NotFound =>
                "Order or payment not found.",

            HttpStatusCode.Conflict =>
                "Payment could not be completed. " +
                "Check the order status before retrying.",

            HttpStatusCode.BadRequest =>
                "Invalid payment amount.",

            _ => "The payment request failed."
        };

        if (response.StatusCode is
            HttpStatusCode.BadRequest or
            HttpStatusCode.Conflict)
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
                // Keep the default message.
            }
        }

        throw new HttpRequestException(
            message, null, response.StatusCode);
    }
}
