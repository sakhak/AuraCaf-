using AuraCafé.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

namespace AuraCafé.Web.Areas.Admin.Pages.Settings;

[Authorize(Roles = "Admin")]
[RequestSizeLimit(6 * 1024 * 1024)]
public class PaymentQrModel(MerchantQrStore store, BakongSandboxApiService sandbox) : PageModel
{
    [BindProperty, Required(ErrorMessage = "Choose a merchant QR image.")]
    public IFormFile? QrImage { get; set; }
    public MerchantQr? Current { get; private set; }
    [TempData] public string? SuccessMessage { get; set; }

    [TempData] public string? ConnectionMessage { get; set; }
    public async Task<IActionResult> OnPostConnectionAsync()
    {
        try
        {
            var result = await sandbox.ConnectionAsync();
            ConnectionMessage = result.AccountExists ? $"Connected to Bakong sandbox. Test account {result.ReceiverAccountId} exists." : "Bakong sandbox responded, but the receiving account was not found.";
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        { ConnectionMessage = ex is HttpRequestException ? ex.Message : "The sandbox connection check timed out."; }
        return RedirectToPage();
    }
    public async Task OnGetAsync() => Current = await store.GetAsync();

    public async Task<IActionResult> OnPostAsync()
    {
        if (ModelState.IsValid && QrImage is not null)
        {
            try
            {
                await store.SaveAsync(QrImage, User.Identity?.Name ?? "Admin", HttpContext.RequestAborted);
                SuccessMessage = "Merchant QR saved. Cashiers can now select QR at checkout.";
                return RedirectToPage();
            }
            catch (ArgumentException ex) { ModelState.AddModelError("QrImage", ex.Message); }
            catch (IOException) { ModelState.AddModelError("", "Unable to save the QR image. Check storage permissions and try again."); }
        }
        Current = await store.GetAsync();
        return Page();
    }
}
