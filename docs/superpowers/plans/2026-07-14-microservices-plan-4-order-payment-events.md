# Plan 4 — Order + Payment + RabbitMQ Events Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:executing-plans. Steps use checkbox (`- [ ]`).

**Goal:** Tách **Order Service** và **Payment Service**, thêm **event bus RabbitMQ** (BuildingBlocks). Order điều phối tạo đơn + link thanh toán qua REST; dùng event bất đồng bộ cho `OrderCreated` (Cart xóa giỏ) và `PaymentSucceeded` (Order mark Paid).

**Architecture (Approach B — Order điều phối, thanh toán QR trừu tượng, KHÔNG PayOS):**
1. `POST /api/orders` → Order: REST Product (giá/tên) → lưu Order → REST Payment create (sinh QR) → lưu `PaymentUrl` + `PaymentQr` → publish `OrderCreated` → trả order + link + QR.
2. Cart nghe `OrderCreated` → xóa giỏ.
3. Quét QR (camera điện thoại) → mở `GET /api/payment/confirm/{code}` → mark PAID → publish `PaymentSucceeded`.
4. Order nghe `PaymentSucceeded` → mark đơn Paid.

**Tech Stack:** .NET 8, MongoDB.Driver 2.28, YARP, xUnit, HttpClient, **RabbitMQ.Client 6.8.1**, **QRCoder 1.6.0** (sinh QR PNG thuần .NET, không PayOS).

## Global Constraints

- Kế thừa Global Constraints Plan 1–3.
- Order Service: DB `PizzaApp_Order`, collections `Orders`. Cổng dev 5005.
- Payment Service: DB `PizzaApp_Payment`, collection `Payments`. Cổng dev 5006.
- Event bus: RabbitMQ topic exchange `pizzaapp.events`; routing key = tên event (`OrderCreatedEvent`, `PaymentSucceededEvent`).
- Publisher **kết nối lazy + bỏ qua lỗi** (không làm chết request nếu broker tạm sự cố — chỉ log). Consumer là `BackgroundService` **retry kết nối** trong vòng lặp (không crash app khi broker chưa lên) → boot được không cần RabbitMQ.
- Thanh toán qua `IPaymentGateway` (mặc định `MockPaymentGateway`, sinh QR). `Payment:PublicBaseUrl` (env) là IP LAN của máy để điện thoại quét QR gọi vào được (KHÔNG dùng localhost).

---

## File Structure (Plan 4)

```
backend/src/BuildingBlocks/Messaging/
  EventBusSettings.cs, IEventBus.cs, IntegrationEvents.cs,
  RabbitMqEventBus.cs, RabbitMqConsumerService.cs, MessagingExtensions.cs
backend/src/Services/Order/
  PizzaApp.Order.Core/          Entities/{Order,OrderItem}.cs, DTOs/*, Interfaces/{IOrderService,IProductClient,IPaymentClient}.cs
  PizzaApp.Order.Infrastructure/ OrderDbContext.cs, Services/OrderService.cs, Clients/{ProductHttpClient,PaymentHttpClient}.cs, Consumers/PaymentSucceededConsumer.cs
  PizzaApp.Order.API/           Controllers/OrdersController.cs, Program.cs, appsettings.json, Dockerfile
backend/src/Services/Payment/
  PizzaApp.Payment.Core/        Entities/Payment.cs, DTOs/*, Interfaces/IPaymentService.cs
  PizzaApp.Payment.Infrastructure/ PaymentDbContext.cs, Services/PaymentService.cs
  PizzaApp.Payment.API/         Controllers/PaymentController.cs, Program.cs, appsettings.json, Dockerfile
backend/src/Services/Cart/PizzaApp.Cart.Infrastructure/Consumers/OrderCreatedConsumer.cs  (mới)
backend/tests/PizzaApp.Order.Tests/, PizzaApp.Payment.Tests/, PizzaApp.BuildingBlocks.Tests/ (thêm test messaging)
backend/src/ApiGateway/appsettings.json   (route orders + payment)
backend/docker-compose.yml                (service order + payment + env RabbitMQ cho các service)
```

---

### Task 1: BuildingBlocks — Event bus RabbitMQ

**Files:**
- Create: `src/BuildingBlocks/Messaging/EventBusSettings.cs`, `IEventBus.cs`, `IntegrationEvents.cs`, `RabbitMqEventBus.cs`, `RabbitMqConsumerService.cs`, `MessagingExtensions.cs`
- Test: `tests/PizzaApp.BuildingBlocks.Tests/IntegrationEventsTests.cs`

**Interfaces:**
- Produces:
  - `class EventBusSettings { string HostName="localhost"; string ExchangeName="pizzaapp.events"; }`
  - `record OrderCreatedEvent(string OrderId, string UserId, decimal TotalPrice)`
  - `record PaymentSucceededEvent(string OrderId)`
  - `interface IEventBus { void Publish<T>(T @event); }`
  - `RabbitMqEventBus : IEventBus` (lazy connect, publish JSON, routing key = `typeof(T).Name`)
  - `RabbitMqConsumerService<T> : BackgroundService` (queue bound tới routing key `typeof(T).Name`, gọi handler qua scope)
  - `MessagingExtensions.AddRabbitMqEventBus(settings)`, `AddRabbitMqConsumer<T>(Func<IServiceProvider,T,Task> handler)`

- [ ] **Step 1: Thêm package RabbitMQ.Client**

```bash
cd backend
dotnet add src/BuildingBlocks package RabbitMQ.Client -v 6.8.1
dotnet add src/BuildingBlocks package Microsoft.Extensions.Hosting.Abstractions -v 8.0.1
dotnet add src/BuildingBlocks package Microsoft.Extensions.DependencyInjection.Abstractions -v 8.0.2
```

- [ ] **Step 2: EventBusSettings + IntegrationEvents + IEventBus**

`Messaging/EventBusSettings.cs`:
```csharp
namespace PizzaApp.BuildingBlocks.Messaging;

public class EventBusSettings
{
    public string HostName { get; set; } = "localhost";
    public string ExchangeName { get; set; } = "pizzaapp.events";
}
```

`Messaging/IntegrationEvents.cs`:
```csharp
namespace PizzaApp.BuildingBlocks.Messaging;

public record OrderCreatedEvent(string OrderId, string UserId, decimal TotalPrice);
public record PaymentSucceededEvent(string OrderId);
```

`Messaging/IEventBus.cs`:
```csharp
namespace PizzaApp.BuildingBlocks.Messaging;

public interface IEventBus
{
    void Publish<T>(T @event);
}
```

- [ ] **Step 3: Failing test (routing key + JSON round-trip)**

`tests/PizzaApp.BuildingBlocks.Tests/IntegrationEventsTests.cs`:
```csharp
using System.Text.Json;
using PizzaApp.BuildingBlocks.Messaging;
using Xunit;

namespace PizzaApp.BuildingBlocks.Tests;

public class IntegrationEventsTests
{
    [Fact]
    public void RoutingKey_IsEventTypeName()
    {
        Assert.Equal("OrderCreatedEvent", EventRouting.RoutingKeyFor<OrderCreatedEvent>());
        Assert.Equal("PaymentSucceededEvent", EventRouting.RoutingKeyFor<PaymentSucceededEvent>());
    }

    [Fact]
    public void OrderCreatedEvent_JsonRoundTrips()
    {
        var evt = new OrderCreatedEvent("o1", "u1", 42.5m);
        var json = JsonSerializer.Serialize(evt);
        var back = JsonSerializer.Deserialize<OrderCreatedEvent>(json);
        Assert.Equal(evt, back);
    }
}
```

- [ ] **Step 4: Chạy test — fail** (`EventRouting` chưa có).

- [ ] **Step 5: EventRouting + RabbitMqEventBus**

`Messaging/RabbitMqEventBus.cs`:
```csharp
using System.Text;
using System.Text.Json;
using RabbitMQ.Client;

namespace PizzaApp.BuildingBlocks.Messaging;

public static class EventRouting
{
    public static string RoutingKeyFor<T>() => typeof(T).Name;
}

public class RabbitMqEventBus : IEventBus, IDisposable
{
    private readonly EventBusSettings _settings;
    private IConnection? _connection;
    private IModel? _channel;
    private readonly object _lock = new();

    public RabbitMqEventBus(EventBusSettings settings) => _settings = settings;

    private void EnsureChannel()
    {
        if (_channel is { IsOpen: true }) return;
        lock (_lock)
        {
            if (_channel is { IsOpen: true }) return;
            var factory = new ConnectionFactory { HostName = _settings.HostName, DispatchConsumersAsync = false };
            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();
            _channel.ExchangeDeclare(_settings.ExchangeName, ExchangeType.Topic, durable: true);
        }
    }

    public void Publish<T>(T @event)
    {
        try
        {
            EnsureChannel();
            var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(@event));
            var props = _channel!.CreateBasicProperties();
            props.ContentType = "application/json";
            props.DeliveryMode = 2; // persistent
            _channel.BasicPublish(_settings.ExchangeName, EventRouting.RoutingKeyFor<T>(), props, body);
        }
        catch (Exception ex)
        {
            // Không làm chết request nếu broker tạm sự cố — chỉ log ra console.
            Console.WriteLine($"[EventBus] Publish {typeof(T).Name} thất bại: {ex.Message}");
        }
    }

    public void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
    }
}
```

- [ ] **Step 6: Chạy test — pass**.

- [ ] **Step 7: RabbitMqConsumerService (resilient background consumer)**

`Messaging/RabbitMqConsumerService.cs`:
```csharp
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace PizzaApp.BuildingBlocks.Messaging;

public class RabbitMqConsumerService<T> : BackgroundService
{
    private readonly EventBusSettings _settings;
    private readonly IServiceProvider _services;
    private readonly Func<IServiceProvider, T, Task> _handler;
    private readonly string _queueName;

    public RabbitMqConsumerService(EventBusSettings settings, IServiceProvider services,
        Func<IServiceProvider, T, Task> handler, string queueName)
    {
        _settings = settings;
        _services = services;
        _handler = handler;
        _queueName = queueName;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Retry kết nối tới khi broker sẵn sàng (không crash app nếu RabbitMQ chưa lên).
        IConnection? connection = null;
        IModel? channel = null;
        while (!stoppingToken.IsCancellationRequested && channel == null)
        {
            try
            {
                var factory = new ConnectionFactory { HostName = _settings.HostName, DispatchConsumersAsync = false };
                connection = factory.CreateConnection();
                channel = connection.CreateModel();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Consumer {_queueName}] chờ RabbitMQ: {ex.Message}");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
        if (channel == null) return;

        channel.ExchangeDeclare(_settings.ExchangeName, ExchangeType.Topic, durable: true);
        channel.QueueDeclare(_queueName, durable: true, exclusive: false, autoDelete: false);
        channel.QueueBind(_queueName, _settings.ExchangeName, EventRouting.RoutingKeyFor<T>());

        var consumer = new EventingBasicConsumer(channel);
        consumer.Received += async (_, ea) =>
        {
            try
            {
                var json = Encoding.UTF8.GetString(ea.Body.ToArray());
                var evt = JsonSerializer.Deserialize<T>(json);
                if (evt != null)
                {
                    using var scope = _services.CreateScope();
                    await _handler(scope.ServiceProvider, evt);
                }
                channel.BasicAck(ea.DeliveryTag, false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Consumer {_queueName}] xử lý lỗi: {ex.Message}");
                channel.BasicNack(ea.DeliveryTag, false, requeue: false);
            }
        };
        channel.BasicConsume(_queueName, autoAck: false, consumer);

        await Task.Delay(Timeout.Infinite, stoppingToken).ContinueWith(_ => { });
        channel.Dispose();
        connection?.Dispose();
    }
}
```

- [ ] **Step 8: MessagingExtensions**

`Messaging/MessagingExtensions.cs`:
```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace PizzaApp.BuildingBlocks.Messaging;

public static class MessagingExtensions
{
    public static IServiceCollection AddRabbitMqEventBus(this IServiceCollection services, EventBusSettings settings)
    {
        services.AddSingleton(settings);
        services.AddSingleton<IEventBus, RabbitMqEventBus>();
        return services;
    }

    public static IServiceCollection AddRabbitMqConsumer<T>(this IServiceCollection services,
        string queueName, Func<IServiceProvider, T, Task> handler)
    {
        services.AddSingleton<IHostedService>(sp =>
            new RabbitMqConsumerService<T>(
                sp.GetRequiredService<EventBusSettings>(), sp, handler, queueName));
        return services;
    }
}
```

- [ ] **Step 9: Build BuildingBlocks + chạy toàn bộ test BuildingBlocks** = pass.

---

### Task 2: Order Service (Core + Infrastructure + clients + consumer + tests)

**Files:**
- Order.Core: `Entities/Order.cs` (Order + OrderItem, thêm `PaymentUrl`), `DTOs/OrderDtos.cs`, `Interfaces/IOrderService.cs`, `Interfaces/IProductClient.cs`, `Interfaces/IPaymentClient.cs`
- Order.Infrastructure: `OrderDbContext.cs`, `Services/OrderService.cs`, `Clients/ProductHttpClient.cs`, `Clients/PaymentHttpClient.cs`
- Test: `tests/PizzaApp.Order.Tests/OrderServiceTests.cs`

**Interfaces:**
- `record ProductInfo(string Id, string Name, string ImageUrl, decimal Price)`; `IProductClient.GetProductAsync(id) : ProductInfo?`
- `record PaymentLink(string CheckoutUrl)`; `IPaymentClient.CreatePaymentAsync(orderId, amount, items) : PaymentLink?` (POST `/api/payment/create`)
- `IOrderService` = giống monolith (Create/Confirm/GetMy/Detail/Cancel/Admin/Shipper).
- `OrderService(OrderDbContext db, IProductClient product, IPaymentClient payment, IEventBus bus)`.
- static `OrderService.MapToDto(Order)`.

- [ ] **Step 1: Scaffold + refs**

```bash
dotnet new classlib -n PizzaApp.Order.Core -o src/Services/Order/PizzaApp.Order.Core -f net8.0
dotnet new classlib -n PizzaApp.Order.Infrastructure -o src/Services/Order/PizzaApp.Order.Infrastructure -f net8.0
dotnet new xunit -n PizzaApp.Order.Tests -o tests/PizzaApp.Order.Tests -f net8.0
rm -f src/Services/Order/PizzaApp.Order.Core/Class1.cs src/Services/Order/PizzaApp.Order.Infrastructure/Class1.cs tests/PizzaApp.Order.Tests/UnitTest1.cs
dotnet sln add src/Services/Order/PizzaApp.Order.Core/PizzaApp.Order.Core.csproj src/Services/Order/PizzaApp.Order.Infrastructure/PizzaApp.Order.Infrastructure.csproj tests/PizzaApp.Order.Tests/PizzaApp.Order.Tests.csproj
dotnet add src/Services/Order/PizzaApp.Order.Infrastructure reference src/Services/Order/PizzaApp.Order.Core/PizzaApp.Order.Core.csproj src/BuildingBlocks/PizzaApp.BuildingBlocks.csproj
dotnet add src/Services/Order/PizzaApp.Order.Infrastructure package MongoDB.Driver -v 2.28.0
dotnet add tests/PizzaApp.Order.Tests reference src/Services/Order/PizzaApp.Order.Infrastructure/PizzaApp.Order.Infrastructure.csproj src/Services/Order/PizzaApp.Order.Core/PizzaApp.Order.Core.csproj
```

- [ ] **Step 2: Order.Core** — `Entities/Order.cs` (dùng alias `OrderEntity` ở Infra vì trùng segment):
```csharp
namespace PizzaApp.Order.Core.Entities;

public class Order
{
    public string Id { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public List<OrderItem> OrderItems { get; set; } = new();
    public decimal TotalPrice { get; set; }
    public string Status { get; set; } = "AwaitingPayment";
    public string PaymentStatus { get; set; } = "Unpaid";
    public string PaymentMethod { get; set; } = "PayOS";
    public string PaymentUrl { get; set; } = string.Empty; // link PayOS (denormalized để hiển thị lại)
    public string DeliveryAddress { get; set; } = string.Empty;
    public string ShipperId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class OrderItem
{
    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string ProductImageUrl { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public string Size { get; set; } = "M";
}
```

`DTOs/OrderDtos.cs`: (CreateOrderDto, OrderItemDto, OrderResultDto, OrderItemResultDto — như monolith; OrderResultDto có `PaymentUrl`).
```csharp
namespace PizzaApp.Order.Core.DTOs;

public class CreateOrderDto
{
    public string DeliveryAddress { get; set; } = string.Empty;
    public List<OrderItemDto> Items { get; set; } = new();
}

public class OrderItemDto
{
    public string ProductId { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string Size { get; set; } = "M";
}

public class OrderResultDto
{
    public string Id { get; set; } = string.Empty;
    public decimal TotalPrice { get; set; }
    public string Status { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = "Unpaid";
    public string PaymentUrl { get; set; } = string.Empty;
    public string DeliveryAddress { get; set; } = string.Empty;
    public string ShipperId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public List<OrderItemResultDto> Items { get; set; } = new();
}

public class OrderItemResultDto
{
    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string ProductImageUrl { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public string Size { get; set; } = "M";
}
```

`Interfaces/IProductClient.cs`:
```csharp
namespace PizzaApp.Order.Core.Interfaces;

public record ProductInfo(string Id, string Name, string ImageUrl, decimal Price);

public interface IProductClient
{
    Task<ProductInfo?> GetProductAsync(string productId);
}
```

`Interfaces/IPaymentClient.cs`:
```csharp
using PizzaApp.Order.Core.DTOs;

namespace PizzaApp.Order.Core.Interfaces;

public record PaymentItem(string Name, int Quantity, int Price);
public record PaymentLink(string CheckoutUrl);

public interface IPaymentClient
{
    /// <summary>Tạo link PayOS cho đơn. Trả null nếu thất bại.</summary>
    Task<PaymentLink?> CreatePaymentAsync(string orderId, decimal amount, List<PaymentItem> items);
}
```

`Interfaces/IOrderService.cs`: (copy chữ ký monolith y nguyên — Create/Confirm/GetMy/Detail/Cancel/GetAll/GetByStatus/UpdateStatus/Claim/GetShipper/UpdateDelivery).
```csharp
using PizzaApp.Order.Core.DTOs;

namespace PizzaApp.Order.Core.Interfaces;

public interface IOrderService
{
    Task<OrderResultDto> CreateOrderAsync(string userId, CreateOrderDto dto);
    Task<bool> ConfirmPaymentAsync(string orderId);
    Task<List<OrderResultDto>> GetMyOrdersAsync(string userId);
    Task<OrderResultDto?> GetOrderDetailAsync(string orderId, string userId);
    Task<bool> CancelOrderAsync(string orderId, string userId);
    Task<List<OrderResultDto>> GetAllOrdersAsync();
    Task<List<OrderResultDto>> GetOrdersByStatusAsync(string status);
    Task<bool> UpdateOrderStatusAsync(string orderId, string status);
    Task<bool> ClaimOrderAsync(string orderId, string shipperId);
    Task<List<OrderResultDto>> GetShipperOrdersAsync(string shipperId);
    Task<bool> UpdateDeliveryStatusAsync(string orderId, string shipperId, string status);
}
```
> Lưu ý: `CreateOrderAsync` đổi kiểu trả về từ `string` (monolith) → `OrderResultDto` để trả kèm `PaymentUrl` cho client trong 1 lời gọi.

- [ ] **Step 3: OrderDbContext** (alias `OrderEntity`, map cả OrderItem):
```csharp
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.IdGenerators;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using PizzaApp.BuildingBlocks.Mongo;
using OrderEntity = PizzaApp.Order.Core.Entities.Order;
using PizzaApp.Order.Core.Entities;

namespace PizzaApp.Order.Infrastructure;

public class OrderDbContext
{
    public IMongoCollection<OrderEntity> Orders { get; }

    public OrderDbContext(MongoContext ctx)
    {
        RegisterMappings();
        Orders = ctx.GetCollection<OrderEntity>("Orders");
    }

    public static void RegisterMappings()
    {
        if (!BsonClassMap.IsClassMapRegistered(typeof(OrderItem)))
        {
            BsonClassMap.RegisterClassMap<OrderItem>(cm => { cm.AutoMap(); cm.SetIgnoreExtraElements(true); });
        }
        if (!BsonClassMap.IsClassMapRegistered(typeof(OrderEntity)))
        {
            BsonClassMap.RegisterClassMap<OrderEntity>(cm =>
            {
                cm.AutoMap();
                cm.SetIgnoreExtraElements(true);
                cm.MapIdProperty(o => o.Id)
                  .SetIdGenerator(StringObjectIdGenerator.Instance)
                  .SetSerializer(new StringSerializer(BsonType.String));
            });
        }
    }
}
```

- [ ] **Step 4: Failing test** `tests/PizzaApp.Order.Tests/OrderServiceTests.cs`:
```csharp
using PizzaApp.Order.Infrastructure.Services;
using Xunit;
using OrderEntity = PizzaApp.Order.Core.Entities.Order;
using PizzaApp.Order.Core.Entities;

namespace PizzaApp.Order.Tests;

public class OrderServiceTests
{
    [Fact]
    public void MapToDto_MapsOrderAndItems()
    {
        var order = new OrderEntity
        {
            Id = "o1", TotalPrice = 20m, Status = "Paid", PaymentStatus = "Paid",
            PaymentUrl = "http://pay", DeliveryAddress = "HN", ShipperId = "s1",
            OrderItems = { new OrderItem { ProductId = "p1", ProductName = "Pizza", Quantity = 2, UnitPrice = 10m, Size = "L" } }
        };

        var dto = OrderService.MapToDto(order);

        Assert.Equal("o1", dto.Id);
        Assert.Equal(20m, dto.TotalPrice);
        Assert.Equal("Paid", dto.Status);
        Assert.Equal("http://pay", dto.PaymentUrl);
        Assert.Single(dto.Items);
        Assert.Equal("Pizza", dto.Items[0].ProductName);
    }
}
```

- [ ] **Step 5: Chạy test — fail**.

- [ ] **Step 6: OrderService** (migrate; CreateOrder gọi Product + Payment, publish OrderCreated):
```csharp
using MongoDB.Driver;
using PizzaApp.Order.Core.DTOs;
using PizzaApp.Order.Core.Interfaces;
using PizzaApp.BuildingBlocks.Messaging;
using OrderEntity = PizzaApp.Order.Core.Entities.Order;
using PizzaApp.Order.Core.Entities;

namespace PizzaApp.Order.Infrastructure.Services;

public class OrderService : IOrderService
{
    private readonly IMongoCollection<OrderEntity> _orders;
    private readonly IProductClient _productClient;
    private readonly IPaymentClient _paymentClient;
    private readonly IEventBus _bus;

    public OrderService(OrderDbContext db, IProductClient productClient, IPaymentClient paymentClient, IEventBus bus)
    {
        _orders = db.Orders;
        _productClient = productClient;
        _paymentClient = paymentClient;
        _bus = bus;
    }

    public async Task<OrderResultDto> CreateOrderAsync(string userId, CreateOrderDto dto)
    {
        if (dto.Items == null || dto.Items.Count == 0)
            throw new InvalidOperationException("Giỏ hàng không có sản phẩm nào.");

        var order = new OrderEntity
        {
            UserId = userId,
            DeliveryAddress = dto.DeliveryAddress,
            Status = "AwaitingPayment",
            PaymentStatus = "Unpaid",
            PaymentMethod = "PayOS",
            CreatedAt = DateTime.UtcNow
        };

        decimal total = 0;
        var payItems = new List<PaymentItem>();
        foreach (var item in dto.Items)
        {
            var product = await _productClient.GetProductAsync(item.ProductId);
            if (product == null) continue;
            order.OrderItems.Add(new OrderItem
            {
                ProductId = product.Id,
                ProductName = product.Name,
                ProductImageUrl = product.ImageUrl,
                Quantity = item.Quantity,
                UnitPrice = product.Price,
                Size = item.Size
            });
            total += product.Price * item.Quantity;
            payItems.Add(new PaymentItem(product.Name, item.Quantity, (int)product.Price));
        }

        if (order.OrderItems.Count == 0)
            throw new InvalidOperationException("Không có sản phẩm hợp lệ trong đơn hàng.");

        order.TotalPrice = total;
        await _orders.InsertOneAsync(order);

        var link = await _paymentClient.CreatePaymentAsync(order.Id, total, payItems);
        if (link == null)
        {
            await _orders.DeleteOneAsync(o => o.Id == order.Id);
            throw new InvalidOperationException("Không tạo được link thanh toán PayOS.");
        }
        order.PaymentUrl = link.CheckoutUrl;
        await _orders.UpdateOneAsync(o => o.Id == order.Id,
            Builders<OrderEntity>.Update.Set(o => o.PaymentUrl, link.CheckoutUrl));

        _bus.Publish(new OrderCreatedEvent(order.Id, userId, total));

        return MapToDto(order);
    }

    public async Task<bool> ConfirmPaymentAsync(string orderId)
    {
        var result = await _orders.UpdateOneAsync(
            o => o.Id == orderId && o.PaymentStatus == "Unpaid",
            Builders<OrderEntity>.Update
                .Set(o => o.PaymentStatus, "Paid")
                .Set(o => o.Status, "Paid"));
        return result.ModifiedCount > 0;
    }

    public async Task<List<OrderResultDto>> GetMyOrdersAsync(string userId)
    {
        var orders = await _orders.Find(o => o.UserId == userId).SortByDescending(o => o.CreatedAt).ToListAsync();
        return orders.Select(MapToDto).ToList();
    }

    public async Task<OrderResultDto?> GetOrderDetailAsync(string orderId, string userId)
    {
        var order = await _orders.Find(o => o.Id == orderId && o.UserId == userId).FirstOrDefaultAsync();
        return order == null ? null : MapToDto(order);
    }

    public async Task<bool> CancelOrderAsync(string orderId, string userId)
    {
        var result = await _orders.UpdateOneAsync(
            o => o.Id == orderId && o.UserId == userId && o.Status == "AwaitingPayment",
            Builders<OrderEntity>.Update.Set(o => o.Status, "Cancelled"));
        return result.ModifiedCount > 0;
    }

    public async Task<List<OrderResultDto>> GetOrdersByStatusAsync(string status)
    {
        var orders = await _orders.Find(o => o.Status == status).ToListAsync();
        return orders.Select(MapToDto).ToList();
    }

    public async Task<List<OrderResultDto>> GetAllOrdersAsync()
    {
        var orders = await _orders.Find(_ => true).SortByDescending(o => o.CreatedAt).ToListAsync();
        return orders.Select(MapToDto).ToList();
    }

    public async Task<bool> UpdateOrderStatusAsync(string orderId, string status)
    {
        var result = await _orders.UpdateOneAsync(o => o.Id == orderId,
            Builders<OrderEntity>.Update.Set(o => o.Status, status));
        return result.ModifiedCount > 0;
    }

    public async Task<bool> ClaimOrderAsync(string orderId, string shipperId)
    {
        var result = await _orders.UpdateOneAsync(
            o => o.Id == orderId && o.Status == "Ready" && o.ShipperId == "",
            Builders<OrderEntity>.Update.Set(o => o.Status, "Delivering").Set(o => o.ShipperId, shipperId));
        return result.ModifiedCount > 0;
    }

    public async Task<List<OrderResultDto>> GetShipperOrdersAsync(string shipperId)
    {
        var orders = await _orders.Find(o => o.ShipperId == shipperId).SortByDescending(o => o.CreatedAt).ToListAsync();
        return orders.Select(MapToDto).ToList();
    }

    public async Task<bool> UpdateDeliveryStatusAsync(string orderId, string shipperId, string status)
    {
        if (status != "Done" && status != "Cancelled") return false;
        var result = await _orders.UpdateOneAsync(
            o => o.Id == orderId && o.ShipperId == shipperId && o.Status == "Delivering",
            Builders<OrderEntity>.Update.Set(o => o.Status, status));
        return result.ModifiedCount > 0;
    }

    public static OrderResultDto MapToDto(OrderEntity o) => new()
    {
        Id = o.Id,
        TotalPrice = o.TotalPrice,
        Status = o.Status,
        PaymentStatus = o.PaymentStatus,
        PaymentUrl = o.PaymentUrl,
        DeliveryAddress = o.DeliveryAddress,
        ShipperId = o.ShipperId,
        CreatedAt = o.CreatedAt,
        Items = o.OrderItems.Select(i => new OrderItemResultDto
        {
            ProductId = i.ProductId,
            ProductName = i.ProductName,
            ProductImageUrl = i.ProductImageUrl,
            Quantity = i.Quantity,
            UnitPrice = i.UnitPrice,
            Size = i.Size
        }).ToList()
    };
}
```

- [ ] **Step 7: Clients** `Clients/ProductHttpClient.cs` (giống Cart's ProductHttpClient, namespace Order) và `Clients/PaymentHttpClient.cs`:
```csharp
// ProductHttpClient.cs
using System.Net;
using System.Net.Http.Json;
using PizzaApp.Order.Core.Interfaces;

namespace PizzaApp.Order.Infrastructure.Clients;

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
        return dto == null ? null : new ProductInfo(dto.Id, dto.Name, dto.ImageUrl, dto.Price);
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
```csharp
// PaymentHttpClient.cs
using System.Net.Http.Json;
using PizzaApp.Order.Core.Interfaces;

namespace PizzaApp.Order.Infrastructure.Clients;

public class PaymentHttpClient : IPaymentClient
{
    private readonly HttpClient _http;
    public PaymentHttpClient(HttpClient http) => _http = http;

    public async Task<PaymentLink?> CreatePaymentAsync(string orderId, decimal amount, List<PaymentItem> items)
    {
        var body = new
        {
            orderId,
            amount,
            items = items.Select(i => new { i.Name, i.Quantity, i.Price })
        };
        var response = await _http.PostAsJsonAsync("api/payment/create", body);
        if (!response.IsSuccessStatusCode) return null;
        var dto = await response.Content.ReadFromJsonAsync<PaymentResponse>();
        return dto == null ? null : new PaymentLink(dto.CheckoutUrl);
    }

    private class PaymentResponse
    {
        public string CheckoutUrl { get; set; } = string.Empty;
    }
}
```

- [ ] **Step 8: Chạy test — pass**.

---

### Task 3: Order.API + consumer PaymentSucceeded + gateway + compose

**Files:**
- Order.Infrastructure: `Consumers/PaymentSucceededHandler.cs` (static handler)
- Order.API: `Controllers/OrdersController.cs`, `Program.cs`, `appsettings.json`, `Dockerfile`
- Modify gateway appsettings + docker-compose.

- [ ] **Step 1: Scaffold API + refs** (webapi --use-controllers; ref Core/Infra/BuildingBlocks; package Swashbuckle 6.6.2).

- [ ] **Step 2: OrdersController** — migrate toàn bộ endpoint monolith (`/api/orders`), lấy userId/role từ JWT. Bao gồm:
  - `POST /` (Authorize) CreateOrder → trả 200 với order (kèm PaymentUrl); catch InvalidOperationException → 400.
  - `GET /my` (Authorize) GetMyOrders.
  - `GET /{id}` (Authorize) GetOrderDetail.
  - `POST /{id}/cancel` (Authorize) Cancel.
  - `GET /admin/all` (Authorize Roles=Admin) GetAll.
  - `GET /admin/status/{status}` (Authorize Roles=Admin) GetByStatus.
  - `PUT /admin/{id}/status` (Authorize Roles=Admin) UpdateStatus.
  - `POST /{id}/claim` (Authorize Roles=Shipper) Claim (shipperId từ JWT).
  - `GET /shipper/mine` (Authorize Roles=Shipper) GetShipperOrders.
  - `PUT /shipper/{id}/delivery` (Authorize Roles=Shipper) UpdateDelivery.

```csharp
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
        => await _orderService.CancelOrderAsync(id, UserId()) ? Ok(new { message = "Đã hủy đơn" }) : BadRequest(new { message = "Không thể hủy đơn" });

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
        => await _orderService.ClaimOrderAsync(id, UserId()) ? Ok(new { message = "Đã nhận đơn" }) : BadRequest(new { message = "Đơn không còn khả dụng" });

    [HttpGet("shipper/mine")]
    [Authorize(Roles = "Shipper")]
    public async Task<IActionResult> ShipperMine() => Ok(await _orderService.GetShipperOrdersAsync(UserId()));

    [HttpPut("shipper/{id}/delivery")]
    [Authorize(Roles = "Shipper")]
    public async Task<IActionResult> Delivery(string id, [FromBody] string status)
        => await _orderService.UpdateDeliveryStatusAsync(id, UserId(), status) ? NoContent() : BadRequest();
}
```

- [ ] **Step 3: Program.cs** — DI: MongoContext, OrderDbContext, IOrderService; HttpClient IProductClient (ProductUrl), IPaymentClient (PaymentUrl); event bus publisher; consumer PaymentSucceeded:
```csharp
using PizzaApp.Order.Core.Interfaces;
using PizzaApp.Order.Infrastructure;
using PizzaApp.Order.Infrastructure.Clients;
using PizzaApp.Order.Infrastructure.Services;
using PizzaApp.BuildingBlocks.Auth;
using PizzaApp.BuildingBlocks.Mongo;
using PizzaApp.BuildingBlocks.Messaging;

var builder = WebApplication.CreateBuilder(args);

var mongoSettings = new MongoSettings();
builder.Configuration.GetSection("MongoDB").Bind(mongoSettings);
var jwtSettings = new JwtSettings();
builder.Configuration.GetSection("JwtSettings").Bind(jwtSettings);
var busSettings = new EventBusSettings();
builder.Configuration.GetSection("EventBus").Bind(busSettings);

builder.Services.AddSingleton(mongoSettings);
builder.Services.AddSingleton(jwtSettings);
builder.Services.AddSingleton<MongoContext>();
builder.Services.AddSingleton<OrderDbContext>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddRabbitMqEventBus(busSettings);

var productUrl = builder.Configuration["Services:ProductUrl"] ?? "http://localhost:5002/";
var paymentUrl = builder.Configuration["Services:PaymentUrl"] ?? "http://localhost:5006/";
builder.Services.AddHttpClient<IProductClient, ProductHttpClient>(c => { c.BaseAddress = new Uri(productUrl); c.Timeout = TimeSpan.FromSeconds(5); });
builder.Services.AddHttpClient<IPaymentClient, PaymentHttpClient>(c => { c.BaseAddress = new Uri(paymentUrl); c.Timeout = TimeSpan.FromSeconds(10); });

// Consumer: PaymentSucceeded -> ConfirmPayment
builder.Services.AddRabbitMqConsumer<PaymentSucceededEvent>("order.payment-succeeded", async (sp, evt) =>
{
    var svc = sp.GetRequiredService<IOrderService>();
    await svc.ConfirmPaymentAsync(evt.OrderId);
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

- [ ] **Step 4: appsettings.json** (Mongo `PizzaApp_Order`, JwtSettings, `Services:ProductUrl`=5002, `Services:PaymentUrl`=5006, `EventBus:HostName`=localhost).

- [ ] **Step 5: Dockerfile** (COPY BuildingBlocks + Services/Order).

- [ ] **Step 6: Gateway route** `/api/orders/{**}` → `order-cluster` (`http://localhost:5005/`); compose thêm `order` service (env Mongo `PizzaApp_Order`, `Services__ProductUrl=http://product:8080/`, `Services__PaymentUrl=http://payment:8080/`, `EventBus__HostName=rabbitmq`), thêm `EventBus__HostName=rabbitmq` cho service khác cần; gateway route env.

- [ ] **Step 7: Build + test toàn solution**.

---

### Task 4: Payment Service (QR trừu tượng — KHÔNG PayOS)

**Files:**
- Payment.Core: `Entities/Payment.cs`, `DTOs/PaymentDtos.cs`, `Interfaces/{IPaymentService,IPaymentGateway}.cs`
- Payment.Infrastructure: `PaymentDbContext.cs`, `Services/PaymentService.cs`, `Gateways/MockPaymentGateway.cs`
- Payment.API: `Controllers/PaymentController.cs`, `Program.cs`, `appsettings.json`, `Dockerfile`
- Test: `tests/PizzaApp.Payment.Tests/PaymentServiceTests.cs`

**Interfaces:**
- `record PaymentCreation(string CheckoutUrl, string QrCodeDataUri)`
- `IPaymentGateway { PaymentCreation CreateQr(string paymentCode, decimal amount, string confirmUrl); }` — `MockPaymentGateway` render QR bằng QRCoder.
- `IPaymentService.CreatePaymentAsync(CreatePaymentDto) : PaymentCreation`; `ConfirmAsync(string code) : bool` (mark PAID + publish PaymentSucceeded); `GetByOrderAsync(orderId) : PaymentView?`.
- `PaymentService(PaymentDbContext db, IPaymentGateway gateway, IEventBus bus, PaymentSettings settings)`.

- [ ] **Step 1: Scaffold + refs**

```bash
dotnet new classlib -n PizzaApp.Payment.Core -o src/Services/Payment/PizzaApp.Payment.Core -f net8.0
dotnet new classlib -n PizzaApp.Payment.Infrastructure -o src/Services/Payment/PizzaApp.Payment.Infrastructure -f net8.0
dotnet new xunit -n PizzaApp.Payment.Tests -o tests/PizzaApp.Payment.Tests -f net8.0
rm -f src/Services/Payment/PizzaApp.Payment.Core/Class1.cs src/Services/Payment/PizzaApp.Payment.Infrastructure/Class1.cs tests/PizzaApp.Payment.Tests/UnitTest1.cs
dotnet sln add src/Services/Payment/PizzaApp.Payment.Core/PizzaApp.Payment.Core.csproj src/Services/Payment/PizzaApp.Payment.Infrastructure/PizzaApp.Payment.Infrastructure.csproj tests/PizzaApp.Payment.Tests/PizzaApp.Payment.Tests.csproj
dotnet add src/Services/Payment/PizzaApp.Payment.Infrastructure reference src/Services/Payment/PizzaApp.Payment.Core/PizzaApp.Payment.Core.csproj src/BuildingBlocks/PizzaApp.BuildingBlocks.csproj
dotnet add src/Services/Payment/PizzaApp.Payment.Infrastructure package MongoDB.Driver -v 2.28.0
dotnet add src/Services/Payment/PizzaApp.Payment.Infrastructure package QRCoder -v 1.6.0
dotnet add tests/PizzaApp.Payment.Tests reference src/Services/Payment/PizzaApp.Payment.Infrastructure/PizzaApp.Payment.Infrastructure.csproj src/Services/Payment/PizzaApp.Payment.Core/PizzaApp.Payment.Core.csproj
```

- [ ] **Step 2: Payment.Core**

`Entities/Payment.cs` (alias `PaymentEntity` ở Infra):
```csharp
namespace PizzaApp.Payment.Core.Entities;

public class Payment
{
    public string Id { get; set; } = string.Empty;
    public string OrderId { get; set; } = string.Empty;
    public string PaymentCode { get; set; } = string.Empty; // mã trong URL confirm/QR
    public decimal Amount { get; set; }
    public string Status { get; set; } = "PENDING"; // PENDING, PAID
    public string CheckoutUrl { get; set; } = string.Empty;
    public string QrCodeDataUri { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
```

`DTOs/PaymentDtos.cs`:
```csharp
namespace PizzaApp.Payment.Core.DTOs;

public class CreatePaymentDto
{
    public string OrderId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public List<PaymentItemDto> Items { get; set; } = new();
}

public class PaymentItemDto
{
    public string Name { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public int Price { get; set; }
}

public class PaymentView
{
    public string OrderId { get; set; } = string.Empty;
    public string CheckoutUrl { get; set; } = string.Empty;
    public string QrCodeDataUri { get; set; } = string.Empty;
    public string Status { get; set; } = "PENDING";
}
```

`Interfaces/IPaymentGateway.cs`:
```csharp
namespace PizzaApp.Payment.Core.Interfaces;

public record PaymentCreation(string CheckoutUrl, string QrCodeDataUri);

public interface IPaymentGateway
{
    /// <summary>Sinh URL xác nhận + ảnh QR (data URI). Trừu tượng — mock hoặc cổng thật.</summary>
    PaymentCreation CreateQr(string paymentCode, decimal amount, string confirmUrl);
}
```

`Interfaces/IPaymentService.cs`:
```csharp
using PizzaApp.Payment.Core.DTOs;

namespace PizzaApp.Payment.Core.Interfaces;

public interface IPaymentService
{
    Task<PaymentCreation> CreatePaymentAsync(CreatePaymentDto dto);
    Task<bool> ConfirmAsync(string paymentCode); // quét QR -> mark PAID + publish PaymentSucceeded
    Task<PaymentView?> GetByOrderAsync(string orderId);
}
```

`PaymentSettings.cs` (Core):
```csharp
namespace PizzaApp.Payment.Core;

public class PaymentSettings
{
    // IP LAN của máy để điện thoại quét QR gọi vào (KHÔNG localhost). Vd http://192.168.1.10:8080
    public string PublicBaseUrl { get; set; } = "http://localhost:8080";
}
```

- [ ] **Step 3: PaymentDbContext** (alias `PaymentEntity`, id string generator — theo mẫu các service trước).

- [ ] **Step 4: MockPaymentGateway** (QRCoder)

`Gateways/MockPaymentGateway.cs`:
```csharp
using QRCoder;
using PizzaApp.Payment.Core.Interfaces;

namespace PizzaApp.Payment.Infrastructure.Gateways;

public class MockPaymentGateway : IPaymentGateway
{
    public PaymentCreation CreateQr(string paymentCode, decimal amount, string confirmUrl)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(confirmUrl, QRCodeGenerator.ECCLevel.Q);
        var png = new PngByteQRCode(data).GetGraphic(20);
        var dataUri = "data:image/png;base64," + Convert.ToBase64String(png);
        return new PaymentCreation(confirmUrl, dataUri);
    }
}
```

- [ ] **Step 5: Failing test** `tests/PizzaApp.Payment.Tests/PaymentServiceTests.cs`:
```csharp
using PizzaApp.Payment.Infrastructure.Gateways;
using Xunit;

namespace PizzaApp.Payment.Tests;

public class PaymentServiceTests
{
    [Fact]
    public void MockGateway_ProducesScannableQrDataUri()
    {
        var gw = new MockPaymentGateway();

        var result = gw.CreateQr("code123", 50000m, "http://192.168.1.10:8080/api/payment/confirm/code123");

        Assert.Equal("http://192.168.1.10:8080/api/payment/confirm/code123", result.CheckoutUrl);
        Assert.StartsWith("data:image/png;base64,", result.QrCodeDataUri);
        Assert.True(result.QrCodeDataUri.Length > 100); // có nội dung ảnh
    }
}
```

- [ ] **Step 6: Chạy test — fail** rồi viết **PaymentService**:

`Services/PaymentService.cs`:
```csharp
using MongoDB.Driver;
using PizzaApp.Payment.Core;
using PizzaApp.Payment.Core.DTOs;
using PizzaApp.Payment.Core.Interfaces;
using PizzaApp.BuildingBlocks.Messaging;
using PaymentEntity = PizzaApp.Payment.Core.Entities.Payment;

namespace PizzaApp.Payment.Infrastructure.Services;

public class PaymentService : IPaymentService
{
    private readonly IMongoCollection<PaymentEntity> _payments;
    private readonly IPaymentGateway _gateway;
    private readonly IEventBus _bus;
    private readonly PaymentSettings _settings;

    public PaymentService(PaymentDbContext db, IPaymentGateway gateway, IEventBus bus, PaymentSettings settings)
    {
        _payments = db.Payments;
        _gateway = gateway;
        _bus = bus;
        _settings = settings;
    }

    public async Task<PaymentCreation> CreatePaymentAsync(CreatePaymentDto dto)
    {
        var code = Guid.NewGuid().ToString("N");
        var confirmUrl = $"{_settings.PublicBaseUrl.TrimEnd('/')}/api/payment/confirm/{code}";
        var creation = _gateway.CreateQr(code, dto.Amount, confirmUrl);

        var payment = new PaymentEntity
        {
            OrderId = dto.OrderId,
            PaymentCode = code,
            Amount = dto.Amount,
            Status = "PENDING",
            CheckoutUrl = creation.CheckoutUrl,
            QrCodeDataUri = creation.QrCodeDataUri
        };
        await _payments.InsertOneAsync(payment);
        return creation;
    }

    public async Task<bool> ConfirmAsync(string paymentCode)
    {
        var payment = await _payments.Find(p => p.PaymentCode == paymentCode).FirstOrDefaultAsync();
        if (payment == null) return false;
        if (payment.Status == "PAID") return true; // idempotent

        await _payments.UpdateOneAsync(p => p.Id == payment.Id,
            Builders<PaymentEntity>.Update.Set(p => p.Status, "PAID"));

        _bus.Publish(new PaymentSucceededEvent(payment.OrderId));
        return true;
    }

    public async Task<PaymentView?> GetByOrderAsync(string orderId)
    {
        var p = await _payments.Find(x => x.OrderId == orderId).FirstOrDefaultAsync();
        return p == null ? null : new PaymentView
        {
            OrderId = p.OrderId, CheckoutUrl = p.CheckoutUrl,
            QrCodeDataUri = p.QrCodeDataUri, Status = p.Status
        };
    }
}
```

- [ ] **Step 7: PaymentController**:
```csharp
using Microsoft.AspNetCore.Mvc;
using PizzaApp.Payment.Core.DTOs;
using PizzaApp.Payment.Core.Interfaces;

namespace PizzaApp.Payment.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class PaymentController : ControllerBase
{
    private readonly IPaymentService _paymentService;
    public PaymentController(IPaymentService paymentService) => _paymentService = paymentService;

    // Gọi nội bộ bởi Order service (REST).
    [HttpPost("create")]
    public async Task<IActionResult> Create([FromBody] CreatePaymentDto dto)
    {
        var creation = await _paymentService.CreatePaymentAsync(dto);
        return Ok(new { checkoutUrl = creation.CheckoutUrl, qrCode = creation.QrCodeDataUri });
    }

    // Quét QR mở URL này (GET để camera điện thoại mở trực tiếp).
    [HttpGet("confirm/{code}")]
    public async Task<IActionResult> Confirm(string code)
    {
        var ok = await _paymentService.ConfirmAsync(code);
        var html = ok
            ? "<html><body style='font-family:sans-serif;text-align:center;padding-top:40px'><h1>✅ Thanh toán thành công</h1><p>Bạn có thể quay lại ứng dụng.</p></body></html>"
            : "<html><body style='font-family:sans-serif;text-align:center;padding-top:40px'><h1>❌ Không tìm thấy giao dịch</h1></body></html>";
        return Content(html, "text/html");
    }

    [HttpGet("order/{orderId}")]
    public async Task<IActionResult> GetByOrder(string orderId)
    {
        var view = await _paymentService.GetByOrderAsync(orderId);
        return view == null ? NotFound() : Ok(view);
    }
}
```

- [ ] **Step 8: Program.cs** — DI Mongo + PaymentDbContext + IPaymentGateway→MockPaymentGateway + PaymentSettings (bind `Payment`) + event bus publisher + IPaymentService + JWT + controllers/swagger/cors:
```csharp
using PizzaApp.Payment.Core;
using PizzaApp.Payment.Core.Interfaces;
using PizzaApp.Payment.Infrastructure;
using PizzaApp.Payment.Infrastructure.Gateways;
using PizzaApp.Payment.Infrastructure.Services;
using PizzaApp.BuildingBlocks.Auth;
using PizzaApp.BuildingBlocks.Mongo;
using PizzaApp.BuildingBlocks.Messaging;

var builder = WebApplication.CreateBuilder(args);

var mongoSettings = new MongoSettings();
builder.Configuration.GetSection("MongoDB").Bind(mongoSettings);
var jwtSettings = new JwtSettings();
builder.Configuration.GetSection("JwtSettings").Bind(jwtSettings);
var busSettings = new EventBusSettings();
builder.Configuration.GetSection("EventBus").Bind(busSettings);
var paymentSettings = new PaymentSettings();
builder.Configuration.GetSection("Payment").Bind(paymentSettings);

builder.Services.AddSingleton(mongoSettings);
builder.Services.AddSingleton(jwtSettings);
builder.Services.AddSingleton(paymentSettings);
builder.Services.AddSingleton<MongoContext>();
builder.Services.AddSingleton<PaymentDbContext>();
builder.Services.AddSingleton<IPaymentGateway, MockPaymentGateway>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddRabbitMqEventBus(busSettings);

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

- [ ] **Step 9: appsettings.json** (Mongo `PizzaApp_Payment`, JwtSettings, `"Payment": { "PublicBaseUrl": "http://localhost:8080" }`, `"EventBus": { "HostName": "localhost" }`).

- [ ] **Step 10: Dockerfile** (COPY BuildingBlocks + Services/Payment).

- [ ] **Step 11: Gateway route** `/api/payment/{**}` → `payment-cluster` (5006). compose thêm `payment` service (env Mongo `PizzaApp_Payment`, `EventBus__HostName=rabbitmq`, `Payment__PublicBaseUrl=${PUBLIC_BASE_URL}`). Thêm `PUBLIC_BASE_URL=http://<IP-LAN>:8080` vào `.env.example`.

- [ ] **Step 12: Build + test toàn solution**.

---

### Task 5: Cart consumer OrderCreated (xóa giỏ)

**Files:**
- Cart.Infrastructure: thêm package/ref BuildingBlocks đã có; `Services/CartService.cs` thêm method `ClearCartAsync` (đã có).
- Cart.API `Program.cs`: thêm `AddRabbitMqEventBus` + `AddRabbitMqConsumer<OrderCreatedEvent>` → gọi `ICartService.ClearCartAsync(evt.UserId)`.
- compose: thêm `EventBus__HostName=rabbitmq` cho `cart`.

- [ ] **Step 1:** Cart.API `Program.cs` thêm:
```csharp
using PizzaApp.BuildingBlocks.Messaging;
// ...
var busSettings = new EventBusSettings();
builder.Configuration.GetSection("EventBus").Bind(busSettings);
builder.Services.AddRabbitMqEventBus(busSettings);
builder.Services.AddRabbitMqConsumer<OrderCreatedEvent>("cart.order-created", async (sp, evt) =>
{
    var svc = sp.GetRequiredService<ICartService>();
    await svc.ClearCartAsync(evt.UserId);
});
```
- [ ] **Step 2:** Cart `appsettings.json` thêm `"EventBus": { "HostName": "localhost" }`; compose `cart.environment` thêm `EventBus__HostName=rabbitmq`.
- [ ] **Step 3:** Build + test toàn solution = 0 error, tất cả pass.
- [ ] **Step 4:** Boot-check DI cho Order.API, Payment.API, Cart.API (consumer retry kết nối, app vẫn "Application started" dù chưa có RabbitMQ).

---

## Self-Review

**Spec coverage:** Order + Payment thành service riêng (DB `PizzaApp_Order`/`PizzaApp_Payment`) ✓; REST đồng bộ Order→Product, Order→Payment ✓; event bất đồng bộ OrderCreated (Cart xóa giỏ) + PaymentSucceeded (Order mark Paid) ✓; RabbitMQ event bus trong BuildingBlocks ✓; PayOS webhook ở Payment ✓; idempotent (ConfirmPayment chỉ đổi khi `PaymentStatus==Unpaid`) ✓.

**Deviation từ spec:** Chọn Approach B (Order điều phối tạo payment trong 1 lời gọi) thay vì 2 lời gọi tách — để giảm thay đổi Flutter, giữ UX monolith. Vẫn giữ tính event-driven cho 2 sự kiện.

**Placeholder scan:** Không TODO/TBD; code đầy đủ.

**Type consistency:** `IEventBus.Publish<T>` + `EventRouting.RoutingKeyFor<T>` dùng nhất quán; `OrderCreatedEvent`/`PaymentSucceededEvent` dùng ở publisher (Order/Payment) và consumer (Cart/Order) — khớp. `IPaymentClient.CreatePaymentAsync` (Order) ↔ `POST /api/payment/create` (Payment) khớp field. Alias `OrderEntity`/`PaymentEntity` tránh trùng namespace.

**Rủi ro:**
- PayOS `createPaymentLink`/`verifyPaymentWebhookData` cần credentials thật + mạng → chỉ verify được khi chạy Docker + có key thật. Boot-check chỉ xác nhận DI.
- Webhook cần URL public để PayOS gọi (ngrok/VPS) — test thật ở Plan 5/deploy.
- Consumer dùng scope để resolve service scoped; DbContext là singleton nên an toàn.
