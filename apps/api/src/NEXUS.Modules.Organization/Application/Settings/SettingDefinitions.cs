using System.Text.Json;

namespace NEXUS.Modules.Organization.Application.Settings;

public enum SettingScope
{
    Platform,
    Tenant,
    Organization,
    Project,
    User
}

public enum SettingValueType
{
    String,
    Integer,
    Decimal,
    Boolean
}

public enum SettingOverrideBehavior
{
    TenantOverride,
    PlatformOnly,
    InheritOnly,
    ConstrainedTenantOverride,
    TenantAndOrganizationOverride,
    ConstrainedTenantAndOrganizationOverride,
    HierarchicalOverride,
    ConstrainedHierarchicalOverride
}

public enum SettingSecurityClassification
{
    Standard,
    Sensitive,
    Secret
}

public enum SettingValueSource
{
    TenantOverride,
    OrganizationOverride,
    ProjectOverride,
    UserOverride,
    PlatformDefault,
    PlatformOverride,
    DefinitionDefault
}

public sealed class SettingDefinition
{
    private readonly Func<JsonElement, string?> _validate;
    private readonly Func<JsonElement, JsonElement, string?>? _validateOverride;

    public SettingDefinition(
        string key,
        SettingValueType type,
        IReadOnlySet<SettingScope> allowedScopes,
        JsonElement defaultValue,
        string validationDescription,
        SettingOverrideBehavior overrideBehavior,
        SettingSecurityClassification securityClassification,
        Func<JsonElement, string?> validate,
        Func<JsonElement, JsonElement, string?>? validateOverride = null)
    {
        Key = key;
        Type = type;
        AllowedScopes = allowedScopes;
        DefaultValue = defaultValue.Clone();
        ValidationDescription = validationDescription;
        OverrideBehavior = overrideBehavior;
        SecurityClassification = securityClassification;
        _validate = validate;
        _validateOverride = validateOverride;
    }

    public string Key { get; }
    public SettingValueType Type { get; }
    public IReadOnlySet<SettingScope> AllowedScopes { get; }
    public JsonElement DefaultValue { get; }
    public string ValidationDescription { get; }
    public SettingOverrideBehavior OverrideBehavior { get; }
    public SettingSecurityClassification SecurityClassification { get; }

    public bool CanOverrideAt(SettingScope scope)
    {
        if (!AllowedScopes.Contains(scope) || OverrideBehavior == SettingOverrideBehavior.InheritOnly) return false;
        return scope switch
        {
            SettingScope.Platform => true,
            SettingScope.Tenant => OverrideBehavior is SettingOverrideBehavior.TenantOverride
                or SettingOverrideBehavior.ConstrainedTenantOverride
                or SettingOverrideBehavior.TenantAndOrganizationOverride
                or SettingOverrideBehavior.ConstrainedTenantAndOrganizationOverride
                or SettingOverrideBehavior.HierarchicalOverride
                or SettingOverrideBehavior.ConstrainedHierarchicalOverride,
            SettingScope.Organization => OverrideBehavior is SettingOverrideBehavior.TenantAndOrganizationOverride
                or SettingOverrideBehavior.ConstrainedTenantAndOrganizationOverride
                or SettingOverrideBehavior.HierarchicalOverride
                or SettingOverrideBehavior.ConstrainedHierarchicalOverride,
            SettingScope.Project or SettingScope.User => OverrideBehavior is SettingOverrideBehavior.HierarchicalOverride
                or SettingOverrideBehavior.ConstrainedHierarchicalOverride,
            _ => false
        };
    }

    public string? Validate(JsonElement value)
    {
        if (!MatchesType(value)) return $"Value must have type {Type}.";
        return _validate(value);
    }

    public string? ValidateOverride(SettingScope scope, JsonElement value, JsonElement inheritedValue)
    {
        if (!CanOverrideAt(scope))
            return "This setting does not allow overrides at this scope.";
        var requiresConstraint = OverrideBehavior is SettingOverrideBehavior.ConstrainedTenantOverride
            or SettingOverrideBehavior.ConstrainedTenantAndOrganizationOverride
            or SettingOverrideBehavior.ConstrainedHierarchicalOverride;
        if (requiresConstraint && _validateOverride is null)
            return "The inherited-value constraint for this setting is not configured.";
        return _validateOverride?.Invoke(value, inheritedValue);
    }

    private bool MatchesType(JsonElement value) => Type switch
    {
        SettingValueType.String => value.ValueKind is JsonValueKind.String or JsonValueKind.Null,
        SettingValueType.Integer => value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out _),
        SettingValueType.Decimal => value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out _),
        SettingValueType.Boolean => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
        _ => false
    };
}

public interface ISettingDefinitionCatalog
{
    IReadOnlyCollection<SettingDefinition> All { get; }
    bool TryGet(string key, out SettingDefinition definition);
}

public sealed class SettingDefinitionCatalog : ISettingDefinitionCatalog
{
    private static readonly IReadOnlySet<SettingScope> PlatformTenantAndOrganization =
        new HashSet<SettingScope> { SettingScope.Platform, SettingScope.Tenant, SettingScope.Organization };

    private static readonly IReadOnlySet<SettingScope> TenantAndOrganization =
        new HashSet<SettingScope> { SettingScope.Tenant, SettingScope.Organization };

    private static readonly IReadOnlySet<SettingScope> TenantOnly =
        new HashSet<SettingScope> { SettingScope.Tenant };

    private readonly IReadOnlyDictionary<string, SettingDefinition> _definitions;

    public SettingDefinitionCatalog()
    {
        var definitions = new[]
        {
            Define("regional.defaultLanguage", SettingValueType.String, PlatformTenantAndOrganization, "en",
                "Must be one of the enabled interface languages: en or ar.", SettingOverrideBehavior.TenantAndOrganizationOverride,
                value => OneOf(value, "en", "ar")),
            Define("regional.defaultTimeZone", SettingValueType.String, PlatformTenantAndOrganization, "UTC",
                "Must be a time-zone identifier recognized by the server.", SettingOverrideBehavior.TenantAndOrganizationOverride,
                ValidateTimeZone),
            Define("regional.defaultDateFormat", SettingValueType.String, PlatformTenantAndOrganization, "yyyy-MM-dd",
                "Must be one of yyyy-MM-dd, dd/MM/yyyy, or MM/dd/yyyy.", SettingOverrideBehavior.TenantAndOrganizationOverride,
                value => OneOf(value, "yyyy-MM-dd", "dd/MM/yyyy", "MM/dd/yyyy")),
            Define("regional.defaultTimeFormat", SettingValueType.String, PlatformTenantAndOrganization, "HH:mm",
                "Must be HH:mm or hh:mm tt.", SettingOverrideBehavior.TenantAndOrganizationOverride,
                value => OneOf(value, "HH:mm", "hh:mm tt")),
            Define("regional.firstDayOfWeek", SettingValueType.String, PlatformTenantAndOrganization, "monday",
                "Must be a weekday name from monday through sunday.", SettingOverrideBehavior.TenantAndOrganizationOverride,
                value => OneOf(value, "monday", "tuesday", "wednesday", "thursday", "friday", "saturday", "sunday")),
            Define<string?>("regional.defaultCurrency", SettingValueType.String,
                TenantAndOrganization, null,
                "May be null; otherwise it must match an active currency code for the tenant.",
                SettingOverrideBehavior.TenantAndOrganizationOverride, ValidateCurrencyCode),
            Define<string?>("workingCalendar.defaultCalendarId", SettingValueType.String,
                TenantAndOrganization, null,
                "May be null; otherwise it must be the ID of an existing Working Calendar in this Tenant.",
                SettingOverrideBehavior.TenantAndOrganizationOverride, ValidateWorkingCalendarId),
            Define(FiscalYearSetting.StartMonthKey, SettingValueType.Integer, TenantOnly,
                FiscalYearSetting.DefaultStartMonth,
                "Must be an integer month from 1 through 12. Fiscal years use this Tenant-level start month.",
                SettingOverrideBehavior.TenantOverride, FiscalYearCalendarService.ValidateStartMonth)
        };

        _definitions = definitions.ToDictionary(definition => definition.Key, StringComparer.Ordinal);
        foreach (var definition in _definitions.Values)
        {
            if (definition.SecurityClassification == SettingSecurityClassification.Secret)
                throw new InvalidOperationException($"Secret setting {definition.Key} cannot be registered in ordinary configuration storage.");
            var error = definition.Validate(definition.DefaultValue);
            if (error is not null) throw new InvalidOperationException($"Invalid default for {definition.Key}: {error}");
        }
    }

    public IReadOnlyCollection<SettingDefinition> All => _definitions.Values.ToArray();

    public bool TryGet(string key, out SettingDefinition definition) => _definitions.TryGetValue(key, out definition!);

    private static SettingDefinition Define<T>(
        string key,
        SettingValueType type,
        IReadOnlySet<SettingScope> scopes,
        T? defaultValue,
        string validationDescription,
        SettingOverrideBehavior behavior,
        Func<JsonElement, string?> validate,
        SettingSecurityClassification securityClassification = SettingSecurityClassification.Standard) =>
        new(key, type, scopes, JsonSerializer.SerializeToElement(defaultValue), validationDescription,
            behavior, securityClassification, validate);

    private static string? OneOf(JsonElement value, params string[] allowed)
    {
        var text = value.ValueKind == JsonValueKind.Null ? null : value.GetString();
        return text is null || !allowed.Contains(text, StringComparer.Ordinal)
            ? $"Value must be one of: {string.Join(", ", allowed)}."
            : null;
    }

    private static string? ValidateTimeZone(JsonElement value)
    {
        var id = value.GetString();
        if (string.IsNullOrWhiteSpace(id)) return "A time-zone identifier is required.";
        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(id);
            return null;
        }
        catch (TimeZoneNotFoundException)
        {
            return "The time-zone identifier is not recognized by this server.";
        }
        catch (InvalidTimeZoneException)
        {
            return "The time-zone identifier is invalid.";
        }
    }

    private static string? ValidateCurrencyCode(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Null) return null;
        var code = value.GetString();
        return code is { Length: 3 } && code.All(char.IsAsciiLetterUpper)
            ? null
            : "Currency must be selected from the tenant's active currency list.";
    }

    private static string? ValidateWorkingCalendarId(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Null) return null;
        return Guid.TryParse(value.GetString(), out var calendarId) && calendarId != Guid.Empty
            ? null
            : "Working Calendar ID must be a non-empty GUID or null to inherit the parent setting.";
    }

}
