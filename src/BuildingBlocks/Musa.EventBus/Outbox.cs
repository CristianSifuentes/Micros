using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Musa.EventBus;

/// <summary>An integration event waiting in the service's own database, written in the business transaction.</summary>
public sealed class OutboxMessage
{
    public Guid Id { get; private set; }
    public string EventName { get; private set; } = "";
    public string Payload { get; private set; } = "";
    public string? TraceParent { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public DateTimeOffset? PublishedAt { get; private set; }

    private OutboxMessage() { }

    public static OutboxMessage From(IntegrationEvent @event) => new()
    {
        Id = @event.Id,
        EventName = @event.GetType().Name,
        Payload = JsonSerializer.Serialize(@event, @event.GetType(), EventJson.Options),
        TraceParent = Activity.Current?.Id,
        OccurredAt = @event.OccurredAt,
    };

    public void MarkPublished(DateTimeOffset now) => PublishedAt = now;
}

public static class EventJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}

public static class OutboxExtensions
{
    public static void AddOutbox(this ModelBuilder modelBuilder) =>
        modelBuilder.Entity<OutboxMessage>(outbox =>
        {
            outbox.ToTable("outbox");
            outbox.HasKey(m => m.Id);
            outbox.HasIndex(m => m.PublishedAt);
        });

    /// <summary>Stages the event; it is committed by the same SaveChanges as the business change.</summary>
    public static void Enqueue(this DbContext context, IntegrationEvent @event) =>
        context.Set<OutboxMessage>().Add(OutboxMessage.From(@event));
}

/// <summary>
/// Reads the outbox as a queue and publishes to the bus. Delivery is at least once: a crash between
/// publish and mark resends the message, so every consumer must be idempotent.
/// </summary>
public sealed class OutboxPublisher<TContext>(
    IServiceScopeFactory scopes, IEventBus bus, TimeProvider time, ILogger<OutboxPublisher<TContext>> logger)
    : BackgroundService where TContext : DbContext
{
    private static readonly TimeSpan Idle = TimeSpan.FromMilliseconds(400);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var published = 0;
            try
            {
                published = await PublishPendingAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Outbox publishing failed; will retry");
            }

            if (published == 0)
            {
                await Task.Delay(Idle, stoppingToken);
            }
        }
    }

    private async Task<int> PublishPendingAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();

        var pending = await context.Set<OutboxMessage>()
            .Where(m => m.PublishedAt == null)
            .OrderBy(m => m.OccurredAt)
            .Take(50)
            .ToListAsync(ct);

        foreach (var message in pending)
        {
            await bus.PublishAsync(message.EventName, message.Id, message.Payload, message.TraceParent, ct);
            message.MarkPublished(time.GetUtcNow());
        }

        await context.SaveChangesAsync(ct);
        return pending.Count;
    }
}
