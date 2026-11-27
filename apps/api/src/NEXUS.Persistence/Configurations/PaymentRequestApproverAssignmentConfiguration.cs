using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXUS.Modules.Approvals.Domain;
using NEXUS.Modules.CostManagement.Domain;
using NEXUS.Modules.Identity.Domain;

namespace NEXUS.Persistence.Configurations;

public sealed class PaymentRequestApproverAssignmentConfiguration : IEntityTypeConfiguration<PaymentRequestApproverAssignment>
{
    public void Configure(EntityTypeBuilder<PaymentRequestApproverAssignment> builder)
    {
        builder.ToTable("PaymentRequestApproverAssignments", table =>
        {
            table.HasCheckConstraint(
                "CK_PaymentRequestApproverAssignments_Position",
                "[Position] IS NULL OR [Position] > 0");
            table.HasCheckConstraint(
                "CK_PaymentRequestApproverAssignments_Requirement",
                "[Requirement] IN (N'Mandatory', N'Optional')");
            table.HasCheckConstraint(
                "CK_PaymentRequestApproverAssignments_State",
                "[State] IN (N'Pending', N'Approved', N'Rejected', N'ReturnedForRevision', N'Closed')");
        });

        builder.HasKey(item => item.Id);
        builder.Property(item => item.Requirement).HasMaxLength(24).IsRequired();
        builder.Property(item => item.State).HasMaxLength(24).IsRequired();
        builder.Property(item => item.Position).IsRequired(false);
        builder.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();

        // This alternate key lets ApprovalAction reference an assignment while
        // proving that both rows belong to the same tenant and request.
        builder.HasAlternateKey(item => new { item.TenantId, item.ApprovalRequestId, item.Id })
            .HasName("AK_PaymentRequestApproverAssignments_Tenant_Request_Id");

        builder.HasOne<ApprovalRequest>()
            .WithMany()
            .HasForeignKey(item => new { item.TenantId, item.ApprovalRequestId })
            .HasPrincipalKey(request => new { request.TenantId, request.Id })
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(item => new { item.TenantId, item.ApproverUserId })
            .HasPrincipalKey(user => new { user.TenantId, user.Id })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ProjectPaymentRequestApprovalCycle>()
            .WithMany()
            .HasForeignKey(item => new { item.TenantId, item.ProjectId, item.ApprovalRequestId, item.ApprovalCycleId })
            .HasPrincipalKey(cycle => new { cycle.TenantId, cycle.ProjectId, cycle.ApprovalRequestId, cycle.Id })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(item => new { item.TenantId, item.ApprovalRequestId, item.ApproverUserId })
            .IsUnique().HasFilter("[ApprovalCycleId] IS NULL")
            .HasDatabaseName("UX_PaymentRequestApproverAssignments_Request_Approver");

        builder.HasIndex(item => new { item.TenantId, item.ApprovalRequestId, item.ApprovalCycleId, item.ApproverUserId })
            .IsUnique().HasFilter("[ApprovalCycleId] IS NOT NULL")
            .HasDatabaseName("UX_PaymentRequestApproverAssignments_Cycle_Approver");

        // Parallel checklist members have no sequential order, so multiple NULL
        // positions remain valid. Sequential positions are unique per request.
        builder.HasIndex(item => new { item.TenantId, item.ApprovalRequestId, item.Position })
            .IsUnique().HasFilter("[Position] IS NOT NULL AND [ApprovalCycleId] IS NULL")
            .HasDatabaseName("UX_PaymentRequestApproverAssignments_Request_Position");

        builder.HasIndex(item => new { item.TenantId, item.ApprovalRequestId, item.ApprovalCycleId, item.Position })
            .IsUnique().HasFilter("[Position] IS NOT NULL AND [ApprovalCycleId] IS NOT NULL")
            .HasDatabaseName("UX_PaymentRequestApproverAssignments_Cycle_Position");
    }
}
