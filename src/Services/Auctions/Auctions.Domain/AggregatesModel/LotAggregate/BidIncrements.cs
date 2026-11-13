namespace Auctions.Domain.AggregatesModel.LotAggregate;

/// <summary>
/// The increment ladder: the higher the price, the larger the step the room is asked to take.
/// Steps are roughly a tenth of the current price, as is customary in sale rooms.
/// </summary>
public static class BidIncrements
{
    private static readonly (decimal Below, decimal Step)[] Ladder =
    [
        (1_000m, 50m), (2_000m, 100m), (5_000m, 250m), (10_000m, 500m), (20_000m, 1_000m),
        (50_000m, 2_500m), (100_000m, 5_000m), (200_000m, 10_000m), (500_000m, 25_000m),
    ];

    public static decimal For(decimal currentPrice)
    {
        foreach (var (below, step) in Ladder)
        {
            if (currentPrice < below) return step;
        }

        return 50_000m;
    }
}
