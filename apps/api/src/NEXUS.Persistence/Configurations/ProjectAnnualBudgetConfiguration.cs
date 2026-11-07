using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXUS.Modules.CostManagement.Domain;
using NEXUS.Modules.ProjectManagement.Domain;

namespace NEXUS.Persistence.Configurations;

public sealed class ProjectAnnualBudgetConfiguration : IEntityTypeConfiguration<ProjectAnnualBudget>
{
    public void Configure(EntityTypeBuilder<ProjectAnnualBudget> builder)
    {
        builder.ToTable("ProjectAnnualBudgets", table =>
        {
            table.HasCheckConstraint(
                "CK_ProjectAnnualBudgets_Status",
                "[Status] IN (N'Draft', N'Approved')");
            table.HasCheckConstraint(
                "CK_ProjectAnnualBudgets_FiscalPeriod",
                "[FiscalYear] BETWEEN 1900 AND 2200 AND [FiscalYearStartDate] <= [FiscalYearEndDate]");
            table.HasCheckConstraint(
                "CK_ProjectAnnualBudgets_Amounts",
                "[NewApprovedFunding] >= 0 AND [ApprovedCarryForward] >= 0 AND " +
                "[ApprovedReservedCarryForward] >= 0 AND [ApprovedReservedCarryForward] <= [ApprovedCarryForward] AND " +
                "([NewApprovedFunding] + [ApprovedCarryForward] + [ApprovedAdjustments]) >= 0");
            table.HasCheckConstraint(
                "CK_ProjectAnnualBudgets_ClosingMetadata",
                "([ClosedAtUtc] IS NULL AND [ClosedByClosingId] IS NULL AND [ClosedBy] IS NULL) OR " +
                "([ClosedAtUtc] IS NOT NULL AND [ClosedByClosingId] IS NOT NULL AND [ClosedBy] IS NOT NULL)");
        });

        builder.HasKey(item => item.Id);
        builder.HasAlternateKey(item => new { item.TenantId, item.ProjectId, item.Id })
            .HasName("AK_ProjectAnnualBudgets_Tenant_Project_Id");
        builder.Property(item => item.FinancialReference).HasMaxLength(100).IsRequired();
        builder.Property(item => item.Currency).HasMaxLength(3).IsRequired();
        builder.Property(item => item.Status).HasMaxLength(16).IsRequired();
        // Match the persistence-wide monetary convention; the API validates the same scale.
        builder.Property(item => item.NewApprovedFunding).HasPrecision(19, 4);
        builder.Property(item => item.ApprovedCarryForward).HasPrecision(19, 4);
        builder.Property(item => item.ApprovedReservedCarryForward).HasPrecision(19, 4);
        builder.Property(item => item.ApprovedAdjustments).HasPrecision(19, 4);
        builder.Property(item => item.FiscalYearStartDate).HasColumnType("date");
        builder.Property(item => item.FiscalYearEndDate).HasColumnType("date");
        builder.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();
        builder.Ignore(item => item.TotalAnnualBudget);

        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(item => new { item.TenantId, item.ProjectId })
            .HasPrincipalKey(project => new { project.TenantId, project.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ProjectAnnualBudgetClosing>()
            .WithMany()
            .HasForeignKey(item => new { item.TenantId, item.ProjectId, item.ClosedByClosingId })
            .HasPrincipalKey(closing => new { closing.TenantId, closing.ProjectId, closing.Id })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(item => new { item.TenantId, item.ProjectId, item.FiscalYear })
            .IsUnique()
            .HasDatabaseName("UX_ProjectAnnualBudgets_Tenant_Project_FiscalYear");
        builder.HasIndex(item => new { item.TenantId, item.FinancialReference })
            .IsUnique()
            .HasDatabaseName("UX_ProjectAnnualBudgets_Tenant_FinancialReference");
        builder.HasIndex(item => new { item.TenantId, item.FiscalYear })
            .HasDatabaseName("IX_ProjectAnnualBudgets_Tenant_FiscalYear");
    }
}
