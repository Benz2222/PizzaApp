using System.Net;
using System.Net.Http.Json;
using PizzaApp.Cart.Core.Interfaces;

namespace PizzaApp.Cart.Infrastructure.Clients;

public class ProductHttpClient : IProductClient
{
    private readonly HttpClient _http;

    public ProductHttpClient(HttpClient http) => _http = http;

    public async Task<ProductInfo?> GetProductAsync(string productId)
    {
        var response = await _http.GetAsync($"api/products/{productId}");
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<ProductResponse>();
        if (dto == null) return null;
        return new ProductInfo(dto.Id, dto.Name, dto.ImageUrl, dto.Price);
    }

    private class ProductResponse
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
        public decimal Price { get; set; }
    }
}
