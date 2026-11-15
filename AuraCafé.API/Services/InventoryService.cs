using AuraCafé.API.Data;
using AuraCafé.API.DTOs.Inventory;
using AuraCafé.API.Interfaces;
using AuraCafé.API.Models;
using Microsoft.EntityFrameworkCore;

namespace AuraCafé.API.Services;

public class InventoryService(IInventoryRepository repository, AppDbContext db) : IInventoryService
{
    private static void ValidatePage(int page)
    {
        if (page is < 1 or > 1000000) throw new ArgumentException("Invalid page number.");
    }
    private static void Validate(IngredientFormDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length is < 2 or > 150 || request.MinimumQuantity < 0)
            throw new ArgumentException("Use a 2-150 character ingredient name and a nonnegative whole-number minimum.");
    }
    private static IngredientDto ToDto(Ingredient i) => new()
    {
        Id = i.Id, Name = i.Name, Unit = i.Unit, QuantityOnHand = i.QuantityOnHand, MinimumQuantity = i.MinimumQuantity, IsActive = i.IsActive
    };

    public async Task<IngredientPageDto> ListAsync(string? search, bool? active, bool lowOnly, int page)
    {
        ValidatePage(page);
        var query = repository.Ingredients.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            if (search.Length > 150) throw new ArgumentException("Search cannot exceed 150 characters.");
            var term = search.Trim().ToUpperInvariant();
            query = query.Where(i => i.NormalizedName.Contains(term));
        }
        if (active.HasValue) query = query.Where(i => i.IsActive == active);
        if (lowOnly) query = query.Where(i => i.IsActive && i.QuantityOnHand <= i.MinimumQuantity);
        var total = await query.CountAsync();
        var rows = await query.OrderBy(i => i.Name).ThenBy(i => i.Id).Skip((page - 1) * 20).Take(20).ToListAsync();
        return new IngredientPageDto { Items = rows.Select(ToDto).ToList(), TotalCount = total };
    }

    public async Task<InventorySummaryDto> SummaryAsync() => new()
    {
        Ingredients = await repository.Ingredients.CountAsync(),
        ActiveIngredients = await repository.Ingredients.CountAsync(i => i.IsActive),
        LowStock = await repository.Ingredients.CountAsync(i => i.IsActive && i.QuantityOnHand > 0 && i.QuantityOnHand <= i.MinimumQuantity),
        OutOfStock = await repository.Ingredients.CountAsync(i => i.IsActive && i.QuantityOnHand == 0),
        Movements = await repository.Movements.CountAsync()
    };

    public async Task<IngredientDto?> GetAsync(int id)
    {
        var ingredient = await repository.Ingredients.AsNoTracking().SingleOrDefaultAsync(i => i.Id == id);
        return ingredient is null ? null : ToDto(ingredient);
    }

    public async Task<IngredientDto> CreateAsync(CreateIngredientDto request)
    {
        Validate(request);
        if (request.Unit is not ("g" or "ml" or "pcs")) throw new ArgumentException("Choose g, ml or pcs. Convert larger units before entering quantities.");
        var normalized = request.Name.Trim().ToUpperInvariant();
        if (await repository.Ingredients.AnyAsync(i => i.NormalizedName == normalized)) throw new InvalidOperationException("An ingredient with this name already exists.");
        var ingredient = new Ingredient { Name = request.Name.Trim(), NormalizedName = normalized, Unit = request.Unit, MinimumQuantity = request.MinimumQuantity, IsActive = request.IsActive };
        repository.Add(ingredient);
        await repository.SaveAsync();
        return ToDto(ingredient);
    }

    public async Task<bool> UpdateAsync(int id, IngredientFormDto request)
    {
        Validate(request);
        var ingredient = await repository.FindAsync(id);
        if (ingredient is null) return false;
        var normalized = request.Name.Trim().ToUpperInvariant();
        if (await repository.Ingredients.AnyAsync(i => i.Id != id && i.NormalizedName == normalized)) throw new InvalidOperationException("An ingredient with this name already exists.");
        ingredient.Name = request.Name.Trim();
        ingredient.NormalizedName = normalized;
        ingredient.MinimumQuantity = request.MinimumQuantity;
        ingredient.IsActive = request.IsActive;
        await repository.SaveAsync();
        return true;
    }

    public async Task<bool> DeactivateAsync(int id)
    {
        var ingredient = await repository.FindAsync(id);
        if (ingredient is null) return false;
        ingredient.IsActive = false;
        await repository.SaveAsync();
        return true;
    }

    public async Task<StockMovementDto> MoveAsync(int ingredientId, string userId, CreateMovementDto request)
    {
        if (request.RequestId == Guid.Empty || request.Quantity == 0 || string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length is < 3 or > 500)
            throw new ArgumentException("A request ID, nonzero whole-number quantity and 3-500 character reason are required.");
        if (request.Type is not ("StockIn" or "StockOut" or "Adjustment") || (request.Type != "Adjustment" && request.Quantity < 0))
            throw new ArgumentException("Stock-in/out quantities must be positive. Adjustments use a signed quantity change.");
        var delta = request.Type == "StockOut" ? -request.Quantity : request.Quantity;
        await using var transaction = await db.Database.BeginTransactionAsync();
        var previous = await repository.Movements.Include(m => m.Ingredient).Include(m => m.PerformedByUser).SingleOrDefaultAsync(m => m.RequestId == request.RequestId);
        if (previous is not null)
        {
            if (previous.IngredientId != ingredientId || previous.PerformedByUserId != userId || previous.Type != request.Type || previous.QuantityChange != delta || previous.Reason != request.Reason.Trim())
                throw new InvalidOperationException("This request ID was already used for a different stock transaction.");
            return ToDto(previous);
        }
        var ingredient = await repository.FindAsync(ingredientId) ?? throw new KeyNotFoundException();
        if (!ingredient.IsActive) throw new InvalidOperationException("Reactivate this ingredient before recording stock changes.");
        var ledgerBalance = await repository.Movements.Where(m => m.IngredientId == ingredientId).OrderByDescending(m => m.Id).Select(m => (long?)m.BalanceAfter).FirstOrDefaultAsync() ?? 0;
        if (ledgerBalance != ingredient.QuantityOnHand) throw new InvalidOperationException("The stock balance does not match its ledger. Review the records before continuing.");
        var after = checked(ingredient.QuantityOnHand + delta);
        if (after < 0) throw new InvalidOperationException("Insufficient stock. This transaction would make the balance negative.");
        var movement = new StockMovement
        {
            RequestId = request.RequestId, IngredientId = ingredient.Id, Ingredient = ingredient, Type = request.Type,
            QuantityChange = delta, BalanceBefore = ingredient.QuantityOnHand, BalanceAfter = after, Reason = request.Reason.Trim(),
            PerformedByUserId = userId, CreatedAtUtc = DateTime.UtcNow
        };
        ingredient.QuantityOnHand = after;
        repository.Add(movement);
        await repository.SaveAsync();
        await transaction.CommitAsync();
        await db.Entry(movement).Reference(m => m.PerformedByUser).LoadAsync();
        return ToDto(movement);
    }

    private static StockMovementDto ToDto(StockMovement m) => new()
    {
        Id = m.Id, RequestId = m.RequestId, IngredientId = m.IngredientId, IngredientName = m.Ingredient.Name, Unit = m.Ingredient.Unit,
        Type = m.Type, QuantityChange = m.QuantityChange, BalanceBefore = m.BalanceBefore, BalanceAfter = m.BalanceAfter,
        Reason = m.Reason, PerformedByUserId = m.PerformedByUserId, PerformedByName = m.PerformedByUser.FullName, CreatedAtUtc = m.CreatedAtUtc
    };

    public async Task<MovementPageDto> MovementsAsync(int? ingredientId, int page)
    {
        ValidatePage(page);
        if (ingredientId <= 0) throw new ArgumentException("Invalid ingredient.");
        var query = repository.Movements.AsNoTracking();
        if (ingredientId.HasValue) query = query.Where(m => m.IngredientId == ingredientId);
        var total = await query.CountAsync();
        var rows = await query.Include(m => m.Ingredient).Include(m => m.PerformedByUser).OrderByDescending(m => m.Id).Skip((page - 1) * 20).Take(20).ToListAsync();
        return new MovementPageDto { TotalCount = total, Items = rows.Select(ToDto).ToList() };
    }

    public async Task<ProductRecipeDto?> RecipeAsync(int productId)
    {
        var product = await db.Products.AsNoTracking().SingleOrDefaultAsync(p => p.Id == productId);
        if (product is null) return null;
        return new ProductRecipeDto
        {
            ProductId = product.Id, ProductName = product.Name,
            Lines = await repository.Recipes.AsNoTracking().Where(r => r.ProductId == productId).OrderBy(r => r.Ingredient.Name).Select(r => new RecipeIngredientDto
            {
                IngredientId = r.IngredientId, Name = r.Ingredient.Name, Unit = r.Ingredient.Unit, QuantityRequired = r.QuantityRequired, IsActive = r.Ingredient.IsActive
            }).ToListAsync()
        };
    }

    public async Task<bool> SaveRecipeAsync(int productId, SaveRecipeDto request)
    {
        if (request.Lines is null || request.Lines.Count > 50 || request.Lines.Any(l => l.IngredientId <= 0 || l.QuantityRequired <= 0) || request.Lines.Select(l => l.IngredientId).Distinct().Count() != request.Lines.Count)
            throw new ArgumentException("Use at most 50 distinct ingredients, each with a positive whole-number quantity.");
        await using var transaction = await db.Database.BeginTransactionAsync();
        if (!await db.Products.AnyAsync(p => p.Id == productId)) return false;
        var ids = request.Lines.Select(l => l.IngredientId).ToList();
        if (await repository.Ingredients.CountAsync(i => ids.Contains(i.Id) && i.IsActive) != ids.Count)
            throw new ArgumentException("Every recipe ingredient must exist and be active.");
        var existing = await repository.Recipes.Where(r => r.ProductId == productId).ToListAsync();
        foreach (var line in existing.Where(r => !ids.Contains(r.IngredientId))) repository.Remove(line);
        foreach (var line in request.Lines)
        {
            var saved = existing.SingleOrDefault(r => r.IngredientId == line.IngredientId);
            if (saved is not null) saved.QuantityRequired = line.QuantityRequired;
            else repository.Add(new ProductIngredient { ProductId = productId, IngredientId = line.IngredientId, QuantityRequired = line.QuantityRequired });
        }
        await repository.SaveAsync();
        await transaction.CommitAsync();
        return true;
    }
}
