using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Musa.EventBus;

namespace Provenance.API;

public sealed record PieceRegistered(Guid PieceId, string Title, string Creator, string Medium, int Year) : IntegrationEvent;
public sealed record LotOpened(int LotId, Guid PieceId, decimal EstimateLow, decimal EstimateHigh) : IntegrationEvent;
public sealed record LotHammered(int LotId, Guid PieceId, string Paddle, decimal HammerPrice, int BidCount) : IntegrationEvent;
public sealed record LotPassed(int LotId, Guid PieceId) : IntegrationEvent;
public sealed record OrderPlaced(Guid PieceId, string Paddle, string Channel, decimal Total) : IntegrationEvent;

/// <summary>Writes the history of every piece from what the rest of Musa announces. It asks nobody anything.</summary>
public sealed class Registrar(ProvenanceContext context) :
    IIntegrationEventHandler<PieceRegistered>, IIntegrationEventHandler<LotOpened>, IIntegrationEventHandler<LotHammered>,
    IIntegrationEventHandler<LotPassed>, IIntegrationEventHandler<OrderPlaced>
{
    private static readonly CultureInfo Es = CultureInfo.GetCultureInfo("es-MX");

    public Task HandleAsync(PieceRegistered e, CancellationToken ct) => RecordAsync(e, e.PieceId, "Registro",
        $"«{e.Title}», de {e.Creator} ({e.Year}, {e.Medium}), entra en el registro de Musa.", ct);

    public Task HandleAsync(LotOpened e, CancellationToken ct) => RecordAsync(e, e.PieceId, "Consignación",
        $"Consignada a subasta como lote {e.LotId}, con una estimación de {Usd(e.EstimateLow)} a {Usd(e.EstimateHigh)}.", ct);

    public Task HandleAsync(LotHammered e, CancellationToken ct) => RecordAsync(e, e.PieceId, "Remate",
        $"Rematada en el lote {e.LotId} por {Usd(e.HammerPrice)} tras {e.BidCount} pujas. Paleta {e.Paddle}.", ct);

    public Task HandleAsync(LotPassed e, CancellationToken ct) => RecordAsync(e, e.PieceId, "Sin remate",
        $"El lote {e.LotId} no alcanza la reserva y la obra vuelve a su consignante.", ct);

    public Task HandleAsync(OrderPlaced e, CancellationToken ct) => RecordAsync(e, e.PieceId, "Adquisición",
        e.Channel == "Auction"
            ? $"Adquirida en subasta por la paleta {e.Paddle}. Total con comisión: {Usd(e.Total)}."
            : $"Adquirida en la boutique por la paleta {e.Paddle} por {Usd(e.Total)}.", ct);

    private async Task RecordAsync(IntegrationEvent e, Guid pieceId, string kind, string statement, CancellationToken ct)
    {
        if (await context.Entries.AnyAsync(x => x.SourceEventId == e.Id, ct))
        {
            return;
        }

        var last = await context.Entries.Where(x => x.PieceId == pieceId).OrderByDescending(x => x.Sequence).FirstOrDefaultAsync(ct);
        context.Entries.Add(LedgerEntry.Append(last, pieceId, kind, statement, e.OccurredAt, e.Id));
        await context.SaveChangesAsync(ct);
    }

    private static string Usd(decimal amount) => amount.ToString("N0", Es) + " USD";
}
