using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXUS.Modules.Notifications.Domain;
using NEXUS.Modules.Organization.Domain;

namespace NEXUS.Persistence.Configurations;

public sealed class EmailNotificationProfileConfiguration : IEntityTypeConfiguration<EmailNotificationProfileEntity>
{
    public void Configure(EntityTypeBuilder<EmailNotificationProfileEntity> builder)
    {
        builder.ToTable("EmailNotificationProfiles", table =>
        {
            table.HasCheckConstraint(
                "CK_EmailNotificationProfiles_ScopeOwner",
                "(([Scope] = N'Platform' AND [TenantId] IS NULL AND [OwnerId] IS NULL AND [OrganizationId] IS NULL) " +
                "OR ([Scope] = N'Tenant' AND [TenantId] IS NOT NULL AND [OwnerId] = [TenantId] AND [OrganizationId] IS NULL) " +
                "OR ([Scope] = N'Organization' AND [TenantId] IS NOT NULL AND [OwnerId] = [OrganizationId] AND [OrganizationId] IS NOT NULL))");
            table.HasCheckConstraint(
                "CK_EmailNotificationProfiles_Port",
                "[Port] BETWEEN 1 AND 65535");
            table.HasCheckConstraint(
                "CK_EmailNotificationProfiles_SecurityMode",
                "[SecurityMode] IN (N'StartTlsRequired', N'ImplicitTls')");
            table.HasCheckConstraint(
                "CK_EmailNotificationProfiles_AuthenticationMode",
                "[AuthenticationMode] IN (N'UsernamePassword', N'TrustedRelay')");
            table.HasCheckConstraint(
                "CK_EmailNotificationProfiles_EnabledProfile",
                "[Enabled] = 0 OR (LEN(LTRIM(RTRIM([Host]))) > 0 AND LEN(LTRIM(RTRIM([FromAddress]))) > 0 AND " +
                "(([AuthenticationMode] = N'UsernamePassword' AND LEN(LTRIM(RTRIM([UserName]))) > 0 AND [ProtectedPassword] IS NOT NULL) " +
                "OR ([AuthenticationMode] = N'TrustedRelay' AND [UserName] IS NULL AND [ProtectedPassword] IS NULL)))");
        });

        builder.HasKey(profile => profile.Id);
        builder.Property(profile => profile.Id).ValueGeneratedNever();
        builder.Property(profile => profile.Scope).HasMaxLength(16).IsRequired();
        builder.Property(profile => profile.TenantId);
        builder.Property(profile => profile.OwnerId);
        builder.Property(profile => profile.OrganizationId);
        builder.Property(profile => profile.Enabled).IsRequired();
        builder.Property(profile => profile.Host).HasMaxLength(255).IsRequired();
        builder.Property(profile => profile.Port).IsRequired();
        builder.Property(profile => profile.SecurityMode).HasMaxLength(32).IsRequired();
        builder.Property(profile => profile.AuthenticationMode).HasMaxLength(32).IsRequired();
        builder.Property(profile => profile.UserName).HasMaxLength(512);
        builder.Property(profile => profile.ProtectedPassword).HasMaxLength(8192);
        builder.Property(profile => profile.FromAddress).HasMaxLength(320).IsRequired();
        builder.Property(profile => profile.FromName).HasMaxLength(200).IsRequired();
        builder.Property(profile => profile.ReplyToAddress).HasMaxLength(320);
        builder.Property(profile => profile.CreatedAt).HasColumnType("datetime2").IsRequired();
        builder.Property(profile => profile.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(profile => profile.UpdatedAt).HasColumnType("datetime2");
        builder.Property(profile => profile.UpdatedBy).HasMaxLength(100);
        builder.Property(profile => profile.DeletedAt).HasColumnType("datetime2");
        builder.Property(profile => profile.DeletedBy).HasMaxLength(100);
        builder.Property(profile => profile.RowVersion).IsRowVersion();

        builder.HasOne<Tenant>().WithMany()
            .HasForeignKey(profile => profile.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Organization>().WithMany()
            .HasForeignKey(profile => new { profile.TenantId, profile.OrganizationId })
            .HasPrincipalKey(organization => new { organization.TenantId, organization.Id })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(profile => profile.Scope)
            .IsUnique()
            .HasDatabaseName("UX_EmailNotificationProfiles_Platform")
            .HasFilter("[Scope] = N'Platform' AND [DeletedAt] IS NULL");
        // Tenant scope has one active profile per Tenant; keep its key shape distinct
        // from the Organization index so EF does not merge two filtered indexes.
        builder.HasIndex(profile => profile.TenantId)
            .IsUnique()
            .HasDatabaseName("UX_EmailNotificationProfiles_Tenant")
            .HasFilter("[Scope] = N'Tenant' AND [DeletedAt] IS NULL");
        builder.HasIndex(profile => new { profile.TenantId, profile.OwnerId })
            .IsUnique()
            .HasDatabaseName("UX_EmailNotificationProfiles_Organization")
            .HasFilter("[Scope] = N'Organization' AND [DeletedAt] IS NULL");
        builder.HasIndex(profile => new { profile.Scope, profile.TenantId, profile.OwnerId })
            .HasDatabaseName("IX_EmailNotificationProfiles_ActiveOwner")
            .HasFilter("[DeletedAt] IS NULL");
    }
}
