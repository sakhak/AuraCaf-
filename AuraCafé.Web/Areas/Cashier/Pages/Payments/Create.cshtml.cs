
using AuraCafé.Web.Models;
using AuraCafé.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AuraCafé.Web.Areas.Cashier.Pages.Payments;

[Authorize(Roles = "Cashier")]
public class CreateModel : PageModel
{
    private readonly MerchantQrStore _merchantQr;
    public MerchantQr? MerchantQr { get; private set; }
    private readonly OrderApiService _orders;
    private readonly PaymentApiService _payments;

    public CreateModel(
        OrderApiService orders,
        PaymentApiService payments, MerchantQrStore merchantQr)
    {
        _merchantQr = merchantQr;
        _orders = orders;
        _payments = payments;
    }

    [BindProperty]
    public CashPaymentForm Payment { get; set; } = new();

    public PosOrderResponseDto? Order { get; set; }

    public PaymentDto? CompletedPayment { get; set; }

    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(int orderId)
    {
        return await LoadOrderAsync(orderId)
            ? Page()
            : NotFound();
    }

    private async Task<bool> LoadOrderAsync(int orderId)
    {
        MerchantQr = await _merchantQr.GetAsync();
        // Reporting regression fixtures exceed the recent-order list cap. Load the
        // specific order through the existing ownership-protected detail endpoint.
        Order = await _orders.GetByIdAsync(orderId);

        if (Order is null)
            return false;

        Payment.OrderId = orderId;

        if (Order.Status == "Paid")
        {
            CompletedPayment =
                await _payments.GetByOrderIdAsync(orderId);
        }

        return true;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            await LoadOrderAsync(Payment.OrderId);
            return Page();
        }

        if (Payment.CashReceived is null)
        {
            ModelState.AddModelError(
                "Payment.CashReceived",
                "Enter the cash received.");

            await LoadOrderAsync(Payment.OrderId);
            return Page();
        }

        decimal cents = Payment.CashReceived.Value * 100m;

        if (cents != decimal.Truncate(cents) ||
            cents < 0 ||
            cents > int.MaxValue)
        {
            ModelState.AddModelError(
                "Payment.CashReceived",
                "Enter a valid amount with at most two decimals.");

            await LoadOrderAsync(Payment.OrderId);
            return Page();
        }

        try
        {
            CompletedPayment = await _payments.CreateCashAsync(
                new CashPaymentRequestDto
                {
                    OrderId = Payment.OrderId,
                    CashReceivedCents = (int)cents
                });

            // Display the actual server response.
            await LoadOrderAsync(Payment.OrderId);

            return Page();
        }
        catch (HttpRequestException ex)
        {
            ErrorMessage = ex.Message;
        }
        catch (TaskCanceledException)
        {
            ErrorMessage =
                "The request timed out. Check the payment " +
                "status before trying again.";
        }

        await LoadOrderAsync(Payment.OrderId);
        return Page();
    }
}
