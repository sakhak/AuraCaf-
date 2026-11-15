namespace AuraCafé.Web.Models;

public class CategoryDto
{
    public int ProductCount { get; set; }
    public int Id { get; set; }
    public string Name { get; set; } = "";
}

public class CategoryPageDto
{
    public List<CategoryDto> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int CategoryCount { get; set; }
    public int AssignedProducts { get; set; }
    public int EmptyCategories { get; set; }
}
