namespace PizzaApp.Order.Core.Entities;

public class Order
{
    public string Id { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public List<OrderItem> OrderItems { get; set; } = new();
    public decimal TotalPrice { get; set; }
    public string Status { get; set; } = "AwaitingPayment";
    public string PaymentStatus { get; set; } = "Unpaid";
    public string PaymentMethod { get; set; } = "QR";
    public string PaymentUrl { get; set; } = string.Empty;  // URL xác nhận (mã trong QR)
    public string PaymentQr { get; set; } = string.Empty;   // ảnh QR data URI (denormalized để hiển thị lại)
    public string DeliveryAddress { get; set; } = string.Empty;
    public string ShipperId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class OrderItem
{
    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string ProductImageUrl { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public string Size { get; set; } = "M";
}
