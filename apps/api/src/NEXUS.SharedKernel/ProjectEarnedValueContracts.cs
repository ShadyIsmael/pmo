namespace NEXUS.SharedKernel;

/// <summary>Versioned planning facts needed to calculate earned value.</summary>
public sealed record ProjectEvmAssignmentInput(
    Guid ActivityKey,
    Guid ResourceId,
    decimal? PlannedHours,
    DateTime? StartDate,
    DateTime? EndDate,
    decimal? WorkingProgress);

public sealed record ProjectEvmPlanSnapshot(
    Guid? BaselineId,
    string? BaselineName,
    int? BaselineVersion,
    DateTime? BaselineDate,
    IReadOnlyList<ProjectEvmAssignmentInput> Assignments);

public sealed record ProjectEvmPlanVersionOption(
    Guid Id,
    int VersionNumber,
    string Name,
    string Type,
    string Status,
    DateTime CreatedAt,
    IReadOnlyList<Guid> ActivityScopeKeys);

public sealed record ProjectEvmPlanOption(
    Guid Id,
    string Name,
    string PlanType,
    string Status,
    Guid? CurrentWorkingVersionId,
    int? CurrentWorkingVersionNumber,
    IReadOnlyList<ProjectEvmPlanVersionOption> Versions);

public sealed record ProjectEvmPlanSelection(
    Guid PlanId,
    Guid WorkingVersionId,
    Guid BaselineVersionId);

public sealed record ProjectEvmMilestoneInput(
    Guid ActivityKey,
    string Name,
    DateTime? BaselineDate,
    DateTime? PlannedDate,
    DateTime? ActualCompletionDate,
    decimal? Progress);

public sealed record ProjectEvmReportingPlanData(
    Guid PlanId,
    string PlanName,
    string PlanType,
    string PlanStatus,
    Guid WorkingVersionId,
    int WorkingVersionNumber,
    Guid BaselineVersionId,
    string BaselineName,
    int BaselineVersionNumber,
    IReadOnlyList<ProjectEvmAssignmentInput> Assignments,
    IReadOnlyList<Guid> ActivityScopeKeys,
    IReadOnlyList<Guid> CurrentActivityIds,
    IReadOnlyList<ProjectEvmMilestoneInput> Milestones);

public interface IProjectEvmPlanLookup
{
    Task<ProjectEvmPlanSnapshot> GetAsync(Guid projectId, Guid tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProjectEvmPlanOption>> GetReportingOptionsAsync(Guid projectId, Guid tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProjectEvmReportingPlanData>> GetReportingPlansAsync(
        Guid projectId,
        Guid tenantId,
        IReadOnlyList<ProjectEvmPlanSelection> selections,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Immutable costing result. Values are only populated when the source data is
/// complete and in the requested project currency; no FX conversion is made.
/// </summary>
public sealed record ProjectEvmLaborCostSummary(
    decimal? BudgetAtCompletion,
    decimal? PlannedValue,
    decimal? EarnedValue,
    decimal? ActualLaborCost,
    string? PlannedValueUnavailableReason,
    string? EarnedValueUnavailableReason,
    string? ActualLaborCostUnavailableReason,
    bool HasActualLaborData);

public interface IProjectEvmLaborCostLookup
{
    Task<ProjectEvmLaborCostSummary> GetAsync(
        Guid tenantId,
        Guid projectId,
        string projectCurrency,
        DateTime statusDate,
        IReadOnlyList<ProjectEvmAssignmentInput> assignments,
        CancellationToken cancellationToken = default,
        IReadOnlyCollection<Guid>? includedPlanActivityIds = null,
        bool includeUnassignedProjectActuals = false);
}

public sealed record ProjectEvmDirectCostSummary(
    decimal Amount,
    bool HasData,
    bool HasCurrencyMismatch);

public interface IProjectEvmDirectCostLookup
{
    Task<ProjectEvmDirectCostSummary> GetAsync(
        Guid tenantId,
        Guid projectId,
        string projectCurrency,
        DateTime statusDate,
        CancellationToken cancellationToken = default);
}
