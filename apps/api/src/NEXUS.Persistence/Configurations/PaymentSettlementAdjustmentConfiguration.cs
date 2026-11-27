using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXUS.Modules.CostManagement.Domain;

namespace NEXUS.Persistence.Configurations;

public sealed class PaymentSettlementAdjustmentConfiguration : IEntityTypeConfiguration<PaymentSettlementAdjustment>
{
    public void Configure(EntityTypeBuilder<PaymentSettlementAdjustment> builder)
    {
        builder.ToTable("PaymentSettlementAdjustments", table =>
            table.HasCheckConstraint("CK_PaymentSettlementAdjustments_Status",
                "[Status] IN (N'PendingApproval', N'Approved', N'Rejected', N'Executed')"));
        builder.HasKey(item => item.Id);
        builder.Property(item => item.AdjustmentType).HasMaxLength(32).IsRequired();
        builder.Property(item => item.Amount).HasPrecision(19, 4);
        builder.Property(item => item.Currency).HasMaxLength(3).IsRequired();
        builder.Property(item => item.Reason).HasMaxLength(2000).IsRequired();
        builder.Property(item => item.Status).HasMaxLength(32).HasDefaultValue(PaymentSettlementAdjustment.Executed).IsRequired();
        builder.Property(item => item.RowVersion).IsConcurrencyToken();
        builder.HasIndex(item => new { item.TenantId, item.IdempotencyKey }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.ApprovalRequestId }).IsUnique()
            .HasFilter("[ApprovalRequestId] IS NOT NULL");
        builder.HasIndex(item => new { item.TenantId, item.ExecutionIdempotencyKey }).IsUnique()
            .HasFilter("[ExecutionIdempotencyKey] IS NOT NULL");
        builder.HasIndex(item => new { item.TenantId, item.ProjectId, item.OriginalSettlementId });
        builder.HasOne(item => item.OriginalSettlement)
            .WithMany()
            .HasForeignKey(item => new { item.TenantId, item.ProjectId, item.OriginalSettlementId })
            .HasPrincipalKey(item => new { item.TenantId, item.ProjectId, item.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<NEXUS.Modules.Approvals.Domain.ApprovalRequest>()
            .WithMany()
            .HasForeignKey(item => new { item.TenantId, item.ApprovalRequestId })
            .HasPrincipalKey(request => new { request.TenantId, request.Id })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
