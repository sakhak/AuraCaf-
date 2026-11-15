using System.Net.Http.Json;
using AuraCafé.Web.Models;

namespace AuraCafé.Web.Services;

public class AuthApiService
{
    private readonly HttpClient _httpClient;

    public AuthApiService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<LoginResult> LoginAsync(
        string email,
        string password)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            "api/auth/login",
            new { Email = email, Password = password });

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException(
                $"API returned {(int)response.StatusCode}: {body}",
                null,
                response.StatusCode);
        }

        return await response.Content.ReadFromJsonAsync<LoginResult>()
            ?? throw new InvalidOperationException(
                "The API returned an empty login response.");
    }
}
