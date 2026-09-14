using JobRadar.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pgvector;

namespace JobRadar.Infrastructure.Persistence.Configurations;

public sealed class JobPostingConfiguration : IEntityTypeConfiguration<JobPosting>
{
    public void Configure(EntityTypeBuilder<JobPosting> builder)
    {
        builder.ToTable("job_postings");

        builder.HasKey(j => j.Id);

        builder.Property(j => j.Id)
            .HasColumnName("id");

        builder.Property(j => j.Title)
            .HasColumnName("title")
            .HasMaxLength(300)
            .IsRequired();

        builder.Property(j => j.Company)
            .HasColumnName("company")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(j => j.Location)
            .HasColumnName("location")
            .HasMaxLength(200);

        builder.Property(j => j.IsRemote)
            .HasColumnName("is_remote")
            .HasDefaultValue(false);

        builder.Property(j => j.Description)
            .HasColumnName("description")
            .IsRequired();

        builder.Property(j => j.SourceUrl)
            .HasColumnName("source_url")
            .HasMaxLength(2048);

        builder.Property(j => j.SalaryMin)
            .HasColumnName("salary_min")
            .HasColumnType("numeric(18,2)");

        builder.Property(j => j.SalaryMax)
            .HasColumnName("salary_max")
            .HasColumnType("numeric(18,2)");

        builder.Property(j => j.SalaryCurrency)
            .HasColumnName("salary_currency")
            .HasMaxLength(10);

        builder.Property(j => j.JobType)
            .HasColumnName("job_type")
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(j => j.ExperienceLevel)
            .HasColumnName("experience_level")
            .HasConversion<string>()
            .HasMaxLength(50);

        // pgvector column — 1536 dimensions (OpenAI text-embedding-ada-002 / text-embedding-3-small)
        // To create an HNSW index after migration, run:
        //   CREATE INDEX ON job_postings USING hnsw (embedding_vector vector_cosine_ops);
        builder.Property(j => j.EmbeddingVector)
            .HasColumnName("embedding_vector")
            .HasColumnType("vector(1536)")
            .HasConversion(
                v => v != null ? new Vector(v) : null,
                v => v != null ? v.ToArray() : null
            )
            .Metadata.SetValueComparer(
                new ValueComparer<float[]>(
                    (c1, c2) => c1 != null && c2 != null && c1.SequenceEqual(c2),
                    c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                    c => c.ToArray()
                )
            );

        builder.Property(j => j.PostedAt)
            .HasColumnName("posted_at")
            .IsRequired();

        builder.Property(j => j.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(j => j.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.Property(j => j.JobSourceId)
            .HasColumnName("job_source_id");

        builder.HasOne(j => j.JobSource)
            .WithMany(s => s.JobPostings)
            .HasForeignKey(j => j.JobSourceId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        builder.HasIndex(j => j.SourceUrl)
            .IsUnique()
            .HasFilter("source_url IS NOT NULL")
            .HasDatabaseName("ix_job_postings_source_url");

        builder.HasIndex(j => j.PostedAt)
            .HasDatabaseName("ix_job_postings_posted_at");
    }
}
