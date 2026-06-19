using MongoDB.Driver;
using PizzaApp.Core.DTOs.Product;
using PizzaApp.Core.Entities;
using PizzaApp.Core.Interfaces;
using PizzaApp.Infrastructure.Data;

namespace PizzaApp.Infrastructure.Services;

public class ProductService : IProductService
{
    private readonly IMongoCollection<Product> _products;
    private readonly IMongoCollection<Category> _categories;

    public ProductService(MongoDbService mongoDb)
    {
        _products = mongoDb.GetCollection<Product>("Products");
        _categories = mongoDb.GetCollection<Category>("Categories");
    }

    public async Task<List<ProductDto>> GetAllAsync()
    {
        var products = await _products.Find(p => p.IsAvailable).ToListAsync();

        // Lấy tên category một lần để tránh query lặp (N+1)
        var categories = await _categories.Find(_ => true).ToListAsync();
        var categoryNames = categories.ToDictionary(c => c.Id, c => c.Name);

        return products.Select(p => MapToDto(p,
            categoryNames.TryGetValue(p.CategoryId, out var name) ? name : string.Empty)).ToList();
    }

    public async Task<ProductDto?> GetByIdAsync(string id)
    {
        var p = await _products.Find(p => p.Id == id).FirstOrDefaultAsync();
        if (p == null) return null;

        var category = await _categories.Find(c => c.Id == p.CategoryId).FirstOrDefaultAsync();
        return MapToDto(p, category?.Name ?? string.Empty);
    }

    public async Task<ProductDto> CreateAsync(CreateProductDTO dto)
    {
        var category = await _categories.Find(c => c.Id == dto.CategoryId).FirstOrDefaultAsync();
        if (category == null)
            throw new Exception("Category not found");

        var product = new Product
        {
            Name = dto.Name,
            Description = dto.Description,
            Price = dto.Price,
            ImageUrl = dto.ImageUrl,
            CategoryId = dto.CategoryId,
            IsAvailable = true
        };

        await _products.InsertOneAsync(product);

        return MapToDto(product, category.Name);
    }

    public async Task<bool> UpdateAsync(string id, UpdateProductDTO dto)
    {
        var update = Builders<Product>.Update
            .Set(p => p.Name, dto.Name)
            .Set(p => p.Description, dto.Description)
            .Set(p => p.Price, dto.Price)
            .Set(p => p.ImageUrl, dto.ImageUrl)
            .Set(p => p.CategoryId, dto.CategoryId)
            .Set(p => p.IsAvailable, dto.IsAvailable);

        var result = await _products.UpdateOneAsync(p => p.Id == id, update);
        return result.MatchedCount > 0;
    }

    public async Task<bool> DeleteAsync(string id)
    {
        var result = await _products.DeleteOneAsync(p => p.Id == id);
        return result.DeletedCount > 0;
    }

    private static ProductDto MapToDto(Product p, string categoryName) => new ProductDto
    {
        Id = p.Id,
        Name = p.Name,
        Description = p.Description,
        Price = p.Price,
        ImageUrl = p.ImageUrl,
        CategoryId = p.CategoryId,
        CategoryName = categoryName,
        IsAvailable = p.IsAvailable
    };
}
