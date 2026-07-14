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
