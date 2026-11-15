
using AuraCafé.Web.Models;
using AuraCafé.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AuraCafé.Web.Areas.Cashier.Pages.Orders;

[Authorize(Roles = "Cashier")]
public class IndexModel : PageModel
{
    private readonly OrderApiService _orders;

    public IndexModel(OrderApiService orders)
    {
        _orders = orders;
    }

    public List<PosOrderResponseDto> Orders { get; set; } = new();

    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync()
    {
        try
        {
            Orders = await _orders.GetMyOrdersAsync();
        }
        catch (HttpRequestException)
        {
            ErrorMessage = "Unable to load order history.";
        }
        catch (TaskCanceledException)
        {
            ErrorMessage = "Order request timed out.";
        }
    }
}
