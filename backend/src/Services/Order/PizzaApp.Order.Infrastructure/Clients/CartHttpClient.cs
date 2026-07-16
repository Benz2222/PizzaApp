using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using PizzaApp.Order.Core.Interfaces;

namespace PizzaApp.Order.Infrastructure.Clients;

public class CartHttpClient : ICartClient
{
    private readonly HttpClient _http;
    private readonly IHttpContextAccessor _ctx;

    public CartHttpClient(HttpClient http, IHttpContextAccessor ctx)
    {
        _http = http;
        _ctx = ctx;
    }

    public async Task<List<CartLine>> GetCartAsync()
    {
        var req = new HttpRequestMessage(HttpMethod.Get, "api/cart");
        var token = _ctx.HttpContext?.Request.Headers["Authorization"].ToString();
        if (!string.IsNullOrEmpty(token))
            req.Headers.TryAddWithoutValidation("Authorization", token);

        var resp = await _http.SendAsync(req);
        if (!resp.IsSuccessStatusCode) return new();
        var items = await resp.Content.ReadFromJsonAsync<List<CartResp>>();
        return items?.Select(i => new CartLine(i.ProductId, i.Quantity, i.Size)).ToList() ?? new();
    }

    private class CartResp
    {
        public string ProductId { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public string Size { get; set; } = "M";
    }
}
