using JobRadar.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobRadar.Infrastructure.Persistence.Configurations;

public sealed class UserSavedJobConfiguration : IEntityTypeConfiguration<UserSavedJob>
{
    public void Configure(EntityTypeBuilder<UserSavedJob> builder)
    {
        builder.ToTable("user_saved_jobs");

        // Composite primary key
        builder.HasKey(s => new { s.UserId, s.JobId });

        builder.Property(s => s.UserId)
            .HasColumnName("user_id");

        builder.Property(s => s.JobId)
            .HasColumnName("job_id");

        builder.Property(s => s.SavedAt)
            .HasColumnName("saved_at")
            .IsRequired();

        // FK: UserSavedJob → User
        builder.HasOne(s => s.User)
            .WithMany(u => u.SavedJobs)
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // FK: UserSavedJob → Job
        builder.HasOne(s => s.Job)
            .WithMany(j => j.SavedByUsers)
            .HasForeignKey(s => s.JobId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(s => s.SavedAt)
            .HasDatabaseName("ix_user_saved_jobs_saved_at");
    }
}
