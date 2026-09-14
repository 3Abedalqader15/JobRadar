using JobRadar.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;
using System.Reflection;

namespace JobRadar.Infrastructure.Persistence;

public sealed class AppDbContext : DbContext
{
    public DbSet<JobPosting> JobPostings => Set<JobPosting>();
    public DbSet<JobSource> JobSources => Set<JobSource>();
    public DbSet<ApplicationRecord> ApplicationRecords => Set<ApplicationRecord>();

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Enable the pgvector extension in PostgreSQL
        modelBuilder.HasPostgresExtension("vector");

        // Apply all IEntityTypeConfiguration<T> from this assembly
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            // Fallback for design-time (migrations) — override with real connection string at runtime
            optionsBuilder.UseNpgsql(
                "Host=localhost;Database=jobradar;Username=postgres;Password=postgres",
                o => o.UseVector());
        }
    }
}
