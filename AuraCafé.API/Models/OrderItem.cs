
namespace AuraCafé.API.Models;

public class OrderItem
{
    public int Id { get; set; }

    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;

    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    public string ProductName { get; set; } = "";

    public int Quantity { get; set; }

    public int UnitPriceCents { get; set; }

    public int SubtotalCents { get; set; }
}
