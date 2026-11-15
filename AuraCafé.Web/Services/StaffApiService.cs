
using System.Net;
using System.Net.Http.Json;
using AuraCafé.Web.Models;

namespace AuraCafé.Web.Services;

public class StaffApiService
{
    private readonly HttpClient _http;

    public StaffApiService(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<StaffDto>> GetAllAsync()
    {
        return await _http.GetFromJsonAsync<List<StaffDto>>(
            "api/staff") ?? new();
    }

    public async Task<StaffDto?> GetByIdAsync(int id)
    {
        using var response =
            await _http.GetAsync($"api/staff/{id}");

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();

        return await response.Content
            .ReadFromJsonAsync<StaffDto>();
    }

    public async Task CreateAsync(CreateStaffForm form)
    {
        var request = new
        {
            form.FullName,
            form.Email,
            form.PhoneNumber,
            form.Position,
            form.HireDate,
            form.Password
        };

        using var response =
            await _http.PostAsJsonAsync("api/staff", request);

        await EnsureSuccessAsync(response);
    }

    public async Task UpdateAsync(
        int id, EditStaffForm form)
    {
        var request = new
        {
            form.FullName,
            form.PhoneNumber,
            form.Position,
            form.HireDate
        };

        using var response =
            await _http.PutAsJsonAsync(
                $"api/staff/{id}", request);

        await EnsureSuccessAsync(response);
    }

    public async Task SetStatusAsync(int id, bool active)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Patch,
            $"api/staff/{id}/status")
        {
            Content = JsonContent.Create(
                new { IsActive = active })
        };

        using var response = await _http.SendAsync(request);

        await EnsureSuccessAsync(response);
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
                "You do not have permission.",

            HttpStatusCode.Conflict =>
                "This record conflicts with existing data.",

            _ => "The staff operation failed."
        };

        if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict)
        {
            // Validation messages are intended for the admin UI.
            // Never include passwords or request payloads.
            try
            {
                using var json = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                if (json.RootElement.TryGetProperty("message", out var text)) message = text.GetString() ?? message;
                else if (json.RootElement.TryGetProperty("errors", out var errors))
                    message = string.Join(" ", errors.EnumerateObject().SelectMany(e => e.Value.EnumerateArray()).Select(v => v.GetString()));
            }
            catch (System.Text.Json.JsonException) { message = "Invalid employee details. Check the form and try again."; }
        }

        throw new HttpRequestException(message, null, response.StatusCode);
    }
}
