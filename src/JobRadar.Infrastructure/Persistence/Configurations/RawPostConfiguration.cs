using JobRadar.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobRadar.Infrastructure.Persistence.Configurations;

public sealed class RawPostConfiguration : IEntityTypeConfiguration<RawPost>
{
    public void Configure(EntityTypeBuilder<RawPost> builder)
    {
        builder.ToTable("raw_posts");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("id");

        builder.Property(r => r.SourceId)
            .HasColumnName("source_id")
            .IsRequired();

        builder.Property(r => r.RawContent)
            .HasColumnName("raw_content")
            .IsRequired();

        builder.Property(r => r.RawUrl)
            .HasColumnName("raw_url")
            .HasMaxLength(2048);

        builder.Property(r => r.FetchedAt)
            .HasColumnName("fetched_at")
            .IsRequired();

        builder.Property(r => r.ProcessingStatus)
            .HasColumnName("processing_status")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        // FK: RawPost → Source
        builder.HasOne(r => r.Source)
            .WithMany(s => s.RawPosts)
            .HasForeignKey(r => r.SourceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(r => r.SourceId)
            .HasDatabaseName("ix_raw_posts_source_id");

        builder.HasIndex(r => r.ProcessingStatus)
            .HasDatabaseName("ix_raw_posts_processing_status");

        builder.HasIndex(r => r.FetchedAt)
            .HasDatabaseName("ix_raw_posts_fetched_at");

        builder.Navigation(r => r.Job).AutoInclude(false);
    }
}
