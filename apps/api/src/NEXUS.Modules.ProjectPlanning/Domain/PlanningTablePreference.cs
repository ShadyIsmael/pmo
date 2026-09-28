using NEXUS.SharedKernel;

namespace NEXUS.Modules.ProjectPlanning.Domain;

/// <summary>Per-user, per-plan Planning table layout. Temporary selection state is intentionally excluded.</summary>
public sealed class PlanningTablePreference : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid PlanId { get; set; }
    public string VisibleColumnsJson { get; set; } = "[]";
    public string ColumnOrderJson { get; set; } = "[]";
    public string ColumnWidthsJson { get; set; } = "{}";
    public string ColumnFiltersJson { get; set; } = "{}";
    public string CollapsedRowIdsJson { get; set; } = "[]";
}
