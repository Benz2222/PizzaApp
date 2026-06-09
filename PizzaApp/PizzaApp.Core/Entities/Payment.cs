using System;

namespace PizzaApp.Core.Entities;

public class Payment
{
    public string Id { get; set; } = string.Empty;
    public string OrderId { get; set; } = string.Empty;
    public long PayOSOrderCode { get; set; } // Mã đơn hàng phía PayOS (kiểu long)
    public decimal Amount { get; set; }
    public string Status { get; set; } = "PENDING"; // PENDING, PAID, CANCELLED
    public string CheckoutUrl { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
