using MongoDB.Driver;
using PizzaApp.Core.DTOs.Cart;
using PizzaApp.Core.Entities;
using PizzaApp.Core.Interfaces;
using PizzaApp.Infrastructure.Data;

namespace PizzaApp.Infrastructure.Services;

public class CartService : ICartService
{
    private readonly IMongoCollection<CartItem> _cartItems;
    private readonly IMongoCollection<Product> _products;

    public CartService(MongoDbService mongoDb)
    {
        _cartItems = mongoDb.GetCollection<CartItem>("CartItems");
        _products = mongoDb.GetCollection<Product>("Products");
    }

    public async Task<List<CartResultDto>> GetCartAsync(string userId)
    {
        var items = await _cartItems.Find(c => c.UserId == userId).ToListAsync();
        return items.Select(i => new CartResultDto
        {
            Id = i.Id,
            ProductId = i.ProductId,
            ProductName = i.ProductName,
            ProductImageUrl = i.ProductImageUrl,
            Quantity = i.Quantity,
            Price = i.Price,
            Size = i.Size
        }).ToList();
    }

    public async Task AddToCartAsync(string userId, CartItemDto dto)
    {
        var product = await _products.Find(p => p.Id == dto.ProductId).FirstOrDefaultAsync();
        if (product == null) throw new Exception("Sản phẩm không tồn tại");

        // Kiểm tra xem đã có sản phẩm này cùng size trong giỏ chưa
        var existing = await _cartItems.Find(c => c.UserId == userId && c.ProductId == dto.ProductId && c.Size == dto.Size).FirstOrDefaultAsync();

        if (existing != null)
        {
            await _cartItems.UpdateOneAsync(c => c.Id == existing.Id,
                Builders<CartItem>.Update.Inc(c => c.Quantity, dto.Quantity));
        }
        else
        {
            var cartItem = new CartItem
            {
                UserId = userId,
                ProductId = dto.ProductId,
                ProductName = product.Name,
                ProductImageUrl = product.ImageUrl,
                Quantity = dto.Quantity,
                Price = product.Price,
                Size = dto.Size
            };
            await _cartItems.InsertOneAsync(cartItem);
        }
    }

    public async Task UpdateQuantityAsync(string userId, string cartItemId, int quantity)
    {
        if (quantity <= 0)
        {
            await _cartItems.DeleteOneAsync(c => c.Id == cartItemId && c.UserId == userId);
        }
        else
        {
            await _cartItems.UpdateOneAsync(c => c.Id == cartItemId && c.UserId == userId,
                Builders<CartItem>.Update.Set(c => c.Quantity, quantity));
        }
    }

    public async Task RemoveFromCartAsync(string userId, string cartItemId)
    {
        await _cartItems.DeleteOneAsync(c => c.Id == cartItemId && c.UserId == userId);
    }

    public async Task ClearCartAsync(string userId)
    {
        await _cartItems.DeleteManyAsync(c => c.UserId == userId);
    }
}
