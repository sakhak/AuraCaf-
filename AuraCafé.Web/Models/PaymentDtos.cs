
using System.ComponentModel.DataAnnotations;

namespace AuraCafé.Web.Models;

public class CashPaymentForm
{
    [Range(1, int.MaxValue)]
    public int OrderId { get; set; }

    [Required]
    [Range(typeof(decimal), "0", "21474836.47")]
    public decimal? CashReceived { get; set; }
}

public class CashPaymentRequestDto
{
    public int OrderId { get; set; }
    public int CashReceivedCents { get; set; }
}

public class PaymentDto
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public string OrderNumber { get; set; } = "";

    public int CollectedByStaffId { get; set; }
    public string CashierName { get; set; } = "";

    public int WorkShiftId { get; set; }
    public string Method { get; set; } = "";
    public string Status { get; set; } = "";

    public int AmountCents { get; set; }
    public int CashReceivedCents { get; set; }
    public int ChangeCents { get; set; }

    public DateTime PaidAtUtc { get; set; }
}
