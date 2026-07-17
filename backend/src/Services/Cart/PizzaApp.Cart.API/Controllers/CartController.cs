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

    /// <summary>Lấy giỏ hàng của người đang đăng nhập.</summary>
    /// <remarks>
    /// UserId lấy từ token, không nhận từ client (tránh xem giỏ của người khác).
    ///
    /// Ví dụ response:
    ///
    ///     [
    ///       {
    ///         "id": "...", "productId": "1", "productName": "Margherita",
    ///         "price": 10000, "quantity": 2, "size": "M"
    ///       }
    ///     ]
    /// </remarks>
    /// <response code="200">Danh sách món trong giỏ (rỗng nếu chưa thêm gì).</response>
    /// <response code="401">Chưa đăng nhập.</response>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetCart() => Ok(await _cartService.GetCartAsync(GetUserId()));

    /// <summary>Thêm một món vào giỏ.</summary>
    /// <remarks>
    /// Service gọi REST sang Product để kiểm tra sản phẩm có thật và lấy tên/giá.
    /// Nếu món đó (cùng size) đã có trong giỏ thì cộng dồn số lượng.
    ///
    /// Ví dụ request:
    ///
    ///     POST /api/cart
    ///     { "productId": "1", "quantity": 2, "size": "M" }
    /// </remarks>
    /// <response code="200">Đã thêm vào giỏ.</response>
    /// <response code="400">Sản phẩm không tồn tại.</response>
    /// <response code="401">Chưa đăng nhập.</response>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
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

    /// <summary>Đổi số lượng của một dòng trong giỏ.</summary>
    /// <param name="id">Id của dòng giỏ hàng (CartItem.Id), không phải ProductId.</param>
    /// <param name="quantity">Số lượng mới. Truyền số trần trong body, ví dụ: 3</param>
    /// <response code="200">Đã cập nhật.</response>
    /// <response code="401">Chưa đăng nhập.</response>
    [HttpPatch("{id}/quantity")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateQuantity(string id, [FromBody] int quantity)
    {
        await _cartService.UpdateQuantityAsync(GetUserId(), id, quantity);
        return Ok(new { message = "Đã cập nhật số lượng" });
    }

    /// <summary>Xoá một món khỏi giỏ.</summary>
    /// <param name="id">Id của dòng giỏ hàng (CartItem.Id).</param>
    /// <response code="200">Đã xoá.</response>
    /// <response code="401">Chưa đăng nhập.</response>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RemoveFromCart(string id)
    {
        await _cartService.RemoveFromCartAsync(GetUserId(), id);
        return Ok(new { message = "Đã xóa khỏi giỏ hàng" });
    }

    /// <summary>Làm trống toàn bộ giỏ hàng.</summary>
    /// <remarks>
    /// Bình thường app không cần gọi API này: sau khi đặt hàng, Order service bắn
    /// event OrderCreated qua RabbitMQ và Cart service tự xoá giỏ.
    /// </remarks>
    /// <response code="200">Đã làm trống giỏ.</response>
    /// <response code="401">Chưa đăng nhập.</response>
    [HttpDelete("clear")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ClearCart()
    {
        await _cartService.ClearCartAsync(GetUserId());
        return Ok(new { message = "Đã làm trống giỏ hàng" });
    }
}
