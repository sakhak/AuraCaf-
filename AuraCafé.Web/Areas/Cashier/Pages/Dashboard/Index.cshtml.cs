using AuraCafé.Web.Models;
using AuraCafé.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AuraCafé.Web.Areas.Cashier.Pages.Dashboard;

[Authorize(Roles = "Cashier")]
public class IndexModel(ShiftApiService shifts, OrderApiService orders) : PageModel
{
    public string CashierName => User.Identity?.Name ?? "Cashier";
    public ShiftDto? Shift { get; private set; }
    public string? ShiftError { get; private set; }
    public string? OrdersError { get; private set; }
    public List<PosOrderResponseDto> RecentOrders { get; private set; } = [];
    public async Task OnGetAsync()
    {
        try { Shift = await shifts.GetCurrentAsync(); }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException) { ShiftError = "Your shift status is unavailable. Check My Shift before taking orders."; }
        try { RecentOrders = (await orders.GetMyOrdersAsync()).OrderByDescending(o => o.CreatedAtUtc).ThenByDescending(o => o.Id).Take(5).ToList(); }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException) { OrdersError = "Recent orders could not be loaded. Refresh or open My Orders."; }
    }
}
