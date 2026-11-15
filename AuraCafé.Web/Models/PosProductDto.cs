
namespace AuraCafé.Web.Models;

public class PosProductDto
{
    public string ImageUrl { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public int PriceCents { get; set; }
    public bool IsAvailable { get; set; }
    public int CategoryId { get; set; }
}

public class PosCategoryDto
{
    public bool IsActive { get; set; } = true;
    public int Id { get; set; }
    public string Name { get; set; } = "";
}
