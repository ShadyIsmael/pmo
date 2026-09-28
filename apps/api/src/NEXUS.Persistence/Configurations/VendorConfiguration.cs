using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXUS.Modules.Organization.Domain;
using NEXUS.Modules.ResourceManagement.Domain;

namespace NEXUS.Persistence.Configurations;

public class VendorConfiguration : IEntityTypeConfiguration<Vendor>
{
    public void Configure(EntityTypeBuilder<Vendor> builder)
    {
        builder.ToTable("Vendors");
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Name).HasMaxLength(200).IsRequired();
        builder.Property(v => v.Code).HasMaxLength(50);
        builder.Property(v => v.ContactName).HasMaxLength(200);
        builder.Property(v => v.ContactEmail).HasMaxLength(256);
        builder.Property(v => v.ContactPhone).HasMaxLength(50);
        builder.Property(v => v.Category).HasMaxLength(100);
        builder.Property(v => v.Status).HasMaxLength(50);
        builder.HasIndex(v => v.Name);
        builder.HasIndex(v => v.TenantId);
        builder.HasIndex(v => new { v.TenantId, v.Code }).IsUnique().HasFilter("[Code] IS NOT NULL");
        builder.HasOne<Lookup>()
            .WithMany()
            .HasForeignKey(v => v.VendorCategoryId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Property(v => v.RowVersion).IsRowVersion();
    }
}
