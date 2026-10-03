using Hangfire;
using Hangfire.PostgreSql;
using JobRadar.Application;
using JobRadar.Infrastructure;
using JobRadar.Infrastructure.Persistence;
using JobRadar.Infrastructure.BackgroundServices;
using JobRadar.Infrastructure.Consumers;
using JobRadar.Workers.Jobs;
using JobRadar.Workers.Ingestion;
using JobRadar.Workers.Ingestion.Services;
using MassTransit;
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
        options.Queues = WorkerQueues.Names;
    });

    // ── MassTransit ──────────────────────────────────────────────────────────
    services.AddMassTransit(x =>
    {
        // Register the consumer so MassTransit discovers it automatically
        x.AddConsumer<RawPostProcessingConsumer>();

        x.AddEntityFrameworkOutbox<AppDbContext>(o =>
        {
            o.UsePostgres();
            o.UseBusOutbox();
        });

        x.UsingRabbitMq((context, cfg) =>
        {
            cfg.Host(configuration.GetConnectionString("RabbitMQ") ?? "amqp://guest:guest@localhost:5672");

            // Dedicated queue for raw-post processing
            cfg.ReceiveEndpoint("raw-post-processing", e =>
            {
                e.ConcurrentMessageLimit = 4; // don't hammer the LLM
                e.ConfigureConsumer<RawPostProcessingConsumer>(context);
            });
        });
    });

    // ── Embedding batch processor (background service) ────────────────────
    services.AddHostedService<EmbeddingBatchProcessor>();

    // ── Register job classes and services for DI ─────────────────────────
    services.AddScoped<JobIngestionJob>();
    services.AddScoped<StaleJobCleanupJob>();
    services.AddScoped<ExternalJobCrawlDispatcherJob>();
    
    services.AddScoped<RssFeedFetcher>();
    services.AddScoped<TelegramFetcher>();
    services.AddScoped<LinkedInScraper>();
    services.AddScoped<FetchSourceJob>();
    services.AddScoped<IngestionDispatcherJob>();
});

var host = builder.Build();

// ── Schedule recurring jobs ────────────────────────────────────────────────
using (var scope = host.Services.CreateScope())
{
    JobStorage.Current = scope.ServiceProvider.GetRequiredService<JobStorage>();
    var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();

    // Every minute: check if any source is due for fetching
    RecurringJob.AddOrUpdate<IngestionDispatcherJob>(
        recurringJobId: "ingestion-dispatcher",
        queue: "ingestion",
        methodCall: job => job.ExecuteAsync(),
        cronExpression: Cron.Minutely(),
        new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });

    // Recurring external job crawl dispatcher (configured interval)
    var crawlIntervalMinutes = config.GetValue<int>("JobCrawler:IntervalMinutes", 60);
    string crawlCron = crawlIntervalMinutes switch
    {
        <= 1 => Cron.Minutely(),
        < 60 => $"*/{crawlIntervalMinutes} * * * *",
        60 => Cron.Hourly(),
        _ => $"0 */{Math.Max(1, crawlIntervalMinutes / 60)} * * *"
    };

    RecurringJob.AddOrUpdate<ExternalJobCrawlDispatcherJob>(
        recurringJobId: "external-job-crawl-dispatcher",
        queue: "ingestion",
        methodCall: job => job.ExecuteAsync(CancellationToken.None),
        cronExpression: crawlCron,
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

internal static class WorkerQueues
{
    public static readonly string[] Names = ["default", "ingestion", "cleanup"];
}
