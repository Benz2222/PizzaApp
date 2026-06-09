using System;
using System.Collections.Generic;

namespace PizzaApp.Core.Entities;

public class Order
{
    public string Id { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public List<OrderItem> OrderItems { get; set; } = new();
    public decimal TotalPrice { get; set; }
    public string Status { get; set; } = "Pending"; // Pending, Preparing, Delivering, Done, Cancelled
    public string PaymentStatus { get; set; } = "Unpaid"; // Unpaid, Paid
    public string PaymentMethod { get; set; } = "COD"; // COD, BankTransfer
    public string DeliveryAddress { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class OrderItem
{
    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty; // Lưu tên để tránh phải Join
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public string Size { get; set; } = "M";
}
