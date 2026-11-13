using Auctions.Domain.AggregatesModel.LotAggregate;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Auctions.Infrastructure.EntityConfigurations;

internal sealed class LotEntityTypeConfiguration : IEntityTypeConfiguration<Lot>
{
    public void Configure(EntityTypeBuilder<Lot> lot)
    {
        lot.ToTable("lots");
        lot.HasKey(l => l.Id);
        lot.Ignore(l => l.DomainEvents);
        lot.Ignore(l => l.NextMinimumBid);

        // Hi/Lo hands out the lot number before the insert, so domain events can carry it.
        lot.Property(l => l.Id).UseHiLo("lot_numbers", AuctionsContext.Schema);
        lot.Property(l => l.Version).IsConcurrencyToken();
        lot.Property(l => l.Status).HasConversion<string>().HasMaxLength(16);
        lot.Property(l => l.Title).HasMaxLength(200);
        lot.Property(l => l.Creator).HasMaxLength(200);
        lot.Property(l => l.LeadingPaddle).HasMaxLength(32);
        lot.ComplexProperty(l => l.Estimate);

        lot.HasMany(l => l.Bids).WithOne().HasForeignKey("LotId").OnDelete(DeleteBehavior.Cascade);
        lot.Navigation(l => l.Bids).UsePropertyAccessMode(PropertyAccessMode.Field);

        lot.HasIndex(l => new { l.Status, l.ClosesAt });
        lot.HasIndex(l => l.PieceId);
    }
}

internal sealed class BidEntityTypeConfiguration : IEntityTypeConfiguration<Bid>
{
    public void Configure(EntityTypeBuilder<Bid> bid)
    {
        bid.ToTable("bids");
        bid.HasKey(b => b.Id);
        bid.Ignore(b => b.DomainEvents);
        bid.Property(b => b.Paddle).HasMaxLength(32);
    }
}

internal sealed class ConsignablePieceEntityTypeConfiguration : IEntityTypeConfiguration<ConsignablePiece>
{
    public void Configure(EntityTypeBuilder<ConsignablePiece> piece)
    {
        piece.ToTable("pieces");
        piece.HasKey(p => p.PieceId);
        piece.Property(p => p.PieceId).ValueGeneratedNever();
    }
}
