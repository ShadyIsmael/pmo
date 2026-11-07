using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.CostManagement.Application;
using NEXUS.Modules.ProjectPlanning.Domain;

namespace NEXUS.Persistence.Lookups;

public sealed class ProjectPlanMilestoneLookup(DbContext context) : IProjectMilestoneReferenceLookup
{
    public Task<bool> IsActiveMilestoneForProjectAsync(Guid projectId, Guid milestoneId, CancellationToken cancellationToken = default) =>
        context.Set<PlanActivity>()
            .AsNoTracking()
            .AnyAsync(activity =>
                activity.Id == milestoneId
                && activity.Type == PlanActivityType.Milestone
                && activity.PlanVersion != null
                && activity.PlanVersion.Type == PlanVersionType.Working
                && activity.PlanVersion.Status == PlanVersionStatus.Active
                && activity.PlanVersion.Plan != null
                && activity.PlanVersion.Plan.ProjectId == projectId,
                cancellationToken);

    public Task<string?> FindActiveMilestoneNameForProjectAsync(Guid projectId, Guid milestoneId, CancellationToken cancellationToken = default) =>
        context.Set<PlanActivity>()
            .AsNoTracking()
            .Where(activity =>
                activity.Id == milestoneId
                && activity.Type == PlanActivityType.Milestone
                && activity.PlanVersion != null
                && activity.PlanVersion.Type == PlanVersionType.Working
                && activity.PlanVersion.Status == PlanVersionStatus.Active
                && activity.PlanVersion.Plan != null
                && activity.PlanVersion.Plan.ProjectId == projectId)
            .Select(activity => activity.Name)
            .FirstOrDefaultAsync(cancellationToken);
}
