
namespace AuraCafé.API.Models;

public class WorkShift
{
    public int Id { get; set; }

    public int? OpeningCashCents { get; set; }
    public long? OpeningCashRiel { get; set; }

    public int StaffId { get; set; }

    public Staff Staff { get; set; } = null!;

    public DateTime ClockInUtc { get; set; }

    public DateTime? ClockOutUtc { get; set; }

    public string Status { get; set; } = "Open";

    public DateTime CreatedAtUtc { get; set; } =
        DateTime.UtcNow;
}
