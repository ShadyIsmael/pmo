using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXUS.Modules.Organization.Domain;

namespace NEXUS.Persistence.Configurations;

public sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("Tenants", table => table.HasCheckConstraint(
            "CK_Tenants_Status",
            "[Status] IN (N'Provisioning', N'Active', N'Suspended', N'Retired')"));
        builder.HasKey(tenant => tenant.Id);
        builder.Property(tenant => tenant.Id).ValueGeneratedNever();
        builder.Property(tenant => tenant.Code).HasMaxLength(64).IsRequired();
        builder.Property(tenant => tenant.DisplayName).HasMaxLength(200).IsRequired();
        builder.Property(tenant => tenant.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(tenant => tenant.CreatedAt).HasColumnType("datetime2").IsRequired();
        builder.Property(tenant => tenant.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(tenant => tenant.UpdatedAt).HasColumnType("datetime2");
        builder.Property(tenant => tenant.UpdatedBy).HasMaxLength(100);
        builder.Property(tenant => tenant.RowVersion).IsRowVersion();
        builder.HasIndex(tenant => tenant.Code).IsUnique().HasDatabaseName("UX_Tenants_Code");
        builder.HasIndex(tenant => tenant.Status).HasDatabaseName("IX_Tenants_Status");
        builder.HasMany(tenant => tenant.Organizations)
            .WithOne()
            .HasForeignKey(organization => organization.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
