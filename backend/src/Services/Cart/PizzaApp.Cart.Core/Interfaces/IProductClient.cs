namespace PizzaApp.Cart.Core.Interfaces;

public record ProductInfo(string Id, string Name, string ImageUrl, decimal Price);

public interface IProductClient
{
    /// <summary>Trả thông tin sản phẩm, hoặc null nếu không tồn tại.</summary>
    Task<ProductInfo?> GetProductAsync(string productId);
}
