namespace PizzaApp.Category.Core.DTOs;

public class CategoryDTO
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public class CategoryStatsDto
{
    public int TotalCategories { get; set; }
}
