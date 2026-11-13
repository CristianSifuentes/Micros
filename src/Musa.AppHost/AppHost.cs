// Musa, movement I. One command raises the whole house: a database per bounded context,
// the broker they speak through, five services and the gateway that is the only door to the street.
var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres");
var eventBus = builder.AddRabbitMQ("eventbus").WithManagementPlugin();

var catalogDb = postgres.AddDatabase("catalogdb");
var auctionsDb = postgres.AddDatabase("auctionsdb");
var orderingDb = postgres.AddDatabase("orderingdb");
var provenanceDb = postgres.AddDatabase("provenancedb");
var journalDb = postgres.AddDatabase("journaldb");

// Each service is handed its own database and nothing else. That is the whole rule.
var catalog = builder.AddProject<Projects.Catalog_API>("catalog-api")
    .WithReference(catalogDb).WaitFor(catalogDb).WithReference(eventBus).WaitFor(eventBus);

var auctions = builder.AddProject<Projects.Auctions_API>("auctions-api")
    .WithReference(auctionsDb).WaitFor(auctionsDb).WithReference(eventBus).WaitFor(eventBus);

var ordering = builder.AddProject<Projects.Ordering_API>("ordering-api")
    .WithReference(orderingDb).WaitFor(orderingDb).WithReference(eventBus).WaitFor(eventBus);

var provenance = builder.AddProject<Projects.Provenance_API>("provenance-api")
    .WithReference(provenanceDb).WaitFor(provenanceDb).WithReference(eventBus).WaitFor(eventBus);

var journal = builder.AddProject<Projects.Journal_API>("journal-api")
    .WithReference(journalDb).WaitFor(journalDb).WithReference(eventBus).WaitFor(eventBus);

builder.AddProject<Projects.Musa_Gateway>("gateway")
    .WithExternalHttpEndpoints()
    .WithReference(catalog).WithReference(auctions).WithReference(ordering)
    .WithReference(provenance).WithReference(journal)
    .WaitFor(catalog).WaitFor(auctions).WaitFor(ordering).WaitFor(provenance).WaitFor(journal);

builder.Build().Run();
