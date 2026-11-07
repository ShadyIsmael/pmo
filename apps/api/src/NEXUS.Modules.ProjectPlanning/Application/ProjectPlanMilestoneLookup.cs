using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.ProjectPlanning.Domain;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.ProjectPlanning.Application;

/// <summary>
/// Returns the five nearest upcoming, incomplete milestones from the same
/// active Working version the Project Plan workspace selects by default. Plan,
/// version, and activity queries retain tenant and soft-delete filters and are
/// restricted to the requested project.
/// </summary>
public sealed class ProjectPlanMilestoneLookup : IProjectPlanMilestoneLookup
{
    private readonly DbContext _context;

    public ProjectPlanMilestoneLookup(DbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<PlanMilestoneLookupItem>> GetUpcomingAsync(Guid projectId, DateTime statusDate, CancellationToken cancellationToken = default)
    {
        var planId = await _context.Set<Plan>()
            .Where(plan => plan.ProjectId == projectId)
            .OrderByDescending(plan => plan.CreatedAt)
            .ThenByDescending(plan => plan.Id)
            .Select(plan => (Guid?)plan.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (!planId.HasValue) return Array.Empty<PlanMilestoneLookupItem>();

        var workingVersionId = await _context.Set<PlanVersion>()
            .Where(version => version.PlanId == planId.Value
                && version.Type == PlanVersionType.Working
                && version.Status == PlanVersionStatus.Active)
            .OrderByDescending(version => version.VersionNumber)
            .Select(version => (Guid?)version.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (!workingVersionId.HasValue) return Array.Empty<PlanMilestoneLookupItem>();

        var statusDay = statusDate.Date;
        return await _context.Set<PlanActivity>()
            .AsNoTracking()
            .Where(activity => activity.PlanVersionId == workingVersionId.Value
                && activity.Type == PlanActivityType.Milestone
                && activity.EndDate.HasValue
                && activity.EndDate.Value >= statusDay
                && !activity.ActualFinish.HasValue
                && (!activity.Progress.HasValue || activity.Progress.Value < 100))
            .OrderBy(activity => activity.EndDate)
            .ThenBy(activity => activity.Name)
            .ThenBy(activity => activity.Id)
            .Take(5)
            .Select(activity => new PlanMilestoneLookupItem(
                activity.Id,
                activity.Name,
                activity.EndDate,
                activity.ActualFinish,
                activity.Progress))
            .ToListAsync(cancellationToken);
    }
}
