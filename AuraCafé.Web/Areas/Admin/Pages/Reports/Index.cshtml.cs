using AuraCafé.Web.Models;
using AuraCafé.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AuraCafé.Web.Areas.Admin.Pages.Reports;

[Authorize(Roles = "Admin")]
public class IndexModel(ReportingApiService reporting, ILogger<IndexModel> logger) : PageModel
{
    [BindProperty(SupportsGet = true)] public DateOnly From { get; set; } = new(ReportDisplay.Today.Year, ReportDisplay.Today.Month, 1);
    [BindProperty(SupportsGet = true)] public DateOnly To { get; set; } = ReportDisplay.Today;
    public SalesReportDto? Report { get; private set; }
    public string? ErrorMessage { get; private set; }
    public async Task OnGetAsync()
    {
        if (!ModelState.IsValid || From > To || To == DateOnly.MaxValue || To.DayNumber - From.DayNumber > 365)
        {
            ErrorMessage = "Choose a valid date range of at most 366 days.";
            return;
        }
        try { Report = await reporting.GetSalesAsync(From, To); }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(ex, "Sales report could not be loaded");
            ErrorMessage = ex is HttpRequestException ? ex.Message : "The report request timed out. Refresh to try again.";
        }
    }
}
