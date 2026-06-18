using PizzaApp.Core.DTOs.Cart;

namespace PizzaApp.Core.Interfaces;

public interface ICartService
{
    Task<List<CartResultDto>> GetCartAsync(string userId);
    Task AddToCartAsync(string userId, CartItemDto dto);
    Task UpdateQuantityAsync(string cartItemId, int quantity);
    Task RemoveFromCartAsync(string cartItemId);
    Task ClearCartAsync(string userId);
}
