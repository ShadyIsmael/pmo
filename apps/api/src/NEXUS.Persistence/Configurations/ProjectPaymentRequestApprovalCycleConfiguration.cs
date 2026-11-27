using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXUS.Modules.CostManagement.Domain;

namespace NEXUS.Persistence.Configurations;

public sealed class ProjectPaymentRequestApprovalCycleConfiguration : IEntityTypeConfiguration<ProjectPaymentRequestApprovalCycle>
{
    public void Configure(EntityTypeBuilder<ProjectPaymentRequestApprovalCycle> builder)
    {
        builder.ToTable("ProjectPaymentRequestApprovalCycles", table =>
        {
            table.HasCheckConstraint("CK_ProjectPaymentRequestApprovalCycles_CycleNumber", "[CycleNumber] > 0");
            table.HasCheckConstraint("CK_ProjectPaymentRequestApprovalCycles_ApprovalMode", "[ApprovalMode] IN (N'Sequential', N'Parallel')");
            table.HasCheckConstraint("CK_ProjectPaymentRequestApprovalCycles_Status", "[Status] IN (N'Pending', N'Approved', N'Rejected', N'ReturnedForRevision', N'Cancelled')");
            table.HasCheckConstraint("CK_ProjectPaymentRequestApprovalCycles_Amounts", "[Amount] > 0 AND [FiscalYear] BETWEEN 1900 AND 2200 AND [Currency] <> N''");
        });

        builder.HasKey(item => item.Id);
        builder.HasAlternateKey(item => new { item.TenantId, item.ProjectId, item.ApprovalRequestId, item.Id })
            .HasName("AK_ProjectPaymentRequestApprovalCycles_Tenant_Project_Request_Id");
        builder.Property(item => item.ApprovalMode).HasMaxLength(24).IsRequired();
        builder.Property(item => item.Amount).HasPrecision(19, 4);
        builder.Property(item => item.Currency).HasMaxLength(3).IsRequired();
        builder.Property(item => item.AnnualBudgetReference).HasMaxLength(100).IsRequired();
        builder.Property(item => item.AnnualBudgetLineReference).HasMaxLength(100).IsRequired();
        builder.Property(item => item.AnnualBudgetLineDescription).HasMaxLength(200).IsRequired();
        builder.Property(item => item.SubmissionFingerprint).HasMaxLength(128).IsRequired();
        builder.Property(item => item.ApproversSnapshotJson).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(item => item.Status).HasMaxLength(32).IsRequired();
        builder.Property(item => item.DecisionComment).HasMaxLength(2000);
        builder.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();

        builder.HasOne<ProjectPaymentRequestDetail>()
            .WithMany()
            .HasForeignKey(item => new { item.TenantId, item.ProjectId, item.ApprovalRequestId })
            .HasPrincipalKey(item => new { item.TenantId, item.ProjectId, item.ApprovalRequestId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ProjectAnnualBudget>()
            .WithMany()
            .HasForeignKey(item => new { item.TenantId, item.ProjectId, item.ProjectAnnualBudgetId })
            .HasPrincipalKey(item => new { item.TenantId, item.ProjectId, item.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PlannedPaymentInstallment>()
            .WithMany()
            .HasForeignKey(item => new { item.TenantId, item.ProjectId, item.PlannedPaymentInstallmentId })
            .HasPrincipalKey(item => new { item.TenantId, item.ProjectId, item.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ProjectAnnualBudgetLine>()
            .WithMany()
            .HasForeignKey(item => new { item.TenantId, item.ProjectId, item.ProjectAnnualBudgetId, item.ProjectAnnualBudgetLineId })
            .HasPrincipalKey(item => new { item.TenantId, item.ProjectId, item.ProjectAnnualBudgetId, item.Id })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(item => new { item.TenantId, item.ApprovalRequestId, item.CycleNumber })
            .IsUnique().HasDatabaseName("UX_ProjectPaymentRequestApprovalCycles_Request_Cycle");
        builder.HasIndex(item => new { item.TenantId, item.ProjectId, item.ApprovalRequestId, item.SubmitIdempotencyKey })
            .IsUnique().HasDatabaseName("UX_ProjectPaymentRequestApprovalCycles_Project_Idempotency");
        builder.HasIndex(item => new { item.TenantId, item.ApprovalRequestId, item.Status, item.SubmittedAtUtc })
            .HasDatabaseName("IX_ProjectPaymentRequestApprovalCycles_Request_Status_Submitted");
    }
}

