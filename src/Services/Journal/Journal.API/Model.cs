using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Musa.EventBus;

namespace Journal.API;

public enum EntryKind
{
    /// <summary>Long-form writing by the editors.</summary>
    Essay,

    /// <summary>A dated note: something seen, read or overheard.</summary>
    Note,

    /// <summary>Written by Musa itself, from what happens in the sale room and the boutique.</summary>
    Chronicle,
}

public sealed class Entry
{
    public Guid Id { get; set; }
    public EntryKind Kind { get; set; }
    public required string Title { get; set; }
    public required string Body { get; set; }
    public required string Author { get; set; }
    public DateTimeOffset PublishedAt { get; set; }
    public Guid? PieceId { get; set; }
    public Guid? SourceEventId { get; set; }
}

public sealed class JournalContext(DbContextOptions<JournalContext> options) : DbContext(options)
{
    public DbSet<Entry> Entries => Set<Entry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("journal");
        modelBuilder.Entity<Entry>(entry =>
        {
            entry.ToTable("entries");
            entry.Property(e => e.Id).ValueGeneratedNever();
            entry.Property(e => e.Kind).HasConversion<string>().HasMaxLength(16);
            entry.HasIndex(e => e.SourceEventId).IsUnique();
            entry.HasIndex(e => e.PublishedAt);
        });
    }
}

public sealed record LotOpened(int LotId, Guid PieceId, string Title, string Creator, decimal EstimateLow, decimal EstimateHigh) : IntegrationEvent;
public sealed record LotHammered(int LotId, Guid PieceId, string Title, string Creator, decimal HammerPrice, int BidCount) : IntegrationEvent;
public sealed record LotPassed(int LotId, Guid PieceId, string Title, string Creator) : IntegrationEvent;
public sealed record OrderPlaced(Guid PieceId, string Title, string Creator, string Channel, decimal Total) : IntegrationEvent;

/// <summary>Musa keeps its own diary: each thing that happens elsewhere becomes a line here.</summary>
public sealed class Chronicler(JournalContext context) :
    IIntegrationEventHandler<LotOpened>, IIntegrationEventHandler<LotHammered>,
    IIntegrationEventHandler<LotPassed>, IIntegrationEventHandler<OrderPlaced>
{
    private static readonly CultureInfo Es = CultureInfo.GetCultureInfo("es-MX");

    public Task HandleAsync(LotOpened e, CancellationToken ct) => WriteAsync(e, e.PieceId,
        $"Lote {e.LotId}: sube a la tarima «{e.Title}»",
        $"La obra de {e.Creator} sale a subasta con una estimación de {Usd(e.EstimateLow)} a {Usd(e.EstimateHigh)}. La sala está abierta.", ct);

    public Task HandleAsync(LotHammered e, CancellationToken ct) => WriteAsync(e, e.PieceId,
        $"Cae el martillo: «{e.Title}», {Usd(e.HammerPrice)}",
        $"{e.BidCount} pujas después, el lote {e.LotId} de {e.Creator} tiene nuevo custodio. El precio dice cuánto; el porqué lo sabrá quien la cuelgue.", ct);

    public Task HandleAsync(LotPassed e, CancellationToken ct) => WriteAsync(e, e.PieceId,
        $"Lote {e.LotId}: «{e.Title}» se queda sin remate",
        $"La sala no llegó a la reserva y la obra de {e.Creator} vuelve a casa. No es un fracaso: es una obra que todavía no ha encontrado a su persona.", ct);

    public Task HandleAsync(OrderPlaced e, CancellationToken ct) => e.Channel == "Boutique"
        ? WriteAsync(e, e.PieceId, $"«{e.Title}» deja la boutique",
            $"La pieza de {e.Creator} ha sido adquirida por {Usd(e.Total)}. Era única; ya no está en venta.", ct)
        : Task.CompletedTask; // auction sales are already told by the hammer

    private async Task WriteAsync(IntegrationEvent e, Guid pieceId, string title, string body, CancellationToken ct)
    {
        if (await context.Entries.AnyAsync(x => x.SourceEventId == e.Id, ct))
        {
            return;
        }

        context.Entries.Add(new Entry
        {
            Id = Guid.CreateVersion7(), Kind = EntryKind.Chronicle, Title = title, Body = body, Author = "Musa",
            PublishedAt = e.OccurredAt, PieceId = pieceId, SourceEventId = e.Id,
        });
        await context.SaveChangesAsync(ct);
    }

    private static string Usd(decimal amount) => amount.ToString("N0", Es) + " USD";
}

public static class FirstIssue
{
    public static IEnumerable<Entry> Entries(DateTimeOffset now) =>
    [
        new()
        {
            Id = Guid.CreateVersion7(), Kind = EntryKind.Essay, Author = "La redacción", PublishedAt = now.AddDays(-6),
            Title = "Lo que un martillo no dice",
            Body = "Una subasta dura noventa segundos y produce una sola cifra. Esa cifra parece una verdad y es apenas un acuerdo: "
                 + "entre dos personas que querían lo mismo esa tarde, en esa sala, con ese dinero.\n\n"
                 + "La estimación es una opinión, la reserva es un secreto y el precio de martillo es una anécdota. "
                 + "El valor está en otra parte: en la procedencia —quién la tuvo, dónde se colgó, quién la miró— y en el tiempo que alguien "
                 + "esté dispuesto a pasar delante de ella. Por eso aquí cada obra lleva su historia encadenada, línea a línea, y nadie puede reescribirla.",
        },
        new()
        {
            Id = Guid.CreateVersion7(), Kind = EntryKind.Essay, Author = "La redacción", PublishedAt = now.AddDays(-3),
            Title = "Elogio de la costura invisible",
            Body = "En un abrigo de doble faz no hay forro que esconda nada. Las dos telas se abren al canto, se doblan hacia dentro y se cierran "
                 + "a mano con una puntada que no debe verse por ninguno de los dos lados. Catorce horas de aguja para que parezca que no hubo ninguna.\n\n"
                 + "El lujo, cuando es serio, es exactamente eso: trabajo que se toma la molestia de desaparecer. "
                 + "Lo mismo vale para un mueble sin tornillos a la vista y para un sistema que no se cae cuando falla una de sus partes.",
        },
        new()
        {
            Id = Guid.CreateVersion7(), Kind = EntryKind.Note, Author = "Cuaderno", PublishedAt = now.AddDays(-1),
            Title = "Nota del día",
            Body = "El yakisugi no es un acabado: es una decisión. Se quema la madera para que el fuego ya no pueda con ella.",
        },
    ];
}
