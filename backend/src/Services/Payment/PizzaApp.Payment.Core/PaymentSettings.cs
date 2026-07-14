namespace PizzaApp.Payment.Core;

public class PaymentSettings
{
    // IP LAN của máy để điện thoại quét QR gọi vào (KHÔNG localhost). Vd http://192.168.1.10:8080
    public string PublicBaseUrl { get; set; } = "http://localhost:8080";
}
