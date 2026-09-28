using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXUS.Modules.ProjectManagement.Domain;

namespace NEXUS.Persistence.Configurations;

public sealed class TimeEntryConfiguration : IEntityTypeConfiguration<TimeEntry>
{
    public void Configure(EntityTypeBuilder<TimeEntry> builder)
    {
        builder.ToTable("TimeEntries");
        builder.HasKey(entry => entry.Id);
        builder.Property(entry => entry.EntrySource).HasMaxLength(20).IsRequired();
        builder.Property(entry => entry.Description).HasMaxLength(2000);
        builder.Property(entry => entry.StartTime).HasColumnType("time");
        builder.Property(entry => entry.EndTime).HasColumnType("time");
        builder.Property(entry => entry.ElapsedSeconds).IsRequired();
        builder.Property(entry => entry.ActualSeconds).IsRequired();
        builder.Property(entry => entry.BreakSeconds).IsRequired();
        builder.Property(entry => entry.PauseSeconds).IsRequired();
        builder.Property(entry => entry.RowVersion).IsRowVersion().IsConcurrencyToken();
        builder.HasIndex(entry => new { entry.TenantId, entry.TimerSessionId })
            .HasDatabaseName("IX_TimeEntries_Tenant_TimerSession");
        builder.HasOne(entry => entry.TimerSession)
            .WithMany()
            .HasForeignKey(entry => entry.TimerSessionId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(entry => new { entry.TenantId, entry.ResourceId, entry.WorkDate })
            .HasDatabaseName("IX_TimeEntries_Tenant_Resource_WorkDate");
        builder.HasIndex(entry => new { entry.TenantId, entry.ProjectId, entry.WorkDate })
            .HasDatabaseName("IX_TimeEntries_Tenant_Project_WorkDate");
        builder.HasIndex(entry => new { entry.TenantId, entry.PlanActivityId, entry.WorkDate })
            .HasDatabaseName("IX_TimeEntries_Tenant_Activity_WorkDate");
        builder.HasIndex(entry => new { entry.TenantId, entry.WorkItemId, entry.WorkDate })
            .HasDatabaseName("IX_TimeEntries_Tenant_WorkItem_WorkDate");
        builder.HasIndex(entry => new { entry.TenantId, entry.ProjectRoleId, entry.WorkDate })
            .HasDatabaseName("IX_TimeEntries_Tenant_Role_WorkDate");
    }
}
