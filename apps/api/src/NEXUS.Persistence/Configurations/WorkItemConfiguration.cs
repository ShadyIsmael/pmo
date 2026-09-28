using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXUS.Modules.WorkItems.Domain;

namespace NEXUS.Persistence.Configurations;

public class WorkItemConfiguration : IEntityTypeConfiguration<WorkItem>
{
    public void Configure(EntityTypeBuilder<WorkItem> builder)
    {
        builder.ToTable("WorkItems");
        builder.HasKey(w => w.Id);
        builder.Property(w => w.Title).HasMaxLength(500).IsRequired();
        builder.Property(w => w.Type).HasMaxLength(50).IsRequired();
        builder.Property(w => w.Priority).HasMaxLength(50).IsRequired();
        builder.Property(w => w.Status).HasMaxLength(50).IsRequired();
        builder.Property(w => w.Source).HasMaxLength(50);
        builder.Property(w => w.Severity).HasMaxLength(50);
        builder.Property(w => w.Resolution).HasMaxLength(4000);
        builder.Property(w => w.Tags).HasMaxLength(500);
        builder.Property(w => w.Labels).HasMaxLength(500);
        builder.HasIndex(w => w.ProjectId);
        builder.HasIndex(w => w.AssigneeId);
        builder.HasIndex(w => new { w.Status, w.ProjectId });
        builder.Property(w => w.RowVersion).IsRowVersion();
    }
}
