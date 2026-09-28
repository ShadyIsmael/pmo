using NEXUS.Modules.ProjectPlanning.Domain;

namespace NEXUS.Modules.ProjectPlanning.Application.DTOs;

public sealed class PlanningTablePreferenceDto
{
    public Guid PlanId { get; set; }
    public IReadOnlyList<string> VisibleColumns { get; set; } = [];
    public IReadOnlyList<string> ColumnOrder { get; set; } = [];
    public IReadOnlyDictionary<string, int> ColumnWidths { get; set; } = new Dictionary<string, int>();
    public IReadOnlyDictionary<string, PlanningColumnFilterDto> ColumnFilters { get; set; } = new Dictionary<string, PlanningColumnFilterDto>();
    public IReadOnlyList<string> CollapsedRowIds { get; set; } = [];
}

public sealed class PlanningColumnFilterDto
{
    public string? Text { get; set; }
    public string? Min { get; set; }
    public string? Max { get; set; }
    public string? Value { get; set; }
}

public sealed class SavePlanningTablePreferenceDto
{
    public IReadOnlyList<string> VisibleColumns { get; set; } = [];
    public IReadOnlyList<string> ColumnOrder { get; set; } = [];
    public IReadOnlyDictionary<string, int> ColumnWidths { get; set; } = new Dictionary<string, int>();
    public IReadOnlyDictionary<string, PlanningColumnFilterDto> ColumnFilters { get; set; } = new Dictionary<string, PlanningColumnFilterDto>();
    public IReadOnlyList<string> CollapsedRowIds { get; set; } = [];
}
