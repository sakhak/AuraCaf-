using AuraCafé.API.DTOs.Inventory;

namespace AuraCafé.API.Interfaces;

public interface IInventoryService
{
    Task<IngredientPageDto> ListAsync(string? search, bool? active, bool lowOnly, int page);
    Task<InventorySummaryDto> SummaryAsync();
    Task<IngredientDto?> GetAsync(int id);
    Task<IngredientDto> CreateAsync(CreateIngredientDto request);
    Task<bool> UpdateAsync(int id, IngredientFormDto request);
    Task<bool> DeactivateAsync(int id);
    Task<StockMovementDto> MoveAsync(int ingredientId, string userId, CreateMovementDto request);
    Task<MovementPageDto> MovementsAsync(int? ingredientId, int page);
    Task<ProductRecipeDto?> RecipeAsync(int productId);
    Task<bool> SaveRecipeAsync(int productId, SaveRecipeDto request);
}
