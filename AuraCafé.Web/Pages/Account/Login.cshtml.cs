
using AuraCafé.Web.Models;
using AuraCafé.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Security.Claims;

namespace AuraCafé.Web.Pages.Account;

public class LoginModel : PageModel
{
    private readonly AuthApiService _auth;
    private readonly AuthSessionStore _sessions;

    public LoginModel(
        AuthApiService auth,
        AuthSessionStore sessions)
    {
        _auth = auth;
        _sessions = sessions;
    }

    [BindProperty]
    public LoginInput Input { get; set; } = new();

    public class LoginInput
    {
        [Required, EmailAddress]
        public string Email { get; set; } = "";

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; } = "";
    }

    public async Task<IActionResult> OnGetAsync()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            if (User.IsInRole("Admin"))
                return RedirectToPage(
                    "/Dashboard/Index", new { area = "Admin" });

            if (User.IsInRole("Cashier"))
                return RedirectToPage(
                    "/Dashboard/Index", new { area = "Cashier" });
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        LoginResult? login;

        try
        {
            login = await _auth.LoginAsync(
                Input.Email, Input.Password);
        }
        catch (HttpRequestException ex)
            when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            ModelState.AddModelError(
                "", "Invalid email or password.");
            return Page();
        }
        catch (HttpRequestException ex)
            when (ex.StatusCode.HasValue)
        {
            ModelState.AddModelError(
                "", "Authentication service returned an error.");
            return Page();
        }
        catch (HttpRequestException)
        {
            ModelState.AddModelError(
                "", "Authentication service is unavailable.");
            return Page();
        }
        catch (TaskCanceledException)
        {
            ModelState.AddModelError(
                "", "Authentication request timed out.");
            return Page();
        }

        if (login is null ||
            login.Role is not ("Admin" or "Cashier"))
        {
            ModelState.AddModelError(
                "", "Invalid email or password.");
            return Page();
        }

        var sessionId = _sessions.Create(login);

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, login.FullName),
            new(ClaimTypes.Role, login.Role),
            new("session_id", sessionId)
        };

        var identity = new ClaimsIdentity(
            claims,
            CookieAuthenticationDefaults.AuthenticationScheme);

        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = false,
                ExpiresUtc = new DateTimeOffset(
                    DateTime.SpecifyKind(
                        login.ExpiresAtUtc,
                        DateTimeKind.Utc))
            });

        if (login.Role == "Admin")
            return RedirectToPage(
                "/Dashboard/Index", new { area = "Admin" });

        return RedirectToPage(
            "/Dashboard/Index", new { area = "Cashier" });
    }
}
