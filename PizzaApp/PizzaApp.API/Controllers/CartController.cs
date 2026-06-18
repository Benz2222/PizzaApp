using Microsoft.AspNetCore.Mvc;
using PizzaApp.Core.DTOs.Cart;
using PizzaApp.Core.Interfaces;

namespace PizzaApp.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class CartController : ControllerBase
{
    private readonly ICartService _cartService;

    public CartController(ICartService cartService)
    {
        _cartService = cartService;
    }

    private string GetUserId() => "666554433221100bbccddeeff"; // Giả lập UserId

    [HttpGet]
    public async Task<IActionResult> GetCart()
    {
        return Ok(await _cartService.GetCartAsync(GetUserId()));
    }

    [HttpPost]
    public async Task<IActionResult> AddToCart([FromBody] CartItemDto dto)
    {
        await _cartService.AddToCartAsync(GetUserId(), dto);
        return Ok(new { message = "Đã thêm vào giỏ hàng" });
    }

    [HttpPatch("{id}/quantity")]
    public async Task<IActionResult> UpdateQuantity(string id, [FromBody] int quantity)
    {
        await _cartService.UpdateQuantityAsync(id, quantity);
        return Ok(new { message = "Đã cập nhật số lượng" });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> RemoveFromCart(string id)
    {
        await _cartService.RemoveFromCartAsync(id);
        return Ok(new { message = "Đã xóa khỏi giỏ hàng" });
    }

    [HttpDelete("clear")]
    public async Task<IActionResult> ClearCart()
    {
        await _cartService.ClearCartAsync(GetUserId());
        return Ok(new { message = "Đã làm trống giỏ hàng" });
    }
}
