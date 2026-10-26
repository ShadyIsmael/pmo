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
        builder.Property(r => r.InitiationType).HasMaxLength(40).IsRequired().HasDefaultValue("ManagerInitiated");
        builder.Property(r => r.RejectionReason).HasMaxLength(500);
        builder.HasIndex(r => r.TenantId);
        builder.HasIndex(r => new { r.TenantId, r.ProjectId, r.Status }).IsUnique().HasFilter("[Status] = 'Pending' AND [IsDeleted] = 0");
        builder.HasIndex(r => r.NewProjectManagerId);
        builder.HasIndex(r => r.Status);
        builder.Property(r => r.RowVersion).IsRowVersion();
    }
}
