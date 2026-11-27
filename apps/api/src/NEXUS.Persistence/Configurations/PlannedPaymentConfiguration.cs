using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXUS.Modules.CostManagement.Domain;
using NEXUS.Modules.ProjectManagement.Domain;

namespace NEXUS.Persistence.Configurations;

public sealed class PlannedPaymentConfiguration : IEntityTypeConfiguration<PlannedPayment>
{
    public void Configure(EntityTypeBuilder<PlannedPayment> builder)
    {
        builder.ToTable("PlannedPayments", table =>
        {
            table.HasCheckConstraint(
                "CK_PlannedPayments_TotalAmount",
                "[TotalAmount] > 0");
            table.HasCheckConstraint(
                "CK_PlannedPayments_State",
                $"[State] IN ({Literal(PlannedPaymentStates.Registered)}, {Literal(PlannedPaymentStates.Locked)}, " +
                $"{Literal(PlannedPaymentStates.Replaced)}, {Literal(PlannedPaymentStates.Cancelled)})");
            table.HasCheckConstraint(
                "CK_PlannedPayments_Currency",
                "LEN([Currency]) = 3");
            table.HasCheckConstraint(
                "CK_PlannedPayments_IdempotencyFingerprint",
                "([IdempotencyKey] IS NULL AND [RegistrationFingerprint] IS NULL) OR " +
                "([IdempotencyKey] IS NOT NULL AND [RegistrationFingerprint] IS NOT NULL)");
            table.HasCheckConstraint(
                "CK_PlannedPayments_UpdateIdempotencyFingerprint",
                "([UpdateIdempotencyKey] IS NULL AND [UpdateFingerprint] IS NULL) OR " +
                "([UpdateIdempotencyKey] IS NOT NULL AND [UpdateFingerprint] IS NOT NULL)");
        });

        builder.HasKey(item => item.Id);
        builder.HasAlternateKey(item => new { item.TenantId, item.ProjectId, item.Id })
            .HasName("AK_PlannedPayments_Tenant_Project_Plan");

        builder.Property(item => item.Reference).HasMaxLength(100).IsRequired();
        builder.Property(item => item.Description).HasMaxLength(2000).IsRequired();
        builder.Property(item => item.Currency).HasMaxLength(3).IsRequired();
        builder.Property(item => item.TotalAmount).HasPrecision(19, 4);
        builder.Property(item => item.State).HasMaxLength(24).IsRequired();
        builder.Property(item => item.RegistrationFingerprint).HasMaxLength(128);
        builder.Property(item => item.UpdateFingerprint).HasMaxLength(128);
        builder.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();

        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(item => new { item.TenantId, item.ProjectId })
            .HasPrincipalKey(project => new { project.TenantId, project.Id })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(item => new { item.TenantId, item.ProjectId, item.Reference })
            .IsUnique()
            .HasDatabaseName("UX_PlannedPayments_Tenant_Project_Reference");
        builder.HasIndex(item => new { item.TenantId, item.ProjectId, item.IdempotencyKey })
            .IsUnique()
            .HasFilter("[IdempotencyKey] IS NOT NULL")
            .HasDatabaseName("UX_PlannedPayments_Tenant_Project_Idempotency");
        builder.HasIndex(item => new { item.TenantId, item.ProjectId, item.UpdateIdempotencyKey })
            .IsUnique()
            .HasFilter("[UpdateIdempotencyKey] IS NOT NULL")
            .HasDatabaseName("UX_PlannedPayments_Tenant_Project_UpdateIdempotency");
        builder.HasIndex(item => new { item.TenantId, item.ProjectId, item.State })
            .HasDatabaseName("IX_PlannedPayments_Tenant_Project_State");
    }

    private static string Literal(string value) => $"N'{value.Replace("'", "''", StringComparison.Ordinal)}'";
}
