using System.ComponentModel.DataAnnotations;

namespace AuraCafé.API.DTOs.Products;

public class CreateProductDto
{
    [StringLength(1000)]
    [RegularExpression(@"^$|^https://[^\s]+$|^/product-images/[a-f0-9]{32}\.(png|jpg|webp)$", ErrorMessage = "Use an HTTPS image URL.")]
    public string ImageUrl { get; set; } = "";
    public bool IsActive { get; set; } = true;

    public bool IsAvailable { get; set; } = true;

    [Required]
    [StringLength(150, MinimumLength = 2)]
    public string Name { get; set; } = "";

    [StringLength(500)]
    public string Description { get; set; } = "";

    [Range(1, int.MaxValue)]
    public int PriceCents { get; set; }

    [Range(1, int.MaxValue)]
    public int CategoryId { get; set; }
}