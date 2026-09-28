using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXUS.Modules.ProjectPlanning.Domain;

namespace NEXUS.Persistence.Configurations;

public class PlanningCalendarConfiguration : IEntityTypeConfiguration<PlanningCalendar>
{
    public void Configure(EntityTypeBuilder<PlanningCalendar> builder)
    {
        builder.ToTable("PlanningCalendars");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Name)
            .HasMaxLength(250)
            .IsRequired();
        builder.Property(e => e.Description).HasMaxLength(2000);

        builder.HasDiscriminator<string>("CalendarKind")
            .HasValue<CalendarTemplate>("Template")
            .HasValue<PlanCalendar>("Plan");
        builder.Property<string>("CalendarKind").HasMaxLength(450);

        builder.Property(e => e.RowVersion)
            .IsRowVersion()
            .IsConcurrencyToken();
    }
}

public class PlanCalendarConfiguration : IEntityTypeConfiguration<PlanCalendar>
{
    public void Configure(EntityTypeBuilder<PlanCalendar> builder)
    {
        builder.Property(e => e.ProjectId).IsRequired();
        builder.Property(e => e.PlanVersionId).IsRequired();
        builder.Property(e => e.TemplateId).IsRequired(false);
        builder.Property(e => e.IsDefault).IsRequired();

        builder.HasOne(e => e.PlanVersion)
            .WithMany(v => v.Calendars)
            .HasForeignKey(e => e.PlanVersionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Template)
            .WithMany(t => t.ProjectOverrides)
            .HasForeignKey(e => e.TemplateId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.TenantId, e.PlanVersionId, e.Name })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0")
            .HasDatabaseName("IX_PlanCalendars_Tenant_Version_Name");

        builder.HasIndex(e => new { e.TenantId, e.PlanVersionId, e.IsDefault })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0 AND [IsDefault] = 1")
            .HasDatabaseName("IX_PlanCalendars_Tenant_Version_Default");
    }
}

public class CalendarDayRuleConfiguration : IEntityTypeConfiguration<CalendarDayRule>
{
    public void Configure(EntityTypeBuilder<CalendarDayRule> builder)
    {
        builder.ToTable("CalendarDayRules");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.PlanningCalendarId).IsRequired();
        builder.Property(e => e.DayOfWeek).HasConversion<int>().IsRequired();
        builder.Property(e => e.StartTime).HasColumnType("time");
        builder.Property(e => e.EndTime).HasColumnType("time");
        builder.Property(e => e.BreakStartTime).HasColumnType("time");
        builder.Property(e => e.BreakEndTime).HasColumnType("time");
        builder.Property(e => e.RowVersion).IsRowVersion().IsConcurrencyToken();

        builder.HasOne(e => e.PlanningCalendar)
            .WithMany(c => c.DayRules)
            .HasForeignKey(e => e.PlanningCalendarId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.TenantId, e.PlanningCalendarId, e.DayOfWeek })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0")
            .HasDatabaseName("IX_CalendarDayRules_Tenant_Calendar_Day");
    }
}

public class CalendarHolidayConfiguration : IEntityTypeConfiguration<CalendarHoliday>
{
    public void Configure(EntityTypeBuilder<CalendarHoliday> builder)
    {
        builder.ToTable("CalendarHolidays");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.PlanningCalendarId).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(250).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(2000);
        builder.Property(e => e.Date).HasColumnType("date").IsRequired();
        builder.Property(e => e.EndDate).HasColumnType("date");
        builder.Property(e => e.RowVersion).IsRowVersion().IsConcurrencyToken();

        builder.HasOne(e => e.PlanningCalendar)
            .WithMany(c => c.Holidays)
            .HasForeignKey(e => e.PlanningCalendarId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.TenantId, e.PlanningCalendarId, e.Date })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0")
            .HasDatabaseName("IX_CalendarHolidays_Tenant_Calendar_Date");
    }
}
