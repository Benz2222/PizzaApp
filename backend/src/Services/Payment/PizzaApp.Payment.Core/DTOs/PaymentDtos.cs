namespace PizzaApp.Payment.Core.DTOs;

public class CreatePaymentDto
{
    public string OrderId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public List<PaymentItemDto> Items { get; set; } = new();
}

public class PaymentItemDto
{
    public string Name { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public int Price { get; set; }
}

public class PaymentView
{
    public string OrderId { get; set; } = string.Empty;
    public string CheckoutUrl { get; set; } = string.Empty;
    public string QrCodeDataUri { get; set; } = string.Empty;
    public string Status { get; set; } = "PENDING";
}
