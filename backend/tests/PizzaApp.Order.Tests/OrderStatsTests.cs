using PizzaApp.Order.Infrastructure.Services;
using Xunit;

namespace PizzaApp.Order.Tests;

public class OrderStatsTests
{
    [Fact]
    public void NormalizeByStatus_DienDu7TrangThai_ThieuThiBang0()
    {
        var raw = new Dictionary<string, int> { ["Paid"] = 5, ["Done"] = 30 };

        var result = OrderService.NormalizeByStatus(raw);

        Assert.Equal(7, result.Count);
        Assert.Equal(5, result["Paid"]);
        Assert.Equal(30, result["Done"]);
        Assert.Equal(0, result["AwaitingPayment"]);
        Assert.Equal(0, result["Preparing"]);
        Assert.Equal(0, result["Ready"]);
        Assert.Equal(0, result["Delivering"]);
        Assert.Equal(0, result["Cancelled"]);
    }

    [Fact]
    public void NormalizeByStatus_BoQuaTrangThaiLa()
    {
        var raw = new Dictionary<string, int> { ["Paid"] = 2, ["TrangThaiLa"] = 99 };

        var result = OrderService.NormalizeByStatus(raw);

        Assert.Equal(7, result.Count);
        Assert.False(result.ContainsKey("TrangThaiLa"));
        Assert.Equal(2, result["Paid"]);
    }

    [Fact]
    public void NormalizeByStatus_RongThiTatCaBang0()
    {
        var result = OrderService.NormalizeByStatus(new Dictionary<string, int>());

        Assert.Equal(7, result.Count);
        Assert.All(result.Values, v => Assert.Equal(0, v));
    }
}
