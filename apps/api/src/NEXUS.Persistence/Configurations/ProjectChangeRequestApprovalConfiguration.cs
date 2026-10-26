using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXUS.Modules.ProjectManagement.Domain;

namespace NEXUS.Persistence.Configurations;

public sealed class ProjectChangeRequestApprovalConfiguration : IEntityTypeConfiguration<ProjectChangeRequestApproval>
{
    public void Configure(EntityTypeBuilder<ProjectChangeRequestApproval> builder)
    {
        builder.ToTable("ProjectChangeRequestApprovals");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.RoleSnapshot).HasMaxLength(200).IsRequired();
        builder.Property(item => item.OriginalApproverNameSnapshot).HasMaxLength(200).IsRequired();
        builder.Property(item => item.ApproverNameSnapshot).HasMaxLength(200).IsRequired();
        builder.Property(item => item.Status).HasMaxLength(30).IsRequired();
        builder.Property(item => item.DecisionComment).HasMaxLength(2000);
        builder.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();
        builder.HasIndex(item => new { item.TenantId, item.ProjectChangeRequestId, item.Sequence }).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(item => new { item.TenantId, item.AssignedToUserId, item.Status });
        builder.HasOne(item => item.ProjectChangeRequest).WithMany(item => item.ApprovalAssignments)
            .HasForeignKey(item => item.ProjectChangeRequestId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ProjectChangeRequestApprovalHistoryConfiguration : IEntityTypeConfiguration<ProjectChangeRequestApprovalHistory>
{
    public void Configure(EntityTypeBuilder<ProjectChangeRequestApprovalHistory> builder)
    {
        builder.ToTable("ProjectChangeRequestApprovalHistories");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Reason).HasMaxLength(2000).IsRequired();
        builder.Property(item => item.FromApproverNameSnapshot).HasMaxLength(200).IsRequired();
        builder.Property(item => item.ToApproverNameSnapshot).HasMaxLength(200).IsRequired();
        builder.Property(item => item.ChangedByNameSnapshot).HasMaxLength(200).IsRequired();
        builder.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();
        builder.HasIndex(item => new { item.TenantId, item.ProjectChangeRequestApprovalId, item.ChangedAt });
        builder.HasOne(item => item.Assignment).WithMany()
            .HasForeignKey(item => item.ProjectChangeRequestApprovalId).OnDelete(DeleteBehavior.NoAction);
    }
}
