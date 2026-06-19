using PizzaApp.Core.DTOs.Category;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PizzaApp.Core.Interfaces
{
    public interface ICategoryService
    {
        Task<List<CategoryDTO>> GetAllAsync();

        Task<CategoryDTO?> GetByIdAsync(string id);

        Task<CategoryDTO> CreateAsync(CreateCategoryDTO dto);

        Task<bool> UpdateAsync(string id, UpdateCategoryDTO dto);

        Task<bool> DeleteAsync(string id);
    }
}
