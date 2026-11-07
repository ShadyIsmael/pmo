using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXUS.Modules.Organization.Domain;

namespace NEXUS.Persistence.Configurations;

public sealed class WorkingCalendarDayRuleConfiguration : IEntityTypeConfiguration<WorkingCalendarDayRule>
{
    public void Configure(EntityTypeBuilder<WorkingCalendarDayRule> builder)
    {
        builder.ToTable("WorkingCalendarDayRules");
        builder.HasKey(rule => rule.Id);
        builder.Property(rule => rule.DayOfWeek).HasConversion<int>().IsRequired();
        builder.Property(rule => rule.StartTime).HasColumnType("time");
        builder.Property(rule => rule.EndTime).HasColumnType("time");
        builder.Property(rule => rule.BreakStartTime).HasColumnType("time");
        builder.Property(rule => rule.BreakEndTime).HasColumnType("time");
        builder.Property(rule => rule.RowVersion).IsRowVersion().IsConcurrencyToken();

        builder.HasOne(rule => rule.WorkingCalendar)
            .WithMany(calendar => calendar.DayRules)
            .HasForeignKey(rule => rule.WorkingCalendarId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(rule => new { rule.TenantId, rule.WorkingCalendarId, rule.DayOfWeek })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0")
            .HasDatabaseName("IX_WorkingCalendarDayRules_Tenant_Calendar_Day");
    }
}
