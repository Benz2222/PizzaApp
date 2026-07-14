namespace PizzaApp.BuildingBlocks.Messaging;

public interface IEventBus
{
    void Publish<T>(T @event);
}
