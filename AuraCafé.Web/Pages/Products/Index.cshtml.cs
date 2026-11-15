using AuraCafé.Web.Models;
using AuraCafé.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AuraCafé.Web.Pages.Products;

public class IndexModel(ProductApiService api, ILogger<IndexModel> logger) : PageModel
{
    public List<ProductDto> Products { get; private set; } = [];
    public List<CategoryDto> Categories { get; private set; } = [];
    [BindProperty(SupportsGet = true)] public string? Search { get; set; }
    [BindProperty(SupportsGet = true)] public int? CategoryId { get; set; }
    [BindProperty(SupportsGet = true)] public bool? Available { get; set; }
    [BindProperty(SupportsGet = true)] public bool? Active { get; set; }
    [BindProperty(SupportsGet = true)] public int PageNumber { get; set; } = 1;
    public int TotalCount { get; private set; }
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / 12d));
    public string? ErrorMessage { get; private set; }
    [TempData] public string? SuccessMessage { get; set; }

    public async Task OnGetAsync()
    {
        if (!ModelState.IsValid || PageNumber < 1 || PageNumber > 1000000 || CategoryId <= 0)
        {
            ErrorMessage = "Invalid filters or page number.";
            return;
        }
        try
        {
            Categories = await api.GetCategoriesAsync();
            var result = await api.SearchAsync(Search, CategoryId, Available, Active, PageNumber);
            TotalCount = result.TotalCount;
            if (PageNumber > TotalPages)
            {
                PageNumber = TotalPages;
                result = await api.SearchAsync(Search, CategoryId, Available, Active, PageNumber);
            }
            Products = result.Items;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogError(ex, "Failed to load product management");
            ErrorMessage = "Unable to load products. Check the API connection and try again.";
        }
    }

    public async Task<IActionResult> OnPostDeactivateAsync(int id)
    {
        try
        {
            await api.DeleteAsync(id);
            SuccessMessage = "Product deactivated. Historical orders are preserved.";
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogError(ex, "Failed to deactivate product {Id}", id);
            TempData["ErrorMessage"] = "Could not confirm deactivation. Check the product before retrying.";
        }
        return RedirectToPage("./Index", new { Search, CategoryId, Available, Active, PageNumber });
    }
}
