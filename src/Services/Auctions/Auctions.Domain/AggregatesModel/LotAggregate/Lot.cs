using Auctions.Domain.Events;
using Auctions.Domain.Exceptions;
using Auctions.Domain.SeedWork;

namespace Auctions.Domain.AggregatesModel.LotAggregate;

/// <summary>
/// One work offered at auction. The lot guards every rule of the sale room: the increment ladder,
/// the confidential reserve, the soft close that denies snipers the last word, and the fall of the hammer.
/// </summary>
public sealed class Lot : Entity, IAggregateRoot
{
    /// <summary>A bid this close to the end pushes the close back by the same span.</summary>
    public static readonly TimeSpan SoftCloseWindow = TimeSpan.FromSeconds(20);

    private readonly List<Bid> _bids = [];

    public Guid PieceId { get; private set; }
    public string Title { get; private set; }
    public string Creator { get; private set; }
    public Estimate Estimate { get; private set; }
    public decimal StartingBid { get; private set; }
    public decimal Reserve { get; private set; }
    public LotStatus Status { get; private set; }
    public DateTimeOffset OpenedAt { get; private set; }
    public DateTimeOffset ClosesAt { get; private set; }
    public decimal? CurrentBid { get; private set; }
    public string? LeadingPaddle { get; private set; }
    public int Version { get; private set; }

    public IReadOnlyCollection<Bid> Bids => _bids.AsReadOnly();

    /// <summary>The least the room may offer next.</summary>
    public decimal NextMinimumBid =>
        CurrentBid is { } current ? current + BidIncrements.For(current) : StartingBid;

#pragma warning disable CS8618 // EF Core materialization
    private Lot() { }
#pragma warning restore CS8618

    public Lot(Guid pieceId, string title, string creator, Estimate estimate, decimal reserve,
        TimeSpan duration, DateTimeOffset now)
    {
        if (pieceId == Guid.Empty) throw new DomainException("A lot must offer a registered piece.");
        if (string.IsNullOrWhiteSpace(title)) throw new DomainException("A lot needs a title.");
        if (duration <= TimeSpan.Zero) throw new DomainException("A sale must last some time.");
        if (reserve < 0) throw new DomainException("A reserve cannot be negative.");
        if (reserve > estimate.Low)
        {
            // Sale room convention: the reserve never exceeds the low estimate.
            throw new DomainException("The reserve may not exceed the low estimate.");
        }

        PieceId = pieceId;
        Title = title.Trim();
        Creator = creator.Trim();
        Estimate = estimate;
        Reserve = reserve;
        StartingBid = RoundToStep(estimate.Low / 2);
        Status = LotStatus.Open;
        OpenedAt = now;
        ClosesAt = now + duration;

        AddDomainEvent(new LotOpenedDomainEvent(this, now));
    }

    public void PlaceBid(string paddle, decimal amount, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(paddle)) throw new DomainException("A bid needs a paddle.");
        if (Status != LotStatus.Open || now >= ClosesAt) throw new DomainException("Bidding on this lot has closed.");
        if (paddle == LeadingPaddle) throw new DomainException("You already hold the leading bid.");
        if (amount < NextMinimumBid)
        {
            throw new DomainException($"The next bid must be at least {NextMinimumBid:N0}.");
        }

        _bids.Add(new Bid(paddle, amount, now));
        CurrentBid = amount;
        LeadingPaddle = paddle;
        Version++;

        var extended = false;
        if (ClosesAt - now < SoftCloseWindow)
        {
            ClosesAt = now + SoftCloseWindow;
            extended = true;
        }

        AddDomainEvent(new BidPlacedDomainEvent(this, paddle, amount, extended, now));
    }

    /// <summary>Brings the hammer down once the clock has run out. Calling it early or twice changes nothing.</summary>
    public void Close(DateTimeOffset now)
    {
        if (Status != LotStatus.Open || now < ClosesAt)
        {
            return;
        }

        Version++;
        if (CurrentBid is { } hammer && hammer >= Reserve)
        {
            Status = LotStatus.Sold;
            AddDomainEvent(new LotHammeredDomainEvent(this, LeadingPaddle!, hammer, now));
        }
        else
        {
            Status = LotStatus.Passed;
            AddDomainEvent(new LotPassedDomainEvent(this, now));
        }
    }

    private static decimal RoundToStep(decimal value)
    {
        var step = BidIncrements.For(value);
        return Math.Max(step, Math.Floor(value / step) * step);
    }
}
