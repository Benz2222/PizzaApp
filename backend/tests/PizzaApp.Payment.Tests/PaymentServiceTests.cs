using PizzaApp.Payment.Core.Interfaces;
using PizzaApp.Payment.Infrastructure.Gateways;
using Xunit;

namespace PizzaApp.Payment.Tests;

public class PaymentServiceTests
{
    [Fact]
    public async Task MockGateway_ProducesScannableQrDataUri()
    {
        var gw = new MockPaymentGateway();
        var url = "http://192.168.1.10:8080/api/payment/confirm/code123";

        var result = await gw.CreateAsync("code123", 50000m, new List<GatewayItem>(), url);

        Assert.Equal(url, result.CheckoutUrl);
        Assert.StartsWith("data:image/png;base64,", result.QrCodeDataUri);
        Assert.True(result.QrCodeDataUri.Length > 100);
        Assert.Equal(0, result.ProviderCode); // mock không có mã cổng
    }
}
