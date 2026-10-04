using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXUS.Modules.Organization.Domain;

namespace NEXUS.Persistence.Configurations;

public sealed class OrganizationalUnitConfiguration : IEntityTypeConfiguration<OrganizationalUnit>
{
    public void Configure(EntityTypeBuilder<OrganizationalUnit> builder)
    {
        builder.ToTable("OrganizationalUnits");
        builder.HasKey(unit => unit.Id);
        builder.Property(unit => unit.Code).HasMaxLength(50).IsRequired();
        builder.Property(unit => unit.Name).HasMaxLength(200).IsRequired();
        builder.Property(unit => unit.TypeCode).HasMaxLength(100).IsRequired();
        builder.Property(unit => unit.Description).HasMaxLength(2000);
        builder.HasIndex(unit => new { unit.TenantId, unit.Code }).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(unit => new { unit.TenantId, unit.ParentOrganizationalUnitId, unit.IsActive });
        builder.HasIndex(unit => new { unit.TenantId, unit.TypeCode, unit.IsActive });
        builder.HasOne(unit => unit.ParentOrganizationalUnit)
            .WithMany(unit => unit.Children)
            .HasForeignKey(unit => unit.ParentOrganizationalUnitId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Property(unit => unit.RowVersion).IsRowVersion();
    }
}
