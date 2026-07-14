using System.Net.Http.Json;
using PizzaApp.Order.Core.Interfaces;

namespace PizzaApp.Order.Infrastructure.Clients;

public class PaymentHttpClient : IPaymentClient
{
    private readonly HttpClient _http;
    public PaymentHttpClient(HttpClient http) => _http = http;

    public async Task<PaymentLink?> CreatePaymentAsync(string orderId, decimal amount, List<PaymentItem> items)
    {
        var body = new
        {
            orderId,
            amount,
            items = items.Select(i => new { i.Name, i.Quantity, i.Price })
        };
        var response = await _http.PostAsJsonAsync("api/payment/create", body);
        if (!response.IsSuccessStatusCode) return null;
        var dto = await response.Content.ReadFromJsonAsync<PaymentResponse>();
        return dto == null ? null : new PaymentLink(dto.CheckoutUrl, dto.QrCode);
    }

    private class PaymentResponse
    {
        public string CheckoutUrl { get; set; } = string.Empty;
        public string QrCode { get; set; } = string.Empty;
    }
}
