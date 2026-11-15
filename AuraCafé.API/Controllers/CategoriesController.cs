using AuraCafé.API.Data;
using AuraCafé.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace AuraCafé.API.Controllers;

[ApiController, Authorize, Route("api/categories")]
public class CategoriesController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll() => Ok(await db.Categories.AsNoTracking().OrderBy(c => c.Name).ThenBy(c => c.Id)
        .Select(c => new { c.Id, c.Name }).ToListAsync());

    [HttpGet("manage"), Authorize(Roles = "Admin")]
    public async Task<IActionResult> Manage(string? search, int page = 1)
    {
        if (page is < 1 or > 1000000 || search?.Length > 100) return BadRequest(new { message = "Invalid search or page number." });
        var query = db.Categories.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToUpper();
            query = query.Where(c => c.Name.ToUpper().Contains(term));
        }
        return Ok(new
        {
            Items = await query.OrderBy(c => c.Name).ThenBy(c => c.Id).Skip((page - 1) * 12).Take(12)
                .Select(c => new { c.Id, c.Name, ProductCount = c.Products.Count }).ToListAsync(),
            TotalCount = await query.CountAsync(),
            CategoryCount = await db.Categories.CountAsync(),
            AssignedProducts = await db.Products.CountAsync(),
            EmptyCategories = await db.Categories.CountAsync(c => !c.Products.Any())
        });
    }

    [HttpGet("{id:int}"), Authorize(Roles = "Admin")]
    public async Task<IActionResult> Get(int id)
    {
        var category = await db.Categories.AsNoTracking().Where(c => c.Id == id)
            .Select(c => new { c.Id, c.Name, ProductCount = c.Products.Count }).SingleOrDefaultAsync();
        return category is null ? NotFound() : Ok(category);
    }

    private static bool Valid(string? name) => !string.IsNullOrWhiteSpace(name) && name.Trim().Length <= 100;

    [HttpPost, Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create(CreateCategoryRequest request)
    {
        if (!Valid(request.Name)) return BadRequest(new { message = "Category name must be 1-100 characters." });
        var name = request.Name.Trim();
        await using var transaction = await db.Database.BeginTransactionAsync();
        if (await db.Categories.AnyAsync(c => c.Name.ToUpper() == name.ToUpper())) return Conflict(new { message = "A category with this name already exists." });
        var category = new Category { Name = name };
        db.Categories.Add(category);
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException) { return Conflict(new { message = "Could not create category. Refresh and check for an existing name." }); }
        await transaction.CommitAsync();
        return CreatedAtAction(nameof(Get), new { id = category.Id }, new { category.Id, category.Name });
    }

    [HttpPut("{id:int}"), Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(int id, CreateCategoryRequest request)
    {
        if (!Valid(request.Name)) return BadRequest(new { message = "Category name must be 1-100 characters." });
        await using var transaction = await db.Database.BeginTransactionAsync();
        var category = await db.Categories.FindAsync(id);
        if (category is null) return NotFound();
        var name = request.Name.Trim();
        if (await db.Categories.AnyAsync(c => c.Id != id && c.Name.ToUpper() == name.ToUpper())) return Conflict(new { message = "A category with this name already exists." });
        category.Name = name;
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException) { return Conflict(new { message = "Could not rename category. Refresh and check for a duplicate name." }); }
        await transaction.CommitAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}"), Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var category = await db.Categories.FindAsync(id);
        if (category is null) return NotFound();
        if (await db.Products.AnyAsync(p => p.CategoryId == id)) return Conflict(new { message = "This category contains products. Reassign those products before deleting it." });
        db.Categories.Remove(category);
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException) { return Conflict(new { message = "Category is still referenced and cannot be deleted. Refresh the list." }); }
        return NoContent();
    }
}

public class CreateCategoryRequest
{
    [Required, StringLength(100)] public string Name { get; set; } = "";
}
