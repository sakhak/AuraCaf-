using AuraCafé.API.DTOs.Products;
using AuraCafé.API.Interfaces.Products;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;


namespace AuraCafé.API.Controllers;

[ApiController]
[Authorize]
[Route("api/products")]
public class ProductsController : ControllerBase
{
    private readonly IProductService _service;

    public ProductsController(IProductService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<List<ProductResponseDto>>> GetAll()
    {
        var products = await _service.GetAllAsync();

        return Ok(products);
    }

    [Authorize(Roles = "Admin")]
    [HttpGet("manage")]
    public async Task<IActionResult> Search(string? search, int? categoryId, bool? available, bool? active, int page = 1, int pageSize = 12)
    {
        if (page < 1 || page > 1000000 || pageSize < 1 || pageSize > 100 || categoryId <= 0)
            return BadRequest(new { message = "Invalid category or pagination." });
        return Ok(await _service.SearchAsync(search, categoryId, available, active, page, pageSize));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ProductResponseDto>> GetById(
        int id)
    {
        var product = await _service.GetByIdAsync(id);

        if (product == null)
            return NotFound();

        return Ok(product);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<ActionResult<ProductResponseDto>> Create(
        CreateProductDto dto)
    {
        try
        {
            var product = await _service.CreateAsync(dto);

            return CreatedAtAction(
                nameof(GetById),
                new { id = product.Id },
                product);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(
        int id, UpdateProductDto dto)
    {
        try
        {
            var updated = await _service.UpdateAsync(id, dto);

            if (!updated)
                return NotFound();

            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var deleted = await _service.DeleteAsync(id);

        if (!deleted)
            return NotFound();

        return NoContent();
    }
}
