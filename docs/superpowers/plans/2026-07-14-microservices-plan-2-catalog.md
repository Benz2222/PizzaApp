# Plan 2 — Catalog (Category + Product Service) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:executing-plans. Steps use checkbox (`- [ ]`).

**Goal:** Tách **Category Service** và **Product Service** thành 2 microservice độc lập, mỗi cái DB riêng, định tuyến qua Gateway. Product lưu `CategoryName` denormalized và gọi REST sang Category khi Create/Update.

**Architecture:** Giống Plan 1 (API/Core/Infrastructure + BuildingBlocks). Product→Category giao tiếp REST đồng bộ qua `ICategoryClient` (HttpClient). Không service nào đọc DB của service khác.

**Tech Stack:** .NET 8, MongoDB.Driver 2.28, YARP, xUnit, HttpClient.

## Global Constraints

- Kế thừa toàn bộ Global Constraints của Plan 1 (net8.0, secrets qua env, ID StringObjectIdGenerator, cùng JWT).
- Category Service: DB `PizzaApp_Category`, collection `Categories`, cổng container 8080 (host dev 5003).
- Product Service: DB `PizzaApp_Product`, collection `Products`, cổng container 8080 (host dev 5002).
- Product **không** truy cập DB Category. `CategoryName` lấy qua REST khi Create/Update, lưu denormalized trên Product.
- Endpoint giữ nguyên đường dẫn monolith: `/api/category`, `/api/products` (controller `Products` → route `/api/products`).
- Chỉ `Admin` được Create/Update/Delete/upload (giữ `[Authorize(Roles="Admin")]`).

---

## File Structure (Plan 2)

```
backend/src/Services/
  Category/
    PizzaApp.Category.Core/         Entities/Category.cs, DTOs/*, Interfaces/ICategoryService.cs
    PizzaApp.Category.Infrastructure/ CategoryDbContext.cs, Services/CategoryService.cs
    PizzaApp.Category.API/          Controllers/CategoryController.cs, Program.cs, appsettings.json, Dockerfile
  Product/
    PizzaApp.Product.Core/          Entities/Product.cs, DTOs/*, Interfaces/{IProductService,ICategoryClient}.cs
    PizzaApp.Product.Infrastructure/ ProductDbContext.cs, Services/ProductService.cs, Clients/CategoryHttpClient.cs
    PizzaApp.Product.API/           Controllers/ProductsController.cs, Program.cs, appsettings.json, Dockerfile
backend/tests/
  PizzaApp.Category.Tests/          CategoryMappingTests.cs
  PizzaApp.Product.Tests/           ProductServiceTests.cs
backend/src/ApiGateway/appsettings.json   (thêm route product + category)
backend/docker-compose.yml                (thêm service product + category)
```

---

### Task 1: Category Service (Core + Infrastructure + tests)

**Files:**
- Create Category.Core: `Entities/Category.cs`, `DTOs/CategoryDTO.cs`, `DTOs/CreateCategoryDTO.cs`, `DTOs/UpdateCategoryDTO.cs`, `Interfaces/ICategoryService.cs`
- Create Category.Infrastructure: `CategoryDbContext.cs`, `Services/CategoryService.cs`
- Test: `tests/PizzaApp.Category.Tests/CategoryMappingTests.cs`

**Interfaces:**
- Produces: `ICategoryService` (GetAll/GetById/Create/Update/Delete), `CategoryDbContext.Categories`, `CategoryService`.

- [ ] **Step 1: Scaffold projects**

```bash
cd backend
dotnet new classlib -n PizzaApp.Category.Core -o src/Services/Category/PizzaApp.Category.Core -f net8.0
dotnet new classlib -n PizzaApp.Category.Infrastructure -o src/Services/Category/PizzaApp.Category.Infrastructure -f net8.0
dotnet new xunit -n PizzaApp.Category.Tests -o tests/PizzaApp.Category.Tests -f net8.0
rm -f src/Services/Category/PizzaApp.Category.Core/Class1.cs src/Services/Category/PizzaApp.Category.Infrastructure/Class1.cs tests/PizzaApp.Category.Tests/UnitTest1.cs
dotnet sln add src/Services/Category/PizzaApp.Category.Core/PizzaApp.Category.Core.csproj src/Services/Category/PizzaApp.Category.Infrastructure/PizzaApp.Category.Infrastructure.csproj tests/PizzaApp.Category.Tests/PizzaApp.Category.Tests.csproj
dotnet add src/Services/Category/PizzaApp.Category.Infrastructure reference src/Services/Category/PizzaApp.Category.Core/PizzaApp.Category.Core.csproj src/BuildingBlocks/PizzaApp.BuildingBlocks.csproj
dotnet add src/Services/Category/PizzaApp.Category.Infrastructure package MongoDB.Driver -v 2.28.0
dotnet add tests/PizzaApp.Category.Tests reference src/Services/Category/PizzaApp.Category.Infrastructure/PizzaApp.Category.Infrastructure.csproj src/Services/Category/PizzaApp.Category.Core/PizzaApp.Category.Core.csproj
```

- [ ] **Step 2: Category.Core files**

`Entities/Category.cs`:
```csharp
namespace PizzaApp.Category.Core.Entities;

public class Category
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}
```

`DTOs/CategoryDTO.cs`:
```csharp
namespace PizzaApp.Category.Core.DTOs;

public class CategoryDTO
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}
```

`DTOs/CreateCategoryDTO.cs`:
```csharp
namespace PizzaApp.Category.Core.DTOs;

public class CreateCategoryDTO
{
    public string Name { get; set; } = string.Empty;
}
```

`DTOs/UpdateCategoryDTO.cs`:
```csharp
namespace PizzaApp.Category.Core.DTOs;

public class UpdateCategoryDTO
{
    public string Name { get; set; } = string.Empty;
}
```

`Interfaces/ICategoryService.cs`:
```csharp
using PizzaApp.Category.Core.DTOs;

namespace PizzaApp.Category.Core.Interfaces;

public interface ICategoryService
{
    Task<List<CategoryDTO>> GetAllAsync();
    Task<CategoryDTO?> GetByIdAsync(string id);
    Task<CategoryDTO> CreateAsync(CreateCategoryDTO dto);
    Task<bool> UpdateAsync(string id, UpdateCategoryDTO dto);
    Task<bool> DeleteAsync(string id);
}
```

- [ ] **Step 3: CategoryDbContext**

`CategoryDbContext.cs`:
```csharp
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.IdGenerators;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using PizzaApp.Category.Core.Entities;
using PizzaApp.BuildingBlocks.Mongo;

namespace PizzaApp.Category.Infrastructure;

public class CategoryDbContext
{
    public IMongoCollection<Category> Categories { get; }

    public CategoryDbContext(MongoContext ctx)
    {
        RegisterMappings();
        Categories = ctx.GetCollection<Category>("Categories");
    }

    public static void RegisterMappings()
    {
        if (!BsonClassMap.IsClassMapRegistered(typeof(Category)))
        {
            BsonClassMap.RegisterClassMap<Category>(cm =>
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

- [ ] **Step 4: Failing test cho mapping**

`tests/PizzaApp.Category.Tests/CategoryMappingTests.cs`:
```csharp
using PizzaApp.Category.Core.Entities;
using PizzaApp.Category.Infrastructure.Services;
using Xunit;

namespace PizzaApp.Category.Tests;

public class CategoryMappingTests
{
    [Fact]
    public void ToDto_MapsIdAndName()
    {
        var entity = new Category { Id = "c1", Name = "Pizza" };

        var dto = CategoryService.ToDto(entity);

        Assert.Equal("c1", dto.Id);
        Assert.Equal("Pizza", dto.Name);
    }
}
```

- [ ] **Step 5: Chạy test — fail (chưa có CategoryService)**

Run: `dotnet test tests/PizzaApp.Category.Tests` → FAIL biên dịch.

- [ ] **Step 6: CategoryService**

`Services/CategoryService.cs`:
```csharp
using MongoDB.Driver;
using PizzaApp.Category.Core.DTOs;
using PizzaApp.Category.Core.Entities;
using PizzaApp.Category.Core.Interfaces;

namespace PizzaApp.Category.Infrastructure.Services;

public class CategoryService : ICategoryService
{
    private readonly IMongoCollection<Category> _categories;

    public CategoryService(CategoryDbContext db) => _categories = db.Categories;

    public async Task<List<CategoryDTO>> GetAllAsync()
    {
        var list = await _categories.Find(_ => true).ToListAsync();
        return list.Select(ToDto).ToList();
    }

    public async Task<CategoryDTO?> GetByIdAsync(string id)
    {
        var c = await _categories.Find(c => c.Id == id).FirstOrDefaultAsync();
        return c == null ? null : ToDto(c);
    }

    public async Task<CategoryDTO> CreateAsync(CreateCategoryDTO dto)
    {
        var category = new Category { Name = dto.Name };
        await _categories.InsertOneAsync(category);
        return ToDto(category);
    }

    public async Task<bool> UpdateAsync(string id, UpdateCategoryDTO dto)
    {
        var result = await _categories.UpdateOneAsync(
            c => c.Id == id,
            Builders<Category>.Update.Set(c => c.Name, dto.Name));
        return result.MatchedCount > 0;
    }

    public async Task<bool> DeleteAsync(string id)
    {
        var result = await _categories.DeleteOneAsync(c => c.Id == id);
        return result.DeletedCount > 0;
    }

    public static CategoryDTO ToDto(Category c) => new() { Id = c.Id, Name = c.Name };
}
```

- [ ] **Step 7: Chạy test — pass**

Run: `dotnet test tests/PizzaApp.Category.Tests` → PASS (1).

---

### Task 2: Category.API + Gateway route + compose

**Files:**
- Create Category.API: `Controllers/CategoryController.cs`, `Program.cs`, `appsettings.json`, `Dockerfile`
- Modify: `src/ApiGateway/appsettings.json` (thêm route/cluster category)
- Modify: `docker-compose.yml` (thêm service category)

**Interfaces:**
- Consumes: `ICategoryService`, `CategoryService`, `CategoryDbContext`, `MongoContext/MongoSettings`, `AddPizzaJwtAuthentication`.
- Produces: endpoints `/api/category` qua cổng 8080; gateway route `/api/category/{**}`.

- [ ] **Step 1: Scaffold API**

```bash
dotnet new webapi -n PizzaApp.Category.API -o src/Services/Category/PizzaApp.Category.API -f net8.0 --use-controllers
rm -f src/Services/Category/PizzaApp.Category.API/WeatherForecast.cs src/Services/Category/PizzaApp.Category.API/Controllers/WeatherForecastController.cs
dotnet sln add src/Services/Category/PizzaApp.Category.API/PizzaApp.Category.API.csproj
dotnet add src/Services/Category/PizzaApp.Category.API reference src/Services/Category/PizzaApp.Category.Core/PizzaApp.Category.Core.csproj src/Services/Category/PizzaApp.Category.Infrastructure/PizzaApp.Category.Infrastructure.csproj src/BuildingBlocks/PizzaApp.BuildingBlocks.csproj
dotnet add src/Services/Category/PizzaApp.Category.API package Swashbuckle.AspNetCore -v 6.6.2
```

- [ ] **Step 2: CategoryController** (migrate, đổi namespace)

`Controllers/CategoryController.cs`:
```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PizzaApp.Category.Core.DTOs;
using PizzaApp.Category.Core.Interfaces;

namespace PizzaApp.Category.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class CategoryController : ControllerBase
{
    private readonly ICategoryService _categoryService;

    public CategoryController(ICategoryService categoryService) => _categoryService = categoryService;

    [HttpGet]
    public async Task<IActionResult> GetAll() => Ok(await _categoryService.GetAllAsync());

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        var category = await _categoryService.GetByIdAsync(id);
        if (category == null) return NotFound();
        return Ok(category);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create(CreateCategoryDTO dto) => Ok(await _categoryService.CreateAsync(dto));

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(string id, UpdateCategoryDTO dto)
    {
        var result = await _categoryService.UpdateAsync(id, dto);
        if (!result) return NotFound();
        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(string id)
    {
        var result = await _categoryService.DeleteAsync(id);
        if (!result) return NotFound();
        return NoContent();
    }
}
```

- [ ] **Step 3: Program.cs** (overwrite mẫu)

```csharp
using PizzaApp.Category.Core.Interfaces;
using PizzaApp.Category.Infrastructure;
using PizzaApp.Category.Infrastructure.Services;
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
builder.Services.AddSingleton<CategoryDbContext>();
builder.Services.AddScoped<ICategoryService, CategoryService>();

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
  "MongoDB": { "ConnectionString": "mongodb://localhost:27017", "DatabaseName": "PizzaApp_Category" },
  "JwtSettings": { "SecretKey": "PizzaAppSuperSecretKey2024_DoNotShare!", "Issuer": "PizzaApp", "Audience": "PizzaAppUsers", "ExpiresInDays": 7 },
  "Logging": { "LogLevel": { "Default": "Information", "Microsoft.AspNetCore": "Warning" } },
  "AllowedHosts": "*"
}
```

- [ ] **Step 5: Dockerfile** (giống Auth, đổi tên project)

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY src/BuildingBlocks/ src/BuildingBlocks/
COPY src/Services/Category/ src/Services/Category/
RUN dotnet restore src/Services/Category/PizzaApp.Category.API/PizzaApp.Category.API.csproj
RUN dotnet publish src/Services/Category/PizzaApp.Category.API/PizzaApp.Category.API.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "PizzaApp.Category.API.dll"]
```

- [ ] **Step 6: Gateway route** — thêm vào `src/ApiGateway/appsettings.json` mục `ReverseProxy.Routes` và `Clusters`:

Route mới:
```json
"category-route": { "ClusterId": "category-cluster", "Match": { "Path": "/api/category/{**catch-all}" } }
```
Cluster mới:
```json
"category-cluster": { "Destinations": { "category-1": { "Address": "http://localhost:5003/" } } }
```

- [ ] **Step 7: docker-compose** — thêm service:
```yaml
  category:
    build:
      context: .
      dockerfile: src/Services/Category/PizzaApp.Category.API/Dockerfile
    environment:
      - MongoDB__ConnectionString=mongodb://mongo:27017
      - MongoDB__DatabaseName=PizzaApp_Category
      - JwtSettings__SecretKey=${JWT_SECRET_KEY}
    depends_on:
      - mongo
```
và trong `gateway.environment` thêm:
```yaml
      - ReverseProxy__Clusters__category-cluster__Destinations__category-1__Address=http://category:8080/
```

- [ ] **Step 8: Build solution** → `dotnet build PizzaApp.Microservices.sln` = 0 error.

---

### Task 3: Product Service (Core + Infrastructure + REST client + tests)

**Files:**
- Create Product.Core: `Entities/Product.cs`, `DTOs/{ProductDto,CreateProductDTO,UpdateProductDTO}.cs`, `Interfaces/IProductService.cs`, `Interfaces/ICategoryClient.cs`
- Create Product.Infrastructure: `ProductDbContext.cs`, `Services/ProductService.cs`, `Clients/CategoryHttpClient.cs`
- Test: `tests/PizzaApp.Product.Tests/ProductServiceTests.cs`

**Interfaces:**
- Produces:
  - `Product` entity với thêm `CategoryName`.
  - `ICategoryClient { Task<string?> GetCategoryNameAsync(string categoryId); }` (trả null nếu category không tồn tại).
  - `IProductService` (GetAll/GetById/Create/Update/Delete) — chữ ký giống monolith.
  - `ProductService(ProductDbContext db, ICategoryClient categoryClient)`.
  - `ProductService.MapToDto(Product)` static, `ProductService.NormalizePaging(page,pageSize)` static.

- [ ] **Step 1: Scaffold projects**

```bash
dotnet new classlib -n PizzaApp.Product.Core -o src/Services/Product/PizzaApp.Product.Core -f net8.0
dotnet new classlib -n PizzaApp.Product.Infrastructure -o src/Services/Product/PizzaApp.Product.Infrastructure -f net8.0
dotnet new xunit -n PizzaApp.Product.Tests -o tests/PizzaApp.Product.Tests -f net8.0
rm -f src/Services/Product/PizzaApp.Product.Core/Class1.cs src/Services/Product/PizzaApp.Product.Infrastructure/Class1.cs tests/PizzaApp.Product.Tests/UnitTest1.cs
dotnet sln add src/Services/Product/PizzaApp.Product.Core/PizzaApp.Product.Core.csproj src/Services/Product/PizzaApp.Product.Infrastructure/PizzaApp.Product.Infrastructure.csproj tests/PizzaApp.Product.Tests/PizzaApp.Product.Tests.csproj
dotnet add src/Services/Product/PizzaApp.Product.Infrastructure reference src/Services/Product/PizzaApp.Product.Core/PizzaApp.Product.Core.csproj src/BuildingBlocks/PizzaApp.BuildingBlocks.csproj
dotnet add src/Services/Product/PizzaApp.Product.Infrastructure package MongoDB.Driver -v 2.28.0
dotnet add tests/PizzaApp.Product.Tests reference src/Services/Product/PizzaApp.Product.Infrastructure/PizzaApp.Product.Infrastructure.csproj src/Services/Product/PizzaApp.Product.Core/PizzaApp.Product.Core.csproj
```

- [ ] **Step 2: Product.Core**

`Entities/Product.cs` (thêm `CategoryName`):
```csharp
namespace PizzaApp.Product.Core.Entities;

public class Product
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public string CategoryId { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty; // denormalized từ Category service
    public bool IsAvailable { get; set; } = true;
}
```

`DTOs/ProductDto.cs`:
```csharp
namespace PizzaApp.Product.Core.DTOs;

public class ProductDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public string CategoryId { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public bool IsAvailable { get; set; }
}
```

`DTOs/CreateProductDTO.cs`:
```csharp
namespace PizzaApp.Product.Core.DTOs;

public class CreateProductDTO
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public string CategoryId { get; set; } = string.Empty;
}
```

`DTOs/UpdateProductDTO.cs`:
```csharp
namespace PizzaApp.Product.Core.DTOs;

public class UpdateProductDTO
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public string CategoryId { get; set; } = string.Empty;
    public bool IsAvailable { get; set; }
}
```

`Interfaces/IProductService.cs`:
```csharp
using PizzaApp.Product.Core.DTOs;

namespace PizzaApp.Product.Core.Interfaces;

public interface IProductService
{
    Task<List<ProductDto>> GetAllAsync(string? search, string? categoryId, int page, int pageSize);
    Task<ProductDto?> GetByIdAsync(string id);
    Task<ProductDto> CreateAsync(CreateProductDTO dto);
    Task<bool> UpdateAsync(string id, UpdateProductDTO dto);
    Task<bool> DeleteAsync(string id);
}
```

`Interfaces/ICategoryClient.cs`:
```csharp
namespace PizzaApp.Product.Core.Interfaces;

public interface ICategoryClient
{
    /// <summary>Trả tên category, hoặc null nếu không tồn tại.</summary>
    Task<string?> GetCategoryNameAsync(string categoryId);
}
```

- [ ] **Step 3: ProductDbContext**

`ProductDbContext.cs`:
```csharp
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.IdGenerators;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using PizzaApp.Product.Core.Entities;
using PizzaApp.BuildingBlocks.Mongo;

namespace PizzaApp.Product.Infrastructure;

public class ProductDbContext
{
    public IMongoCollection<Product> Products { get; }

    public ProductDbContext(MongoContext ctx)
    {
        RegisterMappings();
        Products = ctx.GetCollection<Product>("Products");
    }

    public static void RegisterMappings()
    {
        if (!BsonClassMap.IsClassMapRegistered(typeof(Product)))
        {
            BsonClassMap.RegisterClassMap<Product>(cm =>
            {
                cm.AutoMap();
                cm.SetIgnoreExtraElements(true);
                cm.MapIdProperty(p => p.Id)
                  .SetIdGenerator(StringObjectIdGenerator.Instance)
                  .SetSerializer(new StringSerializer(BsonType.String));
            });
        }
    }
}
```

- [ ] **Step 4: Failing test cho MapToDto + NormalizePaging**

`tests/PizzaApp.Product.Tests/ProductServiceTests.cs`:
```csharp
using PizzaApp.Product.Core.Entities;
using PizzaApp.Product.Infrastructure.Services;
using Xunit;

namespace PizzaApp.Product.Tests;

public class ProductServiceTests
{
    [Fact]
    public void MapToDto_CopiesAllFieldsIncludingCategoryName()
    {
        var p = new Product
        {
            Id = "p1", Name = "Margherita", Description = "cheese",
            Price = 9.5m, ImageUrl = "/uploads/x.jpg",
            CategoryId = "c1", CategoryName = "Pizza", IsAvailable = true
        };

        var dto = ProductService.MapToDto(p);

        Assert.Equal("p1", dto.Id);
        Assert.Equal("Margherita", dto.Name);
        Assert.Equal("Pizza", dto.CategoryName);
        Assert.Equal("c1", dto.CategoryId);
        Assert.True(dto.IsAvailable);
    }

    [Theory]
    [InlineData(0, 0, 1, 20)]
    [InlineData(-3, -1, 1, 20)]
    [InlineData(2, 10, 2, 10)]
    public void NormalizePaging_ClampsInvalidValues(int page, int size, int expPage, int expSize)
    {
        var (p, s) = ProductService.NormalizePaging(page, size);
        Assert.Equal(expPage, p);
        Assert.Equal(expSize, s);
    }
}
```

- [ ] **Step 5: Chạy test — fail**

Run: `dotnet test tests/PizzaApp.Product.Tests` → FAIL biên dịch.

- [ ] **Step 6: ProductService** (denormalize CategoryName; đọc không gọi chéo)

`Services/ProductService.cs`:
```csharp
using MongoDB.Bson;
using MongoDB.Driver;
using PizzaApp.Product.Core.DTOs;
using PizzaApp.Product.Core.Entities;
using PizzaApp.Product.Core.Interfaces;

namespace PizzaApp.Product.Infrastructure.Services;

public class ProductService : IProductService
{
    private readonly IMongoCollection<Product> _products;
    private readonly ICategoryClient _categoryClient;

    public ProductService(ProductDbContext db, ICategoryClient categoryClient)
    {
        _products = db.Products;
        _categoryClient = categoryClient;
    }

    public async Task<List<ProductDto>> GetAllAsync(string? search, string? categoryId, int page, int pageSize)
    {
        var b = Builders<Product>.Filter;
        var filter = b.Eq(p => p.IsAvailable, true);

        if (!string.IsNullOrWhiteSpace(categoryId))
            filter &= b.Eq(p => p.CategoryId, categoryId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var regex = new BsonRegularExpression(search, "i");
            filter &= b.Or(b.Regex(p => p.Name, regex), b.Regex(p => p.Description, regex));
        }

        var (pg, size) = NormalizePaging(page, pageSize);
        var products = await _products.Find(filter)
            .Skip((pg - 1) * size).Limit(size).ToListAsync();

        return products.Select(MapToDto).ToList();
    }

    public async Task<ProductDto?> GetByIdAsync(string id)
    {
        var p = await _products.Find(p => p.Id == id).FirstOrDefaultAsync();
        return p == null ? null : MapToDto(p);
    }

    public async Task<ProductDto> CreateAsync(CreateProductDTO dto)
    {
        var categoryName = await _categoryClient.GetCategoryNameAsync(dto.CategoryId);
        if (categoryName == null)
            throw new InvalidOperationException("Category not found");

        var product = new Product
        {
            Name = dto.Name,
            Description = dto.Description,
            Price = dto.Price,
            ImageUrl = dto.ImageUrl,
            CategoryId = dto.CategoryId,
            CategoryName = categoryName,
            IsAvailable = true
        };
        await _products.InsertOneAsync(product);
        return MapToDto(product);
    }

    public async Task<bool> UpdateAsync(string id, UpdateProductDTO dto)
    {
        var categoryName = await _categoryClient.GetCategoryNameAsync(dto.CategoryId);
        if (categoryName == null)
            throw new InvalidOperationException("Category not found");

        var update = Builders<Product>.Update
            .Set(p => p.Name, dto.Name)
            .Set(p => p.Description, dto.Description)
            .Set(p => p.Price, dto.Price)
            .Set(p => p.ImageUrl, dto.ImageUrl)
            .Set(p => p.CategoryId, dto.CategoryId)
            .Set(p => p.CategoryName, categoryName)
            .Set(p => p.IsAvailable, dto.IsAvailable);

        var result = await _products.UpdateOneAsync(p => p.Id == id, update);
        return result.MatchedCount > 0;
    }

    public async Task<bool> DeleteAsync(string id)
    {
        var result = await _products.DeleteOneAsync(p => p.Id == id);
        return result.DeletedCount > 0;
    }

    public static (int page, int pageSize) NormalizePaging(int page, int pageSize)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        return (page, pageSize);
    }

    public static ProductDto MapToDto(Product p) => new()
    {
        Id = p.Id,
        Name = p.Name,
        Description = p.Description,
        Price = p.Price,
        ImageUrl = p.ImageUrl,
        CategoryId = p.CategoryId,
        CategoryName = p.CategoryName,
        IsAvailable = p.IsAvailable
    };
}
```

- [ ] **Step 7: CategoryHttpClient** (REST client)

`Clients/CategoryHttpClient.cs`:
```csharp
using System.Net;
using System.Net.Http.Json;
using PizzaApp.Product.Core.Interfaces;

namespace PizzaApp.Product.Infrastructure.Clients;

public class CategoryHttpClient : ICategoryClient
{
    private readonly HttpClient _http;

    public CategoryHttpClient(HttpClient http) => _http = http;

    public async Task<string?> GetCategoryNameAsync(string categoryId)
    {
        var response = await _http.GetAsync($"api/category/{categoryId}");
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        var dto = await response.Content.ReadFromJsonAsync<CategoryNameDto>();
        return dto?.Name;
    }

    private class CategoryNameDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }
}
```

- [ ] **Step 8: Chạy test — pass**

Run: `dotnet test tests/PizzaApp.Product.Tests` → PASS (MapToDto + 3 InlineData paging).

---

### Task 4: Product.API + Gateway route + compose

**Files:**
- Create Product.API: `Controllers/ProductsController.cs`, `Program.cs`, `appsettings.json`, `Dockerfile`
- Modify: `src/ApiGateway/appsettings.json` (route product)
- Modify: `docker-compose.yml` (service product + biến CategoryService URL)

**Interfaces:**
- Consumes: `IProductService`, `ProductService`, `ProductDbContext`, `ICategoryClient`/`CategoryHttpClient`, MongoContext/settings, JWT.
- Produces: endpoints `/api/products` (+ `/api/products/upload`), phục vụ static `/uploads/*`.

- [ ] **Step 1: Scaffold API**

```bash
dotnet new webapi -n PizzaApp.Product.API -o src/Services/Product/PizzaApp.Product.API -f net8.0 --use-controllers
rm -f src/Services/Product/PizzaApp.Product.API/WeatherForecast.cs src/Services/Product/PizzaApp.Product.API/Controllers/WeatherForecastController.cs
dotnet sln add src/Services/Product/PizzaApp.Product.API/PizzaApp.Product.API.csproj
dotnet add src/Services/Product/PizzaApp.Product.API reference src/Services/Product/PizzaApp.Product.Core/PizzaApp.Product.Core.csproj src/Services/Product/PizzaApp.Product.Infrastructure/PizzaApp.Product.Infrastructure.csproj src/BuildingBlocks/PizzaApp.BuildingBlocks.csproj
dotnet add src/Services/Product/PizzaApp.Product.API package Swashbuckle.AspNetCore -v 6.6.2
```

- [ ] **Step 2: ProductsController** (migrate; route giữ `/api/products`)

`Controllers/ProductsController.cs`:
```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PizzaApp.Product.Core.DTOs;
using PizzaApp.Product.Core.Interfaces;

namespace PizzaApp.Product.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ProductsController : ControllerBase
{
    private readonly IProductService _productService;
    private readonly IWebHostEnvironment _env;

    public ProductsController(IProductService productService, IWebHostEnvironment env)
    {
        _productService = productService;
        _env = env;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? search = null,
        [FromQuery] string? categoryId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
        => Ok(await _productService.GetAllAsync(search, categoryId, page, pageSize));

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        var product = await _productService.GetByIdAsync(id);
        if (product == null) return NotFound();
        return Ok(product);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create(CreateProductDTO dto)
    {
        try
        {
            var product = await _productService.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = product.Id }, product);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(string id, UpdateProductDTO dto)
    {
        try
        {
            var result = await _productService.UpdateAsync(id, dto);
            if (!result) return NotFound();
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(string id)
    {
        var result = await _productService.DeleteAsync(id);
        if (!result) return NotFound();
        return NoContent();
    }

    [HttpPost("upload")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UploadImage(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "Chưa chọn file ảnh." });

        var allowed = new[] { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowed.Contains(ext))
            return BadRequest(new { message = "Chỉ chấp nhận ảnh (jpg, png, webp, gif)." });

        var webRoot = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
        var uploadDir = Path.Combine(webRoot, "uploads");
        Directory.CreateDirectory(uploadDir);

        var fileName = $"{Guid.NewGuid():N}{ext}";
        var filePath = Path.Combine(uploadDir, fileName);
        using (var stream = System.IO.File.Create(filePath))
            await file.CopyToAsync(stream);

        return Ok(new { imageUrl = $"/uploads/{fileName}" });
    }
}
```

- [ ] **Step 3: Program.cs** (DI + HttpClient cho CategoryClient + static files)

```csharp
using PizzaApp.Product.Core.Interfaces;
using PizzaApp.Product.Infrastructure;
using PizzaApp.Product.Infrastructure.Clients;
using PizzaApp.Product.Infrastructure.Services;
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
builder.Services.AddSingleton<ProductDbContext>();
builder.Services.AddScoped<IProductService, ProductService>();

// REST client tới Category service (địa chỉ đọc từ config "Services:CategoryUrl")
var categoryUrl = builder.Configuration["Services:CategoryUrl"] ?? "http://localhost:5003/";
builder.Services.AddHttpClient<ICategoryClient, CategoryHttpClient>(c =>
{
    c.BaseAddress = new Uri(categoryUrl);
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
app.UseStaticFiles(); // phục vụ /uploads/*
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
```

- [ ] **Step 4: appsettings.json**

```json
{
  "MongoDB": { "ConnectionString": "mongodb://localhost:27017", "DatabaseName": "PizzaApp_Product" },
  "JwtSettings": { "SecretKey": "PizzaAppSuperSecretKey2024_DoNotShare!", "Issuer": "PizzaApp", "Audience": "PizzaAppUsers", "ExpiresInDays": 7 },
  "Services": { "CategoryUrl": "http://localhost:5003/" },
  "Logging": { "LogLevel": { "Default": "Information", "Microsoft.AspNetCore": "Warning" } },
  "AllowedHosts": "*"
}
```

- [ ] **Step 5: Dockerfile** (build cần cả BuildingBlocks + Product)

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY src/BuildingBlocks/ src/BuildingBlocks/
COPY src/Services/Product/ src/Services/Product/
RUN dotnet restore src/Services/Product/PizzaApp.Product.API/PizzaApp.Product.API.csproj
RUN dotnet publish src/Services/Product/PizzaApp.Product.API/PizzaApp.Product.API.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "PizzaApp.Product.API.dll"]
```

- [ ] **Step 6: Gateway route** — thêm vào `src/ApiGateway/appsettings.json`:

Route: `"product-route": { "ClusterId": "product-cluster", "Match": { "Path": "/api/products/{**catch-all}" } }`
Cluster: `"product-cluster": { "Destinations": { "product-1": { "Address": "http://localhost:5002/" } } }`

- [ ] **Step 7: docker-compose** — thêm service product (kèm URL Category nội bộ):
```yaml
  product:
    build:
      context: .
      dockerfile: src/Services/Product/PizzaApp.Product.API/Dockerfile
    environment:
      - MongoDB__ConnectionString=mongodb://mongo:27017
      - MongoDB__DatabaseName=PizzaApp_Product
      - JwtSettings__SecretKey=${JWT_SECRET_KEY}
      - Services__CategoryUrl=http://category:8080/
    depends_on:
      - mongo
      - category
```
và trong `gateway.environment` thêm:
```yaml
      - ReverseProxy__Clusters__product-cluster__Destinations__product-1__Address=http://product:8080/
```

- [ ] **Step 8: Build + test toàn solution**

Run: `dotnet build PizzaApp.Microservices.sln` = 0 error; `dotnet test PizzaApp.Microservices.sln` = tất cả pass.

- [ ] **Step 9: Boot check DI** cho Category.API và Product.API (không cần Docker) — mỗi app chạy `dotnet run --no-build`, xác nhận log "Application started", rồi tắt. (Product cần Category chạy để gọi REST — chỉ verify boot, chưa verify REST khi không có Docker/Mongo.)

---

## Self-Review

**Spec coverage:** Product/Category thành service riêng ✓; DB per service (`PizzaApp_Product`, `PizzaApp_Category`) ✓; không đọc DB chéo — dùng REST + denormalize `CategoryName` ✓ (đúng "REST đồng bộ khi cần" + "denormalize thay vì join"); gateway route ✓; upload ảnh giữ ở Product ✓.

**Placeholder scan:** Không có TODO/TBD; mọi step có code hoặc lệnh cụ thể.

**Type consistency:** `ICategoryClient.GetCategoryNameAsync` dùng ở `ProductService.Create/Update` và impl `CategoryHttpClient` — khớp. `ProductService.MapToDto(Product)` + `NormalizePaging(int,int)` khai báo Task 3 Step 6, test Task 3 Step 4 — khớp. `CategoryService.ToDto(Category)` test Task 1 — khớp.

**Rủi ro:**
- Denormalized `CategoryName` sẽ cũ nếu category bị đổi tên sau đó (chỉ cập nhật khi product được Update). Chấp nhận cho đồ án (eventual consistency); có thể thêm event `CategoryRenamed` ở plan sau nếu cần.
- `AddHttpClient<ICategoryClient, CategoryHttpClient>` tự đăng ký scoped — không cần `AddScoped` thủ công cho `ICategoryClient`.
