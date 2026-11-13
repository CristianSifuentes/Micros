using Microsoft.EntityFrameworkCore;
using Musa.EventBus;

namespace Catalog.API;

public enum Discipline { Art, Fashion, Design }

public enum Availability
{
    Available,

    /// <summary>Consigned to the sale room; bidding is live.</summary>
    OnTheBlock,
    Sold,
}

/// <summary>
/// One unique object: a painting, a garment, a chair. Works with an estimate go to auction;
/// works with a price are sold by the boutique.
/// </summary>
public sealed class Piece
{
    public Guid Id { get; set; }
    public required string Title { get; set; }
    public required string Creator { get; set; }
    public Discipline Discipline { get; set; }
    public required string Medium { get; set; }
    public int Year { get; set; }
    public string? Dimensions { get; set; }
    public required string Story { get; set; }
    public decimal? EstimateLow { get; set; }
    public decimal? EstimateHigh { get; set; }
    public decimal? Price { get; set; }
    public Availability Availability { get; set; }
}

public sealed class CatalogContext(DbContextOptions<CatalogContext> options) : DbContext(options)
{
    public DbSet<Piece> Pieces => Set<Piece>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("catalog");
        modelBuilder.Entity<Piece>(piece =>
        {
            piece.ToTable("pieces");
            piece.Property(p => p.Id).ValueGeneratedNever();
            piece.Property(p => p.Discipline).HasConversion<string>().HasMaxLength(16);
            piece.Property(p => p.Availability).HasConversion<string>().HasMaxLength(16);
            piece.HasIndex(p => p.Discipline);
        });
        modelBuilder.AddOutbox();
    }
}

public sealed record PieceView(Guid Id, string Title, string Creator, string Discipline, string Medium, int Year,
    string? Dimensions, string Story, decimal? EstimateLow, decimal? EstimateHigh, decimal? Price,
    string Availability, string Artwork)
{
    public static PieceView From(Piece p) => new(p.Id, p.Title, p.Creator, p.Discipline.ToString(), p.Medium, p.Year,
        p.Dimensions, p.Story, p.EstimateLow, p.EstimateHigh, p.Price, p.Availability.ToString(),
        $"pieces/{p.Id}/artwork.svg");
}

public sealed record RegisterPiece(string Title, string Creator, Discipline Discipline, string Medium, int Year,
    string? Dimensions, string Story, decimal? EstimateLow, decimal? EstimateHigh, decimal? Price);
