using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXUS.Modules.Approvals.Domain;
using NEXUS.Modules.CostManagement.Domain;
using NEXUS.Modules.ProjectManagement.Domain;

namespace NEXUS.Persistence.Configurations;

public sealed class ProjectAnnualBudgetClosingConfiguration : IEntityTypeConfiguration<ProjectAnnualBudgetClosing>
{
    public void Configure(EntityTypeBuilder<ProjectAnnualBudgetClosing> builder)
    {
        builder.ToTable("ProjectAnnualBudgetClosings", table =>
        {
            table.HasCheckConstraint("CK_ProjectAnnualBudgetClosings_Status",
                "[Status] IN (N'Review', N'PendingApproval', N'Rejected', N'Closed', N'Cancelled')");
            table.HasCheckConstraint("CK_ProjectAnnualBudgetClosings_FiscalPeriod",
                "[FiscalYear] BETWEEN 1900 AND 2200 AND [FiscalYearStartDate] <= [FiscalYearEndDate]");
            table.HasCheckConstraint("CK_ProjectAnnualBudgetClosings_CarryForward",
                "[ProposedFreeCarryForward] >= 0 AND [ApprovedFreeCarryForward] >= 0 AND " +
                "[ApprovedReservedCarryForward] >= 0 AND [NonCarriedFreeBalance] >= 0");
        });

        builder.HasKey(item => item.Id);
        builder.HasAlternateKey(item => new { item.TenantId, item.ProjectId, item.Id })
            .HasName("AK_ProjectAnnualBudgetClosings_Tenant_Project_Id");
        builder.Property(item => item.Currency).HasMaxLength(3).IsRequired();
        builder.Property(item => item.Status).HasMaxLength(24).IsRequired();
        builder.Property(item => item.ApprovalMode).HasMaxLength(24);
        builder.Property(item => item.SnapshotFingerprint).HasMaxLength(128).IsRequired();
        builder.Property(item => item.SnapshotJson).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(item => item.IntegrityIssuesJson).HasColumnType("nvarchar(max)");
        foreach (var property in new[]
        {
            nameof(ProjectAnnualBudgetClosing.ApprovedAnnualBudget), nameof(ProjectAnnualBudgetClosing.NewFunding),
            nameof(ProjectAnnualBudgetClosing.IncomingCarryForward), nameof(ProjectAnnualBudgetClosing.ApprovedAdjustments),
            nameof(ProjectAnnualBudgetClosing.ApprovedPaymentRequestAllocations), nameof(ProjectAnnualBudgetClosing.ApprovedReversalReleases),
            nameof(ProjectAnnualBudgetClosing.EffectiveActiveAllocations), nameof(ProjectAnnualBudgetClosing.ActualSettlementsInFiscalYear),
            nameof(ProjectAnnualBudgetClosing.OutstandingCommitments), nameof(ProjectAnnualBudgetClosing.FreeAvailableBudget),
            nameof(ProjectAnnualBudgetClosing.TotalUnspentBudget), nameof(ProjectAnnualBudgetClosing.ProposedFreeCarryForward),
            nameof(ProjectAnnualBudgetClosing.ApprovedFreeCarryForward), nameof(ProjectAnnualBudgetClosing.ApprovedReservedCarryForward),
            nameof(ProjectAnnualBudgetClosing.NonCarriedFreeBalance), nameof(ProjectAnnualBudgetClosing.NextFiscalYearNewFunding),
            nameof(ProjectAnnualBudgetClosing.NextFiscalYearAdjustments)
        }) builder.Property<decimal>(property).HasPrecision(19, 4);
        builder.Property(item => item.FiscalYearStartDate).HasColumnType("date");
        builder.Property(item => item.FiscalYearEndDate).HasColumnType("date");
        builder.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();

        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(item => new { item.TenantId, item.ProjectId })
            .HasPrincipalKey(item => new { item.TenantId, item.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ProjectAnnualBudget>()
            .WithMany()
            .HasForeignKey(item => new { item.TenantId, item.ProjectId, item.SourceAnnualBudgetId })
            .HasPrincipalKey(item => new { item.TenantId, item.ProjectId, item.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ProjectAnnualBudget>()
            .WithMany()
            .HasForeignKey(item => new { item.TenantId, item.ProjectId, item.DestinationAnnualBudgetId })
            .HasPrincipalKey(item => new { item.TenantId, item.ProjectId, item.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApprovalRequest>()
            .WithMany()
            .HasForeignKey(item => new { item.TenantId, ApprovalId = item.ApprovalRequestId })
            .HasPrincipalKey(item => new { item.TenantId, item.Id })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(item => new { item.TenantId, item.ProjectId, item.FiscalYear })
            .IsUnique().HasFilter("[Status] = N'Closed'")
            .HasDatabaseName("UX_ProjectAnnualBudgetClosings_Closed_Project_FiscalYear");
        builder.HasIndex(item => new { item.TenantId, item.ProjectId, item.FiscalYear })
            .IsUnique().HasFilter("[Status] = N'Review'")
            .HasDatabaseName("UX_ProjectAnnualBudgetClosings_Review_Project_FiscalYear");
        builder.HasIndex(item => new { item.TenantId, item.ProjectId, item.FiscalYear })
            .IsUnique().HasFilter("[Status] = N'PendingApproval'")
            .HasDatabaseName("UX_ProjectAnnualBudgetClosings_Pending_Project_FiscalYear");
        builder.HasIndex(item => new { item.TenantId, item.ProjectId, item.IdempotencyKey })
            .IsUnique().HasDatabaseName("UX_ProjectAnnualBudgetClosings_Idempotency");
        builder.HasIndex(item => new { item.TenantId, item.ProjectId, item.FiscalYear, item.Status })
            .HasDatabaseName("IX_ProjectAnnualBudgetClosings_Project_FiscalYear_Status");
    }
}
