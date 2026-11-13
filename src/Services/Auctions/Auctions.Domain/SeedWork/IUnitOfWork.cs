namespace Auctions.Domain.SeedWork;

public interface IUnitOfWork : IDisposable
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>Dispatches pending domain events and then commits, so handler side effects join the same transaction.</summary>
    Task<bool> SaveEntitiesAsync(CancellationToken cancellationToken = default);
}
