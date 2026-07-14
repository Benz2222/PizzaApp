namespace PizzaApp.BuildingBlocks.Messaging;

public record OrderCreatedEvent(string OrderId, string UserId, decimal TotalPrice);
public record PaymentSucceededEvent(string OrderId);
