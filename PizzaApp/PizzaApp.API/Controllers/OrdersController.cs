using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using PizzaApp.Core.DTOs.Order;
using PizzaApp.Core.Interfaces;
using System.Security.Claims;

namespace PizzaApp.API.Controllers;

[Authorize]   // ← bắt buộc phải đăng nhập
[Route("api/[controller]")]
[ApiController]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;

    public OrdersController(IOrderService orderService)
        => _orderService = orderService;

    [HttpPost]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderDto dto)
    {
        var userId = int.Parse(
            User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        var orderId = await _orderService.CreateOrderAsync(userId, dto);
        return Ok(new { orderId, message = "Đặt hàng thành công!" });
    }

    [HttpGet("my")]
    public async Task<IActionResult> GetMyOrders()
    {
        var userId = int.Parse(
            User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

        var orders = await _orderService.GetMyOrdersAsync(userId);
        return Ok(orders);
    }
}
