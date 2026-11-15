using AuraCafé.Web.Models;
using AuraCafé.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace AuraCafé.Web.Areas.Admin.Pages.Staff;

[Authorize(Roles = "Admin")]
public class IndexModel(StaffApiService staffApi) : PageModel
{
    [BindProperty(SupportsGet = true)] public string? Search { get; set; }
    [BindProperty(SupportsGet = true)] public string? Position { get; set; }
    [BindProperty(SupportsGet = true)] public string? Status { get; set; }
    [BindProperty(SupportsGet = true)] public int PageNumber { get; set; } = 1;
    public List<StaffDto> StaffMembers { get; private set; } = [];
    public int TotalStaff { get; private set; }
    public int ActiveCount { get; private set; }
    public int CashierCount { get; private set; }
    public int LoginCount { get; private set; }
    public int MatchingCount { get; private set; }
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(MatchingCount / 12d));
    public string? ErrorMessage { get; private set; }
    [TempData] public string? SuccessMessage { get; set; }
    public async Task OnGetAsync()
    {
        if (!ModelState.IsValid || Search?.Length > 150 || PageNumber is < 1 or > 1000000
            || (Position is not (null or "" or "Cashier" or "Barista")) || (Status is not (null or "" or "Active" or "Inactive")))
        { ErrorMessage = "Invalid search, filters or page number."; return; }
        try
        {
            // Reuse the existing complete staff endpoint; no browser-side employee dataset.
            var all = await staffApi.GetAllAsync();
            TotalStaff = all.Count;
            ActiveCount = all.Count(s => s.EmploymentStatus == "Active");
            CashierCount = all.Count(s => s.Position == "Cashier");
            LoginCount = all.Count(s => s.HasLoginAccount);
            IEnumerable<StaffDto> filtered = all;
            if (!string.IsNullOrWhiteSpace(Search))
            {
                var term = Search.Trim();
                filtered = filtered.Where(s => s.FullName.Contains(term, StringComparison.OrdinalIgnoreCase)
                    || s.Email.Contains(term, StringComparison.OrdinalIgnoreCase) || s.StaffCode.Contains(term, StringComparison.OrdinalIgnoreCase));
            }
            if (!string.IsNullOrEmpty(Position)) filtered = filtered.Where(s => s.Position == Position);
            if (!string.IsNullOrEmpty(Status)) filtered = filtered.Where(s => s.EmploymentStatus == Status);
            var matching = filtered.OrderBy(s => s.FullName).ThenBy(s => s.Id).ToList();
            MatchingCount = matching.Count;
            PageNumber = Math.Min(PageNumber, TotalPages);
            StaffMembers = matching.Skip((PageNumber - 1) * 12).Take(12).ToList();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException) { ErrorMessage = "Unable to load staff records. Check the API connection and refresh."; }
    }
    public async Task<IActionResult> OnPostStatusAsync(int id, bool active)
    {
        try
        {
            await staffApi.SetStatusAsync(id, active);
            SuccessMessage = active ? "Employee activated successfully." : "Employee deactivated successfully. Linked account access has been disabled.";
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        { TempData["ErrorMessage"] = ex is HttpRequestException ? ex.Message : "Status change could not be confirmed. Refresh before retrying."; }
        return RedirectToPage(new { Search, Position, Status, PageNumber });
    }
}
