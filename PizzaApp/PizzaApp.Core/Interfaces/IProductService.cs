using System.Collections.Generic;
using System.Threading.Tasks;

using PizzaApp.Core.DTOs.Product;

namespace PizzaApp.Core.Interfaces;

public interface IProductService
{
    Task<List<ProductDto>> GetAllAsync();
    Task<ProductDto?> GetByIdAsync(string id);
    Task<ProductDto> CreateAsync(CreateProductDTO dto);
    Task<bool> UpdateAsync(string id, UpdateProductDTO dto);
    Task<bool> DeleteAsync(string id);
}
