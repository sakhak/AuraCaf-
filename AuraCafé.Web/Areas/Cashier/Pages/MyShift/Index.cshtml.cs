using System.ComponentModel.DataAnnotations;
using AuraCafé.Web.Models;
using AuraCafé.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AuraCafé.Web.Areas.Cashier.Pages.MyShift;

[Authorize(Roles = "Cashier")]
public class IndexModel(ShiftApiService shifts, ReportingApiService reports) : PageModel
{
    public ShiftDto? CurrentShift { get; private set; }
    public List<ShiftDto> History { get; private set; } = [];
    public ShiftCashDto? Cash { get; private set; }
    public string? ErrorMessage { get; private set; }
    public bool ClosureUnconfirmed { get; private set; }
    [BindProperty(SupportsGet = true)] public int? ShiftId { get; set; }
    [BindProperty, Range(typeof(long), "0", "9223372036854775807")] public long? OpeningRiel { get; set; }
    [BindProperty, Range(typeof(long), "0", "9223372036854775807")] public long? CountedRiel { get; set; }
    [BindProperty] public int ClosingShiftId { get; set; }
    [BindProperty, Range(typeof(decimal), "0", "21474836.47")] public decimal? OpeningCash { get; set; }
    [BindProperty, Range(typeof(decimal), "0", "92233720368547758.07")] public decimal? CountedCash { get; set; }
    [BindProperty, StringLength(500)] public string? Note { get; set; }
    [TempData] public string? SuccessMessage { get; set; }

    public async Task OnGetAsync() => await LoadAsync();
    private async Task LoadAsync()
    {
        try
        {
            CurrentShift = await shifts.GetCurrentAsync();
            History = await shifts.GetMyHistoryAsync();
            var selected = ShiftId ?? CurrentShift?.Id ?? History.FirstOrDefault()?.Id;
            if (selected.HasValue)
            {
                Cash = await reports.GetShiftAsync(selected.Value);
                if (Cash is null) ErrorMessage = "Shift not found.";
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            ErrorMessage = ex is HttpRequestException ? ex.Message : "Shift status request timed out. Refresh before taking another action.";
        }
    }
    public async Task<IActionResult> OnPostClockInAsync()
    {
        if (!OpeningCash.HasValue || OpeningCash < 0 || OpeningCash > 21474836.47m || OpeningCash * 100m != decimal.Truncate(OpeningCash.Value * 100m))
            ModelState.AddModelError(nameof(OpeningCash), "Enter an opening drawer float in USD with at most two decimals.");
        if (!OpeningRiel.HasValue || OpeningRiel < 0) ModelState.AddModelError(nameof(OpeningRiel), "Enter the opening KHR amount in whole riel, including zero when empty.");
        if (!ModelState.IsValid) { await LoadAsync(); return Page(); }
        try
        {
            await shifts.ClockInAsync((int)(OpeningCash!.Value * 100m), OpeningRiel);
            SuccessMessage = "Clocked in. Opening drawer float recorded.";
            return RedirectToPage();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            ErrorMessage = "Clock-in could not be confirmed. Refresh your current shift before retrying.";
            await LoadAsync(); return Page();
        }
    }
    public async Task<IActionResult> OnPostCloseAndReconcileAsync()
    {
        ShiftId = ClosingShiftId;
        if (ClosingShiftId <= 0 || !CountedCash.HasValue || CountedCash < 0 || CountedCash > 92233720368547758.07m || CountedCash * 100m != decimal.Truncate(CountedCash.Value * 100m))
            ModelState.AddModelError(nameof(CountedCash), "Enter the full drawer count in USD with at most two decimals.");
        if (!ModelState.IsValid) { await LoadAsync(); return Page(); }
        try
        {
            await reports.CloseShiftAsync(ClosingShiftId, (long)(CountedCash!.Value * 100m), Note ?? "", CountedRiel);
            SuccessMessage = "Shift closed and drawer count recorded.";
            return RedirectToPage(new { ShiftId = ClosingShiftId });
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            ErrorMessage = ex is HttpRequestException ? ex.Message : "Closure result is unknown. Check status before trying again.";
            ClosureUnconfirmed = true;
            await LoadAsync();
            if (Cash?.SubmittedAtUtc is not null) { ErrorMessage = null; SuccessMessage = "Confirmed: this shift is closed and reconciled."; }
            return Page();
        }
    }
    public async Task<IActionResult> OnPostClockOutAsync()
    {
        // Retain the original attendance-only route for legacy shifts without recorded float.
        try
        {
            var current = await shifts.GetCurrentAsync();
            if (current?.OpeningCashCents is not null) return BadRequest();
            await shifts.ClockOutAsync();
            SuccessMessage = "Legacy shift closed without drawer reconciliation: opening cash was not recorded.";
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException) { TempData["ShiftError"] = "Could not confirm clock-out. Refresh shift status."; }
        return RedirectToPage();
    }
}
