using Net.payOS;
using Net.payOS.Types;
using QRCoder;
using PizzaApp.Payment.Core.Interfaces;

namespace PizzaApp.Payment.Infrastructure.Gateways;

/// <summary>Cổng thanh toán THẬT — PayOS. Quét QR = chuyển khoản tiền thật.</summary>
public class PayOSPaymentGateway : IPaymentGateway
{
    private readonly PayOS _payOS;
    private readonly PaymentSettingsPayOS _settings;

    public PayOSPaymentGateway(PayOS payOS, PaymentSettingsPayOS settings)
    {
        _payOS = payOS;
        _settings = settings;
    }

    public async Task<PaymentCreation> CreateAsync(string paymentCode, decimal amount,
        List<GatewayItem> items, string confirmUrl)
    {
        // orderCode phải là số & duy nhất với PayOS
        long orderCode = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var payItems = items.Select(i => new ItemData(i.Name, i.Quantity, i.Price)).ToList();
        if (payItems.Count == 0)
            payItems.Add(new ItemData("Don hang PizzaApp", 1, (int)amount));

        var data = new PaymentData(
            orderCode: orderCode,
            amount: (int)amount,
            description: "Thanh toan PizzaApp",
            items: payItems,
            returnUrl: _settings.ReturnUrl,
            cancelUrl: _settings.CancelUrl);

        CreatePaymentResult result = await _payOS.createPaymentLink(data);

        // result.qrCode là chuỗi VietQR (chuẩn EMVCo, Napas247) -> render thành ảnh PNG
        // để app hiển thị. Quét bằng app NGÂN HÀNG (không dùng ví như MoMo - không hỗ trợ QRIBFTTA).
        var qrDataUri = RenderQr(result.qrCode);

        return new PaymentCreation(result.checkoutUrl, qrDataUri, orderCode);
    }

    private static string RenderQr(string payload)
    {
        if (string.IsNullOrEmpty(payload)) return string.Empty;
        using var gen = new QRCodeGenerator();
        using var data = gen.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
        var png = new PngByteQRCode(data).GetGraphic(20);
        return "data:image/png;base64," + Convert.ToBase64String(png);
    }
}

public class PaymentSettingsPayOS
{
    public string ClientId { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string ChecksumKey { get; set; } = string.Empty;
    public string ReturnUrl { get; set; } = "https://example.com/success";
    public string CancelUrl { get; set; } = "https://example.com/cancel";
}
