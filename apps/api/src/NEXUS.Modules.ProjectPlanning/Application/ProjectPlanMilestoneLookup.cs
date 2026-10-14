using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.ProjectPlanning.Domain;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.ProjectPlanning.Application;

/// <summary>
/// Returns the milestones of the same active Working version rendered by the
/// Project Plan workspace, so overview surfaces (e.g. the project overview
/// milestone timeline) reflect the plan of record rather than the legacy
/// unversioned Milestones table.
/// </summary>
public sealed class ProjectPlanMilestoneLookup : IProjectPlanMilestoneLookup
{
    private readonly DbContext _context;

    public ProjectPlanMilestoneLookup(DbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<PlanMilestoneLookupItem>> GetAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var planId = await _context.Set<Plan>()
            .Where(plan => plan.ProjectId == projectId && !plan.IsDeleted)
            .OrderByDescending(plan => plan.CreatedAt)
            .ThenByDescending(plan => plan.Id)
            .Select(plan => (Guid?)plan.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (!planId.HasValue) return Array.Empty<PlanMilestoneLookupItem>();

        var workingVersionId = await _context.Set<PlanVersion>()
            .Where(version => version.PlanId == planId.Value
                && version.Type == PlanVersionType.Working
                && version.Status == PlanVersionStatus.Active
                && !version.IsDeleted)
            .Select(version => (Guid?)version.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (!workingVersionId.HasValue) return Array.Empty<PlanMilestoneLookupItem>();

        return await _context.Set<PlanActivity>()
            .AsNoTracking()
            .Where(activity => activity.PlanVersionId == workingVersionId.Value
                && activity.Type == PlanActivityType.Milestone
                && !activity.IsDeleted)
            .OrderBy(activity => activity.EndDate.HasValue ? 0 : 1)
            .ThenBy(activity => activity.EndDate)
            .ThenBy(activity => activity.Name)
            .Select(activity => new PlanMilestoneLookupItem(
                activity.Id,
                activity.Name,
                activity.EndDate,
                activity.ActualFinish,
                activity.Progress))
            .ToListAsync(cancellationToken);
    }
}
