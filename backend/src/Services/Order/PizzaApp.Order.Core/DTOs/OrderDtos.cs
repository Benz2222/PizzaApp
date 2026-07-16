namespace PizzaApp.Order.Core.DTOs;

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

public class OrderResultDto
{
    public string Id { get; set; } = string.Empty;
    public decimal TotalPrice { get; set; }
    public string Status { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = "Unpaid";
    public string PaymentUrl { get; set; } = string.Empty;
    public string PaymentQr { get; set; } = string.Empty;
    public string DeliveryAddress { get; set; } = string.Empty;
    public string ShipperId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public List<OrderItemResultDto> Items { get; set; } = new();
}

public class OrderItemResultDto
{
    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string ProductImageUrl { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public string Size { get; set; } = "M";
}

public class TopProductDto
{
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal Revenue { get; set; }
}

public class OrderStatsDto
{
    public decimal RevenueToday { get; set; }
    public decimal RevenueTotal { get; set; }
    public int OrdersToday { get; set; }
    public int OrdersTotal { get; set; }
    public Dictionary<string, int> ByStatus { get; set; } = new();
    public List<TopProductDto> TopProducts { get; set; } = new();
}
