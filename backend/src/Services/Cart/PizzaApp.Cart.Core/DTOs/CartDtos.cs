namespace PizzaApp.Cart.Core.DTOs;

public class CartItemDto
{
    public string ProductId { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string Size { get; set; } = "M";
}

public class CartResultDto
{
    public string Id { get; set; } = string.Empty;
    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string ProductImageUrl { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal Price { get; set; }
    public string Size { get; set; } = "M";
    public decimal TotalLinePrice => Price * Quantity;
}
