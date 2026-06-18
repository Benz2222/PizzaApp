namespace PizzaApp.Core.DTOs.Order;

public class CreateOrderDto
{
    public string DeliveryAddress { get; set; } = string.Empty;
    public List<OrderItemDto> Items { get; set; } = new();
}

public class OrderItemDto
{
    public string ProductId { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string Size { get; set; } = "M";
}
