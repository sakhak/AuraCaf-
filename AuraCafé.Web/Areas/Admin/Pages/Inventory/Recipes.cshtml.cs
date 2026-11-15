using System.ComponentModel.DataAnnotations;
using AuraCafé.Web.Models;
using AuraCafé.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace AuraCafé.Web.Areas.Admin.Pages.Inventory;
[Authorize(Roles = "Admin")]
public class RecipesModel(InventoryApiService inventory, ProductApiService products) : PageModel
{
    [BindProperty(SupportsGet = true)] public int? ProductId { get; set; }
    [BindProperty(SupportsGet = true)] public string? IngredientSearch { get; set; }
    [BindProperty, Range(1, int.MaxValue)] public int IngredientId { get; set; }
    [BindProperty, Range(typeof(long), "1", "9223372036854775807")] public long QuantityRequired { get; set; }
    public List<ProductDto> Products { get; private set; } = [];
    public List<IngredientDto> Ingredients { get; private set; } = [];
    public ProductRecipeDto? Recipe { get; private set; }
    public string? ErrorMessage { get; private set; }
    [TempData] public string? SuccessMessage { get; set; }
    public async Task OnGetAsync() => await LoadAsync();
    private async Task LoadAsync()
    {
        try
        {
            Products = await products.GetAllAsync();
            Ingredients = (await inventory.ListAsync(IngredientSearch, true, false, 1))?.Items ?? [];
            if (ProductId.HasValue) { Recipe = await inventory.RecipeAsync(ProductId.Value); if (Recipe is null) ErrorMessage ??= "Product not found."; }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException) { ErrorMessage ??= ex is HttpRequestException ? ex.Message : "Recipe request timed out."; }
    }
    public async Task<IActionResult> OnPostSaveAsync()
    {
        if (!ProductId.HasValue) ModelState.AddModelError(string.Empty, "Select a product.");
        if (!ModelState.IsValid) { await LoadAsync(); return Page(); }
        return await ChangeAsync(lines =>
        {
            lines.RemoveAll(l => l.IngredientId == IngredientId);
            lines.Add(new RecipeLineDto { IngredientId = IngredientId, QuantityRequired = QuantityRequired });
        });
    }
    public Task<IActionResult> OnPostRemoveAsync() => ChangeAsync(lines => lines.RemoveAll(l => l.IngredientId == IngredientId));
    public Task<IActionResult> OnPostClearAsync() => ChangeAsync(lines => lines.Clear());
    private async Task<IActionResult> ChangeAsync(Action<List<RecipeLineDto>> change)
    {
        try
        {
            if (!ProductId.HasValue) return BadRequest();
            var recipe = await inventory.RecipeAsync(ProductId.Value);
            if (recipe is null) return NotFound();
            var lines = recipe.Lines.Select(l => new RecipeLineDto { IngredientId = l.IngredientId, QuantityRequired = l.QuantityRequired }).ToList();
            change(lines);
            await inventory.SaveRecipeAsync(ProductId.Value, new SaveRecipeDto { Lines = lines });
            SuccessMessage = "Recipe saved. No stock has been deducted.";
            return RedirectToPage(new { ProductId });
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException) { ErrorMessage = ex is HttpRequestException ? ex.Message : "Recipe save could not be confirmed. Refresh the recipe before trying again."; await LoadAsync(); return Page(); }
    }
}
