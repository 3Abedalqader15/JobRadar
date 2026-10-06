using JobRadar.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobRadar.Infrastructure.Persistence.Configurations;

public sealed class JobApplicationAnswerConfiguration : IEntityTypeConfiguration<JobApplicationAnswer>
{
    public void Configure(EntityTypeBuilder<JobApplicationAnswer> builder)
    {
        builder.ToTable("job_application_answers");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("id");

        builder.Property(a => a.UserJobApplicationId)
            .HasColumnName("user_job_application_id")
            .IsRequired();

        builder.Property(a => a.QuestionId)
            .HasColumnName("question_id")
            .IsRequired();

        builder.Property(a => a.AnswerText)
            .HasColumnName("answer_text")
            .HasColumnType("text")
            .IsRequired();

        // FK: JobApplicationAnswer → UserJobApplication (Cascade delete when application is deleted)
        builder.HasOne(a => a.UserJobApplication)
            .WithMany(app => app.Answers)
            .HasForeignKey(a => a.UserJobApplicationId)
            .OnDelete(DeleteBehavior.Cascade);

        // FK: JobApplicationAnswer → JobApplicationQuestion
        builder.HasOne(a => a.Question)
            .WithMany(q => q.Answers)
            .HasForeignKey(a => a.QuestionId)
            .OnDelete(DeleteBehavior.Restrict);

        // Unique constraint: one answer per question per application
        builder.HasIndex(a => new { a.UserJobApplicationId, a.QuestionId })
            .IsUnique()
            .HasDatabaseName("ix_job_application_answers_app_question");

        builder.HasIndex(a => a.UserJobApplicationId)
            .HasDatabaseName("ix_job_application_answers_app_id");

        builder.HasIndex(a => a.QuestionId)
            .HasDatabaseName("ix_job_application_answers_question_id");
    }
}
