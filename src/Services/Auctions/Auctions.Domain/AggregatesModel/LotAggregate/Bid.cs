using Auctions.Domain.SeedWork;

namespace Auctions.Domain.AggregatesModel.LotAggregate;

/// <summary>A bid exists only inside its lot; nothing outside the aggregate creates or changes one.</summary>
public sealed class Bid : Entity
{
    public string Paddle { get; private set; }
    public decimal Amount { get; private set; }
    public DateTimeOffset PlacedAt { get; private set; }

    internal Bid(string paddle, decimal amount, DateTimeOffset placedAt) =>
        (Paddle, Amount, PlacedAt) = (paddle, amount, placedAt);
}
