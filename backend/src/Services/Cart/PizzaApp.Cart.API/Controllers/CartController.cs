using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using PizzaApp.Cart.Core.DTOs;
using PizzaApp.Cart.Core.Interfaces;

namespace PizzaApp.Cart.API.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class CartController : ControllerBase
{
    private readonly ICartService _cartService;

    public CartController(ICartService cartService) => _cartService = cartService;

    private string GetUserId()
    {
        var id = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(id))
            throw new UnauthorizedAccessException("User id claim missing");
        return id;
    }

    [HttpGet]
    public async Task<IActionResult> GetCart() => Ok(await _cartService.GetCartAsync(GetUserId()));

    [HttpPost]
    public async Task<IActionResult> AddToCart([FromBody] CartItemDto dto)
    {
        try
        {
            await _cartService.AddToCartAsync(GetUserId(), dto);
            return Ok(new { message = "Đã thêm vào giỏ hàng" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPatch("{id}/quantity")]
    public async Task<IActionResult> UpdateQuantity(string id, [FromBody] int quantity)
    {
        await _cartService.UpdateQuantityAsync(GetUserId(), id, quantity);
        return Ok(new { message = "Đã cập nhật số lượng" });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> RemoveFromCart(string id)
    {
        await _cartService.RemoveFromCartAsync(GetUserId(), id);
        return Ok(new { message = "Đã xóa khỏi giỏ hàng" });
    }

    [HttpDelete("clear")]
    public async Task<IActionResult> ClearCart()
    {
        await _cartService.ClearCartAsync(GetUserId());
        return Ok(new { message = "Đã làm trống giỏ hàng" });
    }
}
