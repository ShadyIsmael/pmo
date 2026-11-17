using NEXUS.Modules.Identity.Domain;

namespace NEXUS.Modules.Identity.Application;

public sealed class UserPermissionAssignmentDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Resource { get; init; } = string.Empty;
    public string Action { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? ScreenNameEn { get; init; }
    public string? ScreenNameAr { get; init; }
    public string? LabelEn { get; init; }
    public string? LabelAr { get; init; }
    public bool IsActive { get; init; }
    public bool IsAssignable { get; init; }
    public bool CanDelegate { get; init; }
    public bool IsRoleGranted { get; init; }
    public string Scope { get; init; } = PermissionScope.Tenant.ToString();
    public IReadOnlyList<string> RoleGrantSources { get; init; } = Array.Empty<string>();
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
