using System.Data;
using System.Data.Common;
using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Organization.Domain;
using OrganizationEntity = NEXUS.Modules.Organization.Domain.Organization;

namespace NEXUS.Modules.Organization.Application.Settings;

/// <summary>
    /// SQL Server-backed configuration repository. Every query includes explicit scope, tenant and
    /// owner predicates; ConfigurationSetting intentionally has no ambient tenant query filter.
/// </summary>
public sealed class EfSettingsRepository : ISettingsRepository
{
    private static readonly HashSet<int> PersistenceErrorNumbers =
    [
        2, 53, 208, 207, 1205, 1222, 4060, 40197, 40501, 40613,
        10928, 10929, 11001, 10060, 10061, 18456
    ];

    private readonly DbContext _context;
    private readonly ISettingDefinitionCatalog _catalog;

    public EfSettingsRepository(DbContext context, ISettingDefinitionCatalog catalog)
    {
        _context = context;
        _catalog = catalog;
    }

    // A successful registration means the durable repository is selected. Actual SQL
    // availability is established by each operation and translated to HTTP 503 on failure.
    public bool IsAvailable => true;

    public async Task<IReadOnlyDictionary<string, StoredConfigurationValue>> ReadAsync(
        SettingsScopeIdentity identity,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await EnsureValidOwnerAsync(identity, cancellationToken);
            var rows = await ActiveRows(identity, tracking: false).ToListAsync(cancellationToken);
            return rows.ToDictionary(row => row.SettingKey, ToStored, StringComparer.Ordinal);
        }
        catch (Exception exception) when (IsPersistenceUnavailable(exception))
        {
            throw new SettingsPersistenceUnavailableException(
                "The durable settings store is unavailable or its schema has not been deployed.", exception);
        }
    }

    public async Task<SettingsStoreWriteResult> UpsertAsync(
        SettingsScopeIdentity identity,
        string key,
        JsonElement value,
        string? expectedVersion,
        string actorId,
        Func<StoredConfigurationValue?, StoredConfigurationValue, CancellationToken, Task> appendAudit,
        CancellationToken cancellationToken = default,
        Func<JsonElement?, JsonElement, CancellationToken, Task<string?>>? validateConfigurationChange = null)
    {
        var definition = GetWritableDefinition(identity.Scope, key, value);
        var expectedRowVersion = ParseVersion(expectedVersion);
        if (string.IsNullOrWhiteSpace(actorId)) throw new ArgumentException("An authenticated actor is required.", nameof(actorId));

        try
        {
            await EnsureValidOwnerAsync(identity, cancellationToken);
            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            var row = await ActiveRows(identity, tracking: true)
                .SingleOrDefaultAsync(setting => setting.SettingKey == key, cancellationToken);
            var previous = row is null ? null : ToStored(row);

            if (validateConfigurationChange is not null)
            {
                var validationError = await validateConfigurationChange(previous?.Value, value, cancellationToken);
                if (validationError is not null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return new SettingsStoreWriteResult(SettingsStoreWriteCode.ValidationFailed, Error: validationError);
                }
            }

            if (row is null)
            {
                if (expectedRowVersion is not null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return new SettingsStoreWriteResult(SettingsStoreWriteCode.ConcurrencyConflict);
                }

                row = new ConfigurationSetting
                {
                    Id = Guid.NewGuid(),
                    Scope = ScopeName(identity.Scope),
                    TenantId = identity.TenantId,
                    OwnerId = identity.OwnerId,
                    OrganizationId = identity.Scope == SettingScope.Organization ? identity.OwnerId : null,
                    SettingKey = key,
                    ValueType = definition.Type.ToString(),
                    ValueJson = value.GetRawText(),
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = actorId
                };
                _context.Set<ConfigurationSetting>().Add(row);
            }
            else
            {
                if (expectedRowVersion is null || !expectedRowVersion.AsSpan().SequenceEqual(row.RowVersion))
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return new SettingsStoreWriteResult(SettingsStoreWriteCode.ConcurrencyConflict);
                }

                row.ValueType = definition.Type.ToString();
                row.ValueJson = value.GetRawText();
                row.UpdatedAt = DateTime.UtcNow;
                row.UpdatedBy = actorId;
            }

            await _context.SaveChangesAsync(cancellationToken);
            var current = ToStored(row);
            await appendAudit(previous, current, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new SettingsStoreWriteResult(SettingsStoreWriteCode.Saved, current);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new SettingsStoreWriteResult(SettingsStoreWriteCode.ConcurrencyConflict);
        }
        catch (Exception exception) when (IsUniqueConstraintViolation(exception))
        {
            // A competing first insert won the unique active-key race.
            return new SettingsStoreWriteResult(SettingsStoreWriteCode.ConcurrencyConflict);
        }
        catch (Exception exception) when (IsPersistenceUnavailable(exception))
        {
            return new SettingsStoreWriteResult(SettingsStoreWriteCode.PersistenceUnavailable);
        }
    }

    public async Task<SettingsStoreWriteResult> ResetAsync(
        SettingsScopeIdentity identity,
        string key,
        string? expectedVersion,
        string actorId,
        Func<StoredConfigurationValue, CancellationToken, Task> appendAudit,
        CancellationToken cancellationToken = default,
        Func<JsonElement?, JsonElement, CancellationToken, Task<string?>>? validateConfigurationChange = null)
    {
        EnsureWritableDefinition(identity.Scope, key);
        var expectedRowVersion = ParseVersion(expectedVersion);
        if (expectedRowVersion is null) return new SettingsStoreWriteResult(SettingsStoreWriteCode.ConcurrencyConflict);
        if (string.IsNullOrWhiteSpace(actorId)) throw new ArgumentException("An authenticated actor is required.", nameof(actorId));

        try
        {
            await EnsureValidOwnerAsync(identity, cancellationToken);
            await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            var row = await ActiveRows(identity, tracking: true)
                .SingleOrDefaultAsync(setting => setting.SettingKey == key, cancellationToken);
            if (row is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new SettingsStoreWriteResult(SettingsStoreWriteCode.NotFound);
            }

            if (!expectedRowVersion.AsSpan().SequenceEqual(row.RowVersion))
            {
                await transaction.RollbackAsync(cancellationToken);
                return new SettingsStoreWriteResult(SettingsStoreWriteCode.ConcurrencyConflict);
            }

            var previous = ToStored(row);
            var definition = EnsureWritableDefinition(identity.Scope, key);
            if (validateConfigurationChange is not null)
            {
                var validationError = await validateConfigurationChange(previous.Value, definition.DefaultValue, cancellationToken);
                if (validationError is not null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return new SettingsStoreWriteResult(SettingsStoreWriteCode.ValidationFailed, Error: validationError);
                }
            }

            row.DeletedAt = DateTime.UtcNow;
            row.DeletedBy = actorId;
            row.UpdatedAt = row.DeletedAt;
            row.UpdatedBy = actorId;

            await _context.SaveChangesAsync(cancellationToken);
            await appendAudit(previous, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new SettingsStoreWriteResult(SettingsStoreWriteCode.Reset);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new SettingsStoreWriteResult(SettingsStoreWriteCode.ConcurrencyConflict);
        }
        catch (Exception exception) when (IsPersistenceUnavailable(exception))
        {
            return new SettingsStoreWriteResult(SettingsStoreWriteCode.PersistenceUnavailable);
        }
    }

    private IQueryable<ConfigurationSetting> ActiveRows(SettingsScopeIdentity identity, bool tracking)
    {
        IQueryable<ConfigurationSetting> query = _context.Set<ConfigurationSetting>().IgnoreQueryFilters();
        query = tracking ? query : query.AsNoTracking();
        return query.Where(setting =>
            setting.Scope == ScopeName(identity.Scope)
            && setting.TenantId == identity.TenantId
            && setting.OwnerId == identity.OwnerId
            && setting.DeletedAt == null);
    }

    private SettingDefinition GetWritableDefinition(SettingScope scope, string key, JsonElement value)
    {
        var definition = EnsureWritableDefinition(scope, key);
        var error = definition.Validate(value);
        if (error is not null) throw new ArgumentException(error, nameof(value));
        if (definition.SecurityClassification == SettingSecurityClassification.Secret)
            throw new ArgumentException("Secret values cannot be stored in the configuration table.", nameof(value));
        return definition;
    }

    private SettingDefinition EnsureWritableDefinition(SettingScope scope, string key)
    {
        if (!_catalog.TryGet(key, out var definition)
            || !definition.AllowedScopes.Contains(scope)
            || !definition.CanOverrideAt(scope))
            throw new ArgumentException("The setting is not defined for the requested scope.", nameof(key));
        return definition;
    }

    private async Task EnsureValidOwnerAsync(SettingsScopeIdentity identity, CancellationToken cancellationToken)
    {
        switch (identity.Scope)
        {
            case SettingScope.Platform when identity.TenantId is null && identity.OwnerId is null:
                return;
            case SettingScope.Tenant when identity.TenantId is { } tenantId
                && tenantId != Guid.Empty && identity.OwnerId == tenantId:
                return;
            case SettingScope.Organization when identity.TenantId is { } organizationTenantId
                && organizationTenantId != Guid.Empty && identity.OwnerId is { } organizationId
                && organizationId != Guid.Empty:
                var belongsToTenant = await _context.Set<OrganizationEntity>()
                    .IgnoreQueryFilters()
                    .AnyAsync(organization => organization.Id == organizationId
                        && organization.TenantId == organizationTenantId && !organization.IsDeleted,
                        cancellationToken);
                if (!belongsToTenant) throw new SettingsOwnerNotFoundException();
                return;
            case SettingScope.Platform:
                throw new ArgumentException("Platform settings cannot have tenant or owner identifiers.");
            case SettingScope.Tenant:
                throw new ArgumentException("Tenant settings require matching tenant and owner identifiers.");
            case SettingScope.Organization:
                throw new ArgumentException("Organization settings require authenticated tenant and organization identifiers.");
            default:
                throw new NotSupportedException("Project and user setting persistence is not enabled in this delivery.");
        }
    }

    private static StoredConfigurationValue ToStored(ConfigurationSetting row)
    {
        using var document = JsonDocument.Parse(row.ValueJson);
        var version = row.RowVersion.Length == 0 ? string.Empty : Convert.ToBase64String(row.RowVersion);
        return new StoredConfigurationValue(row.Id, row.SettingKey, document.RootElement.Clone(), version);
    }

    private static byte[]? ParseVersion(string? version)
    {
        if (string.IsNullOrWhiteSpace(version)) return null;
        try
        {
            var bytes = Convert.FromBase64String(version);
            if (bytes.Length != 8) throw new ArgumentException("Expected version must be an 8-byte SQL Server rowversion.", nameof(version));
            return bytes;
        }
        catch (FormatException exception)
        {
            throw new ArgumentException("Expected version must be valid base64.", nameof(version), exception);
        }
    }

    private static bool IsUniqueConstraintViolation(Exception exception) =>
        exception is DbException && ProviderErrorNumbers(exception).Any(number => number is 2601 or 2627)
        || exception.InnerException is not null && IsUniqueConstraintViolation(exception.InnerException);

    private static bool IsPersistenceUnavailable(Exception exception) =>
        exception is TimeoutException
        || exception is DbException && ProviderErrorNumbers(exception).Any(number => PersistenceErrorNumbers.Contains(number))
        || exception.InnerException is not null && IsPersistenceUnavailable(exception.InnerException);

    private static IEnumerable<int> ProviderErrorNumbers(Exception exception)
    {
        if (exception is not DbException databaseException) return Array.Empty<int>();
        var numberProperty = databaseException.GetType().GetProperty("Number", BindingFlags.Public | BindingFlags.Instance);
        if (numberProperty?.GetValue(databaseException) is int number) return [number];
        return Array.Empty<int>();
    }

    private static string ScopeName(SettingScope scope) => scope switch
    {
        SettingScope.Platform => "Platform",
        SettingScope.Tenant => "Tenant",
        SettingScope.Organization => "Organization",
        _ => throw new NotSupportedException("Project and user setting persistence is not enabled in this delivery.")
    };
}
