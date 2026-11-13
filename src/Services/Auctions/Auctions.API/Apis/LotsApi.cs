using Auctions.API.Application;
using Auctions.Domain.AggregatesModel.LotAggregate;
using Auctions.Domain.Exceptions;
using Auctions.Infrastructure;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Auctions.API.Apis;

public sealed record ConsignRequest(Guid PieceId, int DurationSeconds = 180, decimal? Reserve = null);
public sealed record BidRequest(string Paddle, decimal Amount);

public static class LotsApi
{
    public static void MapLotsApi(this IEndpointRouteBuilder app)
    {
        var lots = app.MapGroup("/lots");

        lots.MapGet("/", async (AuctionsContext db, CancellationToken ct) =>
        {
            var all = await db.Lots.AsNoTracking().Include(l => l.Bids)
                .OrderBy(l => l.Status).ThenBy(l => l.ClosesAt).Take(60).ToListAsync(ct);
            return TypedResults.Ok(all.Select(LotView.From));
        });

        lots.MapGet("/{lotNumber:int}", async Task<Results<Ok<LotView>, NotFound>> (int lotNumber, AuctionsContext db, CancellationToken ct) =>
            await db.Lots.AsNoTracking().Include(l => l.Bids).SingleOrDefaultAsync(l => l.Id == lotNumber, ct) is { } lot
                ? TypedResults.Ok(LotView.From(lot))
                : TypedResults.NotFound());

        lots.MapPost("/", async Task<Results<Created<LotView>, NotFound, Conflict<ProblemDetails>>> (
            ConsignRequest request, AuctionsContext db, ILotRepository repository, TimeProvider time, CancellationToken ct) =>
        {
            var piece = await db.Pieces.AsNoTracking().SingleOrDefaultAsync(p => p.PieceId == request.PieceId, ct);
            if (piece is null) return TypedResults.NotFound();
            if (piece.EstimateLow is not { } low || piece.EstimateHigh is not { } high)
            {
                throw new DomainException("This piece carries no estimate, so it cannot be offered at auction.");
            }

            if (await repository.HasOpenLotForAsync(piece.PieceId, ct))
            {
                return TypedResults.Conflict(new ProblemDetails { Title = "This piece is already on the block." });
            }

            var reserve = request.Reserve ?? Math.Round(low * 0.8m, 0);
            var lot = repository.Add(new Lot(piece.PieceId, piece.Title, piece.Creator, new Estimate(low, high),
                reserve, TimeSpan.FromSeconds(request.DurationSeconds), time.GetUtcNow()));
            await repository.UnitOfWork.SaveEntitiesAsync(ct);
            return TypedResults.Created($"/lots/{lot.Id}", LotView.From(lot));
        });

        lots.MapPost("/{lotNumber:int}/bids", async Task<Results<Ok<LotView>, NotFound>> (
            int lotNumber, BidRequest bid, ILotRepository repository, TimeProvider time, CancellationToken ct) =>
        {
            var lot = await repository.GetAsync(lotNumber, ct);
            if (lot is null) return TypedResults.NotFound();

            lot.PlaceBid(bid.Paddle, bid.Amount, time.GetUtcNow());
            await repository.UnitOfWork.SaveEntitiesAsync(ct);
            return TypedResults.Ok(LotView.From(lot));
        });

        // .NET 10 server-sent events: one long-lived response, one frame per change in the room.
        app.MapGet("/live", (LiveFeed feed, CancellationToken ct) =>
            TypedResults.ServerSentEvents(feed.Watch(ct), eventType: "lot"));
    }
}

/// <summary>A broken rule of the sale room is the caller's problem (422); losing a race for the same lot is a conflict (409).</summary>
public sealed class SaleRoomExceptionHandler(IProblemDetailsService problems) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception exception, CancellationToken ct)
    {
        var (status, title) = exception switch
        {
            DomainException => (StatusCodes.Status422UnprocessableEntity, exception.Message),
            DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "Another bid reached the room first. Look again and bid."),
            _ => (0, ""),
        };
        if (status == 0) return false;

        http.Response.StatusCode = status;
        return await problems.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = http,
            ProblemDetails = new ProblemDetails { Status = status, Title = title },
        });
    }
}
