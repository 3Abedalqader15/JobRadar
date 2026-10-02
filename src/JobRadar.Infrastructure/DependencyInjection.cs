using JobRadar.Application.Abstractions;
using JobRadar.Infrastructure.Persistence;
using JobRadar.Infrastructure.Persistence.Repositories;
using JobRadar.Infrastructure.Services;
using JobRadar.Infrastructure.Services.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JobRadar.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ── EF Core / PostgreSQL ──────────────────────────────────────────────
        var connectionString = configuration.GetConnectionString("PostgreSQL")
            ?? throw new InvalidOperationException(
                "Connection string 'PostgreSQL' is missing from configuration.");

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(
                connectionString,
                npgsql => npgsql.UseVector()  // enable pgvector support
            ));

        // ── Repositories ──────────────────────────────────────────────────────
        services.AddScoped<IJobRepository, JobRepository>();
        services.AddScoped<ISourceRepository, SourceRepository>();
        services.AddScoped<IRawPostRepository, RawPostRepository>();
        services.AddScoped<IUserJobApplicationRepository, UserJobApplicationRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();

        // ── Unit of Work ──────────────────────────────────────────────────────
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // ── Services ──────────────────────────────────────────────────────────
        services.AddScoped<IJobIngestionService, JobRadar.Infrastructure.Services.JobIngestionService>();
        services.AddScoped<ISourceFetcher, JobRadar.Infrastructure.Fetchers.RssFetcher>();
        services.AddScoped<ISourceFetcher, JobRadar.Infrastructure.Fetchers.TelegramFetcher>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        
        // ── Caching ──────────────────────────────────────────────────────────
        services.AddDistributedMemoryCache();

        // ── Gemini AI Services ────────────────────────────────────────────────
        // Named HttpClient used by both Gemini services
        services.AddHttpClient("Gemini", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(60);
        });

        services.AddScoped<ILlmExtractionService, GeminiExtractionService>();
        services.AddScoped<IEmbeddingService, GeminiEmbeddingService>();

        // ── Web Crawlers & Automated Periodic Ingestion ───────────────────────
        services.AddHttpClient("JobCrawler", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) JobRadarCrawler/1.0 (+https://jobradar.io)");
        });

        services.AddScoped<IJobCrawlerProvider, JobRadar.Infrastructure.Crawlers.ArbeitnowCrawlerProvider>();
        services.AddScoped<IJobCrawlerProvider, JobRadar.Infrastructure.Crawlers.RemotiveCrawlerProvider>();
        services.AddScoped<IJobCrawlerProvider, JobRadar.Infrastructure.Crawlers.WeWorkRemotelyRssCrawlerProvider>();

        services.AddScoped<IAutomatedJobIngestionService, JobRadar.Infrastructure.Services.AutomatedJobIngestionService>();
        services.AddScoped<IJobRealtimeNotifier, JobRadar.Infrastructure.Services.NullJobRealtimeNotifier>();

        services.AddHostedService<JobRadar.Infrastructure.BackgroundServices.PeriodicJobCrawlerBackgroundService>();

        return services;
    }
}

