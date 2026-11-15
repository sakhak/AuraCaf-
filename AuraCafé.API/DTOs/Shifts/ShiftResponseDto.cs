
namespace AuraCafé.API.DTOs.Shifts;

public class ShiftResponseDto
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
}
