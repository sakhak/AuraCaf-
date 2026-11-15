
namespace AuraCafé.Web.Models;

public class PosOrderItemDto
{
    public int ProductId { get; set; }
    public int Quantity { get; set; }
}

public class PosCreateOrderDto
{
    public List<PosOrderItemDto> Items { get; set; } = new();
}

public class PosOrderResponseDto
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = "";
    public int SoldByStaffId { get; set; }
    public string CashierName { get; set; } = "";
    public int WorkShiftId { get; set; }
    public int TotalCents { get; set; }
    public string Status { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; }

    public List<PosOrderLineDto> Items { get; set; } = new();
}

public class PosOrderLineDto
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public int Quantity { get; set; }
    public int UnitPriceCents { get; set; }
    public int SubtotalCents { get; set; }
}
