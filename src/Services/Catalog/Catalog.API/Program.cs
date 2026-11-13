using Catalog.API;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Musa.EventBus;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddNpgsqlDbContext<CatalogContext>("catalogdb");
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddOutputCache();
builder.Services.ConfigureHttpJsonOptions(json =>
    json.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));

builder.AddEventBus("eventbus", queueName: "catalog")
    .WithOutbox<CatalogContext>()
    .Subscribe<LotOpened, AvailabilityHandler>()
    .Subscribe<LotPassed, AvailabilityHandler>()
    .Subscribe<OrderPlaced, AvailabilityHandler>();

var app = builder.Build();

app.UseExceptionHandler();
app.UseOutputCache();
app.MapDefaultEndpoints();
app.MapOpenApi();

var pieces = app.MapGroup("/pieces");

pieces.MapGet("/", async (Discipline? discipline, CatalogContext db, CancellationToken ct) =>
{
    var found = await db.Pieces.AsNoTracking()
        .Where(p => discipline == null || p.Discipline == discipline)
        .OrderBy(p => p.Discipline).ThenBy(p => p.Title)
        .ToListAsync(ct);
    return TypedResults.Ok(found.Select(PieceView.From));
});

pieces.MapGet("/{id:guid}", async Task<Results<Ok<PieceView>, NotFound>> (Guid id, CatalogContext db, CancellationToken ct) =>
    await db.Pieces.AsNoTracking().SingleOrDefaultAsync(p => p.Id == id, ct) is { } piece
        ? TypedResults.Ok(PieceView.From(piece))
        : TypedResults.NotFound());

pieces.MapGet("/{id:guid}/artwork.svg", async Task<Results<ContentHttpResult, NotFound>> (Guid id, CatalogContext db, CancellationToken ct) =>
{
    var discipline = await db.Pieces.Where(p => p.Id == id).Select(p => (Discipline?)p.Discipline).SingleOrDefaultAsync(ct);
    return discipline is { } known
        ? TypedResults.Content(GenerativeArtwork.Render(id, known), "image/svg+xml")
        : TypedResults.NotFound();
}).CacheOutput(policy => policy.Expire(TimeSpan.FromDays(1)));

pieces.MapPost("/", async Task<Results<Created<PieceView>, ValidationProblem>> (RegisterPiece request, CatalogContext db, CancellationToken ct) =>
{
    var hasEstimate = request is { EstimateLow: > 0, EstimateHigh: > 0 } && request.EstimateHigh >= request.EstimateLow;
    if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Creator) || !(hasEstimate ^ request.Price > 0))
    {
        return TypedResults.ValidationProblem(new Dictionary<string, string[]>
        {
            ["piece"] = ["A piece needs a title, a creator, and either an estimate (for auction) or a price (for the boutique)."],
        });
    }

    var piece = new Piece
    {
        Id = Guid.CreateVersion7(), Title = request.Title.Trim(), Creator = request.Creator.Trim(),
        Discipline = request.Discipline, Medium = request.Medium, Year = request.Year, Dimensions = request.Dimensions,
        Story = request.Story, EstimateLow = hasEstimate ? request.EstimateLow : null,
        EstimateHigh = hasEstimate ? request.EstimateHigh : null, Price = hasEstimate ? null : request.Price,
    };
    db.Pieces.Add(piece);
    db.Enqueue(PieceRegistered.From(piece)); // same transaction as the piece itself
    await db.SaveChangesAsync(ct);
    return TypedResults.Created($"/pieces/{piece.Id}", PieceView.From(piece));
});

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CatalogContext>();
    await db.Database.EnsureCreatedAsync(); // development shortcut; production migrates as a deployment step
    if (!await db.Pieces.AnyAsync())
    {
        foreach (var piece in Collection.Opening())
        {
            db.Pieces.Add(piece);
            db.Enqueue(PieceRegistered.From(piece));
        }

        await db.SaveChangesAsync();
    }
}

app.Run();

public partial class Program;
