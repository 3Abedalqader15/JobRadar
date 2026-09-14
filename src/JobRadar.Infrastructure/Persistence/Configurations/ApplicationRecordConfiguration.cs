using JobRadar.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobRadar.Infrastructure.Persistence.Configurations;

public sealed class ApplicationRecordConfiguration : IEntityTypeConfiguration<ApplicationRecord>
{
    public void Configure(EntityTypeBuilder<ApplicationRecord> builder)
    {
        builder.ToTable("application_records");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .HasColumnName("id");

        builder.Property(a => a.JobPostingId)
            .HasColumnName("job_posting_id")
            .IsRequired();

        builder.Property(a => a.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(a => a.Notes)
            .HasColumnName("notes")
            .HasMaxLength(5000);

        builder.Property(a => a.AppliedAt)
            .HasColumnName("applied_at")
            .IsRequired();

        builder.Property(a => a.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.HasOne(a => a.JobPosting)
            .WithMany()
            .HasForeignKey(a => a.JobPostingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(a => a.JobPostingId)
            .HasDatabaseName("ix_application_records_job_posting_id");

        builder.HasIndex(a => a.AppliedAt)
            .HasDatabaseName("ix_application_records_applied_at");
    }
}
