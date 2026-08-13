using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXUS.Modules.ResourceManagement.Domain;

namespace NEXUS.Persistence.Configurations;

public class SkillConfiguration : IEntityTypeConfiguration<Skill>
{
    public void Configure(EntityTypeBuilder<Skill> builder)
    {
        builder.ToTable("Skills");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Name).HasMaxLength(200).IsRequired();
        builder.Property(s => s.Category).HasMaxLength(100);
        builder.Property(s => s.Description).HasMaxLength(500);
        builder.HasIndex(s => s.Name).IsUnique();
        builder.HasIndex(s => s.TenantId);
    }
}
