using MongoDB.Driver;
using PizzaApp.Core.DTOs.Category;
using PizzaApp.Core.Entities;
using PizzaApp.Core.Interfaces;
using PizzaApp.Infrastructure.Data;

namespace PizzaApp.Infrastructure.Services;

public class CategoryService : ICategoryService
{
    private readonly IMongoCollection<Category> _categories;

    public CategoryService(MongoDbService mongoDb)
    {
        _categories = mongoDb.GetCollection<Category>("Categories");
    }

    public async Task<List<CategoryDTO>> GetAllAsync()
    {
        var categories = await _categories.Find(_ => true).ToListAsync();
        return categories.Select(c => new CategoryDTO
        {
            Id = c.Id,
            Name = c.Name
        }).ToList();
    }

    public async Task<CategoryDTO?> GetByIdAsync(string id)
    {
        var category = await _categories.Find(c => c.Id == id).FirstOrDefaultAsync();
        if (category == null) return null;

        return new CategoryDTO
        {
            Id = category.Id,
            Name = category.Name
        };
    }

    public async Task<CategoryDTO> CreateAsync(CreateCategoryDTO dto)
    {
        var category = new Category
        {
            Name = dto.Name
        };

        await _categories.InsertOneAsync(category);

        return new CategoryDTO
        {
            Id = category.Id,
            Name = category.Name
        };
    }

    public async Task<bool> UpdateAsync(string id, UpdateCategoryDTO dto)
    {
        var result = await _categories.UpdateOneAsync(
            c => c.Id == id,
            Builders<Category>.Update.Set(c => c.Name, dto.Name));
        return result.MatchedCount > 0;
    }

    public async Task<bool> DeleteAsync(string id)
    {
        var result = await _categories.DeleteOneAsync(c => c.Id == id);
        return result.DeletedCount > 0;
    }
}
