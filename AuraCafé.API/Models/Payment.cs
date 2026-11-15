
namespace AuraCafé.API.Models;

public class Payment
{
    public int Id { get; set; }

    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;

    public int CollectedByStaffId { get; set; }
    public Staff CollectedByStaff { get; set; } = null!;

    public int WorkShiftId { get; set; }
    public WorkShift WorkShift { get; set; } = null!;

    public string Method { get; set; } = "Cash";

    public string Status { get; set; } = "Completed";

    public int AmountCents { get; set; }

    public int CashReceivedCents { get; set; }

    public int ChangeCents { get; set; }

    public DateTime PaidAtUtc { get; set; } =
        DateTime.UtcNow;
}
