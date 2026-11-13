using Auctions.API.Apis;
using Auctions.API.Application;
using Auctions.API.Application.IntegrationEvents;
using Auctions.Domain.AggregatesModel.LotAggregate;
using Auctions.Infrastructure;
using Auctions.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Musa.EventBus;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<SaleRoomExceptionHandler>();
builder.Services.AddOpenApi();

// Registered by hand rather than pooled: the context takes the domain event sink in its constructor.
builder.Services.AddDbContext<AuctionsContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("auctionsdb"), npgsql => npgsql.EnableRetryOnFailure()));
builder.EnrichNpgsqlDbContext<AuctionsContext>();

builder.Services.AddSingleton<LiveFeed>();
builder.Services.AddSingleton<IDomainEventSink, SaleRoomEventSink>();
builder.Services.AddScoped<ILotRepository, LotRepository>();
builder.Services.AddHostedService<Auctioneer>();

builder.AddEventBus("eventbus", queueName: "auctions")
    .WithOutbox<AuctionsContext>()
    .Subscribe<PieceRegistered, PieceRegisteredHandler>();

var app = builder.Build();

app.UseExceptionHandler();
app.MapDefaultEndpoints();
app.MapOpenApi();
app.MapLotsApi();

await using (var scope = app.Services.CreateAsyncScope())
{
    // Development shortcut. Production applies migrations as a separate deployment step.
    await scope.ServiceProvider.GetRequiredService<AuctionsContext>().Database.EnsureCreatedAsync();
}

app.Run();

public partial class Program;
