using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXUS.Modules.ProjectPlanning.Domain;

namespace NEXUS.Persistence.Configurations;

public class PlanDependencyConfiguration : IEntityTypeConfiguration<PlanDependency>
{
    public void Configure(EntityTypeBuilder<PlanDependency> builder)
    {
        builder.ToTable("PlanDependencies");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.PlanVersionId)
            .IsRequired();

        builder.Property(e => e.ActivityId)
            .IsRequired();

        builder.Property(e => e.PredecessorActivityId)
            .IsRequired();

        builder.Property(e => e.Type)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(e => e.Lag);

        builder.Property(e => e.RowVersion)
            .IsRowVersion()
            .IsConcurrencyToken();

        builder.HasIndex(e => new { e.TenantId, e.PlanVersionId, e.ActivityId, e.PredecessorActivityId })
            .IsUnique()
            .HasDatabaseName("IX_PlanDependencies_Tenant_Version_Edge");

        builder.HasOne(e => e.PlanVersion)
            .WithMany(v => v.Dependencies)
            .HasForeignKey(e => e.PlanVersionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}