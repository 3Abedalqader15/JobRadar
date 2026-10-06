using JobRadar.Domain.Entities;
using JobRadar.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobRadar.Infrastructure.Persistence.Configurations;

public sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.ToTable("users");

        // Identity properties (will be added to the users table by migration)
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).HasColumnName("id");

        // Map existing required columns to Identity's properties
        builder.Property(u => u.Email)
            .HasColumnName("email")
            .HasMaxLength(320)
            .IsRequired();

        builder.Property(u => u.PasswordHash)
            .HasColumnName("password_hash")
            .HasMaxLength(500)
            .IsRequired();

        // Custom properties
        builder.Property(u => u.FullName)
            .HasColumnName("full_name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(u => u.PhoneNumber)
            .HasColumnName("phone")
            .HasMaxLength(30);

        builder.Property(u => u.IsDeactivated)
            .HasColumnName("is_deactivated")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(u => u.PreferredJobTitles)
            .HasColumnName("preferred_job_titles")
            .HasColumnType("text[]")
            .HasDefaultValueSql("'{}'::text[]");

        builder.Property(u => u.PreferredLocations)
            .HasColumnName("preferred_locations")
            .HasColumnType("text[]")
            .HasDefaultValueSql("'{}'::text[]");

        builder.Property(u => u.PreferredSkills)
            .HasColumnName("preferred_skills")
            .HasColumnType("text[]")
            .HasDefaultValueSql("'{}'::text[]");

        builder.Property(u => u.ExperienceLevel)
            .HasColumnName("experience_level")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(u => u.TelegramChatId)
            .HasColumnName("telegram_chat_id")
            .HasMaxLength(50);

        builder.Property(u => u.CompanyId)
            .HasColumnName("company_id");

        builder.Property(u => u.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(u => u.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.HasIndex(u => u.Email)
            .IsUnique()
            .HasDatabaseName("ix_users_email");

        builder.HasIndex(u => u.TelegramChatId)
            .IsUnique()
            .HasFilter("telegram_chat_id IS NOT NULL")
            .HasDatabaseName("ix_users_telegram_chat_id");

        builder.HasIndex(u => u.CompanyId)
            .HasDatabaseName("ix_users_company_id");

        builder.HasOne(u => u.Company)
            .WithMany(c => c.Users)
            .HasForeignKey(u => u.CompanyId)
            .OnDelete(DeleteBehavior.SetNull);

        // Navigations
        builder.Navigation(u => u.Company).AutoInclude(false);
        builder.Navigation(u => u.AddedSources).AutoInclude(false);
        builder.Navigation(u => u.Cvs).AutoInclude(false);
        builder.Navigation(u => u.Notifications).AutoInclude(false);
        builder.Navigation(u => u.SavedJobs).AutoInclude(false);
        builder.Navigation(u => u.Applications).AutoInclude(false);
    }
}
