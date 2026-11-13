
using AuraCafé.Web.Models;
using AuraCafé.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AuraCafé.Web.Pages.Products;

public class CreateModel : PageModel
{
    private readonly ProductApiService _productApi;

    public CreateModel(ProductApiService productApi)
    {
        _productApi = productApi;
    }

    [BindProperty]
    public ProductFormDto Product { get; set; } = new();

    public List<CategoryDto> Categories { get; set; } = new();

    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        try
        {
            Categories = await _productApi.GetCategoriesAsync();
        }
        catch (HttpRequestException)
        {
            ErrorMessage = "Could not load categories.";
        }
        catch (TaskCanceledException)
        {
            ErrorMessage = "Category request timed out.";
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            await LoadCategoriesAsync();
            return Page();
        }

        try
        {
            await _productApi.CreateAsync(Product);

            TempData["SuccessMessage"] =
                "Product created successfully.";

            return RedirectToPage("./Index");
        }
        catch (HttpRequestException ex)
        {
            ErrorMessage = $"Could not create product: {ex.Message}";
        }
        catch (TaskCanceledException)
        {
            ErrorMessage = "The request timed out. Check the product list before retrying.";
        }

        await LoadCategoriesAsync();
        return Page();
    }

    private async Task LoadCategoriesAsync()
    {
        try
        {
            Categories = await _productApi.GetCategoriesAsync();
        }
        catch (HttpRequestException)
        {
            ErrorMessage ??= "Could not load categories.";
        }
        catch (TaskCanceledException)
        {
            ErrorMessage ??= "Category request timed out.";
        }
    }
}
