using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace Provenance.API;

/// <summary>
/// One line in a piece's history. Entries are only ever appended, and each carries the hash of the one
/// before it, so a rewritten past no longer adds up.
/// </summary>
public sealed class LedgerEntry
{
    public const string Genesis = "0000000000000000000000000000000000000000000000000000000000000000";

    public long Sequence { get; private set; }
    public Guid PieceId { get; private set; }
    public string Kind { get; private set; } = "";
    public string Statement { get; private set; } = "";
    public DateTimeOffset OccurredAt { get; private set; }
    public Guid SourceEventId { get; private set; }
    public string PreviousHash { get; private set; } = Genesis;
    public string Hash { get; private set; } = "";

    private LedgerEntry() { }

    public static LedgerEntry Append(LedgerEntry? previous, Guid pieceId, string kind, string statement,
        DateTimeOffset occurredAt, Guid sourceEventId)
    {
        var entry = new LedgerEntry
        {
            PieceId = pieceId, Kind = kind, Statement = statement, SourceEventId = sourceEventId,
            // Whole milliseconds, so the stored instant hashes to the same value when read back.
            OccurredAt = DateTimeOffset.FromUnixTimeMilliseconds(occurredAt.ToUnixTimeMilliseconds()),
            PreviousHash = previous?.Hash ?? Genesis,
        };
        entry.Hash = entry.ComputeHash();
        return entry;
    }

    public string ComputeHash() => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(
        $"{PreviousHash}|{PieceId:N}|{Kind}|{Statement}|{OccurredAt.ToUnixTimeMilliseconds()}")));
}

public sealed class ProvenanceContext(DbContextOptions<ProvenanceContext> options) : DbContext(options)
{
    public DbSet<LedgerEntry> Entries => Set<LedgerEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("provenance");
        modelBuilder.Entity<LedgerEntry>(entry =>
        {
            entry.ToTable("ledger");
            entry.HasKey(e => e.Sequence);
            entry.HasIndex(e => e.SourceEventId).IsUnique(); // one event, one line: redelivery cannot write twice
            entry.HasIndex(e => new { e.PieceId, e.Sequence });
        });
    }
}

public sealed record EntryView(string Kind, string Statement, DateTimeOffset OccurredAt, string Hash);

public sealed record ProvenanceView(Guid PieceId, bool Intact, IReadOnlyList<EntryView> Entries)
{
    /// <summary>Recomputes the chain from the first line; a single altered entry breaks every hash after it.</summary>
    public static ProvenanceView From(Guid pieceId, IReadOnlyList<LedgerEntry> entries)
    {
        var intact = true;
        var expectedPrevious = LedgerEntry.Genesis;
        foreach (var entry in entries)
        {
            intact &= entry.PreviousHash == expectedPrevious && entry.Hash == entry.ComputeHash();
            expectedPrevious = entry.Hash;
        }

        return new(pieceId, intact, [.. entries.Select(e => new EntryView(e.Kind, e.Statement, e.OccurredAt, e.Hash))]);
    }
}
