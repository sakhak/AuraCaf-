using AuraCafé.Web.Models;
using AuraCafé.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AuraCafé.Web.Areas.Cashier.Pages.Payments;

[Authorize(Roles = "Cashier")]
public class SandboxModel(OrderApiService orders, BakongSandboxApiService sandbox) : PageModel
{
    public PosOrderResponseDto? Order { get; private set; }
    public BakongSandboxIntentDto? Intent { get; private set; }
    public string? ErrorMessage { get; private set; }
    public async Task<IActionResult> OnGetAsync(int orderId)
    {
        try
        {
            Order = await orders.GetByIdAsync(orderId);
            if (Order is null) return NotFound();
            Intent = await sandbox.GetAsync(orderId);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        { ErrorMessage = "Sandbox checkout could not be loaded. Check the API connection and refresh."; }
        return Page();
    }
    public async Task<IActionResult> OnPostStartAsync(int orderId)
    {
        try { await sandbox.StartAsync(orderId); return RedirectToPage(new { orderId }); }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            ErrorMessage = ex is HttpRequestException ? ex.Message : "The sandbox result is unknown. Refresh before starting again.";
            try { Order = await orders.GetByIdAsync(orderId); Intent = await sandbox.GetAsync(orderId); }
            catch (Exception loadError) when (loadError is HttpRequestException or TaskCanceledException) { }
            return Page();
        }
    }
    public async Task<IActionResult> OnPostCheckAsync(int orderId)
    {
        var json = Request.Headers.Accept.ToString().Contains("application/json", StringComparison.OrdinalIgnoreCase);
        try
        {
            var result = await sandbox.CheckAsync(orderId);
            if (json) return new JsonResult(new { status = result.Status, transactionHash = result.TransactionHash });
            return RedirectToPage(new { orderId });
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            var message = ex is HttpRequestException ? ex.Message : "Sandbox verification timed out. Check again later.";
            if (json) return new JsonResult(new { message }) { StatusCode = 503 };
            ErrorMessage = message;
            try { Order = await orders.GetByIdAsync(orderId); Intent = await sandbox.GetAsync(orderId); }
            catch (Exception loadError) when (loadError is HttpRequestException or TaskCanceledException) { }
            return Page();
        }
    }
}
