using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXUS.Modules.ProjectManagement.Domain;

namespace NEXUS.Persistence.Configurations;

public sealed class TimerSegmentConfiguration : IEntityTypeConfiguration<TimerSegment>
{
    public void Configure(EntityTypeBuilder<TimerSegment> builder)
    {
        builder.ToTable("TimerSegments");
        builder.HasKey(segment => segment.Id);
        builder.Property(segment => segment.SegmentType).HasMaxLength(16).IsRequired();
        builder.Property(segment => segment.StartedAt).HasColumnType("datetimeoffset").IsRequired();
        builder.Property(segment => segment.EndedAt).HasColumnType("datetimeoffset");
        builder.Property(segment => segment.RowVersion).IsRowVersion().IsConcurrencyToken();
        builder.HasIndex(segment => new { segment.TenantId, segment.TimerSessionId, segment.StartedAt })
            .HasDatabaseName("IX_TimerSegments_Tenant_Session_StartedAt");
        builder.HasOne(segment => segment.TimerSession)
            .WithMany(session => session.Segments)
            .HasForeignKey(segment => segment.TimerSessionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
