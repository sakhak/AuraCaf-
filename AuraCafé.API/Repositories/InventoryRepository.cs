using AuraCafé.API.Data;
using AuraCafé.API.Interfaces;
using AuraCafé.API.Models;

namespace AuraCafé.API.Repositories;

public class InventoryRepository(AppDbContext db) : IInventoryRepository
{
    public IQueryable<Ingredient> Ingredients => db.Ingredients;
    public IQueryable<StockMovement> Movements => db.StockMovements;
    public IQueryable<ProductIngredient> Recipes => db.ProductIngredients;
    public async Task<Ingredient?> FindAsync(int id) => await db.Ingredients.FindAsync(id);
    public void Add(Ingredient ingredient) => db.Ingredients.Add(ingredient);
    public void Add(StockMovement movement) => db.StockMovements.Add(movement);
    public void Add(ProductIngredient recipe) => db.ProductIngredients.Add(recipe);
    public void Remove(ProductIngredient recipe) => db.ProductIngredients.Remove(recipe);
    public Task SaveAsync() => db.SaveChangesAsync();
}
