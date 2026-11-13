using Auctions.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Musa.EventBus;

namespace Auctions.API.Application.IntegrationEvents;

// Published by the sale room.
public sealed record LotOpened(int LotId, Guid PieceId, string Title, string Creator,
    decimal EstimateLow, decimal EstimateHigh, DateTimeOffset ClosesAt) : IntegrationEvent;

public sealed record LotHammered(int LotId, Guid PieceId, string Title, string Creator,
    string Paddle, decimal HammerPrice, int BidCount) : IntegrationEvent;

public sealed record LotPassed(int LotId, Guid PieceId, string Title, string Creator) : IntegrationEvent;

// Consumed: only the fields this context cares about.
public sealed record PieceRegistered(Guid PieceId, string Title, string Creator,
    decimal? EstimateLow, decimal? EstimateHigh) : IntegrationEvent;

public sealed class PieceRegisteredHandler(AuctionsContext context) : IIntegrationEventHandler<PieceRegistered>
{
    public async Task HandleAsync(PieceRegistered e, CancellationToken ct)
    {
        // Upsert keeps the handler idempotent: a redelivered event rewrites the same row.
        var piece = await context.Pieces.SingleOrDefaultAsync(p => p.PieceId == e.PieceId, ct);
        if (piece is null)
        {
            context.Pieces.Add(piece = new ConsignablePiece { PieceId = e.PieceId, Title = e.Title, Creator = e.Creator });
        }

        (piece.Title, piece.Creator, piece.EstimateLow, piece.EstimateHigh) = (e.Title, e.Creator, e.EstimateLow, e.EstimateHigh);
        await context.SaveChangesAsync(ct);
    }
}
