
using System.Text.Json;
using AuraCafé.Web.Models;
using AuraCafé.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AuraCafé.Web.Areas.Cashier.Pages.POS;

[Authorize(Roles = "Cashier")]
public class IndexModel : PageModel
{
    private readonly PosProductApiService _products;
    private readonly OrderApiService _orders;
    private readonly ShiftApiService _shifts;

    public IndexModel(
        PosProductApiService products,
        OrderApiService orders,
        ShiftApiService shifts)
    {
        _products = products;
        _orders = orders;
        _shifts = shifts;
    }

    public List<PosProductDto> Products { get; set; } = new();
    public List<PosCategoryDto> Categories { get; set; } = new();

    public bool HasActiveShift { get; set; }

    public string? ErrorMessage { get; set; }

    [TempData]
    public string? SuccessMessage { get; set; }

    [BindProperty]
    public string CartJson { get; set; } = "";

    [BindProperty]
    public bool ProceedToPayment { get; set; }

    [BindProperty]
    public string? PaymentChoice { get; set; }

    public async Task OnGetAsync()
    {
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            Products = await _products.GetAvailableAsync();
            Categories = await _products.GetCategoriesAsync();

            HasActiveShift =
                await _shifts.GetCurrentAsync() is not null;
        }
        catch (HttpRequestException)
        {
            ErrorMessage =
                "Unable to load the POS. Check your API connection.";
        }
        catch (TaskCanceledException)
        {
            ErrorMessage = "The API request timed out.";
        }
    }

    public async Task<IActionResult> OnPostCheckoutAsync()
    {
        List<PosOrderItemDto>? items;

        try
        {
            items = JsonSerializer.Deserialize<List<PosOrderItemDto>>(
                CartJson,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
        }
        catch (JsonException)
        {
            ModelState.AddModelError(
                "", "Invalid shopping cart.");

            await LoadAsync();
            return Page();
        }

        if (items is null ||
            items.Count == 0 ||
            items.Count > 50 ||
            items.Any(x => x.ProductId <= 0 ||
                           x.Quantity < 1 ||
                           x.Quantity > 100) ||
            items.Select(x => x.ProductId)
                .Distinct().Count() != items.Count)
        {
            ModelState.AddModelError(
                "", "Cart contains invalid products or quantities.");

            await LoadAsync();
            return Page();
        }

        try
        {
            var order = await _orders.CreateAsync(
                new PosCreateOrderDto { Items = items });

            if (ProceedToPayment)
                return RedirectToPage("/Payments/Create", new { area = "Cashier", orderId = order.Id, method = PaymentChoice == "qr" ? "qr" : "cash" });

            SuccessMessage =
                $"Order {order.OrderNumber} created. " +
                $"Total: ${order.TotalCents / 100m:0.00}. " +
                "Payment is still pending.";

            return RedirectToPage();
        }
        catch (HttpRequestException ex)
        {
            ErrorMessage = ex.Message;
        }
        catch (TaskCanceledException)
        {
            ErrorMessage =
                "The request timed out. Check My Orders " +
                "before submitting again to avoid duplicates.";
        }

        await LoadAsync();
        return Page();
    }
}
