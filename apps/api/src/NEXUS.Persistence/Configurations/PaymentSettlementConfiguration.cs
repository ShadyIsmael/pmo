using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXUS.Modules.CostManagement.Domain;

namespace NEXUS.Persistence.Configurations;

public sealed class PaymentSettlementConfiguration : IEntityTypeConfiguration<PaymentSettlement>
{
    public void Configure(EntityTypeBuilder<PaymentSettlement> builder)
    {
        builder.ToTable("PaymentSettlements", table =>
        {
            table.HasCheckConstraint(
                "CK_PaymentSettlements_ExactlyOneOwner",
                "([PaymentObligationId] IS NOT NULL AND [ProjectPaymentRequestId] IS NULL) OR " +
                "([PaymentObligationId] IS NULL AND [ProjectPaymentRequestId] IS NOT NULL)");
            table.HasCheckConstraint(
                "CK_PaymentSettlements_RequestCurrencyDescription",
                "([ProjectPaymentRequestId] IS NULL) OR " +
                "([Currency] IS NOT NULL AND LEN([Currency]) = 3 AND [Description] IS NOT NULL " +
                "AND LEN(LTRIM(RTRIM([Description]))) > 0)");
            table.HasCheckConstraint(
                "CK_PaymentSettlements_FundingTransferOwner",
                "[FundingTransferId] IS NULL OR ([ProjectPaymentRequestId] IS NOT NULL AND [PaymentObligationId] IS NULL)");
        });
        builder.HasKey(item => item.Id);
        builder.Property(item => item.IdempotencyKey).IsRequired();
        builder.Property(item => item.PaymentReference).HasMaxLength(100).IsRequired();
        builder.Property(item => item.Currency).HasMaxLength(3);
        builder.Property(item => item.Description).HasMaxLength(2000);
        builder.Property(item => item.Notes).HasMaxLength(2000);
        builder.Property(item => item.Amount).HasPrecision(19, 4);
        builder.Property(item => item.RowVersion).IsConcurrencyToken();
        builder.HasIndex(item => new { item.TenantId, item.IdempotencyKey }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.ProjectId, item.PaymentObligationId, item.SettlementDate });
        builder.HasIndex(item => new { item.TenantId, item.ProjectId, item.ProjectPaymentRequestId, item.SettlementDate });
        builder.HasIndex(item => new { item.TenantId, item.ProjectId, item.ProjectPaymentRequestId, item.PaymentReference })
            .IsUnique()
            .HasFilter("[ProjectPaymentRequestId] IS NOT NULL")
            .HasDatabaseName("UX_PaymentSettlements_PaymentRequest_Reference");
        builder.HasOne(item => item.PaymentObligation)
            .WithMany(item => item.Settlements)
            .HasForeignKey(item => new { item.TenantId, item.ProjectId, item.PaymentObligationId })
            .HasPrincipalKey(item => new { item.TenantId, item.ProjectId, item.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.ProjectPaymentRequest)
            .WithMany(item => item.Settlements)
            .HasForeignKey(item => new { item.TenantId, item.ProjectId, item.ProjectPaymentRequestId })
            .HasPrincipalKey(item => new { item.TenantId, item.ProjectId, item.ApprovalRequestId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ProjectPaymentRequestFundingTransfer>()
            .WithMany()
            .HasForeignKey(item => new { item.TenantId, item.ProjectId, item.FundingTransferId })
            .HasPrincipalKey(transfer => new { transfer.TenantId, transfer.ProjectId, transfer.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
