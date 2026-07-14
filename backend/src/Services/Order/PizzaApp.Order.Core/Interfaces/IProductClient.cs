namespace PizzaApp.Order.Core.Interfaces;

public record ProductInfo(string Id, string Name, string ImageUrl, decimal Price);

public interface IProductClient
{
    Task<ProductInfo?> GetProductAsync(string productId);
}
