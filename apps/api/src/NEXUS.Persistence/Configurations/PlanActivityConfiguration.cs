using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXUS.Modules.ProjectPlanning.Domain;

namespace NEXUS.Persistence.Configurations;

public class PlanActivityConfiguration : IEntityTypeConfiguration<PlanActivity>
{
    public void Configure(EntityTypeBuilder<PlanActivity> builder)
    {
        builder.ToTable("PlanActivities");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.PlanVersionId)
            .IsRequired();

        builder.Property(e => e.SourceActivityId)
            .IsRequired(false);

        builder.Property(e => e.Code)
            .HasMaxLength(50);

        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(250);

        builder.Property(e => e.Description)
            .HasMaxLength(2000);

        builder.Property(e => e.Type)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(e => e.SchedulingMode)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(e => e.ConstraintType)
            .HasConversion<int>()
            .IsRequired();
        builder.Property(e => e.ConstraintDate);
        builder.Property(e => e.Deadline);

        builder.Property(e => e.Duration)
            .IsRequired();

        builder.Property(e => e.SortOrder)
            .IsRequired();

        builder.Property(e => e.StartDate);
        builder.Property(e => e.EndDate);
        builder.Property(e => e.ActualStart);
        builder.Property(e => e.ActualFinish);

        builder.Property(e => e.CalendarId)
            .IsRequired(false);

        builder.Property(e => e.EstimateHours);
        builder.Property(e => e.Progress);

        builder.Property(e => e.WbsId)
            .IsRequired(false);

        builder.Property(e => e.RowVersion)
            .IsRowVersion()
            .IsConcurrencyToken();

        builder.OwnsOne(e => e.TextFormat, format =>
        {
            format.Property(f => f.IsSpecified)
                .HasColumnName("TextFormatSpecified")
                .IsRequired();
            format.Property(f => f.IsBold).HasColumnName("TextFormatBold").IsRequired();
            format.Property(f => f.IsItalic).HasColumnName("TextFormatItalic").IsRequired();
            format.Property(f => f.IsUnderline).HasColumnName("TextFormatUnderline").IsRequired();
            format.Property(f => f.TextColorToken).HasColumnName("TextFormatTextColor");
            format.Property(f => f.HighlightColorToken).HasColumnName("TextFormatHighlightColor");
            format.Property(f => f.FontSizeToken).HasColumnName("TextFormatFontSize");
        });
        builder.Navigation(e => e.TextFormat).IsRequired(false);

        builder.HasIndex(e => new { e.TenantId, e.PlanVersionId, e.Code })
            .IsUnique()
            .HasDatabaseName("IX_PlanActivities_Tenant_Version_Code");

        builder.HasIndex(e => new { e.TenantId, e.PlanVersionId, e.SourceActivityId })
            .HasDatabaseName("IX_PlanActivities_Tenant_Version_SourceActivity");

        builder.HasOne(e => e.PlanVersion)
            .WithMany(v => v.Activities)
            .HasForeignKey(e => e.PlanVersionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Calendar)
            .WithMany(c => c.Activities)
            .HasForeignKey(e => e.CalendarId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.CalendarId);
    }
}
