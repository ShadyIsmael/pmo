using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXUS.Modules.Portfolio.Domain;

namespace NEXUS.Persistence.Configurations;

public sealed class PortfolioSnapshotConfiguration : IEntityTypeConfiguration<PortfolioSnapshot>
{
    public void Configure(EntityTypeBuilder<PortfolioSnapshot> builder)
    {
        builder.ToTable("PortfolioSnapshots");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(300).IsRequired();
        builder.Property(x => x.HandoverReason).HasMaxLength(500).IsRequired();
        builder.Property(x => x.HandoverNotes).HasMaxLength(2000);
        builder.Property(x => x.Data);
        builder.HasIndex(x => x.TenantId);
        builder.HasIndex(x => new { x.TenantId, x.PortfolioId, x.SnapshotNumber }).IsUnique();
        builder.HasOne(x => x.Portfolio).WithMany().HasForeignKey(x => x.PortfolioId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}
