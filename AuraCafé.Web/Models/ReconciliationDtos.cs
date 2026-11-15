using System.ComponentModel.DataAnnotations;

namespace AuraCafé.Web.Models;

public class ClockInCashDto
{
    [Range(0, int.MaxValue)]
    public int OpeningCashCents { get; set; }
    [Range(typeof(long), "0", "9223372036854775807")]
    public long? OpeningCashRiel { get; set; }
}

public class CloseShiftCashDto
{
    [Range(typeof(long), "0", "9223372036854775807")]
    public long CountedCashCents { get; set; }
    [Range(typeof(long), "0", "9223372036854775807")]
    public long? CountedCashRiel { get; set; }
    [StringLength(500)]
    public string Note { get; set; } = "";
}

public class ShiftCashDto
{
    public int WorkShiftId { get; set; }
    public int StaffId { get; set; }
    public string CashierName { get; set; } = "";
    public string ShiftStatus { get; set; } = "";
    public int? OpeningCashCents { get; set; }
    public long? OpeningCashRiel { get; set; }
    public long? ExpectedCashRiel { get; set; }
    public long? CountedCashRiel { get; set; }
    public long? DifferenceCashRiel { get; set; }
    public long RevenueCents { get; set; }
    public int PaidOrders { get; set; }
    public long CashReceivedCents { get; set; }
    public long ChangeCents { get; set; }
    public long? ExpectedCashCents { get; set; }
    public long? CountedCashCents { get; set; }
    public long? DifferenceCents { get; set; }
    public DateTime? SubmittedAtUtc { get; set; }
    public string Note { get; set; } = "";
}
