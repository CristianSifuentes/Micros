using Journal.API;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Musa.EventBus;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddNpgsqlDbContext<JournalContext>("journaldb");
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.ConfigureHttpJsonOptions(json =>
    json.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));

builder.AddEventBus("eventbus", queueName: "journal")
    .Subscribe<LotOpened, Chronicler>()
    .Subscribe<LotHammered, Chronicler>()
    .Subscribe<LotPassed, Chronicler>()
    .Subscribe<OrderPlaced, Chronicler>();

var app = builder.Build();

app.UseExceptionHandler();
app.MapDefaultEndpoints();
app.MapOpenApi();

app.MapGet("/entries", async (EntryKind? kind, JournalContext db, CancellationToken ct) =>
    TypedResults.Ok(await db.Entries.AsNoTracking()
        .Where(e => kind == null || e.Kind == kind)
        .OrderByDescending(e => e.PublishedAt).Take(60).ToListAsync(ct)));

app.MapPost("/notes", async Task<Results<Created<Entry>, ValidationProblem>> (NewNote note, JournalContext db, TimeProvider time, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(note.Body) || note.Body.Length > 2_000)
    {
        return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["body"] = ["A note needs between 1 and 2,000 characters."] });
    }

    var entry = new Entry
    {
        Id = Guid.CreateVersion7(), Kind = EntryKind.Note, Title = string.IsNullOrWhiteSpace(note.Title) ? "Nota del día" : note.Title.Trim(),
        Body = note.Body.Trim(), Author = string.IsNullOrWhiteSpace(note.Author) ? "Cuaderno" : note.Author.Trim(), PublishedAt = time.GetUtcNow(),
    };
    db.Entries.Add(entry);
    await db.SaveChangesAsync(ct);
    return TypedResults.Created($"/entries/{entry.Id}", entry);
});

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<JournalContext>();
    await db.Database.EnsureCreatedAsync();
    if (!await db.Entries.AnyAsync())
    {
        db.Entries.AddRange(FirstIssue.Entries(DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();
    }
}

app.Run();

public sealed record NewNote(string? Title, string Body, string? Author);

public partial class Program;
