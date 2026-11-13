using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Musa.EventBus;

public sealed class EventBusSubscriptions
{
    private readonly Dictionary<string, (Type Event, Type Handler)> _byName = [];

    public string QueueName { get; init; } = "";
    public IReadOnlyDictionary<string, (Type Event, Type Handler)> ByName => _byName;

    internal void Add<TEvent, THandler>() => _byName[typeof(TEvent).Name] = (typeof(TEvent), typeof(THandler));
}

/// <summary>
/// A deliberately small bus: one durable direct exchange, one durable queue per service, routing key = event name.
/// The broker stays dumb; retries beyond the few done here, dead-lettering and sagas belong to a messaging
/// library once the system outgrows this.
/// </summary>
public sealed class RabbitMqEventBus(
    IConnection connection, IServiceScopeFactory scopes, EventBusSubscriptions subscriptions,
    ILogger<RabbitMqEventBus> logger) : IEventBus, IHostedService, IAsyncDisposable
{
    public const string TelemetryName = "Musa.EventBus";
    private const string Exchange = "musa.events";
    private const int MaxAttempts = 3;

    private static readonly ActivitySource Telemetry = new(TelemetryName);
    private readonly SemaphoreSlim _publishLock = new(1, 1);
    private IChannel? _publishChannel;
    private IChannel? _consumeChannel;

    public async Task PublishAsync(string eventName, Guid messageId, string payload, string? traceParent, CancellationToken ct)
    {
        using var activity = Telemetry.StartActivity($"publish {eventName}", ActivityKind.Producer, traceParent);

        await _publishLock.WaitAsync(ct);
        try
        {
            _publishChannel ??= await OpenChannelAsync(ct);
            var properties = new BasicProperties
            {
                MessageId = messageId.ToString(),
                ContentType = "application/json",
                DeliveryMode = DeliveryModes.Persistent,
                Headers = new Dictionary<string, object?> { ["traceparent"] = activity?.Id ?? traceParent },
            };
            await _publishChannel.BasicPublishAsync(Exchange, eventName, mandatory: false, properties,
                Encoding.UTF8.GetBytes(payload), ct);
        }
        finally
        {
            _publishLock.Release();
        }
    }

    public async Task StartAsync(CancellationToken ct)
    {
        if (subscriptions.ByName.Count == 0)
        {
            return;
        }

        _consumeChannel = await OpenChannelAsync(ct);
        await _consumeChannel.QueueDeclareAsync(subscriptions.QueueName, durable: true, exclusive: false,
            autoDelete: false, cancellationToken: ct);
        foreach (var eventName in subscriptions.ByName.Keys)
        {
            await _consumeChannel.QueueBindAsync(subscriptions.QueueName, Exchange, eventName, cancellationToken: ct);
        }

        var consumer = new AsyncEventingBasicConsumer(_consumeChannel);
        consumer.ReceivedAsync += OnReceivedAsync;
        await _consumeChannel.BasicConsumeAsync(subscriptions.QueueName, autoAck: false, consumer, ct);
    }

    private async Task OnReceivedAsync(object sender, BasicDeliverEventArgs delivery)
    {
        var eventName = delivery.RoutingKey;
        var parent = delivery.BasicProperties.Headers?.TryGetValue("traceparent", out var raw) == true && raw is byte[] bytes
            ? Encoding.UTF8.GetString(bytes)
            : null;
        using var activity = Telemetry.StartActivity($"consume {eventName}", ActivityKind.Consumer, parent);

        if (!subscriptions.ByName.TryGetValue(eventName, out var subscription))
        {
            await _consumeChannel!.BasicAckAsync(delivery.DeliveryTag, multiple: false);
            return;
        }

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var @event = JsonSerializer.Deserialize(delivery.Body.Span, subscription.Event, EventJson.Options)!;
                await using var scope = scopes.CreateAsyncScope();
                dynamic handler = scope.ServiceProvider.GetRequiredService(subscription.Handler);
                await handler.HandleAsync((dynamic)@event, CancellationToken.None);
                await _consumeChannel!.BasicAckAsync(delivery.DeliveryTag, multiple: false);
                return;
            }
            catch (Exception ex) when (attempt < MaxAttempts)
            {
                logger.LogWarning(ex, "Handling {EventName} failed (attempt {Attempt}); retrying", eventName, attempt);
                await Task.Delay(TimeSpan.FromMilliseconds(200 * attempt * attempt));
            }
            catch (Exception ex)
            {
                activity?.AddException(ex);
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                logger.LogError(ex, "Giving up on {EventName} {MessageId}", eventName, delivery.BasicProperties.MessageId);
                await _consumeChannel!.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: false);
                return;
            }
        }
    }

    private async Task<IChannel> OpenChannelAsync(CancellationToken ct)
    {
        var channel = await connection.CreateChannelAsync(cancellationToken: ct);
        await channel.ExchangeDeclareAsync(Exchange, ExchangeType.Direct, durable: true, cancellationToken: ct);
        return channel;
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        if (_publishChannel is not null) await _publishChannel.DisposeAsync();
        if (_consumeChannel is not null) await _consumeChannel.DisposeAsync();
        _publishLock.Dispose();
    }
}
