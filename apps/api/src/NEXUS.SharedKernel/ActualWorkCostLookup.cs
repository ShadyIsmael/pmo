namespace NEXUS.SharedKernel;

/// <summary>
/// Read-only facts projected from the authoritative TimeEntry store.
/// This contract deliberately does not expose the ProjectManagement entity.
/// </summary>
public sealed record ActualWorkCostSlice(
    Guid TimeEntryId,
    Guid TenantId,
    Guid ResourceId,
    Guid ProjectId,
    DateTime WorkDate,
    TimeSpan StartTime,
    TimeSpan EndTime,
    int ActualSeconds,
    Guid? PlanActivityId,
    Guid? WorkItemId,
    Guid? ProjectRoleId,
    string EntrySource);

public interface IActualWorkCostLookup
{
    Task<IReadOnlyList<ActualWorkCostSlice>> GetAsync(
        Guid tenantId,
        DateTime from,
        DateTime to,
        IReadOnlyCollection<Guid>? resourceIds = null,
        IReadOnlyCollection<Guid>? projectIds = null,
        Guid? projectRoleId = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Read-only planned-work facts. PlanAssignment rows linked to a
/// ResourceAllocation are intentionally excluded so the two planning
/// projections cannot double-count the same assignment.
/// </summary>
public sealed record PlannedWorkCostSlice(
    Guid PlanAssignmentId,
    Guid TenantId,
    Guid ResourceId,
    Guid ProjectId,
    Guid PlanActivityId,
    Guid? ProjectRoleId,
    DateTime? StartDate,
    DateTime? EndDate,
    decimal PlannedHours);

public interface IPlannedWorkCostLookup
{
    Task<IReadOnlyList<PlannedWorkCostSlice>> GetPlannedAsync(
        Guid tenantId,
        DateTime from,
        DateTime to,
        IReadOnlyCollection<Guid>? resourceIds = null,
        IReadOnlyCollection<Guid>? projectIds = null,
        Guid? projectRoleId = null,
        CancellationToken cancellationToken = default);
}
