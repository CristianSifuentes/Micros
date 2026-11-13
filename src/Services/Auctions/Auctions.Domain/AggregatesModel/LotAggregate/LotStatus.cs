namespace Auctions.Domain.AggregatesModel.LotAggregate;

public enum LotStatus
{
    /// <summary>Bidding is live.</summary>
    Open = 1,

    /// <summary>The hammer fell at or above the reserve.</summary>
    Sold = 2,

    /// <summary>Bidding ended below the reserve (or with no bids): the lot is bought in and returns to its consignor.</summary>
    Passed = 3,
}
