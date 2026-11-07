using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXUS.Modules.Approvals.Domain;
using NEXUS.Modules.CostManagement.Domain;

namespace NEXUS.Persistence.Configurations;

public sealed class ApprovalActionConfiguration : IEntityTypeConfiguration<ApprovalAction>
{
    public void Configure(EntityTypeBuilder<ApprovalAction> builder)
    {
        builder.ToTable("ApprovalActions");
        builder.HasKey(action => action.Id);

        builder.HasOne<PaymentRequestApproverAssignment>()
            .WithMany()
            .HasForeignKey(action => new
            {
                action.TenantId,
                action.ApprovalRequestId,
                action.ApproverAssignmentId
            })
            .HasPrincipalKey(assignment => new
            {
                assignment.TenantId,
                assignment.ApprovalRequestId,
                assignment.Id
            })
            .OnDelete(DeleteBehavior.Restrict);

        // Null keeps old and non-financial actions compatible. A financial
        // assignment can contribute at most one durable decision.
        builder.HasIndex(action => action.ApproverAssignmentId)
            .IsUnique()
            .HasFilter("[ApproverAssignmentId] IS NOT NULL")
            .HasDatabaseName("UX_ApprovalActions_ApproverAssignment");
        builder.HasIndex(action => action.ApprovalRequestId);
    }
}
