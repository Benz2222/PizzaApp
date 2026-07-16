using PizzaApp.Product.Infrastructure.Services;
using Xunit;
using ProductEntity = PizzaApp.Product.Core.Entities.Product;

namespace PizzaApp.Product.Tests;

public class ProductServiceTests
{
    [Fact]
    public void MapToDto_CopiesAllFieldsIncludingCategoryName()
    {
        var p = new ProductEntity
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
