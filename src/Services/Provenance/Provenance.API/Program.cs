using Microsoft.EntityFrameworkCore;
using Musa.EventBus;
using Provenance.API;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddNpgsqlDbContext<ProvenanceContext>("provenancedb");
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

builder.AddEventBus("eventbus", queueName: "provenance")
    .Subscribe<PieceRegistered, Registrar>()
    .Subscribe<LotOpened, Registrar>()
    .Subscribe<LotHammered, Registrar>()
    .Subscribe<LotPassed, Registrar>()
    .Subscribe<OrderPlaced, Registrar>();

var app = builder.Build();

app.UseExceptionHandler();
app.MapDefaultEndpoints();
app.MapOpenApi();

app.MapGet("/pieces/{pieceId:guid}", async (Guid pieceId, ProvenanceContext db, CancellationToken ct) =>
{
    var entries = await db.Entries.AsNoTracking().Where(e => e.PieceId == pieceId).OrderBy(e => e.Sequence).ToListAsync(ct);
    return TypedResults.Ok(ProvenanceView.From(pieceId, entries));
});

await using (var scope = app.Services.CreateAsyncScope())
{
    await scope.ServiceProvider.GetRequiredService<ProvenanceContext>().Database.EnsureCreatedAsync();
}

app.Run();

public partial class Program;
