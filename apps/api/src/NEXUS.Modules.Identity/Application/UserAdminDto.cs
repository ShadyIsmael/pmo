using NEXUS.Modules.Identity.Domain;

namespace NEXUS.Modules.Identity.Application;

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
    public List<string> Roles { get; set; } = new();
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
    public List<string> Roles { get; set; } = new();
}

public class UpdateUserDto
{
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public List<string> Roles { get; set; } = new();
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
