using JobRadar.Application.Abstractions;
using JobRadar.Infrastructure.Persistence;
using JobRadar.Infrastructure.Persistence.Repositories;
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
        services.AddScoped<IJobPostingRepository, JobPostingRepository>();
        services.AddScoped<IJobSourceRepository, JobSourceRepository>();
        services.AddScoped<IApplicationRecordRepository, ApplicationRecordRepository>();

        // ── Unit of Work ──────────────────────────────────────────────────────
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // ── Services ──────────────────────────────────────────────────────────
        services.AddScoped<IJobIngestionService, JobRadar.Infrastructure.Services.JobIngestionService>();

        return services;
    }
}
