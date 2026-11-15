
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AuraCafé.Web.Pages;

public class IndexModel : PageModel
{
    public IActionResult OnGet()
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return RedirectToPage("/Account/Login");
        }

        if (User.IsInRole("Admin"))
        {
            return RedirectToPage(
                "/Dashboard/Index",
                new { area = "Admin" });
        }

        if (User.IsInRole("Cashier"))
        {
            return RedirectToPage(
                "/Dashboard/Index",
                new { area = "Cashier" });
        }

        return RedirectToPage("/Account/Denied");
    }
}
