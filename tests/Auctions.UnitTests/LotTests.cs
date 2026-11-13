using Auctions.Domain.AggregatesModel.LotAggregate;
using Auctions.Domain.Events;
using Auctions.Domain.Exceptions;

namespace Auctions.UnitTests;

public class LotTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 13, 19, 0, 0, TimeSpan.Zero);

    private static Lot NewLot(decimal low = 40_000m, decimal high = 60_000m, decimal reserve = 35_000m) =>
        new(Guid.NewGuid(), "Nocturne en bleu de Prusse", "Inés Valcárcel", new Estimate(low, high), reserve,
            TimeSpan.FromMinutes(5), Start);

    [Fact]
    public void Opening_a_lot_starts_bidding_at_half_the_low_estimate()
    {
        var lot = NewLot();

        Assert.Equal(LotStatus.Open, lot.Status);
        Assert.Equal(20_000m, lot.StartingBid);
        Assert.Equal(20_000m, lot.NextMinimumBid);
        Assert.IsType<LotOpenedDomainEvent>(Assert.Single(lot.DomainEvents));
    }

    [Fact]
    public void The_reserve_may_not_exceed_the_low_estimate() =>
        Assert.Throws<DomainException>(() => NewLot(reserve: 45_000m));

    [Fact]
    public void Each_bid_must_climb_the_increment_ladder()
    {
        var lot = NewLot();
        lot.PlaceBid("P-101", 20_000m, Start.AddSeconds(5));

        Assert.Equal(22_500m, lot.NextMinimumBid);
        Assert.Throws<DomainException>(() => lot.PlaceBid("P-202", 21_000m, Start.AddSeconds(6)));

        lot.PlaceBid("P-202", 22_500m, Start.AddSeconds(7));

        Assert.Equal("P-202", lot.LeadingPaddle);
        Assert.Equal(2, lot.Bids.Count);
    }

    [Fact]
    public void The_leading_paddle_cannot_bid_against_itself()
    {
        var lot = NewLot();
        lot.PlaceBid("P-101", 20_000m, Start.AddSeconds(5));

        Assert.Throws<DomainException>(() => lot.PlaceBid("P-101", 30_000m, Start.AddSeconds(6)));
    }

    [Fact]
    public void A_late_bid_pushes_the_close_back()
    {
        var lot = NewLot();
        var late = lot.ClosesAt - TimeSpan.FromSeconds(3);

        lot.PlaceBid("P-101", 20_000m, late);

        Assert.Equal(late + Lot.SoftCloseWindow, lot.ClosesAt);
        Assert.True(lot.DomainEvents.OfType<BidPlacedDomainEvent>().Single().ExtendedClose);
    }

    [Fact]
    public void No_bid_is_taken_after_the_clock_runs_out()
    {
        var lot = NewLot();

        Assert.Throws<DomainException>(() => lot.PlaceBid("P-101", 20_000m, lot.ClosesAt));
    }

    [Fact]
    public void The_hammer_falls_when_the_reserve_is_met()
    {
        var lot = NewLot();
        lot.PlaceBid("P-101", 36_000m, Start.AddSeconds(5));

        lot.Close(lot.ClosesAt);

        Assert.Equal(LotStatus.Sold, lot.Status);
        var hammered = lot.DomainEvents.OfType<LotHammeredDomainEvent>().Single();
        Assert.Equal(("P-101", 36_000m), (hammered.Paddle, hammered.HammerPrice));
    }

    [Fact]
    public void A_lot_below_its_reserve_is_bought_in()
    {
        var lot = NewLot();
        lot.PlaceBid("P-101", 20_000m, Start.AddSeconds(5));

        lot.Close(lot.ClosesAt);

        Assert.Equal(LotStatus.Passed, lot.Status);
        Assert.Single(lot.DomainEvents.OfType<LotPassedDomainEvent>());
    }

    [Fact]
    public void Closing_early_or_twice_changes_nothing()
    {
        var lot = NewLot();
        lot.PlaceBid("P-101", 36_000m, Start.AddSeconds(5));

        lot.Close(Start.AddSeconds(10));
        Assert.Equal(LotStatus.Open, lot.Status);

        lot.Close(lot.ClosesAt);
        lot.Close(lot.ClosesAt.AddMinutes(1));

        Assert.Single(lot.DomainEvents.OfType<LotHammeredDomainEvent>());
    }

    [Theory]
    [InlineData(500, 50)]
    [InlineData(1_000, 100)]
    [InlineData(22_500, 2_500)]
    [InlineData(150_000, 10_000)]
    [InlineData(2_000_000, 50_000)]
    public void The_ladder_widens_with_the_price(decimal price, decimal step) =>
        Assert.Equal(step, BidIncrements.For(price));
}
