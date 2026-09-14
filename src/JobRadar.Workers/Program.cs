using Hangfire;
using Hangfire.PostgreSql;
using JobRadar.Application;
using JobRadar.Infrastructure;
using JobRadar.Workers.Jobs;
using Serilog;

var builder = Host.CreateDefaultBuilder(args);

builder.UseSerilog((ctx, lc) =>
    lc.ReadFrom.Configuration(ctx.Configuration));

builder.ConfigureServices((ctx, services) =>
{
    var configuration = ctx.Configuration;

    // ── Application + Infrastructure ──────────────────────────────────────
    services.AddApplication();
    services.AddInfrastructure(configuration);

    // ── Hangfire ──────────────────────────────────────────────────────────
    var hangfireConn = configuration.GetConnectionString("Hangfire")
        ?? configuration.GetConnectionString("PostgreSQL")
        ?? throw new InvalidOperationException("Hangfire connection string is missing.");

    services.AddHangfire(cfg => cfg
        .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
        .UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings()
        .UsePostgreSqlStorage(
            options => options.UseNpgsqlConnection(hangfireConn),
            new PostgreSqlStorageOptions
            {
                SchemaName = "hangfire",
                PrepareSchemaIfNecessary = true,
                QueuePollInterval = TimeSpan.FromSeconds(15)
            }));

    services.AddHangfireServer(options =>
    {
        options.WorkerCount = Environment.ProcessorCount * 2;
        options.Queues = new[] { "default", "ingestion", "cleanup" };
    });

    // ── Register job classes for DI ───────────────────────────────────────
    services.AddScoped<JobIngestionJob>();
    services.AddScoped<StaleJobCleanupJob>();
});

var host = builder.Build();

// ── Schedule recurring jobs ────────────────────────────────────────────────
using (var scope = host.Services.CreateScope())
{
    // Every 6 hours: ingest all enabled sources
    RecurringJob.AddOrUpdate<JobIngestionJob>(
        recurringJobId: "job-ingestion-all-sources",
        queue: "ingestion",
        methodCall: job => job.ExecuteAsync(CancellationToken.None),
        cronExpression: "0 */6 * * *",
        new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });

    // Every day at midnight: remove stale postings
    RecurringJob.AddOrUpdate<StaleJobCleanupJob>(
        recurringJobId: "stale-job-cleanup",
        queue: "cleanup",
        methodCall: job => job.ExecuteAsync(CancellationToken.None),
        cronExpression: Cron.Daily(),
        new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });
}

await host.RunAsync();
