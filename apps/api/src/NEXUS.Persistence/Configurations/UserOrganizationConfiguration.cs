using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXUS.Modules.Identity.Domain;
using NEXUS.Modules.Organization.Domain;

namespace NEXUS.Persistence.Configurations;

public sealed class UserOrganizationConfiguration : IEntityTypeConfiguration<UserOrganization>
{
    public void Configure(EntityTypeBuilder<UserOrganization> builder)
    {
        builder.ToTable("UserOrganizations");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.RowVersion).IsRowVersion();

        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(item => new { item.TenantId, item.OrganizationId })
            .HasPrincipalKey(organization => new { organization.TenantId, organization.Id })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<NEXUS.Modules.Identity.Domain.User>()
            .WithMany(user => user.UserOrganizations)
            .HasForeignKey(item => new { item.TenantId, item.UserId })
            .HasPrincipalKey(user => new { user.TenantId, user.Id })
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(item => new { item.TenantId, item.OrganizationId, item.UserId })
            .HasDatabaseName("IX_UserOrganizations_ActiveOrgUser")
            .HasFilter("[IsDeleted] = 0");
        builder.HasIndex(item => new { item.TenantId, item.UserId, item.OrganizationId })
            .IsUnique()
            .HasDatabaseName("UX_UserOrganizations_ActiveUserOrg")
            .HasFilter("[IsDeleted] = 0");
    }
}