using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXUS.Modules.ProjectPlanning.Domain;

namespace NEXUS.Persistence.Configurations;

public class PlanAssignmentConfiguration : IEntityTypeConfiguration<PlanAssignment>
{
    public void Configure(EntityTypeBuilder<PlanAssignment> builder)
    {
        builder.ToTable("PlanAssignments");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.PlanVersionId)
            .IsRequired();

        builder.Property(e => e.ActivityId)
            .IsRequired();

        // References Resource Management by id; no FK is enforced here because the
        // module only talks to Resource Management by id (interface), never by DbContext.
        builder.Property(e => e.ResourceId)
            .IsRequired();

        builder.Property(e => e.AllocationPct);
        builder.Property(e => e.HoursPerWeek);
        builder.Property(e => e.StartDate);
        builder.Property(e => e.EndDate);

        builder.Property(e => e.RowVersion)
            .IsRowVersion()
            .IsConcurrencyToken();

        builder.HasIndex(e => new { e.TenantId, e.PlanVersionId, e.ActivityId, e.ResourceId })
            .IsUnique()
            .HasDatabaseName("IX_PlanAssignments_Tenant_Version_Activity_Resource");

        builder.HasOne(e => e.PlanVersion)
            .WithMany(v => v.Assignments)
            .HasForeignKey(e => e.PlanVersionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}