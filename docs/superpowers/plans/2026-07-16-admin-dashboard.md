# Admin Dashboard Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:executing-plans. Steps dùng checkbox (`- [ ]`).

**Goal:** Thêm màn hình dashboard cho Admin trong app Flutter, số liệu lấy từ 4 endpoint `/admin/stats` do từng microservice tự tính.

**Architecture:** Mỗi service expose `GET /api/<service>/admin/stats` (`[Authorize(Roles="Admin")]`), tính bằng MongoDB aggregation. App gọi 4 endpoint song song (`Future.wait`), 1 service lỗi không kéo sập cả màn hình.

**Tech Stack:** .NET 8, MongoDB.Driver 2.28, Flutter, YARP Gateway (cổng 8090).

## Global Constraints

- Kế thừa Global Constraints của Plan 1–5 (net8.0, secrets qua env, alias `OrderEntity`/`ProductEntity`/`PaymentEntity` tránh trùng namespace).
- Mọi endpoint stats: `[Authorize(Roles = "Admin")]`.
- **Doanh thu** = `PaymentStatus == "Paid"` **và** `Status != "Cancelled"`. **Số đơn** = đếm mọi đơn.
- **"Hôm nay"** = `DateTime.UtcNow.Date` → hết ngày, theo **UTC** (khớp `CreatedAt`).
- `topProducts` = top 5 theo **số lượng**, chỉ từ đơn đã thanh toán.
- `byStatus` = đủ **7 trạng thái**: `AwaitingPayment, Paid, Preparing, Ready, Delivering, Done, Cancelled` (không có đơn → 0).
- Flutter: **không chế màu mới** — dùng lại màu trong `lib/core/order_status.dart`.
- Tên field BSON = tên property C# (PascalCase) — `AutoMap()` không đổi tên.

---

## File Structure

```
backend/src/BuildingBlocks/Mongo/MongoContext.cs           (SỬA: đăng ký serializer decimal)
backend/src/Services/Order/
  PizzaApp.Order.Core/DTOs/OrderDtos.cs                    (SỬA: + OrderStatsDto, TopProductDto)
  PizzaApp.Order.Core/Interfaces/IOrderService.cs          (SỬA: + GetStatsAsync)
  PizzaApp.Order.Infrastructure/Services/OrderService.cs   (SỬA: + GetStatsAsync + helper thuần)
  PizzaApp.Order.API/Controllers/OrdersController.cs       (SỬA: + GET admin/stats)
backend/src/Services/Auth/...      (tương tự: AuthStatsDto, GetStatsAsync, GET admin/stats)
backend/src/Services/Product/...   (tương tự)
backend/src/Services/Category/...  (tương tự)
backend/tests/PizzaApp.Order.Tests/OrderStatsTests.cs      (TẠO)
pizza_flutter/lib/core/order_status.dart                   (SỬA: tách hàm nhận String)
pizza_flutter/lib/models/dashboard.dart                    (TẠO)
pizza_flutter/lib/services/dashboard_service.dart          (TẠO)
pizza_flutter/lib/screens/admin_dashboard_screen.dart      (TẠO)
pizza_flutter/lib/screens/account_screen.dart              (SỬA: + menu "Bảng điều khiển")
```

---

### Task 1: Sửa bug — decimal bị lưu thành String

**Bối cảnh:** MongoDB.Driver mặc định lưu `decimal` thành **String** → `$sum` không cộng được, sort theo giá sai thứ tự (`79000` đứng sau `129000`). Sửa bằng cách đăng ký serializer **toàn cục 1 lần** ở BuildingBlocks — không gắn attribute vào entity (giữ Core sạch, không phụ thuộc MongoDB).

**Files:**
- Modify: `backend/src/BuildingBlocks/Mongo/MongoContext.cs`

**Interfaces:**
- Produces: mọi `decimal`/`decimal?` trong mọi service được lưu dưới dạng `Decimal128` (số) thay vì String.

- [ ] **Step 1: Xem bug trước khi sửa (để đối chiếu sau)**

Run:
```bash
docker exec backend-mongo-1 mongosh "$(grep '^MONGO_CONNECTION=' backend/.env | cut -d= -f2-)" --quiet --eval 'db.getSiblingDB("PizzaApp_Product").Products.find({},{Name:1,Price:1,_id:0}).sort({Price:1}).forEach(x=>print(x.Price+"  "+x.Name))'
```
Expected: thứ tự SAI (`10000.0`, `119000`, `129000`, `79000`) và `Price` là chuỗi.

- [ ] **Step 2: Đăng ký serializer decimal toàn cục**

Sửa `backend/src/BuildingBlocks/Mongo/MongoContext.cs` thành:

```csharp
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;

namespace PizzaApp.BuildingBlocks.Mongo;

public class MongoContext
{
    private readonly IMongoDatabase _database;

    // Chạy 1 lần cho cả tiến trình, TRƯỚC khi map bất kỳ entity nào.
    static MongoContext()
    {
        RegisterDecimalAsNumber();
    }

    /// <summary>
    /// Mặc định driver lưu decimal thành String -> $sum không cộng được,
    /// sort theo giá sai thứ tự (so sánh chuỗi). Ép lưu thành Decimal128 (số).
    /// </summary>
    private static void RegisterDecimalAsNumber()
    {
        BsonSerializer.RegisterSerializer(typeof(decimal),
            new DecimalSerializer(BsonType.Decimal128));
        BsonSerializer.RegisterSerializer(typeof(decimal?),
            new NullableSerializer<decimal>(new DecimalSerializer(BsonType.Decimal128)));
    }

    public MongoContext(MongoSettings settings)
    {
        var client = new MongoClient(settings.ConnectionString);
        _database = client.GetDatabase(settings.DatabaseName);
    }

    public IMongoCollection<T> GetCollection<T>(string name) =>
        _database.GetCollection<T>(name);
}
```

- [ ] **Step 3: Build**

Run: `cd backend && dotnet build PizzaApp.Microservices.sln`
Expected: `Build succeeded. 0 Error(s)`

- [ ] **Step 4: Convert 4 sản phẩm cũ từ String sang số**

Run:
```bash
cd backend
URI=$(grep '^MONGO_CONNECTION=' .env | cut -d= -f2-)
docker exec backend-mongo-1 mongosh "$URI" --quiet --eval '
const c = db.getSiblingDB("PizzaApp_Product").Products;
let n = 0;
c.find({ Price: { $type: "string" } }).forEach(p => {
  c.updateOne({_id: p._id}, {$set: {Price: NumberDecimal(p.Price)}});
  n++;
});
print("da convert " + n + " san pham");
'
```
Expected: `da convert 4 san pham`

> Đơn hàng/giỏ/payment trên Atlas đang **rỗng** → không cần convert, đơn mới sẽ tự đúng nhờ Step 2.

- [ ] **Step 5: Xác minh bug đã hết**

Run:
```bash
docker exec backend-mongo-1 mongosh "$URI" --quiet --eval 'db.getSiblingDB("PizzaApp_Product").Products.find({},{Name:1,Price:1,_id:0}).sort({Price:1}).forEach(x=>print(x.Price+"  "+x.Name))'
```
Expected: thứ tự ĐÚNG tăng dần — `10000`, `79000` (Veggie), `119000`, `129000`.

- [ ] **Step 6: Xác minh app không hỏng (giá vẫn đọc được)**

Run: `curl -s http://localhost:8090/api/products | head -c 200`
Expected: JSON có `"price":10000` — đọc bình thường.

> Nếu lỗi kiểu (`FormatException`), nghĩa là còn document nào đó chưa convert → chạy lại Step 4.

- [ ] **Step 7: Rebuild service dùng decimal & kiểm tra**

Run:
```bash
docker compose up -d --build product order cart payment
curl -s http://localhost:8090/api/products | head -c 120
```
Expected: 200, JSON sản phẩm bình thường.

---

### Task 2: Order — endpoint `/admin/stats`

**Files:**
- Modify: `backend/src/Services/Order/PizzaApp.Order.Core/DTOs/OrderDtos.cs`
- Modify: `backend/src/Services/Order/PizzaApp.Order.Core/Interfaces/IOrderService.cs`
- Modify: `backend/src/Services/Order/PizzaApp.Order.Infrastructure/Services/OrderService.cs`
- Modify: `backend/src/Services/Order/PizzaApp.Order.API/Controllers/OrdersController.cs`
- Create: `backend/tests/PizzaApp.Order.Tests/OrderStatsTests.cs`

**Interfaces:**
- Produces:
  - `class OrderStatsDto { decimal RevenueToday; decimal RevenueTotal; int OrdersToday; int OrdersTotal; Dictionary<string,int> ByStatus; List<TopProductDto> TopProducts; }`
  - `class TopProductDto { string ProductName; int Quantity; decimal Revenue; }`
  - `Task<OrderStatsDto> IOrderService.GetStatsAsync()`
  - `static Dictionary<string,int> OrderService.NormalizeByStatus(Dictionary<string,int> raw)` — đảm bảo đủ 7 trạng thái

- [ ] **Step 1: Thêm DTO**

Thêm vào cuối `backend/src/Services/Order/PizzaApp.Order.Core/DTOs/OrderDtos.cs`:

```csharp
public class TopProductDto
{
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal Revenue { get; set; }
}

public class OrderStatsDto
{
    public decimal RevenueToday { get; set; }
    public decimal RevenueTotal { get; set; }
    public int OrdersToday { get; set; }
    public int OrdersTotal { get; set; }
    public Dictionary<string, int> ByStatus { get; set; } = new();
    public List<TopProductDto> TopProducts { get; set; } = new();
}
```

- [ ] **Step 2: Thêm vào interface**

Trong `IOrderService.cs`, thêm dòng sau `Task<List<OrderResultDto>> GetAllOrdersAsync();`:
```csharp
    Task<OrderStatsDto> GetStatsAsync();
```

- [ ] **Step 3: Viết test thất bại cho NormalizeByStatus**

Tạo `backend/tests/PizzaApp.Order.Tests/OrderStatsTests.cs`:

```csharp
using PizzaApp.Order.Infrastructure.Services;
using Xunit;

namespace PizzaApp.Order.Tests;

public class OrderStatsTests
{
    [Fact]
    public void NormalizeByStatus_DienDu7TrangThai_ThieuThiBang0()
    {
        var raw = new Dictionary<string, int> { ["Paid"] = 5, ["Done"] = 30 };

        var result = OrderService.NormalizeByStatus(raw);

        Assert.Equal(7, result.Count);
        Assert.Equal(5, result["Paid"]);
        Assert.Equal(30, result["Done"]);
        Assert.Equal(0, result["AwaitingPayment"]);
        Assert.Equal(0, result["Preparing"]);
        Assert.Equal(0, result["Ready"]);
        Assert.Equal(0, result["Delivering"]);
        Assert.Equal(0, result["Cancelled"]);
    }

    [Fact]
    public void NormalizeByStatus_BoQuaTrangThaiLa()
    {
        var raw = new Dictionary<string, int> { ["Paid"] = 2, ["TrangThaiLa"] = 99 };

        var result = OrderService.NormalizeByStatus(raw);

        Assert.Equal(7, result.Count);
        Assert.False(result.ContainsKey("TrangThaiLa"));
        Assert.Equal(2, result["Paid"]);
    }

    [Fact]
    public void NormalizeByStatus_RongThiTatCaBang0()
    {
        var result = OrderService.NormalizeByStatus(new Dictionary<string, int>());

        Assert.Equal(7, result.Count);
        Assert.All(result.Values, v => Assert.Equal(0, v));
    }
}
```

- [ ] **Step 4: Chạy test — phải FAIL**

Run: `cd backend && dotnet test tests/PizzaApp.Order.Tests`
Expected: FAIL biên dịch — `NormalizeByStatus` không tồn tại.

- [ ] **Step 5: Viết GetStatsAsync + helper**

Thêm vào `OrderService.cs` (trước `public static OrderResultDto MapToDto`):

```csharp
    /// <summary>Danh sách trạng thái hợp lệ, theo đúng vòng đời đơn.</summary>
    private static readonly string[] AllStatuses =
        { "AwaitingPayment", "Paid", "Preparing", "Ready", "Delivering", "Done", "Cancelled" };

    /// <summary>Điền đủ 7 trạng thái (thiếu = 0), bỏ trạng thái lạ.</summary>
    public static Dictionary<string, int> NormalizeByStatus(Dictionary<string, int> raw)
    {
        var result = new Dictionary<string, int>();
        foreach (var s in AllStatuses)
            result[s] = raw.TryGetValue(s, out var n) ? n : 0;
        return result;
    }

    public async Task<OrderStatsDto> GetStatsAsync()
    {
        var todayStart = DateTime.UtcNow.Date;

        var b = Builders<OrderEntity>.Filter;
        // Doanh thu: chỉ đơn đã trả tiền và không bị huỷ
        var paidFilter = b.And(b.Eq(o => o.PaymentStatus, "Paid"), b.Ne(o => o.Status, "Cancelled"));
        var paidTodayFilter = b.And(paidFilter, b.Gte(o => o.CreatedAt, todayStart));

        var revenueTotal = await SumRevenueAsync(paidFilter);
        var revenueToday = await SumRevenueAsync(paidTodayFilter);

        // Số đơn: đếm MỌI đơn (kể cả chưa trả tiền / đã huỷ)
        var ordersTotal = (int)await _orders.CountDocumentsAsync(b.Empty);
        var ordersToday = (int)await _orders.CountDocumentsAsync(b.Gte(o => o.CreatedAt, todayStart));

        // Đếm theo trạng thái
        var statusGroups = await _orders.Aggregate()
            .Group(o => o.Status, g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();
        var rawByStatus = statusGroups.ToDictionary(x => x.Status ?? "", x => x.Count);

        return new OrderStatsDto
        {
            RevenueTotal = revenueTotal,
            RevenueToday = revenueToday,
            OrdersTotal = ordersTotal,
            OrdersToday = ordersToday,
            ByStatus = NormalizeByStatus(rawByStatus),
            TopProducts = await GetTopProductsAsync()
        };
    }

    private async Task<decimal> SumRevenueAsync(FilterDefinition<OrderEntity> filter)
    {
        var result = await _orders.Aggregate().Match(filter)
            .Group(o => 1, g => new { Total = g.Sum(o => o.TotalPrice) })
            .FirstOrDefaultAsync();
        return result?.Total ?? 0m;
    }

    /// <summary>Top 5 món bán chạy (theo số lượng), chỉ tính đơn đã thanh toán.</summary>
    private async Task<List<TopProductDto>> GetTopProductsAsync()
    {
        var pipeline = new[]
        {
            new BsonDocument("$match", new BsonDocument
            {
                { "PaymentStatus", "Paid" },
                { "Status", new BsonDocument("$ne", "Cancelled") }
            }),
            new BsonDocument("$unwind", "$OrderItems"),
            new BsonDocument("$group", new BsonDocument
            {
                { "_id", "$OrderItems.ProductName" },
                { "quantity", new BsonDocument("$sum", "$OrderItems.Quantity") },
                { "revenue", new BsonDocument("$sum",
                    new BsonDocument("$multiply", new BsonArray
                        { "$OrderItems.Quantity", "$OrderItems.UnitPrice" })) }
            }),
            new BsonDocument("$sort", new BsonDocument("quantity", -1)),
            new BsonDocument("$limit", 5)
        };

        var docs = await _orders.Aggregate<BsonDocument>(pipeline).ToListAsync();
        return docs.Select(d => new TopProductDto
        {
            ProductName = d["_id"].IsBsonNull ? "" : d["_id"].AsString,
            Quantity = d["quantity"].ToInt32(),
            Revenue = d["revenue"].ToDecimal()
        }).ToList();
    }
```

- [ ] **Step 6: Chạy test — phải PASS**

Run: `cd backend && dotnet test tests/PizzaApp.Order.Tests`
Expected: PASS (4 test: 1 cũ + 3 mới).

- [ ] **Step 7: Thêm endpoint vào controller**

Thêm vào `OrdersController.cs` (sau action `All()`):

```csharp
    [HttpGet("admin/stats")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Stats() => Ok(await _orderService.GetStatsAsync());
```

- [ ] **Step 8: Build + chạy thật**

Run:
```bash
cd backend && dotnet build PizzaApp.Microservices.sln && docker compose up -d --build order
TOKEN=$(curl -s -X POST http://localhost:8090/api/auth/login -H "Content-Type: application/json" -d '{"email":"admin@pizza.com","password":"admin123"}' | sed -n 's/.*"token":"\([^"]*\)".*/\1/p')
curl -s http://localhost:8090/api/orders/admin/stats -H "Authorization: Bearer $TOKEN"
```
Expected: JSON có `revenueToday`, `revenueTotal`, `ordersTotal`, `byStatus` (đủ 7 khoá), `topProducts`.

---

### Task 3: Auth — endpoint `/admin/stats`

**Files:**
- Modify: `backend/src/Services/Auth/PizzaApp.Auth.Core/DTOs/UserProfileDto.cs`
- Modify: `backend/src/Services/Auth/PizzaApp.Auth.Core/Interfaces/IAuthService.cs`
- Modify: `backend/src/Services/Auth/PizzaApp.Auth.Infrastructure/Services/AuthService.cs`
- Modify: `backend/src/Services/Auth/PizzaApp.Auth.API/Controllers/AuthController.cs`

**Interfaces:**
- Produces: `class AuthStatsDto { int TotalUsers; Dictionary<string,int> ByRole; }`, `Task<AuthStatsDto> IAuthService.GetStatsAsync()`

- [ ] **Step 1: Thêm DTO**

Thêm vào cuối `UserProfileDto.cs`:
```csharp
public class AuthStatsDto
{
    public int TotalUsers { get; set; }
    public Dictionary<string, int> ByRole { get; set; } = new();
}
```

- [ ] **Step 2: Thêm vào interface** — trong `IAuthService.cs` thêm:
```csharp
    Task<AuthStatsDto> GetStatsAsync();
```

- [ ] **Step 3: Cài đặt trong `AuthService.cs`** (thêm trước `private string GenerateToken` nếu có, hoặc cuối class):
```csharp
    public async Task<AuthStatsDto> GetStatsAsync()
    {
        var groups = await _users.Aggregate()
            .Group(u => u.Role, g => new { Role = g.Key, Count = g.Count() })
            .ToListAsync();

        var byRole = groups.ToDictionary(x => string.IsNullOrEmpty(x.Role) ? "Customer" : x.Role,
                                         x => x.Count);
        return new AuthStatsDto
        {
            TotalUsers = byRole.Values.Sum(),
            ByRole = byRole
        };
    }
```

- [ ] **Step 4: Thêm endpoint** vào `AuthController.cs`:
```csharp
    [HttpGet("admin/stats")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Stats() => Ok(await _authService.GetStatsAsync());
```

- [ ] **Step 5: Build + verify**
```bash
cd backend && dotnet build PizzaApp.Microservices.sln && docker compose up -d --build auth
curl -s http://localhost:8090/api/auth/admin/stats -H "Authorization: Bearer $TOKEN"
```
Expected: `{"totalUsers":22,"byRole":{"Customer":...,"Admin":...}}`

---

### Task 4: Product + Category — endpoint `/admin/stats`

**Files:**
- Modify: `backend/src/Services/Product/PizzaApp.Product.Core/DTOs/ProductDto.cs`, `Interfaces/IProductService.cs`, `Infrastructure/Services/ProductService.cs`, `API/Controllers/ProductsController.cs`
- Modify: `backend/src/Services/Category/PizzaApp.Category.Core/DTOs/CategoryDTO.cs`, `Interfaces/ICategoryService.cs`, `Infrastructure/Services/CategoryService.cs`, `API/Controllers/CategoryController.cs`

**Interfaces:**
- Produces: `class ProductStatsDto { int TotalProducts; int Available; int Unavailable; }`, `class CategoryStatsDto { int TotalCategories; }`

- [ ] **Step 1: Product DTO** — thêm vào cuối `ProductDto.cs`:
```csharp
public class ProductStatsDto
{
    public int TotalProducts { get; set; }
    public int Available { get; set; }
    public int Unavailable { get; set; }
}
```

- [ ] **Step 2: Product interface** — thêm vào `IProductService.cs`:
```csharp
    Task<ProductStatsDto> GetStatsAsync();
```

- [ ] **Step 3: Product service** — thêm vào `ProductService.cs`:
```csharp
    public async Task<ProductStatsDto> GetStatsAsync()
    {
        var total = (int)await _products.CountDocumentsAsync(Builders<ProductEntity>.Filter.Empty);
        var available = (int)await _products.CountDocumentsAsync(
            Builders<ProductEntity>.Filter.Eq(p => p.IsAvailable, true));
        return new ProductStatsDto
        {
            TotalProducts = total,
            Available = available,
            Unavailable = total - available
        };
    }
```

- [ ] **Step 4: Product controller** — thêm vào `ProductsController.cs`:
```csharp
    [HttpGet("admin/stats")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Stats() => Ok(await _productService.GetStatsAsync());
```

> ⚠️ Đặt action này **TRƯỚC** `GetById(string id)` trong file, tránh route `{id}` nuốt mất `admin/stats`.

- [ ] **Step 5: Category DTO** — thêm vào cuối `CategoryDTO.cs`:
```csharp
public class CategoryStatsDto
{
    public int TotalCategories { get; set; }
}
```

- [ ] **Step 6: Category interface** — thêm vào `ICategoryService.cs`:
```csharp
    Task<CategoryStatsDto> GetStatsAsync();
```

- [ ] **Step 7: Category service** — thêm vào `CategoryService.cs`:
```csharp
    public async Task<CategoryStatsDto> GetStatsAsync()
    {
        var total = (int)await _categories.CountDocumentsAsync(
            Builders<CategoryEntity>.Filter.Empty);
        return new CategoryStatsDto { TotalCategories = total };
    }
```

- [ ] **Step 8: Category controller** — thêm vào `CategoryController.cs`, **trước** `GetById`:
```csharp
    [HttpGet("admin/stats")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Stats() => Ok(await _categoryService.GetStatsAsync());
```

- [ ] **Step 9: Build + verify cả 4 endpoint**
```bash
cd backend && dotnet build PizzaApp.Microservices.sln && docker compose up -d --build product category
for e in orders/admin/stats auth/admin/stats products/admin/stats category/admin/stats; do
  echo "--- $e ---"; curl -s "http://localhost:8090/api/$e" -H "Authorization: Bearer $TOKEN"; echo
done
```
Expected: cả 4 trả JSON hợp lệ, không cái nào 401/404.

---

### Task 5: Flutter — tái sử dụng màu + model + service

**Files:**
- Modify: `pizza_flutter/lib/core/order_status.dart`
- Create: `pizza_flutter/lib/models/dashboard.dart`
- Create: `pizza_flutter/lib/services/dashboard_service.dart`

**Interfaces:**
- Produces:
  - `String orderStatusLabelByName(String status)` / `Color orderStatusColorByName(String status)`
  - `class OrderStats`, `AuthStats`, `ProductStats`, `CategoryStats`, `DashboardData`
  - `Future<DashboardData> DashboardService.load()`

- [ ] **Step 1: Tách hàm nhận String trong `order_status.dart`**

Dashboard chỉ có **tên trạng thái** (khoá của `byStatus`), không có `OrderModel` → tách phần lõi ra để dùng chung, tránh copy màu (DRY).

Thay `order_status.dart` thành:

```dart
import 'package:flutter/material.dart';
import '../models/order.dart';

const _unpaidColor = Color(0xFFBA7517);

/// Nhãn theo TÊN trạng thái (dùng cho dashboard - nơi chỉ có chuỗi).
String orderStatusLabelByName(String status) {
  switch (status) {
    case 'AwaitingPayment':
      return 'Chờ thanh toán';
    case 'Paid':
      return 'Đã thanh toán';
    case 'Preparing':
      return 'Đang chuẩn bị';
    case 'Ready':
      return 'Chờ giao';
    case 'Delivering':
      return 'Đang giao';
    case 'Done':
      return 'Đã giao';
    case 'Cancelled':
      return 'Đã hủy';
    default:
      return status;
  }
}

/// Màu theo TÊN trạng thái (dùng cho dashboard).
Color orderStatusColorByName(String status) {
  switch (status) {
    case 'AwaitingPayment':
      return _unpaidColor;
    case 'Paid':
      return const Color(0xFF8E44AD);
    case 'Preparing':
      return const Color(0xFFD85A30);
    case 'Ready':
      return const Color(0xFFBA7517);
    case 'Delivering':
      return const Color(0xFF2D7DD2);
    case 'Done':
      return const Color(0xFF639922);
    case 'Cancelled':
      return Colors.grey;
    default:
      return const Color(0xFF639922);
  }
}

String orderStatusLabel(OrderModel o) =>
    o.isUnpaid ? 'Chờ thanh toán' : orderStatusLabelByName(o.status);

Color orderStatusColor(OrderModel o) =>
    o.isUnpaid ? _unpaidColor : orderStatusColorByName(o.status);

Widget orderStatusBadge(OrderModel o) {
  final color = orderStatusColor(o);
  return Container(
    padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
    decoration: BoxDecoration(
      color: color.withValues(alpha: 0.12),
      borderRadius: BorderRadius.circular(20),
    ),
    child: Text(orderStatusLabel(o),
        style: TextStyle(
            fontSize: 11, fontWeight: FontWeight.w700, color: color)),
  );
}
```

- [ ] **Step 2: Kiểm tra không hỏng màn cũ**

Run: `cd pizza_flutter && flutter analyze lib/core/order_status.dart`
Expected: `No issues found!` (hành vi giữ nguyên: `orderStatusLabel`/`orderStatusColor`/`orderStatusBadge` vẫn dùng được như cũ).

- [ ] **Step 3: Tạo model** `pizza_flutter/lib/models/dashboard.dart`:

```dart
class TopProduct {
  final String productName;
  final int quantity;
  final double revenue;
  TopProduct({required this.productName, required this.quantity, required this.revenue});

  factory TopProduct.fromJson(Map<String, dynamic> j) => TopProduct(
        productName: j['productName'] ?? '',
        quantity: (j['quantity'] as num?)?.toInt() ?? 0,
        revenue: (j['revenue'] as num?)?.toDouble() ?? 0,
      );
}

class OrderStats {
  final double revenueToday, revenueTotal;
  final int ordersToday, ordersTotal;
  final Map<String, int> byStatus;
  final List<TopProduct> topProducts;
  OrderStats({
    required this.revenueToday, required this.revenueTotal,
    required this.ordersToday, required this.ordersTotal,
    required this.byStatus, required this.topProducts,
  });

  factory OrderStats.fromJson(Map<String, dynamic> j) => OrderStats(
        revenueToday: (j['revenueToday'] as num?)?.toDouble() ?? 0,
        revenueTotal: (j['revenueTotal'] as num?)?.toDouble() ?? 0,
        ordersToday: (j['ordersToday'] as num?)?.toInt() ?? 0,
        ordersTotal: (j['ordersTotal'] as num?)?.toInt() ?? 0,
        byStatus: ((j['byStatus'] as Map?) ?? {})
            .map((k, v) => MapEntry(k.toString(), (v as num).toInt())),
        topProducts: ((j['topProducts'] as List?) ?? [])
            .map((e) => TopProduct.fromJson(e))
            .toList(),
      );
}

class AuthStats {
  final int totalUsers;
  final Map<String, int> byRole;
  AuthStats({required this.totalUsers, required this.byRole});

  factory AuthStats.fromJson(Map<String, dynamic> j) => AuthStats(
        totalUsers: (j['totalUsers'] as num?)?.toInt() ?? 0,
        byRole: ((j['byRole'] as Map?) ?? {})
            .map((k, v) => MapEntry(k.toString(), (v as num).toInt())),
      );
}

class ProductStats {
  final int totalProducts, available, unavailable;
  ProductStats({required this.totalProducts, required this.available, required this.unavailable});

  factory ProductStats.fromJson(Map<String, dynamic> j) => ProductStats(
        totalProducts: (j['totalProducts'] as num?)?.toInt() ?? 0,
        available: (j['available'] as num?)?.toInt() ?? 0,
        unavailable: (j['unavailable'] as num?)?.toInt() ?? 0,
      );
}

class CategoryStats {
  final int totalCategories;
  CategoryStats({required this.totalCategories});

  factory CategoryStats.fromJson(Map<String, dynamic> j) => CategoryStats(
        totalCategories: (j['totalCategories'] as num?)?.toInt() ?? 0,
      );
}

/// Gộp 4 nguồn. Cái nào null = service đó lỗi -> UI hiện "—".
class DashboardData {
  final OrderStats? orders;
  final AuthStats? auth;
  final ProductStats? products;
  final CategoryStats? categories;
  DashboardData({this.orders, this.auth, this.products, this.categories});

  bool get allFailed =>
      orders == null && auth == null && products == null && categories == null;
}
```

- [ ] **Step 4: Tạo service** `pizza_flutter/lib/services/dashboard_service.dart`:

```dart
import 'dart:convert';
import 'package:http/http.dart' as http;
import '../core/constants.dart';
import '../models/dashboard.dart';
import 'auth_service.dart';

class DashboardService {
  static Future<Map<String, String>> _headers() async {
    final token = await AuthService.getToken();
    return {
      'Content-Type': 'application/json',
      'Authorization': 'Bearer $token',
    };
  }

  /// Gọi 1 endpoint, LỖI TRẢ NULL (không ném) -> 1 service chết không kéo sập dashboard.
  static Future<T?> _get<T>(
      String path, T Function(Map<String, dynamic>) parse) async {
    try {
      final res = await http
          .get(Uri.parse('${AppConstants.baseUrl}$path'), headers: await _headers())
          .timeout(const Duration(seconds: 10));
      if (res.statusCode != 200) return null;
      return parse(jsonDecode(res.body));
    } catch (_) {
      return null;
    }
  }

  /// Gọi 4 endpoint SONG SONG (~1 vòng mạng).
  static Future<DashboardData> load() async {
    final results = await Future.wait([
      _get('/orders/admin/stats', (j) => OrderStats.fromJson(j)),
      _get('/auth/admin/stats', (j) => AuthStats.fromJson(j)),
      _get('/products/admin/stats', (j) => ProductStats.fromJson(j)),
      _get('/category/admin/stats', (j) => CategoryStats.fromJson(j)),
    ]);

    return DashboardData(
      orders: results[0] as OrderStats?,
      auth: results[1] as AuthStats?,
      products: results[2] as ProductStats?,
      categories: results[3] as CategoryStats?,
    );
  }
}
```

- [ ] **Step 5: Kiểm tra**

Run: `cd pizza_flutter && flutter analyze lib/models/dashboard.dart lib/services/dashboard_service.dart lib/core/order_status.dart`
Expected: `No issues found!`

---

### Task 6: Flutter — màn hình dashboard + menu

**Files:**
- Create: `pizza_flutter/lib/screens/admin_dashboard_screen.dart`
- Modify: `pizza_flutter/lib/screens/account_screen.dart`

**Interfaces:**
- Consumes: `DashboardService.load()`, `orderStatusLabelByName`, `orderStatusColorByName`
- Produces: `class AdminDashboardScreen extends StatefulWidget`

- [ ] **Step 1: Tạo `pizza_flutter/lib/screens/admin_dashboard_screen.dart`**

```dart
import 'package:flutter/material.dart';
import '../core/order_status.dart';
import '../models/dashboard.dart';
import '../services/dashboard_service.dart';

class AdminDashboardScreen extends StatefulWidget {
  const AdminDashboardScreen({super.key});

  @override
  State<AdminDashboardScreen> createState() => _AdminDashboardScreenState();
}

class _AdminDashboardScreenState extends State<AdminDashboardScreen> {
  DashboardData? _data;
  bool _loading = true;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() => _loading = true);
    final d = await DashboardService.load();
    if (mounted) setState(() { _data = d; _loading = false; });
  }

  String _money(double v) {
    final s = v.toStringAsFixed(0);
    final buf = StringBuffer();
    for (var i = 0; i < s.length; i++) {
      if (i > 0 && (s.length - i) % 3 == 0) buf.write('.');
      buf.write(s[i]);
    }
    return '${buf}đ';
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: const Color(0xFFFDF8F3),
      appBar: AppBar(
        backgroundColor: const Color(0xFFD85A30),
        foregroundColor: Colors.white,
        title: const Text('Bảng điều khiển',
            style: TextStyle(fontWeight: FontWeight.w800)),
        elevation: 0,
      ),
      body: _loading
          ? const Center(child: CircularProgressIndicator(color: Color(0xFFD85A30)))
          : RefreshIndicator(
              onRefresh: _load,
              color: const Color(0xFFD85A30),
              child: ListView(
                padding: const EdgeInsets.all(16),
                children: [
                  if (_data!.allFailed) _errorBanner(),
                  _revenueCards(),
                  const SizedBox(height: 16),
                  _statusCard(),
                  const SizedBox(height: 16),
                  _topProductsCard(),
                  const SizedBox(height: 16),
                  _systemCard(),
                ],
              ),
            ),
    );
  }

  Widget _errorBanner() => Container(
        margin: const EdgeInsets.only(bottom: 16),
        padding: const EdgeInsets.all(14),
        decoration: BoxDecoration(
          color: const Color(0xFFFDECEA),
          borderRadius: BorderRadius.circular(12),
        ),
        child: const Row(children: [
          Icon(Icons.wifi_off, color: Colors.red, size: 18),
          SizedBox(width: 8),
          Expanded(child: Text('Không tải được dữ liệu. Kéo xuống để thử lại.',
              style: TextStyle(fontSize: 13, color: Colors.red))),
        ]),
      );

  Widget _card({required String title, required Widget child}) => Container(
        width: double.infinity,
        padding: const EdgeInsets.all(16),
        decoration: BoxDecoration(
          color: Colors.white,
          borderRadius: BorderRadius.circular(14),
          border: Border.all(color: const Color(0xFFD3D1C7), width: 0.5),
        ),
        child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
          Text(title, style: const TextStyle(fontWeight: FontWeight.w800, fontSize: 15)),
          const SizedBox(height: 12),
          child,
        ]),
      );

  Widget _statTile(String label, String value, IconData icon) => Expanded(
        child: Container(
          padding: const EdgeInsets.all(14),
          decoration: BoxDecoration(
            color: Colors.white,
            borderRadius: BorderRadius.circular(14),
            border: Border.all(color: const Color(0xFFD3D1C7), width: 0.5),
          ),
          child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
            Icon(icon, color: const Color(0xFFD85A30), size: 20),
            const SizedBox(height: 8),
            Text(value,
                style: const TextStyle(fontWeight: FontWeight.w900, fontSize: 17)),
            const SizedBox(height: 2),
            Text(label, style: const TextStyle(fontSize: 11, color: Colors.grey)),
          ]),
        ),
      );

  Widget _revenueCards() {
    final o = _data!.orders;
    return Column(children: [
      Row(children: [
        _statTile('Doanh thu hôm nay',
            o == null ? '—' : _money(o.revenueToday), Icons.today),
        const SizedBox(width: 10),
        _statTile('Doanh thu tổng',
            o == null ? '—' : _money(o.revenueTotal), Icons.payments),
      ]),
      const SizedBox(height: 10),
      Row(children: [
        _statTile('Đơn hôm nay',
            o == null ? '—' : '${o.ordersToday}', Icons.receipt_long),
        const SizedBox(width: 10),
        _statTile('Tổng đơn',
            o == null ? '—' : '${o.ordersTotal}', Icons.list_alt),
      ]),
    ]);
  }

  Widget _statusCard() {
    final o = _data!.orders;
    if (o == null) {
      return _card(title: 'Đơn theo trạng thái', child: const Text('— Không tải được',
          style: TextStyle(color: Colors.grey, fontSize: 13)));
    }
    return _card(
      title: 'Đơn theo trạng thái',
      child: Column(
        children: o.byStatus.entries.map((e) {
          final color = orderStatusColorByName(e.key);
          return Padding(
            padding: const EdgeInsets.symmetric(vertical: 5),
            child: Row(children: [
              Container(width: 10, height: 10,
                  decoration: BoxDecoration(color: color, shape: BoxShape.circle)),
              const SizedBox(width: 10),
              Expanded(child: Text(orderStatusLabelByName(e.key),
                  style: const TextStyle(fontSize: 13))),
              Text('${e.value}',
                  style: const TextStyle(fontWeight: FontWeight.w800, fontSize: 13)),
            ]),
          );
        }).toList(),
      ),
    );
  }

  Widget _topProductsCard() {
    final o = _data!.orders;
    if (o == null || o.topProducts.isEmpty) {
      return _card(title: 'Món bán chạy', child: Text(
          o == null ? '— Không tải được' : 'Chưa có đơn đã thanh toán',
          style: const TextStyle(color: Colors.grey, fontSize: 13)));
    }
    return _card(
      title: 'Top món bán chạy',
      child: Column(
        children: o.topProducts.asMap().entries.map((e) {
          final p = e.value;
          return Padding(
            padding: const EdgeInsets.symmetric(vertical: 5),
            child: Row(children: [
              Container(
                width: 22, height: 22, alignment: Alignment.center,
                decoration: BoxDecoration(
                    color: const Color(0xFFFAECE7),
                    borderRadius: BorderRadius.circular(6)),
                child: Text('${e.key + 1}',
                    style: const TextStyle(fontSize: 11, fontWeight: FontWeight.w800,
                        color: Color(0xFFD85A30))),
              ),
              const SizedBox(width: 10),
              Expanded(child: Text(p.productName,
                  style: const TextStyle(fontSize: 13, fontWeight: FontWeight.w600))),
              Text('x${p.quantity}',
                  style: const TextStyle(fontSize: 12, color: Colors.grey)),
              const SizedBox(width: 10),
              Text(_money(p.revenue),
                  style: const TextStyle(fontSize: 12, fontWeight: FontWeight.w800,
                      color: Color(0xFFD85A30))),
            ]),
          );
        }).toList(),
      ),
    );
  }

  Widget _row(String label, String value) => Padding(
        padding: const EdgeInsets.symmetric(vertical: 5),
        child: Row(children: [
          Expanded(child: Text(label, style: const TextStyle(fontSize: 13))),
          Text(value,
              style: const TextStyle(fontWeight: FontWeight.w800, fontSize: 13)),
        ]),
      );

  Widget _systemCard() {
    final a = _data!.auth;
    final p = _data!.products;
    final c = _data!.categories;
    return _card(
      title: 'Tổng quan hệ thống',
      child: Column(children: [
        _row('Tổng người dùng', a == null ? '—' : '${a.totalUsers}'),
        if (a != null)
          ...a.byRole.entries.map((e) => Padding(
                padding: const EdgeInsets.only(left: 12),
                child: _row('· ${e.key}', '${e.value}'),
              )),
        const Divider(height: 18),
        _row('Sản phẩm', p == null ? '—' : '${p.totalProducts}'),
        if (p != null) ...[
          Padding(padding: const EdgeInsets.only(left: 12),
              child: _row('· Còn bán', '${p.available}')),
          Padding(padding: const EdgeInsets.only(left: 12),
              child: _row('· Ngừng bán', '${p.unavailable}')),
        ],
        const Divider(height: 18),
        _row('Danh mục', c == null ? '—' : '${c.totalCategories}'),
      ]),
    );
  }
}
```

- [ ] **Step 2: Thêm menu vào `account_screen.dart`**

Thêm import ở đầu file:
```dart
import 'admin_dashboard_screen.dart';
```

Rồi thêm mục **ngay TRƯỚC** dòng `_menuTile(Icons.receipt_long_outlined, 'Quản lý đơn hàng',` (khoảng dòng 102):
```dart
                      _menuTile(Icons.dashboard_outlined, 'Bảng điều khiển',
                          () => Navigator.push(context, MaterialPageRoute(
                              builder: (_) => const AdminDashboardScreen()))),
```

- [ ] **Step 3: Kiểm tra**

Run: `cd pizza_flutter && flutter analyze lib/screens/admin_dashboard_screen.dart lib/screens/account_screen.dart`
Expected: `No issues found!`

- [ ] **Step 4: Chạy thật trên emulator**

Run:
```bash
cd pizza_flutter && flutter run --no-enable-impeller
```
Sau đó: đăng nhập `admin@pizza.com` / `admin123` → tab **Tài khoản** → **Bảng điều khiển**.
Expected: hiện 4 thẻ số liệu, danh sách 7 trạng thái có chấm màu, top món, tổng quan hệ thống.

- [ ] **Step 5: Kiểm tra resilience (1 service chết không kéo sập)**

Run:
```bash
cd backend && docker compose stop auth
```
Mở lại dashboard trong app (kéo xuống làm mới).
Expected: phần "Tổng người dùng" hiện `—`, **các phần khác vẫn hiện số bình thường**.

Bật lại:
```bash
docker compose start auth
```

---

## Self-Review

**Spec coverage:**
- 4 endpoint `/admin/stats` với payload đúng spec ✓ (Task 2, 3, 4)
- Quy tắc doanh thu (Paid, không Cancelled) ✓ (Task 2 Step 5 — `paidFilter`)
- "Hôm nay" theo UTC ✓ (`DateTime.UtcNow.Date`)
- `byStatus` đủ 7 trạng thái ✓ (`NormalizeByStatus` + 3 test)
- `topProducts` top 5 theo số lượng, chỉ đơn đã trả ✓ (Task 2 Step 5)
- App gọi song song `Future.wait` ✓ (Task 5 Step 4)
- Resilience: 1 service chết vẫn hiện phần còn lại ✓ (Task 5 `_get` trả null + Task 6 Step 5 verify)
- Dùng lại màu `order_status.dart` ✓ (Task 5 Step 1 tách `orderStatusColorByName`)
- Menu "Bảng điều khiển" trên cùng ✓ (Task 6 Step 2)
- Pull-to-refresh ✓ (`RefreshIndicator`)
- YAGNI: không chart/lọc ngày/export ✓

**Phát sinh ngoài spec (bắt buộc):** Task 1 — sửa bug `decimal` bị lưu thành String. Không sửa thì `$sum` trả 0 → dashboard doanh thu **luôn bằng 0**. Spec giả định aggregate được, nhưng thực tế không.

**Placeholder scan:** Không có TBD/TODO; mọi step đều có code hoặc lệnh cụ thể.

**Type consistency:**
- `OrderStatsDto` (C#) ↔ `OrderStats.fromJson` (Dart): `revenueToday/revenueTotal/ordersToday/ordersTotal/byStatus/topProducts` — khớp (ASP.NET trả camelCase mặc định).
- `TopProductDto` ↔ `TopProduct`: `productName/quantity/revenue` — khớp.
- `AuthStatsDto` ↔ `AuthStats`: `totalUsers/byRole` — khớp.
- `ProductStatsDto` ↔ `ProductStats`: `totalProducts/available/unavailable` — khớp.
- `CategoryStatsDto` ↔ `CategoryStats`: `totalCategories` — khớp.
- `NormalizeByStatus` khai báo Task 2 Step 5, test Task 2 Step 3 — khớp.
- `orderStatusColorByName`/`orderStatusLabelByName` tạo Task 5 Step 1, dùng Task 6 Step 1 — khớp.

**Rủi ro:**
- Route `admin/stats` phải đặt **trước** `{id}` ở Product/Category controller, không thì `{id}` nuốt mất → 404/400. Đã ghi cảnh báo ở Task 4.
- `$unwind`/`$sum` phụ thuộc Task 1 đã convert xong. **Task 1 phải chạy trước Task 2.**
- `d["_id"].AsString` sẽ ném nếu `ProductName` null → đã bọc `IsBsonNull`.
