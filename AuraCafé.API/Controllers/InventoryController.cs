using System.Security.Claims;
using AuraCafé.API.DTOs.Inventory;
using AuraCafé.API.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AuraCafé.API.Controllers;

[ApiController, Authorize(Roles = "Admin"), Route("api/inventory")]
public class InventoryController(IInventoryService service, ILogger<InventoryController> logger) : ControllerBase
{
    private async Task<IActionResult> ExecuteAsync(Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (OverflowException) { return BadRequest(new { message = "The quantity exceeds the supported whole-number range." }); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
        catch (Exception ex) when (ex is DbUpdateException or SqliteException)
        {
            logger.LogWarning(ex, "Inventory database conflict");
            return Conflict(new { message = "The inventory transaction could not be confirmed. Refresh the ledger before retrying with the same request ID." });
        }
    }

    [HttpGet("summary")] public Task<IActionResult> Summary() => ExecuteAsync(async () => Ok(await service.SummaryAsync()));
    [HttpGet("ingredients")] public Task<IActionResult> List(string? search, bool? active, bool lowOnly = false, int page = 1) => ExecuteAsync(async () => Ok(await service.ListAsync(search, active, lowOnly, page)));
    [HttpGet("ingredients/{id:int}")] public Task<IActionResult> Get(int id) => ExecuteAsync(async () => await service.GetAsync(id) is { } value ? Ok(value) : NotFound());
    [HttpPost("ingredients")] public Task<IActionResult> Create(CreateIngredientDto request) => ExecuteAsync(async () =>
    {
        var value = await service.CreateAsync(request);
        return CreatedAtAction(nameof(Get), new { id = value.Id }, value);
    });
    [HttpPut("ingredients/{id:int}")] public Task<IActionResult> Update(int id, IngredientFormDto request) => ExecuteAsync(async () => await service.UpdateAsync(id, request) ? NoContent() : NotFound());
    [HttpDelete("ingredients/{id:int}")] public Task<IActionResult> Deactivate(int id) => ExecuteAsync(async () => await service.DeactivateAsync(id) ? NoContent() : NotFound());
    [HttpPost("ingredients/{id:int}/movements")] public Task<IActionResult> Move(int id, CreateMovementDto request) => ExecuteAsync(async () =>
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return userId is null ? Unauthorized() : Ok(await service.MoveAsync(id, userId, request));
    });
    [HttpGet("movements")] public Task<IActionResult> Movements(int? ingredientId, int page = 1) => ExecuteAsync(async () => Ok(await service.MovementsAsync(ingredientId, page)));
    [HttpGet("products/{id:int}/recipe")] public Task<IActionResult> Recipe(int id) => ExecuteAsync(async () => await service.RecipeAsync(id) is { } value ? Ok(value) : NotFound());
    [HttpPut("products/{id:int}/recipe")] public Task<IActionResult> SaveRecipe(int id, SaveRecipeDto request) => ExecuteAsync(async () => await service.SaveRecipeAsync(id, request) ? NoContent() : NotFound());
}
