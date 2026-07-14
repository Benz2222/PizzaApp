using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace PizzaApp.BuildingBlocks.Messaging;

public static class MessagingExtensions
{
    public static IServiceCollection AddRabbitMqEventBus(this IServiceCollection services, EventBusSettings settings)
    {
        services.AddSingleton(settings);
        services.AddSingleton<IEventBus, RabbitMqEventBus>();
        return services;
    }

    public static IServiceCollection AddRabbitMqConsumer<T>(this IServiceCollection services,
        string queueName, Func<IServiceProvider, T, Task> handler)
    {
        services.AddSingleton<IHostedService>(sp =>
            new RabbitMqConsumerService<T>(
                sp.GetRequiredService<EventBusSettings>(), sp, handler, queueName));
        return services;
    }
}
