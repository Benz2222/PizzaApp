using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using PizzaApp.Core.DTOs.Order;
using PizzaApp.Core.Interfaces;

namespace PizzaApp.API.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;
    private readonly ICartService _cartService;

    public OrdersController(IOrderService orderService, ICartService cartService)
    {
        _orderService = orderService;
        _cartService = cartService;
    }

    private string GetUserId()
    {
        var id = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(id))
            throw new UnauthorizedAccessException("User id claim missing");
        return id;
    }

    // --- LUỒNG THANH TOÁN TỪ GIỎ HÀNG ---

    [HttpPost("checkout")]
    public async Task<IActionResult> Checkout([FromBody] string deliveryAddress)
    {
        try
        {
            var userId = GetUserId();

            // 1. Lấy giỏ hàng của User
            var cartItems = await _cartService.GetCartAsync(userId);
            if (cartItems == null || !cartItems.Any())
            {
                return BadRequest(new { message = "Giỏ hàng của bạn đang trống." });
            }

            // 2. Chuyển đổi từ Cart sang Order DTO
            var createOrderDto = new CreateOrderDto
            {
                DeliveryAddress = deliveryAddress,
                Items = cartItems.Select(c => new OrderItemDto
                {
                    ProductId = c.ProductId,
                    Quantity = c.Quantity,
                    Size = c.Size
                }).ToList()
            };

            // 3. Tạo đơn hàng và lấy link thanh toán PayOS
            var orderId = await _orderService.CreateOrderAsync(userId, createOrderDto);

            // 4. Lấy thông tin chi tiết đơn hàng để lấy Link PayOS
            var orderDetail = await _orderService.GetOrderDetailAsync(orderId, userId);

            // 5. Xóa giỏ hàng sau khi đã đặt hàng thành công
            await _cartService.ClearCartAsync(userId);

            return Ok(new
            {
                orderId = orderId,
                checkoutUrl = orderDetail?.PaymentUrl,
                message = "Đã tạo đơn hàng thành công từ giỏ hàng!"
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // --- CÁC ENDPOINT KHÁC ---

    [HttpGet("my")]
    public async Task<IActionResult> GetMyOrders()
    {
        var orders = await _orderService.GetMyOrdersAsync(GetUserId());
        return Ok(orders);
    }

    [HttpPost("{id}/cancel")]
    public async Task<IActionResult> CancelOrder(string id)
    {
        var success = await _orderService.CancelOrderAsync(id, GetUserId());
        if (!success)
            return BadRequest(new { message = "Không thể hủy đơn (không tồn tại hoặc đã qua bước thanh toán)." });
        return Ok(new { message = "Đã hủy đơn hàng." });
    }

    // --- ADMIN: quản lý tất cả đơn ---

    [HttpGet("all")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetAllOrders()
    {
        var orders = await _orderService.GetAllOrdersAsync();
        return Ok(orders);
    }

    // Admin xác nhận đã thanh toán -> đơn chuyển sang "Paid"
    [HttpPost("{id}/confirm-payment")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ConfirmPayment(string id)
    {
        var success = await _orderService.ConfirmPaymentAsync(id);
        if (!success)
            return BadRequest(new { message = "Không xác nhận được (đơn không tồn tại hoặc đã thanh toán)." });
        return Ok(new { message = "Đã xác nhận thanh toán." });
    }

    // Admin đổi trạng thái: Paid -> Preparing -> Ready
    [HttpPatch("{id}/status")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateStatus(string id, [FromBody] string status)
    {
        var success = await _orderService.UpdateOrderStatusAsync(id, status);
        if (!success) return NotFound();
        return Ok(new { message = $"Đã cập nhật: {status}" });
    }

    // --- SHIPPER ---

    // Đơn đang chờ giao (Ready, chưa ai nhận)
    [HttpGet("shipper/available")]
    [Authorize(Roles = "Shipper,Admin")]
    public async Task<IActionResult> GetOrdersForShipper()
    {
        var orders = await _orderService.GetOrdersByStatusAsync("Ready");
        return Ok(orders);
    }

    // Đơn shipper này đã nhận
    [HttpGet("shipper/mine")]
    [Authorize(Roles = "Shipper,Admin")]
    public async Task<IActionResult> GetMyDeliveries()
    {
        var orders = await _orderService.GetShipperOrdersAsync(GetUserId());
        return Ok(orders);
    }

    // Shipper nhận đơn (Ready -> Delivering, gán cho shipper này)
    [HttpPost("{id}/claim")]
    [Authorize(Roles = "Shipper,Admin")]
    public async Task<IActionResult> ClaimOrder(string id)
    {
        var success = await _orderService.ClaimOrderAsync(id, GetUserId());
        if (!success)
            return BadRequest(new { message = "Không nhận được đơn (đã có shipper khác nhận)." });
        return Ok(new { message = "Đã nhận đơn." });
    }

    // Shipper cập nhật đơn của mình: Delivering -> Done / Cancelled
    [HttpPost("{id}/delivery-status")]
    [Authorize(Roles = "Shipper,Admin")]
    public async Task<IActionResult> UpdateDeliveryStatus(string id, [FromBody] string status)
    {
        var success = await _orderService.UpdateDeliveryStatusAsync(id, GetUserId(), status);
        if (!success)
            return BadRequest(new { message = "Không cập nhật được (đơn không phải của bạn hoặc trạng thái không hợp lệ)." });
        return Ok(new { message = $"Đã cập nhật: {status}" });
    }
}
