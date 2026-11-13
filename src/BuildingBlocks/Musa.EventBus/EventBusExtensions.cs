using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Musa.EventBus;

public sealed class EventBusBuilder(IServiceCollection services, EventBusSubscriptions subscriptions)
{
    public EventBusBuilder Subscribe<TEvent, THandler>()
        where TEvent : IntegrationEvent
        where THandler : class, IIntegrationEventHandler<TEvent>
    {
        subscriptions.Add<TEvent, THandler>();
        services.AddScoped<THandler>();
        return this;
    }

    /// <summary>Publishes this context's outbox table in the background.</summary>
    public EventBusBuilder WithOutbox<TContext>() where TContext : DbContext
    {
        services.AddHostedService<OutboxPublisher<TContext>>();
        return this;
    }
}

public static class EventBusExtensions
{
    /// <param name="connectionName">Aspire resource name of the broker.</param>
    /// <param name="queueName">This service's queue; one per bounded context.</param>
    public static EventBusBuilder AddEventBus(this IHostApplicationBuilder builder, string connectionName, string queueName)
    {
        builder.AddRabbitMQClient(connectionName);

        var subscriptions = new EventBusSubscriptions { QueueName = queueName };
        builder.Services.AddSingleton(subscriptions);
        builder.Services.TryAddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<RabbitMqEventBus>();
        builder.Services.AddSingleton<IEventBus>(sp => sp.GetRequiredService<RabbitMqEventBus>());
        builder.Services.AddHostedService(sp => sp.GetRequiredService<RabbitMqEventBus>());

        return new EventBusBuilder(builder.Services, subscriptions);
    }
}
