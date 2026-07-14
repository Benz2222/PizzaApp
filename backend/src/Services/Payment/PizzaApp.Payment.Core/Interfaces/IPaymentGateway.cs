namespace PizzaApp.Payment.Core.Interfaces;

public record PaymentCreation(string CheckoutUrl, string QrCodeDataUri);

public interface IPaymentGateway
{
    /// <summary>Sinh URL xác nhận + ảnh QR (data URI). Trừu tượng — mock hoặc cổng thật.</summary>
    PaymentCreation CreateQr(string paymentCode, decimal amount, string confirmUrl);
}
