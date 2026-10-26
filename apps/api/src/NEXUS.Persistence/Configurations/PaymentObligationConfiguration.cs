using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXUS.Modules.CostManagement.Domain;

namespace NEXUS.Persistence.Configurations;

public sealed class PaymentObligationConfiguration : IEntityTypeConfiguration<PaymentObligation>
{
    public void Configure(EntityTypeBuilder<PaymentObligation> builder)
    {
        builder.ToTable("PaymentObligations");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.PaymentReference).HasMaxLength(100).IsRequired();
        builder.Property(item => item.ProjectName).HasMaxLength(250).IsRequired();
        builder.Property(item => item.Purpose).HasMaxLength(250).IsRequired();
        builder.Property(item => item.Description).HasMaxLength(2000).IsRequired();
        builder.Property(item => item.Currency).HasMaxLength(3).IsRequired();
        builder.Property(item => item.Notes).HasMaxLength(2000);
        builder.Property(item => item.Amount).HasPrecision(19, 4);
        builder.Property(item => item.RowVersion).IsConcurrencyToken();
        builder.HasIndex(item => new { item.TenantId, item.ProjectId, item.DueDate });
        builder.HasIndex(item => new { item.TenantId, item.ProjectId, item.PaymentReference });
        builder.HasIndex(item => new { item.TenantId, item.MilestoneId });

        builder.HasMany(item => item.Settlements)
            .WithOne(item => item.PaymentObligation)
            .HasForeignKey(item => item.PaymentObligationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
