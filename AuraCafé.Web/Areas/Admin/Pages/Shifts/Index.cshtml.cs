
using AuraCafé.Web.Models;
using AuraCafé.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AuraCafé.Web.Areas.Admin.Pages.Shifts;

[Authorize(Roles = "Admin")]
public class IndexModel : PageModel
{
    private readonly ShiftApiService _shifts;

    public IndexModel(ShiftApiService shifts)
    {
        _shifts = shifts;
    }

    public List<ShiftDto> Shifts { get; set; } = new();

    public string? ErrorMessage { get; set; }

    public int ActiveCount =>
        Shifts.Count(x => x.ClockOutUtc is null);

    public int CompletedCount =>
        Shifts.Count(x => x.ClockOutUtc is not null);

    public decimal TotalHours =>
        Shifts.Sum(x => x.HoursWorked ?? 0);

    public async Task OnGetAsync()
    {
        try
        {
            Shifts = await _shifts.GetAllAsync();
        }
        catch (HttpRequestException)
        {
            ErrorMessage = "Unable to load shift records.";
        }
        catch (TaskCanceledException)
        {
            ErrorMessage = "Shift request timed out.";
        }
    }
}
