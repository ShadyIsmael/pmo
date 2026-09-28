using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NEXUS.Modules.ProjectPlanning.Domain;

namespace NEXUS.Persistence.Configurations;

public sealed class PlanningTablePreferenceConfiguration : IEntityTypeConfiguration<PlanningTablePreference>
{
    public void Configure(EntityTypeBuilder<PlanningTablePreference> builder)
    {
        builder.ToTable("PlanningTablePreferences");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.VisibleColumnsJson).IsRequired();
        builder.Property(item => item.ColumnOrderJson).IsRequired();
        builder.Property(item => item.ColumnWidthsJson).IsRequired();
        builder.Property(item => item.ColumnFiltersJson).IsRequired();
        builder.Property(item => item.CollapsedRowIdsJson).IsRequired();
        builder.HasIndex(item => new { item.TenantId, item.UserId, item.PlanId }).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.Property(item => item.RowVersion).IsRowVersion().IsConcurrencyToken();
    }
}
