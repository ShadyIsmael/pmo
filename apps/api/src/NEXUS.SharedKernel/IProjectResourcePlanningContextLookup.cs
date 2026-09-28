namespace NEXUS.SharedKernel;

public interface IProjectResourcePlanningContextLookup
{
    Task<ProjectResourcePlanningContext?> GetPlanningContextAsync(Guid projectId, Guid resourceId, CancellationToken cancellationToken = default);
    Task<bool> HasActiveProjectMemberRoleAsync(Guid projectId, Guid resourceId, Guid projectRoleId, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<Guid, ProjectResourcePlanningContext>> GetPlanningContextsAsync(Guid projectId, IEnumerable<Guid> resourceIds, CancellationToken cancellationToken = default);
}

public sealed record ProjectResourcePlanningContext(
    Guid ProjectMemberId,
    Guid ProjectId,
    Guid ResourceId,
    string CalendarPolicy,
    Guid? ProjectCalendarId,
    IReadOnlyList<Guid> RoleIds,
    bool IsActive);
