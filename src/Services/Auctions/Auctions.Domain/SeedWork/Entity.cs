namespace Auctions.Domain.SeedWork;

/// <summary>Base class for domain entities: identity-based equality plus a deferred domain event list.</summary>
public abstract class Entity
{
    private int? _requestedHashCode;
    private List<IDomainEvent>? _domainEvents;

    public virtual int Id { get; protected set; }

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents?.AsReadOnly() ?? [];

    public void AddDomainEvent(IDomainEvent eventItem) => (_domainEvents ??= []).Add(eventItem);

    public void RemoveDomainEvent(IDomainEvent eventItem) => _domainEvents?.Remove(eventItem);

    public void ClearDomainEvents() => _domainEvents?.Clear();

    public bool IsTransient() => Id == default;

    public override bool Equals(object? obj)
    {
        if (obj is not Entity other) return false;
        if (ReferenceEquals(this, other)) return true;
        if (GetType() != other.GetType()) return false;
        if (other.IsTransient() || IsTransient()) return false;
        return other.Id == Id;
    }

    public override int GetHashCode() =>
        IsTransient() ? base.GetHashCode() : _requestedHashCode ??= Id.GetHashCode() ^ 31;

    public static bool operator ==(Entity? left, Entity? right) =>
        left is null ? right is null : left.Equals(right);

    public static bool operator !=(Entity? left, Entity? right) => !(left == right);
}
