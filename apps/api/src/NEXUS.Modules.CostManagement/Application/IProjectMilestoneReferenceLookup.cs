namespace NEXUS.Modules.CostManagement.Application;

public interface IProjectMilestoneReferenceLookup
{
    Task<bool> IsActiveMilestoneForProjectAsync(Guid projectId, Guid milestoneId, CancellationToken cancellationToken = default);
    Task<string?> FindActiveMilestoneNameForProjectAsync(Guid projectId, Guid milestoneId, CancellationToken cancellationToken = default);
}
