namespace PizzaApp.Product.Core.Interfaces;

public interface ICategoryClient
{
    /// <summary>Trả tên category, hoặc null nếu không tồn tại.</summary>
    Task<string?> GetCategoryNameAsync(string categoryId);
}
