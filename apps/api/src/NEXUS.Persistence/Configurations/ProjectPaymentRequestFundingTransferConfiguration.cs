using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXUS.Modules.CostManagement.Domain;
using NEXUS.Modules.ProjectManagement.Domain;

namespace NEXUS.Persistence.Configurations;

public sealed class ProjectPaymentRequestFundingTransferConfiguration : IEntityTypeConfiguration<ProjectPaymentRequestFundingTransfer>
{
    public void Configure(EntityTypeBuilder<ProjectPaymentRequestFundingTransfer> builder)
    {
        builder.ToTable("ProjectPaymentRequestFundingTransfers", table =>
            table.HasCheckConstraint("CK_ProjectPaymentRequestFundingTransfers_Amount",
                "[Amount] > 0 AND [SourceFiscalYear] BETWEEN 1900 AND 2199 AND " +
                "[DestinationFiscalYear] = [SourceFiscalYear] + 1 AND [Currency] <> N''"));
        builder.HasKey(item => item.Id);
        builder.HasAlternateKey(item => new { item.TenantId, item.ProjectId, item.Id })
            .HasName("AK_ProjectPaymentRequestFundingTransfers_Tenant_Project_Id");
        builder.Property(item => item.Amount).HasPrecision(19, 4);
        builder.Property(item => item.Currency).HasMaxLength(3).IsRequired();
        builder.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();

        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(item => new { item.TenantId, item.ProjectId })
            .HasPrincipalKey(item => new { item.TenantId, item.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ProjectPaymentRequestDetail>()
            .WithMany()
            .HasForeignKey(item => new { item.TenantId, item.ProjectId, ApprovalRequestId = item.PaymentRequestId })
            .HasPrincipalKey(item => new { item.TenantId, item.ProjectId, ApprovalRequestId = item.ApprovalRequestId })
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
        builder.HasOne<ProjectAnnualBudgetClosing>()
            .WithMany()
            .HasForeignKey(item => new { item.TenantId, item.ProjectId, item.ClosingId })
            .HasPrincipalKey(item => new { item.TenantId, item.ProjectId, item.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ProjectPaymentRequestFundingTransfer>()
            .WithMany()
            .HasForeignKey(item => new { item.TenantId, item.ProjectId, item.SupersedesFundingTransferId })
            .HasPrincipalKey(item => new { item.TenantId, item.ProjectId, item.Id })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(item => new { item.TenantId, item.ProjectId, item.PaymentRequestId, item.DestinationFiscalYear })
            .IsUnique().HasDatabaseName("UX_ProjectPaymentRequestFundingTransfers_Request_DestinationFiscalYear");
        builder.HasIndex(item => new { item.TenantId, item.IdempotencyKey })
            .IsUnique().HasDatabaseName("UX_ProjectPaymentRequestFundingTransfers_Idempotency");
        builder.HasIndex(item => new { item.TenantId, item.ProjectId, item.SourceAnnualBudgetId, item.PaymentRequestId })
            .HasDatabaseName("IX_ProjectPaymentRequestFundingTransfers_SourceBudget_Request");
        builder.HasIndex(item => new { item.TenantId, item.ProjectId, item.DestinationAnnualBudgetId, item.PaymentRequestId })
            .HasDatabaseName("IX_ProjectPaymentRequestFundingTransfers_DestinationBudget_Request");
        builder.HasIndex(item => new { item.TenantId, item.ProjectId, item.SupersedesFundingTransferId })
            .IsUnique().HasFilter("[SupersedesFundingTransferId] IS NOT NULL")
            .HasDatabaseName("UX_ProjectPaymentRequestFundingTransfers_ActiveChainSuccessor");
    }
}
