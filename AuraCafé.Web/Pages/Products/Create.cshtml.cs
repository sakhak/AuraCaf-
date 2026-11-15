
using AuraCafé.Web.Models;
using AuraCafé.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AuraCafé.Web.Pages.Products;

[RequestSizeLimit(6 * 1024 * 1024), RequestFormLimits(MultipartBodyLengthLimit = 6 * 1024 * 1024)]
public class CreateModel : PageModel
{
    private readonly ProductApiService _productApi;
    private readonly ProductImageStore _images;

    public CreateModel(ProductApiService productApi, ProductImageStore images)
    {
        _productApi = productApi;
        _images = images;
    }

    [BindProperty]
    public ProductFormDto Product { get; set; } = new();
    [BindProperty] public IFormFile? ImageUpload { get; set; }

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
            Product.ImageUrl = ImageUpload is null ? "" : await _images.SaveAsync(ImageUpload, HttpContext.RequestAborted);
            await _productApi.CreateAsync(Product);

            TempData["SuccessMessage"] =
                "Product created successfully.";

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
