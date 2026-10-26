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
            var current = FindCurrentActivity(activity);

            return ToAssignmentInput(assignment, activity, current?.Progress);
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

    public async Task<IReadOnlyList<ProjectEvmPlanOption>> GetReportingOptionsAsync(
        Guid projectId,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var plans = await _context.Set<Plan>().AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.ProjectId == projectId && !item.IsDeleted)
            .OrderBy(item => item.Name).ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);
        if (plans.Count == 0) return Array.Empty<ProjectEvmPlanOption>();

        var planIds = plans.Select(item => item.Id).ToList();
        var versions = await _context.Set<PlanVersion>().AsNoTracking()
            .Where(item => item.TenantId == tenantId && planIds.Contains(item.PlanId) && !item.IsDeleted)
            .OrderBy(item => item.VersionNumber).ToListAsync(cancellationToken);
        var approvedBaselineIds = versions.Where(item => item.Type == PlanVersionType.Baseline && item.Status == PlanVersionStatus.Approved)
            .Select(item => item.Id).ToList();
        Dictionary<Guid, IReadOnlyList<Guid>> scopeByVersion;
        if (approvedBaselineIds.Count == 0)
        {
            scopeByVersion = new Dictionary<Guid, IReadOnlyList<Guid>>();
        }
        else
        {
            var baselineActivityRows = await _context.Set<PlanActivity>().AsNoTracking()
                .Where(item => item.TenantId == tenantId && approvedBaselineIds.Contains(item.PlanVersionId) && !item.IsDeleted)
                .Select(item => new { item.PlanVersionId, item.SourceActivityId, item.Id })
                .ToListAsync(cancellationToken);
            scopeByVersion = baselineActivityRows.GroupBy(item => item.PlanVersionId)
                .ToDictionary(group => group.Key, group => (IReadOnlyList<Guid>)group
                    .Select(item => item.SourceActivityId ?? item.Id).Distinct().ToList());
        }
        var versionsByPlan = versions.GroupBy(item => item.PlanId).ToDictionary(group => group.Key, group => group.ToList());

        return plans.Select(plan =>
        {
            var planVersions = versionsByPlan.GetValueOrDefault(plan.Id) ?? [];
            var working = planVersions.Where(item => item.Type == PlanVersionType.Working && item.Status == PlanVersionStatus.Active)
                .OrderByDescending(item => item.VersionNumber).ThenByDescending(item => item.CreatedAt).FirstOrDefault();
            return new ProjectEvmPlanOption(
                plan.Id,
                plan.Name,
                plan.PlanType.ToString(),
                plan.Status.ToString(),
                working?.Id,
                working?.VersionNumber,
                planVersions.Select(version => new ProjectEvmPlanVersionOption(
                    version.Id,
                    version.VersionNumber,
                    version.Name ?? $"{version.Type} v{version.VersionNumber}",
                    version.Type.ToString(),
                    version.Status.ToString(),
                    version.CreatedAt,
                    scopeByVersion.GetValueOrDefault(version.Id) ?? Array.Empty<Guid>()))
                    .OrderByDescending(version => version.VersionNumber).ThenBy(version => version.Type, StringComparer.Ordinal)
                    .ToList());
        }).ToList();
    }

    public async Task<IReadOnlyList<ProjectEvmReportingPlanData>> GetReportingPlansAsync(
        Guid projectId,
        Guid tenantId,
        IReadOnlyList<ProjectEvmPlanSelection> selections,
        CancellationToken cancellationToken = default)
    {
        if (selections.Count == 0) return Array.Empty<ProjectEvmReportingPlanData>();
        var planIds = selections.Select(item => item.PlanId).Distinct().ToList();
        if (planIds.Count != selections.Count) return Array.Empty<ProjectEvmReportingPlanData>();

        var plans = await _context.Set<Plan>().AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.ProjectId == projectId && planIds.Contains(item.Id) && !item.IsDeleted)
            .ToListAsync(cancellationToken);
        if (plans.Count != planIds.Count) return Array.Empty<ProjectEvmReportingPlanData>();

        var requestedVersionIds = selections.SelectMany(item => new[] { item.WorkingVersionId, item.BaselineVersionId }).Distinct().ToList();
        var versions = await _context.Set<PlanVersion>().AsNoTracking()
            .Where(item => item.TenantId == tenantId && requestedVersionIds.Contains(item.Id) && planIds.Contains(item.PlanId) && !item.IsDeleted)
            .ToListAsync(cancellationToken);
        var versionById = versions.ToDictionary(item => item.Id);
        if (selections.Any(selection => !versionById.TryGetValue(selection.WorkingVersionId, out var working)
                || working.PlanId != selection.PlanId || working.Type != PlanVersionType.Working || working.Status != PlanVersionStatus.Active
                || !versionById.TryGetValue(selection.BaselineVersionId, out var baseline)
                || baseline.PlanId != selection.PlanId || baseline.Type != PlanVersionType.Baseline || baseline.Status != PlanVersionStatus.Approved))
            return Array.Empty<ProjectEvmReportingPlanData>();

        var versionIds = selections.SelectMany(item => new[] { item.WorkingVersionId, item.BaselineVersionId }).Distinct().ToList();
        var activities = await _context.Set<PlanActivity>().AsNoTracking()
            .Where(item => item.TenantId == tenantId && versionIds.Contains(item.PlanVersionId) && !item.IsDeleted)
            .ToListAsync(cancellationToken);
        var activitiesByVersion = activities.GroupBy(item => item.PlanVersionId).ToDictionary(group => group.Key, group => group.ToList());
        var assignments = await _context.Set<PlanAssignment>().AsNoTracking()
            .Where(item => item.TenantId == tenantId && selections.Select(selection => selection.BaselineVersionId).Contains(item.PlanVersionId) && !item.IsDeleted)
            .ToListAsync(cancellationToken);
        var assignmentsByVersion = assignments.GroupBy(item => item.PlanVersionId).ToDictionary(group => group.Key, group => group.ToList());
        var planById = plans.ToDictionary(item => item.Id);

        var result = new List<ProjectEvmReportingPlanData>(selections.Count);
        foreach (var selection in selections)
        {
            var plan = planById[selection.PlanId];
            var working = versionById[selection.WorkingVersionId];
            var baseline = versionById[selection.BaselineVersionId];
            var baselineActivities = activitiesByVersion.GetValueOrDefault(baseline.Id) ?? [];
            var workingActivities = activitiesByVersion.GetValueOrDefault(working.Id) ?? [];
            var workingByLineage = workingActivities
                .GroupBy(item => item.SourceActivityId ?? item.Id)
                .Where(group => group.Count() == 1)
                .ToDictionary(group => group.Key, group => group.Single());
            var workingByCode = workingActivities.Where(item => !item.SourceActivityId.HasValue && !string.IsNullOrWhiteSpace(item.Code))
                .GroupBy(item => item.Code, StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() == 1)
                .ToDictionary(group => group.Key, group => group.Single(), StringComparer.OrdinalIgnoreCase);

            PlanActivity? FindCurrent(PlanActivity activity)
            {
                var key = activity.SourceActivityId ?? activity.Id;
                if (workingByLineage.TryGetValue(key, out var current)) return current;
                return !activity.SourceActivityId.HasValue && workingByCode.TryGetValue(activity.Code, out current) ? current : null;
            }

            var baselineById = baselineActivities.ToDictionary(item => item.Id);
            var planAssignments = assignmentsByVersion.GetValueOrDefault(baseline.Id) ?? [];
            var evmAssignments = planAssignments.Select(assignment =>
            {
                baselineById.TryGetValue(assignment.ActivityId, out var activity);
                return ToAssignmentInput(assignment, activity, activity is null ? null : FindCurrent(activity)?.Progress);
            }).ToList();
            var assignedActivityIds = planAssignments.Select(item => item.ActivityId).ToHashSet();
            evmAssignments.AddRange(baselineActivities
                .Where(activity => activity.Type == PlanActivityType.Task && activity.EstimateHours.GetValueOrDefault() > 0m && !assignedActivityIds.Contains(activity.Id))
                .Select(activity => new ProjectEvmAssignmentInput(
                    activity.SourceActivityId ?? activity.Id,
                    Guid.Empty,
                    activity.EstimateHours,
                    activity.StartDate,
                    activity.EndDate,
                    FindCurrent(activity)?.Progress)));

            var milestones = baselineActivities.Where(item => item.Type == PlanActivityType.Milestone).Select(activity =>
            {
                var current = FindCurrent(activity);
                return new ProjectEvmMilestoneInput(
                    activity.SourceActivityId ?? activity.Id,
                    activity.Name,
                    activity.EndDate ?? activity.StartDate,
                    current?.EndDate ?? current?.StartDate,
                    current?.ActualFinish,
                    current?.Progress);
            }).OrderBy(item => item.PlannedDate ?? item.BaselineDate).ThenBy(item => item.Name, StringComparer.Ordinal).ToList();

            result.Add(new ProjectEvmReportingPlanData(
                plan.Id,
                plan.Name,
                plan.PlanType.ToString(),
                plan.Status.ToString(),
                working.Id,
                working.VersionNumber,
                baseline.Id,
                baseline.Name ?? $"Baseline v{baseline.VersionNumber}",
                baseline.VersionNumber,
                evmAssignments,
                baselineActivities.Select(item => item.SourceActivityId ?? item.Id).Distinct().ToList(),
                workingActivities.Select(item => item.Id).ToList(),
                milestones));
        }
        return result;
    }

    private static ProjectEvmAssignmentInput ToAssignmentInput(
        PlanAssignment assignment,
        PlanActivity? activity,
        decimal? workingProgress)
    {
        var startDate = assignment.StartDate ?? activity?.StartDate;
        var endDate = assignment.EndDate ?? activity?.EndDate;
        return new ProjectEvmAssignmentInput(
            activity is null ? Guid.Empty : activity.SourceActivityId ?? activity.Id,
            assignment.ResourceId,
            PlanAssignmentHoursCalculator.ResolvePlannedHours(
                assignment.PlannedHours, assignment.HoursPerWeek, startDate, endDate),
            startDate,
            endDate,
            workingProgress);
    }
}
