namespace NEXUS.Modules.Identity.Application;

public sealed class PermissionCatalogItemDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Resource { get; init; } = string.Empty;
    public string Action { get; init; } = string.Empty;
    public string? Description { get; init; }
}

public class RoleSummaryDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? Category { get; init; }
    public bool IsSystem { get; init; }
    public int PermissionCount { get; init; }
    public int AssignedUserCount { get; init; }
}

public sealed class RoleDetailDto : RoleSummaryDto
{
    public IReadOnlyList<PermissionCatalogItemDto> Permissions { get; init; } = [];
}

public sealed class SaveRoleDto
{
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? Category { get; init; }
    public List<Guid> PermissionIds { get; init; } = [];
}
