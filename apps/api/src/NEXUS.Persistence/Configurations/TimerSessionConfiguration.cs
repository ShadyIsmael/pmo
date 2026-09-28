using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXUS.Modules.ProjectManagement.Domain;

namespace NEXUS.Persistence.Configurations;

public sealed class TimerSessionConfiguration : IEntityTypeConfiguration<TimerSession>
{
    public void Configure(EntityTypeBuilder<TimerSession> builder)
    {
        builder.ToTable("TimerSessions");
        builder.HasKey(session => session.Id);
        builder.Property(session => session.Status).HasMaxLength(32).IsRequired();
        builder.Property(session => session.StartedAt).HasColumnType("datetimeoffset").IsRequired();
        builder.Property(session => session.EndedAt).HasColumnType("datetimeoffset");
        builder.Property(session => session.LastHeartbeatAt).HasColumnType("datetimeoffset");
        builder.Property(session => session.LastActivityAt).HasColumnType("datetimeoffset");
        builder.Property(session => session.RowVersion).IsRowVersion().IsConcurrencyToken();
        builder.HasIndex(session => new { session.TenantId, session.ResourceId })
            .HasDatabaseName("UX_TimerSessions_Active_Resource")
            .IsUnique()
            .HasFilter("[IsDeleted] = 0 AND [Status] IN ('Running', 'OnBreak', 'Paused', 'IdlePaused')");
        builder.HasIndex(session => new { session.TenantId, session.UserId })
            .HasDatabaseName("UX_TimerSessions_Active_User")
            .IsUnique()
            .HasFilter("[IsDeleted] = 0 AND [Status] IN ('Running', 'OnBreak', 'Paused', 'IdlePaused')");
        builder.HasIndex(session => new { session.TenantId, session.UserId, session.Status })
            .HasDatabaseName("IX_TimerSessions_Tenant_User_Status");
    }
}
