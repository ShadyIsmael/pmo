using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXUS.Modules.Organization.Domain;

namespace NEXUS.Persistence.Configurations;

public sealed class ResourceReportingLineConfiguration : IEntityTypeConfiguration<ResourceReportingLine>
{
    public void Configure(EntityTypeBuilder<ResourceReportingLine> builder)
    {
        builder.ToTable("ResourceReportingLines");
        builder.HasKey(line => line.Id);
        builder.Property(line => line.ReportingType).HasMaxLength(50).IsRequired();
        builder.Property(line => line.RowVersion).IsRowVersion();
        builder.HasIndex(line => new { line.TenantId, line.ResourceId, line.EffectiveFrom });
        builder.HasIndex(line => new { line.TenantId, line.ManagerResourceId, line.IsActive });
        builder.HasIndex(line => new { line.TenantId, line.ResourceId, line.IsPrimary, line.IsActive })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0 AND [IsActive] = 1 AND [IsPrimary] = 1");
    }
}
