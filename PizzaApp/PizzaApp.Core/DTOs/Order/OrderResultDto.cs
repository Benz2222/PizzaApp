using System;
using System.Collections.Generic;

namespace PizzaApp.Core.DTOs.Order;

public class OrderResultDto
{
    public string Id { get; set; } = string.Empty;
    public decimal TotalPrice { get; set; }
    public string Status { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = "Unpaid";
    public string PaymentUrl { get; set; } = string.Empty;
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
