using AuraCafé.Web.Models;
using AuraCafé.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace AuraCafé.Web.Areas.Admin.Pages.Inventory;
[Authorize(Roles = "Admin")]
public class MovementsModel(InventoryApiService inventory) : PageModel
{
    [BindProperty(SupportsGet = true)] public int? IngredientId { get; set; }
    [BindProperty(SupportsGet = true)] public int PageNumber { get; set; } = 1;
    [BindProperty] public CreateMovementDto Input { get; set; } = new() { RequestId = Guid.NewGuid() };
    public IngredientDto? Ingredient { get; private set; }
    public MovementPageDto? Ledger { get; private set; }
    public int TotalPages => Math.Max(1, (int)Math.Ceiling((Ledger?.TotalCount ?? 0) / 20d));
    public string? ErrorMessage { get; private set; }
    [TempData] public string? SuccessMessage { get; set; }
    public async Task OnGetAsync() => await LoadAsync();
    private async Task LoadAsync()
    {
        if (PageNumber is < 1 or > 1000000 || IngredientId <= 0) { ErrorMessage ??= "Invalid ingredient or page."; return; }
        try
        {
            if (IngredientId.HasValue)
            {
                Ingredient = await inventory.IngredientAsync(IngredientId.Value);
                if (Ingredient is null) { ErrorMessage ??= "Ingredient not found."; return; }
            }
            Ledger = await inventory.MovementsAsync(IngredientId, PageNumber);
            if (PageNumber > TotalPages) { PageNumber = TotalPages; Ledger = await inventory.MovementsAsync(IngredientId, PageNumber); }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException) { ErrorMessage ??= ex is HttpRequestException ? ex.Message : "Ledger request timed out."; }
    }
    public async Task<IActionResult> OnPostAsync()
    {
        if (!IngredientId.HasValue || Input.RequestId == Guid.Empty || Input.Quantity == 0 || (Input.Type != "Adjustment" && Input.Quantity < 0))
            ModelState.AddModelError(string.Empty, "Choose an ingredient and a valid nonzero whole-number quantity. Only adjustments accept a negative delta.");
        if (!ModelState.IsValid) { await LoadAsync(); return Page(); }
        try
        {
            var saved = await inventory.MoveAsync(IngredientId!.Value, Input);
            SuccessMessage = $"Movement #{saved.Id} confirmed. Balance: {saved.BalanceAfter} {saved.Unit}.";
            return RedirectToPage(new { IngredientId });
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            ErrorMessage = ex is HttpRequestException ? ex.Message : "The result is unknown. Refresh the ledger; retry only the unchanged transaction with the same request ID.";
            await LoadAsync();
            return Page();
        }
    }
}
