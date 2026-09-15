using JobRadar.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobRadar.Infrastructure.Persistence.Configurations;

public sealed class UserJobApplicationConfiguration : IEntityTypeConfiguration<UserJobApplication>
{
    public void Configure(EntityTypeBuilder<UserJobApplication> builder)
    {
        builder.ToTable("user_job_applications");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("id");

        builder.Property(a => a.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(a => a.JobId)
            .HasColumnName("job_id")
            .IsRequired();

        builder.Property(a => a.AppliedAt)
            .HasColumnName("applied_at")
            .IsRequired();

        builder.Property(a => a.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(a => a.Notes)
            .HasColumnName("notes")
            .HasMaxLength(5000);

        builder.Property(a => a.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        // FK: UserJobApplication → User
        builder.HasOne(a => a.User)
            .WithMany(u => u.Applications)
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // FK: UserJobApplication → Job
        builder.HasOne(a => a.Job)
            .WithMany(j => j.Applications)
            .HasForeignKey(a => a.JobId)
            .OnDelete(DeleteBehavior.Cascade);

        // Prevent duplicate applications for the same user+job
        builder.HasIndex(a => new { a.UserId, a.JobId })
            .IsUnique()
            .HasDatabaseName("ix_user_job_applications_user_job");

        builder.HasIndex(a => a.AppliedAt)
            .HasDatabaseName("ix_user_job_applications_applied_at");
    }
}
