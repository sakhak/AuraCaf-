using AuraCafé.API.Data;
using AuraCafé.API.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuraCafé.API.Controllers;

[ApiController]
[Route("api/categories")]
public class CategoriesController : ControllerBase
{
    private readonly AppDbContext _context;

    public CategoriesController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var categories = await _context.Categories
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new { c.Id, c.Name })
            .ToListAsync();

        return Ok(categories);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateCategoryRequest request)
    {
        var name = request.Name?.Trim();

        if (string.IsNullOrWhiteSpace(name)
            || name.Length > 100)
        {
            return BadRequest(new
            {
                message = "Category name must be 1-100 characters."
            });
        }

        var exists = await _context.Categories
            .AnyAsync(c => c.Name == name);

        if (exists)
        {
            return Conflict(new
            {
                message = "Category already exists."
            });
        }

        var category = new Category { Name = name };

        _context.Categories.Add(category);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return Conflict(new
            {
                message = "Could not create category. It may already exist."
            });
        }

        return Created(
            $"/api/categories/{category.Id}",
            new { category.Id, category.Name });
    }
}

public class CreateCategoryRequest
{
    public string Name { get; set; } = "";
}