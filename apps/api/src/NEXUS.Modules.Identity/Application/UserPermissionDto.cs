using NEXUS.Modules.Identity.Domain;

namespace NEXUS.Modules.Identity.Application;

public sealed class UserPermissionAssignmentDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Resource { get; init; } = string.Empty;
    public string Action { get; init; } = string.Empty;
    public string? Description { get; init; }
    public bool IsRoleGranted { get; init; }
    public PermissionOverrideEffect? OverrideEffect { get; init; }
    public bool IsEffective { get; init; }
}

public sealed class UpdateUserPermissionsDto
{
    public List<UserPermissionOverrideInput> Overrides { get; init; } = new();
}

public sealed class UserPermissionOverrideInput
{
    public Guid PermissionId { get; init; }
    public PermissionOverrideEffect Effect { get; init; }
}
