using JobRadar.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;
using System.Reflection;

namespace JobRadar.Infrastructure.Persistence;

public sealed class AppDbContext : DbContext
{
    // ── Core Entities ─────────────────────────────────────────────────────────
    public DbSet<User> Users => Set<User>();
    public DbSet<Source> Sources => Set<Source>();
    public DbSet<RawPost> RawPosts => Set<RawPost>();
    public DbSet<Job> Jobs => Set<Job>();

    // ── Lookup / Skill ────────────────────────────────────────────────────────
    public DbSet<Skill> Skills => Set<Skill>();
    public DbSet<JobSkillMap> JobSkillMaps => Set<JobSkillMap>();

    // ── User Activity ─────────────────────────────────────────────────────────
    public DbSet<UserSavedJob> UserSavedJobs => Set<UserSavedJob>();
    public DbSet<UserJobApplication> UserJobApplications => Set<UserJobApplication>();
    public DbSet<Notification> Notifications => Set<Notification>();

    // ── CV & ATS ──────────────────────────────────────────────────────────────
    public DbSet<CvTemplate> CvTemplates => Set<CvTemplate>();
    public DbSet<Cv> Cvs => Set<Cv>();
    public DbSet<CvJobMatchAnalysis> CvJobMatchAnalyses => Set<CvJobMatchAnalysis>();

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
