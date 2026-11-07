using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXUS.Modules.Audit.Domain;

namespace NEXUS.Persistence.Configurations;

/// <summary>
/// Audit entries are an append-only, tenant-scoped log. The configuration keeps
/// indexed key columns short enough for SQL Server indexes and adds the access
/// paths used by the project audit trail.
/// </summary>
public class AuditEntryConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        builder.ToTable("AuditEntries");
        builder.HasKey(entry => entry.Id);

        builder.Property(entry => entry.EntityType).HasMaxLength(128).IsRequired();
        builder.Property(entry => entry.Action).HasMaxLength(96).IsRequired();
        builder.Property(entry => entry.Source).HasMaxLength(32);
        builder.Property(entry => entry.CorrelationId).HasMaxLength(128);
        builder.Property(entry => entry.Description).HasMaxLength(500);
        builder.Property(entry => entry.ChangedFields).HasMaxLength(1000);
        builder.Property(entry => entry.UserName).HasMaxLength(256);
        builder.Property(entry => entry.IpAddress).HasMaxLength(64);
        builder.Property(entry => entry.UserAgent).HasMaxLength(512);

        // Project trail listing and deterministic newest-first paging.
        builder.HasIndex(entry => new { entry.TenantId, entry.ProjectId, entry.Timestamp });
        builder.HasIndex(entry => new { entry.TenantId, entry.Timestamp });

        // Entity drill-down and filter facets.
        builder.HasIndex(entry => new { entry.TenantId, entry.EntityType, entry.EntityId });
        builder.HasIndex(entry => new { entry.TenantId, entry.Action });
        builder.HasIndex(entry => new { entry.TenantId, entry.UserId });
    }
}
