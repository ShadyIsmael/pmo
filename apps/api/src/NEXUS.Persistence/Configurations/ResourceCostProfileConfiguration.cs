using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXUS.Modules.ResourceManagement.Domain;

namespace NEXUS.Persistence.Configurations;

public class ResourceCostProfileConfiguration : IEntityTypeConfiguration<ResourceCostProfile>
{
    public void Configure(EntityTypeBuilder<ResourceCostProfile> builder)
    {
        builder.ToTable("ResourceCostProfiles");
        builder.HasKey(cp => cp.Id);
        builder.Property(cp => cp.CompensationType).HasMaxLength(50).IsRequired();
        builder.Property(cp => cp.Currency).HasMaxLength(10).IsRequired();
        builder.Property(cp => cp.OvertimeRateType).HasMaxLength(50);

        builder.HasOne(cp => cp.Resource)
            .WithMany(r => r.CostProfiles)
            .HasForeignKey(cp => cp.ResourceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(cp => cp.TenantId);
        builder.HasIndex(cp => new { cp.ResourceId, cp.EffectiveFrom });
        builder.HasIndex(cp => cp.ResourceId);
        builder.Property(cp => cp.RowVersion).IsRowVersion();
    }
}
