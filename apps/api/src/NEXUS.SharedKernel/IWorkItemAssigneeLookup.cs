namespace NEXUS.SharedKernel;

public interface IWorkItemAssigneeLookup
{
    Task<IReadOnlyList<WorkItemAssigneeReference>> GetEligibleAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WorkItemTeamReference>> GetEligibleTeamsAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<bool> IsActiveProjectMemberAsync(Guid projectId, Guid resourceId, CancellationToken cancellationToken = default);
    Task<bool> IsActiveProjectTeamAsync(Guid projectId, Guid teamId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Guid>> GetActiveTeamIdsForUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<bool> IsCurrentTeamMemberAsync(Guid teamId, Guid resourceId, CancellationToken cancellationToken = default);
}

public sealed record WorkItemAssigneeReference(
    Guid ResourceId,
    string Name,
    string? Email,
    string? JobTitle);

public sealed record WorkItemTeamReference(Guid TeamId, string Name, string? Code);
