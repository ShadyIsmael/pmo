using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXUS.Modules.CostManagement.Domain;

namespace NEXUS.Persistence.Configurations;

public sealed class PlannedPaymentLifetimeReservationConfiguration : IEntityTypeConfiguration<PlannedPaymentLifetimeReservation>
{
    public void Configure(EntityTypeBuilder<PlannedPaymentLifetimeReservation> builder)
    {
        builder.ToTable("PlannedPaymentLifetimeReservations", table =>
        {
            table.HasCheckConstraint(
                "CK_PlannedPaymentLifetimeReservations_ReservedAmount",
                "[ReservedAmount] > 0");
            table.HasCheckConstraint(
                "CK_PlannedPaymentLifetimeReservations_ReleasedAmount",
                "[ReleasedAmount] >= 0 AND [ReleasedAmount] <= [ReservedAmount]");
        });

        builder.HasKey(item => item.Id);
        builder.HasAlternateKey(item => new { item.TenantId, item.ProjectId, item.Id })
            .HasName("AK_PlannedPaymentLifetimeReservations_Tenant_Project_Reservation");

        builder.Property(item => item.ReservedAmount).HasPrecision(19, 4);
        builder.Property(item => item.ReleasedAmount).HasPrecision(19, 4);
        builder.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();

        builder.HasOne(item => item.PlannedPayment)
            .WithOne(plan => plan.Reservation)
            .HasForeignKey<PlannedPaymentLifetimeReservation>(item => new { item.TenantId, item.ProjectId, item.PlannedPaymentId })
            .HasPrincipalKey<PlannedPayment>(plan => new { plan.TenantId, plan.ProjectId, plan.Id })
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(item => new { item.TenantId, item.PlannedPaymentId })
            .IsUnique()
            .HasDatabaseName("UX_PlannedPaymentLifetimeReservations_Tenant_Plan");
        builder.HasIndex(item => new { item.TenantId, item.ProjectId })
            .HasDatabaseName("IX_PlannedPaymentLifetimeReservations_Tenant_Project");
    }
}
