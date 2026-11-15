using AuraCafé.Web.Models;
using AuraCafé.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AuraCafé.Web.Areas.Admin.Pages.Shifts;

[Authorize(Roles = "Admin")]
public class ReconciliationModel(ReportingApiService reporting) : PageModel
{
    public ShiftCashDto? Cash { get; private set; }
    public string? ErrorMessage { get; private set; }
    public async Task<IActionResult> OnGetAsync(int id)
    {
        try
        {
            Cash = await reporting.GetShiftAsync(id);
            if (Cash is null) return NotFound();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            ErrorMessage = ex is HttpRequestException ? ex.Message : "Shift summary timed out. Refresh to try again.";
        }
        return Page();
    }
}
