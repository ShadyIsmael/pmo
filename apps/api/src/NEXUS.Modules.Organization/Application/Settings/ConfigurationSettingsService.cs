using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.Organization.Application;

namespace NEXUS.Modules.Organization.Application.Settings;

public sealed class ConfigurationSettingsService : IConfigurationSettingsService
{
    private readonly ISettingDefinitionCatalog _catalog;
    private readonly ISettingsRepository _repository;
    private readonly EffectiveConfigurationResolver _resolver;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly IAuditService _auditService;
    private readonly ICurrencyService _currencyService;
    private readonly IOrganizationSettingsAccessService _organizationAccess;
    private readonly IFiscalYearConfigurationChangeGuard? _fiscalYearChangeGuard;

    public ConfigurationSettingsService(
        ISettingDefinitionCatalog catalog,
        ISettingsRepository repository,
        EffectiveConfigurationResolver resolver,
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        IAuditService auditService,
        ICurrencyService currencyService,
        IOrganizationSettingsAccessService organizationAccess,
        IFiscalYearConfigurationChangeGuard? fiscalYearChangeGuard = null)
    {
        _catalog = catalog;
        _repository = repository;
        _resolver = resolver;
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _auditService = auditService;
        _currencyService = currencyService;
        _organizationAccess = organizationAccess;
        _fiscalYearChangeGuard = fiscalYearChangeGuard;
    }

    public async Task<SettingsReadResponse> GetPlatformAsync(CancellationToken cancellationToken = default)
    {
        var platform = await _repository.ReadAsync(SettingsScopeIdentity.Platform(), cancellationToken);
        return new SettingsReadResponse(SettingScope.Platform, _repository.IsAvailable,
            _resolver.ResolvePlatform(_catalog, platform).Select(ToDto).ToArray());
    }

    public async Task<SettingsOperationResult<SettingsReadResponse>> GetTenantAsync(CancellationToken cancellationToken = default)
    {
        if (!TryGetTenantId(out var tenantId))
            return Failure<SettingsReadResponse>(SettingsOperationCode.TenantContextMissing, "A trusted tenant context is required.");
        if (!await IsAuthorizedForSettingsAsync("Settings.View"))
            return Failure<SettingsReadResponse>(SettingsOperationCode.AccessDenied, "Settings.View permission is required.");

        return await ReadTenantSettingsAsync(tenantId, cancellationToken);
    }

    private async Task<SettingsOperationResult<SettingsReadResponse>> ReadTenantSettingsAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {

        var platform = await _repository.ReadAsync(SettingsScopeIdentity.Platform(), cancellationToken);
        var tenant = await _repository.ReadAsync(SettingsScopeIdentity.Tenant(tenantId), cancellationToken);
        var response = new SettingsReadResponse(SettingScope.Tenant, _repository.IsAvailable,
            _resolver.ResolveTenant(_catalog, platform, tenant).Select(ToDto).ToArray(), tenantId);
        return Success(response);
    }

    public async Task<SettingsOperationResult<SettingsReadResponse>> GetOrganizationAsync(
        Guid organizationId, CancellationToken cancellationToken = default)
    {
        if (!TryGetTenantId(out var tenantId))
            return Failure<SettingsReadResponse>(SettingsOperationCode.TenantContextMissing, "A trusted tenant context is required.");
        if (!await IsAuthorizedForSettingsAsync("Settings.View"))
            return Failure<SettingsReadResponse>(SettingsOperationCode.AccessDenied, "Settings.View permission is required.");
        if (!await _organizationAccess.HasAccessAsync(organizationId, manage: false, cancellationToken: cancellationToken))
            return Failure<SettingsReadResponse>(SettingsOperationCode.AccessDenied, "The authenticated user has no settings access to this organization.");

        var identity = SettingsScopeIdentity.Organization(tenantId, organizationId);
        var platform = await _repository.ReadAsync(SettingsScopeIdentity.Platform(), cancellationToken);
        var tenant = await _repository.ReadAsync(SettingsScopeIdentity.Tenant(tenantId), cancellationToken);
        var organization = await _repository.ReadAsync(identity, cancellationToken);
        var response = new SettingsReadResponse(SettingScope.Organization, _repository.IsAvailable,
            _resolver.ResolveOrganization(_catalog, platform, tenant, organization).Select(ToDto).ToArray(), organizationId);
        return Success(response);
    }

    public async Task<SettingsOperationResult<SettingsReadResponse>> GetTenantRuntimeAsync(CancellationToken cancellationToken = default)
    {
        if (!TryGetTenantId(out var tenantId))
            return Failure<SettingsReadResponse>(SettingsOperationCode.TenantContextMissing, "A trusted tenant context is required.");
        var result = await ReadTenantSettingsAsync(tenantId, cancellationToken);
        if (!result.Succeeded || result.Value is null)
            return new SettingsOperationResult<SettingsReadResponse>(result.Code, Error: result.Error,
                ValidationErrors: result.ValidationErrors);

        return Success(result.Value with
        {
            Settings = result.Value.Settings.Where(setting => setting.Key.StartsWith("regional.", StringComparison.Ordinal)).ToArray()
        });
    }

    public Task<SettingsOperationResult<EffectiveSettingDto>> UpdatePlatformAsync(
        string key, UpdateSettingRequest request, CancellationToken cancellationToken = default) =>
        UpdateAsync(SettingScope.Platform, null, key, request, cancellationToken);

    public Task<SettingsOperationResult<EffectiveSettingDto>> UpdateTenantAsync(
        string key, UpdateSettingRequest request, CancellationToken cancellationToken = default) =>
        UpdateAsync(SettingScope.Tenant, null, key, request, cancellationToken);

    public Task<SettingsOperationResult<EffectiveSettingDto>> UpdateOrganizationAsync(
        Guid organizationId, string key, UpdateSettingRequest request, CancellationToken cancellationToken = default) =>
        UpdateAsync(SettingScope.Organization, organizationId, key, request, cancellationToken);

    public Task<SettingsOperationResult<EffectiveSettingDto>> ResetPlatformAsync(
        string key, ResetSettingRequest request, CancellationToken cancellationToken = default) =>
        ResetAsync(SettingScope.Platform, null, key, request, cancellationToken);

    public Task<SettingsOperationResult<EffectiveSettingDto>> ResetTenantAsync(
        string key, ResetSettingRequest request, CancellationToken cancellationToken = default) =>
        ResetAsync(SettingScope.Tenant, null, key, request, cancellationToken);

    public Task<SettingsOperationResult<EffectiveSettingDto>> ResetOrganizationAsync(
        Guid organizationId, string key, ResetSettingRequest request, CancellationToken cancellationToken = default) =>
        ResetAsync(SettingScope.Organization, organizationId, key, request, cancellationToken);

    private async Task<SettingsOperationResult<EffectiveSettingDto>> UpdateAsync(
        SettingScope scope,
        Guid? organizationId,
        string key,
        UpdateSettingRequest request,
        CancellationToken cancellationToken)
    {
        if (!_catalog.TryGet(key, out var definition))
            return Failure<EffectiveSettingDto>(SettingsOperationCode.UnknownSetting, "Unknown setting key.");
        if (!definition.CanOverrideAt(scope))
            return Failure<EffectiveSettingDto>(SettingsOperationCode.ScopeNotAllowed, "This setting cannot be changed at the requested scope.");
        if (definition.SecurityClassification == SettingSecurityClassification.Secret)
            return Failure<EffectiveSettingDto>(SettingsOperationCode.ScopeNotAllowed, "Secret values cannot be stored as ordinary settings.");
        if (!TryGetTenantId(out var auditTenantId))
            return Failure<EffectiveSettingDto>(SettingsOperationCode.TenantContextMissing, "A trusted authenticated tenant context is required for audited writes.");
        if (scope is SettingScope.Tenant or SettingScope.Organization
            && !await IsAuthorizedForSettingsAsync("Settings.ManageTenantConfiguration"))
            return Failure<EffectiveSettingDto>(SettingsOperationCode.AccessDenied, "Settings.ManageTenantConfiguration permission is required.");
        if (scope == SettingScope.Organization
            && (organizationId is null || !await _organizationAccess.HasAccessAsync(organizationId.Value, manage: true, cancellationToken: cancellationToken)))
            return Failure<EffectiveSettingDto>(SettingsOperationCode.AccessDenied, "Organization settings management access is required.");

        var validationError = definition.Validate(request.Value);
        if (validationError is not null) return ValidationFailure<EffectiveSettingDto>("value", validationError);
        var versionError = ValidateVersion(request.ExpectedVersion);
        if (versionError is not null) return ValidationFailure<EffectiveSettingDto>("expectedVersion", versionError);

        var platformValues = scope is SettingScope.Tenant or SettingScope.Organization
            ? await _repository.ReadAsync(SettingsScopeIdentity.Platform(), cancellationToken)
            : new Dictionary<string, StoredConfigurationValue>();
        var inheritedValue = definition.DefaultValue;
        if (scope is SettingScope.Tenant or SettingScope.Organization)
        {
            inheritedValue = platformValues.TryGetValue(key, out var platformStored)
                ? platformStored.Value
                : definition.DefaultValue;
            if (scope == SettingScope.Organization)
            {
                var tenantValues = await _repository.ReadAsync(SettingsScopeIdentity.Tenant(auditTenantId), cancellationToken);
                if (tenantValues.TryGetValue(key, out var tenantStored)) inheritedValue = tenantStored.Value;
            }
            if (definition.OverrideBehavior is SettingOverrideBehavior.ConstrainedTenantOverride
                or SettingOverrideBehavior.ConstrainedTenantAndOrganizationOverride
                or SettingOverrideBehavior.ConstrainedHierarchicalOverride)
            {
                var overrideError = definition.ValidateOverride(scope, request.Value, inheritedValue);
                if (overrideError is not null) return ValidationFailure<EffectiveSettingDto>("value", overrideError);
            }
        }

        if (scope is SettingScope.Tenant or SettingScope.Organization
            && key == "regional.defaultCurrency" && request.Value.ValueKind == JsonValueKind.String)
        {
            var code = request.Value.GetString();
            var activeCurrencies = await _currencyService.GetAllAsync(cancellationToken);
            if (code is null || !activeCurrencies.Any(currency =>
                    string.Equals(currency.Code, code, StringComparison.OrdinalIgnoreCase)))
                return ValidationFailure<EffectiveSettingDto>("value", "Choose an active currency configured for this tenant.");
        }

        var identity = CreateIdentity(scope, auditTenantId, organizationId);
        var actor = CurrentActor();
        if (actor.UserId is null || actor.UserId == Guid.Empty)
            return Failure<EffectiveSettingDto>(SettingsOperationCode.TenantContextMissing, "A trusted authenticated actor is required for audited writes.");
        Func<JsonElement?, JsonElement, CancellationToken, Task<string?>>? validateConfigurationChange = null;
        if (scope == SettingScope.Tenant && key == FiscalYearSetting.StartMonthKey)
            validateConfigurationChange = (previous, current, token) => ValidateFiscalYearChangeAsync(auditTenantId, previous, current, token);
        var storeResult = await _repository.UpsertAsync(identity, key, request.Value, request.ExpectedVersion,
            actor.UserId.Value.ToString(),
            (previous, current, token) => AppendAuditAsync(identity, auditTenantId, actor, definition, previous, current, "Updated", token),
            cancellationToken, validateConfigurationChange);
        if (storeResult.Code == SettingsStoreWriteCode.ConcurrencyConflict)
            return Failure<EffectiveSettingDto>(SettingsOperationCode.ConcurrencyConflict, "The setting changed after it was loaded. Reload and try again.");
        if (storeResult.Code == SettingsStoreWriteCode.ValidationFailed)
            return Failure<EffectiveSettingDto>(SettingsOperationCode.FiscalYearChangeBlocked, storeResult.Error ?? "The fiscal calendar cannot be changed while fiscal-year records exist.");
        if (storeResult.Code == SettingsStoreWriteCode.PersistenceUnavailable)
            return Failure<EffectiveSettingDto>(SettingsOperationCode.PersistenceUnavailable, "The durable settings store is unavailable or its schema has not been deployed.");
        if (storeResult.Code != SettingsStoreWriteCode.Saved || storeResult.Value is null)
            return Failure<EffectiveSettingDto>(SettingsOperationCode.NotFound, "The setting could not be saved.");

        var source = scope switch
        {
            SettingScope.Platform => SettingValueSource.PlatformOverride,
            SettingScope.Tenant => SettingValueSource.TenantOverride,
            SettingScope.Organization => SettingValueSource.OrganizationOverride,
            _ => throw new NotSupportedException()
        };
        return Success(ToDto(new EffectiveSettingValue(definition, storeResult.Value.Value, source,
            false, definition.CanOverrideAt(scope), storeResult.Value.Version)));
    }

    private async Task<SettingsOperationResult<EffectiveSettingDto>> ResetAsync(
        SettingScope scope,
        Guid? organizationId,
        string key,
        ResetSettingRequest request,
        CancellationToken cancellationToken)
    {
        if (!_catalog.TryGet(key, out var definition))
            return Failure<EffectiveSettingDto>(SettingsOperationCode.UnknownSetting, "Unknown setting key.");
        if (!definition.CanOverrideAt(scope))
            return Failure<EffectiveSettingDto>(SettingsOperationCode.ScopeNotAllowed, "This setting cannot be reset at the requested scope.");
        if (!TryGetTenantId(out var auditTenantId))
            return Failure<EffectiveSettingDto>(SettingsOperationCode.TenantContextMissing, "A trusted authenticated tenant context is required for audited writes.");
        if (scope is SettingScope.Tenant or SettingScope.Organization
            && !await IsAuthorizedForSettingsAsync("Settings.ManageTenantConfiguration"))
            return Failure<EffectiveSettingDto>(SettingsOperationCode.AccessDenied, "Settings.ManageTenantConfiguration permission is required.");
        if (scope == SettingScope.Organization
            && (organizationId is null || !await _organizationAccess.HasAccessAsync(organizationId.Value, manage: true, cancellationToken: cancellationToken)))
            return Failure<EffectiveSettingDto>(SettingsOperationCode.AccessDenied, "Organization settings management access is required.");
        var versionError = ValidateVersion(request.ExpectedVersion);
        if (versionError is not null) return ValidationFailure<EffectiveSettingDto>("expectedVersion", versionError);

        var identity = CreateIdentity(scope, auditTenantId, organizationId);
        var actor = CurrentActor();
        if (actor.UserId is null || actor.UserId == Guid.Empty)
            return Failure<EffectiveSettingDto>(SettingsOperationCode.TenantContextMissing, "A trusted authenticated actor is required for audited writes.");
        Func<JsonElement?, JsonElement, CancellationToken, Task<string?>>? validateConfigurationChange = null;
        if (scope == SettingScope.Tenant && key == FiscalYearSetting.StartMonthKey)
            validateConfigurationChange = (previous, current, token) => ValidateFiscalYearChangeAsync(auditTenantId, previous, current, token);
        var storeResult = await _repository.ResetAsync(identity, key, request.ExpectedVersion,
            actor.UserId.Value.ToString(),
            (previous, token) => AppendAuditAsync(identity, auditTenantId, actor, definition, previous, null, "Reset", token),
            cancellationToken, validateConfigurationChange);
        if (storeResult.Code == SettingsStoreWriteCode.ConcurrencyConflict)
            return Failure<EffectiveSettingDto>(SettingsOperationCode.ConcurrencyConflict, "The setting changed after it was loaded. Reload and try again.");
        if (storeResult.Code == SettingsStoreWriteCode.ValidationFailed)
            return Failure<EffectiveSettingDto>(SettingsOperationCode.FiscalYearChangeBlocked, storeResult.Error ?? "The fiscal calendar cannot be changed while fiscal-year records exist.");
        if (storeResult.Code == SettingsStoreWriteCode.PersistenceUnavailable)
            return Failure<EffectiveSettingDto>(SettingsOperationCode.PersistenceUnavailable, "The durable settings store is unavailable or its schema has not been deployed.");
        if (storeResult.Code == SettingsStoreWriteCode.NotFound)
            return Failure<EffectiveSettingDto>(SettingsOperationCode.NotFound, "No override exists at this scope.");
        if (storeResult.Code != SettingsStoreWriteCode.Reset)
            return Failure<EffectiveSettingDto>(SettingsOperationCode.NotFound, "The setting override could not be reset.");

        var platformValues = await _repository.ReadAsync(SettingsScopeIdentity.Platform(), cancellationToken);
        IReadOnlyList<EffectiveSettingValue> resolved;
        if (scope == SettingScope.Platform)
            resolved = _resolver.ResolvePlatform(_catalog, platformValues);
        else
        {
            var tenantValues = await _repository.ReadAsync(SettingsScopeIdentity.Tenant(auditTenantId), cancellationToken);
            if (scope == SettingScope.Tenant)
                resolved = _resolver.ResolveTenant(_catalog, platformValues, tenantValues);
            else
            {
                var organizationValues = await _repository.ReadAsync(identity, cancellationToken);
                resolved = _resolver.ResolveOrganization(_catalog, platformValues, tenantValues, organizationValues);
            }
        }
        return Success(ToDto(resolved.Single(item => item.Definition.Key == key)));
    }

    private async Task AppendAuditAsync(
        SettingsScopeIdentity identity,
        Guid auditTenantId,
        ActorContext actor,
        SettingDefinition definition,
        StoredConfigurationValue? previous,
        StoredConfigurationValue? current,
        string action,
        CancellationToken cancellationToken)
    {
        if (definition.Key == FiscalYearSetting.StartMonthKey)
        {
            var previousMonth = ReadMonth(previous?.Value) ?? FiscalYearSetting.DefaultStartMonth;
            var newMonth = current is null
                ? FiscalYearSetting.DefaultStartMonth
                : ReadMonth(current.Value) ?? FiscalYearSetting.DefaultStartMonth;
            if (previousMonth == newMonth) return;

            var fiscalYearEntry = new AuditEntry
            {
                TenantId = auditTenantId,
                EntityType = "TenantFiscalYearConfiguration",
                EntityId = auditTenantId,
                Action = "FiscalYearStartMonthChanged",
                ChangedFields = "FiscalYearStartMonth",
                Description = $"Tenant fiscal year start month changed from {previousMonth} to {newMonth}.",
                OriginalValues = JsonSerializer.Serialize(new { tenantId = auditTenantId, fiscalYearStartMonth = previousMonth }),
                NewValues = JsonSerializer.Serialize(new { tenantId = auditTenantId, fiscalYearStartMonth = newMonth }),
                Changes = JsonSerializer.Serialize(new { fiscalYearStartMonth = new { from = previousMonth, to = newMonth } }),
                Source = AuditSources.User,
                CorrelationId = _httpContextAccessor.HttpContext?.Request.Headers["X-Correlation-Id"].FirstOrDefault()
                    ?? _httpContextAccessor.HttpContext?.TraceIdentifier,
                UserId = actor.UserId,
                UserName = actor.UserName,
                CreatedBy = actor.UserId?.ToString() ?? actor.UserName ?? "authenticated-user",
                Timestamp = DateTime.UtcNow
            };
            var fiscalAuditResult = await _auditService.AppendAsync(fiscalYearEntry, cancellationToken);
            if (fiscalAuditResult.IsFailure)
                throw new InvalidOperationException(fiscalAuditResult.Error?.Message ?? "The fiscal year configuration audit event could not be appended.");
            return;
        }

        var entry = new AuditEntry
        {
            TenantId = auditTenantId,
            EntityType = identity.Scope switch
            {
                SettingScope.Platform => "PlatformSetting",
                SettingScope.Tenant => "TenantSetting",
                SettingScope.Organization => "OrganizationSetting",
                _ => "ConfigurationSetting"
            },
            EntityId = identity.OwnerId ?? current?.Id ?? previous?.Id ?? Guid.Empty,
            Action = action,
            ChangedFields = "Value",
            Description = $"{identity.Scope} setting {definition.Key} {action.ToLowerInvariant()}.",
            OriginalValues = previous is null ? null : JsonSerializer.Serialize(new
            {
                key = definition.Key,
                scope = identity.Scope.ToString(),
                tenantId = identity.TenantId,
                ownerId = identity.OwnerId,
                value = previous.Value
            }),
            NewValues = current is null ? null : JsonSerializer.Serialize(new
            {
                key = definition.Key,
                scope = identity.Scope.ToString(),
                tenantId = identity.TenantId,
                ownerId = identity.OwnerId,
                value = current.Value
            }),
            Changes = JsonSerializer.Serialize(new
            {
                key = definition.Key,
                scope = identity.Scope.ToString(),
                tenantId = identity.TenantId,
                ownerId = identity.OwnerId,
                from = previous?.Value,
                to = current?.Value
            }),
            Source = AuditSources.User,
            CorrelationId = _httpContextAccessor.HttpContext?.Request.Headers["X-Correlation-Id"].FirstOrDefault()
                ?? _httpContextAccessor.HttpContext?.TraceIdentifier,
            UserId = actor.UserId,
            UserName = actor.UserName,
            CreatedBy = actor.UserId?.ToString() ?? actor.UserName ?? "authenticated-user"
        };
        var result = await _auditService.AppendAsync(entry, cancellationToken);
        if (result.IsFailure) throw new InvalidOperationException(result.Error?.Message ?? "The settings audit event could not be appended.");
    }

    private async Task<string?> ValidateFiscalYearChangeAsync(
        Guid tenantId,
        JsonElement? previous,
        JsonElement next,
        CancellationToken cancellationToken)
    {
        var previousMonth = previous is { } previousValue && previousValue.TryGetInt32(out var storedMonth)
            ? storedMonth
            : FiscalYearSetting.DefaultStartMonth;
        if (!next.TryGetInt32(out var newMonth))
            return "Fiscal year start month must be an integer from 1 through 12.";
        if (previousMonth == newMonth) return null;
        if (_fiscalYearChangeGuard is null)
            return "Fiscal-year history protection is unavailable. Contact an administrator before changing this setting.";
        return await _fiscalYearChangeGuard.ValidateChangeAsync(tenantId, previousMonth, newMonth, cancellationToken);
    }

    private static int? ReadMonth(JsonElement? value) =>
        value is { } json && json.TryGetInt32(out var month) ? month : null;

    private static SettingsScopeIdentity CreateIdentity(SettingScope scope, Guid tenantId, Guid? organizationId) => scope switch
    {
        SettingScope.Platform => SettingsScopeIdentity.Platform(),
        SettingScope.Tenant => SettingsScopeIdentity.Tenant(tenantId),
        SettingScope.Organization when organizationId is { } id => SettingsScopeIdentity.Organization(tenantId, id),
        SettingScope.Organization => throw new ArgumentException("An organization identifier is required."),
        _ => throw new NotSupportedException("Project and user settings are not enabled in this delivery.")
    };

    private static string? ValidateVersion(string? version)
    {
        if (string.IsNullOrWhiteSpace(version)) return null;
        try
        {
            return Convert.FromBase64String(version).Length == 8 ? null : "Expected version must be a SQL Server rowversion token.";
        }
        catch (FormatException)
        {
            return "Expected version must be a valid base64 SQL Server rowversion token.";
        }
    }

    private static EffectiveSettingDto ToDto(EffectiveSettingValue value) => new(
        value.Definition.Key,
        value.Definition.Type,
        value.Definition.DefaultValue.Clone(),
        value.Value.Clone(),
        value.Source.ToString(),
        value.IsInherited,
        value.CanOverride,
        value.Version,
        value.Definition.ValidationDescription,
        value.Definition.AllowedScopes.ToArray(),
        value.Definition.OverrideBehavior,
        value.Definition.SecurityClassification);

    private bool TryGetTenantId(out Guid tenantId)
    {
        tenantId = Guid.Empty;
        var principal = _httpContextAccessor.HttpContext?.User;
        return principal?.Identity?.IsAuthenticated == true
            && Guid.TryParse(principal.FindFirstValue("tenantId"), out tenantId)
            && tenantId != Guid.Empty;
    }

    private async Task<bool> IsAuthorizedForSettingsAsync(string policy)
    {
        var principal = _httpContextAccessor.HttpContext?.User;
        if (principal?.Identity?.IsAuthenticated != true) return false;
        var result = await _authorizationService.AuthorizeAsync(principal, resource: null, policyName: policy);
        return result.Succeeded;
    }

    private ActorContext CurrentActor()
    {
        var principal = _httpContextAccessor.HttpContext?.User;
        var actorId = Guid.TryParse(principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var parsed)
            ? parsed
            : Guid.TryParse(principal?.FindFirstValue("sub"), out parsed) ? parsed : (Guid?)null;
        return new ActorContext(actorId is { } id && id != Guid.Empty ? id : null, principal?.Identity?.Name);
    }

    private static SettingsOperationResult<T> Success<T>(T value) => new(SettingsOperationCode.Success, value);
    private static SettingsOperationResult<T> Failure<T>(SettingsOperationCode code, string error) => new(code, Error: error);
    private static SettingsOperationResult<T> ValidationFailure<T>(string field, string message) =>
        new(SettingsOperationCode.ValidationFailed, Error: "The submitted setting value is invalid.",
            ValidationErrors: new Dictionary<string, string[]> { [field] = new[] { message } });

    private sealed record ActorContext(Guid? UserId, string? UserName);
}
