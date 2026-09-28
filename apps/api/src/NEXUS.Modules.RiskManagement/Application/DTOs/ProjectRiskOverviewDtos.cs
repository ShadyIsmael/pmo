using NEXUS.Modules.RiskManagement.Domain;

namespace NEXUS.Modules.RiskManagement.Application.DTOs;

public sealed class ProjectRiskOverviewDto
{
    public int OpenCount { get; init; }
    public int HighCriticalOpenCount { get; init; }
    public IReadOnlyList<ProjectRiskOverviewItemDto> TopRisks { get; init; } = [];
}

public sealed class ProjectRiskOverviewItemDto
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public RiskLevel RiskLevel { get; init; }
    public RiskStatus Status { get; init; }
}
