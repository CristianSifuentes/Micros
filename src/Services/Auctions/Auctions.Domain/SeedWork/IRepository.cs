namespace Auctions.Domain.SeedWork;

/// <summary>One repository per aggregate root. Contracts live in the domain, implementations in infrastructure.</summary>
public interface IRepository<T> where T : IAggregateRoot
{
    IUnitOfWork UnitOfWork { get; }
}
