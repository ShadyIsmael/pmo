using System.Text.Json;

namespace NEXUS.Modules.Organization.Application.Settings;

public enum SettingsStoreWriteCode
{
    Saved,
    Reset,
    NotFound,
    ConcurrencyConflict,
    PersistenceUnavailable,
    ValidationFailed
}

public sealed record SettingsStoreWriteResult(
    SettingsStoreWriteCode Code,
    StoredConfigurationValue? Value = null,
    string? Error = null);

public sealed record SettingsScopeIdentity(SettingScope Scope, Guid? TenantId, Guid? OwnerId)
{
    public static SettingsScopeIdentity Platform() => new(SettingScope.Platform, null, null);

    public static SettingsScopeIdentity Tenant(Guid tenantId) => tenantId == Guid.Empty
        ? throw new ArgumentException("A trusted tenant identifier is required.", nameof(tenantId))
        : new SettingsScopeIdentity(SettingScope.Tenant, tenantId, tenantId);

    public static SettingsScopeIdentity Organization(Guid tenantId, Guid organizationId) =>
        tenantId == Guid.Empty || organizationId == Guid.Empty
            ? throw new ArgumentException("Trusted tenant and organization identifiers are required.")
            : new SettingsScopeIdentity(SettingScope.Organization, tenantId, organizationId);
}

/// <summary>
/// Persistence contract for platform, tenant and organization settings. Implementations enforce the unique
/// active (scope, owner, key) key and expected rowversion in the same transaction as the audit callback.
/// A null expected version is valid only when no active row exists; existing rows require an exact rowversion match.
/// </summary>
public interface ISettingsRepository
{
    bool IsAvailable { get; }

    Task<IReadOnlyDictionary<string, StoredConfigurationValue>> ReadAsync(
        SettingsScopeIdentity identity,
        CancellationToken cancellationToken = default);

    Task<SettingsStoreWriteResult> UpsertAsync(
        SettingsScopeIdentity identity,
        string key,
        JsonElement value,
        string? expectedVersion,
        string actorId,
        Func<StoredConfigurationValue?, StoredConfigurationValue, CancellationToken, Task> appendAudit,
        CancellationToken cancellationToken = default,
        Func<JsonElement?, JsonElement, CancellationToken, Task<string?>>? validateConfigurationChange = null);

    Task<SettingsStoreWriteResult> ResetAsync(
        SettingsScopeIdentity identity,
        string key,
        string? expectedVersion,
        string actorId,
        Func<StoredConfigurationValue, CancellationToken, Task> appendAudit,
        CancellationToken cancellationToken = default,
        Func<JsonElement?, JsonElement, CancellationToken, Task<string?>>? validateConfigurationChange = null);
}

/// <summary>Validates whether a Tenant fiscal calendar can change without realigning persisted fiscal records.</summary>
public interface IFiscalYearConfigurationChangeGuard
{
    Task<string?> ValidateChangeAsync(Guid tenantId, int previousStartMonth, int newStartMonth, CancellationToken cancellationToken = default);
}

public sealed class SettingsOwnerNotFoundException : Exception
{
    public SettingsOwnerNotFoundException() : base("The requested setting owner does not belong to the authenticated tenant.") { }
}

public sealed class SettingsPersistenceUnavailableException : Exception
{
    public SettingsPersistenceUnavailableException(string message, Exception? innerException = null)
        : base(message, innerException) { }
}
