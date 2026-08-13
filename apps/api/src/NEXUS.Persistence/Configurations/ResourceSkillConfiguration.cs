using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXUS.Modules.ResourceManagement.Domain;

namespace NEXUS.Persistence.Configurations;

public class ResourceSkillConfiguration : IEntityTypeConfiguration<ResourceSkill>
{
    public void Configure(EntityTypeBuilder<ResourceSkill> builder)
    {
        builder.ToTable("ResourceSkills");
        builder.HasKey(rs => rs.Id);
        builder.Property(rs => rs.Proficiency).HasMaxLength(50).IsRequired();

        builder.HasOne(rs => rs.Resource)
            .WithMany(r => r.Skills)
            .HasForeignKey(rs => rs.ResourceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(rs => rs.Skill)
            .WithMany()
            .HasForeignKey(rs => rs.SkillId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(rs => new { rs.ResourceId, rs.SkillId }).IsUnique();
        builder.HasIndex(rs => rs.TenantId);
    }
}
