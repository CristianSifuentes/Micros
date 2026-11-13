using Microsoft.EntityFrameworkCore;
using Musa.EventBus;

namespace Ordering.API;

public sealed record OrderPlaced(Guid OrderId, Guid PieceId, string Title, string Creator, string Paddle,
    string Channel, int? LotNumber, decimal Price, decimal BuyersPremium, decimal Total) : IntegrationEvent
{
    public static OrderPlaced From(Order o) => new(o.Id, o.PieceId, o.Title, o.Creator, o.Paddle,
        o.Channel.ToString(), o.LotNumber, o.Price, o.BuyersPremium, o.Total);
}

public sealed record PieceRegistered(Guid PieceId, string Title, string Creator, decimal? Price) : IntegrationEvent;
public sealed record LotHammered(int LotId, Guid PieceId, string Title, string Creator, string Paddle, decimal HammerPrice) : IntegrationEvent;

public sealed class PieceRegisteredHandler(OrderingContext context) : IIntegrationEventHandler<PieceRegistered>
{
    public async Task HandleAsync(PieceRegistered e, CancellationToken ct)
    {
        var piece = await context.Pieces.SingleOrDefaultAsync(p => p.PieceId == e.PieceId, ct);
        if (piece is null)
        {
            context.Pieces.Add(piece = new PieceOnOffer { PieceId = e.PieceId, Title = e.Title, Creator = e.Creator });
        }

        (piece.Title, piece.Creator, piece.Price) = (e.Title, e.Creator, e.Price);
        await context.SaveChangesAsync(ct);
    }
}

/// <summary>The hammer has fallen: raise the winner's invoice.</summary>
public sealed class LotHammeredHandler(OrderingContext context, TimeProvider time) : IIntegrationEventHandler<LotHammered>
{
    public async Task HandleAsync(LotHammered e, CancellationToken ct)
    {
        if (await context.Orders.AnyAsync(o => o.PieceId == e.PieceId, ct))
        {
            return; // already invoiced: the event was redelivered
        }

        var premium = BuyersPremium.On(e.HammerPrice);
        var order = new Order
        {
            Id = Guid.CreateVersion7(), PieceId = e.PieceId, Title = e.Title, Creator = e.Creator, Paddle = e.Paddle,
            Channel = SalesChannel.Auction, LotNumber = e.LotId, Price = e.HammerPrice, BuyersPremium = premium,
            Total = e.HammerPrice + premium, PlacedAt = time.GetUtcNow(),
        };
        context.Orders.Add(order);
        context.Enqueue(OrderPlaced.From(order));
        await context.SaveChangesAsync(ct);
    }
}
