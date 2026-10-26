using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.ProjectPlanning.Domain;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.ProjectPlanning.Application;

/// <summary>
/// Calculates the current plan completion from the same active Working version
/// rendered by the Project Plan workspace. A missing activity percentage is not
/// treated as zero; the result is unavailable until at least one activity has a
/// stored percentage of completion.
/// </summary>
public sealed class ProjectPlanProgressLookup : IProjectPlanProgressLookup
{
    private readonly DbContext _context;

    public ProjectPlanProgressLookup(DbContext context)
    {
        _context = context;
    }

    public async Task<decimal?> GetAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var progressByProject = await GetManyAsync([projectId], cancellationToken);
        return progressByProject.TryGetValue(projectId, out var progress) ? progress : null;
    }

    public async Task<IReadOnlyDictionary<Guid, decimal?>> GetManyAsync(
        IEnumerable<Guid> projectIds,
        CancellationToken cancellationToken = default)
    {
        var requestedIds = projectIds.Where(id => id != Guid.Empty).Distinct().ToArray();
        var result = requestedIds.ToDictionary(id => id, _ => (decimal?)null);
        if (requestedIds.Length == 0) return result;

        // Resolve the page's projects in batches so the registry does not issue
        // one planning query per project. EF's tenant query filters remain active.
        var plans = await _context.Set<Plan>()
            .Where(plan => requestedIds.Contains(plan.ProjectId) && !plan.IsDeleted)
            .OrderBy(plan => plan.ProjectId)
            .ThenByDescending(plan => plan.CreatedAt)
            .ThenByDescending(plan => plan.Id)
            .Select(plan => new { plan.ProjectId, plan.Id })
            .ToListAsync(cancellationToken);
        var latestPlans = plans
            .GroupBy(plan => plan.ProjectId)
            .Select(group => group.First())
            .ToArray();
        if (latestPlans.Length == 0) return result;

        var planIds = latestPlans.Select(plan => plan.Id).ToArray();
        var workingVersions = await _context.Set<PlanVersion>()
            .Where(version => planIds.Contains(version.PlanId)
                && version.Type == PlanVersionType.Working
                && version.Status == PlanVersionStatus.Active
                && !version.IsDeleted)
            .OrderBy(version => version.PlanId)
            .ThenByDescending(version => version.CreatedAt)
            .ThenByDescending(version => version.Id)
            .Select(version => new { version.PlanId, version.Id })
            .ToListAsync(cancellationToken);
        var latestWorkingVersions = workingVersions
            .GroupBy(version => version.PlanId)
            .Select(group => group.First())
            .ToArray();
        if (latestWorkingVersions.Length == 0) return result;

        var workingVersionIds = latestWorkingVersions.Select(version => version.Id).ToArray();
        var progressRows = await _context.Set<PlanActivity>()
            .Where(activity => workingVersionIds.Contains(activity.PlanVersionId)
                && !activity.IsDeleted
                && activity.Progress.HasValue)
            .Select(activity => new { activity.PlanVersionId, Progress = activity.Progress!.Value })
            .ToListAsync(cancellationToken);
        var progressByVersion = progressRows
            .GroupBy(activity => activity.PlanVersionId)
            .ToDictionary(group => group.Key, group => Math.Clamp(group.Average(activity => activity.Progress), 0m, 100m));
        var projectByPlanId = latestPlans.ToDictionary(plan => plan.Id, plan => plan.ProjectId);

        foreach (var version in latestWorkingVersions)
        {
            if (progressByVersion.TryGetValue(version.Id, out var progress)
                && projectByPlanId.TryGetValue(version.PlanId, out var projectId))
            {
                result[projectId] = progress;
            }
        }

        return result;
    }
}
