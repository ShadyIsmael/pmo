using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXUS.Modules.Identity.Domain;
using NEXUS.Modules.Organization.Domain;

namespace NEXUS.Persistence.Configurations;

public sealed class OrganizationSettingsGrantConfiguration : IEntityTypeConfiguration<OrganizationSettingsGrant>
{
    public void Configure(EntityTypeBuilder<OrganizationSettingsGrant> builder)
    {
        builder.ToTable("OrganizationSettingsGrants", table =>
            table.HasCheckConstraint(
                "CK_OrganizationSettingsGrants_ManageRequiresRead",
                "[CanManage] = 0 OR [CanRead] = 1"));
        builder.HasKey(grant => grant.Id);
        builder.Property(grant => grant.Id).ValueGeneratedNever();
        builder.Property(grant => grant.TenantId).IsRequired();
        builder.Property(grant => grant.OrganizationId).IsRequired();
        builder.Property(grant => grant.UserId).IsRequired();
        builder.Property(grant => grant.CanRead).IsRequired();
        builder.Property(grant => grant.CanManage).IsRequired();
        builder.Property(grant => grant.CreatedAt).HasColumnType("datetime2").IsRequired();
        builder.Property(grant => grant.CreatedBy).HasMaxLength(100).IsRequired();
        builder.Property(grant => grant.UpdatedAt).HasColumnType("datetime2");
        builder.Property(grant => grant.UpdatedBy).HasMaxLength(100);
        builder.Property(grant => grant.IsDeleted).HasDefaultValue(false);
        builder.Property(grant => grant.RowVersion).IsRowVersion();

        builder.HasOne<Organization>().WithMany()
            .HasForeignKey(grant => new { grant.TenantId, grant.OrganizationId })
            .HasPrincipalKey(organization => new { organization.TenantId, organization.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany()
            .HasForeignKey(grant => new { grant.TenantId, grant.UserId })
            .HasPrincipalKey(user => new { user.TenantId, user.Id })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(grant => new { grant.TenantId, grant.OrganizationId, grant.UserId })
            .IsUnique()
            .HasDatabaseName("UX_OrganizationSettingsGrants_ActiveOrgUser")
            .HasFilter("[IsDeleted] = 0");
        builder.HasIndex(grant => new { grant.TenantId, grant.UserId, grant.OrganizationId })
            .HasDatabaseName("IX_OrganizationSettingsGrants_ActiveUserOrg")
            // SQL Server rejects OR in filtered-index predicates; this broader active-row index keeps the lookup valid.
            .HasFilter("[IsDeleted] = 0");
    }
}
