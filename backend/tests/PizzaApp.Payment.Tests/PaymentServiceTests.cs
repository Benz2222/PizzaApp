using PizzaApp.Payment.Infrastructure.Gateways;
using Xunit;

namespace PizzaApp.Payment.Tests;

public class PaymentServiceTests
{
    [Fact]
    public void MockGateway_ProducesScannableQrDataUri()
    {
        var gw = new MockPaymentGateway();

        var result = gw.CreateQr("code123", 50000m, "http://192.168.1.10:8080/api/payment/confirm/code123");

        Assert.Equal("http://192.168.1.10:8080/api/payment/confirm/code123", result.CheckoutUrl);
        Assert.StartsWith("data:image/png;base64,", result.QrCodeDataUri);
        Assert.True(result.QrCodeDataUri.Length > 100); // có nội dung ảnh
    }
}
