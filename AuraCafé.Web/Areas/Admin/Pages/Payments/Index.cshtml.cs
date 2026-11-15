
using AuraCafé.Web.Models;
using AuraCafé.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AuraCafé.Web.Areas.Admin.Pages.Payments;

[Authorize(Roles = "Admin")]
public class IndexModel : PageModel
{
    private readonly PaymentApiService _payments;

    public IndexModel(PaymentApiService payments)
    {
        _payments = payments;
    }

    public List<PaymentDto> Payments { get; set; } = new();

    public string? ErrorMessage { get; set; }

    public int PaymentCount => Payments.Count;

    public long TotalCollectedCents =>
        Payments.Sum(x => (long)x.AmountCents);

    public async Task OnGetAsync()
    {
        try
        {
            Payments = await _payments.GetAllAsync();
        }
        catch (HttpRequestException)
        {
            ErrorMessage = "Unable to load payments.";
        }
        catch (TaskCanceledException)
        {
            ErrorMessage = "Payment request timed out.";
        }
    }
}
