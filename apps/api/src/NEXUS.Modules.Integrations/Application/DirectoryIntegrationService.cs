using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.Identity.Domain;
using NEXUS.Modules.Integrations.Domain;
using NEXUS.Modules.Organization.Domain;
using NEXUS.Modules.ResourceManagement.Domain;

namespace NEXUS.Modules.Integrations.Application;

public sealed partial class DirectoryIntegrationService : IDirectoryIntegrationService
{
    private const int MaximumDirectoryItems = 5000;
    private const int MaximumPageSize = 100;
    private static readonly Regex SafeSecretReference = new("^[A-Za-z0-9][A-Za-z0-9._-]{0,99}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly string[] SupportedTargetFields = ["firstName", "lastName", "email", "username", "jobTitle", "employeeId", "organizationalUnit", "manager", "accountStatus"];
    private readonly DbContext _context;
    private readonly IDirectorySecretResolver _secretResolver;
    private readonly IReadOnlyDictionary<string, IDirectoryProvider> _providers;
    private readonly IPasswordHasher<User> _passwordHasher;

    public DirectoryIntegrationService(DbContext context, IDirectorySecretResolver secretResolver, IEnumerable<IDirectoryProvider> providers, IPasswordHasher<User> passwordHasher)
    {
        _context = context;
        _secretResolver = secretResolver;
        _providers = providers.ToDictionary(provider => provider.ProviderType, StringComparer.OrdinalIgnoreCase);
        _passwordHasher = passwordHasher;
    }

    public async Task<IReadOnlyList<DirectoryConnectionDto>> GetConnectionsAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var connections = await _context.Set<DirectoryConnection>()
            .Where(connection => connection.TenantId == tenantId && !connection.IsDeleted)
            .OrderByDescending(connection => connection.IsDefault)
            .ThenBy(connection => connection.Name)
            .ToListAsync(cancellationToken);
        return connections.Select(ToConnectionDto).ToArray();
    }

    public async Task<IReadOnlyList<DirectoryReferenceOptionDto>> GetOrganizationalUnitsAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
        await _context.Set<OrganizationalUnit>()
            .Where(unit => unit.TenantId == tenantId && unit.IsActive && !unit.IsDeleted)
            .OrderBy(unit => unit.Name)
            .Select(unit => new DirectoryReferenceOptionDto(unit.Id, unit.Name, unit.Code))
            .ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyList<DirectoryReferenceOptionDto>> GetCalendarsAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
        await _context.Set<WorkingCalendar>()
            .Where(calendar => calendar.TenantId == tenantId && !calendar.IsDeleted)
            .OrderByDescending(calendar => calendar.IsDefault).ThenBy(calendar => calendar.Name)
            .Select(calendar => new DirectoryReferenceOptionDto(calendar.Id, calendar.Name, null))
            .ToArrayAsync(cancellationToken);

    public async Task<DirectoryConnectionDto> SaveConnectionAsync(Guid tenantId, Guid actorId, Guid? connectionId, SaveDirectoryConnectionRequest request, CancellationToken cancellationToken = default)
    {
        ValidateConnection(request);
        var connection = connectionId.HasValue
            ? await FindConnectionAsync(tenantId, connectionId.Value, cancellationToken)
            : new DirectoryConnection { TenantId = tenantId, CreatedBy = actorId.ToString("D") };
        var before = connectionId.HasValue ? ConnectionAuditSnapshot(connection) : null;

        if (request.IsDefault)
        {
            var defaults = await _context.Set<DirectoryConnection>()
                .Where(item => item.TenantId == tenantId && !item.IsDeleted && item.IsDefault && item.Id != connection.Id)
                .ToListAsync(cancellationToken);
            foreach (var currentDefault in defaults)
            {
                currentDefault.IsDefault = false;
                currentDefault.UpdatedAt = DateTime.UtcNow;
                currentDefault.UpdatedBy = actorId.ToString("D");
            }
        }

        connection.Name = request.Name.Trim();
        connection.ProviderType = request.ProviderType;
        connection.IsActive = request.IsActive;
        connection.IsDefault = request.IsDefault;
        connection.DirectoryTenantId = Clean(request.DirectoryTenantId);
        connection.ClientId = Clean(request.ClientId);
        connection.CredentialReference = Clean(request.CredentialReference);
        connection.Host = Clean(request.Host);
        connection.Port = request.Port;
        connection.UseSsl = request.UseSsl;
        connection.BaseDn = Clean(request.BaseDn);
        connection.BindUser = Clean(request.BindUser);
        connection.BindCredentialReference = Clean(request.BindCredentialReference);
        connection.Domain = Clean(request.Domain);
        connection.UpdatedAt = connectionId.HasValue ? DateTime.UtcNow : null;
        connection.UpdatedBy = connectionId.HasValue ? actorId.ToString("D") : null;
        if (!connectionId.HasValue) _context.Add(connection);
        AddAudit(tenantId, actorId, connectionId.HasValue ? "Update" : "Create", "DirectoryConnection", connection.Id,
            before, ConnectionAuditSnapshot(connection));
        await _context.SaveChangesAsync(cancellationToken);
        return ToConnectionDto(connection);
    }

    public async Task<DirectoryProviderTestResult> TestConnectionAsync(Guid tenantId, Guid actorId, Guid connectionId, CancellationToken cancellationToken = default)
    {
        var connection = await FindConnectionAsync(tenantId, connectionId, cancellationToken);
        var provider = GetProvider(connection.ProviderType);
        var credential = ResolveCredential(connection);
        var result = await provider.TestConnectionAsync(connection, credential, cancellationToken);
        AddAudit(tenantId, actorId, result.Connected ? "ConnectionTestSucceeded" : "ConnectionTestFailed", "DirectoryConnection", connection.Id,
            null, new { result.Provider, result.ErrorCode, result.LatencyMilliseconds });
        await _context.SaveChangesAsync(cancellationToken);
        return result;
    }

    public async Task<DirectorySyncScopeDto> GetScopeAsync(Guid tenantId, Guid connectionId, CancellationToken cancellationToken = default)
    {
        _ = await FindConnectionAsync(tenantId, connectionId, cancellationToken);
        var scope = await _context.Set<DirectorySyncScope>()
            .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.DirectoryConnectionId == connectionId && !item.IsDeleted, cancellationToken);
        return ToScopeDto(scope ?? NewScope(tenantId, connectionId));
    }

    public async Task<DirectorySyncScopeDto> SaveScopeAsync(Guid tenantId, Guid actorId, Guid connectionId, SaveDirectorySyncScopeRequest request, CancellationToken cancellationToken = default)
    {
        _ = await FindConnectionAsync(tenantId, connectionId, cancellationToken);
        ValidateScope(request);
        var scope = await _context.Set<DirectorySyncScope>()
            .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.DirectoryConnectionId == connectionId && !item.IsDeleted, cancellationToken);
        var before = scope is null ? null : ScopeAuditSnapshot(scope);
        var isNew = scope is null;
        scope ??= NewScope(tenantId, connectionId);
        scope.ScopeType = request.ScopeType;
        scope.SelectedGroupIdsJson = JsonSerializer.Serialize(NormalizeValues(request.SelectedGroupIds));
        scope.SelectedUserIdsJson = JsonSerializer.Serialize(NormalizeValues(request.SelectedUserIds));
        scope.OrganizationalUnitsJson = JsonSerializer.Serialize(NormalizeValues(request.OrganizationalUnits));
        scope.UserFilter = Clean(request.UserFilter);
        scope.GroupFilter = Clean(request.GroupFilter);
        scope.MaxItems = request.MaxItems;
        scope.UpdatedAt = DateTime.UtcNow;
        scope.UpdatedBy = actorId.ToString("D");
        if (isNew) _context.Add(scope);
        AddAudit(tenantId, actorId, "Update", "DirectorySyncScope", scope.Id, before, ScopeAuditSnapshot(scope));
        await _context.SaveChangesAsync(cancellationToken);
        return ToScopeDto(scope);
    }

    public async Task<IReadOnlyList<DirectoryFieldMappingDto>> GetFieldMappingsAsync(Guid tenantId, Guid connectionId, CancellationToken cancellationToken = default)
    {
        var connection = await FindConnectionAsync(tenantId, connectionId, cancellationToken);
        var mappings = await _context.Set<DirectoryFieldMapping>()
            .Where(mapping => mapping.TenantId == tenantId && mapping.DirectoryConnectionId == connectionId && !mapping.IsDeleted)
            .OrderBy(mapping => mapping.TargetField)
            .Select(mapping => new DirectoryFieldMappingDto(mapping.TargetField, mapping.SourceAttribute, mapping.UpdatePolicy, mapping.Transform))
            .ToArrayAsync(cancellationToken);
        return mappings.Length == 0 ? DefaultFieldMappings(connection.ProviderType) : mappings;
    }

    public async Task<IReadOnlyList<DirectoryFieldMappingDto>> SaveFieldMappingsAsync(Guid tenantId, Guid actorId, Guid connectionId, SaveDirectoryFieldMappingsRequest request, CancellationToken cancellationToken = default)
    {
        _ = await FindConnectionAsync(tenantId, connectionId, cancellationToken);
        ValidateFieldMappings(request.Mappings);
        var existing = await _context.Set<DirectoryFieldMapping>()
            .Where(mapping => mapping.TenantId == tenantId && mapping.DirectoryConnectionId == connectionId && !mapping.IsDeleted)
            .ToListAsync(cancellationToken);
        var before = existing.Select(MapField).ToArray();
        foreach (var item in existing)
        {
            item.IsDeleted = true;
            item.UpdatedAt = DateTime.UtcNow;
            item.UpdatedBy = actorId.ToString("D");
        }
        foreach (var item in request.Mappings)
        {
            _context.Add(new DirectoryFieldMapping
            {
                TenantId = tenantId,
                DirectoryConnectionId = connectionId,
                TargetField = item.TargetField.Trim(),
                SourceAttribute = item.SourceAttribute.Trim(),
                UpdatePolicy = item.UpdatePolicy,
                Transform = Clean(item.Transform),
                CreatedBy = actorId.ToString("D")
            });
        }
        AddAudit(tenantId, actorId, "Update", "DirectoryFieldMappings", connectionId, before, request.Mappings);
        await _context.SaveChangesAsync(cancellationToken);
        return request.Mappings.ToArray();
    }

    public async Task<DirectoryProviderPage<DirectoryGroupDto>> GetDirectoryGroupsAsync(Guid tenantId, Guid connectionId, DirectoryProviderQuery query, CancellationToken cancellationToken = default)
    {
        var connection = await FindConnectionAsync(tenantId, connectionId, cancellationToken);
        var provider = GetProvider(connection.ProviderType);
        var page = await provider.GetGroupsAsync(connection, ResolveCredential(connection), NormalizeQuery(query), cancellationToken);
        return new DirectoryProviderPage<DirectoryGroupDto>(
            page.Items.Select(group => new DirectoryGroupDto(group.ExternalDirectoryId, group.Name, group.Description, group.IsEnabled)).ToArray(),
            page.Page, page.PageSize, page.HasMore);
    }

    public async Task<IReadOnlyList<DirectoryGroupMappingDto>> GetGroupMappingsAsync(Guid tenantId, Guid connectionId, CancellationToken cancellationToken = default)
    {
        _ = await FindConnectionAsync(tenantId, connectionId, cancellationToken);
        return await _context.Set<DirectoryGroupMapping>()
            .Where(mapping => mapping.TenantId == tenantId && mapping.DirectoryConnectionId == connectionId && !mapping.IsDeleted)
            .OrderBy(mapping => mapping.ExternalGroupName)
            .Select(mapping => new DirectoryGroupMappingDto(mapping.ExternalGroupId, mapping.ExternalGroupName, mapping.RoleId, mapping.IsActive))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DirectoryGroupMappingDto>> SaveGroupMappingsAsync(Guid tenantId, Guid actorId, Guid connectionId, SaveDirectoryGroupMappingsRequest request, CancellationToken cancellationToken = default)
    {
        _ = await FindConnectionAsync(tenantId, connectionId, cancellationToken);
        if (request.Mappings.Count > 500 || request.Mappings.Any(mapping => string.IsNullOrWhiteSpace(mapping.ExternalGroupId) || string.IsNullOrWhiteSpace(mapping.ExternalGroupName)))
            throw BadRequest("DIRECTORY_GROUP_MAPPING_INVALID", "Provide up to 500 complete group mappings.");
        if (request.Mappings.Select(mapping => mapping.ExternalGroupId).Distinct(StringComparer.OrdinalIgnoreCase).Count() != request.Mappings.Count)
            throw BadRequest("DIRECTORY_GROUP_MAPPING_DUPLICATE", "A directory group can have only one mapping per connection.");
        var roleIds = request.Mappings.Where(mapping => mapping.RoleId.HasValue).Select(mapping => mapping.RoleId!.Value).Distinct().ToArray();
        if (roleIds.Length > 0)
        {
            var activeRoles = await _context.Set<Role>().Where(role => role.TenantId == tenantId && !role.IsDeleted && roleIds.Contains(role.Id)).Select(role => role.Id).ToArrayAsync(cancellationToken);
            if (activeRoles.Length != roleIds.Length) throw BadRequest("DIRECTORY_ROLE_INVALID", "Every mapped NEXUS Role must be active and belong to the current tenant.");
        }
        var oldMappings = await _context.Set<DirectoryGroupMapping>()
            .Where(mapping => mapping.TenantId == tenantId && mapping.DirectoryConnectionId == connectionId && !mapping.IsDeleted)
            .ToListAsync(cancellationToken);
        var before = oldMappings.Select(MapGroupMapping).ToArray();
        foreach (var old in oldMappings) { old.IsDeleted = true; old.UpdatedAt = DateTime.UtcNow; old.UpdatedBy = actorId.ToString("D"); }
        foreach (var item in request.Mappings)
            _context.Add(new DirectoryGroupMapping { TenantId = tenantId, DirectoryConnectionId = connectionId, ExternalGroupId = item.ExternalGroupId.Trim(), ExternalGroupName = item.ExternalGroupName.Trim(), RoleId = item.RoleId, IsActive = item.IsActive, CreatedBy = actorId.ToString("D") });
        AddAudit(tenantId, actorId, "Update", "DirectoryGroupMappings", connectionId, before, request.Mappings);
        await _context.SaveChangesAsync(cancellationToken);
        return request.Mappings.ToArray();
    }

    public async Task<IReadOnlyList<DirectoryOrganizationMappingDto>> GetOrganizationMappingsAsync(Guid tenantId, Guid connectionId, CancellationToken cancellationToken = default)
    {
        _ = await FindConnectionAsync(tenantId, connectionId, cancellationToken);
        return await _context.Set<DirectoryOrganizationMapping>()
            .Where(mapping => mapping.TenantId == tenantId && mapping.DirectoryConnectionId == connectionId && !mapping.IsDeleted)
            .OrderBy(mapping => mapping.DirectoryValue)
            .Select(mapping => new DirectoryOrganizationMappingDto(mapping.DirectoryValue, mapping.OrganizationalUnitId))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DirectoryOrganizationMappingDto>> SaveOrganizationMappingsAsync(Guid tenantId, Guid actorId, Guid connectionId, SaveDirectoryOrganizationMappingsRequest request, CancellationToken cancellationToken = default)
    {
        _ = await FindConnectionAsync(tenantId, connectionId, cancellationToken);
        if (request.Mappings.Count > 500 || request.Mappings.Any(mapping => string.IsNullOrWhiteSpace(mapping.DirectoryValue)))
            throw BadRequest("DIRECTORY_ORGANIZATION_MAPPING_INVALID", "Provide up to 500 complete department-to-organization mappings.");
        if (request.Mappings.Select(mapping => mapping.DirectoryValue.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != request.Mappings.Count)
            throw BadRequest("DIRECTORY_ORGANIZATION_MAPPING_DUPLICATE", "A directory department value can have only one organization mapping.");
        var ids = request.Mappings.Select(mapping => mapping.OrganizationalUnitId).Distinct().ToArray();
        var units = await _context.Set<OrganizationalUnit>().Where(unit => unit.TenantId == tenantId && !unit.IsDeleted && unit.IsActive && ids.Contains(unit.Id)).Select(unit => unit.Id).ToArrayAsync(cancellationToken);
        if (units.Length != ids.Length) throw BadRequest("DIRECTORY_ORGANIZATION_UNIT_INVALID", "Mapped organizational units must be active and belong to the current tenant.");
        var existing = await _context.Set<DirectoryOrganizationMapping>()
            .Where(mapping => mapping.TenantId == tenantId && mapping.DirectoryConnectionId == connectionId && !mapping.IsDeleted)
            .ToListAsync(cancellationToken);
        var before = existing.Select(mapping => new DirectoryOrganizationMappingDto(mapping.DirectoryValue, mapping.OrganizationalUnitId)).ToArray();
        foreach (var item in existing) { item.IsDeleted = true; item.UpdatedAt = DateTime.UtcNow; item.UpdatedBy = actorId.ToString("D"); }
        foreach (var item in request.Mappings)
            _context.Add(new DirectoryOrganizationMapping { TenantId = tenantId, DirectoryConnectionId = connectionId, DirectoryValue = item.DirectoryValue.Trim(), OrganizationalUnitId = item.OrganizationalUnitId, CreatedBy = actorId.ToString("D") });
        AddAudit(tenantId, actorId, "Update", "DirectoryOrganizationMappings", connectionId, before, request.Mappings);
        await _context.SaveChangesAsync(cancellationToken);
        return request.Mappings.ToArray();
    }

    public async Task<DirectoryProvisioningPolicyDto> GetProvisioningPolicyAsync(Guid tenantId, Guid connectionId, CancellationToken cancellationToken = default)
    {
        _ = await FindConnectionAsync(tenantId, connectionId, cancellationToken);
        var policy = await _context.Set<DirectoryProvisioningPolicy>()
            .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.DirectoryConnectionId == connectionId && !item.IsDeleted, cancellationToken);
        return ToPolicyDto(policy ?? NewPolicy(tenantId, connectionId));
    }

    public async Task<DirectoryProvisioningPolicyDto> SaveProvisioningPolicyAsync(Guid tenantId, Guid actorId, Guid connectionId, SaveDirectoryProvisioningPolicyRequest request, CancellationToken cancellationToken = default)
    {
        _ = await FindConnectionAsync(tenantId, connectionId, cancellationToken);
        ValidatePolicy(request);
        if (request.DefaultOrganizationalUnitId is { } unitId && !await _context.Set<OrganizationalUnit>().AnyAsync(unit => unit.Id == unitId && unit.TenantId == tenantId && !unit.IsDeleted && unit.IsActive, cancellationToken))
            throw BadRequest("DIRECTORY_ORGANIZATION_UNIT_INVALID", "The default organizational unit must be active in the current tenant.");
        if (request.DefaultBaseCalendarId is { } calendarId && !await _context.Set<WorkingCalendar>().AnyAsync(calendar => calendar.Id == calendarId && calendar.TenantId == tenantId && !calendar.IsDeleted, cancellationToken))
            throw BadRequest("DIRECTORY_CALENDAR_INVALID", "The default base calendar must exist in the current tenant.");
        var policy = await _context.Set<DirectoryProvisioningPolicy>()
            .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.DirectoryConnectionId == connectionId && !item.IsDeleted, cancellationToken);
        var before = policy is null ? null : ToPolicyDto(policy);
        var isNew = policy is null;
        policy ??= NewPolicy(tenantId, connectionId);
        policy.MissingUserAction = request.MissingUserAction;
        policy.MissingGroupAction = request.MissingGroupAction;
        policy.DisabledUserAction = request.DisabledUserAction;
        policy.OutOfScopeUserAction = request.OutOfScopeUserAction;
        policy.ChangedGroupAction = request.ChangedGroupAction;
        policy.MissingGroupReferenceAction = request.MissingGroupReferenceAction;
        policy.CreateResource = request.CreateResource;
        policy.DefaultOrganizationalUnitId = request.DefaultOrganizationalUnitId;
        policy.DefaultBaseCalendarId = request.DefaultBaseCalendarId;
        policy.DefaultEmploymentType = Clean(request.DefaultEmploymentType);
        policy.DefaultSeniority = Clean(request.DefaultSeniority);
        policy.UpdatedAt = DateTime.UtcNow;
        policy.UpdatedBy = actorId.ToString("D");
        if (isNew) _context.Add(policy);
        var after = ToPolicyDto(policy);
        AddAudit(tenantId, actorId, "Update", "DirectoryProvisioningPolicy", policy.Id, before, after);
        await _context.SaveChangesAsync(cancellationToken);
        return after;
    }

    public async Task<IReadOnlyList<DirectorySyncRunDto>> GetHistoryAsync(Guid tenantId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var runs = await _context.Set<DirectorySyncRun>()
            .Where(run => run.TenantId == tenantId && !run.IsDeleted)
            .OrderByDescending(run => run.StartedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        var ids = runs.Select(run => run.DirectoryConnectionId).Distinct().ToArray();
        var names = await _context.Set<DirectoryConnection>().Where(connection => connection.TenantId == tenantId && ids.Contains(connection.Id)).ToDictionaryAsync(connection => connection.Id, connection => connection.Name, cancellationToken);
        return runs.Select(run => ToRunDto(run, names.GetValueOrDefault(run.DirectoryConnectionId) ?? "—")).ToArray();
    }

    public async Task<DirectorySyncRunDetailDto> GetHistoryDetailAsync(Guid tenantId, Guid runId, CancellationToken cancellationToken = default)
    {
        var run = await _context.Set<DirectorySyncRun>().FirstOrDefaultAsync(item => item.Id == runId && item.TenantId == tenantId && !item.IsDeleted, cancellationToken)
            ?? throw NotFound("DIRECTORY_SYNC_RUN_NOT_FOUND", "The synchronization run was not found.");
        var connectionName = await _context.Set<DirectoryConnection>().Where(connection => connection.Id == run.DirectoryConnectionId && connection.TenantId == tenantId).Select(connection => connection.Name).FirstOrDefaultAsync(cancellationToken) ?? "—";
        var itemEntities = await _context.Set<DirectorySyncItem>()
            .Where(item => item.TenantId == tenantId && item.DirectorySyncRunId == runId && !item.IsDeleted)
            .OrderBy(item => item.ItemType).ThenBy(item => item.DisplayName)
            .ToArrayAsync(cancellationToken);
        var items = itemEntities.Select(item => new DirectorySyncItemDto(item.Id, item.ItemType, item.ExternalDirectoryId, item.DisplayName, item.Classification, item.ProposedAction, item.MatchedUserId, item.Status, item.ErrorCode, ReadChanges(item.DetailsJson))).ToArray();
        return new DirectorySyncRunDetailDto(ToRunDto(run, connectionName), items);
    }

    private async Task<DirectoryConnection> FindConnectionAsync(Guid tenantId, Guid connectionId, CancellationToken cancellationToken) =>
        await _context.Set<DirectoryConnection>().FirstOrDefaultAsync(connection => connection.Id == connectionId && connection.TenantId == tenantId && !connection.IsDeleted, cancellationToken)
        ?? throw NotFound("DIRECTORY_CONNECTION_NOT_FOUND", "The directory connection was not found.");

    private IDirectoryProvider GetProvider(string providerType) => _providers.TryGetValue(providerType, out var provider)
        ? provider
        : throw BadRequest("DIRECTORY_PROVIDER_UNSUPPORTED", "Choose Microsoft Entra ID or Active Directory / LDAP.");

    private string? ResolveCredential(DirectoryConnection connection)
    {
        var reference = connection.ProviderType == DirectoryProviderTypes.Entra ? connection.CredentialReference : connection.BindCredentialReference;
        return _secretResolver.Resolve(connection.TenantId, connection.Id, reference);
    }

    private DirectoryConnectionDto ToConnectionDto(DirectoryConnection connection)
    {
        var entra = connection.ProviderType == DirectoryProviderTypes.Entra;
        var reference = entra ? connection.CredentialReference : connection.BindCredentialReference;
        var credentialConfigured = !string.IsNullOrWhiteSpace(ResolveCredential(connection));
        return new DirectoryConnectionDto(connection.Id, connection.Name, connection.ProviderType, connection.IsActive, connection.IsDefault,
            connection.DirectoryTenantId, connection.ClientId, entra ? reference : null, entra && credentialConfigured,
            connection.Host, connection.Port, connection.UseSsl, connection.BaseDn, connection.BindUser,
            entra ? null : reference, !entra && credentialConfigured, connection.Domain, connection.CreatedAt, connection.UpdatedAt);
    }

    private static DirectorySyncScope NewScope(Guid tenantId, Guid connectionId) => new() { TenantId = tenantId, DirectoryConnectionId = connectionId, ScopeType = DirectoryScopeTypes.SelectedGroups, MaxItems = 500 };
    private static DirectoryProvisioningPolicy NewPolicy(Guid tenantId, Guid connectionId) => new() { TenantId = tenantId, DirectoryConnectionId = connectionId };
    private static DirectorySyncScopeDto ToScopeDto(DirectorySyncScope scope) => new(scope.ScopeType, ReadValues(scope.SelectedGroupIdsJson), ReadValues(scope.SelectedUserIdsJson), ReadValues(scope.OrganizationalUnitsJson), scope.UserFilter, scope.GroupFilter, scope.MaxItems);
    private static DirectoryProvisioningPolicyDto ToPolicyDto(DirectoryProvisioningPolicy policy) => new(policy.MissingUserAction, policy.MissingGroupAction, policy.DisabledUserAction, policy.OutOfScopeUserAction, policy.ChangedGroupAction, policy.MissingGroupReferenceAction, policy.CreateResource, policy.DefaultOrganizationalUnitId, policy.DefaultBaseCalendarId, policy.DefaultEmploymentType, policy.DefaultSeniority);
    private static DirectorySyncRunDto ToRunDto(DirectorySyncRun run, string connectionName) => new(run.Id, run.DirectoryConnectionId, connectionName, run.StartedAt, run.CompletedAt, run.Mode, run.Scope, run.Status, run.UsersFetched, run.UsersCreated, run.UsersUpdated, run.UsersDeactivated, run.UsersSkipped, run.GroupsCreated, run.GroupsUpdated, run.GroupsDeactivated, run.Conflicts, run.Failures);
    private static DirectoryFieldMappingDto MapField(DirectoryFieldMapping mapping) => new(mapping.TargetField, mapping.SourceAttribute, mapping.UpdatePolicy, mapping.Transform);
    private static DirectoryGroupMappingDto MapGroupMapping(DirectoryGroupMapping mapping) => new(mapping.ExternalGroupId, mapping.ExternalGroupName, mapping.RoleId, mapping.IsActive);
    private static IReadOnlyDictionary<string, string?> ReadChanges(string? json) => DeserializeChanges(json);
    private static DirectoryIntegrationException BadRequest(string code, string message) => new(StatusCodes.Status400BadRequest, code, message);
    private static DirectoryIntegrationException NotFound(string code, string message) => new(StatusCodes.Status404NotFound, code, message);

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static IReadOnlyList<string> NormalizeValues(IEnumerable<string> values) => values.Select(value => value.Trim()).Where(value => value.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    private static string[] ReadValues(string json) { try { return JsonSerializer.Deserialize<string[]>(json) ?? []; } catch (JsonException) { return []; } }
    private static DirectoryProviderQuery NormalizeQuery(DirectoryProviderQuery query) => new(Math.Max(1, query.Page), Math.Clamp(query.PageSize, 1, MaximumPageSize), Clean(query.Search));
    private static IReadOnlyDictionary<string, string?> DeserializeChanges(string? json) { try { return JsonSerializer.Deserialize<Dictionary<string, string?>>(json ?? "{}") ?? new Dictionary<string, string?>(); } catch (JsonException) { return new Dictionary<string, string?>(); } }

    private static void ValidateConnection(SaveDirectoryConnectionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 150)
            throw BadRequest("DIRECTORY_NAME_INVALID", "Connection name is required and must be 150 characters or fewer.");
        if (request.ProviderType is not (DirectoryProviderTypes.Entra or DirectoryProviderTypes.Ldap))
            throw BadRequest("DIRECTORY_PROVIDER_UNSUPPORTED", "Choose Microsoft Entra ID or Active Directory / LDAP.");
        if (request.ProviderType == DirectoryProviderTypes.Entra)
        {
            if (!Guid.TryParse(request.DirectoryTenantId, out _) || !Guid.TryParse(request.ClientId, out _))
                throw BadRequest("ENTRA_CONFIGURATION_INVALID", "Enter a valid Entra tenant ID and application client ID.");
            ValidateSecretReference(request.CredentialReference);
            return;
        }
        if (string.IsNullOrWhiteSpace(request.Host) || request.Host.Length > 255 || request.Host.Contains('/') || request.Host.Contains('\\'))
            throw BadRequest("LDAP_HOST_INVALID", "Enter an LDAP host name or IP address without a URL scheme or path.");
        if (request.Port is not (636 or 3269) || !request.UseSsl)
            throw BadRequest("LDAP_TLS_REQUIRED", "Use secure LDAP (LDAPS) on port 636 or 3269.");
        if (string.IsNullOrWhiteSpace(request.BaseDn) || request.BaseDn.Length > 1024 || request.BaseDn.Any(char.IsControl))
            throw BadRequest("LDAP_BASE_DN_INVALID", "Enter a valid LDAP Base DN (up to 1024 characters).");
        if (string.IsNullOrWhiteSpace(request.BindUser) || request.BindUser.Length > 256)
            throw BadRequest("LDAP_BIND_USER_INVALID", "Enter a bind identity of 256 characters or fewer.");
        ValidateSecretReference(request.BindCredentialReference);
    }

    private static void ValidateSecretReference(string? reference)
    {
        if (!string.IsNullOrWhiteSpace(reference) && !SafeSecretReference.IsMatch(reference.Trim()))
            throw BadRequest("DIRECTORY_SECRET_REFERENCE_INVALID", "Credential reference may contain letters, numbers, dots, underscores, and hyphens only.");
    }

    private static void ValidateScope(SaveDirectorySyncScopeRequest request)
    {
        if (request.ScopeType is not (DirectoryScopeTypes.SelectedGroups or DirectoryScopeTypes.SelectedUsers or DirectoryScopeTypes.OrganizationalUnits or DirectoryScopeTypes.AllUsers))
            throw BadRequest("DIRECTORY_SCOPE_INVALID", "Choose selected groups, selected users, organizational units, or all users.");
        if (request.MaxItems is < 1 or > MaximumDirectoryItems)
            throw BadRequest("DIRECTORY_SCOPE_LIMIT_INVALID", "Maximum sync scope must be between 1 and 5000 records.");
        if (request.SelectedGroupIds.Count > 50 || request.SelectedUserIds.Count > 1000 || request.OrganizationalUnits.Count > 50)
            throw BadRequest("DIRECTORY_SCOPE_TOO_LARGE", "The selected scope exceeds its safe limit.");
        if ((request.UserFilter?.Length ?? 0) > 1024 || (request.GroupFilter?.Length ?? 0) > 1024)
            throw BadRequest("DIRECTORY_FILTER_TOO_LARGE", "Directory filters must be 1024 characters or fewer.");
        if (request.ScopeType == DirectoryScopeTypes.SelectedGroups && request.SelectedGroupIds.Count == 0
            || request.ScopeType == DirectoryScopeTypes.SelectedUsers && request.SelectedUserIds.Count == 0
            || request.ScopeType == DirectoryScopeTypes.OrganizationalUnits && request.OrganizationalUnits.Count == 0)
            throw BadRequest("DIRECTORY_SCOPE_EMPTY", "Choose at least one group, user, or organizational unit. All Users must be selected explicitly.");
    }

    private static void ValidateFieldMappings(IReadOnlyCollection<DirectoryFieldMappingDto> mappings)
    {
        if (mappings.Count > SupportedTargetFields.Length || mappings.Any(mapping => !SupportedTargetFields.Contains(mapping.TargetField, StringComparer.Ordinal)))
            throw BadRequest("DIRECTORY_FIELD_MAPPING_INVALID", "Choose only supported NEXUS fields and configure each field once.");
        if (mappings.Select(mapping => mapping.TargetField).Distinct(StringComparer.Ordinal).Count() != mappings.Count)
            throw BadRequest("DIRECTORY_FIELD_MAPPING_DUPLICATE", "Each NEXUS field can have only one directory mapping.");
        foreach (var mapping in mappings)
        {
            if (string.IsNullOrWhiteSpace(mapping.SourceAttribute) || mapping.SourceAttribute.Length > 100 || !Regex.IsMatch(mapping.SourceAttribute, "^[A-Za-z][A-Za-z0-9._-]*$", RegexOptions.CultureInvariant))
                throw BadRequest("DIRECTORY_ATTRIBUTE_INVALID", "Enter a directory attribute name using letters, numbers, dots, underscores, and hyphens.");
            if (mapping.UpdatePolicy is not (DirectoryUpdatePolicies.Ignore or DirectoryUpdatePolicies.Review or DirectoryUpdatePolicies.AutoUpdate))
                throw BadRequest("DIRECTORY_UPDATE_POLICY_INVALID", "Choose Ignore, Review, or Auto Update for every mapped field.");
            if (Clean(mapping.Transform) is { } transform && transform is not ("Trim" or "Lowercase" or "Uppercase"))
                throw BadRequest("DIRECTORY_TRANSFORM_INVALID", "Supported transformations are Trim, Lowercase, and Uppercase.");
        }
    }

    private static void ValidatePolicy(SaveDirectoryProvisioningPolicyRequest request)
    {
        if (!IsOneOf(request.MissingUserAction, DirectoryProvisioningActions.Ignore, DirectoryProvisioningActions.Review, DirectoryProvisioningActions.AutoCreate)
            || !IsOneOf(request.MissingGroupAction, DirectoryProvisioningActions.Ignore, DirectoryProvisioningActions.Review, DirectoryProvisioningActions.AutoCreate))
            throw BadRequest("DIRECTORY_MISSING_ACTION_INVALID", "Missing users and groups support Ignore, Review, or Auto Create.");
        if (!IsOneOf(request.DisabledUserAction, DirectoryProvisioningActions.DoNothing, DirectoryProvisioningActions.Review, DirectoryProvisioningActions.AutoDeactivate)
            || !IsOneOf(request.OutOfScopeUserAction, DirectoryProvisioningActions.DoNothing, DirectoryProvisioningActions.Review, DirectoryProvisioningActions.AutoDeactivate)
            || !IsOneOf(request.MissingGroupReferenceAction, DirectoryProvisioningActions.DoNothing, DirectoryProvisioningActions.Review, DirectoryProvisioningActions.AutoDeactivate))
            throw BadRequest("DIRECTORY_DEACTIVATION_ACTION_INVALID", "Disabled or missing records support Do Nothing, Review, or Auto Deactivate.");
        if (!IsOneOf(request.ChangedGroupAction, DirectoryProvisioningActions.Ignore, DirectoryProvisioningActions.Review, DirectoryProvisioningActions.AutoUpdate))
            throw BadRequest("DIRECTORY_GROUP_ACTION_INVALID", "Changed groups support Ignore, Review, or Auto Update.");
        if ((request.DefaultEmploymentType?.Length ?? 0) > 100 || (request.DefaultSeniority?.Length ?? 0) > 100)
            throw BadRequest("DIRECTORY_DEFAULT_INVALID", "Resource defaults must be 100 characters or fewer.");
    }

    private static bool IsOneOf(string value, params string[] allowed) => allowed.Contains(value, StringComparer.Ordinal);

    private static IReadOnlyList<DirectoryFieldMappingDto> DefaultFieldMappings(string providerType) => providerType == DirectoryProviderTypes.Entra
        ? [
            new("firstName", "givenName", DirectoryUpdatePolicies.AutoUpdate, null),
            new("lastName", "surname", DirectoryUpdatePolicies.AutoUpdate, null),
            new("email", "mail", DirectoryUpdatePolicies.AutoUpdate, null),
            new("username", "userPrincipalName", DirectoryUpdatePolicies.AutoUpdate, null),
            new("jobTitle", "jobTitle", DirectoryUpdatePolicies.AutoUpdate, null),
            new("employeeId", "employeeId", DirectoryUpdatePolicies.AutoUpdate, null),
            new("organizationalUnit", "department", DirectoryUpdatePolicies.Review, null),
            new("manager", "manager", DirectoryUpdatePolicies.Review, null),
            new("accountStatus", "accountEnabled", DirectoryUpdatePolicies.Review, null)
        ]
        : [
            new("firstName", "givenName", DirectoryUpdatePolicies.AutoUpdate, null),
            new("lastName", "sn", DirectoryUpdatePolicies.AutoUpdate, null),
            new("email", "mail", DirectoryUpdatePolicies.AutoUpdate, null),
            new("username", "userPrincipalName", DirectoryUpdatePolicies.AutoUpdate, null),
            new("jobTitle", "title", DirectoryUpdatePolicies.AutoUpdate, null),
            new("employeeId", "employeeID", DirectoryUpdatePolicies.AutoUpdate, null),
            new("organizationalUnit", "department", DirectoryUpdatePolicies.Review, null),
            new("manager", "manager", DirectoryUpdatePolicies.Review, null),
            new("accountStatus", "accountEnabled", DirectoryUpdatePolicies.Review, null)
        ];

    private static object? ScopeAuditSnapshot(DirectorySyncScope? scope) => scope is null ? null : new
    {
        scope.ScopeType,
        SelectedGroupIds = ReadValues(scope.SelectedGroupIdsJson),
        SelectedUserIds = ReadValues(scope.SelectedUserIdsJson),
        OrganizationalUnits = ReadValues(scope.OrganizationalUnitsJson),
        scope.UserFilter,
        scope.GroupFilter,
        scope.MaxItems
    };

    private void AddAudit(Guid tenantId, Guid? actorId, string action, string entityType, Guid entityId, object? before, object? after)
    {
        _context.Add(new AuditEntry
        {
            TenantId = tenantId,
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            UserId = actorId,
            UserName = actorId?.ToString("D"),
            OriginalValues = before is null ? null : JsonSerializer.Serialize(before),
            NewValues = after is null ? null : JsonSerializer.Serialize(after),
            Changes = "DirectoryIntegration",
            Timestamp = DateTime.UtcNow,
            CreatedBy = actorId?.ToString("D") ?? "DirectoryIntegration"
        });
    }

    private static object ConnectionAuditSnapshot(DirectoryConnection connection) => new
    {
        connection.Name, connection.ProviderType, connection.IsActive, connection.IsDefault,
        connection.DirectoryTenantId, connection.ClientId, CredentialConfigured = !string.IsNullOrWhiteSpace(connection.CredentialReference),
        connection.Host, connection.Port, connection.UseSsl, connection.BaseDn, connection.BindUser,
        BindCredentialConfigured = !string.IsNullOrWhiteSpace(connection.BindCredentialReference), connection.Domain
    };
}
