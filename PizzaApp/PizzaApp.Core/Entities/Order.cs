using System;
using System.Collections.Generic;

namespace PizzaApp.Core.Entities;

public class Order
{
    public string Id { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public List<OrderItem> OrderItems { get; set; } = new();
    public decimal TotalPrice { get; set; }
    // AwaitingPayment -> Paid -> Preparing -> Ready -> Delivering -> Done / Cancelled
    public string Status { get; set; } = "AwaitingPayment";
    public string PaymentStatus { get; set; } = "Unpaid"; // Unpaid, Paid
    public string PaymentMethod { get; set; } = "COD"; // COD, BankTransfer
    public string DeliveryAddress { get; set; } = string.Empty;
    public string ShipperId { get; set; } = string.Empty; // shipper nhận đơn (rỗng = chưa ai nhận)
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class OrderItem
{
    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty; // Lưu tên để tránh phải Join
    public string ProductImageUrl { get; set; } = string.Empty; // Lưu ảnh để hiển thị lịch sử đơn
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public string Size { get; set; } = "M";
}
