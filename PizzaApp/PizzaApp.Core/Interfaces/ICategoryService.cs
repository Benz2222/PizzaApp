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

        Task<CategoryDTO?> GetByIdAsync(int id);

        Task<CategoryDTO> CreateAsync(CreateCategoryDTO dto);

        Task<bool> UpdateAsync(int id, UpdateCategoryDTO dto);

        Task<bool> DeleteAsync(int id);
    }
}
