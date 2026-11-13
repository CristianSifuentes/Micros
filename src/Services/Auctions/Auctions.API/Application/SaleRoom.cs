using System.Diagnostics.Metrics;
using System.Threading.Channels;
using Auctions.API.Application.IntegrationEvents;
using Auctions.Domain.AggregatesModel.LotAggregate;
using Auctions.Domain.Events;
using Auctions.Domain.SeedWork;
using Auctions.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Musa.EventBus;

namespace Auctions.API.Application;

/// <summary>What the room is allowed to see. The reserve stays confidential; only whether it has been met is shown.</summary>
public sealed record LotView(int LotNumber, Guid PieceId, string Title, string Creator, decimal EstimateLow,
    decimal EstimateHigh, decimal StartingBid, decimal? CurrentBid, decimal NextMinimumBid, string? LeadingPaddle,
    string Status, bool ReserveMet, DateTimeOffset ClosesAt, IReadOnlyList<BidView> Bids)
{
    public static LotView From(Lot lot) => new(lot.Id, lot.PieceId, lot.Title, lot.Creator, lot.Estimate.Low,
        lot.Estimate.High, lot.StartingBid, lot.CurrentBid, lot.NextMinimumBid, lot.LeadingPaddle,
        lot.Status.ToString(), lot.CurrentBid >= lot.Reserve, lot.ClosesAt,
        [.. lot.Bids.OrderByDescending(b => b.Amount).Select(b => new BidView(b.Paddle, b.Amount, b.PlacedAt))]);
}

public sealed record BidView(string Paddle, decimal Amount, DateTimeOffset PlacedAt);

/// <summary>
/// Fans lot updates out to every connected screen. In-process on purpose: with more than one replica,
/// put a backplane (Redis pub/sub) behind this class.
/// </summary>
public sealed class LiveFeed
{
    private readonly Lock _gate = new();
    private readonly List<Channel<LotView>> _screens = [];

    public void Publish(LotView lot)
    {
        lock (_gate)
        {
            foreach (var screen in _screens) screen.Writer.TryWrite(lot);
        }
    }

    public async IAsyncEnumerable<LotView> Watch(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        var screen = Channel.CreateBounded<LotView>(new BoundedChannelOptions(64) { FullMode = BoundedChannelFullMode.DropOldest });
        lock (_gate) _screens.Add(screen);
        try
        {
            await foreach (var lot in screen.Reader.ReadAllAsync(ct)) yield return lot;
        }
        finally
        {
            lock (_gate) _screens.Remove(screen);
        }
    }
}

public static class SaleRoomTelemetry
{
    public const string Name = "Musa.Auctions";
    private static readonly Meter Meter = new(Name);
    public static readonly Counter<long> Bids = Meter.CreateCounter<long>("musa.auctions.bids", description: "Bids accepted");
    public static readonly Counter<long> Hammers = Meter.CreateCounter<long>("musa.auctions.hammers", description: "Lots sold");
    public static readonly Histogram<double> HammerPrice = Meter.CreateHistogram<double>("musa.auctions.hammer_price", "USD");
}

/// <summary>Turns what happened in the domain into what the rest of Musa hears about (outbox) and what screens show (feed).</summary>
public sealed class SaleRoomEventSink(LiveFeed feed) : IDomainEventSink
{
    public void BeforeCommit(IDomainEvent domainEvent, DbContext context)
    {
        IntegrationEvent? outgoing = domainEvent switch
        {
            LotOpenedDomainEvent e => new LotOpened(e.Lot.Id, e.Lot.PieceId, e.Lot.Title, e.Lot.Creator,
                e.Lot.Estimate.Low, e.Lot.Estimate.High, e.Lot.ClosesAt),
            LotHammeredDomainEvent e => new LotHammered(e.Lot.Id, e.Lot.PieceId, e.Lot.Title, e.Lot.Creator,
                e.Paddle, e.HammerPrice, e.Lot.Bids.Count),
            LotPassedDomainEvent e => new LotPassed(e.Lot.Id, e.Lot.PieceId, e.Lot.Title, e.Lot.Creator),
            _ => null, // individual bids stay inside the sale room
        };

        if (outgoing is not null) context.Enqueue(outgoing);
    }

    public void AfterCommit(IDomainEvent domainEvent)
    {
        switch (domainEvent)
        {
            case BidPlacedDomainEvent e:
                SaleRoomTelemetry.Bids.Add(1);
                feed.Publish(LotView.From(e.Lot));
                break;
            case LotHammeredDomainEvent e:
                SaleRoomTelemetry.Hammers.Add(1);
                SaleRoomTelemetry.HammerPrice.Record((double)e.HammerPrice);
                feed.Publish(LotView.From(e.Lot));
                break;
            case LotOpenedDomainEvent e: feed.Publish(LotView.From(e.Lot)); break;
            case LotPassedDomainEvent e: feed.Publish(LotView.From(e.Lot)); break;
        }
    }
}

/// <summary>The auctioneer's clock: brings the hammer down on every lot whose time has run out.</summary>
public sealed class Auctioneer(IServiceScopeFactory scopes, TimeProvider time, ILogger<Auctioneer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1), time);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var lots = scope.ServiceProvider.GetRequiredService<ILotRepository>();
                var now = time.GetUtcNow();
                var due = await lots.GetDueAsync(now, stoppingToken);
                if (due.Count == 0) continue;

                foreach (var lot in due) lot.Close(now);
                await lots.UnitOfWork.SaveEntitiesAsync(stoppingToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                // A bid landed while the hammer was falling; the soft close moved the deadline. Next tick decides.
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "The auctioneer could not close due lots");
            }
        }
    }
}
