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
