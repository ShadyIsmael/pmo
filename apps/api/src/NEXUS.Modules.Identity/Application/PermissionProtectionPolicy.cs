using Microsoft.Extensions.Options;

namespace NEXUS.Modules.Identity.Application;

public sealed class PermissionProtectionOptions
{
    public const string SectionName = "Security:PermissionProtection";

    /// <summary>
    /// Permission keys the Product Owner has explicitly designated as protected.
    /// The empty default makes every active, non-reserved permission grantable.
    /// </summary>
    public string[] ProtectedPermissionKeys { get; set; } = [];
}

public interface IPermissionProtectionPolicy
{
    IReadOnlySet<string> ProtectedPermissionKeys { get; }
    bool IsProtected(string permissionKey);
    bool IsGrantable(PermissionCatalogEntry permission);
}

/// <summary>
/// The single source of permission protection. Grantability is open by default;
/// only keys listed in Security:PermissionProtection:ProtectedPermissionKeys
/// receive the non-grantable protection rule. Scope and authorization checks are
/// evaluated separately and are not changed by this policy.
/// </summary>
public sealed class PermissionProtectionPolicy : IPermissionProtectionPolicy
{
    private readonly HashSet<string> _protectedPermissionKeys;

    public static PermissionProtectionPolicy Empty { get; } = new(Array.Empty<string>());

    public PermissionProtectionPolicy(IOptions<PermissionProtectionOptions> options)
        : this(options.Value.ProtectedPermissionKeys)
    {
    }

    private PermissionProtectionPolicy(IEnumerable<string> protectedPermissionKeys)
    {
        _protectedPermissionKeys = protectedPermissionKeys
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Select(key => key.Trim())
            .ToHashSet(StringComparer.Ordinal);
    }

    public IReadOnlySet<string> ProtectedPermissionKeys => _protectedPermissionKeys;

    public bool IsProtected(string permissionKey) => _protectedPermissionKeys.Contains(permissionKey);

    public bool IsGrantable(PermissionCatalogEntry permission) =>
        permission.IsActive && !IsProtected(permission.Key);

    public static bool IsValidConfiguration(PermissionProtectionOptions options) =>
        options.ProtectedPermissionKeys is not null
        && options.ProtectedPermissionKeys.All(key =>
            !string.IsNullOrWhiteSpace(key)
            && PermissionCatalog.ByKey.TryGetValue(key.Trim(), out var permission)
            && permission.IsActive)
        && options.ProtectedPermissionKeys
            .Select(key => key?.Trim())
            .Distinct(StringComparer.Ordinal).Count()
            == options.ProtectedPermissionKeys.Length;
}
