namespace NEXUS.Modules.ProjectPlanning.Application.DTOs;

public sealed class ComparisonFieldChangeDto
{
    public object? Before { get; init; }
    public object? After { get; init; }
}

public sealed class VersionEntityDifferenceDto
{
    public string Key { get; init; } = string.Empty;
    public string ChangeType { get; init; } = string.Empty;
    public IReadOnlyDictionary<string, object?>? Before { get; init; }
    public IReadOnlyDictionary<string, object?>? After { get; init; }
    public IReadOnlyDictionary<string, ComparisonFieldChangeDto> Fields { get; init; } =
        new Dictionary<string, ComparisonFieldChangeDto>();
}

public sealed class VersionComparisonSummaryDto
{
    public int Total { get; init; }
    public int Added { get; init; }
    public int Removed { get; init; }
    public int Modified { get; init; }
    public int Unchanged { get; init; }
}

public sealed class PlanVersionComparisonDto
{
    public Guid PlanId { get; init; }
    public Guid FromVersionId { get; init; }
    public int FromVersionNumber { get; init; }
    public string FromVersionType { get; init; } = string.Empty;
    public string FromVersionStatus { get; init; } = string.Empty;
    public Guid ToVersionId { get; init; }
    public int ToVersionNumber { get; init; }
    public string ToVersionType { get; init; } = string.Empty;
    public string ToVersionStatus { get; init; } = string.Empty;

    public IReadOnlyList<VersionEntityDifferenceDto> Wbs { get; init; } = Array.Empty<VersionEntityDifferenceDto>();
    public IReadOnlyList<VersionEntityDifferenceDto> Activities { get; init; } = Array.Empty<VersionEntityDifferenceDto>();
    public IReadOnlyList<VersionEntityDifferenceDto> Dependencies { get; init; } = Array.Empty<VersionEntityDifferenceDto>();
    public IReadOnlyList<VersionEntityDifferenceDto> Deliverables { get; init; } = Array.Empty<VersionEntityDifferenceDto>();
    public IReadOnlyList<VersionEntityDifferenceDto> Assignments { get; init; } = Array.Empty<VersionEntityDifferenceDto>();
    public IReadOnlyList<VersionEntityDifferenceDto> Calendars { get; init; } = Array.Empty<VersionEntityDifferenceDto>();
    public IReadOnlyDictionary<string, VersionComparisonSummaryDto> Summary { get; init; } =
        new Dictionary<string, VersionComparisonSummaryDto>();
}
