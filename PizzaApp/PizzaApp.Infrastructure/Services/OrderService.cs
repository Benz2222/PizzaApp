using MongoDB.Driver;
using PizzaApp.Core.DTOs.Order;
using PizzaApp.Core.Entities;
using PizzaApp.Core.Interfaces;
using PizzaApp.Infrastructure.Data;
using Net.payOS;
using Net.payOS.Types;

namespace PizzaApp.Infrastructure.Services;

public class OrderService : IOrderService
{
    private readonly IMongoCollection<Order> _orders;
    private readonly IMongoCollection<Product> _products;
    private readonly IMongoCollection<Payment> _payments;
    private readonly PayOS _payOS;

    public OrderService(MongoDbService mongoDb, PayOS payOS)
    {
        _orders = mongoDb.GetCollection<Order>("Orders");
        _products = mongoDb.GetCollection<Product>("Products");
        _payments = mongoDb.GetCollection<Payment>("Payments");
        _payOS = payOS;
    }

    public async Task<string> CreateOrderAsync(string userId, CreateOrderDto dto)
    {
        if (dto.Items == null || !dto.Items.Any())
            throw new System.Exception("Giỏ hàng không có sản phẩm nào.");

        var order = new Order
        {
            UserId = userId,
            DeliveryAddress = dto.DeliveryAddress,
            Status = "AwaitingPayment",
            PaymentStatus = "Unpaid",
            PaymentMethod = "PayOS",
            CreatedAt = System.DateTime.UtcNow
        };

        decimal total = 0;
        var payOSItems = new System.Collections.Generic.List<ItemData>();

        foreach (var item in dto.Items)
        {
            var product = await _products.Find(p => p.Id == item.ProductId).FirstOrDefaultAsync();
            if (product == null) continue;

            order.OrderItems.Add(new OrderItem
            {
                ProductId = item.ProductId,
                ProductName = product.Name,
                ProductImageUrl = product.ImageUrl,
                Quantity = item.Quantity,
                UnitPrice = product.Price,
                Size = item.Size
            });
            total += product.Price * item.Quantity;

            payOSItems.Add(new ItemData(product.Name, item.Quantity, (int)product.Price));
        }

        // Không cho tạo đơn rỗng (vd tất cả ProductId đều không hợp lệ)
        if (!order.OrderItems.Any())
            throw new System.Exception("Không có sản phẩm hợp lệ trong đơn hàng.");

        order.TotalPrice = total;
        await _orders.InsertOneAsync(order);

        try
        {
            // orderCode phải DUY NHẤT với PayOS -> dùng mốc thời gian mili-giây thay vì giây
            long orderCode = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            var paymentData = new PaymentData(
                orderCode: orderCode,
                amount: (int)total,
                description: "Thanh toan Pizza",
                items: payOSItems,
                returnUrl: "https://localhost:7211/api/Orders/my",
                cancelUrl: "https://localhost:7211/api/Cart"
            );

            CreatePaymentResult paymentLink = await _payOS.createPaymentLink(paymentData);

            var payment = new Payment
            {
                OrderId = order.Id,
                PayOSOrderCode = orderCode,
                Amount = total,
                Status = "PENDING",
                CheckoutUrl = paymentLink.checkoutUrl
            };
            await _payments.InsertOneAsync(payment);
        }
        catch (System.Exception ex)
        {
            // PayOS lỗi -> xóa đơn vừa tạo để không còn đơn rác, rồi báo lỗi ra ngoài
            // (Controller sẽ trả lỗi và KHÔNG xóa giỏ hàng của khách)
            await _orders.DeleteOneAsync(o => o.Id == order.Id);
            throw new System.Exception("Không tạo được link thanh toán PayOS: " + ex.Message);
        }

        return order.Id;
    }

    public async Task<bool> ConfirmPaymentAsync(string orderId)
    {
        await _payments.UpdateOneAsync(
            p => p.OrderId == orderId,
            Builders<Payment>.Update.Set(p => p.Status, "PAID")
        );

        var result = await _orders.UpdateOneAsync(
            o => o.Id == orderId && o.PaymentStatus == "Unpaid",
            Builders<Order>.Update
                .Set(o => o.PaymentStatus, "Paid")
                .Set(o => o.Status, "Paid") // đã thanh toán -> chờ admin xử lý
        );
        return result.ModifiedCount > 0;
    }

    public async Task<List<OrderResultDto>> GetMyOrdersAsync(string userId)
    {
        var orders = await _orders.Find(o => o.UserId == userId).SortByDescending(o => o.CreatedAt).ToListAsync();
        var dtos = new List<OrderResultDto>();
        foreach (var o in orders)
        {
            var p = await _payments.Find(pay => pay.OrderId == o.Id).FirstOrDefaultAsync();
            var dto = MapToDto(o);
            if (p != null && o.PaymentStatus == "Unpaid") dto.PaymentUrl = p.CheckoutUrl;
            dtos.Add(dto);
        }
        return dtos;
    }

    public async Task<OrderResultDto?> GetOrderDetailAsync(string orderId, string userId)
    {
        var order = await _orders.Find(o => o.Id == orderId && o.UserId == userId).FirstOrDefaultAsync();
        if (order == null) return null;
        var p = await _payments.Find(pay => pay.OrderId == order.Id).FirstOrDefaultAsync();
        var dto = MapToDto(order);
        if (p != null && order.PaymentStatus == "Unpaid") dto.PaymentUrl = p.CheckoutUrl;
        return dto;
    }

    public async Task<bool> CancelOrderAsync(string orderId, string userId)
    {
        var result = await _orders.UpdateOneAsync(
            o => o.Id == orderId && o.UserId == userId && o.Status == "AwaitingPayment",
            Builders<Order>.Update.Set(o => o.Status, "Cancelled")
        );
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
        var result = await _orders.UpdateOneAsync(o => o.Id == orderId, Builders<Order>.Update.Set(o => o.Status, status));
        return result.ModifiedCount > 0;
    }

    // Shipper nhận đơn "Ready" -> "Delivering" + gán ShipperId (atomic, tránh 2 shipper nhận cùng lúc)
    public async Task<bool> ClaimOrderAsync(string orderId, string shipperId)
    {
        var result = await _orders.UpdateOneAsync(
            o => o.Id == orderId && o.Status == "Ready" && o.ShipperId == "",
            Builders<Order>.Update
                .Set(o => o.Status, "Delivering")
                .Set(o => o.ShipperId, shipperId)
        );
        return result.ModifiedCount > 0;
    }

    // Đơn của shipper này (đang giao / đã giao)
    public async Task<List<OrderResultDto>> GetShipperOrdersAsync(string shipperId)
    {
        var orders = await _orders.Find(o => o.ShipperId == shipperId)
            .SortByDescending(o => o.CreatedAt).ToListAsync();
        return orders.Select(MapToDto).ToList();
    }

    // Shipper đổi trạng thái đơn của mình: Delivering -> Done / Cancelled
    public async Task<bool> UpdateDeliveryStatusAsync(string orderId, string shipperId, string status)
    {
        if (status != "Done" && status != "Cancelled") return false;
        var result = await _orders.UpdateOneAsync(
            o => o.Id == orderId && o.ShipperId == shipperId && o.Status == "Delivering",
            Builders<Order>.Update.Set(o => o.Status, status)
        );
        return result.ModifiedCount > 0;
    }

    private static OrderResultDto MapToDto(Order o)
    {
        return new OrderResultDto
        {
            Id = o.Id,
            TotalPrice = o.TotalPrice,
            Status = o.Status,
            PaymentStatus = o.PaymentStatus,
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
}
