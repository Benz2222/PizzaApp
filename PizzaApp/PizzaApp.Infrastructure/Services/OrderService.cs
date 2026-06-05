using Microsoft.EntityFrameworkCore;
using PizzaApp.Core.DTOs.Order;
using PizzaApp.Core.Entities;
using PizzaApp.Core.Interfaces;
using PizzaApp.Infrastructure.Data;

namespace PizzaApp.Infrastructure.Services;

public class OrderService : IOrderService
{
    private readonly AppDbContext _db;

    public OrderService(AppDbContext db) => _db = db;

    public async Task<int> CreateOrderAsync(int userId, CreateOrderDto dto)
    {
        var order = new Order
        {
            UserId = userId,
            DeliveryAddress = dto.DeliveryAddress,
            Status = "Pending"
        };

        decimal total = 0;

        foreach (var item in dto.Items)
        {
            var product = await _db.Products.FindAsync(item.ProductId);
            if (product == null) continue;

            order.OrderItems.Add(new OrderItem
            {
                ProductId = item.ProductId,
                Quantity = item.Quantity,
                UnitPrice = product.Price,
                Size = item.Size
            });

            total += product.Price * item.Quantity;
        }

        order.TotalPrice = total;

        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        return order.Id;
    }

    public async Task<List<OrderResultDto>> GetMyOrdersAsync(int userId)
    {
        return await _db.Orders
            .Where(o => o.UserId == userId)
            .Include(o => o.OrderItems)
            .OrderByDescending(o => o.CreatedAt)
            .Select(o => new OrderResultDto
            {
                Id = o.Id,
                TotalPrice = o.TotalPrice,
                Status = o.Status,
                CreatedAt = o.CreatedAt,
                Items = o.OrderItems.Select(i => new OrderItemDto
                {
                    ProductId = i.ProductId,
                    Quantity = i.Quantity,
                    Size = i.Size
                }).ToList()
            })
            .ToListAsync();
    }
}