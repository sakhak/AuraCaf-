using System.ComponentModel.DataAnnotations;

namespace AuraCafé.API.DTOs.Products;

public class UpdateProductDto
{
    [Required]
    [StringLength(150, MinimumLength = 2)]
    public string Name { get; set; } = "";

    [StringLength(500)]
    public string Description { get; set; } = "";

    [Range(1, int.MaxValue)]
    public int PriceCents { get; set; }

    [Range(1, int.MaxValue)]
    public int CategoryId { get; set; }

    public bool IsAvailable { get; set; }
}