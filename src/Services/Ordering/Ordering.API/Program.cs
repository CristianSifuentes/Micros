using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Musa.EventBus;
using Ordering.API;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddNpgsqlDbContext<OrderingContext>("orderingdb");
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.ConfigureHttpJsonOptions(json =>
    json.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));

builder.AddEventBus("eventbus", queueName: "ordering")
    .WithOutbox<OrderingContext>()
    .Subscribe<PieceRegistered, PieceRegisteredHandler>()
    .Subscribe<LotHammered, LotHammeredHandler>();

var app = builder.Build();

app.UseExceptionHandler();
app.MapDefaultEndpoints();
app.MapOpenApi();

var orders = app.MapGroup("/orders");

orders.MapGet("/", async (string? paddle, OrderingContext db, CancellationToken ct) =>
    TypedResults.Ok(await db.Orders.AsNoTracking()
        .Where(o => paddle == null || o.Paddle == paddle)
        .OrderByDescending(o => o.PlacedAt).Take(100).ToListAsync(ct)));

// Boutique purchase. The price comes from this service's own copy of the piece, never from the client.
orders.MapPost("/", async Task<Results<Created<Order>, NotFound, Conflict<ProblemDetails>, UnprocessableEntity<ProblemDetails>>> (
    BuyRequest request, OrderingContext db, TimeProvider time, CancellationToken ct) =>
{
    var piece = await db.Pieces.AsNoTracking().SingleOrDefaultAsync(p => p.PieceId == request.PieceId, ct);
    if (piece is null) return TypedResults.NotFound();
    if (piece.Price is not { } price)
    {
        return TypedResults.UnprocessableEntity(new ProblemDetails { Title = "This piece is offered at auction, not in the boutique." });
    }

    var order = new Order
    {
        Id = Guid.CreateVersion7(), PieceId = piece.PieceId, Title = piece.Title, Creator = piece.Creator,
        Paddle = request.Paddle, Channel = SalesChannel.Boutique, Price = price, Total = price, PlacedAt = time.GetUtcNow(),
    };
    db.Orders.Add(order);
    db.Enqueue(OrderPlaced.From(order));
    try
    {
        await db.SaveChangesAsync(ct);
    }
    catch (DbUpdateException) when (db.Orders.AsNoTracking().Any(o => o.PieceId == piece.PieceId))
    {
        return TypedResults.Conflict(new ProblemDetails { Title = "This piece has just found its collector." });
    }

    return TypedResults.Created($"/orders/{order.Id}", order);
});

await using (var scope = app.Services.CreateAsyncScope())
{
    await scope.ServiceProvider.GetRequiredService<OrderingContext>().Database.EnsureCreatedAsync();
}

app.Run();

public sealed record BuyRequest(Guid PieceId, string Paddle);

public partial class Program;
