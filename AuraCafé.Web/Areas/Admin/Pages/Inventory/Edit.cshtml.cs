using AuraCafé.Web.Models;
using AuraCafé.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace AuraCafé.Web.Areas.Admin.Pages.Inventory;
[Authorize(Roles = "Admin")]
public class EditModel(InventoryApiService inventory) : PageModel
{
    [BindProperty] public CreateIngredientDto Input { get; set; } = new();
    public int? IngredientId { get; private set; }
    public string? ErrorMessage { get; private set; }
    public async Task<IActionResult> OnGetAsync(int? id)
    {
        IngredientId = id;
        if (!id.HasValue) return Page();
        try
        {
            var value = await inventory.IngredientAsync(id.Value);
            if (value is null) return NotFound();
            Input = new CreateIngredientDto { Name = value.Name, Unit = value.Unit, MinimumQuantity = value.MinimumQuantity, IsActive = value.IsActive };
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException) { ErrorMessage = "Unable to load this ingredient. Refresh before editing."; }
        return Page();
    }
    public async Task<IActionResult> OnPostAsync(int? id)
    {
        IngredientId = id;
        if (!ModelState.IsValid) return Page();
        try
        {
            await inventory.SaveIngredientAsync(id, Input);
            TempData["SuccessMessage"] = id.HasValue ? "Ingredient updated." : "Ingredient created with zero stock. Receive supplies through a stock movement.";
            return RedirectToPage("./Index");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException) { ErrorMessage = ex is HttpRequestException ? ex.Message : "The save result is unknown. Check the ingredient list before trying again."; }
        return Page();
    }
}
