
using System.Net.Http.Json;
using System.Net;
using AuraCafé.Web.Models;

namespace AuraCafé.Web.Services;

public class ShiftApiService
{
    private readonly HttpClient _http;

    public ShiftApiService(HttpClient http)
    {
        _http = http;
    }

    public async Task<ShiftDto?> GetCurrentAsync()
    {
        using var response = await _http.GetAsync(
            "api/shifts/my/current");

        if (response.StatusCode == HttpStatusCode.NoContent)
            return null;

        response.EnsureSuccessStatusCode();

        return await response.Content
            .ReadFromJsonAsync<ShiftDto>();
    }

    public async Task<List<ShiftDto>> GetMyHistoryAsync()
    {
        return await _http.GetFromJsonAsync<List<ShiftDto>>(
            "api/shifts/my/history") ?? new();
    }

    public async Task<List<ShiftDto>> GetAllAsync()
    {
        return await _http.GetFromJsonAsync<List<ShiftDto>>(
            "api/shifts") ?? new();
    }

    public async Task ClockInAsync(int? openingCashCents = null, long? openingCashRiel = null)
    {
        using var response = openingCashCents.HasValue
            ? await _http.PostAsJsonAsync("api/shifts/clock-in", new { OpeningCashCents = openingCashCents.Value, OpeningCashRiel = openingCashRiel })
            : await _http.PostAsync("api/shifts/clock-in", content: null);

        await EnsureSuccessAsync(response);
    }

    public async Task ClockOutAsync()
    {
        using var response = await _http.PostAsync(
            "api/shifts/clock-out", content: null);

        await EnsureSuccessAsync(response);
    }

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
            return;

        var message = response.StatusCode switch
        {
            System.Net.HttpStatusCode.Unauthorized =>
                "Your login session has expired.",

            System.Net.HttpStatusCode.Forbidden =>
                "You do not have permission.",

            System.Net.HttpStatusCode.Conflict =>
                "The shift operation conflicts with " +
                "your current shift status.",

            _ => "The shift request failed."
        };

        throw new HttpRequestException(message);
    }
}
