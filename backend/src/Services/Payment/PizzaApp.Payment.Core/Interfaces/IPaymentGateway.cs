namespace PizzaApp.Payment.Core.Interfaces;

public record GatewayItem(string Name, int Quantity, int Price);

/// <param name="CheckoutUrl">Link để mở trang thanh toán (app launchUrl).</param>
/// <param name="QrCodeDataUri">Ảnh QR (data URI PNG) để app hiển thị.</param>
/// <param name="ProviderCode">Mã giao dịch phía cổng (PayOS orderCode) — dùng khớp webhook. 0 nếu mock.</param>
public record PaymentCreation(string CheckoutUrl, string QrCodeDataUri, long ProviderCode);

public interface IPaymentGateway
{
    /// <summary>Tạo giao dịch + QR. Trừu tượng: Mock (giả lập) hoặc PayOS (tiền thật).</summary>
    Task<PaymentCreation> CreateAsync(string paymentCode, decimal amount, List<GatewayItem> items, string confirmUrl);
}
