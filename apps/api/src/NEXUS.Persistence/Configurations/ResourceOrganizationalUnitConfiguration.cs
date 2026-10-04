using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXUS.Modules.Organization.Domain;

namespace NEXUS.Persistence.Configurations;

public sealed class ResourceOrganizationalUnitConfiguration : IEntityTypeConfiguration<ResourceOrganizationalUnit>
{
    public void Configure(EntityTypeBuilder<ResourceOrganizationalUnit> builder)
    {
        builder.ToTable("ResourceOrganizationalUnits");
        builder.HasKey(membership => membership.Id);
        builder.Property(membership => membership.RowVersion).IsRowVersion();
        builder.HasIndex(membership => new { membership.TenantId, membership.ResourceId, membership.EffectiveFrom });
        builder.HasIndex(membership => new { membership.TenantId, membership.OrganizationalUnitId, membership.IsActive });
        builder.HasIndex(membership => new { membership.TenantId, membership.ResourceId, membership.IsPrimary, membership.IsActive })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0 AND [IsActive] = 1 AND [IsPrimary] = 1");
        builder.HasOne(membership => membership.OrganizationalUnit)
            .WithMany()
            .HasForeignKey(membership => membership.OrganizationalUnitId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
