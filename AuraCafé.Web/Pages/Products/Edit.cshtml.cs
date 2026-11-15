
using AuraCafé.Web.Models;
using AuraCafé.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AuraCafé.Web.Pages.Products;

[RequestSizeLimit(6 * 1024 * 1024), RequestFormLimits(MultipartBodyLengthLimit = 6 * 1024 * 1024)]
public class EditModel : PageModel
{
    private readonly ProductApiService _productApi;
    private readonly ProductImageStore _images;

    public EditModel(ProductApiService productApi, ProductImageStore images)
    {
        _productApi = productApi;
        _images = images;
    }

    [BindProperty]
    public ProductFormDto Product { get; set; } = new();
    [BindProperty] public IFormFile? ImageUpload { get; set; }

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
                ImageUrl = existing.ImageUrl,
                IsActive = existing.IsActive,
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

        try
        {
            var existing = await _productApi.GetByIdAsync(id);
            if (existing is null) return NotFound();
            Product.ImageUrl = existing.ImageUrl;
            if (!ModelState.IsValid) { await LoadCategoriesAsync(); return Page(); }
            if (ImageUpload is not null) Product.ImageUrl = await _images.SaveAsync(ImageUpload, HttpContext.RequestAborted);
            await _productApi.UpdateAsync(id, Product);

            TempData["SuccessMessage"] =
                "Product updated successfully.";

            return RedirectToPage("./Index");
        }
        catch (ArgumentException ex)
        {
            ModelState.AddModelError(nameof(ImageUpload), ex.Message);
        }
        catch (IOException)
        {
            ModelState.AddModelError(nameof(ImageUpload), "The image could not be saved. Check the server's image-storage access.");
        }
        catch (HttpRequestException ex)
        {
            ErrorMessage = ex.Message;
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
