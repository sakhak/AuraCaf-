using AuraCafé.API.Data;
using AuraCafé.API.Interfaces.Products;
using AuraCafé.API.Models;
using Microsoft.EntityFrameworkCore;

namespace AuraCafé.API.Repositories;

public class ProductRepository : IProductRepository
{
    private readonly AppDbContext _context;

    public ProductRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<(List<Product> Items, int Total)> SearchAsync(string? search, int? categoryId, bool? available, bool? active, int page, int pageSize)
    {
        var query = _context.Products.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(p => p.Name.ToLower().Contains(term) || p.Description.ToLower().Contains(term));
        }
        if (categoryId.HasValue) query = query.Where(p => p.CategoryId == categoryId);
        if (available.HasValue) query = query.Where(p => p.IsAvailable == available);
        if (active.HasValue) query = query.Where(p => p.IsActive == active);
        var total = await query.CountAsync();
        var items = await query.Include(p => p.Category).OrderBy(p => p.Name).ThenBy(p => p.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return (items, total);
    }

    public async Task<List<Product>> GetAllAsync()
    {
        return await _context.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .OrderBy(p => p.Name)
            .ToListAsync();
    }

    public async Task<Product?> GetByIdAsync(int id)
    {
        return await _context.Products
            .Include(p => p.Category)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task AddAsync(Product product)
    {
        await _context.Products.AddAsync(product);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }

    public void Delete(Product product)
    {
        _context.Products.Remove(product);
    }
    public async Task<bool> ExistsAsync(
    string name,
    int categoryId)
    {
        return await _context.Products
            .AnyAsync(p =>
                p.Name == name &&
                p.CategoryId == categoryId);
    }
}