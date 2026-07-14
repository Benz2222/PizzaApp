# Plan 1 — Nền tảng + Auth Service (Implementation Plan)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Dựng khung monorepo microservice + hạ tầng Docker (MongoDB, RabbitMQ), tách **Auth Service** độc lập, và một **API Gateway (YARP)** validate JWT — sao cho đăng ký/đăng nhập chạy end-to-end qua Gateway.

**Architecture:** Mỗi service là một ASP.NET Core Web API (net8.0) với cấu trúc `*.API / *.Core / *.Infrastructure`, MongoDB database riêng. Một project chung `BuildingBlocks` chứa JWT helper + Mongo base. Gateway dùng YARP làm reverse proxy + validate JWT bằng cùng secret. Toàn bộ chạy qua `docker compose`.

**Tech Stack:** .NET 8, ASP.NET Core Web API, MongoDB.Driver 2.28, BCrypt.Net-Next 4.2, Yarp.ReverseProxy 2.x, xUnit, Docker Compose, RabbitMQ (dựng sẵn cho các plan sau).

## Global Constraints

- TargetFramework: `net8.0`, `Nullable=enable`, `ImplicitUsings=enable` cho mọi project (khớp monolith hiện tại).
- MongoDB.Driver version `2.28.0`; BCrypt.Net-Next `4.2.0` (khớp monolith).
- JWT: cùng `SecretKey`, `Issuer=PizzaApp`, `Audience=PizzaAppUsers`, `NameClaimType=ClaimTypes.NameIdentifier`, `RoleClaimType=ClaimTypes.Role`, `ClockSkew=Zero` — giống hệt monolith để token cũ vẫn hợp lệ.
- **Không hardcode secrets**: mọi secret (JWT key, Mongo connection, PayOS) đọc từ biến môi trường / user-secrets. `appsettings.json` chỉ chứa giá trị mặc định dev không nhạy cảm.
- Auth Service dùng database `PizzaApp_Auth`, collection `Users`. ID kiểu string dùng `StringObjectIdGenerator` (giống monolith).
- Mọi code thư mục mới nằm dưới `backend/`. Không sửa `PizzaApp/` (monolith cũ) trong plan này.
- Root namespace mỗi project theo tên project (vd `PizzaApp.Auth.API`, `PizzaApp.BuildingBlocks`).

---

## File Structure (Plan 1)

```
backend/
  PizzaApp.Microservices.sln
  src/
    BuildingBlocks/
      PizzaApp.BuildingBlocks.csproj
      Auth/JwtSettings.cs
      Auth/JwtTokenGenerator.cs
      Auth/JwtAuthenticationExtensions.cs
      Mongo/MongoSettings.cs
      Mongo/MongoContext.cs
    Services/Auth/
      PizzaApp.Auth.Core/
        PizzaApp.Auth.Core.csproj
        Entities/User.cs
        DTOs/{RegisterDto,LoginDto,AuthResponseDto,UserProfileDto,ForgotPasswordDto,ResetPasswordDto}.cs
        Interfaces/IAuthService.cs
      PizzaApp.Auth.Infrastructure/
        PizzaApp.Auth.Infrastructure.csproj
        AuthDbContext.cs
        Services/AuthService.cs
      PizzaApp.Auth.API/
        PizzaApp.Auth.API.csproj
        Controllers/AuthController.cs
        Program.cs
        appsettings.json
        Dockerfile
    ApiGateway/
      PizzaApp.ApiGateway.csproj
      Program.cs
      appsettings.json
      Dockerfile
  tests/
    PizzaApp.BuildingBlocks.Tests/
      PizzaApp.BuildingBlocks.Tests.csproj
      JwtTokenGeneratorTests.cs
    PizzaApp.Auth.Tests/
      PizzaApp.Auth.Tests.csproj
      AuthServiceTests.cs
  docker-compose.yml
  .env.example
  smoke-test-auth.sh
```

---

### Task 1: Solution + BuildingBlocks JWT (TDD)

**Files:**
- Create: `backend/PizzaApp.Microservices.sln`
- Create: `backend/src/BuildingBlocks/PizzaApp.BuildingBlocks.csproj`
- Create: `backend/src/BuildingBlocks/Auth/JwtSettings.cs`
- Create: `backend/src/BuildingBlocks/Auth/JwtTokenGenerator.cs`
- Test: `backend/tests/PizzaApp.BuildingBlocks.Tests/JwtTokenGeneratorTests.cs`
- Test: `backend/tests/PizzaApp.BuildingBlocks.Tests/PizzaApp.BuildingBlocks.Tests.csproj`

**Interfaces:**
- Produces:
  - `class JwtSettings { string SecretKey; string Issuer; string Audience; int ExpiresInDays; }`
  - `class JwtTokenGenerator(JwtSettings settings)` với `string Generate(string userId, string email, string fullName, string role)`

- [ ] **Step 1: Tạo solution + BuildingBlocks project + test project**

```bash
cd backend
dotnet new sln -n PizzaApp.Microservices
dotnet new classlib -n PizzaApp.BuildingBlocks -o src/BuildingBlocks -f net8.0
dotnet new xunit -n PizzaApp.BuildingBlocks.Tests -o tests/PizzaApp.BuildingBlocks.Tests -f net8.0
dotnet sln add src/BuildingBlocks/PizzaApp.BuildingBlocks.csproj
dotnet sln add tests/PizzaApp.BuildingBlocks.Tests/PizzaApp.BuildingBlocks.Tests.csproj
dotnet add tests/PizzaApp.BuildingBlocks.Tests reference src/BuildingBlocks/PizzaApp.BuildingBlocks.csproj
dotnet add src/BuildingBlocks package Microsoft.AspNetCore.Authentication.JwtBearer -v 8.0.27
dotnet add src/BuildingBlocks package System.IdentityModel.Tokens.Jwt -v 8.0.2
rm -f src/BuildingBlocks/Class1.cs tests/PizzaApp.BuildingBlocks.Tests/UnitTest1.cs
```

Nội dung `src/BuildingBlocks/PizzaApp.BuildingBlocks.csproj` sau khi thêm package phải là SDK `Microsoft.NET.Sdk` với `net8.0`, `Nullable=enable`, `ImplicitUsings=enable`. Nếu chưa có 2 dòng đó, thêm vào `<PropertyGroup>`.

- [ ] **Step 2: Viết test thất bại cho JwtTokenGenerator**

Create `backend/tests/PizzaApp.BuildingBlocks.Tests/JwtTokenGeneratorTests.cs`:

```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using PizzaApp.BuildingBlocks.Auth;
using Xunit;

namespace PizzaApp.BuildingBlocks.Tests;

public class JwtTokenGeneratorTests
{
    private static JwtSettings Settings() => new()
    {
        SecretKey = "PizzaAppSuperSecretKey2024_DoNotShare!",
        Issuer = "PizzaApp",
        Audience = "PizzaAppUsers",
        ExpiresInDays = 7
    };

    [Fact]
    public void Generate_EmbedsUserIdEmailAndRoleClaims()
    {
        var gen = new JwtTokenGenerator(Settings());

        var token = gen.Generate("user-123", "a@b.com", "Nguyen Van A", "Customer");

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.Equal("PizzaApp", jwt.Issuer);
        Assert.Contains(jwt.Audiences, a => a == "PizzaAppUsers");
        Assert.Equal("user-123", jwt.Claims.First(c => c.Type == ClaimTypes.NameIdentifier).Value);
        Assert.Equal("a@b.com", jwt.Claims.First(c => c.Type == ClaimTypes.Email).Value);
        Assert.Equal("Customer", jwt.Claims.First(c => c.Type == ClaimTypes.Role).Value);
    }

    [Fact]
    public void Generate_SetsExpiryFromSettings()
    {
        var gen = new JwtTokenGenerator(Settings());

        var token = gen.Generate("u", "e", "n", "Customer");

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.True(jwt.ValidTo > DateTime.UtcNow.AddDays(6));
        Assert.True(jwt.ValidTo < DateTime.UtcNow.AddDays(8));
    }
}
```

- [ ] **Step 3: Chạy test — phải fail vì chưa có class**

Run: `dotnet test backend/tests/PizzaApp.BuildingBlocks.Tests`
Expected: FAIL biên dịch — `JwtSettings` / `JwtTokenGenerator` không tồn tại.

- [ ] **Step 4: Viết JwtSettings**

Create `backend/src/BuildingBlocks/Auth/JwtSettings.cs`:

```csharp
namespace PizzaApp.BuildingBlocks.Auth;

public class JwtSettings
{
    public string SecretKey { get; set; } = string.Empty;
    public string Issuer { get; set; } = "PizzaApp";
    public string Audience { get; set; } = "PizzaAppUsers";
    public int ExpiresInDays { get; set; } = 7;
}
```

- [ ] **Step 5: Viết JwtTokenGenerator**

Create `backend/src/BuildingBlocks/Auth/JwtTokenGenerator.cs`:

```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace PizzaApp.BuildingBlocks.Auth;

public class JwtTokenGenerator
{
    private readonly JwtSettings _settings;

    public JwtTokenGenerator(JwtSettings settings) => _settings = settings;

    public string Generate(string userId, string email, string fullName, string role)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.SecretKey));
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Email, email),
            new Claim(ClaimTypes.Name, fullName),
            new Claim(ClaimTypes.Role, role)
        };

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddDays(_settings.ExpiresInDays),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
```

- [ ] **Step 6: Chạy test — phải pass**

Run: `dotnet test backend/tests/PizzaApp.BuildingBlocks.Tests`
Expected: PASS (2 test).

- [ ] **Step 7: Commit**

```bash
git add backend/PizzaApp.Microservices.sln backend/src/BuildingBlocks backend/tests/PizzaApp.BuildingBlocks.Tests
git commit -m "feat(backend): scaffold microservices solution + JWT token generator in BuildingBlocks"
```

---

### Task 2: BuildingBlocks — JWT validation extension + Mongo base

**Files:**
- Create: `backend/src/BuildingBlocks/Auth/JwtAuthenticationExtensions.cs`
- Create: `backend/src/BuildingBlocks/Mongo/MongoSettings.cs`
- Create: `backend/src/BuildingBlocks/Mongo/MongoContext.cs`

**Interfaces:**
- Consumes: `JwtSettings` (Task 1).
- Produces:
  - `static IServiceCollection AddPizzaJwtAuthentication(this IServiceCollection services, JwtSettings settings)` — cấu hình JwtBearer giống monolith.
  - `class MongoSettings { string ConnectionString; string DatabaseName; }`
  - `class MongoContext(MongoSettings settings)` với `IMongoCollection<T> GetCollection<T>(string name)`.

- [ ] **Step 1: Thêm MongoDB.Driver vào BuildingBlocks**

```bash
dotnet add backend/src/BuildingBlocks package MongoDB.Driver -v 2.28.0
```

- [ ] **Step 2: Viết JwtAuthenticationExtensions**

Create `backend/src/BuildingBlocks/Auth/JwtAuthenticationExtensions.cs`:

```csharp
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace PizzaApp.BuildingBlocks.Auth;

public static class JwtAuthenticationExtensions
{
    public static IServiceCollection AddPizzaJwtAuthentication(
        this IServiceCollection services, JwtSettings settings)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(settings.SecretKey)),
                    ValidateIssuer = true,
                    ValidIssuer = settings.Issuer,
                    ValidateAudience = true,
                    ValidAudience = settings.Audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero,
                    NameClaimType = ClaimTypes.NameIdentifier,
                    RoleClaimType = ClaimTypes.Role
                };
            });
        return services;
    }
}
```

- [ ] **Step 3: Thêm reference JwtBearer package (nếu thiếu)**

Package `Microsoft.AspNetCore.Authentication.JwtBearer` đã thêm ở Task 1. Xác nhận có trong csproj; nếu không:
```bash
dotnet add backend/src/BuildingBlocks package Microsoft.AspNetCore.Authentication.JwtBearer -v 8.0.27
```

- [ ] **Step 4: Viết MongoSettings + MongoContext**

Create `backend/src/BuildingBlocks/Mongo/MongoSettings.cs`:

```csharp
namespace PizzaApp.BuildingBlocks.Mongo;

public class MongoSettings
{
    public string ConnectionString { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = string.Empty;
}
```

Create `backend/src/BuildingBlocks/Mongo/MongoContext.cs`:

```csharp
using MongoDB.Driver;

namespace PizzaApp.BuildingBlocks.Mongo;

public class MongoContext
{
    private readonly IMongoDatabase _database;

    public MongoContext(MongoSettings settings)
    {
        var client = new MongoClient(settings.ConnectionString);
        _database = client.GetDatabase(settings.DatabaseName);
    }

    public IMongoCollection<T> GetCollection<T>(string name) =>
        _database.GetCollection<T>(name);
}
```

- [ ] **Step 5: Build BuildingBlocks — phải xanh**

Run: `dotnet build backend/src/BuildingBlocks`
Expected: Build succeeded, 0 error.

- [ ] **Step 6: Commit**

```bash
git add backend/src/BuildingBlocks
git commit -m "feat(backend): add JWT validation extension and Mongo context to BuildingBlocks"
```

---

### Task 3: Auth.Core (entities, DTOs, interface)

**Files:**
- Create: `backend/src/Services/Auth/PizzaApp.Auth.Core/PizzaApp.Auth.Core.csproj`
- Create: `backend/src/Services/Auth/PizzaApp.Auth.Core/Entities/User.cs`
- Create: `backend/src/Services/Auth/PizzaApp.Auth.Core/DTOs/RegisterDto.cs`, `LoginDto.cs`, `AuthResponseDto.cs`, `UserProfileDto.cs`, `ForgotPasswordDto.cs`, `ResetPasswordDto.cs`
- Create: `backend/src/Services/Auth/PizzaApp.Auth.Core/Interfaces/IAuthService.cs`

**Interfaces:**
- Produces: `namespace PizzaApp.Auth.Core.*` — `User` entity, 6 DTO, `IAuthService` (5 method giống monolith).

- [ ] **Step 1: Tạo project và add vào solution**

```bash
dotnet new classlib -n PizzaApp.Auth.Core -o backend/src/Services/Auth/PizzaApp.Auth.Core -f net8.0
rm -f backend/src/Services/Auth/PizzaApp.Auth.Core/Class1.cs
dotnet sln backend/PizzaApp.Microservices.sln add backend/src/Services/Auth/PizzaApp.Auth.Core/PizzaApp.Auth.Core.csproj
```

Đảm bảo csproj có `<Nullable>enable</Nullable>` và `<ImplicitUsings>enable</ImplicitUsings>` trong `<PropertyGroup>`.

- [ ] **Step 2: Viết entity User**

Create `backend/src/Services/Auth/PizzaApp.Auth.Core/Entities/User.cs`:

```csharp
namespace PizzaApp.Auth.Core.Entities;

public class User
{
    public string Id { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Role { get; set; } = "Customer"; // Customer, Admin, Shipper
    public string? ResetPasswordToken { get; set; }
    public DateTime? ResetPasswordTokenExpiry { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
```

- [ ] **Step 3: Viết 6 DTO**

Create `backend/src/Services/Auth/PizzaApp.Auth.Core/DTOs/RegisterDto.cs`:

```csharp
using System.ComponentModel.DataAnnotations;

namespace PizzaApp.Auth.Core.DTOs;

public class RegisterDto
{
    [Required(ErrorMessage = "Họ tên không được để trống")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email không được để trống")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Mật khẩu không được để trống")]
    [MinLength(6, ErrorMessage = "Mật khẩu phải có ít nhất 6 ký tự")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Số điện thoại không được để trống")]
    [Phone(ErrorMessage = "Số điện thoại không hợp lệ")]
    public string PhoneNumber { get; set; } = string.Empty;
}
```

Create `backend/src/Services/Auth/PizzaApp.Auth.Core/DTOs/LoginDto.cs`:

```csharp
using System.ComponentModel.DataAnnotations;

namespace PizzaApp.Auth.Core.DTOs;

public class LoginDto
{
    [Required(ErrorMessage = "Email không được để trống")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Mật khẩu không được để trống")]
    [MinLength(6, ErrorMessage = "Mật khẩu phải có ít nhất 6 ký tự")]
    public string Password { get; set; } = string.Empty;
}
```

Create `backend/src/Services/Auth/PizzaApp.Auth.Core/DTOs/AuthResponseDto.cs`:

```csharp
namespace PizzaApp.Auth.Core.DTOs;

public class AuthResponseDto
{
    public string Token { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
}
```

Create `backend/src/Services/Auth/PizzaApp.Auth.Core/DTOs/UserProfileDto.cs`:

```csharp
namespace PizzaApp.Auth.Core.DTOs;

public class UserProfileDto
{
    public string Id { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}
```

Create `backend/src/Services/Auth/PizzaApp.Auth.Core/DTOs/ForgotPasswordDto.cs`:

```csharp
using System.ComponentModel.DataAnnotations;

namespace PizzaApp.Auth.Core.DTOs;

public class ForgotPasswordDto
{
    [Required(ErrorMessage = "Email không được để trống")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ")]
    public string Email { get; set; } = string.Empty;
}
```

Create `backend/src/Services/Auth/PizzaApp.Auth.Core/DTOs/ResetPasswordDto.cs`:

```csharp
using System.ComponentModel.DataAnnotations;

namespace PizzaApp.Auth.Core.DTOs;

public class ResetPasswordDto
{
    [Required(ErrorMessage = "Email không được để trống")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Token không được để trống")]
    public string Token { get; set; } = string.Empty;

    [Required(ErrorMessage = "Mật khẩu mới không được để trống")]
    [MinLength(6, ErrorMessage = "Mật khẩu mới phải có ít nhất 6 ký tự")]
    public string NewPassword { get; set; } = string.Empty;
}
```

- [ ] **Step 4: Viết IAuthService**

Create `backend/src/Services/Auth/PizzaApp.Auth.Core/Interfaces/IAuthService.cs`:

```csharp
using PizzaApp.Auth.Core.DTOs;

namespace PizzaApp.Auth.Core.Interfaces;

public interface IAuthService
{
    Task<AuthResponseDto?> RegisterAsync(RegisterDto dto);
    Task<AuthResponseDto?> LoginAsync(LoginDto dto);
    Task<string> ForgotPasswordAsync(string email);
    Task ResetPasswordAsync(ResetPasswordDto dto);
    Task<UserProfileDto?> GetProfileAsync(string userId);
}
```

- [ ] **Step 5: Build — phải xanh**

Run: `dotnet build backend/src/Services/Auth/PizzaApp.Auth.Core`
Expected: Build succeeded.

- [ ] **Step 6: Commit**

```bash
git add backend/src/Services/Auth/PizzaApp.Auth.Core backend/PizzaApp.Microservices.sln
git commit -m "feat(auth): add Auth.Core entities, DTOs and IAuthService interface"
```

---

### Task 4: Auth.Infrastructure — AuthDbContext + AuthService (TDD cho token expiry logic)

**Files:**
- Create: `backend/src/Services/Auth/PizzaApp.Auth.Infrastructure/PizzaApp.Auth.Infrastructure.csproj`
- Create: `backend/src/Services/Auth/PizzaApp.Auth.Infrastructure/AuthDbContext.cs`
- Create: `backend/src/Services/Auth/PizzaApp.Auth.Infrastructure/Services/AuthService.cs`
- Create: `backend/tests/PizzaApp.Auth.Tests/PizzaApp.Auth.Tests.csproj`
- Test: `backend/tests/PizzaApp.Auth.Tests/AuthServiceTests.cs`

**Interfaces:**
- Consumes: `MongoContext`, `MongoSettings` (Task 2); `JwtTokenGenerator`, `JwtSettings` (Task 1); `IAuthService`, DTOs, `User` (Task 3).
- Produces:
  - `class AuthDbContext(MongoContext ctx)` với property `IMongoCollection<User> Users` và static `RegisterMappings()` (đăng ký BsonClassMap cho `User` với `StringObjectIdGenerator`).
  - `class AuthService(AuthDbContext db, JwtTokenGenerator tokens) : IAuthService`.

- [ ] **Step 1: Tạo project Infrastructure + test project**

```bash
dotnet new classlib -n PizzaApp.Auth.Infrastructure -o backend/src/Services/Auth/PizzaApp.Auth.Infrastructure -f net8.0
rm -f backend/src/Services/Auth/PizzaApp.Auth.Infrastructure/Class1.cs
dotnet new xunit -n PizzaApp.Auth.Tests -o backend/tests/PizzaApp.Auth.Tests -f net8.0
rm -f backend/tests/PizzaApp.Auth.Tests/UnitTest1.cs

dotnet sln backend/PizzaApp.Microservices.sln add backend/src/Services/Auth/PizzaApp.Auth.Infrastructure/PizzaApp.Auth.Infrastructure.csproj
dotnet sln backend/PizzaApp.Microservices.sln add backend/tests/PizzaApp.Auth.Tests/PizzaApp.Auth.Tests.csproj

dotnet add backend/src/Services/Auth/PizzaApp.Auth.Infrastructure reference backend/src/Services/Auth/PizzaApp.Auth.Core/PizzaApp.Auth.Core.csproj
dotnet add backend/src/Services/Auth/PizzaApp.Auth.Infrastructure reference backend/src/BuildingBlocks/PizzaApp.BuildingBlocks.csproj
dotnet add backend/src/Services/Auth/PizzaApp.Auth.Infrastructure package MongoDB.Driver -v 2.28.0
dotnet add backend/src/Services/Auth/PizzaApp.Auth.Infrastructure package BCrypt.Net-Next -v 4.2.0

dotnet add backend/tests/PizzaApp.Auth.Tests reference backend/src/Services/Auth/PizzaApp.Auth.Infrastructure/PizzaApp.Auth.Infrastructure.csproj
dotnet add backend/tests/PizzaApp.Auth.Tests reference backend/src/Services/Auth/PizzaApp.Auth.Core/PizzaApp.Auth.Core.csproj
```

- [ ] **Step 2: Viết AuthDbContext**

Create `backend/src/Services/Auth/PizzaApp.Auth.Infrastructure/AuthDbContext.cs`:

```csharp
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.IdGenerators;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using PizzaApp.Auth.Core.Entities;
using PizzaApp.BuildingBlocks.Mongo;

namespace PizzaApp.Auth.Infrastructure;

public class AuthDbContext
{
    public IMongoCollection<User> Users { get; }

    public AuthDbContext(MongoContext ctx)
    {
        RegisterMappings();
        Users = ctx.GetCollection<User>("Users");
    }

    public static void RegisterMappings()
    {
        if (!BsonClassMap.IsClassMapRegistered(typeof(User)))
        {
            BsonClassMap.RegisterClassMap<User>(cm =>
            {
                cm.AutoMap();
                cm.SetIgnoreExtraElements(true);
                cm.MapIdProperty(u => u.Id)
                  .SetIdGenerator(StringObjectIdGenerator.Instance)
                  .SetSerializer(new StringSerializer(BsonType.String));
            });
        }
    }
}
```

- [ ] **Step 3: Viết test thất bại cho AuthService (logic reset-password không cần Mongo)**

Tách phần logic thuần (kiểm token/expiry) để test được không cần DB. Ta sẽ test qua method tĩnh `AuthService.ValidateResetToken`.

Create `backend/tests/PizzaApp.Auth.Tests/AuthServiceTests.cs`:

```csharp
using PizzaApp.Auth.Core.Entities;
using PizzaApp.Auth.Infrastructure.Services;
using Xunit;

namespace PizzaApp.Auth.Tests;

public class AuthServiceTests
{
    [Fact]
    public void ValidateResetToken_ValidTokenWithinExpiry_ReturnsNull()
    {
        var user = new User
        {
            ResetPasswordToken = "abc",
            ResetPasswordTokenExpiry = DateTime.UtcNow.AddMinutes(10)
        };

        var error = AuthService.ValidateResetToken(user, "abc");

        Assert.Null(error);
    }

    [Fact]
    public void ValidateResetToken_WrongToken_ReturnsError()
    {
        var user = new User
        {
            ResetPasswordToken = "abc",
            ResetPasswordTokenExpiry = DateTime.UtcNow.AddMinutes(10)
        };

        var error = AuthService.ValidateResetToken(user, "wrong");

        Assert.Equal("Token không hợp lệ!", error);
    }

    [Fact]
    public void ValidateResetToken_Expired_ReturnsError()
    {
        var user = new User
        {
            ResetPasswordToken = "abc",
            ResetPasswordTokenExpiry = DateTime.UtcNow.AddMinutes(-1)
        };

        var error = AuthService.ValidateResetToken(user, "abc");

        Assert.Equal("Token đã hết hạn! Vui lòng yêu cầu lại.", error);
    }
}
```

- [ ] **Step 4: Chạy test — phải fail (chưa có AuthService)**

Run: `dotnet test backend/tests/PizzaApp.Auth.Tests`
Expected: FAIL biên dịch — `AuthService` không tồn tại.

- [ ] **Step 5: Viết AuthService (migrate từ monolith, đổi nguồn phụ thuộc)**

Create `backend/src/Services/Auth/PizzaApp.Auth.Infrastructure/Services/AuthService.cs`:

```csharp
using System.Security.Cryptography;
using MongoDB.Driver;
using PizzaApp.Auth.Core.DTOs;
using PizzaApp.Auth.Core.Entities;
using PizzaApp.Auth.Core.Interfaces;
using PizzaApp.BuildingBlocks.Auth;

namespace PizzaApp.Auth.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly IMongoCollection<User> _users;
    private readonly JwtTokenGenerator _tokens;

    public AuthService(AuthDbContext db, JwtTokenGenerator tokens)
    {
        _users = db.Users;
        _tokens = tokens;
    }

    public async Task<AuthResponseDto?> RegisterAsync(RegisterDto dto)
    {
        var exists = await _users.Find(u => u.Email == dto.Email).AnyAsync();
        if (exists) return null;

        var user = new User
        {
            FullName = dto.FullName,
            Email = dto.Email,
            PhoneNumber = dto.PhoneNumber,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password)
        };
        await _users.InsertOneAsync(user);
        return ToAuthResponse(user);
    }

    public async Task<AuthResponseDto?> LoginAsync(LoginDto dto)
    {
        var user = await _users.Find(u => u.Email == dto.Email).FirstOrDefaultAsync();
        if (user == null) return null;
        if (!BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash)) return null;
        return ToAuthResponse(user);
    }

    public async Task<string> ForgotPasswordAsync(string email)
    {
        var user = await _users.Find(u => u.Email == email).FirstOrDefaultAsync();
        if (user is null)
            throw new InvalidOperationException("Email không tồn tại trong hệ thống!");

        var resetToken = GenerateSecureToken();
        var update = Builders<User>.Update
            .Set(u => u.ResetPasswordToken, resetToken)
            .Set(u => u.ResetPasswordTokenExpiry, DateTime.UtcNow.AddMinutes(15));
        await _users.UpdateOneAsync(u => u.Id == user.Id, update);
        return resetToken;
    }

    public async Task ResetPasswordAsync(ResetPasswordDto dto)
    {
        var user = await _users.Find(u => u.Email == dto.Email).FirstOrDefaultAsync();
        if (user is null)
            throw new InvalidOperationException("Email không tồn tại trong hệ thống!");

        var error = ValidateResetToken(user, dto.Token);
        if (error is not null)
            throw new InvalidOperationException(error);

        var newHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
        var update = Builders<User>.Update
            .Set(u => u.PasswordHash, newHash)
            .Set(u => u.ResetPasswordToken, (string?)null)
            .Set(u => u.ResetPasswordTokenExpiry, (DateTime?)null);
        await _users.UpdateOneAsync(u => u.Id == user.Id, update);
    }

    public async Task<UserProfileDto?> GetProfileAsync(string userId)
    {
        var user = await _users.Find(u => u.Id == userId).FirstOrDefaultAsync();
        if (user == null) return null;
        return new UserProfileDto
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            Address = user.Address,
            Role = user.Role
        };
    }

    /// <summary>Kiểm token reset. Trả null nếu hợp lệ, ngược lại trả message lỗi.</summary>
    public static string? ValidateResetToken(User user, string token)
    {
        if (user.ResetPasswordToken != token)
            return "Token không hợp lệ!";
        if (user.ResetPasswordTokenExpiry is null || user.ResetPasswordTokenExpiry < DateTime.UtcNow)
            return "Token đã hết hạn! Vui lòng yêu cầu lại.";
        return null;
    }

    private AuthResponseDto ToAuthResponse(User user) => new()
    {
        Token = _tokens.Generate(user.Id, user.Email, user.FullName, user.Role),
        Id = user.Id,
        FullName = user.FullName,
        Email = user.Email,
        PhoneNumber = user.PhoneNumber
    };

    private static string GenerateSecureToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToHexString(bytes).ToLower();
    }
}
```

- [ ] **Step 6: Chạy test — phải pass**

Run: `dotnet test backend/tests/PizzaApp.Auth.Tests`
Expected: PASS (3 test).

- [ ] **Step 7: Commit**

```bash
git add backend/src/Services/Auth/PizzaApp.Auth.Infrastructure backend/tests/PizzaApp.Auth.Tests backend/PizzaApp.Microservices.sln
git commit -m "feat(auth): add AuthDbContext and AuthService with reset-token validation tests"
```

---

### Task 5: Auth.API — controller, Program.cs, config, Dockerfile

**Files:**
- Create: `backend/src/Services/Auth/PizzaApp.Auth.API/PizzaApp.Auth.API.csproj`
- Create: `backend/src/Services/Auth/PizzaApp.Auth.API/Controllers/AuthController.cs`
- Create: `backend/src/Services/Auth/PizzaApp.Auth.API/Program.cs`
- Create: `backend/src/Services/Auth/PizzaApp.Auth.API/appsettings.json`
- Create: `backend/src/Services/Auth/PizzaApp.Auth.API/Dockerfile`

**Interfaces:**
- Consumes: `IAuthService`, DTOs (Task 3); `AuthService`, `AuthDbContext` (Task 4); `MongoContext`, `MongoSettings`, `JwtSettings`, `JwtTokenGenerator`, `AddPizzaJwtAuthentication` (Task 1–2).
- Produces: Web API lắng nghe cổng 8080 trong container, endpoint `POST /api/auth/register`, `POST /api/auth/login`, `GET /api/auth/me`, `POST /api/auth/forgot-password`, `POST /api/auth/reset-password`.

- [ ] **Step 1: Tạo project Web API + references + packages**

```bash
dotnet new webapi -n PizzaApp.Auth.API -o backend/src/Services/Auth/PizzaApp.Auth.API -f net8.0 --no-openapi false
rm -f backend/src/Services/Auth/PizzaApp.Auth.API/WeatherForecast.cs backend/src/Services/Auth/PizzaApp.Auth.API/Controllers/WeatherForecastController.cs
dotnet sln backend/PizzaApp.Microservices.sln add backend/src/Services/Auth/PizzaApp.Auth.API/PizzaApp.Auth.API.csproj
dotnet add backend/src/Services/Auth/PizzaApp.Auth.API reference backend/src/Services/Auth/PizzaApp.Auth.Core/PizzaApp.Auth.Core.csproj
dotnet add backend/src/Services/Auth/PizzaApp.Auth.API reference backend/src/Services/Auth/PizzaApp.Auth.Infrastructure/PizzaApp.Auth.Infrastructure.csproj
dotnet add backend/src/Services/Auth/PizzaApp.Auth.API reference backend/src/BuildingBlocks/PizzaApp.BuildingBlocks.csproj
dotnet add backend/src/Services/Auth/PizzaApp.Auth.API package Swashbuckle.AspNetCore -v 6.6.2
```

- [ ] **Step 2: Viết AuthController (migrate, đổi namespace)**

Create `backend/src/Services/Auth/PizzaApp.Auth.API/Controllers/AuthController.cs`:

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using PizzaApp.Auth.Core.DTOs;
using PizzaApp.Auth.Core.Interfaces;

namespace PizzaApp.Auth.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService) => _authService = authService;

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterDto dto)
    {
        var result = await _authService.RegisterAsync(dto);
        if (result == null)
            return BadRequest(new { message = "Email đã được sử dụng!" });
        return Ok(new { message = "Đăng ký thành công!", data = result });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto dto)
    {
        var result = await _authService.LoginAsync(dto);
        if (result == null)
            return Unauthorized(new { message = "Email hoặc mật khẩu không đúng!" });
        return Ok(new { message = "Đăng nhập thành công!", data = result });
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> GetMe()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();
        var profile = await _authService.GetProfileAsync(userId);
        if (profile == null)
            return NotFound(new { message = "Không tìm thấy tài khoản." });
        return Ok(profile);
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto dto)
    {
        try
        {
            var token = await _authService.ForgotPasswordAsync(dto.Email);
            return Ok(new { message = "Token reset password đã được tạo.", token });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto dto)
    {
        try
        {
            await _authService.ResetPasswordAsync(dto);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        return Ok(new { message = "Đổi mật khẩu thành công!" });
    }
}
```

- [ ] **Step 3: Viết Program.cs (DI + JWT + config binding)**

Create/overwrite `backend/src/Services/Auth/PizzaApp.Auth.API/Program.cs`:

```csharp
using PizzaApp.Auth.Core.Interfaces;
using PizzaApp.Auth.Infrastructure;
using PizzaApp.Auth.Infrastructure.Services;
using PizzaApp.BuildingBlocks.Auth;
using PizzaApp.BuildingBlocks.Mongo;

var builder = WebApplication.CreateBuilder(args);

// Config binding (đọc từ appsettings + biến môi trường)
var mongoSettings = new MongoSettings();
builder.Configuration.GetSection("MongoDB").Bind(mongoSettings);
var jwtSettings = new JwtSettings();
builder.Configuration.GetSection("JwtSettings").Bind(jwtSettings);

builder.Services.AddSingleton(mongoSettings);
builder.Services.AddSingleton(jwtSettings);
builder.Services.AddSingleton<MongoContext>();
builder.Services.AddSingleton<AuthDbContext>();
builder.Services.AddSingleton<JwtTokenGenerator>();
builder.Services.AddScoped<IAuthService, AuthService>();

builder.Services.AddPizzaJwtAuthentication(jwtSettings);
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(o => o.AddPolicy("AllowAll",
    p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI();
app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
```

Lưu ý: `MongoContext`, `AuthDbContext`, `JwtTokenGenerator` được DI resolve nhờ các settings đã đăng ký singleton (constructor của chúng nhận đúng 1 tham số là settings/context tương ứng).

- [ ] **Step 4: Viết appsettings.json (giá trị dev, secrets qua env)**

Create/overwrite `backend/src/Services/Auth/PizzaApp.Auth.API/appsettings.json`:

```json
{
  "MongoDB": {
    "ConnectionString": "mongodb://localhost:27017",
    "DatabaseName": "PizzaApp_Auth"
  },
  "JwtSettings": {
    "SecretKey": "PizzaAppSuperSecretKey2024_DoNotShare!",
    "Issuer": "PizzaApp",
    "Audience": "PizzaAppUsers",
    "ExpiresInDays": 7
  },
  "Logging": { "LogLevel": { "Default": "Information", "Microsoft.AspNetCore": "Warning" } },
  "AllowedHosts": "*"
}
```

> Ghi chú: trong container, `MongoDB:ConnectionString` và `JwtSettings:SecretKey` sẽ bị **ghi đè bằng biến môi trường** ở docker-compose (Task 6). ASP.NET Core map `MongoDB__ConnectionString` → `MongoDB:ConnectionString`.

- [ ] **Step 5: Viết Dockerfile**

Create `backend/src/Services/Auth/PizzaApp.Auth.API/Dockerfile`:

```dockerfile
# Build từ thư mục backend/ làm context
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY src/BuildingBlocks/ src/BuildingBlocks/
COPY src/Services/Auth/ src/Services/Auth/
RUN dotnet restore src/Services/Auth/PizzaApp.Auth.API/PizzaApp.Auth.API.csproj
RUN dotnet publish src/Services/Auth/PizzaApp.Auth.API/PizzaApp.Auth.API.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "PizzaApp.Auth.API.dll"]
```

- [ ] **Step 6: Build toàn solution — phải xanh**

Run: `dotnet build backend/PizzaApp.Microservices.sln`
Expected: Build succeeded, 0 error.

- [ ] **Step 7: Commit**

```bash
git add backend/src/Services/Auth/PizzaApp.Auth.API backend/PizzaApp.Microservices.sln
git commit -m "feat(auth): add Auth.API controller, DI wiring, config and Dockerfile"
```

---

### Task 6: API Gateway (YARP) + Dockerfile

**Files:**
- Create: `backend/src/ApiGateway/PizzaApp.ApiGateway.csproj`
- Create: `backend/src/ApiGateway/Program.cs`
- Create: `backend/src/ApiGateway/appsettings.json`
- Create: `backend/src/ApiGateway/Dockerfile`

**Interfaces:**
- Consumes: `JwtSettings`, `AddPizzaJwtAuthentication` (BuildingBlocks).
- Produces: reverse proxy cổng 8080, route `/api/auth/{**catch-all}` → cluster `auth` (`http://auth:8080`). Validate JWT nhưng **không bắt buộc auth** ở gateway (mỗi service tự `[Authorize]`); gateway chỉ forward header `Authorization`.

- [ ] **Step 1: Tạo project + package YARP + reference BuildingBlocks**

```bash
dotnet new web -n PizzaApp.ApiGateway -o backend/src/ApiGateway -f net8.0
dotnet sln backend/PizzaApp.Microservices.sln add backend/src/ApiGateway/PizzaApp.ApiGateway.csproj
dotnet add backend/src/ApiGateway package Yarp.ReverseProxy -v 2.2.0
dotnet add backend/src/ApiGateway reference backend/src/BuildingBlocks/PizzaApp.BuildingBlocks.csproj
```

- [ ] **Step 2: Viết Program.cs**

Create/overwrite `backend/src/ApiGateway/Program.cs`:

```csharp
using PizzaApp.BuildingBlocks.Auth;

var builder = WebApplication.CreateBuilder(args);

var jwtSettings = new JwtSettings();
builder.Configuration.GetSection("JwtSettings").Bind(jwtSettings);
builder.Services.AddSingleton(jwtSettings);
builder.Services.AddPizzaJwtAuthentication(jwtSettings);

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

builder.Services.AddCors(o => o.AddPolicy("AllowAll",
    p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

var app = builder.Build();
app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();
app.MapReverseProxy();
app.Run();
```

- [ ] **Step 3: Viết appsettings.json (routes + clusters)**

Create/overwrite `backend/src/ApiGateway/appsettings.json`:

```json
{
  "JwtSettings": {
    "SecretKey": "PizzaAppSuperSecretKey2024_DoNotShare!",
    "Issuer": "PizzaApp",
    "Audience": "PizzaAppUsers",
    "ExpiresInDays": 7
  },
  "ReverseProxy": {
    "Routes": {
      "auth-route": {
        "ClusterId": "auth-cluster",
        "Match": { "Path": "/api/auth/{**catch-all}" }
      }
    },
    "Clusters": {
      "auth-cluster": {
        "Destinations": {
          "auth-1": { "Address": "http://localhost:5001/" }
        }
      }
    }
  },
  "Logging": { "LogLevel": { "Default": "Information" } },
  "AllowedHosts": "*"
}
```

> Ghi chú: địa chỉ `http://localhost:5001/` dùng khi chạy ngoài Docker. Trong docker-compose (Task 6) sẽ ghi đè bằng biến môi trường `ReverseProxy__Clusters__auth-cluster__Destinations__auth-1__Address=http://auth:8080/`.

- [ ] **Step 4: Viết Dockerfile**

Create `backend/src/ApiGateway/Dockerfile`:

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY src/BuildingBlocks/ src/BuildingBlocks/
COPY src/ApiGateway/ src/ApiGateway/
RUN dotnet restore src/ApiGateway/PizzaApp.ApiGateway.csproj
RUN dotnet publish src/ApiGateway/PizzaApp.ApiGateway.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "PizzaApp.ApiGateway.dll"]
```

- [ ] **Step 5: Build — phải xanh**

Run: `dotnet build backend/src/ApiGateway`
Expected: Build succeeded.

- [ ] **Step 6: Commit**

```bash
git add backend/src/ApiGateway backend/PizzaApp.Microservices.sln
git commit -m "feat(gateway): add YARP API gateway routing to Auth service"
```

---

### Task 7: Docker Compose + smoke test end-to-end

**Files:**
- Create: `backend/docker-compose.yml`
- Create: `backend/.env.example`
- Create: `backend/smoke-test-auth.sh`

**Interfaces:**
- Consumes: Auth.API image (Task 5), Gateway image (Task 6).
- Produces: stack chạy được với `docker compose up`; gateway expose cổng host `8080`.

- [ ] **Step 1: Viết docker-compose.yml**

Create `backend/docker-compose.yml`:

```yaml
services:
  mongo:
    image: mongo:7
    ports:
      - "27017:27017"
    volumes:
      - mongo-data:/data/db

  rabbitmq:
    image: rabbitmq:3-management
    ports:
      - "5672:5672"
      - "15672:15672"

  auth:
    build:
      context: .
      dockerfile: src/Services/Auth/PizzaApp.Auth.API/Dockerfile
    environment:
      - MongoDB__ConnectionString=mongodb://mongo:27017
      - MongoDB__DatabaseName=PizzaApp_Auth
      - JwtSettings__SecretKey=${JWT_SECRET_KEY}
    depends_on:
      - mongo

  gateway:
    build:
      context: .
      dockerfile: src/ApiGateway/Dockerfile
    ports:
      - "8080:8080"
    environment:
      - JwtSettings__SecretKey=${JWT_SECRET_KEY}
      - ReverseProxy__Clusters__auth-cluster__Destinations__auth-1__Address=http://auth:8080/
    depends_on:
      - auth

volumes:
  mongo-data:
```

- [ ] **Step 2: Viết .env.example**

Create `backend/.env.example`:

```env
# Copy thành .env và điền giá trị thật. KHÔNG commit .env.
JWT_SECRET_KEY=PizzaAppSuperSecretKey2024_DoNotShare!
```

Thêm dòng `backend/.env` vào `.gitignore` gốc repo (nếu chưa có), rồi tạo `.env` local từ mẫu:
```bash
grep -qxF 'backend/.env' .gitignore || echo 'backend/.env' >> .gitignore
cp backend/.env.example backend/.env
```

- [ ] **Step 3: Khởi động stack**

Run: `cd backend && docker compose up -d --build`
Expected: 4 container `mongo`, `rabbitmq`, `auth`, `gateway` ở trạng thái `Up`. Kiểm tra: `docker compose ps`.

- [ ] **Step 4: Viết smoke test script**

Create `backend/smoke-test-auth.sh`:

```bash
#!/usr/bin/env bash
set -euo pipefail
BASE="http://localhost:8080"
EMAIL="smoke_$(date +%s)@test.com"

echo "== Register =="
REG=$(curl -s -X POST "$BASE/api/auth/register" \
  -H "Content-Type: application/json" \
  -d "{\"fullName\":\"Smoke Test\",\"email\":\"$EMAIL\",\"password\":\"secret123\",\"phoneNumber\":\"0900000000\"}")
echo "$REG"
echo "$REG" | grep -q '"token"' || { echo "FAIL: no token on register"; exit 1; }

echo "== Login =="
LOGIN=$(curl -s -X POST "$BASE/api/auth/login" \
  -H "Content-Type: application/json" \
  -d "{\"email\":\"$EMAIL\",\"password\":\"secret123\"}")
echo "$LOGIN"
TOKEN=$(echo "$LOGIN" | sed -n 's/.*"token":"\([^"]*\)".*/\1/p')
[ -n "$TOKEN" ] || { echo "FAIL: no token on login"; exit 1; }

echo "== GetMe (qua gateway, JWT) =="
ME=$(curl -s "$BASE/api/auth/me" -H "Authorization: Bearer $TOKEN")
echo "$ME"
echo "$ME" | grep -q "$EMAIL" || { echo "FAIL: /me không trả đúng user"; exit 1; }

echo "ALL AUTH SMOKE TESTS PASSED"
```

- [ ] **Step 5: Chạy smoke test qua Gateway**

Run: `bash backend/smoke-test-auth.sh`
Expected: In ra `ALL AUTH SMOKE TESTS PASSED`. Xác nhận: register trả token, login trả token, `/api/auth/me` (đi qua gateway + validate JWT) trả đúng email vừa đăng ký.

> Trên Windows không có bash: chạy `docker compose` bằng PowerShell, và thực thi smoke test bằng `bash backend/smoke-test-auth.sh` qua Git Bash (đã có sẵn), hoặc dùng `Invoke-RestMethod` tương đương.

- [ ] **Step 6: Dừng stack**

Run: `cd backend && docker compose down`
Expected: Các container dừng và gỡ.

- [ ] **Step 7: Commit**

```bash
git add backend/docker-compose.yml backend/.env.example backend/smoke-test-auth.sh .gitignore
git commit -m "feat(backend): docker-compose stack (mongo, rabbitmq, auth, gateway) + auth smoke test"
```

---

## Self-Review

**Spec coverage (Plan 1 phần liên quan):**
- Kiến trúc gateway + service riêng ✓ (Task 5, 6)
- Database per service (`PizzaApp_Auth`) ✓ (Task 5 appsettings, Task 7 compose)
- BuildingBlocks chứa JWT + Mongo, không business logic ✓ (Task 1, 2)
- JWT dùng chung, tách secrets khỏi appsettings ✓ (Task 5–7: secret qua env)
- RabbitMQ dựng sẵn cho plan sau ✓ (Task 7 compose)
- Docker Compose 1 lệnh chạy ✓ (Task 7)
- Smoke test luồng chính (đăng nhập qua gateway) ✓ (Task 7)
- CI/CD, Flutter, các service còn lại → thuộc Plan 2–5 (ngoài phạm vi Plan 1, đúng thiết kế).

**Placeholder scan:** Không có TBD/TODO chưa giải quyết; mọi step có code/lệnh cụ thể.

**Type consistency:** `JwtTokenGenerator.Generate(userId,email,fullName,role)` dùng nhất quán ở AuthService (Task 4). `AuthDbContext.Users` dùng ở AuthService. `MongoContext.GetCollection<T>` dùng ở AuthDbContext. `AddPizzaJwtAuthentication(JwtSettings)` dùng ở Auth.API + Gateway. `ValidateResetToken(User, string)` khai báo Task 4 Step 5, test Task 4 Step 3 — khớp.

**Rủi ro cần lưu ý khi thực thi:**
- DI resolve `MongoContext`/`AuthDbContext`/`JwtTokenGenerator` dựa trên constructor 1 tham số đã đăng ký. Nếu gặp lỗi resolve, đăng ký tường minh bằng factory `AddSingleton(sp => new MongoContext(sp.GetRequiredService<MongoSettings>()))`.
- `dotnet new webapi` mẫu net8.0 có thể dùng minimal API + không tạo `Controllers/`. Nếu vậy, tạo thư mục `Controllers/` thủ công và giữ `AddControllers()/MapControllers()` như Program.cs đã viết.
