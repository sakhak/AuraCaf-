
namespace AuraCafé.API.Models;

public class Order
{
    public int Id { get; set; }

    public string OrderNumber { get; set; } = "";

    public int SoldByStaffId { get; set; }
    public Staff SoldByStaff { get; set; } = null!;

    public int WorkShiftId { get; set; }
    public WorkShift WorkShift { get; set; } = null!;

    public int TotalCents { get; set; }

    public string Status { get; set; } = "Pending";

    public DateTime CreatedAtUtc { get; set; } =
        DateTime.UtcNow;

    public List<OrderItem> Items { get; set; } = new();
}
