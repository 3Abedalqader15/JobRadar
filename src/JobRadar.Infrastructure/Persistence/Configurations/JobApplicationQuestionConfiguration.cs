using JobRadar.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobRadar.Infrastructure.Persistence.Configurations;

public sealed class JobApplicationQuestionConfiguration : IEntityTypeConfiguration<JobApplicationQuestion>
{
    public void Configure(EntityTypeBuilder<JobApplicationQuestion> builder)
    {
        builder.ToTable("job_application_questions");

        builder.HasKey(q => q.Id);
        builder.Property(q => q.Id).HasColumnName("id");

        builder.Property(q => q.JobId)
            .HasColumnName("job_id")
            .IsRequired();

        builder.Property(q => q.QuestionText)
            .HasColumnName("question_text")
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(q => q.QuestionType)
            .HasColumnName("question_type")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(q => q.OptionsJson)
            .HasColumnName("options_json")
            .HasColumnType("text");

        builder.Property(q => q.IsRequired)
            .HasColumnName("is_required")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(q => q.DisplayOrder)
            .HasColumnName("display_order")
            .HasDefaultValue(0)
            .IsRequired();

        // FK: JobApplicationQuestion → Job (Cascade delete when job is deleted)
        builder.HasOne(q => q.Job)
            .WithMany(j => j.Questions)
            .HasForeignKey(q => q.JobId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(q => q.JobId)
            .HasDatabaseName("ix_job_application_questions_job_id");

        builder.HasIndex(q => new { q.JobId, q.DisplayOrder })
            .HasDatabaseName("ix_job_application_questions_job_order");
    }
}
