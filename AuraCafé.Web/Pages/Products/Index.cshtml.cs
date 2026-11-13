
using AuraCafé.Web.Models;
using AuraCafé.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AuraCafé.Web.Pages.Products;

public class IndexModel : PageModel
{
    private readonly ProductApiService _productApi;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(
        ProductApiService productApi,
        ILogger<IndexModel> logger)
    {
        _productApi = productApi;
        _logger = logger;
    }

    public List<ProductDto> Products { get; set; } = new();

    public string? ErrorMessage { get; set; }

    [TempData]
    public string? SuccessMessage { get; set; }

    public async Task OnGetAsync()
    {
        try
        {
            Products = await _productApi.GetAllAsync();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to load products");
            ErrorMessage = "Unable to load products. Check that the API is running.";
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Product request timed out");
            ErrorMessage = "The API request timed out.";
        }
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        try
        {
            await _productApi.DeleteAsync(id);
            SuccessMessage = "Product deleted successfully.";
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to delete product {Id}", id);
            TempData["ErrorMessage"] = "Unable to delete product.";
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Delete request timed out for {Id}", id);
            TempData["ErrorMessage"] = "Delete request timed out.";
        }

        return RedirectToPage();
    }
}
