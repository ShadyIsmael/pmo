using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXUS.Modules.CostManagement.Domain;

namespace NEXUS.Persistence.Configurations;

public sealed class PaymentNotificationDispatchConfiguration : IEntityTypeConfiguration<PaymentNotificationDispatch>
{
    public void Configure(EntityTypeBuilder<PaymentNotificationDispatch> builder)
    {
        builder.ToTable("PaymentNotificationDispatches");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.NotificationType).HasMaxLength(50).IsRequired();
        builder.HasIndex(item => new { item.TenantId, item.PaymentObligationId, item.UserId, item.NotificationType, item.NotificationDate })
            .IsUnique();
        builder.HasIndex(item => new { item.TenantId, item.PaymentObligationId });
        builder.HasOne<PaymentObligation>()
            .WithMany()
            .HasForeignKey(item => item.PaymentObligationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
