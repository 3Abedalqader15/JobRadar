using JobRadar.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobRadar.Infrastructure.Persistence.Configurations;

public sealed class CvTemplateConfiguration : IEntityTypeConfiguration<CvTemplate>
{
    public void Configure(EntityTypeBuilder<CvTemplate> builder)
    {
        builder.ToTable("cv_templates");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasColumnName("id");

        builder.Property(t => t.Name)
            .HasColumnName("name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(t => t.PreviewImageUrl)
            .HasColumnName("preview_image_url")
            .HasMaxLength(2048);

        builder.Property(t => t.IsAtsFriendly)
            .HasColumnName("is_ats_friendly")
            .HasDefaultValue(false);

        builder.HasIndex(t => t.Name)
            .IsUnique()
            .HasDatabaseName("ix_cv_templates_name");

        builder.Navigation(t => t.Cvs).AutoInclude(false);
    }
}
