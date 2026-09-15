using JobRadar.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobRadar.Infrastructure.Persistence.Configurations;

public sealed class JobSkillMapConfiguration : IEntityTypeConfiguration<JobSkillMap>
{
    public void Configure(EntityTypeBuilder<JobSkillMap> builder)
    {
        builder.ToTable("job_skill_maps");

        // Composite primary key
        builder.HasKey(m => new { m.JobId, m.SkillId });

        builder.Property(m => m.JobId)
            .HasColumnName("job_id");

        builder.Property(m => m.SkillId)
            .HasColumnName("skill_id");

        // FK: JobSkillMap → Job
        builder.HasOne(m => m.Job)
            .WithMany(j => j.JobSkills)
            .HasForeignKey(m => m.JobId)
            .OnDelete(DeleteBehavior.Cascade);

        // FK: JobSkillMap → Skill
        builder.HasOne(m => m.Skill)
            .WithMany(s => s.JobSkills)
            .HasForeignKey(m => m.SkillId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(m => m.SkillId)
            .HasDatabaseName("ix_job_skill_maps_skill_id");
    }
}
