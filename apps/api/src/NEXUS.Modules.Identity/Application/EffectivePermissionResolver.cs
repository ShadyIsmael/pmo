using NEXUS.Modules.Identity.Domain;

namespace NEXUS.Modules.Identity.Application;

/// <summary>Resolves inherited grants and overrides with Deny precedence.</summary>
public static class EffectivePermissionResolver
{
    public static HashSet<string> Resolve(
        IEnumerable<string> inheritedPermissions,
        IEnumerable<(string Permission, PermissionOverrideEffect Effect)> overrides)
    {
        var effective = inheritedPermissions.ToHashSet(StringComparer.Ordinal);
        var denied = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in overrides)
        {
            if (item.Effect == PermissionOverrideEffect.Allow)
                effective.Add(item.Permission);
            else if (item.Effect == PermissionOverrideEffect.Deny)
                denied.Add(item.Permission);
        }

        effective.ExceptWith(denied);
        return effective;
    }
}
