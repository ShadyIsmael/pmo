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

        // Version number is unique per tenant/plan.
        builder.HasIndex(e => new { e.TenantId, e.PlanId, e.VersionNumber })
            .IsUnique()
            .HasDatabaseName("IX_PlanVersions_Tenant_Plan_Version");

        // At most one active (editable) Working version per tenant/plan.
        // Working = Type 0, Active = Status 0.
        builder.HasIndex(e => new { e.TenantId, e.PlanId, e.Type })
            .IsUnique()
            .HasDatabaseName("IX_PlanVersions_Tenant_Plan_ActiveWorking")
            .HasFilter("\"Type\" = 0 AND \"Status\" = 0");

        // At most one current approved Baseline per project.
        // Baseline = Type 1, Approved = Status 3.
        builder.HasIndex(e => new { e.TenantId, e.PlanId, e.Status })
            .IsUnique()
            .HasDatabaseName("IX_PlanVersions_Tenant_Plan_ApprovedBaseline")
            .HasFilter("\"Type\" = 1 AND \"Status\" = 3");

        builder.HasOne(e => e.Plan)
            .WithMany(p => p.Versions)
            .HasForeignKey(e => e.PlanId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}