using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.ProjectPlanning.Domain;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.ProjectPlanning.Application;

/// <summary>
/// Reads EVM inputs from the same newest plan selected by the planning
/// workspace, its latest approved immutable baseline, and its active Working
/// version. Baseline assignments are matched to current activities by stable
/// source lineage, with unique activity-code matching for legacy rows only.
/// </summary>
public sealed class ProjectEvmPlanLookup : IProjectEvmPlanLookup
{
    private readonly DbContext _context;

    public ProjectEvmPlanLookup(DbContext context) => _context = context;

    public async Task<ProjectEvmPlanSnapshot> GetAsync(Guid projectId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var plan = await _context.Set<Plan>().AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.ProjectId == projectId && !item.IsDeleted)
            .OrderByDescending(item => item.CreatedAt)
            .ThenByDescending(item => item.Id)
            .Select(item => new { item.Id })
            .FirstOrDefaultAsync(cancellationToken);
        if (plan is null) return new ProjectEvmPlanSnapshot(null, null, null, null, []);

        var versions = await _context.Set<PlanVersion>().AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.PlanId == plan.Id && !item.IsDeleted)
            .ToListAsync(cancellationToken);
        var baseline = versions.Where(item => item.Type == PlanVersionType.Baseline && item.Status == PlanVersionStatus.Approved)
            .OrderByDescending(item => item.VersionNumber).ThenByDescending(item => item.CreatedAt).FirstOrDefault();
        var working = versions.Where(item => item.Type == PlanVersionType.Working && item.Status == PlanVersionStatus.Active)
            .OrderByDescending(item => item.VersionNumber).ThenByDescending(item => item.CreatedAt).FirstOrDefault();
        if (baseline is null) return new ProjectEvmPlanSnapshot(null, null, null, null, []);

        var baselineActivities = await _context.Set<PlanActivity>().AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.PlanVersionId == baseline.Id && !item.IsDeleted)
            .ToListAsync(cancellationToken);
        List<PlanActivity> workingActivities = working is null
            ? new List<PlanActivity>()
            : await _context.Set<PlanActivity>().AsNoTracking()
                .Where(item => item.TenantId == tenantId && item.PlanVersionId == working.Id && !item.IsDeleted)
                .ToListAsync(cancellationToken);

        var workingByLineage = workingActivities
            .GroupBy(item => item.SourceActivityId ?? item.Id)
            .Where(group => group.Count() == 1)
            .ToDictionary(group => group.Key, group => group.Single());
        var workingByCode = workingActivities
            .Where(item => !item.SourceActivityId.HasValue && !string.IsNullOrWhiteSpace(item.Code))
            .GroupBy(item => item.Code, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() == 1)
            .ToDictionary(group => group.Key, group => group.Single(), StringComparer.OrdinalIgnoreCase);
        var baselineById = baselineActivities.ToDictionary(item => item.Id);

        var assignments = await _context.Set<PlanAssignment>().AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.PlanVersionId == baseline.Id && !item.IsDeleted)
            .ToListAsync(cancellationToken);

        PlanActivity? FindCurrentActivity(PlanActivity? activity)
        {
            if (activity is null) return null;
            var key = activity.SourceActivityId ?? activity.Id;
            PlanActivity? current = null;
            if (key != Guid.Empty) workingByLineage.TryGetValue(key, out current);
            if (current is null && !activity.SourceActivityId.HasValue)
                workingByCode.TryGetValue(activity.Code, out current);
            return current;
        }

        var inputs = assignments.Select(assignment =>
        {
            baselineById.TryGetValue(assignment.ActivityId, out var activity);
            var key = activity is null ? Guid.Empty : activity.SourceActivityId ?? activity.Id;
            var current = FindCurrentActivity(activity);

            return new ProjectEvmAssignmentInput(
                key,
                assignment.ResourceId,
                assignment.PlannedHours,
                assignment.StartDate ?? activity?.StartDate,
                assignment.EndDate ?? activity?.EndDate,
                current?.Progress);
        }).ToList();

        var assignedActivityIds = assignments.Select(item => item.ActivityId).ToHashSet();
        inputs.AddRange(baselineActivities
            .Where(activity => activity.Type == PlanActivityType.Task
                && activity.EstimateHours.GetValueOrDefault() > 0m
                && !assignedActivityIds.Contains(activity.Id))
            .Select(activity => new ProjectEvmAssignmentInput(
                activity.SourceActivityId ?? activity.Id,
                Guid.Empty,
                activity.EstimateHours,
                activity.StartDate,
                activity.EndDate,
                FindCurrentActivity(activity)?.Progress)));

        return new ProjectEvmPlanSnapshot(baseline.Id, baseline.Name, baseline.VersionNumber, baseline.CreatedAt, inputs);
    }
}
