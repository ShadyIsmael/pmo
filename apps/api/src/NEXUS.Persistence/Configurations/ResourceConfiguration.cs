using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXUS.Modules.ResourceManagement.Domain;

namespace NEXUS.Persistence.Configurations;

public class ResourceConfiguration : IEntityTypeConfiguration<Resource>
{
    public void Configure(EntityTypeBuilder<Resource> builder)
    {
        builder.ToTable("Resources");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Name).HasMaxLength(200).IsRequired();
        builder.Property(r => r.EmployeeId).HasMaxLength(50);
        builder.Property(r => r.JobTitle).HasMaxLength(200);
        builder.Property(r => r.Email).HasMaxLength(256);
        builder.Property(r => r.Phone).HasMaxLength(50);
        builder.Property(r => r.AvatarUrl).HasMaxLength(500);
        builder.Property(r => r.Type).HasMaxLength(50).IsRequired();
        builder.Property(r => r.Status).HasMaxLength(50).IsRequired();
        builder.Property(r => r.Currency).HasMaxLength(10);
        builder.Property(r => r.EmploymentType).HasMaxLength(50);
        builder.Property(r => r.Seniority).HasMaxLength(50);
        builder.Property(r => r.Timezone).HasMaxLength(50);
        builder.Property(r => r.Notes).HasMaxLength(2000);

        builder.HasIndex(r => r.TenantId);
        builder.HasIndex(r => r.Status);
        builder.HasIndex(r => r.Type);
        builder.HasIndex(r => r.Email);
        builder.HasIndex(r => r.EmployeeId);
        builder.HasIndex(r => r.VendorId);
        builder.HasOne(r => r.Vendor)
            .WithMany()
            .HasForeignKey(r => r.VendorId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.Property(r => r.RowVersion).IsRowVersion();
    }
}
