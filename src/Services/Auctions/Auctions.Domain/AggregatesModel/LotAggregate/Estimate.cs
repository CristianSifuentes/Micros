using Auctions.Domain.Exceptions;
using Auctions.Domain.SeedWork;

namespace Auctions.Domain.AggregatesModel.LotAggregate;

/// <summary>The house's published opinion of value: a range, never a promise.</summary>
public sealed class Estimate : ValueObject
{
    public decimal Low { get; private set; }
    public decimal High { get; private set; }

    public Estimate(decimal low, decimal high)
    {
        if (low <= 0 || high < low)
        {
            throw new DomainException("An estimate needs a positive low figure and a high figure at or above it.");
        }

        (Low, High) = (low, high);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Low;
        yield return High;
    }
}
