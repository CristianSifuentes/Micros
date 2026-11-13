using Auctions.Domain.AggregatesModel.LotAggregate;
using Auctions.Domain.SeedWork;
using Microsoft.EntityFrameworkCore;

namespace Auctions.Infrastructure.Repositories;

public sealed class LotRepository(AuctionsContext context) : ILotRepository
{
    public IUnitOfWork UnitOfWork => context;

    public Lot Add(Lot lot) => context.Lots.Add(lot).Entity;

    public Task<Lot?> GetAsync(int lotId, CancellationToken cancellationToken) =>
        context.Lots.Include(l => l.Bids).SingleOrDefaultAsync(l => l.Id == lotId, cancellationToken);

    public Task<bool> HasOpenLotForAsync(Guid pieceId, CancellationToken cancellationToken) =>
        context.Lots.AnyAsync(l => l.PieceId == pieceId && l.Status == LotStatus.Open, cancellationToken);

    public async Task<IReadOnlyList<Lot>> GetDueAsync(DateTimeOffset now, CancellationToken cancellationToken) =>
        await context.Lots.Include(l => l.Bids).Where(l => l.Status == LotStatus.Open && l.ClosesAt <= now).ToListAsync(cancellationToken);
}
