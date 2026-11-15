
namespace AuraCafé.API.DTOs.Payments;

public class PaymentResponseDto
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
