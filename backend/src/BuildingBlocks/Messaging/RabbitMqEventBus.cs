using System.Text;
using System.Text.Json;
using RabbitMQ.Client;

namespace PizzaApp.BuildingBlocks.Messaging;

public static class EventRouting
{
    public static string RoutingKeyFor<T>() => typeof(T).Name;
}

public class RabbitMqEventBus : IEventBus, IDisposable
{
    private readonly EventBusSettings _settings;
    private IConnection? _connection;
    private IModel? _channel;
    private readonly object _lock = new();

    public RabbitMqEventBus(EventBusSettings settings) => _settings = settings;

    private void EnsureChannel()
    {
        if (_channel is { IsOpen: true }) return;
        lock (_lock)
        {
            if (_channel is { IsOpen: true }) return;
            var factory = new ConnectionFactory { HostName = _settings.HostName, DispatchConsumersAsync = false };
            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();
            _channel.ExchangeDeclare(_settings.ExchangeName, ExchangeType.Topic, durable: true);
        }
    }

    public void Publish<T>(T @event)
    {
        try
        {
            EnsureChannel();
            var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(@event));
            var props = _channel!.CreateBasicProperties();
            props.ContentType = "application/json";
            props.DeliveryMode = 2; // persistent
            _channel.BasicPublish(_settings.ExchangeName, EventRouting.RoutingKeyFor<T>(), props, body);
        }
        catch (Exception ex)
        {
            // Không làm chết request nếu broker tạm sự cố — chỉ log ra console.
            Console.WriteLine($"[EventBus] Publish {typeof(T).Name} thất bại: {ex.Message}");
        }
    }

    public void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
    }
}
