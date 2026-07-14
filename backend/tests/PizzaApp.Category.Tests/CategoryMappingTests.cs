using PizzaApp.Category.Infrastructure.Services;
using Xunit;
using CategoryEntity = PizzaApp.Category.Core.Entities.Category;

namespace PizzaApp.Category.Tests;

public class CategoryMappingTests
{
    [Fact]
    public void ToDto_MapsIdAndName()
    {
        var entity = new CategoryEntity { Id = "c1", Name = "Pizza" };

        var dto = CategoryService.ToDto(entity);

        Assert.Equal("c1", dto.Id);
        Assert.Equal("Pizza", dto.Name);
    }
}
