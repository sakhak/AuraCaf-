using AuraCafé.API.Models;

namespace AuraCafé.API.Interfaces;

public interface IProductRepository
{
    Task<List<Product>> GetAllAsync();

    Task<Product?> GetByIdAsync(int id);

    Task AddAsync(Product product);

    Task SaveChangesAsync();

    void Delete(Product product);

    Task<bool> ExistsAsync(string name, int categoryId);
}