using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXUS.Modules.Portfolio.Domain;

namespace NEXUS.Persistence.Configurations;

public sealed class PortfolioConfiguration : IEntityTypeConfiguration<Portfolio>
{
    public void Configure(EntityTypeBuilder<Portfolio> builder)
    {
        builder.ToTable("Portfolios");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.PublicId).HasDefaultValueSql("NEWID()");
        builder.HasIndex(x => x.PublicId).IsUnique();
        builder.Property(x => x.Name).HasMaxLength(300).IsRequired();
        builder.Property(x => x.Code).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(50);
        builder.HasIndex(x => new { x.TenantId, x.Code }).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.HasIndex(x => new { x.TenantId, x.Status });
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}
