using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXUS.Modules.Organization.Domain;

namespace NEXUS.Persistence.Configurations;

public sealed class LookupConfiguration : IEntityTypeConfiguration<Lookup>
{
    public void Configure(EntityTypeBuilder<Lookup> builder)
    {
        builder.ToTable("Lookups");
        builder.HasKey(lookup => lookup.Id);
        builder.Property(lookup => lookup.Category).HasMaxLength(100).IsRequired();
        builder.Property(lookup => lookup.Code).HasMaxLength(100).IsRequired();
        builder.Property(lookup => lookup.Name).HasMaxLength(200).IsRequired();
        builder.Property(lookup => lookup.NameEn).HasMaxLength(200).IsRequired();
        builder.Property(lookup => lookup.NameAr).HasMaxLength(200).IsRequired();
        builder.Property(lookup => lookup.Description).HasMaxLength(1000);
        builder.Property(lookup => lookup.DisplayOrder).HasColumnName("SortOrder");
        builder.Property(lookup => lookup.Color).HasMaxLength(50);
        builder.HasIndex(lookup => new { lookup.TenantId, lookup.Category, lookup.Code }).IsUnique();
        builder.HasIndex(lookup => new { lookup.TenantId, lookup.Category, lookup.IsActive, lookup.DisplayOrder });
    }
}
