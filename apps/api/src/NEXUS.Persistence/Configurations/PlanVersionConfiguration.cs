using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXUS.Modules.ProjectPlanning.Domain;

namespace NEXUS.Persistence.Configurations;

public class PlanVersionConfiguration : IEntityTypeConfiguration<PlanVersion>
{
    public void Configure(EntityTypeBuilder<PlanVersion> builder)
    {
        builder.ToTable("PlanVersions");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.PlanId)
            .IsRequired();

        builder.Property(e => e.VersionNumber)
            .IsRequired();

        builder.Property(e => e.Type)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(e => e.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(e => e.Name)
            .HasMaxLength(250);

        builder.Property(e => e.SourceVersionId)
            .IsRequired(false);

        builder.Property(e => e.SourceBaselineId)
            .IsRequired(false);

        builder.Property(e => e.RowVersion)
            .IsRowVersion()
            .IsConcurrencyToken();

        // Working and Baseline version sequences may each start at v1; numbers
        // are therefore unique within a version type for the tenant/plan.
        builder.HasIndex(e => new { e.TenantId, e.PlanId, e.VersionNumber, e.Type })
            .IsUnique()
            .HasDatabaseName("IX_PlanVersions_Tenant_Plan_Version");

        // At most one active (editable) Working version per tenant/plan.
        // Working = Type 0, Active = Status 0.
        builder.HasIndex(e => new { e.TenantId, e.PlanId, e.Type })
            .IsUnique()
            .HasDatabaseName("IX_PlanVersions_Tenant_Plan_ActiveWorking")
            .HasFilter("\"Type\" = 0 AND \"Status\" = 0 AND \"IsDeleted\" = 0");

        // Approved Baselines are immutable history. The latest approved row is
        // the comparison baseline; prior approved snapshots remain queryable.
        builder.HasIndex(e => new { e.TenantId, e.PlanId, e.Status })
            .IsUnique(false)
            .HasDatabaseName("IX_PlanVersions_Tenant_Plan_ApprovedBaseline")
            .HasFilter("\"Type\" = 1 AND \"Status\" = 3 AND \"IsDeleted\" = 0");

        builder.HasOne(e => e.Plan)
            .WithMany(p => p.Versions)
            .HasForeignKey(e => e.PlanId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
