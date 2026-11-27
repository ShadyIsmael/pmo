using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXUS.Modules.Identity.Domain;
using NEXUS.Modules.Organization.Domain;

namespace NEXUS.Persistence.Configurations;

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("Roles", table =>
        {
            table.HasCheckConstraint(
                "CK_Roles_OrganizationOwnershipMatchesRoleKind",
                "([IsSystem] = 0 AND [SystemRoleKey] IS NULL AND (([OrganizationId] IS NOT NULL AND [IsLegacyOrganizationOwnershipUnresolved] = 0) OR ([OrganizationId] IS NULL AND [IsLegacyOrganizationOwnershipUnresolved] = 1))) OR ([IsSystem] = 1 AND [OrganizationId] IS NULL AND [IsLegacyOrganizationOwnershipUnresolved] = 0 AND [SystemRoleKey] COLLATE Latin1_General_100_BIN2 IN (N'tenant.admin', N'organization.admin'))");
            table.HasCheckConstraint(
                "CK_Roles_SystemRoleKeyRequiresSystemRole",
                "([IsSystem] = 0 AND [SystemRoleKey] IS NULL AND ([RoleKey] IS NULL OR [RoleKey] COLLATE Latin1_General_100_BIN2 NOT IN (N'tenant.admin', N'organization.admin'))) OR ([IsSystem] = 1 AND [SystemRoleKey] IS NOT NULL AND [RoleKey] IS NOT NULL AND [SystemRoleKey] COLLATE Latin1_General_100_BIN2 IN (N'tenant.admin', N'organization.admin') AND [RoleKey] COLLATE Latin1_General_100_BIN2 = [SystemRoleKey] COLLATE Latin1_General_100_BIN2)");
        });
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Name).HasMaxLength(100).IsRequired();
        builder.Property(r => r.Description).HasMaxLength(500);
        builder.Property(r => r.Category).HasMaxLength(100);
        builder.Property(r => r.SystemRoleKey).HasMaxLength(64);
        builder.Property(r => r.RoleKey).HasMaxLength(64);
        builder.Property(r => r.OrganizationId);
        builder.Property(r => r.IsLegacyOrganizationOwnershipUnresolved);
        builder.Property(r => r.RowVersion).IsRowVersion();

        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(r => new { r.TenantId, r.OrganizationId })
            .HasPrincipalKey(organization => new { organization.TenantId, organization.Id })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => new { r.TenantId, r.Name })
            .IsUnique()
            .HasDatabaseName("UX_Roles_Tenant_Unowned_Name")
            .HasFilter("[OrganizationId] IS NULL AND [IsDeleted] = 0");
        builder.HasIndex(r => new { r.TenantId, r.RoleKey })
            .IsUnique()
            .HasDatabaseName("UX_Roles_Tenant_RoleKey")
            .HasFilter("[RoleKey] IS NOT NULL AND [IsDeleted] = 0");
        builder.HasIndex(r => new { r.TenantId, r.SystemRoleKey })
            .IsUnique()
            .HasDatabaseName("UX_Roles_Tenant_SystemRoleKey")
            .HasFilter("[SystemRoleKey] IS NOT NULL AND [IsDeleted] = 0");
        builder.HasIndex(r => new { r.TenantId, r.OrganizationId, r.Name })
            .IsUnique()
            .HasDatabaseName("UX_Roles_Tenant_Organization_Name")
            .HasFilter("[OrganizationId] IS NOT NULL AND [IsDeleted] = 0");
    }
}