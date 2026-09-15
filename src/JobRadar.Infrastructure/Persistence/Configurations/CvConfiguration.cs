using JobRadar.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobRadar.Infrastructure.Persistence.Configurations;

public sealed class CvConfiguration : IEntityTypeConfiguration<Cv>
{
    public void Configure(EntityTypeBuilder<Cv> builder)
    {
        builder.ToTable("cvs");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id");

        builder.Property(c => c.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(c => c.TemplateId)
            .HasColumnName("template_id");

        builder.Property(c => c.ContentJson)
            .HasColumnName("content_json")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(c => c.GeneratedPdfUrl)
            .HasColumnName("generated_pdf_url")
            .HasMaxLength(2048);

        builder.Property(c => c.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(c => c.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        // FK: Cv → User
        builder.HasOne(c => c.User)
            .WithMany(u => u.Cvs)
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // FK: Cv → CvTemplate (optional)
        builder.HasOne(c => c.Template)
            .WithMany(t => t.Cvs)
            .HasForeignKey(c => c.TemplateId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        builder.HasIndex(c => c.UserId)
            .HasDatabaseName("ix_cvs_user_id");

        builder.Navigation(c => c.Analyses).AutoInclude(false);
    }
}
