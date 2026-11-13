using Auctions.Domain.AggregatesModel.LotAggregate;
using Auctions.Domain.SeedWork;
using Microsoft.EntityFrameworkCore;
using Musa.EventBus;

namespace Auctions.Infrastructure;

/// <summary>Receives the domain events of a unit of work: once inside the transaction, once after it commits.</summary>
public interface IDomainEventSink
{
    /// <summary>Runs before SaveChanges; anything staged on the context joins the same transaction.</summary>
    void BeforeCommit(IDomainEvent domainEvent, DbContext context);

    /// <summary>Runs after a successful commit; for effects that must never fire for a rolled-back change.</summary>
    void AfterCommit(IDomainEvent domainEvent);
}

public sealed class AuctionsContext(DbContextOptions<AuctionsContext> options, IDomainEventSink sink)
    : DbContext(options), IUnitOfWork
{
    public const string Schema = "auctions";

    public DbSet<Lot> Lots => Set<Lot>();
    public DbSet<ConsignablePiece> Pieces => Set<ConsignablePiece>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AuctionsContext).Assembly);
        modelBuilder.AddOutbox();
    }

    public async Task<bool> SaveEntitiesAsync(CancellationToken cancellationToken = default)
    {
        var entities = ChangeTracker.Entries<Entity>().Select(e => e.Entity).Where(e => e.DomainEvents.Count != 0).ToList();
        var domainEvents = entities.SelectMany(e => e.DomainEvents).ToList();
        entities.ForEach(e => e.ClearDomainEvents());

        foreach (var domainEvent in domainEvents)
        {
            sink.BeforeCommit(domainEvent, this);
        }

        await base.SaveChangesAsync(cancellationToken);

        foreach (var domainEvent in domainEvents)
        {
            sink.AfterCommit(domainEvent);
        }

        return true;
    }
}

/// <summary>What this context needs to know about a catalogue piece, replicated from Catalog's events.</summary>
public sealed class ConsignablePiece
{
    public Guid PieceId { get; set; }
    public required string Title { get; set; }
    public required string Creator { get; set; }
    public decimal? EstimateLow { get; set; }
    public decimal? EstimateHigh { get; set; }
}
