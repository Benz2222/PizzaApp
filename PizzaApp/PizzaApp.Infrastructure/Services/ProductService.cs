using MongoDB.Driver;
using PizzaApp.Core.DTOs.Product;
using PizzaApp.Core.Entities;
using PizzaApp.Core.Interfaces;
using PizzaApp.Infrastructure.Data;

namespace PizzaApp.Infrastructure.Services;

public class ProductService : IProductService
{
    private readonly IMongoCollection<Product> _products;

    public ProductService(MongoDbService mongoDb)
    {
        _products = mongoDb.GetCollection<Product>("Products");
    }

    public async Task<List<ProductDto>> GetAllAsync()
    {
        var products = await _products.Find(p => p.IsAvailable).ToListAsync();
        return products.Select(p => new ProductDto
        {
            Id = p.Id,
            Name = p.Name,
            Description = p.Description,
            Price = p.Price,
            ImageUrl = p.ImageUrl,
            Category = p.Category
        }).ToList();
    }

    public async Task<ProductDto?> GetByIdAsync(string id)
    {
        var p = await _products.Find(p => p.Id == id).FirstOrDefaultAsync();
        if (p == null) return null;

        return new ProductDto
        {
            Id = p.Id,
            Name = p.Name,
            Description = p.Description,
            Price = p.Price,
            ImageUrl = p.ImageUrl,
            Category = p.Category
        };
    }
}
