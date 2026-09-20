using JobRadar.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pgvector;

namespace JobRadar.Infrastructure.Persistence.Configurations;

public sealed class JobConfiguration : IEntityTypeConfiguration<Job>
{
    public void Configure(EntityTypeBuilder<Job> builder)
    {
        builder.ToTable("jobs");

        builder.HasKey(j => j.Id);
        builder.Property(j => j.Id).HasColumnName("id");

        builder.Property(j => j.RawPostId)
            .HasColumnName("raw_post_id");

        builder.Property(j => j.SourceId)
            .HasColumnName("source_id")
            .IsRequired();

        builder.Property(j => j.Title)
            .HasColumnName("title")
            .HasMaxLength(300)
            .IsRequired();

        builder.Property(j => j.CompanyName)
            .HasColumnName("company_name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(j => j.Description)
            .HasColumnName("description")
            .IsRequired();

        builder.Property(j => j.Location)
            .HasColumnName("location")
            .HasMaxLength(200);

        builder.Property(j => j.IsRemote)
            .HasColumnName("is_remote")
            .HasDefaultValue(false);

        builder.Property(j => j.EmploymentType)
            .HasColumnName("employment_type")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(j => j.ExperienceLevel)
            .HasColumnName("experience_level")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(j => j.SalaryMin)
            .HasColumnName("salary_min")
            .HasColumnType("numeric(18,2)");

        builder.Property(j => j.SalaryMax)
            .HasColumnName("salary_max")
            .HasColumnType("numeric(18,2)");

        builder.Property(j => j.SalaryCurrency)
            .HasColumnName("salary_currency")
            .HasMaxLength(10);

        builder.Property(j => j.ExternalApplyUrl)
            .HasColumnName("external_apply_url")
            .HasMaxLength(2048);

        builder.Property(j => j.PostedAt)
            .HasColumnName("posted_at")
            .IsRequired();

        builder.Property(j => j.ExpiresAt)
            .HasColumnName("expires_at");

        builder.Property(j => j.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true);

        builder.Property(j => j.ViewsCount)
            .HasColumnName("views_count")
            .HasDefaultValue(0);

        builder.Property(j => j.ApplicantsClickCount)
            .HasColumnName("applicants_click_count")
            .HasDefaultValue(0);

        // ── pgvector column ─────────────────────────────────────────────────────
        // Dimensions: 1536 (compatible with OpenAI text-embedding-3-small and similar)
        // After the migration runs, create the HNSW index manually for production:
        //   CREATE INDEX ON jobs USING hnsw (embedding vector_cosine_ops);
        builder.Property(j => j.Embedding)
            .HasColumnName("embedding")
            .HasColumnType("vector(1536)");
        // ────────────────────────────────────────────────────────────────────────

        builder.Property(j => j.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(j => j.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        // FK: Job → Source
        builder.HasOne(j => j.Source)
            .WithMany(s => s.Jobs)
            .HasForeignKey(j => j.SourceId)
            .OnDelete(DeleteBehavior.Restrict);

        // FK: Job → RawPost (optional 1-to-1 from Job side)
        builder.HasOne(j => j.RawPost)
            .WithOne(r => r.Job)
            .HasForeignKey<Job>(j => j.RawPostId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        builder.HasIndex(j => j.SourceId)
            .HasDatabaseName("ix_jobs_source_id");

        builder.HasIndex(j => j.PostedAt)
            .HasDatabaseName("ix_jobs_posted_at");

        builder.HasIndex(j => j.IsActive)
            .HasDatabaseName("ix_jobs_is_active");

        builder.HasIndex(j => j.EmploymentType)
            .HasDatabaseName("ix_jobs_employment_type");

        builder.Navigation(j => j.JobSkills).AutoInclude(false);
        builder.Navigation(j => j.SavedByUsers).AutoInclude(false);
        builder.Navigation(j => j.Applications).AutoInclude(false);
        builder.Navigation(j => j.MatchAnalyses).AutoInclude(false);
    }
}
