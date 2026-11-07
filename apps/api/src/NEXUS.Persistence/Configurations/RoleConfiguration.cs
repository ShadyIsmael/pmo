using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXUS.Modules.Identity.Domain;

namespace NEXUS.Persistence.Configurations;

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("Roles", table =>
            table.HasCheckConstraint(
                "CK_Roles_SystemRoleKeyRequiresSystemRole",
                "[SystemRoleKey] IS NULL OR [IsSystem] = 1"));
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Name).HasMaxLength(100).IsRequired();
        builder.Property(r => r.Description).HasMaxLength(500);
        builder.Property(r => r.Category).HasMaxLength(100);
        builder.Property(r => r.SystemRoleKey).HasMaxLength(64);
        builder.HasIndex(r => new { r.TenantId, r.Name })
            .IsUnique()
            .HasDatabaseName("UX_Roles_Tenant_Name");
        builder.HasIndex(r => new { r.TenantId, r.SystemRoleKey })
            .IsUnique()
            .HasFilter("[SystemRoleKey] IS NOT NULL AND [IsDeleted] = 0")
            .HasDatabaseName("UX_Roles_Tenant_SystemRoleKey");
        builder.Property(r => r.RowVersion).IsRowVersion();
    }
}
