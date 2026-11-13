using Auctions.Domain.AggregatesModel.LotAggregate;
using Auctions.Domain.SeedWork;

namespace Auctions.Domain.Events;

public sealed record LotOpenedDomainEvent(Lot Lot, DateTimeOffset At) : IDomainEvent
{
    public DateTime OccurredOn => At.UtcDateTime;
}

public sealed record BidPlacedDomainEvent(Lot Lot, string Paddle, decimal Amount, bool ExtendedClose, DateTimeOffset At) : IDomainEvent
{
    public DateTime OccurredOn => At.UtcDateTime;
}

public sealed record LotHammeredDomainEvent(Lot Lot, string Paddle, decimal HammerPrice, DateTimeOffset At) : IDomainEvent
{
    public DateTime OccurredOn => At.UtcDateTime;
}

public sealed record LotPassedDomainEvent(Lot Lot, DateTimeOffset At) : IDomainEvent
{
    public DateTime OccurredOn => At.UtcDateTime;
}
