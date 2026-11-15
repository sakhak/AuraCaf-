using AuraCafé.API.DTOs.Products;

namespace AuraCafé.API.Interfaces.Products;

public interface IProductService
{
    Task<List<ProductResponseDto>> GetAllAsync();
    Task<ProductPageDto> SearchAsync(string? search, int? categoryId, bool? available, bool? active, int page, int pageSize);

    Task<ProductResponseDto?> GetByIdAsync(int id);

    Task<ProductResponseDto> CreateAsync(
        CreateProductDto dto);

    Task<bool> UpdateAsync(
        int id, UpdateProductDto dto);

    Task<bool> DeleteAsync(int id);
}