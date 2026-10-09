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
                npgsql => npgsql.UseVector()
                    .MigrationsAssembly(typeof(AppDbContext).Assembly.FullName)
            ));

        // ── Repositories ──────────────────────────────────────────────────────
        services.AddScoped<ICompanyRepository, CompanyRepository>();
        services.AddScoped<IJobRepository, JobRepository>();
        services.AddScoped<ISourceRepository, SourceRepository>();
        services.AddScoped<IRawPostRepository, RawPostRepository>();
        services.AddScoped<IUserJobApplicationRepository, UserJobApplicationRepository>();
        services.AddScoped<IJobApplicationQuestionRepository, JobApplicationQuestionRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();

        // ── Unit of Work ──────────────────────────────────────────────────────
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // ── Services ──────────────────────────────────────────────────────────
        services.AddScoped<IJobIngestionService, JobRadar.Infrastructure.Services.JobIngestionService>();
        services.AddScoped<ISourceFetcher, JobRadar.Infrastructure.Fetchers.RssFetcher>();
        services.AddScoped<ISourceFetcher, JobRadar.Infrastructure.Fetchers.TelegramFetcher>();
        services.AddScoped<ISourceFetcher, JobRadar.Infrastructure.Fetchers.CompanyCareersPageFetcher>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        
        // ── Storage Services (Cloudflare R2 via S3 SDK) ───────────────────────
        services.Configure<JobRadar.Infrastructure.Services.Storage.R2StorageSettings>(
            configuration.GetSection(JobRadar.Infrastructure.Services.Storage.R2StorageSettings.SectionName));
        services.AddScoped<IFileStorageService, JobRadar.Infrastructure.Services.Storage.R2FileStorageService>();

        // ── Caching ──────────────────────────────────────────────────────────
        services.AddDistributedMemoryCache();

        // ── Gemini AI Services ────────────────────────────────────────────────
        services.AddSingleton<JobRadar.Infrastructure.Prompts.IGeminiPromptProvider, JobRadar.Infrastructure.Prompts.GeminiPromptProvider>();
        services.AddOptions<CvMatchWeightsOptions>()
            .Bind(configuration.GetSection(CvMatchWeightsOptions.SectionName))
            .Validate(options => options.ValidateWeights(), "Gemini:CvMatch:Weights sum must equal 100")
            .ValidateOnStart();

        // Named HttpClient used by both Gemini services (configurable via Gemini:TimeoutSeconds)
        var geminiTimeoutSeconds = configuration.GetValue<int>("Gemini:TimeoutSeconds", 120);
        services.AddHttpClient("Gemini", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(geminiTimeoutSeconds);
        });

        services.AddScoped<ILlmExtractionService, GeminiExtractionService>();
        services.AddScoped<IEmbeddingService, GeminiEmbeddingService>();
        services.AddScoped<IDocumentTextExtractionService, DocumentTextExtractionService>();
        services.AddScoped<ICvMatchAnalysisService, GeminiCvMatchAnalysisService>();
        services.AddScoped<JobRadar.Application.Common.Search.IQueryUnderstandingService, QueryUnderstandingService>();

        // ── Web Crawlers & Automated Periodic Ingestion ───────────────────────
        services.AddHttpClient("JobCrawler", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(35);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0.0.0 Safari/537.36");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json, text/html, application/xhtml+xml, application/xml;q=0.9, */*;q=0.8");
        });

        // Global & Top-Tier Remote Crawlers
        services.AddScoped<IJobCrawlerProvider, JobRadar.Infrastructure.Crawlers.ArbeitnowCrawlerProvider>();
        services.AddScoped<IJobCrawlerProvider, JobRadar.Infrastructure.Crawlers.RemotiveCrawlerProvider>();
        services.AddScoped<IJobCrawlerProvider, JobRadar.Infrastructure.Crawlers.WeWorkRemotelyRssCrawlerProvider>();
        services.AddScoped<IJobCrawlerProvider, JobRadar.Infrastructure.Crawlers.RemoteOkCrawlerProvider>();
        services.AddScoped<IJobCrawlerProvider, JobRadar.Infrastructure.Crawlers.HimalayasCrawlerProvider>();
        services.AddScoped<IJobCrawlerProvider, JobRadar.Infrastructure.Crawlers.JobicyCrawlerProvider>();
        services.AddScoped<IJobCrawlerProvider, JobRadar.Infrastructure.Crawlers.JobspressoCrawlerProvider>();

        // MENA Regional, Jordan & Gulf Market Crawlers
        services.AddScoped<IJobCrawlerProvider, JobRadar.Infrastructure.Crawlers.BankOfJordanCrawlerProvider>();
        services.AddScoped<IJobCrawlerProvider, JobRadar.Infrastructure.Crawlers.AkhtabootCrawlerProvider>();
        services.AddScoped<IJobCrawlerProvider, JobRadar.Infrastructure.Crawlers.BaytJordanCrawlerProvider>();
        services.AddScoped<IJobCrawlerProvider, JobRadar.Infrastructure.Crawlers.TanqeebMenaCrawlerProvider>();

        services.AddSingleton<IJobEmbeddingChannel, JobRadar.Infrastructure.BackgroundServices.JobEmbeddingChannel>();
        services.AddScoped<IJobRealtimeNotifier, JobRadar.Infrastructure.Services.NullJobRealtimeNotifier>();

        // ── MediatR Handlers in Infrastructure ─────────────────────────────────
        services.AddTransient<MediatR.IRequestHandler<JobRadar.Application.Features.JobApplications.Queries.GetAllApplications.GetAllApplicationsQuery, JobRadar.Application.Features.JobApplications.Queries.GetAllApplications.GetAllApplicationsResponse>, JobRadar.Infrastructure.Features.JobApplications.GetAllApplicationsQueryHandler>();

        return services;
    }
}

