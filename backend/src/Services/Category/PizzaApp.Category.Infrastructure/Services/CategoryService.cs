using MongoDB.Driver;
using PizzaApp.Category.Core.DTOs;
using PizzaApp.Category.Core.Interfaces;
using CategoryEntity = PizzaApp.Category.Core.Entities.Category;

namespace PizzaApp.Category.Infrastructure.Services;

public class CategoryService : ICategoryService
{
    private readonly IMongoCollection<CategoryEntity> _categories;

    public CategoryService(CategoryDbContext db) => _categories = db.Categories;

    public async Task<List<CategoryDTO>> GetAllAsync()
    {
        var list = await _categories.Find(_ => true).ToListAsync();
        return list.Select(ToDto).ToList();
    }

    public async Task<CategoryDTO?> GetByIdAsync(string id)
    {
        var c = await _categories.Find(c => c.Id == id).FirstOrDefaultAsync();
        return c == null ? null : ToDto(c);
    }

    public async Task<CategoryDTO> CreateAsync(CreateCategoryDTO dto)
    {
        var category = new CategoryEntity { Name = dto.Name };
        await _categories.InsertOneAsync(category);
        return ToDto(category);
    }

    public async Task<bool> UpdateAsync(string id, UpdateCategoryDTO dto)
    {
        var result = await _categories.UpdateOneAsync(
            c => c.Id == id,
            Builders<CategoryEntity>.Update.Set(c => c.Name, dto.Name));
        return result.MatchedCount > 0;
    }

    public async Task<bool> DeleteAsync(string id)
    {
        var result = await _categories.DeleteOneAsync(c => c.Id == id);
        return result.DeletedCount > 0;
    }

    public async Task<CategoryStatsDto> GetStatsAsync()
    {
        var total = (int)await _categories.CountDocumentsAsync(
            Builders<CategoryEntity>.Filter.Empty);
        return new CategoryStatsDto { TotalCategories = total };
    }

    public static CategoryDTO ToDto(CategoryEntity c) => new() { Id = c.Id, Name = c.Name };
}
