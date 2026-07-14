using QRCoder;
using PizzaApp.Payment.Core.Interfaces;

namespace PizzaApp.Payment.Infrastructure.Gateways;

public class MockPaymentGateway : IPaymentGateway
{
    public PaymentCreation CreateQr(string paymentCode, decimal amount, string confirmUrl)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(confirmUrl, QRCodeGenerator.ECCLevel.Q);
        var png = new PngByteQRCode(data).GetGraphic(20);
        var dataUri = "data:image/png;base64," + Convert.ToBase64String(png);
        return new PaymentCreation(confirmUrl, dataUri);
    }
}
