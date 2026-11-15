using AuraCafé.API.Models;

namespace AuraCafé.API.Interfaces;

public interface IInventoryRepository
{
    IQueryable<Ingredient> Ingredients { get; }
    IQueryable<StockMovement> Movements { get; }
    IQueryable<ProductIngredient> Recipes { get; }
    Task<Ingredient?> FindAsync(int id);
    void Add(Ingredient ingredient);
    void Add(StockMovement movement);
    void Add(ProductIngredient recipe);
    void Remove(ProductIngredient recipe);
    Task SaveAsync();
}
