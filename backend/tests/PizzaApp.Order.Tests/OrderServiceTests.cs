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
            PaymentUrl = "http://pay", PaymentQr = "data:image/png;base64,AAA",
            DeliveryAddress = "HN", ShipperId = "s1",
            OrderItems = { new OrderItem { ProductId = "p1", ProductName = "Pizza", Quantity = 2, UnitPrice = 10m, Size = "L" } }
        };

        var dto = OrderService.MapToDto(order);

        Assert.Equal("o1", dto.Id);
        Assert.Equal(20m, dto.TotalPrice);
        Assert.Equal("Paid", dto.Status);
        Assert.Equal("http://pay", dto.PaymentUrl);
        Assert.Equal("data:image/png;base64,AAA", dto.PaymentQr);
        Assert.Single(dto.Items);
        Assert.Equal("Pizza", dto.Items[0].ProductName);
    }
}
