using NEXUS.SharedKernel;

namespace NEXUS.Modules.Identity.Domain;

public class Role : BaseEntity
{
    public const string TenantAdminSystemRoleKey = "tenant.admin";

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Category { get; set; }
    public bool IsSystem { get; set; }
    public string? SystemRoleKey { get; private set; }
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public ICollection<Permission> Permissions { get; set; } = new List<Permission>();

    /// <summary>
    /// Assigns an immutable, code-defined identity to a trusted system role. The
    /// display name remains editable metadata and is never used as the identity.
    /// </summary>
    public void AssignSystemRoleKey(string systemRoleKey, Guid expectedTenantId)
    {
        if (!IsSystem || expectedTenantId == Guid.Empty || TenantId != expectedTenantId)
            throw new InvalidOperationException("Only system roles in the explicitly verified Tenant scope can receive a system-role identity.");

        if (string.IsNullOrWhiteSpace(systemRoleKey)
            || systemRoleKey.Length > 64
            || !string.Equals(systemRoleKey, systemRoleKey.Trim(), StringComparison.Ordinal))
            throw new ArgumentException("A system-role identity must be a non-empty, trimmed value of at most 64 characters.", nameof(systemRoleKey));

        if (SystemRoleKey is not null && !string.Equals(SystemRoleKey, systemRoleKey, StringComparison.Ordinal))
            throw new InvalidOperationException("A system-role identity is immutable once assigned.");

        SystemRoleKey = systemRoleKey;
    }
}
