using AuraCafé.Web.Models;
using AuraCafé.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AuraCafé.Web.Areas.Admin.Pages.Shifts;

[Authorize(Roles = "Admin")]
public class IndexModel(ShiftApiService shifts) : PageModel
{
    [BindProperty(SupportsGet = true)] public string? Search { get; set; }
    [BindProperty(SupportsGet = true)] public string? Status { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? From { get; set; }
    [BindProperty(SupportsGet = true)] public DateOnly? To { get; set; }
    [BindProperty(SupportsGet = true)] public int PageNumber { get; set; } = 1;
    public List<ShiftDto> Shifts { get; private set; } = [];
    public string? ErrorMessage { get; private set; }
    public int LoadedCount { get; private set; }
    public int ActiveCount { get; private set; }
    public int CompletedCount { get; private set; }
    public decimal TotalHours { get; private set; }
    public int MatchingCount { get; private set; }
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(MatchingCount / 12d));
    public async Task OnGetAsync()
    {
        if (!ModelState.IsValid || Search?.Length > 150 || PageNumber is < 1 or > 1000000
            || Status is not (null or "" or "Open" or "Closed") || (From.HasValue && To.HasValue && From > To))
        { ErrorMessage = "Check the search, status, page number and date range. From must be on or before To."; return; }
        try
        {
            var all = await shifts.GetAllAsync();
            LoadedCount = all.Count;
            ActiveCount = all.Count(s => s.ClockOutUtc is null);
            CompletedCount = all.Count(s => s.ClockOutUtc.HasValue);
            TotalHours = all.Where(s => s.ClockOutUtc.HasValue).Sum(s => s.HoursWorked ?? 0m);
            IEnumerable<ShiftDto> filtered = all;
            if (!string.IsNullOrWhiteSpace(Search))
            {
                var term = Search.Trim();
                filtered = filtered.Where(s => s.StaffName.Contains(term, StringComparison.OrdinalIgnoreCase)
                    || s.StaffCode.Contains(term, StringComparison.OrdinalIgnoreCase) || s.Id.ToString().Equals(term, StringComparison.Ordinal));
            }
            if (Status == "Open") filtered = filtered.Where(s => s.ClockOutUtc is null);
            if (Status == "Closed") filtered = filtered.Where(s => s.ClockOutUtc.HasValue);
            DateOnly LocalDay(DateTime utc) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc,DateTimeKind.Utc),ReportDisplay.Zone));
            if (From.HasValue) filtered = filtered.Where(s => LocalDay(s.ClockInUtc) >= From.Value);
            if (To.HasValue) filtered = filtered.Where(s => LocalDay(s.ClockInUtc) <= To.Value);
            var matches = filtered.OrderByDescending(s => s.ClockOutUtc is null).ThenByDescending(s => s.ClockInUtc).ThenByDescending(s => s.Id).ToList();
            MatchingCount = matches.Count;
            PageNumber = Math.Min(PageNumber, TotalPages);
            Shifts = matches.Skip((PageNumber - 1) * 12).Take(12).ToList();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        { ErrorMessage = "Unable to load shift records. Check the API connection and refresh."; }
    }
}
