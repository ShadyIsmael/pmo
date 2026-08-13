using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXUS.Modules.Portfolio.Domain;

namespace NEXUS.Persistence.Configurations;

public sealed class PortfolioManagerAssignmentConfiguration : IEntityTypeConfiguration<PortfolioManagerAssignment>
{
    public void Configure(EntityTypeBuilder<PortfolioManagerAssignment> builder)
    {
        builder.ToTable("PortfolioManagerAssignments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.HandoverReason).HasMaxLength(500);
        builder.Property(x => x.HandoverNotes).HasMaxLength(2000);
        builder.HasIndex(x => x.TenantId);
        builder.HasIndex(x => x.PortfolioId);
        builder.HasIndex(x => x.PortfolioManagerId);
        builder.HasIndex(x => x.PortfolioId).IsUnique().HasFilter("[IsCurrent] = 1 AND [IsDeleted] = 0");
        builder.HasOne(x => x.Portfolio).WithMany().HasForeignKey(x => x.PortfolioId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.RowVersion).IsRowVersion();
    }
}
