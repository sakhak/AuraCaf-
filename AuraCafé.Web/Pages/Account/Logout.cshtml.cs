
using AuraCafé.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AuraCafé.Web.Pages.Account;

public class LogoutModel : PageModel
{
    private readonly AuthSessionStore _sessions;

    public LogoutModel(AuthSessionStore sessions)
    {
        _sessions = sessions;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var id = User.FindFirst("session_id")?.Value;

        if (id is not null)
            _sessions.Remove(id);

        await HttpContext.SignOutAsync(
            CookieAuthenticationDefaults.AuthenticationScheme);

        return RedirectToPage("/Account/Login");
    }
}
