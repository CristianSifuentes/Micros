using Auctions.Domain.SeedWork;

namespace Auctions.Domain.AggregatesModel.LotAggregate;

public interface ILotRepository : IRepository<Lot>
{
    Lot Add(Lot lot);
    Task<Lot?> GetAsync(int lotId, CancellationToken cancellationToken);
    Task<bool> HasOpenLotForAsync(Guid pieceId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Lot>> GetDueAsync(DateTimeOffset now, CancellationToken cancellationToken);
}
