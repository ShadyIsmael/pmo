using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXUS.Modules.ProjectManagement.Domain;

namespace NEXUS.Persistence.Configurations;

public class ProjectSnapshotConfiguration : IEntityTypeConfiguration<ProjectSnapshot>
{
    public void Configure(EntityTypeBuilder<ProjectSnapshot> builder)
    {
        builder.ToTable("ProjectSnapshots");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.SnapshotName).HasMaxLength(200);
        builder.Property(s => s.SnapshotType).HasMaxLength(50);
        builder.Property(s => s.Name).HasMaxLength(200);
        builder.Property(s => s.Code).HasMaxLength(50);
        builder.Property(s => s.Currency).HasMaxLength(3);
        builder.Property(s => s.HandoverReason).HasMaxLength(500);
        builder.Property(s => s.HandoverNotes).HasMaxLength(2000);
        builder.Property(s => s.Data);
        builder.HasIndex(s => s.TenantId);
        builder.HasIndex(s => s.ProjectId);
        builder.HasIndex(s => s.SnapshotNumber);
        builder.Property(s => s.RowVersion).IsRowVersion();
    }
}
