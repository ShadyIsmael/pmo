using System.Text.Json;

namespace NEXUS.Modules.Organization.Application.Settings;

public sealed record StoredConfigurationValue(Guid Id, string Key, JsonElement Value, string Version);

public sealed record EffectiveSettingValue(
    SettingDefinition Definition,
    JsonElement Value,
    SettingValueSource Source,
    bool IsInherited,
    bool CanOverride,
    string? Version);

/// <summary>Resolves one authenticated owner chain. Maps passed to this resolver must already be owner-scoped.</summary>
public sealed class EffectiveConfigurationResolver
{
    public IReadOnlyList<EffectiveSettingValue> ResolvePlatform(
        ISettingDefinitionCatalog catalog,
        IReadOnlyDictionary<string, StoredConfigurationValue> platformValues) => catalog.All
        .Where(definition => definition.AllowedScopes.Contains(SettingScope.Platform))
        .Select(definition =>
        {
            if (!platformValues.TryGetValue(definition.Key, out var stored))
                return new EffectiveSettingValue(definition, definition.DefaultValue.Clone(), SettingValueSource.DefinitionDefault,
                    false, definition.CanOverrideAt(SettingScope.Platform), null);

            if (!definition.CanOverrideAt(SettingScope.Platform))
                throw new InvalidOperationException($"Stored platform override for {definition.Key} is prohibited by its definition.");
            EnsureStoredValueIsValid(definition, stored.Value);
            return new EffectiveSettingValue(definition, stored.Value.Clone(), SettingValueSource.PlatformOverride,
                false, definition.CanOverrideAt(SettingScope.Platform), stored.Version);
        })
        .OrderBy(value => value.Definition.Key, StringComparer.Ordinal)
        .ToArray();

    public IReadOnlyList<EffectiveSettingValue> ResolveTenant(
        ISettingDefinitionCatalog catalog,
        IReadOnlyDictionary<string, StoredConfigurationValue> platformValues,
        IReadOnlyDictionary<string, StoredConfigurationValue> tenantValues) => catalog.All
        .Where(definition => definition.AllowedScopes.Contains(SettingScope.Tenant))
        .Select(definition =>
        {
            var parent = ResolvePlatformValue(definition, platformValues);
            if (!tenantValues.TryGetValue(definition.Key, out var tenantValue))
                return new EffectiveSettingValue(definition, parent.Value, parent.Source, true,
                    definition.CanOverrideAt(SettingScope.Tenant), null);

            if (!definition.CanOverrideAt(SettingScope.Tenant))
                throw new InvalidOperationException($"Stored tenant override for {definition.Key} is prohibited by its definition.");
            EnsureStoredValueIsValid(definition, tenantValue.Value);
            ValidateInheritedConstraint(definition, SettingScope.Tenant, tenantValue.Value, parent.Value);
            return new EffectiveSettingValue(definition, tenantValue.Value.Clone(), SettingValueSource.TenantOverride,
                false, definition.CanOverrideAt(SettingScope.Tenant), tenantValue.Version);
        })
        .OrderBy(value => value.Definition.Key, StringComparer.Ordinal)
        .ToArray();

    public IReadOnlyList<EffectiveSettingValue> ResolveOrganization(
        ISettingDefinitionCatalog catalog,
        IReadOnlyDictionary<string, StoredConfigurationValue> platformValues,
        IReadOnlyDictionary<string, StoredConfigurationValue> tenantValues,
        IReadOnlyDictionary<string, StoredConfigurationValue> organizationValues) =>
        ResolveEffectiveConfiguration(catalog, SettingScope.Organization, platformValues, tenantValues, organizationValues);

    /// <summary>
    /// Resolves a scope using the approved precedence Platform → Tenant → Organization → Project → User.
    /// Project and User maps are extension points; this delivery exposes no Project/User persistence or APIs.
    /// </summary>
    public IReadOnlyList<EffectiveSettingValue> ResolveEffectiveConfiguration(
        ISettingDefinitionCatalog catalog,
        SettingScope requestedScope,
        IReadOnlyDictionary<string, StoredConfigurationValue> platformValues,
        IReadOnlyDictionary<string, StoredConfigurationValue>? tenantValues = null,
        IReadOnlyDictionary<string, StoredConfigurationValue>? organizationValues = null,
        IReadOnlyDictionary<string, StoredConfigurationValue>? projectValues = null,
        IReadOnlyDictionary<string, StoredConfigurationValue>? userValues = null)
    {
        return catalog.All
            .Where(definition => definition.AllowedScopes.Contains(requestedScope))
            .Select(definition =>
            {
                var value = definition.DefaultValue.Clone();
                var source = requestedScope == SettingScope.Platform || !definition.AllowedScopes.Contains(SettingScope.Platform)
                    ? SettingValueSource.DefinitionDefault
                    : SettingValueSource.PlatformDefault;
                SettingScope? valueScope = null;
                string? version = null;

                foreach (var (scope, values, overrideSource) in new[]
                {
                    (SettingScope.Platform, platformValues, SettingValueSource.PlatformOverride),
                    (SettingScope.Tenant, tenantValues, SettingValueSource.TenantOverride),
                    (SettingScope.Organization, organizationValues, SettingValueSource.OrganizationOverride),
                    (SettingScope.Project, projectValues, SettingValueSource.ProjectOverride),
                    (SettingScope.User, userValues, SettingValueSource.UserOverride)
                })
                {
                    if (ScopeOrder(scope) > ScopeOrder(requestedScope)
                        || !definition.AllowedScopes.Contains(scope)
                        || values is null
                        || !values.TryGetValue(definition.Key, out var stored)) continue;
                    if (!definition.CanOverrideAt(scope))
                        throw new InvalidOperationException($"Stored {scope} override for {definition.Key} is prohibited by its definition.");
                    EnsureStoredValueIsValid(definition, stored.Value);
                    ValidateInheritedConstraint(definition, scope, stored.Value, value);
                    value = stored.Value.Clone();
                    source = overrideSource;
                    valueScope = scope;
                    version = stored.Version;
                }

                return new EffectiveSettingValue(definition, value, source,
                    valueScope != requestedScope, definition.CanOverrideAt(requestedScope),
                    valueScope == requestedScope ? version : null);
            })
            .OrderBy(value => value.Definition.Key, StringComparer.Ordinal)
            .ToArray();
    }

    private static (JsonElement Value, SettingValueSource Source) ResolvePlatformValue(
        SettingDefinition definition,
        IReadOnlyDictionary<string, StoredConfigurationValue> platformValues)
    {
        if (!platformValues.TryGetValue(definition.Key, out var platformValue))
            return (definition.DefaultValue.Clone(), SettingValueSource.DefinitionDefault);
        if (!definition.CanOverrideAt(SettingScope.Platform))
            throw new InvalidOperationException($"Stored platform override for {definition.Key} is prohibited by its definition.");
        EnsureStoredValueIsValid(definition, platformValue.Value);
        return (platformValue.Value.Clone(), SettingValueSource.PlatformOverride);
    }

    private static void ValidateInheritedConstraint(
        SettingDefinition definition, SettingScope scope, JsonElement value, JsonElement inheritedValue)
    {
        if (definition.OverrideBehavior is not (SettingOverrideBehavior.ConstrainedTenantOverride
            or SettingOverrideBehavior.ConstrainedTenantAndOrganizationOverride
            or SettingOverrideBehavior.ConstrainedHierarchicalOverride)) return;
        var error = definition.ValidateOverride(scope, value, inheritedValue);
        if (error is not null) throw new InvalidOperationException($"Stored override for {definition.Key} violates inherited policy: {error}");
    }

    private static void EnsureStoredValueIsValid(SettingDefinition definition, JsonElement value)
    {
        var error = definition.Validate(value);
        if (error is not null) throw new InvalidOperationException($"Persisted value for {definition.Key} is invalid: {error}");
    }

    private static int ScopeOrder(SettingScope scope) => scope switch
    {
        SettingScope.Platform => 0,
        SettingScope.Tenant => 1,
        SettingScope.Organization => 2,
        SettingScope.Project => 3,
        SettingScope.User => 4,
        _ => throw new ArgumentOutOfRangeException(nameof(scope))
    };
}
