using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXUS.Modules.Organization.Domain;

namespace NEXUS.Persistence.Configurations;

public sealed class ConfigurationSettingConfiguration : IEntityTypeConfiguration<ConfigurationSetting>
{
    public void Configure(EntityTypeBuilder<ConfigurationSetting> builder)
    {
        builder.ToTable("ConfigurationSettings", table =>
        {
            table.HasCheckConstraint(
                "CK_ConfigurationSettings_ScopeOwner",
                "(([Scope] = N'Platform' AND [TenantId] IS NULL AND [OwnerId] IS NULL AND [OrganizationId] IS NULL) " +
                "OR ([Scope] = N'Tenant' AND [TenantId] IS NOT NULL AND [OwnerId] = [TenantId] AND [OrganizationId] IS NULL) " +
                "OR ([Scope] = N'Organization' AND [TenantId] IS NOT NULL AND [OwnerId] = [OrganizationId] AND [OrganizationId] IS NOT NULL))");
            table.HasCheckConstraint(
                "CK_ConfigurationSettings_ValueJson_IsJson",
                "ISJSON([ValueJson], VALUE) = 1");
            table.HasCheckConstraint(
                "CK_ConfigurationSettings_ValueType",
                "[ValueType] IN (N'String', N'Integer', N'Decimal', N'Boolean')");
        });

        builder.HasKey(setting => setting.Id);
        builder.Property(setting => setting.Id).ValueGeneratedNever();
        builder.Property(setting => setting.Scope).HasMaxLength(16).IsRequired();
        builder.Property(setting => setting.TenantId);
        builder.Property(setting => setting.OwnerId);
        builder.Property(setting => setting.OrganizationId);
        builder.Property(setting => setting.SettingKey).HasMaxLength(160).IsRequired();
        builder.Property(setting => setting.ValueType).HasMaxLength(16).IsRequired();
        builder.Property(setting => setting.ValueJson).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(setting => setting.CreatedAt).HasColumnType("datetime2").IsRequired();
        builder.Property(setting => setting.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(setting => setting.UpdatedAt).HasColumnType("datetime2");
        builder.Property(setting => setting.UpdatedBy).HasMaxLength(100);
        builder.Property(setting => setting.DeletedAt).HasColumnType("datetime2");
        builder.Property(setting => setting.DeletedBy).HasMaxLength(100);
        builder.Property(setting => setting.RowVersion).IsRowVersion();

        // This nullable scope-specific FK lets SQL Server enforce Organization ownership
        // without treating Platform or Tenant owner IDs as Organization IDs.
        builder.HasOne<Organization>().WithMany()
            .HasForeignKey(setting => new { setting.TenantId, setting.OrganizationId })
            .HasPrincipalKey(organization => new { organization.TenantId, organization.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Tenant>().WithMany()
            .HasForeignKey(setting => setting.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        // Separate filtered unique indexes avoid SQL Server's NULL-key behavior for
        // platform rows and make the active uniqueness rule explicit per scope.
        builder.HasIndex(setting => setting.SettingKey)
            .IsUnique()
            .HasDatabaseName("UX_ConfigurationSettings_Platform_Key")
            .HasFilter("[Scope] = N'Platform' AND [DeletedAt] IS NULL");

        builder.HasIndex(setting => new { setting.TenantId, setting.OwnerId, setting.SettingKey })
            .IsUnique()
            .HasDatabaseName("UX_ConfigurationSettings_Tenant_Owner_Key")
            .HasFilter("[Scope] = N'Tenant' AND [DeletedAt] IS NULL");

        builder.HasIndex(setting => new { setting.TenantId, setting.OwnerId, setting.SettingKey })
            .IsUnique()
            .HasDatabaseName("UX_ConfigurationSettings_Organization_Owner_Key")
            .HasFilter("[Scope] = N'Organization' AND [DeletedAt] IS NULL");

        builder.HasIndex(setting => new { setting.Scope, setting.TenantId, setting.OwnerId })
            .HasDatabaseName("IX_ConfigurationSettings_ActiveOwner")
            .HasFilter("[DeletedAt] IS NULL");
    }
}
