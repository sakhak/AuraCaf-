using System.ComponentModel.DataAnnotations;

namespace AuraCafé.Web.Models;

public class ProductDto
{
    public string ImageUrl { get; set; } = "";
    public bool IsActive { get; set; } = true;
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
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    [StringLength(1000)]
    [RegularExpression(@"^$|^https://[^\s]+$|^/product-images/[a-f0-9]{32}\.(png|jpg|webp)$", ErrorMessage = "Use an HTTPS image URL.")]
    public string ImageUrl { get; set; } = "";
    public bool IsActive { get; set; } = true;
    [Required(ErrorMessage = "Product name is required.")]
    [StringLength(150, MinimumLength = 2)]
    public string Name { get; set; } = "";

    [DisplayFormat(ConvertEmptyStringToNull = false)]
    [StringLength(500)]
    public string Description { get; set; } = "";

    [Range(1, int.MaxValue, ErrorMessage = "Price must be greater than zero.")]
    public int PriceCents { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Select a category.")]
    public int CategoryId { get; set; }

    public bool IsAvailable { get; set; } = true;
}

public class ProductPageDto
{
    public List<ProductDto> Items { get; set; } = [];
    public int TotalCount { get; set; }
}
