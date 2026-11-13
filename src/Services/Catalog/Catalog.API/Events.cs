using Microsoft.EntityFrameworkCore;
using Musa.EventBus;

namespace Catalog.API;

public sealed record PieceRegistered(Guid PieceId, string Title, string Creator, string Discipline, string Medium,
    int Year, decimal? EstimateLow, decimal? EstimateHigh, decimal? Price) : IntegrationEvent
{
    public static PieceRegistered From(Piece p) => new(p.Id, p.Title, p.Creator, p.Discipline.ToString(), p.Medium,
        p.Year, p.EstimateLow, p.EstimateHigh, p.Price);
}

// Consumed. Each handler sets a state rather than toggling one, so a redelivered event is harmless.
public sealed record LotOpened(Guid PieceId) : IntegrationEvent;
public sealed record LotPassed(Guid PieceId) : IntegrationEvent;
public sealed record OrderPlaced(Guid PieceId) : IntegrationEvent;

public sealed class AvailabilityHandler(CatalogContext context) :
    IIntegrationEventHandler<LotOpened>, IIntegrationEventHandler<LotPassed>, IIntegrationEventHandler<OrderPlaced>
{
    public Task HandleAsync(LotOpened e, CancellationToken ct) => SetAsync(e.PieceId, Availability.OnTheBlock, ct);
    public Task HandleAsync(LotPassed e, CancellationToken ct) => SetAsync(e.PieceId, Availability.Available, ct);
    public Task HandleAsync(OrderPlaced e, CancellationToken ct) => SetAsync(e.PieceId, Availability.Sold, ct);

    private Task SetAsync(Guid pieceId, Availability availability, CancellationToken ct) =>
        context.Pieces
            .Where(p => p.Id == pieceId && p.Availability != Availability.Sold) // sold is final
            .ExecuteUpdateAsync(set => set.SetProperty(p => p.Availability, availability), ct);
}
