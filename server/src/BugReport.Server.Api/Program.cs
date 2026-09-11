using BugReport.Server.Api.Endpoints;
using BugReport.Server.Api.Json;
using BugReport.Server.Api.Models;
using BugReport.Server.Api.Storage;
using DbUp;
using Npgsql;

var builder = WebApplication.CreateSlimBuilder(args);

// =================== 【迁移入口：--migrate 或 RUN_MIGRATION_ONLY=true】 ===================
var runMigrationOnly = args.Contains("--migrate")
    || string.Equals(Environment.GetEnvironmentVariable("RUN_MIGRATION_ONLY"), "true", StringComparison.OrdinalIgnoreCase);

if (runMigrationOnly)
{
    var migrateConn = builder.Configuration.GetConnectionString("Postgres")
        ?? throw new InvalidOperationException("缺少 ConnectionStrings__Postgres");
    Console.WriteLine("Executing database migrations (BugReport)...");
    var upgrader = DeployChanges.To
        .PostgresqlDatabase(migrateConn)
        .WithScriptsEmbeddedInAssembly(typeof(Program).Assembly)
        .WithTransaction()
        .LogToConsole()
        .Build();
    var result = upgrader.PerformUpgrade();
    if (!result.Successful)
    {
        Console.Error.WriteLine($"DB migration failed: {result.Error}");
        Environment.Exit(1);
    }
    Console.WriteLine("Migration completed successfully.");
    return; // 只跑迁移，不启动 Web
}
// ========================================================================================

builder.Services.ConfigureHttpJsonOptions(opts =>
{
    opts.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonContext.Default);
});

var connStr = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("缺少 ConnectionStrings__Postgres");

builder.Services.AddSingleton(new NpgsqlDataSourceBuilder(connStr).Build());
builder.Services.AddSingleton<IReportStore, PostgresReportStore>();

var app = builder.Build();

// 正常启动路径不再执行迁移。由 bugreport-migrate Job 或 --migrate 完成。
// （原「启动时自动迁移」已移除，避免多副本竞态）

app.MapGet("/health", () => Results.Ok(new HealthResponse { Status = "ok" }));

app.MapIngestEndpoints();
app.MapQueryEndpoints();

app.Run();
