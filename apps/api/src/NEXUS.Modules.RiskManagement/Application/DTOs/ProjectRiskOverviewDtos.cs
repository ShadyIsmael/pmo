using NEXUS.Modules.RiskManagement.Domain;

namespace NEXUS.Modules.RiskManagement.Application.DTOs;

public sealed class ProjectRiskOverviewDto
{
    /// <summary>Risks not formally closed. Mitigated risks remain included until their status becomes Closed.</summary>
    public int OpenCount { get; init; }
    public int HighCriticalOpenCount { get; init; }
    public IReadOnlyList<ProjectRiskStatusCountDto> StatusCounts { get; init; } = [];
    public IReadOnlyList<ProjectRiskLevelCountDto> LevelCounts { get; init; } = [];
    public IReadOnlyList<ProjectRiskOverviewItemDto> TopRisks { get; init; } = [];
}

public sealed class ProjectRiskStatusCountDto
{
    public string Status { get; init; } = string.Empty;
    public int Count { get; init; }
}

public sealed class ProjectRiskLevelCountDto
{
    public string RiskLevel { get; init; } = string.Empty;
    public int Count { get; init; }
    public int DisplayOrder { get; init; }
}

public sealed class ProjectRiskOverviewItemDto
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public RiskLevel RiskLevel { get; init; }
    public RiskStatus Status { get; init; }
}
