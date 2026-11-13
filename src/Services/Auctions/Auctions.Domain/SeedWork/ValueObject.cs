namespace Auctions.Domain.SeedWork;

/// <summary>Base class for value objects: no identity, equality by the value of all components.</summary>
public abstract class ValueObject
{
    protected abstract IEnumerable<object?> GetEqualityComponents();

    public override bool Equals(object? obj) =>
        obj is not null
        && obj.GetType() == GetType()
        && GetEqualityComponents().SequenceEqual(((ValueObject)obj).GetEqualityComponents());

    public override int GetHashCode() =>
        GetEqualityComponents().Select(x => x?.GetHashCode() ?? 0).Aggregate(0, (x, y) => x ^ y);

    public static bool operator ==(ValueObject? left, ValueObject? right) =>
        left is null ? right is null : left.Equals(right);

    public static bool operator !=(ValueObject? left, ValueObject? right) => !(left == right);
}
