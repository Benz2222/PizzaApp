using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using PizzaApp.Order.Core.Interfaces;

namespace PizzaApp.Order.API.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;
    public OrdersController(IOrderService orderService) => _orderService = orderService;

    private string UserId() => User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";

    [HttpPost("checkout")]
    public async Task<IActionResult> Checkout([FromBody] string deliveryAddress)
    {
        try
        {
            var order = await _orderService.CheckoutFromCartAsync(UserId(), deliveryAddress);
            return Ok(new
            {
                orderId = order.Id,
                checkoutUrl = order.PaymentUrl,
                qrCode = order.PaymentQr,
                message = "Đã tạo đơn hàng thành công!"
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("my")]
    public async Task<IActionResult> My() => Ok(await _orderService.GetMyOrdersAsync(UserId()));

    [HttpPost("{id}/cancel")]
    public async Task<IActionResult> Cancel(string id)
        => await _orderService.CancelOrderAsync(id, UserId())
            ? Ok(new { message = "Đã hủy đơn" })
            : BadRequest(new { message = "Không thể hủy đơn" });

    [HttpGet("all")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> All() => Ok(await _orderService.GetAllOrdersAsync());

    [HttpPost("{id}/confirm-payment")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ConfirmPayment(string id)
        => await _orderService.ConfirmPaymentAsync(id)
            ? Ok(new { message = "Đã xác nhận thanh toán" })
            : BadRequest(new { message = "Không thể xác nhận" });

    [HttpPatch("{id}/status")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Status(string id, [FromBody] string status)
        => await _orderService.UpdateOrderStatusAsync(id, status)
            ? Ok(new { message = "Đã cập nhật" })
            : NotFound();

    [HttpGet("shipper/available")]
    [Authorize(Roles = "Shipper,Admin")]
    public async Task<IActionResult> Available() => Ok(await _orderService.GetOrdersByStatusAsync("Ready"));

    [HttpGet("shipper/mine")]
    [Authorize(Roles = "Shipper,Admin")]
    public async Task<IActionResult> ShipperMine() => Ok(await _orderService.GetShipperOrdersAsync(UserId()));

    [HttpPost("{id}/claim")]
    [Authorize(Roles = "Shipper,Admin")]
    public async Task<IActionResult> Claim(string id)
        => await _orderService.ClaimOrderAsync(id, UserId())
            ? Ok(new { message = "Đã nhận đơn." })
            : BadRequest(new { message = "Không nhận được đơn (đã có shipper khác nhận)." });

    [HttpPost("{id}/delivery-status")]
    [Authorize(Roles = "Shipper,Admin")]
    public async Task<IActionResult> Delivery(string id, [FromBody] string status)
        => await _orderService.UpdateDeliveryStatusAsync(id, UserId(), status)
            ? Ok(new { message = "Đã cập nhật" })
            : BadRequest(new { message = "Cập nhật thất bại" });
}
