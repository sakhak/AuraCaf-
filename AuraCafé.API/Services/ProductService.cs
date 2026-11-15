using AuraCafé.API.Data;
using AuraCafé.API.DTOs.Products;
using AuraCafé.API.Interfaces.Products;
using AuraCafé.API.Models;
using Microsoft.EntityFrameworkCore;

namespace AuraCafé.API.Services;

public class ProductService : IProductService
{
    private readonly IProductRepository _repository;
    private readonly AppDbContext _context;

    public ProductService(
        IProductRepository repository,
        AppDbContext context)
    {
        _repository = repository;
        _context = context;
    }

    public async Task<List<ProductResponseDto>> GetAllAsync()
    {
        var products = await _repository.GetAllAsync();

        return products.Select(ToResponse).ToList();
    }

    public async Task<ProductResponseDto?> GetByIdAsync(int id)
    {
        var product = await _repository.GetByIdAsync(id);

        return product == null ? null : ToResponse(product);
    }

    public async Task<ProductResponseDto> CreateAsync(
        CreateProductDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name) || dto.Name.Trim().Length is < 2 or > 150)
            throw new ArgumentException(
                "Product name must be 2-150 characters after trimming.");

        if (dto.Description?.Length > 500)
            throw new ArgumentException("Description cannot exceed 500 characters.");

        if (dto.PriceCents <= 0)
            throw new ArgumentException(
                "Price must be greater than zero.");

        var category = await _context.Categories
            .FindAsync(dto.CategoryId);

        if (category == null)
            throw new ArgumentException(
                "Category does not exist.");
        var exists = await _repository.ExistsAsync(
            dto.Name.Trim(),
            dto.CategoryId);

        if (exists)
        {
            throw new ArgumentException(
                "A product with this name already exists in this category.");
        }

        var product = new Product
        {
            Name = dto.Name.Trim(),
            Description = dto.Description?.Trim() ?? "",
            PriceCents = dto.PriceCents,
            CategoryId = dto.CategoryId,
            Category = category,
            IsAvailable = dto.IsAvailable,
            IsActive = dto.IsActive,
            ImageUrl = ValidateImageUrl(dto.ImageUrl),
            CreatedAt = DateTime.UtcNow
        };

        await _repository.AddAsync(product);
        await _repository.SaveChangesAsync();

        return ToResponse(product);
    }

    public async Task<bool> UpdateAsync(
        int id, UpdateProductDto dto)
    {
        var product = await _repository.GetByIdAsync(id);

        if (product == null)
            return false;

        if (string.IsNullOrWhiteSpace(dto.Name) || dto.Name.Trim().Length is < 2 or > 150)
            throw new ArgumentException(
                "Product name must be 2-150 characters after trimming.");

        if (dto.Description?.Length > 500)
            throw new ArgumentException("Description cannot exceed 500 characters.");

        if (dto.PriceCents <= 0)
            throw new ArgumentException(
                "Price must be greater than zero.");

        var category = await _context.Categories
            .FindAsync(dto.CategoryId);

        if (category == null)
            throw new ArgumentException(
                "Category does not exist.");

        if (await _context.Products.AnyAsync(p => p.Id != id && p.Name == dto.Name.Trim() && p.CategoryId == dto.CategoryId))
            throw new ArgumentException("A product with this name already exists in this category.");

        product.ImageUrl = ValidateImageUrl(dto.ImageUrl);
        product.IsActive = dto.IsActive;
        product.Name = dto.Name.Trim();
        product.Description = dto.Description?.Trim() ?? "";
        product.PriceCents = dto.PriceCents;
        product.CategoryId = dto.CategoryId;
        product.Category = category;
        product.IsAvailable = dto.IsAvailable;

        await _repository.SaveChangesAsync();

        return true;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var product = await _repository.GetByIdAsync(id);

        if (product == null)
            return false;

        // Retire products instead of removing historical references.
        product.IsActive = false;
        await _repository.SaveChangesAsync();

        return true;
    }

    private static string ValidateImageUrl(string? value)
    {
        value = value?.Trim() ?? "";
        if (value.Length == 0) return "";
        if (System.Text.RegularExpressions.Regex.IsMatch(value, @"\A/product-images/[a-f0-9]{32}\.(png|jpg|webp)\z")) return value;
        if (value.Length > 1000 || !Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || uri.Scheme != "https" || string.IsNullOrEmpty(uri.Host) || !string.IsNullOrEmpty(uri.UserInfo))
            throw new ArgumentException("Use a valid HTTPS image URL without credentials.");
        return value;
    }

    public async Task<ProductPageDto> SearchAsync(string? search, int? categoryId, bool? available, bool? active, int page, int pageSize)
    {
        if (page < 1 || page > 1000000 || pageSize is < 1 or > 100 || categoryId <= 0)
            throw new ArgumentException("Invalid category or pagination.");
        var (products, total) = await _repository.SearchAsync(search, categoryId, available, active, page, pageSize);
        return new ProductPageDto { Items = products.Select(ToResponse).ToList(), TotalCount = total };
    }

    private static ProductResponseDto ToResponse(Product product)
    {
        return new ProductResponseDto
        {
            Id = product.Id,
            Name = product.Name,
            Description = product.Description,
            PriceCents = product.PriceCents,
            CategoryId = product.CategoryId,
            CategoryName = product.Category.Name,
            IsAvailable = product.IsAvailable,
            IsActive = product.IsActive,
            ImageUrl = product.ImageUrl,
            CreatedAt = product.CreatedAt
        };
    }
}
