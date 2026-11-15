
using System.ComponentModel.DataAnnotations;

namespace AuraCafé.API.DTOs.Payments;

public class CreateCashPaymentDto
{
    [Range(1, int.MaxValue)]
    public int OrderId { get; set; }

    [Range(0, int.MaxValue)]
    public int CashReceivedCents { get; set; }
}
