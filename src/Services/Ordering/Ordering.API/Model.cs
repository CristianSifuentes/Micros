using Microsoft.EntityFrameworkCore;
using Musa.EventBus;

namespace Ordering.API;

public enum SalesChannel { Auction, Boutique }

public sealed class Order
{
    public Guid Id { get; set; }
    public Guid PieceId { get; set; }
    public required string Title { get; set; }
    public required string Creator { get; set; }
    public required string Paddle { get; set; }
    public SalesChannel Channel { get; set; }
    public int? LotNumber { get; set; }

    /// <summary>Hammer price at auction, list price in the boutique.</summary>
    public decimal Price { get; set; }
    public decimal BuyersPremium { get; set; }
    public decimal Total { get; set; }
    public DateTimeOffset PlacedAt { get; set; }
}

/// <summary>The little this context needs to know about a piece, kept current from Catalog's events.</summary>
public sealed class PieceOnOffer
{
    public Guid PieceId { get; set; }
    public required string Title { get; set; }
    public required string Creator { get; set; }
    public decimal? Price { get; set; }
}

public sealed class OrderingContext(DbContextOptions<OrderingContext> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<PieceOnOffer> Pieces => Set<PieceOnOffer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("ordering");
        modelBuilder.Entity<Order>(order =>
        {
            order.ToTable("orders");
            order.Property(o => o.Id).ValueGeneratedNever();
            order.Property(o => o.Channel).HasConversion<string>().HasMaxLength(16);
            // Every piece is unique, so it can be sold exactly once. The database is the final arbiter of that rule.
            order.HasIndex(o => o.PieceId).IsUnique();
            order.HasIndex(o => o.Paddle);
        });
        modelBuilder.Entity<PieceOnOffer>(piece =>
        {
            piece.ToTable("pieces");
            piece.HasKey(p => p.PieceId);
            piece.Property(p => p.PieceId).ValueGeneratedNever();
        });
        modelBuilder.AddOutbox();
    }
}

/// <summary>
/// The fee the house adds to the hammer price, charged in tiers so that it lightens as the price climbs.
/// The rates are illustrative, modelled on the tiered schedules auction houses publish.
/// </summary>
public static class BuyersPremium
{
    private static readonly (decimal UpTo, decimal Rate)[] Tiers =
        [(1_000_000m, 0.26m), (6_000_000m, 0.20m), (decimal.MaxValue, 0.139m)];

    public static decimal On(decimal hammerPrice)
    {
        decimal premium = 0, floor = 0;
        foreach (var (upTo, rate) in Tiers)
        {
            if (hammerPrice <= floor) break;
            premium += (Math.Min(hammerPrice, upTo) - floor) * rate;
            floor = upTo;
        }

        return Math.Round(premium, 2);
    }
}
