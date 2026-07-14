using System.Net;
using System.Net.Http.Json;
using PizzaApp.Product.Core.Interfaces;

namespace PizzaApp.Product.Infrastructure.Clients;

public class CategoryHttpClient : ICategoryClient
{
    private readonly HttpClient _http;

    public CategoryHttpClient(HttpClient http) => _http = http;

    public async Task<string?> GetCategoryNameAsync(string categoryId)
    {
        var response = await _http.GetAsync($"api/category/{categoryId}");
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<CategoryNameDto>();
        return dto?.Name;
    }

    private class CategoryNameDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }
}
