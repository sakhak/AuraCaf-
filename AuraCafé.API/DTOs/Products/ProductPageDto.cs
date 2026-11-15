namespace AuraCafé.API.DTOs.Products;

public class ProductPageDto
{
    public List<ProductResponseDto> Items { get; set; } = [];
    public int TotalCount { get; set; }
}
