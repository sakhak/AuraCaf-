using System.Net.Http.Json;
using AuraCafé.Web.Models;

namespace AuraCafé.Web.Services;

public class CafeApiService
{
    private readonly HttpClient _httpClient;

    public CafeApiService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<CafeInfo?> GetCafeInfoAsync()
    {
        return await _httpClient
            .GetFromJsonAsync<CafeInfo>("api/cafe/info");
    }
}
