using BugReport.Server.Api.Endpoints;
using BugReport.Server.Api.Json;
using BugReport.Server.Api.Models;
using BugReport.Server.Api.Storage;
using DbUp;
using Npgsql;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Services.ConfigureHttpJsonOptions(opts =>
{
    opts.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonContext.Default);
});

var connStr = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("缺少 ConnectionStrings__Postgres");

builder.Services.AddSingleton(new NpgsqlDataSourceBuilder(connStr).Build());
builder.Services.AddSingleton<IReportStore, PostgresReportStore>();

var app = builder.Build();

// DbUp migration: v1 single-replica – run on startup.
// For multi-replica, move this to a one-shot job to avoid concurrent migration races.
{
    var upgrader = DeployChanges.To
        .PostgresqlDatabase(connStr)
        .WithScriptsEmbeddedInAssembly(typeof(Program).Assembly)
        .WithTransaction()
        .LogToConsole()
        .Build();
    var result = upgrader.PerformUpgrade();
    if (!result.Successful)
        throw new Exception("DB migration failed: " + result.Error);
}

app.MapGet("/health", () => Results.Ok(new HealthResponse { Status = "ok" }));

app.MapIngestEndpoints();
app.MapQueryEndpoints();

app.Run();
