using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AuraCafé.Web.Models;

namespace AuraCafé.Web.Services;

public class ReportingApiService(HttpClient http)
{
    public async Task<SalesReportDto> GetSalesAsync(DateOnly from, DateOnly to)
    {
        using var response = await http.GetAsync($"api/reports/sales?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}");
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<SalesReportDto>() ?? throw new HttpRequestException("The reporting response was empty.");
    }

    public async Task<ShiftCashDto?> GetShiftAsync(int id)
    {
        using var response = await http.GetAsync($"api/shifts/{id}/reconciliation");
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<ShiftCashDto>();
    }

    public async Task CloseShiftAsync(int id, long countedCashCents, string note, long? countedCashRiel = null)
    {
        using var response = await http.PostAsJsonAsync($"api/shifts/{id}/reconciliation", new { CountedCashCents = countedCashCents, CountedCashRiel = countedCashRiel, Note = note });
        await EnsureSuccessAsync(response);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;
        var message = "The reporting request could not be completed. Check your session and connection.";
        if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict)
        {
            try
            {
                using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                if (json.RootElement.TryGetProperty("message", out var value)) message = value.GetString() ?? message;
            }
            catch (JsonException) { }
        }
        throw new HttpRequestException(message, null, response.StatusCode);
    }
}
