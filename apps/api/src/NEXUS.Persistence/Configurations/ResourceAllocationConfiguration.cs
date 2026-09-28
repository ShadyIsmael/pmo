using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXUS.Modules.ResourceManagement.Domain;

namespace NEXUS.Persistence.Configurations;

public class ResourceAllocationConfiguration : IEntityTypeConfiguration<ResourceAllocation>
{
    public void Configure(EntityTypeBuilder<ResourceAllocation> builder)
    {
        builder.ToTable("ResourceAllocations");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Status).HasMaxLength(50).IsRequired();
        builder.Property(a => a.Notes).HasMaxLength(1000);
        builder.Property(a => a.AllocationPercentage).HasPrecision(7, 4);
        builder.Property(a => a.HoursPerWeek).HasColumnType("decimal(10,2)");
        builder.Property(a => a.PlannedHours).HasColumnType("decimal(10,2)");
        builder.Property(a => a.ProjectName).HasMaxLength(300);
        builder.Property(a => a.IsBillable).HasDefaultValue(true);
        builder.Property(a => a.AllocationSource).HasMaxLength(50).IsRequired().HasDefaultValue("Manual");
        builder.Property(a => a.SourceReferenceId);

        builder.HasOne(a => a.Resource)
            .WithMany(r => r.Allocations)
            .HasForeignKey(a => a.ResourceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(a => a.ResourceId);
        builder.HasIndex(a => a.ProjectId);
        builder.HasIndex(a => a.WorkItemId);
        builder.HasIndex(a => a.Status);
        builder.HasIndex(a => a.TenantId);
        builder.HasIndex(a => new { a.ResourceId, a.StartDate, a.EndDate });
        builder.HasIndex(a => new { a.AllocationSource, a.SourceReferenceId });
        builder.Property(a => a.RowVersion).IsRowVersion();
    }
}
