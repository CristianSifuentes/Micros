using Auctions.Domain.SeedWork;

namespace Auctions.UnitTests;

public class SeedWorkTests
{
    private sealed class Money(decimal amount, string currency) : ValueObject
    {
        public decimal Amount { get; } = amount;
        public string Currency { get; } = currency;

        protected override IEnumerable<object?> GetEqualityComponents()
        {
            yield return Amount;
            yield return Currency;
        }
    }

    private sealed class Sample : Entity
    {
        public Sample(int id = 0) => Id = id;
    }

    private sealed record SampleHappened(DateTime OccurredOn) : IDomainEvent;

    [Fact]
    public void Value_objects_with_same_components_are_equal()
    {
        Assert.Equal(new Money(10m, "EUR"), new Money(10m, "EUR"));
        Assert.True(new Money(10m, "EUR") == new Money(10m, "EUR"));
        Assert.NotEqual(new Money(10m, "EUR"), new Money(10m, "USD"));
    }

    [Fact]
    public void Entities_are_equal_by_identity_and_transient_ones_never_are()
    {
        Assert.Equal(new Sample(7), new Sample(7));
        Assert.NotEqual(new Sample(7), new Sample(8));
        Assert.NotEqual(new Sample(), new Sample());
    }

    [Fact]
    public void Domain_events_are_collected_until_cleared()
    {
        var entity = new Sample(1);
        entity.AddDomainEvent(new SampleHappened(DateTime.UtcNow));

        Assert.Single(entity.DomainEvents);

        entity.ClearDomainEvents();

        Assert.Empty(entity.DomainEvents);
    }
}
