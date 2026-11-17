namespace NEXUS.Modules.Identity.Application;

public sealed class PermissionCatalogItemDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Resource { get; init; } = string.Empty;
    public string Action { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string ModuleId { get; init; } = string.Empty;
    public string ModuleNameEn { get; init; } = string.Empty;
    public string ModuleNameAr { get; init; } = string.Empty;
    public string ScreenId { get; init; } = string.Empty;
    public string ScreenNameEn { get; init; } = string.Empty;
    public string ScreenNameAr { get; init; } = string.Empty;
    public string Scope { get; init; } = "Tenant";
    public string Level { get; init; } = "Action";
    public string LabelEn { get; init; } = string.Empty;
    public string LabelAr { get; init; } = string.Empty;
    public string DescriptionEn { get; init; } = string.Empty;
    public string DescriptionAr { get; init; } = string.Empty;
    public string Sensitivity { get; init; } = "Low";
    public bool IsSensitive { get; init; }
    public IReadOnlyList<string> RequiredPermissionKeys { get; init; } = [];
    public IReadOnlyList<string> AllowedCategories { get; init; } = [];
    public bool IsActive { get; init; }
    public bool IsAssignable { get; init; }
    public bool CanGrantToOrganizationRole { get; init; }
    public bool? CanDelegate { get; init; }
    public bool? CanDelegateToCustomRole { get; init; }
    public bool? CanDelegateToOrdinaryRole { get; init; }
}

public class RoleSummaryDto
{
    public Guid Id { get; init; }
    public Guid? OrganizationId { get; init; }
    public string? OrganizationName { get; init; }
    public bool IsOrganizationOwned { get; init; }
    public bool IsLegacyOrganizationOwnershipUnresolved { get; init; }
    /// <summary>Ownership scope: Organization for operational roles, Tenant for protected system roles.</summary>
    public string Scope { get; init; } = "Tenant";
    public string? RoleKey { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? Category { get; init; }
    public bool IsSystem { get; init; }
    public bool IsProtected { get; init; }
    public bool IsProtectedSystemAdmin { get; init; }
    public bool IsAssignable { get; init; }
    public bool CanEditDetails { get; init; }
    public bool CanManagePermissions { get; init; }
    public bool CanDelete { get; init; }
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
    public Guid? OrganizationId { get; init; }
    public List<Guid> PermissionIds { get; init; } = [];
}

public sealed class RoleOrganizationOptionDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
}
