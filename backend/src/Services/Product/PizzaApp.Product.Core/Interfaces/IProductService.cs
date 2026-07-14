using PizzaApp.Product.Core.DTOs;

namespace PizzaApp.Product.Core.Interfaces;

public interface IProductService
{
    Task<List<ProductDto>> GetAllAsync(string? search, string? categoryId, int page, int pageSize);
    Task<ProductDto?> GetByIdAsync(string id);
    Task<ProductDto> CreateAsync(CreateProductDTO dto);
    Task<bool> UpdateAsync(string id, UpdateProductDTO dto);
    Task<bool> DeleteAsync(string id);
}
