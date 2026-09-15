using JobRadar.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobRadar.Infrastructure.Persistence.Configurations;

public sealed class SourceConfiguration : IEntityTypeConfiguration<Source>
{
    public void Configure(EntityTypeBuilder<Source> builder)
    {
        builder.ToTable("sources");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("id");

        builder.Property(s => s.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(s => s.Type)
            .HasColumnName("type")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(s => s.Url)
            .HasColumnName("url")
            .HasMaxLength(2048)
            .IsRequired();

        builder.Property(s => s.AddedByUserId)
            .HasColumnName("added_by_user_id");

        builder.Property(s => s.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(s => s.FetchIntervalMinutes)
            .HasColumnName("fetch_interval_minutes")
            .HasDefaultValue(60);

        builder.Property(s => s.LastFetchedAt)
            .HasColumnName("last_fetched_at");

        builder.Property(s => s.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        // FK: Source → User (nullable — admin-created sources have no user)
        builder.HasOne(s => s.AddedByUser)
            .WithMany(u => u.AddedSources)
            .HasForeignKey(s => s.AddedByUserId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        builder.HasIndex(s => s.Url)
            .IsUnique()
            .HasDatabaseName("ix_sources_url");

        builder.HasIndex(s => s.Status)
            .HasDatabaseName("ix_sources_status");

        builder.Navigation(s => s.RawPosts).AutoInclude(false);
        builder.Navigation(s => s.Jobs).AutoInclude(false);
    }
}
