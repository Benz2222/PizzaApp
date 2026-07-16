using QRCoder;
using PizzaApp.Payment.Core.Interfaces;

namespace PizzaApp.Payment.Infrastructure.Gateways;

/// <summary>Cổng giả lập — QR trỏ tới trang xác nhận nội bộ, KHÔNG tiền thật. Dùng để demo offline.</summary>
public class MockPaymentGateway : IPaymentGateway
{
    public Task<PaymentCreation> CreateAsync(string paymentCode, decimal amount,
        List<GatewayItem> items, string confirmUrl)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(confirmUrl, QRCodeGenerator.ECCLevel.Q);
        var png = new PngByteQRCode(data).GetGraphic(20);
        var dataUri = "data:image/png;base64," + Convert.ToBase64String(png);
        return Task.FromResult(new PaymentCreation(confirmUrl, dataUri, 0));
    }
}
