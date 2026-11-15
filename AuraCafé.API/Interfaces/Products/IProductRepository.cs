using AuraCafé.API.Models;

namespace AuraCafé.API.Interfaces.Products;

public interface IProductRepository
{
    Task<List<Product>> GetAllAsync();
    Task<(List<Product> Items, int Total)> SearchAsync(string? search, int? categoryId, bool? available, bool? active, int page, int pageSize);

    Task<Product?> GetByIdAsync(int id);

    Task AddAsync(Product product);

    Task SaveChangesAsync();

    void Delete(Product product);

    Task<bool> ExistsAsync(string name, int categoryId);
}