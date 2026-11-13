
using AuraCafé.Web.Models;
using AuraCafé.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AuraCafé.Web.Pages.Products;

public class EditModel : PageModel
{
    private readonly ProductApiService _productApi;

    public EditModel(ProductApiService productApi)
    {
        _productApi = productApi;
    }

    [BindProperty]
    public ProductFormDto Product { get; set; } = new();

    public int ProductId { get; set; }

    public List<CategoryDto> Categories { get; set; } = new();

    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        try
        {
            var existing = await _productApi.GetByIdAsync(id);

            if (existing == null)
                return NotFound();

            ProductId = id;

            Product = new ProductFormDto
            {
                Name = existing.Name,
                Description = existing.Description,
                PriceCents = existing.PriceCents,
                CategoryId = existing.CategoryId,
                IsAvailable = existing.IsAvailable
            };

            Categories = await _productApi.GetCategoriesAsync();
        }
        catch (HttpRequestException)
        {
            ErrorMessage = "Unable to load product information.";
        }
        catch (TaskCanceledException)
        {
            ErrorMessage = "The API request timed out.";
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        ProductId = id;

        if (!ModelState.IsValid)
        {
            await LoadCategoriesAsync();
            return Page();
        }

        try
        {
            await _productApi.UpdateAsync(id, Product);

            TempData["SuccessMessage"] =
                "Product updated successfully.";

            return RedirectToPage("./Index");
        }
        catch (HttpRequestException)
        {
            ErrorMessage = "Unable to update product. Check the API and product details.";
        }
        catch (TaskCanceledException)
        {
            ErrorMessage = "Request timed out. Check the product before retrying.";
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
            ErrorMessage ??= "Unable to load categories.";
        }
        catch (TaskCanceledException)
        {
            ErrorMessage ??= "Category request timed out.";
        }
    }
}
