
namespace AuraCafé.API.DTOs.Orders;

public class OrderResponseDto
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = "";

    public int SoldByStaffId { get; set; }
    public string CashierName { get; set; } = "";

    public int WorkShiftId { get; set; }

    public int TotalCents { get; set; }
    public string Status { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; }

    public List<OrderItemResponseDto> Items { get; set; } = new();
}

public class OrderItemResponseDto
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public int Quantity { get; set; }
    public int UnitPriceCents { get; set; }
    public int SubtotalCents { get; set; }
}
