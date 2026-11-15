using AuraCafé.API.Auth;

namespace AuraCafé.API.Models;

public class StockMovement
{
    public int Id { get; set; }
    public Guid RequestId { get; set; }
    public int IngredientId { get; set; }
    public Ingredient Ingredient { get; set; } = null!;
    public string Type { get; set; } = "";
    public long QuantityChange { get; set; }
    public long BalanceBefore { get; set; }
    public long BalanceAfter { get; set; }
    public string Reason { get; set; } = "";
    public string PerformedByUserId { get; set; } = "";
    public ApplicationUser PerformedByUser { get; set; } = null!;
    public DateTime CreatedAtUtc { get; set; }
}
