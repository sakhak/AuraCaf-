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
    private bool ValidRange() => ModelState.IsValid && From <= To && To != DateOnly.MaxValue && To.DayNumber - From.DayNumber <= 365;
    public Task OnGetAsync() => LoadAsync();
    private async Task LoadAsync()
    {
        if (!ValidRange())
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
    public async Task<IActionResult> OnGetExportAsync()
    {
        if (!ValidRange()) return BadRequest(new { message = "Choose a valid date range of at most 366 days." });
        await LoadAsync();
        if (Report is not { } report) return StatusCode(503, new { message = ErrorMessage ?? "Report unavailable." });
        var csv = new System.Text.StringBuilder();
        string Number(long value) => value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        string Money(long value) => (value / 100m).ToString("0.00",System.Globalization.CultureInfo.InvariantCulture);
        string Cell(string value)
        {
            // Protect spreadsheet users from formulas in names; quote every CSV cell.
            var trimmed = value.TrimStart();
            if ((trimmed.Length > 0 && "=+-@".Contains(trimmed[0])) || value.StartsWith('\t') || value.StartsWith('\r')) value = "'" + value;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
        void Row(params string[] cells) => csv.AppendLine(string.Join(",",cells.Select(Cell)));
        Row("RecordType","Period","Name","PaidOrders","Quantity","RevenueUSD","CashReceivedUSD","ChangeUSD");
        var fromText = From.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        var toText = To.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        var period = $"{fromText} to {toText}";
        Row("Summary",period,"Completed cash payments",Number(report.PaidOrders),"",Money(report.RevenueCents),Money(report.CashReceivedCents),Money(report.ChangeCents));
        foreach (var day in report.Daily) Row("Daily",day.Date.ToString("yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture),"",Number(day.PaidOrders),"",Money(day.RevenueCents),"","");
        foreach (var month in report.Monthly) Row("Monthly",month.Date.ToString("yyyy-MM",System.Globalization.CultureInfo.InvariantCulture),"Selected dates only",Number(month.PaidOrders),"",Money(month.RevenueCents),"","");
        foreach (var cashier in report.Cashiers) Row("Cashier",period,$"{cashier.Name} (#{cashier.StaffId})",Number(cashier.PaidOrders),"",Money(cashier.RevenueCents),Money(cashier.CashReceivedCents),Money(cashier.ChangeCents));
        foreach (var product in report.Products) Row("ProductTop20",period,$"{product.Name} (#{product.ProductId})","",Number(product.Quantity),Money(product.RevenueCents),"","");
        Row("Note",period,"USD; Asia/Bangkok; group rows are alternative breakdowns, not additive revenue; products limited to top 20.","","","","","");
        Response.Headers.CacheControl = "no-store";
        var encoding = new System.Text.UTF8Encoding(true);
        return File(encoding.GetPreamble().Concat(encoding.GetBytes(csv.ToString())).ToArray(),"text/csv; charset=utf-8",$"aura-sales-{fromText}-to-{toText}.csv");
    }

}
