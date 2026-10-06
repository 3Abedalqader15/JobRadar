using JobRadar.Domain.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MassTransit;
using Pgvector.EntityFrameworkCore;
using System.Reflection;

namespace JobRadar.Infrastructure.Persistence;

public sealed class AppDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>
{
    // ── Core Entities ─────────────────────────────────────────────────────────
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Source> Sources => Set<Source>();
    public DbSet<RawPost> RawPosts => Set<RawPost>();
    public DbSet<Job> Jobs => Set<Job>();

    // ── Lookup / Skill ────────────────────────────────────────────────────────
    public DbSet<Skill> Skills => Set<Skill>();
    public DbSet<JobSkillMap> JobSkillMaps => Set<JobSkillMap>();

    // ── User Activity ─────────────────────────────────────────────────────────
    public DbSet<UserSavedJob> UserSavedJobs => Set<UserSavedJob>();
    public DbSet<UserJobApplication> UserJobApplications => Set<UserJobApplication>();
    public DbSet<JobApplicationQuestion> JobApplicationQuestions => Set<JobApplicationQuestion>();
    public DbSet<JobApplicationAnswer> JobApplicationAnswers => Set<JobApplicationAnswer>();
    public DbSet<Notification> Notifications => Set<Notification>();

    // ── CV & ATS ──────────────────────────────────────────────────────────────
    public DbSet<CvTemplate> CvTemplates => Set<CvTemplate>();
    public DbSet<Cv> Cvs => Set<Cv>();
    public DbSet<CvJobMatchAnalysis> CvJobMatchAnalyses => Set<CvJobMatchAnalysis>();

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationRole>();

        // Enable the pgvector extension in PostgreSQL (when not running InMemory)
        if (Database.ProviderName != "Microsoft.EntityFrameworkCore.InMemory")
        {
            builder.HasPostgresExtension("vector");
        }

        // Apply all IEntityTypeConfiguration<T> from this assembly
        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        if (Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory")
        {
            builder.Entity<Job>().Ignore(j => j.Embedding);
            builder.Entity<UserJobApplication>()
                .Property(a => a.AiMissingKeywords)
                .HasConversion(
                    v => v == null ? null : string.Join(';', v),
                    v => v == null ? null : v.Split(';', StringSplitOptions.RemoveEmptyEntries));
        }

        builder.AddInboxStateEntity();
        builder.AddOutboxMessageEntity();
        builder.AddOutboxStateEntity();
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
