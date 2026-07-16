namespace PizzaApp.BuildingBlocks.Messaging;

public class EventBusSettings
{
    public string HostName { get; set; } = "localhost";
    public string ExchangeName { get; set; } = "pizzaapp.events";
}
