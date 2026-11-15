namespace AuraCafé.API.DTOs.Products;

public class ProductResponseDto
{
    public int Id { get; set; }

    public string Name { get; set; } = "";

    public string Description { get; set; } = "";

    public string ImageUrl { get; set; } = "";
    public bool IsActive { get; set; } = true;

    public int PriceCents { get; set; }

    public int CategoryId { get; set; }

    public string CategoryName { get; set; } = "";

    public bool IsAvailable { get; set; }

    public DateTime CreatedAt { get; set; }
}