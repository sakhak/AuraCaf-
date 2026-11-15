
namespace AuraCafé.Web.Models;

public class ShiftDto
{
    public int? OpeningCashCents { get; set; }
    public long? OpeningCashRiel { get; set; }
    public int Id { get; set; }
    public int StaffId { get; set; }
    public string StaffCode { get; set; } = "";
    public string StaffName { get; set; } = "";

    public DateTime ClockInUtc { get; set; }
    public DateTime? ClockOutUtc { get; set; }

    public string Status { get; set; } = "";
    public decimal? HoursWorked { get; set; }

    public DateTime ClockInLocal =>
        DateTime.SpecifyKind(
            ClockInUtc, DateTimeKind.Utc).ToLocalTime();

    public DateTime? ClockOutLocal =>
        ClockOutUtc.HasValue
            ? DateTime.SpecifyKind(
                ClockOutUtc.Value,
                DateTimeKind.Utc).ToLocalTime()
            : null;
}
