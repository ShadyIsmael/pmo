using NEXUS.Modules.CostManagement.Application.DTOs;

namespace NEXUS.Api.ProgramManagement;

/// <summary>
/// One project in the current, authorized Program or Portfolio hierarchy.
/// </summary>
public sealed record ProgramPortfolioCashFlowProjectMembershipDto(
    Guid ProjectId,
    Guid ProjectPublicId,
    string ProjectName,
    string? Currency,
    Guid? ProgramId,
    string? ProgramName,
    bool IsDirectProject);

/// <summary>
/// Full hierarchy membership for a Cash Flow request. This is an internal API
/// service contract; TenantId is used to scope the shared project reader.
/// </summary>
public sealed record ProgramPortfolioCashFlowProjectScopeDto(
    Guid TenantId,
    bool PartialCoverage,
    IReadOnlyList<ProgramPortfolioCashFlowProjectMembershipDto> Projects);

/// <summary>
/// Currency-safe Cash Flow totals for a Program or Portfolio. Lifecycle amounts
/// remain separate because they describe overlapping stages of the same money.
/// </summary>
public sealed record ProgramPortfolioCashFlowDto(
    string EntityType,
    Guid EntityId,
    DateOnly? PeriodFrom,
    DateOnly? PeriodTo,
    CashFlowGranularity Granularity,
    CashFlowDataQualityStatus DataQualityStatus,
    bool PartialCoverage,
    int IncludedProjectCount,
    int ProjectsWithUnavailableData,
    bool HasMixedCurrencies,
    IReadOnlyList<ProgramPortfolioCashFlowCurrencyGroupDto> CurrencyGroups,
    IReadOnlyList<ProgramPortfolioCashFlowProjectContributionDto> Projects,
    IReadOnlyList<ProgramPortfolioCashFlowQualityIssueDto> DataQualityIssues);

public sealed record ProgramPortfolioCashFlowCurrencyGroupDto(
    string? Currency,
    int ContributingProjectCount,
    CashFlowDataQualityStatus DataQualityStatus,
    IReadOnlyList<ProgramPortfolioCashFlowBucketDto> Buckets,
    ProgramPortfolioCashFlowUndatedDto Undated,
    IReadOnlyList<string> QualityCodes);

public sealed record ProgramPortfolioCashFlowBucketDto(
    DateOnly PeriodStart,
    decimal? PlannedResidual,
    decimal? CommittedUnpaid,
    decimal? ActualGross,
    decimal? ExecutedAdjustments,
    decimal? ActualNet,
    CashFlowDataQualityStatus DataQualityStatus,
    IReadOnlyList<string> QualityCodes,
    IReadOnlyList<ProgramPortfolioCashFlowItemDto> Items);

public sealed record ProgramPortfolioCashFlowUndatedDto(
    decimal? PlannedResidual,
    decimal? CommittedUnpaid,
    decimal? ActualGross,
    decimal? ExecutedAdjustments,
    decimal? ActualNet,
    CashFlowDataQualityStatus DataQualityStatus,
    IReadOnlyList<string> QualityCodes,
    IReadOnlyList<ProgramPortfolioCashFlowItemDto> Items);

public sealed record ProgramPortfolioCashFlowItemDto(
    Guid ProjectId,
    Guid ProjectPublicId,
    string ProjectName,
    Guid? ProgramId,
    string? ProgramName,
    bool IsDirectProject,
    CashFlowLineDto Line);

public sealed record ProgramPortfolioCashFlowProjectContributionDto(
    Guid ProjectId,
    Guid ProjectPublicId,
    string ProjectName,
    string? Currency,
    Guid? ProgramId,
    string? ProgramName,
    bool IsDirectProject,
    ProjectCashFlowDto CashFlow);

public sealed record ProgramPortfolioCashFlowQualityIssueDto(
    Guid ProjectId,
    string ProjectName,
    CashFlowQualityIssueDto Issue);
