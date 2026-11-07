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
        builder.HasIndex(m => m.TeamId);
        builder.HasIndex(m => new { m.ProjectId, m.ResourceId, m.Status });
        builder.HasIndex(m => new { m.TenantId, m.ProjectId, m.TeamId, m.Status });
        builder.ToTable("ProjectMembers", table => table.HasCheckConstraint(
            "CK_ProjectMembers_ExactlyOneAssignmentTarget",
            "(([ResourceId] IS NOT NULL AND [TeamId] IS NULL) OR ([ResourceId] IS NULL AND [TeamId] IS NOT NULL))"));
        builder.HasOne(m => m.Team)
            .WithMany()
            .HasForeignKey(m => m.TeamId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Property(m => m.RowVersion).IsRowVersion();
    }
}
