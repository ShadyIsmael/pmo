using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXUS.Modules.ResourceManagement.Domain;

namespace NEXUS.Persistence.Configurations;

public class ContractConfiguration : IEntityTypeConfiguration<Contract>
{
    public void Configure(EntityTypeBuilder<Contract> builder)
    {
        builder.ToTable("Contracts");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Type).HasMaxLength(100).IsRequired();
        builder.Property(c => c.Currency).HasMaxLength(10);
        builder.Property(c => c.Terms).HasMaxLength(4000);
        builder.Property(c => c.VendorName).HasMaxLength(200);
        builder.Property(c => c.Status).HasMaxLength(50).IsRequired();

        builder.HasOne(c => c.Resource)
            .WithMany()
            .HasForeignKey(c => c.ResourceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(c => c.TenantId);
    }
}
