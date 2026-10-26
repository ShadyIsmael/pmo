using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.Modules.ProjectPlanning.Domain;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.ProjectManagement.Application;

/// <summary>
/// Cross-module read adapter for ResourceManagement analytics. TimeEntry
/// remains the only actual-work source; this adapter only returns immutable
/// query facts.
/// </summary>
public sealed class ActualWorkCostLookup : IActualWorkCostLookup, IPlannedWorkCostLookup
{
    private readonly DbContext _context;

    public ActualWorkCostLookup(DbContext context) => _context = context;

    public async Task<IReadOnlyList<ActualWorkCostSlice>> GetAsync(
        Guid tenantId,
        DateTime from,
        DateTime to,
        IReadOnlyCollection<Guid>? resourceIds = null,
        IReadOnlyCollection<Guid>? projectIds = null,
        Guid? projectRoleId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Set<TimeEntry>()
            .AsNoTracking()
            .Where(entry => entry.TenantId == tenantId
                && !entry.IsDeleted
                && entry.WorkDate >= from.Date
                && entry.WorkDate <= to.Date
                && entry.ActualSeconds >= 0);

        if (resourceIds is { Count: > 0 }) query = query.Where(entry => resourceIds.Contains(entry.ResourceId));
        if (projectIds is { Count: > 0 }) query = query.Where(entry => projectIds.Contains(entry.ProjectId));
        if (projectRoleId.HasValue) query = query.Where(entry => entry.ProjectRoleId == projectRoleId.Value);

        var slices = await query
            .OrderBy(entry => entry.WorkDate)
            .ThenBy(entry => entry.Id)
            .Select(entry => new ActualWorkCostSlice(
                entry.Id,
                entry.TenantId,
                entry.ResourceId,
                entry.ProjectId,
                entry.WorkDate,
                entry.StartTime,
                entry.EndTime,
                entry.ActualSeconds,
                entry.PlanActivityId,
                entry.WorkItemId,
                entry.ProjectRoleId,
                entry.EntrySource))
            .ToListAsync(cancellationToken);

        return slices.OrderBy(entry => entry.WorkDate)
            .ThenBy(entry => entry.StartTime)
            .ThenBy(entry => entry.TimeEntryId)
            .ToList();
    }

    public async Task<IReadOnlyList<PlannedWorkCostSlice>> GetPlannedAsync(
        Guid tenantId,
        DateTime from,
        DateTime to,
        IReadOnlyCollection<Guid>? resourceIds = null,
        IReadOnlyCollection<Guid>? projectIds = null,
        Guid? projectRoleId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Set<PlanAssignment>()
            .AsNoTracking()
            .Where(assignment => assignment.TenantId == tenantId
                && !assignment.IsDeleted
                && assignment.PlannedHours.HasValue
                && assignment.PlannedHours.Value >= 0
                && !assignment.ResourceAllocationId.HasValue
                && assignment.PlanVersion != null
                && assignment.PlanVersion.Plan != null
                && (!assignment.StartDate.HasValue || assignment.StartDate.Value.Date <= to.Date)
                && (!assignment.EndDate.HasValue || assignment.EndDate.Value.Date >= from.Date));

        if (resourceIds is { Count: > 0 }) query = query.Where(assignment => resourceIds.Contains(assignment.ResourceId));
        if (projectIds is { Count: > 0 }) query = query.Where(assignment => projectIds.Contains(assignment.PlanVersion!.Plan!.ProjectId));
        if (projectRoleId.HasValue) query = query.Where(assignment => assignment.ProjectRoleId == projectRoleId.Value);

        return await query
            .OrderBy(assignment => assignment.StartDate)
            .ThenBy(assignment => assignment.Id)
            .Select(assignment => new PlannedWorkCostSlice(
                assignment.Id,
                assignment.TenantId,
                assignment.ResourceId,
                assignment.PlanVersion!.Plan!.ProjectId,
                assignment.ActivityId,
                assignment.ProjectRoleId,
                assignment.StartDate,
                assignment.EndDate,
                assignment.PlannedHours!.Value))
            .ToListAsync(cancellationToken);
    }
}
