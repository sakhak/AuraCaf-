
using AuraCafé.Web.Models;
using AuraCafé.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AuraCafé.Web.Areas.Admin.Pages.Staff;

[Authorize(Roles = "Admin")]
public class EditModel : PageModel
{
    private readonly StaffApiService _staffApi;

    public EditModel(StaffApiService staffApi)
    {
        _staffApi = staffApi;
    }

    [BindProperty]
    public EditStaffForm Staff { get; set; } = new();

    public string StaffCode { get; set; } = "";

    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        StaffDto? staff;
        try { staff = await _staffApi.GetByIdAsync(id); }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException) { ErrorMessage = "Unable to load this employee. Refresh before editing."; return Page(); }
        if (staff is null) return NotFound();

        StaffCode = staff.StaffCode;

        Staff = new EditStaffForm
        {
            FullName = staff.FullName,
            PhoneNumber = staff.PhoneNumber,
            Position = staff.Position,
            HireDate = staff.HireDate
        };

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        try
        {
            var current = await _staffApi.GetByIdAsync(id);
            if (current is null) return NotFound();
            StaffCode = current.StaffCode;
            if (!ModelState.IsValid) return Page();
            await _staffApi.UpdateAsync(id, Staff);

            TempData["SuccessMessage"] =
                "Employee updated successfully.";

            return RedirectToPage("./Index");
        }
        catch (HttpRequestException ex)
        {
            ErrorMessage = ex.Message;
            return Page();
        }
        catch (TaskCanceledException)
        {
            ErrorMessage = "Request timed out.";
            return Page();
        }
    }
}
