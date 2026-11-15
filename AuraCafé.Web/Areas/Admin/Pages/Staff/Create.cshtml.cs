
using AuraCafé.Web.Models;
using AuraCafé.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AuraCafé.Web.Areas.Admin.Pages.Staff;

[Authorize(Roles = "Admin")]
public class CreateModel : PageModel
{
    private readonly StaffApiService _staffApi;

    public CreateModel(StaffApiService staffApi)
    {
        _staffApi = staffApi;
    }

    [BindProperty]
    public CreateStaffForm Staff { get; set; } = new();

    public string? ErrorMessage { get; set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (Staff.Position == "Cashier" &&
            string.IsNullOrWhiteSpace(Staff.Password))
        {
            ModelState.AddModelError(
                "Staff.Password",
                "A password is required for Cashiers.");
        }

        if (!ModelState.IsValid)
            return Page();

        try
        {
            await _staffApi.CreateAsync(Staff);

            TempData["SuccessMessage"] =
                "Employee created successfully.";

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
