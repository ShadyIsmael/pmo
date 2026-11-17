namespace NEXUS.SharedKernel;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class OrganizationScopedCollectionPermissionAttribute : Attribute
{
    public IReadOnlyList<string> PermissionKeys { get; }

    public OrganizationScopedCollectionPermissionAttribute(params string[] permissionKeys)
    {
        if (permissionKeys is null || permissionKeys.Length == 0 || permissionKeys.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("At least one non-empty permission key is required.", nameof(permissionKeys));

        PermissionKeys = Array.AsReadOnly(permissionKeys.Distinct(StringComparer.Ordinal).ToArray());
    }
}
