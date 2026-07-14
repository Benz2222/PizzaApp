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
