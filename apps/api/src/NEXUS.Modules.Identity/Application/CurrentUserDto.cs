using NEXUS.Modules.Identity.Domain;

namespace NEXUS.Modules.Identity.Application;

public class CurrentUserDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FullName => $"{FirstName} {LastName}";
    public string? JobTitle { get; set; }
    public string? Phone { get; set; }
    public string? AvatarUrl { get; set; }
    public string? TenantId { get; set; }
    public AuthenticationSource AuthenticationSource { get; set; }
    public bool MustChangePassword { get; set; }
    public DateTime? TemporaryPasswordExpiresAt { get; set; }
    public List<RoleDto> Roles { get; set; } = new();
    public List<string> Permissions { get; set; } = new();
}

public class RoleDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Category { get; set; }
}
