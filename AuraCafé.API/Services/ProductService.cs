using AuraCafé.API.Data;
using AuraCafé.API.DTOs.Products;
using AuraCafé.API.Interfaces;
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
        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new ArgumentException(
                "Product name is required.");

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
            IsAvailable = true,
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

        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new ArgumentException(
                "Product name is required.");

        if (dto.PriceCents <= 0)
            throw new ArgumentException(
                "Price must be greater than zero.");

        var category = await _context.Categories
            .FindAsync(dto.CategoryId);

        if (category == null)
            throw new ArgumentException(
                "Category does not exist.");

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

        _repository.Delete(product);
        await _repository.SaveChangesAsync();

        return true;
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
            CreatedAt = product.CreatedAt
        };
    }
}