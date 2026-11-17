using NEXUS.Modules.Identity.Domain;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.Identity.Application;

/// <summary>
/// Resolves stable Role IDs and validates tenant, Organization, protected-role,
/// permission-delegation, and self-escalation rules before UserRole rows are saved.
/// </summary>
public interface IRoleAssignmentAuthorizationService
{
    Task<Result<IReadOnlyList<Role>>> ResolveAndAuthorizeAsync(
        IReadOnlyCollection<Guid> roleIds,
        Guid actorUserId,
        Guid tenantId,
        Guid? targetUserId,
        CancellationToken cancellationToken = default);
}
