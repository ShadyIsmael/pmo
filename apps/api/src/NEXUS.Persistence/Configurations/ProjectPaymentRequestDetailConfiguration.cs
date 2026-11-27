using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXUS.Modules.Approvals.Domain;
using NEXUS.Modules.CostManagement.Domain;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.Modules.ProjectPlanning.Domain;

namespace NEXUS.Persistence.Configurations;

public sealed class ProjectPaymentRequestDetailConfiguration : IEntityTypeConfiguration<ProjectPaymentRequestDetail>
{
    public void Configure(EntityTypeBuilder<ProjectPaymentRequestDetail> builder)
    {
        builder.ToTable("ProjectPaymentRequestDetails", table =>
        {
            table.HasCheckConstraint(
                "CK_ProjectPaymentRequestDetails_ApprovalMode",
                "[ApprovalMode] IS NULL OR [ApprovalMode] IN (N'Sequential', N'Parallel')");
            table.HasCheckConstraint(
                "CK_ProjectPaymentRequestDetails_RequestKind",
                "[RequestKind] IN (N'PaymentRequest', N'ReversalRequest')");
            table.HasCheckConstraint(
                "CK_ProjectPaymentRequestDetails_ReversalReference",
                "([RequestKind] = N'PaymentRequest' AND [ReversesApprovalRequestId] IS NULL) OR " +
                "([RequestKind] = N'ReversalRequest' AND [ReversesApprovalRequestId] IS NOT NULL " +
                "AND [ReversesApprovalRequestId] <> [ApprovalRequestId])");
            table.HasCheckConstraint(
                "CK_ProjectPaymentRequestDetails_IdempotencyFingerprint",
                "([IdempotencyKey] IS NULL AND [RequestFingerprint] IS NULL) OR " +
                "([IdempotencyKey] IS NOT NULL AND [RequestFingerprint] IS NOT NULL)");
            table.HasCheckConstraint(
                "CK_ProjectPaymentRequestDetails_AnnualBudgetAssociation",
                "([ProjectAnnualBudgetId] IS NULL AND [AnnualBudgetFiscalYear] IS NULL AND [AnnualBudgetReference] IS NULL) OR " +
                "([ProjectAnnualBudgetId] IS NOT NULL AND [AnnualBudgetFiscalYear] BETWEEN 1900 AND 2200 AND [AnnualBudgetReference] IS NOT NULL)");
            table.HasCheckConstraint(
                "CK_ProjectPaymentRequestDetails_AnnualBudgetLineAssociation",
                "[ProjectAnnualBudgetLineId] IS NULL OR [ProjectAnnualBudgetId] IS NOT NULL");
            table.HasCheckConstraint(
                "CK_ProjectPaymentRequestDetails_ApprovalCycleNumber",
                "([CurrentApprovalCycleId] IS NULL AND [CurrentApprovalCycleNumber] = 0) OR " +
                "([CurrentApprovalCycleId] IS NOT NULL AND [CurrentApprovalCycleNumber] > 0)");
        });

        builder.HasKey(item => item.ApprovalRequestId)
            .HasName("PK_ProjectPaymentRequestDetails");
        builder.HasAlternateKey(item => new { item.TenantId, item.ApprovalRequestId })
            .HasName("AK_ProjectPaymentRequestDetails_TenantId_ApprovalRequestId");
        builder.HasAlternateKey(item => new { item.TenantId, item.ProjectId, item.ApprovalRequestId })
            .HasName("AK_ProjectPaymentRequestDetails_Tenant_Project_Request");
        builder.Property(item => item.ApprovalMode).HasMaxLength(24);
        builder.Property(item => item.RequestKind).HasMaxLength(24).IsRequired();
        builder.Property(item => item.RequestFingerprint).HasMaxLength(128);
        builder.Property(item => item.PlannedPaymentInstallmentId);
        builder.Property(item => item.ProjectAnnualBudgetLineId);
        builder.Property(item => item.CurrentApprovalCycleNumber).HasDefaultValue(0);
        builder.Property(item => item.AnnualBudgetFiscalYear);
        builder.Property(item => item.AnnualBudgetReference).HasMaxLength(100);
        builder.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();

        builder.HasOne<ApprovalRequest>()
            .WithOne()
            .HasForeignKey<ProjectPaymentRequestDetail>(item => new { item.TenantId, item.ApprovalRequestId })
            .HasPrincipalKey<ApprovalRequest>(request => new { request.TenantId, request.Id })
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(item => new { item.TenantId, item.ProjectId })
            .HasPrincipalKey(project => new { project.TenantId, project.Id })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ProjectAnnualBudget>()
            .WithMany()
            .HasForeignKey(item => new { item.TenantId, item.ProjectId, item.ProjectAnnualBudgetId })
            .HasPrincipalKey(budget => new { budget.TenantId, budget.ProjectId, budget.Id })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<PlanActivity>()
            .WithMany()
            .HasForeignKey(item => new { item.TenantId, item.PlanActivityId })
            .HasPrincipalKey(activity => new { activity.TenantId, activity.Id })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<PlannedPaymentInstallment>()
            .WithMany()
            .HasForeignKey(item => new { item.TenantId, item.ProjectId, item.PlannedPaymentInstallmentId })
            .HasPrincipalKey(installment => new { installment.TenantId, installment.ProjectId, installment.Id })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ProjectAnnualBudgetLine>()
            .WithMany()
            .HasForeignKey(item => new { item.TenantId, item.ProjectId, item.ProjectAnnualBudgetId, item.ProjectAnnualBudgetLineId })
            .HasPrincipalKey(line => new { line.TenantId, line.ProjectId, line.ProjectAnnualBudgetId, line.Id })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ProjectPaymentRequestApprovalCycle>()
            .WithMany()
            .HasForeignKey(item => new { item.TenantId, item.ProjectId, item.ApprovalRequestId, item.CurrentApprovalCycleId })
            .HasPrincipalKey(cycle => new { cycle.TenantId, cycle.ProjectId, cycle.ApprovalRequestId, cycle.Id })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(item => item.ReversesRequest)
            .WithMany()
            .HasForeignKey(item => new { item.TenantId, item.ProjectId, item.ReversesApprovalRequestId })
            .HasPrincipalKey(original => new { original.TenantId, original.ProjectId, ApprovalRequestId = original.ApprovalRequestId })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ProjectPaymentRequestFundingTransfer>()
            .WithMany()
            .HasForeignKey(item => new { item.TenantId, item.ProjectId, item.FundingTransferId })
            .HasPrincipalKey(transfer => new { transfer.TenantId, transfer.ProjectId, transfer.Id })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(item => new { item.TenantId, item.ProjectId, item.RequestKind })
            .HasDatabaseName("IX_ProjectPaymentRequestDetails_Tenant_Project_Kind");
        builder.HasIndex(item => new { item.TenantId, item.ProjectId, item.ProjectAnnualBudgetId })
            .HasDatabaseName("IX_ProjectPaymentRequestDetails_AnnualBudget");
        builder.HasIndex(item => new { item.TenantId, item.ProjectId, item.PlannedPaymentInstallmentId })
            .HasDatabaseName("IX_ProjectPaymentRequestDetails_PlannedInstallment");
        builder.HasIndex(item => new { item.TenantId, item.ProjectId, item.IdempotencyKey })
            .IsUnique()
            .HasFilter("[IdempotencyKey] IS NOT NULL")
            .HasDatabaseName("UX_ProjectPaymentRequestDetails_Project_Idempotency");
        builder.HasIndex(item => new { item.TenantId, item.ReversesApprovalRequestId })
            .HasDatabaseName("IX_ProjectPaymentRequestDetails_ReversesRequest");
    }
}
