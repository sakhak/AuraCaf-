
using System.ComponentModel.DataAnnotations;

namespace AuraCafé.Web.Models;

public class ProductDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public int PriceCents { get; set; }
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = "";
    public bool IsAvailable { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ProductFormDto
{
    [Required(ErrorMessage = "Product name is required.")]
    [StringLength(150, MinimumLength = 2)]
    public string Name { get; set; } = "";

    [StringLength(500)]
    public string Description { get; set; } = "";

    [Range(1, int.MaxValue, ErrorMessage = "Price must be greater than zero.")]
    public int PriceCents { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Select a category.")]
    public int CategoryId { get; set; }

    public bool IsAvailable { get; set; } = true;
}
