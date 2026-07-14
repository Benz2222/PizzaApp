namespace PizzaApp.Payment.Core.Entities;

public class Payment
{
    public string Id { get; set; } = string.Empty;
    public string OrderId { get; set; } = string.Empty;
    public string PaymentCode { get; set; } = string.Empty; // mã trong URL confirm/QR
    public decimal Amount { get; set; }
    public string Status { get; set; } = "PENDING"; // PENDING, PAID
    public string CheckoutUrl { get; set; } = string.Empty;
    public string QrCodeDataUri { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
