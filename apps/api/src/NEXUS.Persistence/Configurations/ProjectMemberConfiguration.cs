using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXUS.Modules.ProjectManagement.Domain;

namespace NEXUS.Persistence.Configurations;

public class ProjectMemberConfiguration : IEntityTypeConfiguration<ProjectMember>
{
    public void Configure(EntityTypeBuilder<ProjectMember> builder)
    {
        builder.ToTable("ProjectMembers");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Role).HasMaxLength(200).IsRequired();
        builder.Property(m => m.Status).HasMaxLength(50).IsRequired();
        builder.Property(m => m.CalendarPolicy).HasMaxLength(50).IsRequired();

        builder.HasIndex(m => m.TenantId);
        builder.HasIndex(m => m.ProjectId);
        builder.HasIndex(m => m.ResourceId);
        builder.HasIndex(m => new { m.ProjectId, m.ResourceId, m.Status });
        builder.Property(m => m.RowVersion).IsRowVersion();
    }
}
