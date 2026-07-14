# Plan 5 — Order.API khớp app + nối Flutter (A+B)

> REQUIRED SUB-SKILL: superpowers:executing-plans. Steps dùng checkbox.

**Goal:** Đưa Order.API về đúng contract mà app Flutter đang gọi, thêm Cart client (checkout đọc giỏ từ Cart service), thêm route ảnh ở gateway, và đổi `baseUrl` app sang gateway. Sau plan này app chạy end-to-end qua gateway.

**Constraints:** kế thừa Plan 1–4. Order gọi Cart/Product qua REST, **forward JWT** của người dùng. Không xóa monolith trong plan này.

---

### Task 1: Order — ICartClient + checkout đọc giỏ

**Files:**
- Create `Order.Core/Interfaces/ICartClient.cs`
- Create `Order.Infrastructure/Clients/CartHttpClient.cs`
- Modify `Order.Core/Interfaces/IOrderService.cs` (+ `CheckoutFromCartAsync`)
- Modify `Order.Infrastructure/Services/OrderService.cs` (inject ICartClient, add CheckoutFromCartAsync)

- [ ] **Step 1: ICartClient**
```csharp
namespace PizzaApp.Order.Core.Interfaces;

public record CartLine(string ProductId, int Quantity, string Size);

public interface ICartClient
{
    Task<List<CartLine>> GetCartAsync();
}
```

- [ ] **Step 2: IOrderService thêm** `Task<OrderResultDto> CheckoutFromCartAsync(string userId, string deliveryAddress);`

- [ ] **Step 3: OrderService** — inject `ICartClient cartClient`, thêm:
```csharp
public async Task<OrderResultDto> CheckoutFromCartAsync(string userId, string deliveryAddress)
{
    var cart = await _cartClient.GetCartAsync();
    if (cart.Count == 0) throw new InvalidOperationException("Giỏ hàng của bạn đang trống.");
    var dto = new CreateOrderDto
    {
        DeliveryAddress = deliveryAddress,
        Items = cart.Select(c => new OrderItemDto { ProductId = c.ProductId, Quantity = c.Quantity, Size = c.Size }).ToList()
    };
    return await CreateOrderAsync(userId, dto);
}
```
(Constructor thêm tham số `ICartClient cartClient` và gán field.)

- [ ] **Step 4: CartHttpClient** — forward JWT qua IHttpContextAccessor:
```csharp
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using PizzaApp.Order.Core.Interfaces;

namespace PizzaApp.Order.Infrastructure.Clients;

public class CartHttpClient : ICartClient
{
    private readonly HttpClient _http;
    private readonly IHttpContextAccessor _ctx;
    public CartHttpClient(HttpClient http, IHttpContextAccessor ctx) { _http = http; _ctx = ctx; }

    public async Task<List<CartLine>> GetCartAsync()
    {
        var req = new HttpRequestMessage(HttpMethod.Get, "api/cart");
        var token = _ctx.HttpContext?.Request.Headers["Authorization"].ToString();
        if (!string.IsNullOrEmpty(token)) req.Headers.TryAddWithoutValidation("Authorization", token);
        var resp = await _http.SendAsync(req);
        if (!resp.IsSuccessStatusCode) return new();
        var items = await resp.Content.ReadFromJsonAsync<List<CartResp>>();
        return items?.Select(i => new CartLine(i.ProductId, i.Quantity, i.Size)).ToList() ?? new();
    }

    private class CartResp
    {
        public string ProductId { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public string Size { get; set; } = "M";
    }
}
```

- [ ] **Step 5:** Build Order.Infrastructure (chưa cần test riêng — logic mỏng).

---

### Task 2: Order.API — routes khớp app + DI

**Files:** Modify `Order.API/Controllers/OrdersController.cs`, `Program.cs`, `appsettings.json`, `docker-compose.yml`.

- [ ] **Step 1: OrdersController** viết lại đúng route monolith:
```csharp
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
            return Ok(new { orderId = order.Id, checkoutUrl = order.PaymentUrl, qrCode = order.PaymentQr, message = "Đã tạo đơn hàng thành công!" });
        }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpGet("my")]
    public async Task<IActionResult> My() => Ok(await _orderService.GetMyOrdersAsync(UserId()));

    [HttpPost("{id}/cancel")]
    public async Task<IActionResult> Cancel(string id)
        => await _orderService.CancelOrderAsync(id, UserId()) ? Ok(new { message = "Đã hủy đơn" }) : BadRequest(new { message = "Không thể hủy đơn" });

    [HttpGet("all")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> All() => Ok(await _orderService.GetAllOrdersAsync());

    [HttpPost("{id}/confirm-payment")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ConfirmPayment(string id)
        => await _orderService.ConfirmPaymentAsync(id) ? Ok(new { message = "Đã xác nhận thanh toán" }) : BadRequest(new { message = "Không thể xác nhận" });

    [HttpPatch("{id}/status")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Status(string id, [FromBody] string status)
        => await _orderService.UpdateOrderStatusAsync(id, status) ? Ok(new { message = "Đã cập nhật" }) : NotFound();

    [HttpGet("shipper/available")]
    [Authorize(Roles = "Shipper,Admin")]
    public async Task<IActionResult> Available() => Ok(await _orderService.GetOrdersByStatusAsync("Ready"));

    [HttpGet("shipper/mine")]
    [Authorize(Roles = "Shipper,Admin")]
    public async Task<IActionResult> ShipperMine() => Ok(await _orderService.GetShipperOrdersAsync(UserId()));

    [HttpPost("{id}/claim")]
    [Authorize(Roles = "Shipper,Admin")]
    public async Task<IActionResult> Claim(string id)
        => await _orderService.ClaimOrderAsync(id, UserId()) ? Ok(new { message = "Đã nhận đơn." }) : BadRequest(new { message = "Không nhận được đơn (đã có shipper khác nhận)." });

    [HttpPost("{id}/delivery-status")]
    [Authorize(Roles = "Shipper,Admin")]
    public async Task<IActionResult> Delivery(string id, [FromBody] string status)
        => await _orderService.UpdateDeliveryStatusAsync(id, UserId(), status) ? Ok(new { message = "Đã cập nhật" }) : BadRequest(new { message = "Cập nhật thất bại" });
}
```

- [ ] **Step 2: Program.cs** thêm IHttpContextAccessor + CartHttpClient:
```csharp
builder.Services.AddHttpContextAccessor();
var cartUrl = builder.Configuration["Services:CartUrl"] ?? "http://localhost:5004/";
builder.Services.AddHttpClient<ICartClient, CartHttpClient>(c => { c.BaseAddress = new Uri(cartUrl); c.Timeout = TimeSpan.FromSeconds(5); });
```
(thêm `using PizzaApp.Order.Infrastructure.Clients;` nếu chưa có — đã có.)

- [ ] **Step 3: appsettings.json** thêm `"Services": { ..., "CartUrl": "http://localhost:5004/" }`.

- [ ] **Step 4: docker-compose** — `order.environment` thêm `Services__CartUrl=http://cart:8080/`; `order.depends_on` thêm `cart`.

- [ ] **Step 5:** Build + test toàn solution = 0 error, pass. Boot-check Order.API.

---

### Task 3: Gateway route ảnh /uploads

- [ ] **Step 1:** `ApiGateway/appsettings.json` thêm route:
```json
"uploads-route": { "ClusterId": "product-cluster", "Match": { "Path": "/uploads/{**catch-all}" } }
```
(dùng lại `product-cluster` sẵn có — ảnh do Product service phục vụ.)

- [ ] **Step 2:** Build gateway.

---

### Task 4: Flutter đổi baseUrl → gateway

- [ ] **Step 1:** `pizza_flutter/lib/core/constants.dart`:
```dart
static String get baseUrl =>
    kIsWeb ? 'http://localhost:8080/api' : 'http://10.0.2.2:8080/api';
```
(Thiết bị thật: đổi thành `http://<IP-LAN>:8080/api`.)

- [ ] **Step 2:** `flutter analyze` (nếu Flutter SDK có) để chắc không lỗi cú pháp. Nếu không có Flutter SDK, chỉ sửa file.

---

## Self-Review
- App gọi `/orders/checkout`, `/orders/all`, `/orders/{id}/confirm-payment`, PATCH `/orders/{id}/status`, `/orders/shipper/available`, `/orders/{id}/delivery-status` → Order.API giờ khớp hết ✓
- Checkout đọc giỏ từ Cart (REST + forward JWT) ✓; trả `{orderId, checkoutUrl}` như app mong đợi ✓
- checkoutUrl = trang cổng thanh toán QR → app `launchUrl` mở → xác nhận ✓
- Ảnh `/uploads/*` route qua gateway → Product service ✓
- baseUrl app → gateway 8080 ✓
- **Rủi ro:** app `launchUrl` mở `http://10.0.2.2:8080/...` trên emulator OK; thiết bị thật cần IP LAN + `PUBLIC_BASE_URL` khớp để QR/redirect trỏ đúng. Cần Docker chạy để test thật.
