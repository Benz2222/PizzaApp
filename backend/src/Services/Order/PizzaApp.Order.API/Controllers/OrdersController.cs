using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using PizzaApp.Order.Core.DTOs;
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

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateOrderDto dto)
    {
        try { return Ok(await _orderService.CreateOrderAsync(UserId(), dto)); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpGet("my")]
    public async Task<IActionResult> GetMy() => Ok(await _orderService.GetMyOrdersAsync(UserId()));

    [HttpGet("{id}")]
    public async Task<IActionResult> Detail(string id)
    {
        var o = await _orderService.GetOrderDetailAsync(id, UserId());
        return o == null ? NotFound() : Ok(o);
    }

    [HttpPost("{id}/cancel")]
    public async Task<IActionResult> Cancel(string id)
        => await _orderService.CancelOrderAsync(id, UserId())
            ? Ok(new { message = "Đã hủy đơn" })
            : BadRequest(new { message = "Không thể hủy đơn" });

    [HttpGet("admin/all")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> All() => Ok(await _orderService.GetAllOrdersAsync());

    [HttpGet("admin/status/{status}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ByStatus(string status) => Ok(await _orderService.GetOrdersByStatusAsync(status));

    [HttpPut("admin/{id}/status")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateStatus(string id, [FromBody] string status)
        => await _orderService.UpdateOrderStatusAsync(id, status) ? NoContent() : NotFound();

    [HttpPost("{id}/claim")]
    [Authorize(Roles = "Shipper")]
    public async Task<IActionResult> Claim(string id)
        => await _orderService.ClaimOrderAsync(id, UserId())
            ? Ok(new { message = "Đã nhận đơn" })
            : BadRequest(new { message = "Đơn không còn khả dụng" });

    [HttpGet("shipper/mine")]
    [Authorize(Roles = "Shipper")]
    public async Task<IActionResult> ShipperMine() => Ok(await _orderService.GetShipperOrdersAsync(UserId()));

    [HttpPut("shipper/{id}/delivery")]
    [Authorize(Roles = "Shipper")]
    public async Task<IActionResult> Delivery(string id, [FromBody] string status)
        => await _orderService.UpdateDeliveryStatusAsync(id, UserId(), status) ? NoContent() : BadRequest();
}
