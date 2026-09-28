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
        var planId = await _context.Set<Plan>()
            .Where(plan => plan.ProjectId == projectId && !plan.IsDeleted)
            .OrderByDescending(plan => plan.CreatedAt)
            .ThenByDescending(plan => plan.Id)
            .Select(plan => (Guid?)plan.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (!planId.HasValue) return null;

        var workingVersionId = await _context.Set<PlanVersion>()
            .Where(version => version.PlanId == planId.Value
                && version.Type == PlanVersionType.Working
                && version.Status == PlanVersionStatus.Active
                && !version.IsDeleted)
            .Select(version => (Guid?)version.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (!workingVersionId.HasValue) return null;

        var progressValues = await _context.Set<PlanActivity>()
            .Where(activity => activity.PlanVersionId == workingVersionId.Value
                && !activity.IsDeleted
                && activity.Progress.HasValue)
            .Select(activity => activity.Progress!.Value)
            .ToListAsync(cancellationToken);

        if (progressValues.Count == 0) return null;

        return Math.Clamp(progressValues.Average(), 0m, 100m);
    }
}
