using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXUS.Modules.ProjectManagement.Domain;

namespace NEXUS.Persistence.Configurations;

public class ProjectHandoverRequestConfiguration : IEntityTypeConfiguration<ProjectHandoverRequest>
{
    public void Configure(EntityTypeBuilder<ProjectHandoverRequest> builder)
    {
        builder.ToTable("ProjectHandoverRequests");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Reason).HasMaxLength(500);
        builder.Property(r => r.Notes).HasMaxLength(2000);
        builder.Property(r => r.Status).HasMaxLength(20);
        builder.Property(r => r.RejectionReason).HasMaxLength(500);
        builder.HasIndex(r => r.TenantId);
        builder.HasIndex(r => r.ProjectId);
        builder.HasIndex(r => r.NewProjectManagerId);
        builder.HasIndex(r => r.Status);
        builder.Property(r => r.RowVersion).IsRowVersion();
    }
}
