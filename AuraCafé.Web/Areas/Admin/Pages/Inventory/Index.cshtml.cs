using AuraCafé.Web.Models;
using AuraCafé.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace AuraCafé.Web.Areas.Admin.Pages.Inventory;
[Authorize(Roles = "Admin")]
public class IndexModel(InventoryApiService inventory) : PageModel
{
    [BindProperty(SupportsGet = true)] public string? Search { get; set; }
    [BindProperty(SupportsGet = true)] public bool? Active { get; set; }
    [BindProperty(SupportsGet = true)] public bool LowOnly { get; set; }
    [BindProperty(SupportsGet = true)] public int PageNumber { get; set; } = 1;
    public IngredientPageDto? PageData { get; private set; }
    public InventorySummaryDto? Summary { get; private set; }
    public int TotalPages => Math.Max(1, (int)Math.Ceiling((PageData?.TotalCount ?? 0) / 20d));
    public string? ErrorMessage { get; private set; }
    [TempData] public string? SuccessMessage { get; set; }
    public async Task OnGetAsync()
    {
        if (!ModelState.IsValid || PageNumber is < 1 or > 1000000) { ErrorMessage = "Invalid filters or page number."; return; }
        try
        {
            Summary = await inventory.SummaryAsync();
            PageData = await inventory.ListAsync(Search, Active, LowOnly, PageNumber);
            if (PageNumber > TotalPages) { PageNumber = TotalPages; PageData = await inventory.ListAsync(Search, Active, LowOnly, PageNumber); }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException) { ErrorMessage = ex is HttpRequestException ? ex.Message : "Inventory request timed out. Refresh to try again."; }
    }
    public async Task<IActionResult> OnPostDeactivateAsync(int id)
    {
        try { await inventory.DeactivateAsync(id); SuccessMessage = "Ingredient deactivated. Ledger and recipe references are retained."; }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException) { TempData["InventoryError"] = "Could not confirm deactivation. Refresh before retrying."; }
        return RedirectToPage(new { Search, Active, LowOnly, PageNumber });
    }
}
