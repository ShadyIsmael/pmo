namespace NEXUS.SharedKernel;

public interface IWorkItemAssigneeLookup
{
    Task<IReadOnlyList<WorkItemAssigneeReference>> GetEligibleAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<bool> IsActiveProjectMemberAsync(Guid projectId, Guid userId, CancellationToken cancellationToken = default);
}

public sealed record WorkItemAssigneeReference(
    Guid AssigneeId,
    Guid ResourceId,
    string Name,
    string? Email,
    string? JobTitle);
