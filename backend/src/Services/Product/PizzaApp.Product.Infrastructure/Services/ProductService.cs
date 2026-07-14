using MongoDB.Bson;
using MongoDB.Driver;
using PizzaApp.Product.Core.DTOs;
using PizzaApp.Product.Core.Interfaces;
using ProductEntity = PizzaApp.Product.Core.Entities.Product;

namespace PizzaApp.Product.Infrastructure.Services;

public class ProductService : IProductService
{
    private readonly IMongoCollection<ProductEntity> _products;
    private readonly ICategoryClient _categoryClient;

    public ProductService(ProductDbContext db, ICategoryClient categoryClient)
    {
        _products = db.Products;
        _categoryClient = categoryClient;
    }

    public async Task<List<ProductDto>> GetAllAsync(string? search, string? categoryId, int page, int pageSize)
    {
        var b = Builders<ProductEntity>.Filter;
        var filter = b.Eq(p => p.IsAvailable, true);

        if (!string.IsNullOrWhiteSpace(categoryId))
            filter &= b.Eq(p => p.CategoryId, categoryId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var regex = new BsonRegularExpression(search, "i");
            filter &= b.Or(b.Regex(p => p.Name, regex), b.Regex(p => p.Description, regex));
        }

        var (pg, size) = NormalizePaging(page, pageSize);
        var products = await _products.Find(filter)
            .Skip((pg - 1) * size).Limit(size).ToListAsync();

        return products.Select(MapToDto).ToList();
    }

    public async Task<ProductDto?> GetByIdAsync(string id)
    {
        var p = await _products.Find(p => p.Id == id).FirstOrDefaultAsync();
        return p == null ? null : MapToDto(p);
    }

    public async Task<ProductDto> CreateAsync(CreateProductDTO dto)
    {
        var categoryName = await _categoryClient.GetCategoryNameAsync(dto.CategoryId);
        if (categoryName == null)
            throw new InvalidOperationException("Category not found");

        var product = new ProductEntity
        {
            Name = dto.Name,
            Description = dto.Description,
            Price = dto.Price,
            ImageUrl = dto.ImageUrl,
            CategoryId = dto.CategoryId,
            CategoryName = categoryName,
            IsAvailable = true
        };
        await _products.InsertOneAsync(product);
        return MapToDto(product);
    }

    public async Task<bool> UpdateAsync(string id, UpdateProductDTO dto)
    {
        var categoryName = await _categoryClient.GetCategoryNameAsync(dto.CategoryId);
        if (categoryName == null)
            throw new InvalidOperationException("Category not found");

        var update = Builders<ProductEntity>.Update
            .Set(p => p.Name, dto.Name)
            .Set(p => p.Description, dto.Description)
            .Set(p => p.Price, dto.Price)
            .Set(p => p.ImageUrl, dto.ImageUrl)
            .Set(p => p.CategoryId, dto.CategoryId)
            .Set(p => p.CategoryName, categoryName)
            .Set(p => p.IsAvailable, dto.IsAvailable);

        var result = await _products.UpdateOneAsync(p => p.Id == id, update);
        return result.MatchedCount > 0;
    }

    public async Task<bool> DeleteAsync(string id)
    {
        var result = await _products.DeleteOneAsync(p => p.Id == id);
        return result.DeletedCount > 0;
    }

    public static (int page, int pageSize) NormalizePaging(int page, int pageSize)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        return (page, pageSize);
    }

    public static ProductDto MapToDto(ProductEntity p) => new()
    {
        Id = p.Id,
        Name = p.Name,
        Description = p.Description,
        Price = p.Price,
        ImageUrl = p.ImageUrl,
        CategoryId = p.CategoryId,
        CategoryName = p.CategoryName,
        IsAvailable = p.IsAvailable
    };
}
