using System.Text.Json;

namespace NEXUS.Modules.Organization.Application.Settings;

public sealed record EffectiveSettingDto(
    string Key,
    SettingValueType Type,
    JsonElement DefaultValue,
    JsonElement Value,
    string ValueSource,
    bool IsInherited,
    bool CanOverride,
    string? Version,
    string ValidationDescription,
    IReadOnlyCollection<SettingScope> AllowedScopes,
    SettingOverrideBehavior OverrideBehavior,
    SettingSecurityClassification SecurityClassification);

public sealed record SettingsReadResponse(
    SettingScope Scope,
    bool PersistenceAvailable,
    IReadOnlyList<EffectiveSettingDto> Settings,
    Guid? OwnerId = null);

public sealed class UpdateSettingRequest
{
    public JsonElement Value { get; init; }
    public string? ExpectedVersion { get; init; }
}

public sealed class ResetSettingRequest
{
    public string? ExpectedVersion { get; init; }
}

public sealed record SettingsAccessResponse(
    bool CanReadTenant,
    bool CanWriteTenant,
    bool CanReadPlatform,
    bool CanWritePlatform);

public enum SettingsOperationCode
{
    Success,
    UnknownSetting,
    ScopeNotAllowed,
    ValidationFailed,
    TenantContextMissing,
    AccessDenied,
    ConcurrencyConflict,
    NotFound,
    PersistenceUnavailable,
    FiscalYearChangeBlocked
}

public sealed record SettingsOperationResult<T>(
    SettingsOperationCode Code,
    T? Value = default,
    string? Error = null,
    IReadOnlyDictionary<string, string[]>? ValidationErrors = null)
{
    public bool Succeeded => Code == SettingsOperationCode.Success;
}

public interface IConfigurationSettingsService
{
    Task<SettingsReadResponse> GetPlatformAsync(CancellationToken cancellationToken = default);
    Task<SettingsOperationResult<SettingsReadResponse>> GetTenantAsync(CancellationToken cancellationToken = default);
    Task<SettingsOperationResult<SettingsReadResponse>> GetOrganizationAsync(Guid organizationId, CancellationToken cancellationToken = default);
    Task<SettingsOperationResult<SettingsReadResponse>> GetTenantRuntimeAsync(CancellationToken cancellationToken = default);
    Task<SettingsOperationResult<EffectiveSettingDto>> UpdatePlatformAsync(string key, UpdateSettingRequest request, CancellationToken cancellationToken = default);
    Task<SettingsOperationResult<EffectiveSettingDto>> UpdateTenantAsync(string key, UpdateSettingRequest request, CancellationToken cancellationToken = default);
    Task<SettingsOperationResult<EffectiveSettingDto>> UpdateOrganizationAsync(Guid organizationId, string key, UpdateSettingRequest request, CancellationToken cancellationToken = default);
    Task<SettingsOperationResult<EffectiveSettingDto>> ResetPlatformAsync(string key, ResetSettingRequest request, CancellationToken cancellationToken = default);
    Task<SettingsOperationResult<EffectiveSettingDto>> ResetTenantAsync(string key, ResetSettingRequest request, CancellationToken cancellationToken = default);
    Task<SettingsOperationResult<EffectiveSettingDto>> ResetOrganizationAsync(Guid organizationId, string key, ResetSettingRequest request, CancellationToken cancellationToken = default);
}
