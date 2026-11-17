using NEXUS.Modules.Identity.Domain;

namespace NEXUS.Modules.Identity.Application;

public sealed class UserOrganizationDto
{
    public Guid Id { get; set; }
    public string? Name { get; set; }
}

public sealed class UserAdminRoleDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid? OrganizationId { get; set; }
    public string? OrganizationName { get; set; }
    public string Scope { get; set; } = "Tenant";
    public bool IsOrganizationOwned { get; set; }
    public bool IsLegacyOrganizationOwnershipUnresolved { get; set; }
    public bool IsOrganizationOwnershipUnresolved { get; set; }
    public bool IsProtected { get; set; }
    public bool CanRemove { get; set; }
}

public class UserAdminDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public AuthenticationSource AuthenticationSource { get; set; }
    public bool MustChangePassword { get; set; }
    public DateTime? TemporaryPasswordExpiresAt { get; set; }
    public List<UserAdminRoleDto> Roles { get; set; } = new();
    public List<UserOrganizationDto> Organizations { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
}

public class CreateUserDto
{
    public string Email { get; set; } = string.Empty;
    public AuthenticationSource AuthenticationSource { get; set; } = AuthenticationSource.Local;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public List<Guid>? RoleIds { get; set; }
}

public class UpdateUserDto
{
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public List<Guid>? RoleIds { get; set; }
}

public sealed class CreatedUserDto
{
    public UserAdminDto User { get; init; } = null!;
    public string? TemporaryPassword { get; init; }
}

public sealed class TemporaryPasswordResetDto
{
    public Guid UserId { get; init; }
    public string TemporaryPassword { get; init; } = string.Empty;
    public DateTime ExpiresAt { get; init; }
}
