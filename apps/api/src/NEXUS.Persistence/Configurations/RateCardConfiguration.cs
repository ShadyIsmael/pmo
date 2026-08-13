using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXUS.Modules.ResourceManagement.Domain;

namespace NEXUS.Persistence.Configurations;

public class RateCardConfiguration : IEntityTypeConfiguration<RateCard>
{
    public void Configure(EntityTypeBuilder<RateCard> builder)
    {
        builder.ToTable("RateCards");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Name).HasMaxLength(200).IsRequired();
        builder.Property(r => r.Role).HasMaxLength(200).IsRequired();
        builder.Property(r => r.Seniority).HasMaxLength(50);
        builder.Property(r => r.Currency).HasMaxLength(10);
        builder.HasIndex(r => r.TenantId);
    }
}
