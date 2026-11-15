namespace AuraCafé.API.Models;

public class Product
{
    public int Id { get; set; }

    public string Name { get; set; } = "";

    public string Description { get; set; } = "";

    public string ImageUrl { get; set; } = "";
    public bool IsActive { get; set; } = true;

    public int PriceCents { get; set; }

    public bool IsAvailable { get; set; } = true;

    public int CategoryId { get; set; }

    public Category Category { get; set; } = null!;

    public DateTime CreatedAt { get; set; }
        = DateTime.UtcNow;
}