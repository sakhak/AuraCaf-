using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace AuraCafé.Web.Models;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public class IngredientFormDto
{
    [Required, StringLength(150, MinimumLength = 2)] public string Name { get; set; } = "";
    [Range(typeof(long), "0", "9223372036854775807")] public long MinimumQuantity { get; set; }
    public bool IsActive { get; set; } = true;
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public class CreateIngredientDto : IngredientFormDto
{
    [Required, RegularExpression("^(g|ml|pcs)$")] public string Unit { get; set; } = "g";
}

public class IngredientDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Unit { get; set; } = "";
    public long QuantityOnHand { get; set; }
    public long MinimumQuantity { get; set; }
    public bool IsActive { get; set; }
    public string StockStatus => !IsActive ? "Inactive" : QuantityOnHand == 0 ? "Out of stock" : QuantityOnHand <= MinimumQuantity ? "Low stock" : "Healthy";
}

public class IngredientPageDto
{
    public List<IngredientDto> Items { get; set; } = [];
    public int TotalCount { get; set; }
}

public class InventorySummaryDto
{
    public int Ingredients { get; set; }
    public int ActiveIngredients { get; set; }
    public int LowStock { get; set; }
    public int OutOfStock { get; set; }
    public int Movements { get; set; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public class CreateMovementDto
{
    public Guid RequestId { get; set; }
    [Required, RegularExpression("^(StockIn|StockOut|Adjustment)$")] public string Type { get; set; } = "StockIn";
    // Positive for stock-in/out. Signed delta for an adjustment; never a replacement balance.
    public long Quantity { get; set; }
    [Required, StringLength(500, MinimumLength = 3)] public string Reason { get; set; } = "";
}

public class StockMovementDto
{
    public int Id { get; set; }
    public Guid RequestId { get; set; }
    public int IngredientId { get; set; }
    public string IngredientName { get; set; } = "";
    public string Unit { get; set; } = "";
    public string Type { get; set; } = "";
    public long QuantityChange { get; set; }
    public long BalanceBefore { get; set; }
    public long BalanceAfter { get; set; }
    public string Reason { get; set; } = "";
    public string PerformedByUserId { get; set; } = "";
    public string PerformedByName { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; }
}

public class MovementPageDto
{
    public List<StockMovementDto> Items { get; set; } = [];
    public int TotalCount { get; set; }
}

public class RecipeLineDto
{
    [Range(1, int.MaxValue)] public int IngredientId { get; set; }
    [Range(typeof(long), "1", "9223372036854775807")] public long QuantityRequired { get; set; }
}

public class SaveRecipeDto
{
    [Required, MaxLength(50)] public List<RecipeLineDto> Lines { get; set; } = [];
}

public class RecipeIngredientDto : RecipeLineDto
{
    public string Name { get; set; } = "";
    public string Unit { get; set; } = "";
    public bool IsActive { get; set; }
}

public class ProductRecipeDto
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public List<RecipeIngredientDto> Lines { get; set; } = [];
}
