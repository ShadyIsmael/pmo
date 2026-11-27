using NEXUS.SharedKernel;

namespace NEXUS.Modules.Identity.Domain;

public class User : BaseEntity
{
    public string Email { get; set; } = string.Empty;
    public string? PasswordHash { get; set; }
    public AuthenticationSource AuthenticationSource { get; set; } = AuthenticationSource.Local;
    public bool MustChangePassword { get; set; }
    public DateTime? TemporaryPasswordExpiresAt { get; set; }
    public DateTime? PasswordChangedAt { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FullName => $"{FirstName} {LastName}";
    public bool IsActive { get; set; } = true;
    public DateTime? LastLoginAt { get; set; }
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public ICollection<UserSession> Sessions { get; set; } = new List<UserSession>();
    public ICollection<UserPermissionOverride> PermissionOverrides { get; set; } = new List<UserPermissionOverride>();
    public ICollection<UserOrganization> UserOrganizations { get; set; } = new List<UserOrganization>();
}

public enum AuthenticationSource
{
    Local = 0,
    EntraId = 1,
    ActiveDirectoryLdap = 2
}
