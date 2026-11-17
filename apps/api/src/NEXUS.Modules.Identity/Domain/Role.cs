using NEXUS.SharedKernel;

namespace NEXUS.Modules.Identity.Domain;

public class Role : BaseEntity
{
    public const string TenantAdminRoleKey = "tenant.admin";
    public const string TenantAdminSystemRoleKey = TenantAdminRoleKey;
    public const string OrganizationAdminRoleKey = "organization.admin";
    public const string OrganizationAdminSystemRoleKey = OrganizationAdminRoleKey;

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Category { get; set; }
    /// <summary>
    /// Immutable owner for an operational Role. Null is reserved for the two
    /// approved tenant-scoped protected System Roles and explicitly marked
    /// unresolved legacy records awaiting evidence-based reconciliation.
    /// </summary>
    public Guid? OrganizationId { get; private set; }
    /// <summary>
    /// Marks a pre-Organization-ownership operational Role whose owner cannot be
    /// established safely. Such a record is retained for audit/reconciliation,
    /// but is not assignable and grants no effective permissions.
    /// </summary>
    public bool IsLegacyOrganizationOwnershipUnresolved { get; private set; }
    public bool IsSystem { get; set; }
    public string? RoleKey { get; private set; }
    public string? SystemRoleKey { get; private set; }
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public ICollection<Permission> Permissions { get; set; } = new List<Permission>();

    /// <summary>Assigns the Role's Organization exactly once during creation.</summary>
    public void AssignOrganization(Guid organizationId)
    {
        if (organizationId == Guid.Empty)
            throw new ArgumentException("An operational Role requires a valid Organization.", nameof(organizationId));
        if (IsSystem || SystemRoleKey is not null)
            throw new InvalidOperationException("Protected System Roles cannot be Organization-owned.");
        if (IsLegacyOrganizationOwnershipUnresolved)
            throw new InvalidOperationException("An unresolved legacy Role cannot be reassigned. Create a new Role instead.");
        if (OrganizationId.HasValue && OrganizationId.Value != organizationId)
            throw new InvalidOperationException("A Role's Organization is immutable once assigned.");

        OrganizationId = organizationId;
    }

    /// <summary>
    /// Assigns an immutable, code-defined role identity independently of its
    /// classification. The display name remains editable metadata and is never
    /// used as the identity.
    /// </summary>
    public void AssignRoleKey(string roleKey, Guid expectedTenantId)
    {
        if (expectedTenantId == Guid.Empty || TenantId != expectedTenantId)
            throw new InvalidOperationException("A canonical role identity requires an explicitly verified Tenant scope.");

        ValidateRoleKey(roleKey, nameof(roleKey));

        if (!IsSystem && (roleKey is TenantAdminSystemRoleKey or OrganizationAdminSystemRoleKey))
            throw new ArgumentException("Reserved administrative RoleKeys can only be assigned to their approved System Role identities.", nameof(roleKey));

        if (SystemRoleKey is not null && !string.Equals(SystemRoleKey, roleKey, StringComparison.Ordinal))
            throw new InvalidOperationException("The canonical role identity cannot differ from its System Role identity.");
        if (RoleKey is not null && !string.Equals(RoleKey, roleKey, StringComparison.Ordinal))
            throw new InvalidOperationException("A canonical role identity is immutable once assigned.");

        RoleKey = roleKey;
    }

    /// <summary>
    /// Assigns one of the two approved System Role identities. For compatibility
    /// with trusted provisioning callers, this also establishes the matching
    /// neutral RoleKey when it has not yet been assigned.
    /// </summary>
    public void AssignSystemRoleKey(string systemRoleKey, Guid expectedTenantId)
    {
        if (!IsSystem || expectedTenantId == Guid.Empty || TenantId != expectedTenantId)
            throw new InvalidOperationException("Only system roles in the explicitly verified Tenant scope can receive a system-role identity.");
        if (OrganizationId is not null || IsLegacyOrganizationOwnershipUnresolved)
            throw new InvalidOperationException("A System Role must remain Tenant-scoped and cannot carry unresolved Organization ownership.");
        ValidateRoleKey(systemRoleKey, nameof(systemRoleKey));
        if (SystemRoleKey is not null && !string.Equals(SystemRoleKey, systemRoleKey, StringComparison.Ordinal))
            throw new InvalidOperationException("A system-role identity is immutable once assigned.");
        if (RoleKey is not null && !string.Equals(RoleKey, systemRoleKey, StringComparison.Ordinal))
            throw new InvalidOperationException("A canonical role identity is immutable once assigned.");
        if (systemRoleKey is not TenantAdminSystemRoleKey and not OrganizationAdminSystemRoleKey)
            throw new ArgumentException("Only Tenant Admin and Organization Admin may have System Role identities.", nameof(systemRoleKey));

        AssignRoleKey(systemRoleKey, expectedTenantId);

        SystemRoleKey = systemRoleKey;
    }

    /// <summary>
    /// Removes legacy System classification from a canonical ordinary role while
    /// retaining its stable RoleKey. The two approved System Role identities
    /// cannot be downgraded through this method.
    /// </summary>
    public void MarkAsNonSystemRole()
    {
        if (SystemRoleKey is TenantAdminSystemRoleKey or OrganizationAdminSystemRoleKey)
            throw new InvalidOperationException("An approved System Role identity cannot be reclassified as ordinary.");
        if (SystemRoleKey is not null && !string.Equals(SystemRoleKey, RoleKey, StringComparison.Ordinal))
            throw new InvalidOperationException("A legacy system-role identity cannot be removed until the matching neutral RoleKey is established.");

        SystemRoleKey = null;
        IsSystem = false;
    }

    private static void ValidateRoleKey(string roleKey, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(roleKey)
            || roleKey.Length > 64
            || !string.Equals(roleKey, roleKey.Trim(), StringComparison.Ordinal))
            throw new ArgumentException("A canonical role identity must be a non-empty, trimmed value of at most 64 characters.", parameterName);
    }
}
