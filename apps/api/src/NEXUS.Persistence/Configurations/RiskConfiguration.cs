using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXUS.Modules.RiskManagement.Domain;

namespace NEXUS.Persistence.Configurations;

public sealed class RiskConfiguration : IEntityTypeConfiguration<Risk>
{
    public void Configure(EntityTypeBuilder<Risk> builder)
    {
        builder.ToTable("Risks");
        builder.HasKey(risk => risk.Id);
        builder.Property(risk => risk.Title).HasMaxLength(300).IsRequired();
        builder.Property(risk => risk.Description).HasMaxLength(4000).IsRequired();
        builder.Property(risk => risk.RiskLevel).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(risk => risk.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(risk => risk.RowVersion).IsRowVersion();
        builder.HasIndex(risk => new { risk.TenantId, risk.ProjectId });
        builder.HasIndex(risk => new { risk.TenantId, risk.Status });
        builder.HasIndex(risk => new { risk.TenantId, risk.OwnerResourceId });
    }
}
