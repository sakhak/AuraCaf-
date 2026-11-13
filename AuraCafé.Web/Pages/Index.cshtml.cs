using AuraCafé.Web.Models;
using AuraCafé.Web.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AuraCafé.Web.Pages;

public class IndexModel : PageModel
{
    private readonly CafeApiService _cafeApiService;
    private readonly ILogger<IndexModel> _logger;

    public CafeInfo? Cafe { get; private set; }

    // This property fixes your error
    public string? ErrorMessage { get; private set; }

    public IndexModel(
        CafeApiService cafeApiService,
        ILogger<IndexModel> logger)
    {
        _cafeApiService = cafeApiService;
        _logger = logger;
    }

    public async Task OnGetAsync()
    {
        try
        {
            Cafe = await _cafeApiService.GetCafeInfoAsync();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(
                ex,
                "Failed to connect to Aura Café API.");

            ErrorMessage = "Cannot connect to Aura Café API.";
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(
                ex,
                "Aura Café API request timed out.");

            ErrorMessage = "API request timed out.";
        }
    }
}