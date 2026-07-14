using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace PizzaApp.BuildingBlocks.Messaging;

public class RabbitMqConsumerService<T> : BackgroundService
{
    private readonly EventBusSettings _settings;
    private readonly IServiceProvider _services;
    private readonly Func<IServiceProvider, T, Task> _handler;
    private readonly string _queueName;

    public RabbitMqConsumerService(EventBusSettings settings, IServiceProvider services,
        Func<IServiceProvider, T, Task> handler, string queueName)
    {
        _settings = settings;
        _services = services;
        _handler = handler;
        _queueName = queueName;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Retry kết nối tới khi broker sẵn sàng (không crash app nếu RabbitMQ chưa lên).
        IConnection? connection = null;
        IModel? channel = null;
        while (!stoppingToken.IsCancellationRequested && channel == null)
        {
            try
            {
                var factory = new ConnectionFactory { HostName = _settings.HostName, DispatchConsumersAsync = false };
                connection = factory.CreateConnection();
                channel = connection.CreateModel();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Consumer {_queueName}] chờ RabbitMQ: {ex.Message}");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
        if (channel == null) return;

        channel.ExchangeDeclare(_settings.ExchangeName, ExchangeType.Topic, durable: true);
        channel.QueueDeclare(_queueName, durable: true, exclusive: false, autoDelete: false);
        channel.QueueBind(_queueName, _settings.ExchangeName, EventRouting.RoutingKeyFor<T>());

        var consumer = new EventingBasicConsumer(channel);
        consumer.Received += async (_, ea) =>
        {
            try
            {
                var json = Encoding.UTF8.GetString(ea.Body.ToArray());
                var evt = JsonSerializer.Deserialize<T>(json);
                if (evt != null)
                {
                    using var scope = _services.CreateScope();
                    await _handler(scope.ServiceProvider, evt);
                }
                channel.BasicAck(ea.DeliveryTag, false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Consumer {_queueName}] xử lý lỗi: {ex.Message}");
                channel.BasicNack(ea.DeliveryTag, false, requeue: false);
            }
        };
        channel.BasicConsume(_queueName, autoAck: false, consumer);

        try { await Task.Delay(Timeout.Infinite, stoppingToken); }
        catch (TaskCanceledException) { }

        channel.Dispose();
        connection?.Dispose();
    }
}
