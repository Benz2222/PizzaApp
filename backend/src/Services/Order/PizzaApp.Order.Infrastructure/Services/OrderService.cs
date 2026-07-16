using MongoDB.Bson;
using MongoDB.Driver;
using PizzaApp.Order.Core.DTOs;
using PizzaApp.Order.Core.Interfaces;
using PizzaApp.BuildingBlocks.Messaging;
using PizzaApp.Order.Core.Entities;
using OrderEntity = PizzaApp.Order.Core.Entities.Order;

namespace PizzaApp.Order.Infrastructure.Services;

public class OrderService : IOrderService
{
    private readonly IMongoCollection<OrderEntity> _orders;
    private readonly IProductClient _productClient;
    private readonly IPaymentClient _paymentClient;
    private readonly ICartClient _cartClient;
    private readonly IEventBus _bus;

    public OrderService(OrderDbContext db, IProductClient productClient, IPaymentClient paymentClient, ICartClient cartClient, IEventBus bus)
    {
        _orders = db.Orders;
        _productClient = productClient;
        _paymentClient = paymentClient;
        _cartClient = cartClient;
        _bus = bus;
    }

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
            PaymentMethod = "QR",
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
            throw new InvalidOperationException("Không tạo được thanh toán.");
        }
        order.PaymentUrl = link.CheckoutUrl;
        order.PaymentQr = link.QrCode;
        await _orders.UpdateOneAsync(o => o.Id == order.Id,
            Builders<OrderEntity>.Update
                .Set(o => o.PaymentUrl, link.CheckoutUrl)
                .Set(o => o.PaymentQr, link.QrCode));

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

    public static OrderResultDto MapToDto(OrderEntity o) => new()
    {
        Id = o.Id,
        TotalPrice = o.TotalPrice,
        Status = o.Status,
        PaymentStatus = o.PaymentStatus,
        PaymentUrl = o.PaymentUrl,
        PaymentQr = o.PaymentQr,
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
