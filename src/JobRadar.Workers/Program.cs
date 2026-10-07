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

builder.UseWindowsService();

builder.ConfigureAppConfiguration((ctx, config) =>
{
    config.SetBasePath(AppContext.BaseDirectory);
    config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
    config.AddJsonFile($"appsettings.{ctx.HostingEnvironment.EnvironmentName}.json", optional: true, reloadOnChange: true);
    config.AddUserSecrets(typeof(Program).Assembly, optional: true);
    config.AddEnvironmentVariables();
});

builder.UseSerilog((ctx, lc) =>
    lc.ReadFrom.Configuration(ctx.Configuration));

builder.ConfigureServices((ctx, services) =>
{
    var configuration = ctx.Configuration;

    // ── Application + Infrastructure ──────────────────────────────────────
    services.AddApplication();
    services.AddInfrastructure(configuration);

    // ── Identity & Context for MediatR handlers in Worker process ────────
    services.AddIdentityCore<JobRadar.Domain.Entities.ApplicationUser>()
        .AddRoles<JobRadar.Domain.Entities.ApplicationRole>()
        .AddEntityFrameworkStores<JobRadar.Infrastructure.Persistence.AppDbContext>();

    services.AddScoped<JobRadar.Application.Abstractions.ICurrentUserService, JobRadar.Infrastructure.Services.BackgroundWorkerCurrentUserService>();

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
        options.WorkerCount = 2;
        options.Queues = WorkerQueues.Names;
    });

    // ── MassTransit with Fail-Fast RabbitMQ ─────────────────────────────────
    var rabbitMqConn = configuration.GetConnectionString("RabbitMQ");
    if (string.IsNullOrWhiteSpace(rabbitMqConn) || rabbitMqConn == "in-memory")
    {
        throw new InvalidOperationException("RabbitMQ connection string not configured — refusing to silently fall back to in-memory transport, which breaks outbox durability guarantees.");
    }

    services.AddMassTransit(x =>
    {
        // Register the consumers so MassTransit discovers them automatically
        x.AddConsumer<RawPostProcessingConsumer>();
        x.AddConsumer<JobRadar.Infrastructure.Consumers.CvAnalysisConsumer>();

        x.AddEntityFrameworkOutbox<AppDbContext>(o =>
        {
            o.UsePostgres();
            o.UseBusOutbox();
        });

        x.UsingRabbitMq((context, cfg) =>
        {
            cfg.Host(rabbitMqConn);

            // Dedicated queue for raw-post processing
            cfg.ReceiveEndpoint("raw-post-processing", e =>
            {
                e.ConcurrentMessageLimit = 1;
                e.UseRateLimit(4, TimeSpan.FromMinutes(1));
                e.ConfigureConsumer<RawPostProcessingConsumer>(context);
            });

            // Dedicated queue for CV analysis
            cfg.ReceiveEndpoint("cv-analysis-processing", e =>
            {
                e.ConcurrentMessageLimit = 1;
                e.ConfigureConsumer<JobRadar.Infrastructure.Consumers.CvAnalysisConsumer>(context);
            });
        });
    });

    // ── Embedding batch processor (background service) ────────────────────
    services.AddHostedService<EmbeddingBatchProcessor>();

    // ── Register job classes and services for DI ─────────────────────────
    services.AddScoped<JobIngestionJob>();
    services.AddScoped<StaleJobCleanupJob>();
    services.AddScoped<ExternalJobCrawlDispatcherJob>();
    services.AddScoped<RawPostBacklogSweepJob>();
    
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

    // Every 5 minutes: sweep and process pending raw posts backlog
    RecurringJob.AddOrUpdate<RawPostBacklogSweepJob>(
        recurringJobId: "raw-post-backlog-sweep",
        queue: "ingestion",
        methodCall: job => job.ExecuteAsync(CancellationToken.None),
        cronExpression: "*/5 * * * *",
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

// Defense-in-depth: run initial sweep once on startup asynchronously
_ = Task.Run(async () =>
{
    try
    {
        await Task.Delay(3000); // Wait for host and bus to fully initialize
        using var sweepScope = host.Services.CreateScope();
        var sweepJob = sweepScope.ServiceProvider.GetRequiredService<RawPostBacklogSweepJob>();
        await sweepJob.ExecuteAsync();
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Initial RawPostBacklogSweepJob execution encountered an error.");
    }
});

await host.RunAsync();

internal static class WorkerQueues
{
    public static readonly string[] Names = ["default", "ingestion", "cleanup"];
}
