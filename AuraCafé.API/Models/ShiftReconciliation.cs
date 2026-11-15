namespace AuraCafé.API.Models;

public class ShiftReconciliation
{
    public int Id { get; set; }
    public int WorkShiftId { get; set; }
    public WorkShift WorkShift { get; set; } = null!;
    public int SubmittedByStaffId { get; set; }
    public Staff SubmittedByStaff { get; set; } = null!;
    public int OpeningCashCents { get; set; }
    public long? OpeningCashRiel { get; set; }
    public long? ExpectedCashRiel { get; set; }
    public long? CountedCashRiel { get; set; }
    public long? DifferenceCashRiel { get; set; }
    public long RevenueCents { get; set; }
    public int PaidOrders { get; set; }
    public long CashReceivedCents { get; set; }
    public long ChangeCents { get; set; }
    public long ExpectedCashCents { get; set; }
    public long CountedCashCents { get; set; }
    public long DifferenceCents { get; set; }
    public DateTime SubmittedAtUtc { get; set; }
    public string Note { get; set; } = "";
}
