using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXUS.Modules.ProjectManagement.Domain;

namespace NEXUS.Persistence.Configurations;

public class ProjectMemberRoleConfiguration : IEntityTypeConfiguration<ProjectMemberRole>
{
    public void Configure(EntityTypeBuilder<ProjectMemberRole> builder)
    {
        builder.ToTable("ProjectMemberRoles");
        builder.HasKey(pmr => pmr.Id);

        builder.HasOne(pmr => pmr.ProjectMember)
            .WithMany(m => m.FunctionalRoles)
            .HasForeignKey(pmr => pmr.ProjectMemberId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(pmr => pmr.TenantId);
        builder.HasIndex(pmr => new { pmr.TenantId, pmr.ProjectMemberId, pmr.ProjectRoleId }).IsUnique();
        builder.Property(pmr => pmr.RowVersion).IsRowVersion();
    }
}
