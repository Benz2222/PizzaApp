namespace PizzaApp.Order.Core.Interfaces;

public record PaymentItem(string Name, int Quantity, int Price);
public record PaymentLink(string CheckoutUrl, string QrCode);

public interface IPaymentClient
{
    /// <summary>Tạo link + QR thanh toán cho đơn. Trả null nếu thất bại.</summary>
    Task<PaymentLink?> CreatePaymentAsync(string orderId, decimal amount, List<PaymentItem> items);
}
