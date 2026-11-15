
using System.ComponentModel.DataAnnotations;

namespace AuraCafé.API.DTOs.Orders;

public class CreateOrderDto
{
    [Required]
    [MinLength(1)]
    [MaxLength(50)]
    public List<CreateOrderItemDto> Items { get; set; } = new();
}

public class CreateOrderItemDto
{
    [Range(1, int.MaxValue)]
    public int ProductId { get; set; }

    [Range(1, 100)]
    public int Quantity { get; set; }
}
