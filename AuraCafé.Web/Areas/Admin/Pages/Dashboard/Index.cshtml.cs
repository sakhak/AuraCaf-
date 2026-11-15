using AuraCafé.Web.Models;
using AuraCafé.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AuraCafé.Web.Areas.Admin.Pages.Dashboard;

[Authorize(Roles = "Admin")]
public class IndexModel(ReportingApiService reporting, ILogger<IndexModel> logger) : PageModel
{
    public string AdminName => User.Identity?.Name ?? "Administrator";
    public SalesReportDto? Report { get; private set; }
    public SalesReportDto? Week { get; private set; }
    public string? ChartError { get; private set; }
    public string? ErrorMessage { get; private set; }
    public async Task OnGetAsync()
    {
        var today = ReportDisplay.Today;
        try { Report = await reporting.GetSalesAsync(today, today); }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(ex, "Dashboard report unavailable");
            ErrorMessage = ex is HttpRequestException ? ex.Message : "The dashboard request timed out. Refresh to try again.";
        }
        try { Week = await reporting.GetSalesAsync(today.AddDays(-6), today); }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(ex, "Dashboard weekly report unavailable");
            ChartError = "The seven-day sales view is unavailable. Refresh to try again.";
        }
    }
}
