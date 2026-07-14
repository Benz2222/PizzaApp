# Plan 3 — Cart Service Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:executing-plans. Steps use checkbox (`- [ ]`).

**Goal:** Tách **Cart Service** thành microservice độc lập (DB `PizzaApp_Cart`), định tuyến qua Gateway. Khi thêm vào giỏ, Cart gọi REST sang Product Service để lấy snapshot (tên/ảnh/giá) và lưu denormalized.

**Architecture:** Giống Plan 1/2. Cart→Product giao tiếp REST đồng bộ qua `IProductClient`. Cart chỉ đọc/ghi DB của mình. Endpoint yêu cầu JWT (`[Authorize]`), `userId` lấy từ claim.

**Tech Stack:** .NET 8, MongoDB.Driver 2.28, YARP, xUnit, HttpClient.

## Global Constraints

- Kế thừa Global Constraints Plan 1/2 (net8.0, secrets qua env, ID StringObjectIdGenerator, cùng JWT).
- Cart Service: DB `PizzaApp_Cart`, collection `CartItems`, cổng container 8080 (host dev 5004).
- Cart **không** truy cập DB Product. Snapshot (`ProductName`, `ProductImageUrl`, `Price`) lấy qua REST khi AddToCart, lưu denormalized trên CartItem.
- Endpoint giữ đường dẫn monolith: `/api/cart` (controller `Cart`). Toàn bộ `[Authorize]` (mọi role đăng nhập).
- `userId` lấy từ `ClaimTypes.NameIdentifier` (giống monolith).

---

## File Structure (Plan 3)

```
backend/src/Services/Cart/
  PizzaApp.Cart.Core/          Entities/CartItem.cs, DTOs/CartDtos.cs, Interfaces/{ICartService,IProductClient}.cs
  PizzaApp.Cart.Infrastructure/ CartDbContext.cs, Services/CartService.cs, Clients/ProductHttpClient.cs
  PizzaApp.Cart.API/           Controllers/CartController.cs, Program.cs, appsettings.json, Dockerfile
backend/tests/PizzaApp.Cart.Tests/  CartServiceTests.cs
backend/src/ApiGateway/appsettings.json   (thêm route cart)
backend/docker-compose.yml                (thêm service cart)
```

---

### Task 1: Cart Service (Core + Infrastructure + REST client + tests)

**Files:**
- Create Cart.Core: `Entities/CartItem.cs`, `DTOs/CartDtos.cs`, `Interfaces/ICartService.cs`, `Interfaces/IProductClient.cs`
- Create Cart.Infrastructure: `CartDbContext.cs`, `Services/CartService.cs`, `Clients/ProductHttpClient.cs`
- Test: `tests/PizzaApp.Cart.Tests/CartServiceTests.cs`

**Interfaces:**
- Produces:
  - `CartItem` entity (Id, UserId, ProductId, ProductName, ProductImageUrl, Quantity, Price, Size).
  - `CartItemDto` (ProductId, Quantity, Size), `CartResultDto` (+ `TotalLinePrice => Price*Quantity`).
  - `record ProductInfo(string Id, string Name, string ImageUrl, decimal Price)`.
  - `IProductClient { Task<ProductInfo?> GetProductAsync(string productId); }` (null nếu không tồn tại).
  - `ICartService` (GetCart/AddToCart/UpdateQuantity/RemoveFromCart/ClearCart).
  - `CartService(CartDbContext db, IProductClient productClient)`, static `CartService.ToResultDto(CartItem)`.

- [ ] **Step 1: Scaffold projects**

```bash
cd backend
dotnet new classlib -n PizzaApp.Cart.Core -o src/Services/Cart/PizzaApp.Cart.Core -f net8.0
dotnet new classlib -n PizzaApp.Cart.Infrastructure -o src/Services/Cart/PizzaApp.Cart.Infrastructure -f net8.0
dotnet new xunit -n PizzaApp.Cart.Tests -o tests/PizzaApp.Cart.Tests -f net8.0
rm -f src/Services/Cart/PizzaApp.Cart.Core/Class1.cs src/Services/Cart/PizzaApp.Cart.Infrastructure/Class1.cs tests/PizzaApp.Cart.Tests/UnitTest1.cs
dotnet sln add src/Services/Cart/PizzaApp.Cart.Core/PizzaApp.Cart.Core.csproj src/Services/Cart/PizzaApp.Cart.Infrastructure/PizzaApp.Cart.Infrastructure.csproj tests/PizzaApp.Cart.Tests/PizzaApp.Cart.Tests.csproj
dotnet add src/Services/Cart/PizzaApp.Cart.Infrastructure reference src/Services/Cart/PizzaApp.Cart.Core/PizzaApp.Cart.Core.csproj src/BuildingBlocks/PizzaApp.BuildingBlocks.csproj
dotnet add src/Services/Cart/PizzaApp.Cart.Infrastructure package MongoDB.Driver -v 2.28.0
dotnet add tests/PizzaApp.Cart.Tests reference src/Services/Cart/PizzaApp.Cart.Infrastructure/PizzaApp.Cart.Infrastructure.csproj src/Services/Cart/PizzaApp.Cart.Core/PizzaApp.Cart.Core.csproj
```

- [ ] **Step 2: Cart.Core files**

`Entities/CartItem.cs`:
```csharp
namespace PizzaApp.Cart.Core.Entities;

public class CartItem
{
    public string Id { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string ProductImageUrl { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal Price { get; set; }
    public string Size { get; set; } = "M";
}
```

`DTOs/CartDtos.cs`:
```csharp
namespace PizzaApp.Cart.Core.DTOs;

public class CartItemDto
{
    public string ProductId { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string Size { get; set; } = "M";
}

public class CartResultDto
{
    public string Id { get; set; } = string.Empty;
    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string ProductImageUrl { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal Price { get; set; }
    public string Size { get; set; } = "M";
    public decimal TotalLinePrice => Price * Quantity;
}
```

`Interfaces/ICartService.cs`:
```csharp
using PizzaApp.Cart.Core.DTOs;

namespace PizzaApp.Cart.Core.Interfaces;

public interface ICartService
{
    Task<List<CartResultDto>> GetCartAsync(string userId);
    Task AddToCartAsync(string userId, CartItemDto dto);
    Task UpdateQuantityAsync(string userId, string cartItemId, int quantity);
    Task RemoveFromCartAsync(string userId, string cartItemId);
    Task ClearCartAsync(string userId);
}
```

`Interfaces/IProductClient.cs`:
```csharp
namespace PizzaApp.Cart.Core.Interfaces;

public record ProductInfo(string Id, string Name, string ImageUrl, decimal Price);

public interface IProductClient
{
    /// <summary>Trả thông tin sản phẩm, hoặc null nếu không tồn tại.</summary>
    Task<ProductInfo?> GetProductAsync(string productId);
}
```

- [ ] **Step 3: CartDbContext**

`CartDbContext.cs`:
```csharp
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.IdGenerators;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using PizzaApp.Cart.Core.Entities;
using PizzaApp.BuildingBlocks.Mongo;

namespace PizzaApp.Cart.Infrastructure;

public class CartDbContext
{
    public IMongoCollection<CartItem> CartItems { get; }

    public CartDbContext(MongoContext ctx)
    {
        RegisterMappings();
        CartItems = ctx.GetCollection<CartItem>("CartItems");
    }

    public static void RegisterMappings()
    {
        if (!BsonClassMap.IsClassMapRegistered(typeof(CartItem)))
        {
            BsonClassMap.RegisterClassMap<CartItem>(cm =>
            {
                cm.AutoMap();
                cm.SetIgnoreExtraElements(true);
                cm.MapIdProperty(c => c.Id)
                  .SetIdGenerator(StringObjectIdGenerator.Instance)
                  .SetSerializer(new StringSerializer(BsonType.String));
            });
        }
    }
}
```

- [ ] **Step 4: Failing test cho ToResultDto + TotalLinePrice**

`tests/PizzaApp.Cart.Tests/CartServiceTests.cs`:
```csharp
using PizzaApp.Cart.Core.Entities;
using PizzaApp.Cart.Infrastructure.Services;
using Xunit;

namespace PizzaApp.Cart.Tests;

public class CartServiceTests
{
    [Fact]
    public void ToResultDto_MapsFieldsAndComputesLineTotal()
    {
        var item = new CartItem
        {
            Id = "i1", ProductId = "p1", ProductName = "Margherita",
            ProductImageUrl = "/uploads/x.jpg", Quantity = 3, Price = 10m, Size = "L"
        };

        var dto = CartService.ToResultDto(item);

        Assert.Equal("i1", dto.Id);
        Assert.Equal("p1", dto.ProductId);
        Assert.Equal("Margherita", dto.ProductName);
        Assert.Equal("L", dto.Size);
        Assert.Equal(3, dto.Quantity);
        Assert.Equal(30m, dto.TotalLinePrice);
    }
}
```

- [ ] **Step 5: Chạy test — fail** (`dotnet test tests/PizzaApp.Cart.Tests`).

- [ ] **Step 6: CartService** (denormalize; AddToCart gọi Product qua REST)

`Services/CartService.cs`:
```csharp
using MongoDB.Driver;
using PizzaApp.Cart.Core.DTOs;
using PizzaApp.Cart.Core.Entities;
using PizzaApp.Cart.Core.Interfaces;

namespace PizzaApp.Cart.Infrastructure.Services;

public class CartService : ICartService
{
    private readonly IMongoCollection<CartItem> _cartItems;
    private readonly IProductClient _productClient;

    public CartService(CartDbContext db, IProductClient productClient)
    {
        _cartItems = db.CartItems;
        _productClient = productClient;
    }

    public async Task<List<CartResultDto>> GetCartAsync(string userId)
    {
        var items = await _cartItems.Find(c => c.UserId == userId).ToListAsync();
        return items.Select(ToResultDto).ToList();
    }

    public async Task AddToCartAsync(string userId, CartItemDto dto)
    {
        var product = await _productClient.GetProductAsync(dto.ProductId);
        if (product == null) throw new InvalidOperationException("Sản phẩm không tồn tại");

        var existing = await _cartItems
            .Find(c => c.UserId == userId && c.ProductId == dto.ProductId && c.Size == dto.Size)
            .FirstOrDefaultAsync();

        if (existing != null)
        {
            await _cartItems.UpdateOneAsync(c => c.Id == existing.Id,
                Builders<CartItem>.Update.Inc(c => c.Quantity, dto.Quantity));
        }
        else
        {
            var cartItem = new CartItem
            {
                UserId = userId,
                ProductId = dto.ProductId,
                ProductName = product.Name,
                ProductImageUrl = product.ImageUrl,
                Quantity = dto.Quantity,
                Price = product.Price,
                Size = dto.Size
            };
            await _cartItems.InsertOneAsync(cartItem);
        }
    }

    public async Task UpdateQuantityAsync(string userId, string cartItemId, int quantity)
    {
        if (quantity <= 0)
        {
            await _cartItems.DeleteOneAsync(c => c.Id == cartItemId && c.UserId == userId);
        }
        else
        {
            await _cartItems.UpdateOneAsync(c => c.Id == cartItemId && c.UserId == userId,
                Builders<CartItem>.Update.Set(c => c.Quantity, quantity));
        }
    }

    public async Task RemoveFromCartAsync(string userId, string cartItemId)
        => await _cartItems.DeleteOneAsync(c => c.Id == cartItemId && c.UserId == userId);

    public async Task ClearCartAsync(string userId)
        => await _cartItems.DeleteManyAsync(c => c.UserId == userId);

    public static CartResultDto ToResultDto(CartItem i) => new()
    {
        Id = i.Id,
        ProductId = i.ProductId,
        ProductName = i.ProductName,
        ProductImageUrl = i.ProductImageUrl,
        Quantity = i.Quantity,
        Price = i.Price,
        Size = i.Size
    };
}
```

- [ ] **Step 7: ProductHttpClient** (REST client)

`Clients/ProductHttpClient.cs`:
```csharp
using System.Net;
using System.Net.Http.Json;
using PizzaApp.Cart.Core.Interfaces;

namespace PizzaApp.Cart.Infrastructure.Clients;

public class ProductHttpClient : IProductClient
{
    private readonly HttpClient _http;

    public ProductHttpClient(HttpClient http) => _http = http;

    public async Task<ProductInfo?> GetProductAsync(string productId)
    {
        var response = await _http.GetAsync($"api/products/{productId}");
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<ProductResponse>();
        if (dto == null) return null;
        return new ProductInfo(dto.Id, dto.Name, dto.ImageUrl, dto.Price);
    }

    private class ProductResponse
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
        public decimal Price { get; set; }
    }
}
```

- [ ] **Step 8: Chạy test — pass** (`dotnet test tests/PizzaApp.Cart.Tests` → 1 pass).

---

### Task 2: Cart.API + Gateway route + compose

**Files:**
- Create Cart.API: `Controllers/CartController.cs`, `Program.cs`, `appsettings.json`, `Dockerfile`
- Modify: `src/ApiGateway/appsettings.json` (route cart)
- Modify: `docker-compose.yml` (service cart + ProductUrl)

**Interfaces:**
- Consumes: `ICartService`, `CartService`, `CartDbContext`, `IProductClient`/`ProductHttpClient`, MongoContext/settings, JWT.
- Produces: endpoints `/api/cart` (Authorize); gateway route `/api/cart/{**}`.

- [ ] **Step 1: Scaffold API**

```bash
dotnet new webapi -n PizzaApp.Cart.API -o src/Services/Cart/PizzaApp.Cart.API -f net8.0 --use-controllers
rm -f src/Services/Cart/PizzaApp.Cart.API/WeatherForecast.cs src/Services/Cart/PizzaApp.Cart.API/Controllers/WeatherForecastController.cs
dotnet sln add src/Services/Cart/PizzaApp.Cart.API/PizzaApp.Cart.API.csproj
dotnet add src/Services/Cart/PizzaApp.Cart.API reference src/Services/Cart/PizzaApp.Cart.Core/PizzaApp.Cart.Core.csproj src/Services/Cart/PizzaApp.Cart.Infrastructure/PizzaApp.Cart.Infrastructure.csproj src/BuildingBlocks/PizzaApp.BuildingBlocks.csproj
dotnet add src/Services/Cart/PizzaApp.Cart.API package Swashbuckle.AspNetCore -v 6.6.2
```

- [ ] **Step 2: CartController** (migrate; userId từ JWT)

`Controllers/CartController.cs`:
```csharp
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
```

- [ ] **Step 3: Program.cs** (DI + HttpClient tới Product)

```csharp
using PizzaApp.Cart.Core.Interfaces;
using PizzaApp.Cart.Infrastructure;
using PizzaApp.Cart.Infrastructure.Clients;
using PizzaApp.Cart.Infrastructure.Services;
using PizzaApp.BuildingBlocks.Auth;
using PizzaApp.BuildingBlocks.Mongo;

var builder = WebApplication.CreateBuilder(args);

var mongoSettings = new MongoSettings();
builder.Configuration.GetSection("MongoDB").Bind(mongoSettings);
var jwtSettings = new JwtSettings();
builder.Configuration.GetSection("JwtSettings").Bind(jwtSettings);

builder.Services.AddSingleton(mongoSettings);
builder.Services.AddSingleton(jwtSettings);
builder.Services.AddSingleton<MongoContext>();
builder.Services.AddSingleton<CartDbContext>();
builder.Services.AddScoped<ICartService, CartService>();

var productUrl = builder.Configuration["Services:ProductUrl"] ?? "http://localhost:5002/";
builder.Services.AddHttpClient<IProductClient, ProductHttpClient>(c =>
{
    c.BaseAddress = new Uri(productUrl);
    c.Timeout = TimeSpan.FromSeconds(5);
});

builder.Services.AddPizzaJwtAuthentication(jwtSettings);
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(o => o.AddPolicy("AllowAll", p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI();
app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
```

- [ ] **Step 4: appsettings.json**

```json
{
  "MongoDB": { "ConnectionString": "mongodb://localhost:27017", "DatabaseName": "PizzaApp_Cart" },
  "JwtSettings": { "SecretKey": "PizzaAppSuperSecretKey2024_DoNotShare!", "Issuer": "PizzaApp", "Audience": "PizzaAppUsers", "ExpiresInDays": 7 },
  "Services": { "ProductUrl": "http://localhost:5002/" },
  "Logging": { "LogLevel": { "Default": "Information", "Microsoft.AspNetCore": "Warning" } },
  "AllowedHosts": "*"
}
```

- [ ] **Step 5: Dockerfile**

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY src/BuildingBlocks/ src/BuildingBlocks/
COPY src/Services/Cart/ src/Services/Cart/
RUN dotnet restore src/Services/Cart/PizzaApp.Cart.API/PizzaApp.Cart.API.csproj
RUN dotnet publish src/Services/Cart/PizzaApp.Cart.API/PizzaApp.Cart.API.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "PizzaApp.Cart.API.dll"]
```

- [ ] **Step 6: Gateway route** — thêm vào `src/ApiGateway/appsettings.json`:

Route: `"cart-route": { "ClusterId": "cart-cluster", "Match": { "Path": "/api/cart/{**catch-all}" } }`
Cluster: `"cart-cluster": { "Destinations": { "cart-1": { "Address": "http://localhost:5004/" } } }`

- [ ] **Step 7: docker-compose** — thêm service:
```yaml
  cart:
    build:
      context: .
      dockerfile: src/Services/Cart/PizzaApp.Cart.API/Dockerfile
    environment:
      - MongoDB__ConnectionString=mongodb://mongo:27017
      - MongoDB__DatabaseName=PizzaApp_Cart
      - JwtSettings__SecretKey=${JWT_SECRET_KEY}
      - Services__ProductUrl=http://product:8080/
    depends_on:
      - mongo
      - product
```
và trong `gateway.environment` thêm:
```yaml
      - ReverseProxy__Clusters__cart-cluster__Destinations__cart-1__Address=http://cart:8080/
```
và thêm `cart` vào `gateway.depends_on`.

- [ ] **Step 8: Build + test toàn solution** = 0 error, tất cả pass.

- [ ] **Step 9: Boot check DI** cho Cart.API (`dotnet run --no-build`, xác nhận "Application started", tắt).

---

## Self-Review

**Spec coverage:** Cart thành service riêng ✓; DB per service (`PizzaApp_Cart`) ✓; không đọc DB Product — dùng REST + denormalize snapshot ✓; gateway route `/api/cart` ✓; JWT `[Authorize]` giữ nguyên ✓.

**Placeholder scan:** Không TODO/TBD; mọi step có code/lệnh.

**Type consistency:** `IProductClient.GetProductAsync` → `ProductInfo?` dùng ở `CartService.AddToCartAsync` và impl `ProductHttpClient` — khớp. `CartService.ToResultDto(CartItem)` khai báo Task 1 Step 6, test Task 1 Step 4 — khớp. Entity `CartItem` ≠ segment namespace `Cart` → không cần alias.

**Rủi ro:**
- Snapshot giá/tên trong giỏ sẽ cũ nếu sản phẩm đổi giá sau khi đã thêm vào giỏ (giống hành vi monolith — chấp nhận).
- `AddHttpClient<IProductClient, ProductHttpClient>` tự đăng ký scoped.
