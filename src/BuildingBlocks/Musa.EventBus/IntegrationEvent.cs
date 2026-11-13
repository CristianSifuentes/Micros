namespace Musa.EventBus;

/// <summary>
/// A fact published to other services. The contract is the event name (the record's type name) plus its
/// JSON shape. Every service declares its own records; nothing here is shared between bounded contexts.
/// </summary>
public abstract record IntegrationEvent
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}

public interface IIntegrationEventHandler<in TEvent> where TEvent : IntegrationEvent
{
    Task HandleAsync(TEvent @event, CancellationToken cancellationToken);
}

public interface IEventBus
{
    Task PublishAsync(string eventName, Guid messageId, string payload, string? traceParent, CancellationToken cancellationToken);
}
