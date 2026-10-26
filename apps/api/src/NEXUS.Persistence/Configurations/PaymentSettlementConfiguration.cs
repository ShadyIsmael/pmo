using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXUS.Modules.CostManagement.Domain;

namespace NEXUS.Persistence.Configurations;

public sealed class PaymentSettlementConfiguration : IEntityTypeConfiguration<PaymentSettlement>
{
    public void Configure(EntityTypeBuilder<PaymentSettlement> builder)
    {
        builder.ToTable("PaymentSettlements");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.IdempotencyKey).IsRequired();
        builder.Property(item => item.PaymentReference).HasMaxLength(100).IsRequired();
        builder.Property(item => item.Notes).HasMaxLength(2000);
        builder.Property(item => item.Amount).HasPrecision(19, 4);
        builder.Property(item => item.RowVersion).IsConcurrencyToken();
        builder.HasIndex(item => new { item.TenantId, item.IdempotencyKey }).IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.PaymentObligationId, item.SettlementDate });
    }
}
