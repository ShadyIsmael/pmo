using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXUS.Modules.ProjectManagement.Domain;

namespace NEXUS.Persistence.Configurations;

public class ProjectManagerAssignmentConfiguration : IEntityTypeConfiguration<ProjectManagerAssignment>
{
    public void Configure(EntityTypeBuilder<ProjectManagerAssignment> builder)
    {
        builder.ToTable("ProjectManagerAssignments");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.HandoverReason).HasMaxLength(500);
        builder.Property(a => a.HandoverNotes).HasMaxLength(2000);
        builder.HasIndex(a => a.TenantId);
        builder.HasIndex(a => a.ProjectId);
        builder.HasIndex(a => a.ProjectManagerId);
        builder.HasIndex(a => new { a.ProjectId })
            .IsUnique()
            .HasFilter("[IsCurrent] = 1");
        builder.Property(a => a.RowVersion).IsRowVersion();
    }
}
