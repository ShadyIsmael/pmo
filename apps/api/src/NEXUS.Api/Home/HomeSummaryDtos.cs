namespace NEXUS.Api.Home;

public sealed record HomeSummaryDto(
    int ActiveProjectsCount,
    int ProjectsAtRiskCount,
    int OpenRisksCount,
    int CriticalOpenRisksCount,
    IReadOnlyDictionary<string, int> ProjectHealthCounts,
    IReadOnlyList<HomeBudgetSummaryDto> BudgetByCurrency,
    IReadOnlyList<HomeProjectSummaryDto> AttentionProjects,
    IReadOnlyList<HomeRiskSummaryDto> TopRisks);

public sealed record HomeBudgetSummaryDto(
    string Currency,
    decimal Budget,
    decimal ActualCost);

public sealed record HomeProjectSummaryDto(
    Guid Id,
    string Name,
    decimal Progress,
    string Health,
    string RagStatus);

public sealed record HomeRiskSummaryDto(
    Guid Id,
    string Title,
    string ProjectName,
    string RiskLevel,
    string Status);
