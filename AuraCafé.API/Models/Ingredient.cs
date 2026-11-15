namespace AuraCafé.API.Models;

public class Ingredient
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string NormalizedName { get; set; } = "";
    public string Unit { get; set; } = "g";
    public long QuantityOnHand { get; set; }
    public long MinimumQuantity { get; set; }
    public bool IsActive { get; set; } = true;
}
