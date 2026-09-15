using JobRadar.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobRadar.Infrastructure.Persistence.Configurations;

public sealed class CvJobMatchAnalysisConfiguration : IEntityTypeConfiguration<CvJobMatchAnalysis>
{
    public void Configure(EntityTypeBuilder<CvJobMatchAnalysis> builder)
    {
        builder.ToTable("cv_job_match_analyses");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("id");

        builder.Property(a => a.CvId)
            .HasColumnName("cv_id")
            .IsRequired();

        builder.Property(a => a.JobId)
            .HasColumnName("job_id")
            .IsRequired();

        builder.Property(a => a.AtsScore)
            .HasColumnName("ats_score")
            .IsRequired();

        builder.Property(a => a.MissingKeywords)
            .HasColumnName("missing_keywords")
            .HasColumnType("text[]")
            .HasDefaultValueSql("'{}'::text[]");

        builder.Property(a => a.Suggestions)
            .HasColumnName("suggestions")
            .IsRequired();

        builder.Property(a => a.AnalyzedAt)
            .HasColumnName("analyzed_at")
            .IsRequired();

        // FK: CvJobMatchAnalysis → Cv
        builder.HasOne(a => a.Cv)
            .WithMany(c => c.Analyses)
            .HasForeignKey(a => a.CvId)
            .OnDelete(DeleteBehavior.Cascade);

        // FK: CvJobMatchAnalysis → Job
        builder.HasOne(a => a.Job)
            .WithMany(j => j.MatchAnalyses)
            .HasForeignKey(a => a.JobId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(a => new { a.CvId, a.JobId })
            .HasDatabaseName("ix_cv_job_match_analyses_cv_job");

        builder.HasIndex(a => a.AnalyzedAt)
            .HasDatabaseName("ix_cv_job_match_analyses_analyzed_at");
    }
}
