namespace Auctions.Domain.SeedWork;

/// <summary>Something that already happened in this domain. Name implementations in past tense; keep them immutable.</summary>
public interface IDomainEvent
{
    DateTime OccurredOn { get; }
}
