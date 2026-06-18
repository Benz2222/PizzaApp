using Microsoft.EntityFrameworkCore;
using PizzaApp.Core.DTOs.Product;
using PizzaApp.Core.Entities;
using PizzaApp.Core.Interfaces;
using PizzaApp.Infrastructure.Data;

namespace PizzaApp.Infrastructure.Services;

public class ProductService : IProductService
{
    private readonly AppDbContext _db;

    public ProductService(AppDbContext db) => _db = db;

    public async Task<List<ProductDto>> GetAllAsync()
    {
        return await _db.Products
            .Where(p => p.IsAvailable)
            .Select(p => new ProductDto
            {
                Id = p.Id,
                Name = p.Name,
                Description = p.Description,
                Price = p.Price,
                ImageUrl = p.ImageUrl,
                CategoryId = p.CategoryId,
                CategoryName = p.Category.Name,
                IsAvailable = p.IsAvailable
            })
            .ToListAsync();
    }

    public async Task<ProductDto?> GetByIdAsync(int id)
    {
        var p = await _db.Products.Include(p => p.Category)
        .FirstOrDefaultAsync(p => p.Id == id);
        if (p == null) return null;

        return new ProductDto
        {
            Id = p.Id,
            Name = p.Name,
            Description = p.Description,
            Price = p.Price,
            ImageUrl = p.ImageUrl,
            CategoryId = p.CategoryId,
            CategoryName = p.Category.Name,
            IsAvailable = p.IsAvailable
        };
    }

    public async Task<ProductDto> CreateAsync(CreateProductDTO dto)
    {
        var category = await _db.Categories
        .FirstOrDefaultAsync(c => c.Id == dto.CategoryId);

        if (category == null)
            throw new Exception("Category not found");

        var product = new Product
        {
            Name = dto.Name,
            Description = dto.Description,
            Price = dto.Price,
            ImageUrl = dto.ImageUrl,
            CategoryId = dto.CategoryId,
            //Category = category,
            IsAvailable = true
        };



        _db.Products.Add(product);

        await _db.SaveChangesAsync();

        return new ProductDto
        {
            Id = product.Id,
            Name = product.Name,
            Description = product.Description,
            Price = product.Price,
            ImageUrl = product.ImageUrl,
            CategoryId = product.CategoryId,
            CategoryName = category.Name,
            IsAvailable = product.IsAvailable
        };
    }

    public async Task<bool> UpdateAsync(int id, UpdateProductDTO dto)
    {
        var product = await _db.Products.FindAsync(id);

        if (product == null)
            return false;

        product.Name = dto.Name;
        product.Description = dto.Description;
        product.Price = dto.Price;
        product.ImageUrl = dto.ImageUrl;
        product.CategoryId = dto.CategoryId;
        product.IsAvailable = dto.IsAvailable;

        await _db.SaveChangesAsync();

        return true;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var product = await _db.Products.FindAsync(id);

        if (product == null)
            return false;

        _db.Products.Remove(product);

        await _db.SaveChangesAsync();

        return true;
    }

}