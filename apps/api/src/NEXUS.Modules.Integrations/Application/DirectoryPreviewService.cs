using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Identity.Domain;
using NEXUS.Modules.Integrations.Domain;
using NEXUS.Modules.Organization.Domain;

namespace NEXUS.Modules.Integrations.Application;

public sealed partial class DirectoryIntegrationService
{
    public async Task<DirectoryPreviewResultDto> PreviewAsync(Guid tenantId, Guid connectionId, string itemType, DirectoryProviderQuery query, CancellationToken cancellationToken = default)
    {
        var connection = await FindConnectionAsync(tenantId, connectionId, cancellationToken);
        if (!connection.IsActive) throw BadRequest("DIRECTORY_CONNECTION_INACTIVE", "Activate this directory connection before previewing it.");
        var type = itemType.Equals("Groups", StringComparison.OrdinalIgnoreCase) ? "Groups"
            : itemType.Equals("Users", StringComparison.OrdinalIgnoreCase) ? "Users"
            : throw BadRequest("DIRECTORY_PREVIEW_TYPE_INVALID", "Preview Users or Groups.");
        var normalized = NormalizeQuery(query);
        var timer = Stopwatch.StartNew();
        List<DirectoryPreviewItemDto> allItems;
        if (type == "Users")
        {
            var source = await FetchAllUsersAsync(connection, normalized, cancellationToken);
            allItems = await BuildUserPreviewItemsAsync(tenantId, connection, source, cancellationToken);
        }
        else
        {
            var source = await FetchAllGroupsAsync(connection, normalized, MaximumDirectoryItems, cancellationToken);
            allItems = await BuildGroupPreviewItemsAsync(tenantId, connection, source, cancellationToken);
        }
        timer.Stop();
        var skip = (normalized.Page - 1) * normalized.PageSize;
        return new DirectoryPreviewResultDto(
            connectionId,
            normalized.Page,
            normalized.PageSize,
            allItems.Count > skip + normalized.PageSize,
            allItems.Skip(skip).Take(normalized.PageSize).ToArray(),
            new DirectoryProviderTestResult(true, connection.ProviderType, connection.DirectoryTenantId ?? connection.Host, timer.ElapsedMilliseconds, null, "The requested preview page was fetched from the configured directory."));
    }

    internal async Task<(IReadOnlyList<DirectoryUserRecord> Items, bool Truncated)> FetchAllUsersAsync(
        DirectoryConnection connection,
        DirectoryProviderQuery query,
        CancellationToken cancellationToken)
    {
        var scope = await _context.Set<DirectorySyncScope>()
            .FirstOrDefaultAsync(item => item.TenantId == connection.TenantId && item.DirectoryConnectionId == connection.Id && !item.IsDeleted, cancellationToken)
            ?? NewScope(connection.TenantId, connection.Id);
        if (scope.ScopeType == DirectoryScopeTypes.SelectedGroups && ReadValues(scope.SelectedGroupIdsJson).Length == 0
            || scope.ScopeType == DirectoryScopeTypes.SelectedUsers && ReadValues(scope.SelectedUserIdsJson).Length == 0
            || scope.ScopeType == DirectoryScopeTypes.OrganizationalUnits && ReadValues(scope.OrganizationalUnitsJson).Length == 0)
            return ([], false);

        var provider = GetProvider(connection.ProviderType);
        var credential = ResolveCredential(connection);
        var all = new List<DirectoryUserRecord>();
        // Fetch the bounded synchronization scope in one provider call. Providers still
        // use their own 100-row wire pages, but do not restart Graph/LDAP paging for each
        // application page (which would make a 5,000-row preview quadratic).
        var pageSize = Math.Clamp(scope.MaxItems, 1, MaximumDirectoryItems);
        var truncated = false;
        for (var page = 1; all.Count < scope.MaxItems; page++)
        {
            var result = await provider.GetUsersAsync(connection, scope, credential, new DirectoryProviderQuery(page, pageSize, query.Search), cancellationToken);
            all.AddRange(result.Items);
            if (!result.HasMore) break;
            if (all.Count >= scope.MaxItems) { truncated = true; break; }
        }
        return (all.Take(scope.MaxItems).DistinctBy(user => user.ExternalDirectoryId, StringComparer.OrdinalIgnoreCase).ToArray(), truncated);
    }

    internal async Task<(IReadOnlyList<DirectoryGroupRecord> Items, bool Truncated)> FetchAllGroupsAsync(
        DirectoryConnection connection,
        DirectoryProviderQuery query,
        int maximumItems,
        CancellationToken cancellationToken)
    {
        var provider = GetProvider(connection.ProviderType);
        var credential = ResolveCredential(connection);
        var all = new List<DirectoryGroupRecord>();
        var max = Math.Clamp(maximumItems, 1, MaximumDirectoryItems);
        var truncated = false;
        for (var page = 1; all.Count < max; page++)
        {
            var result = await provider.GetGroupsAsync(connection, credential, new DirectoryProviderQuery(page, max, query.Search), cancellationToken);
            all.AddRange(result.Items);
            if (!result.HasMore) break;
            if (all.Count >= max) { truncated = true; break; }
        }
        return (all.Take(max).DistinctBy(group => group.ExternalDirectoryId, StringComparer.OrdinalIgnoreCase).ToArray(), truncated);
    }

    private async Task<List<DirectoryPreviewItemDto>> BuildUserPreviewItemsAsync(
        Guid tenantId,
        DirectoryConnection connection,
        (IReadOnlyList<DirectoryUserRecord> Items, bool Truncated) source,
        CancellationToken cancellationToken)
    {
        var scope = await _context.Set<DirectorySyncScope>()
            .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.DirectoryConnectionId == connection.Id && !item.IsDeleted, cancellationToken)
            ?? NewScope(tenantId, connection.Id);
        var links = await _context.Set<DirectoryUserLink>()
            .Where(link => link.TenantId == tenantId && link.DirectoryConnectionId == connection.Id && !link.IsDeleted)
            .OrderBy(link => link.Id).Take(MaximumDirectoryItems).ToListAsync(cancellationToken);
        var users = await _context.Set<User>()
            .Where(user => user.TenantId == tenantId && !user.IsDeleted)
            .Take(MaximumDirectoryItems)
            .ToListAsync(cancellationToken);
        var userById = users.ToDictionary(user => user.Id);
        var mappingSet = await _context.Set<DirectoryFieldMapping>()
            .Where(mapping => mapping.TenantId == tenantId && mapping.DirectoryConnectionId == connection.Id && !mapping.IsDeleted)
            .ToListAsync(cancellationToken);
        var fieldMappings = mappingSet.Count > 0 ? mappingSet.Select(MapField).ToArray() : DefaultFieldMappings(connection.ProviderType).ToArray();
        var orgMappings = await _context.Set<DirectoryOrganizationMapping>()
            .Where(mapping => mapping.TenantId == tenantId && mapping.DirectoryConnectionId == connection.Id && !mapping.IsDeleted)
            .ToListAsync(cancellationToken);
        var groupMappings = await _context.Set<DirectoryGroupMapping>()
            .Where(mapping => mapping.TenantId == tenantId && mapping.DirectoryConnectionId == connection.Id && !mapping.IsDeleted && mapping.IsActive)
            .ToListAsync(cancellationToken);
        var policy = await _context.Set<DirectoryProvisioningPolicy>()
            .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.DirectoryConnectionId == connection.Id && !item.IsDeleted, cancellationToken)
            ?? NewPolicy(tenantId, connection.Id);
        var roleIds = groupMappings.Where(mapping => mapping.RoleId.HasValue).Select(mapping => mapping.RoleId!.Value).Distinct().ToArray();
        var activeRoles = roleIds.Length == 0 ? new Dictionary<Guid, string>() : await _context.Set<Role>()
            .Where(role => role.TenantId == tenantId && !role.IsDeleted && roleIds.Contains(role.Id))
            .ToDictionaryAsync(role => role.Id, role => role.Name, cancellationToken);
        var userRoleIds = userById.Keys.Count == 0 ? new List<UserRolePreview>() : await _context.Set<UserRole>()
            .Where(assignment => assignment.TenantId == tenantId && !assignment.IsDeleted && userById.Keys.Contains(assignment.UserId))
            .Select(assignment => new UserRolePreview(assignment.UserId, assignment.RoleId))
            .ToListAsync(cancellationToken);
        var existingRoles = userRoleIds.GroupBy(item => item.UserId).ToDictionary(group => group.Key, group => group.Select(item => item.RoleId).ToHashSet());
        var presentIds = source.Items.Select(item => item.ExternalDirectoryId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var items = new List<DirectoryPreviewItemDto>();

        foreach (var record in source.Items)
        {
            var mappedEmail = ReadMappedValue(record, fieldMappings, "email");
            var identityRecord = string.IsNullOrWhiteSpace(mappedEmail) ? record : record with { Email = mappedEmail };
            var match = DirectoryIdentityMatcher.Match(connection.Id, identityRecord, links, users);
            var matchedUser = match.UserId is { } id && userById.TryGetValue(id, out var user) ? user : null;
            var changes = GetUserChanges(record, matchedUser, links.FirstOrDefault(link => link.DirectoryConnectionId == connection.Id && string.Equals(link.ExternalDirectoryId, record.ExternalDirectoryId, StringComparison.OrdinalIgnoreCase)), fieldMappings, orgMappings);
            DirectoryGroupMapping[] relatedGroupMappings = record.GroupMembershipsLoaded
                ? groupMappings.Where(mapping => record.GroupIds.Contains(mapping.ExternalGroupId, StringComparer.OrdinalIgnoreCase)).ToArray()
                : Array.Empty<DirectoryGroupMapping>();
            var invalidRole = relatedGroupMappings.Any(mapping => mapping.RoleId.HasValue && !activeRoles.ContainsKey(mapping.RoleId.Value));
            if (match.IsConflict || invalidRole)
            {
                items.Add(new DirectoryPreviewItemDto("User", record.ExternalDirectoryId, record.DisplayName ?? record.UserPrincipalName ?? record.ExternalDirectoryId,
                    "Conflict", "Review", invalidRole ? "A mapped NEXUS Role is inactive or unavailable." : match.Reason, null, null, changes, false));
                continue;
            }

            if (matchedUser is null)
            {
                var action = ToPreviewAction(policy.MissingUserAction);
                foreach (var mapping in relatedGroupMappings.Where(mapping => mapping.RoleId.HasValue))
                {
                    var roleName = activeRoles.GetValueOrDefault(mapping.RoleId!.Value);
                    if (roleName is not null) changes[$"role:{mapping.RoleId}"] = $"Assign after approved user creation: {roleName}";
                }
                items.Add(new DirectoryPreviewItemDto("User", record.ExternalDirectoryId, record.DisplayName ?? record.UserPrincipalName ?? record.ExternalDirectoryId,
                    "New", action, PolicyReason(policy.MissingUserAction, "The user does not exist in NEXUS."), null, null, changes,
                    policy.MissingUserAction != DirectoryProvisioningActions.Ignore));
                continue;
            }

            if (!record.IsEnabled)
            {
                var action = ToPreviewAction(policy.DisabledUserAction);
                items.Add(new DirectoryPreviewItemDto("User", record.ExternalDirectoryId, record.DisplayName ?? record.UserPrincipalName ?? record.ExternalDirectoryId,
                    "Disabled", action, PolicyReason(policy.DisabledUserAction, "The directory account is disabled."), matchedUser.Id, matchedUser.FullName, changes,
                    policy.DisabledUserAction != DirectoryProvisioningActions.DoNothing));
                continue;
            }

            foreach (var mapping in relatedGroupMappings.Where(mapping => mapping.RoleId.HasValue))
            {
                if (existingRoles.TryGetValue(matchedUser.Id, out var assigned) && assigned.Contains(mapping.RoleId!.Value)) continue;
                var roleName = activeRoles.GetValueOrDefault(mapping.RoleId!.Value);
                if (roleName is not null) changes[$"role:{mapping.RoleId}"] = $"Assign through mapped group: {roleName}";
            }

            var classification = changes.Count == 0 ? "NoChange" : "Changed";
            var proposed = changes.Count == 0 ? "None" : fieldMappings.Any(mapping => mapping.UpdatePolicy == DirectoryUpdatePolicies.Review && changes.ContainsKey(mapping.TargetField)) ? "Review" : "Update";
            items.Add(new DirectoryPreviewItemDto("User", record.ExternalDirectoryId, record.DisplayName ?? record.UserPrincipalName ?? record.ExternalDirectoryId,
                classification, proposed, proposed == "None" ? "No mapped values changed." : PolicyReason(proposed, "Mapped values differ from NEXUS."), matchedUser.Id, matchedUser.FullName, changes, changes.Count > 0));
        }

        if (!source.Truncated)
        {
            var selectedUserIds = ReadValues(scope.SelectedUserIdsJson).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var link in links.Where(link => !presentIds.Contains(link.ExternalDirectoryId)
                && (scope.ScopeType != DirectoryScopeTypes.SelectedUsers || selectedUserIds.Contains(link.ExternalDirectoryId))))
            {
                var action = ToPreviewAction(policy.OutOfScopeUserAction);
                var displayName = link.DisplayName ?? link.UserPrincipalName ?? link.ExternalDirectoryId;
                items.Add(new DirectoryPreviewItemDto("User", link.ExternalDirectoryId, displayName, "Missing", action,
                    PolicyReason(policy.OutOfScopeUserAction, "A linked NEXUS user is no longer present in the configured sync scope."), link.UserId,
                    link.UserId is { } userId && userById.TryGetValue(userId, out var user) ? user.FullName : null,
                    new Dictionary<string, string?>(), policy.OutOfScopeUserAction != DirectoryProvisioningActions.DoNothing));
            }
        }

        return items.OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private async Task<List<DirectoryPreviewItemDto>> BuildGroupPreviewItemsAsync(
        Guid tenantId,
        DirectoryConnection connection,
        (IReadOnlyList<DirectoryGroupRecord> Items, bool Truncated) catalog,
        CancellationToken cancellationToken)
    {
        var scope = await _context.Set<DirectorySyncScope>()
            .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.DirectoryConnectionId == connection.Id && !item.IsDeleted, cancellationToken)
            ?? NewScope(tenantId, connection.Id);
        var policy = await _context.Set<DirectoryProvisioningPolicy>()
            .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.DirectoryConnectionId == connection.Id && !item.IsDeleted, cancellationToken)
            ?? NewPolicy(tenantId, connection.Id);
        var known = await _context.Set<DirectoryGroup>()
            .Where(group => group.TenantId == tenantId && group.DirectoryConnectionId == connection.Id && !group.IsDeleted)
            .Take(MaximumDirectoryItems).ToListAsync(cancellationToken);
        var mappings = await _context.Set<DirectoryGroupMapping>()
            .Where(mapping => mapping.TenantId == tenantId && mapping.DirectoryConnectionId == connection.Id && !mapping.IsDeleted)
            .ToListAsync(cancellationToken);
        var items = new List<DirectoryPreviewItemDto>();
        var present = catalog.Items.Select(group => group.ExternalDirectoryId).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var record in catalog.Items)
        {
            var existing = known.FirstOrDefault(group => string.Equals(group.ExternalDirectoryId, record.ExternalDirectoryId, StringComparison.OrdinalIgnoreCase));
            var mappingConflict = mappings.FirstOrDefault(mapping => mapping.IsActive && string.Equals(mapping.ExternalGroupId, record.ExternalDirectoryId, StringComparison.OrdinalIgnoreCase) && mapping.RoleId.HasValue) is { } roleMapping
                && !await _context.Set<Role>().AnyAsync(role => role.Id == roleMapping.RoleId && role.TenantId == tenantId && !role.IsDeleted, cancellationToken);
            var changes = new Dictionary<string, string?>();
            if (existing is null)
            {
                var missingAction = ToPreviewAction(policy.MissingGroupAction);
                if (mappingConflict)
                    items.Add(new DirectoryPreviewItemDto("Group", record.ExternalDirectoryId, record.Name, "Conflict", "Review", "The mapped NEXUS Role is inactive or unavailable.", null, null, changes, false));
                else
                    items.Add(new DirectoryPreviewItemDto("Group", record.ExternalDirectoryId, record.Name, "New", missingAction, PolicyReason(policy.MissingGroupAction, "The external group reference does not exist in NEXUS."), null, null, changes, policy.MissingGroupAction != DirectoryProvisioningActions.Ignore));
                continue;
            }

            if (!string.Equals(existing.Name, record.Name, StringComparison.Ordinal)) changes["name"] = record.Name;
            if (!string.Equals(existing.Description ?? string.Empty, record.Description ?? string.Empty, StringComparison.Ordinal)) changes["description"] = record.Description;
            var isChanged = changes.Count > 0 || !record.IsEnabled;
            var action = isChanged ? ToPreviewAction(policy.ChangedGroupAction) : "None";
            if (mappingConflict)
                items.Add(new DirectoryPreviewItemDto("Group", record.ExternalDirectoryId, record.Name, "Conflict", "Review", "The mapped NEXUS Role is inactive or unavailable.", null, null, changes, false));
            else
                items.Add(new DirectoryPreviewItemDto("Group", record.ExternalDirectoryId, record.Name, isChanged ? "Changed" : "NoChange", action,
                    isChanged ? PolicyReason(policy.ChangedGroupAction, "Directory group properties changed.") : "No mapped values changed.", null, null, changes,
                    isChanged && policy.ChangedGroupAction != DirectoryProvisioningActions.Ignore));
        }

        if (!catalog.Truncated)
        {
            foreach (var group in known.Where(group => !present.Contains(group.ExternalDirectoryId)))
            {
                var action = ToPreviewAction(policy.MissingGroupReferenceAction);
                items.Add(new DirectoryPreviewItemDto("Group", group.ExternalDirectoryId, group.Name, "Missing", action,
                    PolicyReason(policy.MissingGroupReferenceAction, "A linked external group was not returned by the directory query."), null, null, new Dictionary<string, string?>(),
                    policy.MissingGroupReferenceAction != DirectoryProvisioningActions.DoNothing));
            }
        }
        return items.OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static Dictionary<string, string?> GetUserChanges(
        DirectoryUserRecord source,
        User? user,
        DirectoryUserLink? link,
        IReadOnlyCollection<DirectoryFieldMappingDto> mappings,
        IReadOnlyCollection<DirectoryOrganizationMapping> organizationMappings)
    {
        var changes = new Dictionary<string, string?>();
        foreach (var mapping in mappings.Where(mapping => mapping.UpdatePolicy != DirectoryUpdatePolicies.Ignore))
        {
            if (!source.Attributes.TryGetValue(mapping.SourceAttribute, out var raw)) continue;
            if (string.IsNullOrWhiteSpace(raw) && mapping.TargetField is not ("employeeId" or "organizationalUnit" or "manager")) continue;
            var value = TransformValue(raw, mapping.Transform);
            var current = mapping.TargetField switch
            {
                "firstName" => user?.FirstName,
                "lastName" => user?.LastName,
                "email" => user?.Email,
                "username" => link?.Username ?? link?.UserPrincipalName,
                "jobTitle" => user?.JobTitle,
                "employeeId" => link?.EmployeeId,
                "organizationalUnit" => link?.Department,
                "manager" => link?.ManagerExternalId,
                "accountStatus" => user?.IsActive.ToString().ToLowerInvariant(),
                _ => null
            };
            if (string.Equals(current ?? string.Empty, value ?? string.Empty, StringComparison.OrdinalIgnoreCase)) continue;
            changes[mapping.TargetField] = value;
        }
        var department = ReadMappedValue(source, mappings, "organizationalUnit");
        if (!string.IsNullOrWhiteSpace(department) && mappings.Any(mapping => mapping.TargetField == "organizationalUnit" && mapping.UpdatePolicy != DirectoryUpdatePolicies.Ignore)
            && !organizationMappings.Any(mapping => string.Equals(mapping.DirectoryValue, department, StringComparison.OrdinalIgnoreCase)))
            changes["organizationMapping"] = $"Review required: no NEXUS organizational unit is mapped to '{department}'.";
        if (!string.IsNullOrWhiteSpace(ReadMappedValue(source, mappings, "manager")))
            changes["managerMapping"] = "Manager mapping will be checked when the selected sync action is applied.";
        return changes;
    }

    private static string? TransformValue(string? value, string? transform) => transform switch
    {
        "Trim" => value?.Trim(),
        "Lowercase" => value?.ToLowerInvariant(),
        "Uppercase" => value?.ToUpperInvariant(),
        _ => value
    };

    private static string ToPreviewAction(string policyAction) => policyAction switch
    {
        DirectoryProvisioningActions.AutoCreate => "Create",
        DirectoryProvisioningActions.AutoUpdate => "Update",
        DirectoryProvisioningActions.AutoDeactivate => "Deactivate",
        DirectoryProvisioningActions.Ignore or DirectoryProvisioningActions.DoNothing => "Ignore",
        _ => "Review"
    };

    private static string PolicyReason(string policyAction, string reason) => $"{reason} Provisioning policy: {policyAction}.";

    private sealed record UserRolePreview(Guid UserId, Guid RoleId);
}
