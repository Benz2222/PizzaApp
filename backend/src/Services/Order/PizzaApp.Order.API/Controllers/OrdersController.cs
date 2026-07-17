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

    /// <summary>Đặt hàng từ giỏ hàng hiện tại. Trả về link + QR để thanh toán.</summary>
    /// <param name="deliveryAddress">Địa chỉ giao hàng. Gửi chuỗi trần trong body, ví dụ: "12 Nguyễn Huệ, Q1"</param>
    /// <remarks>
    /// Đây là API phức tạp nhất hệ thống, gọi 3 service khác:
    ///
    /// 1. **Cart** (REST) — lấy các món đang có trong giỏ
    /// 2. **Product** (REST) — lấy **giá thật** của từng món. Không bao giờ tin giá client gửi lên
    /// 3. **Payment** (REST) — tạo giao dịch, lấy link + QR. Nếu bước này lỗi thì đơn vừa tạo bị xoá
    /// 4. Bắn event **OrderCreated** qua RabbitMQ → Cart service tự xoá giỏ (không chờ)
    ///
    /// Tên và giá món được lưu thẳng vào đơn, nên sau này sản phẩm đổi giá hay bị xoá
    /// thì đơn cũ vẫn giữ nguyên giá lúc mua.
    ///
    /// Ví dụ request:
    ///
    ///     POST /api/orders/checkout
    ///     "12 Nguyễn Huệ, Quận 1, TP.HCM"
    ///
    /// Ví dụ response:
    ///
    ///     {
    ///       "orderId": "6a59475f2b5f9d568f33f511",
    ///       "checkoutUrl": "https://pay.payos.vn/web/28a052e343324b4c...",
    ///       "qrCode": "data:image/png;base64,iVBORw0KG...",
    ///       "message": "Đã tạo đơn hàng thành công!"
    ///     }
    /// </remarks>
    /// <response code="200">Đã tạo đơn, trả về link thanh toán và QR.</response>
    /// <response code="400">Giỏ hàng trống, không có sản phẩm hợp lệ, hoặc không tạo được thanh toán.</response>
    /// <response code="401">Chưa đăng nhập.</response>
    [HttpPost("checkout")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
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

    /// <summary>Lấy các đơn của chính người đang đăng nhập, mới nhất trước.</summary>
    /// <remarks>App dùng API này cho tab "Đơn hàng" và để theo dõi trạng thái đơn.</remarks>
    /// <response code="200">Danh sách đơn.</response>
    /// <response code="401">Chưa đăng nhập.</response>
    [HttpGet("my")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> My() => Ok(await _orderService.GetMyOrdersAsync(UserId()));

    /// <summary>Khách tự huỷ đơn của mình.</summary>
    /// <param name="id">Id đơn hàng.</param>
    /// <remarks>Chỉ huỷ được khi đơn còn ở trạng thái `AwaitingPayment` (chưa trả tiền).</remarks>
    /// <response code="200">Đã huỷ.</response>
    /// <response code="400">Đơn đã trả tiền hoặc không phải đơn của bạn.</response>
    [HttpPost("{id}/cancel")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Cancel(string id)
        => await _orderService.CancelOrderAsync(id, UserId())
            ? Ok(new { message = "Đã hủy đơn" })
            : BadRequest(new { message = "Không thể hủy đơn" });

    /// <summary>Lấy toàn bộ đơn của mọi khách (chỉ Admin).</summary>
    /// <response code="200">Danh sách đơn, mới nhất trước.</response>
    /// <response code="403">Không phải Admin.</response>
    [HttpGet("all")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> All() => Ok(await _orderService.GetAllOrdersAsync());

    /// <summary>Thống kê doanh thu, số đơn, top món bán chạy (chỉ Admin).</summary>
    /// <remarks>
    /// Doanh thu chỉ tính đơn đã thanh toán và chưa huỷ. Số đơn thì đếm tất cả.
    /// Trả thêm số đơn theo từng trạng thái (đủ 7 trạng thái, thiếu thì = 0)
    /// và top 5 món bán chạy nhất.
    /// </remarks>
    /// <response code="200">Số liệu cho Dashboard.</response>
    /// <response code="403">Không phải Admin.</response>
    [HttpGet("admin/stats")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Stats() => Ok(await _orderService.GetStatsAsync());

    /// <summary>Admin xác nhận đơn đã thanh toán bằng tay.</summary>
    /// <param name="id">Id đơn hàng.</param>
    /// <remarks>
    /// Bình thường không cần dùng: khi khách trả tiền, PayOS gọi webhook → Payment service
    /// bắn event `PaymentSucceeded` → Order service tự đổi đơn sang Paid.
    /// API này chỉ để chữa cháy khi webhook không tới được.
    /// </remarks>
    /// <response code="200">Đã xác nhận.</response>
    /// <response code="400">Đơn đã thanh toán rồi hoặc không tồn tại.</response>
    [HttpPost("{id}/confirm-payment")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ConfirmPayment(string id)
        => await _orderService.ConfirmPaymentAsync(id)
            ? Ok(new { message = "Đã xác nhận thanh toán" })
            : BadRequest(new { message = "Không thể xác nhận" });

    /// <summary>Admin đổi trạng thái đơn (ví dụ bếp làm xong thì chuyển sang Ready).</summary>
    /// <param name="id">Id đơn hàng.</param>
    /// <param name="status">
    /// Trạng thái mới. Gửi chuỗi trần trong body, ví dụ: "Preparing".
    /// Vòng đời: `AwaitingPayment` → `Paid` → `Preparing` → `Ready` → `Delivering` → `Done`, hoặc `Cancelled`.
    /// </param>
    /// <response code="200">Đã cập nhật.</response>
    /// <response code="404">Không tìm thấy đơn.</response>
    [HttpPatch("{id}/status")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Status(string id, [FromBody] string status)
        => await _orderService.UpdateOrderStatusAsync(id, status)
            ? Ok(new { message = "Đã cập nhật" })
            : NotFound();

    /// <summary>Danh sách đơn đang chờ shipper nhận (Shipper hoặc Admin).</summary>
    /// <remarks>Là các đơn ở trạng thái `Ready` — bếp đã làm xong, chờ người giao.</remarks>
    /// <response code="200">Danh sách đơn có thể nhận.</response>
    /// <response code="403">Không phải Shipper/Admin.</response>
    [HttpGet("shipper/available")]
    [Authorize(Roles = "Shipper,Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Available() => Ok(await _orderService.GetOrdersByStatusAsync("Ready"));

    /// <summary>Các đơn mà shipper đang đăng nhập đã nhận (Shipper hoặc Admin).</summary>
    /// <response code="200">Danh sách đơn của shipper này.</response>
    /// <response code="403">Không phải Shipper/Admin.</response>
    [HttpGet("shipper/mine")]
    [Authorize(Roles = "Shipper,Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ShipperMine() => Ok(await _orderService.GetShipperOrdersAsync(UserId()));

    /// <summary>Shipper nhận một đơn để đi giao.</summary>
    /// <param name="id">Id đơn hàng.</param>
    /// <remarks>
    /// Chỉ nhận được đơn đang `Ready` và **chưa có shipper nào nhận**. Việc kiểm tra này
    /// nằm ngay trong điều kiện update của MongoDB, nên hai shipper bấm cùng lúc thì
    /// chỉ một người nhận được — người kia nhận lỗi. Nhận xong đơn chuyển sang `Delivering`.
    /// </remarks>
    /// <response code="200">Nhận đơn thành công.</response>
    /// <response code="400">Đơn chưa sẵn sàng hoặc shipper khác đã nhận trước.</response>
    [HttpPost("{id}/claim")]
    [Authorize(Roles = "Shipper,Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Claim(string id)
        => await _orderService.ClaimOrderAsync(id, UserId())
            ? Ok(new { message = "Đã nhận đơn." })
            : BadRequest(new { message = "Không nhận được đơn (đã có shipper khác nhận)." });

    /// <summary>Shipper báo kết quả giao hàng.</summary>
    /// <param name="id">Id đơn hàng.</param>
    /// <param name="status">Chỉ nhận `"Done"` (giao xong) hoặc `"Cancelled"` (giao thất bại). Gửi chuỗi trần trong body.</param>
    /// <remarks>Chỉ cập nhật được đơn do **chính shipper này** nhận và đang ở trạng thái `Delivering`.</remarks>
    /// <response code="200">Đã cập nhật.</response>
    /// <response code="400">Trạng thái không hợp lệ, hoặc đơn không phải của shipper này.</response>
    [HttpPost("{id}/delivery-status")]
    [Authorize(Roles = "Shipper,Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Delivery(string id, [FromBody] string status)
        => await _orderService.UpdateDeliveryStatusAsync(id, UserId(), status)
            ? Ok(new { message = "Đã cập nhật" })
            : BadRequest(new { message = "Cập nhật thất bại" });
}
