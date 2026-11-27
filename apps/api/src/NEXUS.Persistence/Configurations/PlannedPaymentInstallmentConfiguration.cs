using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXUS.Modules.CostManagement.Domain;
using NEXUS.Modules.ProjectPlanning.Domain;

namespace NEXUS.Persistence.Configurations;

public sealed class PlannedPaymentInstallmentConfiguration : IEntityTypeConfiguration<PlannedPaymentInstallment>
{
    public void Configure(EntityTypeBuilder<PlannedPaymentInstallment> builder)
    {
        builder.ToTable("PlannedPaymentInstallments", table =>
        {
            table.HasCheckConstraint(
                "CK_PlannedPaymentInstallments_Sequence",
                "[Sequence] > 0");
            table.HasCheckConstraint(
                "CK_PlannedPaymentInstallments_Amount",
                "[Amount] > 0");
        });

        builder.HasKey(item => item.Id);
        builder.HasAlternateKey(item => new { item.TenantId, item.ProjectId, item.Id })
            .HasName("AK_PlannedPaymentInstallments_Tenant_Project_Installment");

        builder.Property(item => item.Sequence).IsRequired();
        builder.Property(item => item.Amount).HasPrecision(19, 4);
        builder.Property(item => item.Description).HasMaxLength(2000);
        builder.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();

        builder.HasOne(item => item.PlannedPayment)
            .WithMany(plan => plan.Installments)
            .HasForeignKey(item => new { item.TenantId, item.ProjectId, item.PlannedPaymentId })
            .HasPrincipalKey(plan => new { plan.TenantId, plan.ProjectId, plan.Id })
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<PlanActivity>()
            .WithMany()
            .HasForeignKey(item => new { item.TenantId, item.PlanActivityId })
            .HasPrincipalKey(activity => new { activity.TenantId, activity.Id })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(item => new { item.TenantId, item.PlannedPaymentId, item.Sequence })
            .IsUnique()
            .HasDatabaseName("UX_PlannedPaymentInstallments_Plan_Sequence");
        builder.HasIndex(item => new { item.TenantId, item.ProjectId, item.PlanActivityId })
            .HasDatabaseName("IX_PlannedPaymentInstallments_Tenant_Project_Milestone");
    }
}
