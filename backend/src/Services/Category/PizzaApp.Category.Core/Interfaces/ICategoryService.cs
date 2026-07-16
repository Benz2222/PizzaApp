using PizzaApp.Category.Core.DTOs;

namespace PizzaApp.Category.Core.Interfaces;

public interface ICategoryService
{
    Task<List<CategoryDTO>> GetAllAsync();
    Task<CategoryDTO?> GetByIdAsync(string id);
    Task<CategoryDTO> CreateAsync(CreateCategoryDTO dto);
    Task<bool> UpdateAsync(string id, UpdateCategoryDTO dto);
    Task<bool> DeleteAsync(string id);
    Task<CategoryStatsDto> GetStatsAsync();
}
