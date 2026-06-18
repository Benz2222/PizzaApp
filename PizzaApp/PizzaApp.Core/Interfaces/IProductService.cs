using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using PizzaApp.Core.DTOs.Product;

namespace PizzaApp.Core.Interfaces;

public interface IProductService
{
    Task<List<ProductDto>> GetAllAsync();
    Task<ProductDto?> GetByIdAsync(int id);

    Task<ProductDto> CreateAsync(CreateProductDTO dto);

    Task<bool> UpdateAsync(int id, UpdateProductDTO dto);

    Task<bool> DeleteAsync(int id);
}
