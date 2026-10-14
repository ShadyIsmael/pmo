using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.Identity.Domain;
using NEXUS.Modules.Integrations.Domain;
using NEXUS.Modules.Organization.Domain;
using NEXUS.Modules.ResourceManagement.Domain;

namespace NEXUS.Modules.Integrations.Application;

public sealed partial class DirectoryIntegrationService
{
    private const int MaximumSelectedSyncItems = 500;

    public async Task<DirectorySyncExecutionResultDto> ExecuteSyncAsync(
        Guid tenantId,
        Guid actorId,
        Guid connectionId,
        ExecuteDirectorySyncRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Items.Count is < 1 or > MaximumSelectedSyncItems)
            throw BadRequest("DIRECTORY_SYNC_SELECTION_INVALID", $"Select between 1 and {MaximumSelectedSyncItems} directory records.");

        var connection = await FindConnectionAsync(tenantId, connectionId, cancellationToken);
        if (!connection.IsActive)
            throw BadRequest("DIRECTORY_CONNECTION_INACTIVE", "Activate this directory connection before synchronizing records.");

        var selected = request.Items.Select(NormalizeSelectedItem).ToArray();
        if (selected.Select(item => $"{item.ItemType}\u001f{item.ExternalDirectoryId}").Distinct(StringComparer.OrdinalIgnoreCase).Count() != selected.Length)
            throw BadRequest("DIRECTORY_SYNC_SELECTION_DUPLICATE", "A directory record can be selected only once per synchronization run.");

        var needUsers = selected.Any(item => item.ItemType == "User");
        var needGroups = selected.Any(item => item.ItemType == "Group");
        (IReadOnlyList<DirectoryUserRecord> Items, bool Truncated) users = ([], false);
        (IReadOnlyList<DirectoryGroupRecord> Items, bool Truncated) groups = ([], false);
        var userPreview = new List<DirectoryPreviewItemDto>();
        var groupPreview = new List<DirectoryPreviewItemDto>();

        if (needUsers)
        {
            users = await FetchAllUsersAsync(connection, new DirectoryProviderQuery(1, MaximumPageSize), cancellationToken);
            userPreview = await BuildUserPreviewItemsAsync(tenantId, connection, users, cancellationToken);
        }
        if (needGroups)
        {
            groups = await FetchAllGroupsAsync(connection, new DirectoryProviderQuery(1, MaximumPageSize), MaximumDirectoryItems, cancellationToken);
            groupPreview = await BuildGroupPreviewItemsAsync(tenantId, connection, groups, cancellationToken);
        }

        var previewBySelection = new Dictionary<string, DirectoryPreviewItemDto>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in userPreview) previewBySelection[$"User\u001f{item.ExternalDirectoryId}"] = item;
        foreach (var item in groupPreview) previewBySelection[$"Group\u001f{item.ExternalDirectoryId}"] = item;
        foreach (var item in selected)
        {
            if (!previewBySelection.TryGetValue($"{item.ItemType}\u001f{item.ExternalDirectoryId}", out var preview))
                throw BadRequest("DIRECTORY_SYNC_ITEM_OUT_OF_SCOPE", "One or more selected records are no longer present in the configured directory scope. Refresh the preview.");
            ValidateSelectedAction(item, preview);
            if (item.ItemType == "User" && item.Action == "Deactivate" && users.Truncated)
                throw BadRequest("DIRECTORY_SYNC_INCOMPLETE_SCOPE", "Deactivation is blocked because the directory query reached its configured record limit. Increase or narrow the scope, then preview again.");
            if (item.ItemType == "Group" && item.Action == "Deactivate" && groups.Truncated)
                throw BadRequest("DIRECTORY_SYNC_INCOMPLETE_SCOPE", "Group deactivation is blocked because the directory query reached its configured record limit. Increase or narrow the scope, then preview again.");
        }

        var policy = await _context.Set<DirectoryProvisioningPolicy>()
            .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.DirectoryConnectionId == connectionId && !item.IsDeleted, cancellationToken)
            ?? NewPolicy(tenantId, connectionId);
        var mappings = await _context.Set<DirectoryFieldMapping>()
            .Where(item => item.TenantId == tenantId && item.DirectoryConnectionId == connectionId && !item.IsDeleted)
            .ToListAsync(cancellationToken);
        var fieldMappings = mappings.Count > 0 ? mappings.Select(MapField).ToArray() : DefaultFieldMappings(connection.ProviderType).ToArray();

        var run = new DirectorySyncRun
        {
            TenantId = tenantId,
            DirectoryConnectionId = connectionId,
            TriggeredByUserId = actorId,
            Mode = "Manual",
            Scope = $"Selected:{selected.Length};Users:{(needUsers ? users.Items.Count : 0)};Groups:{(needGroups ? groups.Items.Count : 0)}",
            Status = "Running",
            UsersFetched = needUsers ? users.Items.Count : 0,
            CreatedBy = actorId.ToString("D")
        };
        _context.Add(run);
        await _context.SaveChangesAsync(cancellationToken);

        var userByExternalId = users.Items.ToDictionary(item => item.ExternalDirectoryId, StringComparer.OrdinalIgnoreCase);
        var groupByExternalId = groups.Items.ToDictionary(item => item.ExternalDirectoryId, StringComparer.OrdinalIgnoreCase);
        var persistedItems = new List<DirectorySyncItem>(selected.Length);
        try
        {
            foreach (var item in selected)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var preview = previewBySelection[$"{item.ItemType}\u001f{item.ExternalDirectoryId}"];
                var syncItem = new DirectorySyncItem
                {
                    TenantId = tenantId,
                    DirectorySyncRunId = run.Id,
                    ItemType = item.ItemType,
                    ExternalDirectoryId = item.ExternalDirectoryId,
                    Classification = preview.Classification,
                    ProposedAction = item.Action,
                    MatchedUserId = preview.MatchedUserId,
                    DisplayName = preview.DisplayName,
                    DetailsJson = JsonSerializer.Serialize(preview.Changes),
                    Status = "Skipped",
                    CreatedBy = actorId.ToString("D")
                };

                if (item.Action is "Review" or "Ignore")
                {
                    syncItem.Status = item.Action == "Review" ? "Review" : "Skipped";
                    run.UsersSkipped += item.ItemType == "User" ? 1 : 0;
                }
                else if (item.ItemType == "Group")
                {
                    var record = groupByExternalId[item.ExternalDirectoryId];
                    await ApplyGroupActionAsync(tenantId, actorId, connectionId, record, item.Action, cancellationToken);
                    syncItem.Status = "Applied";
                    if (item.Action == "Create") run.GroupsCreated++;
                    else if (item.Action == "Update") run.GroupsUpdated++;
                    else if (item.Action == "Deactivate") run.GroupsDeactivated++;
                }
                else
                {
                    userByExternalId.TryGetValue(item.ExternalDirectoryId, out var record);
                    var matchedUserId = await ApplyUserActionAsync(
                        tenantId, actorId, connection, record, preview, item.Action, fieldMappings, policy, cancellationToken);
                    syncItem.MatchedUserId = matchedUserId ?? preview.MatchedUserId;
                    syncItem.Status = "Applied";
                    if (item.Action == "Create") run.UsersCreated++;
                    else if (item.Action == "Update") run.UsersUpdated++;
                    else if (item.Action == "Deactivate") run.UsersDeactivated++;
                }

                _context.Add(syncItem);
                persistedItems.Add(syncItem);
                await _context.SaveChangesAsync(cancellationToken);
            }

            run.Status = "Completed";
            run.CompletedAt = DateTime.UtcNow;
            run.UpdatedBy = actorId.ToString("D");
            AddAudit(tenantId, actorId, "DirectorySyncCompleted", "DirectorySyncRun", run.Id, null,
                new { run.DirectoryConnectionId, run.Status, run.UsersFetched, run.UsersCreated, run.UsersUpdated, run.UsersDeactivated, run.UsersSkipped, run.GroupsCreated, run.GroupsUpdated, run.GroupsDeactivated });
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            run.Status = "Failed";
            run.CompletedAt = DateTime.UtcNow;
            run.Failures++;
            run.UpdatedBy = actorId.ToString("D");
            AddAudit(tenantId, actorId, "DirectorySyncFailed", "DirectorySyncRun", run.Id, null,
                new { run.DirectoryConnectionId, run.Status, run.Failures });
            await _context.SaveChangesAsync(CancellationToken.None);
            throw;
        }

        var connectionName = connection.Name;
        var itemDtos = persistedItems.Select(item => new DirectorySyncItemDto(
            item.Id, item.ItemType, item.ExternalDirectoryId, item.DisplayName, item.Classification, item.ProposedAction,
            item.MatchedUserId, item.Status, item.ErrorCode, ReadChanges(item.DetailsJson))).ToArray();
        return new DirectorySyncExecutionResultDto(ToRunDto(run, connectionName), itemDtos);
    }

    private static SelectedDirectorySyncItem NormalizeSelectedItem(SelectedDirectorySyncItem item)
    {
        if (item is null) throw BadRequest("DIRECTORY_SYNC_ITEM_INVALID", "A selected directory record is invalid.");
        var itemType = item.ItemType.Equals("User", StringComparison.OrdinalIgnoreCase) ? "User"
            : item.ItemType.Equals("Group", StringComparison.OrdinalIgnoreCase) ? "Group"
            : throw BadRequest("DIRECTORY_SYNC_ITEM_TYPE_INVALID", "Synchronization items must be Users or Groups.");
        var id = Clean(item.ExternalDirectoryId);
        if (string.IsNullOrWhiteSpace(id) || id.Length > 256)
            throw BadRequest("DIRECTORY_SYNC_EXTERNAL_ID_INVALID", "A selected directory record has an invalid external ID.");
        var action = item.Action.Trim();
        if (action is not ("Create" or "Update" or "Deactivate" or "Review" or "Ignore"))
            throw BadRequest("DIRECTORY_SYNC_ACTION_INVALID", "Choose Create, Update, Deactivate, Review, or Ignore for a selected directory record.");
        return new SelectedDirectorySyncItem(itemType, id, action);
    }

    private static void ValidateSelectedAction(SelectedDirectorySyncItem selected, DirectoryPreviewItemDto preview)
    {
        if (preview.Classification == "Conflict")
            throw BadRequest("DIRECTORY_SYNC_CONFLICT", "Resolve the identity or mapping conflict before applying this directory record.");
        if (selected.Action is "Review" or "Ignore") return;
        if (!preview.Selectable)
            throw BadRequest("DIRECTORY_SYNC_NOT_SELECTABLE", "The current provisioning policy does not allow this item to be synchronized.");

        var valid = selected.ItemType switch
        {
            "User" when preview.Classification == "New" => selected.Action == "Create",
            "User" when preview.Classification == "Changed" => selected.Action == "Update",
            "User" when preview.Classification is "Disabled" or "Missing" => selected.Action == "Deactivate",
            "Group" when preview.Classification == "New" => selected.Action == "Create",
            "Group" when preview.Classification == "Changed" => selected.Action == "Update",
            "Group" when preview.Classification == "Missing" => selected.Action == "Deactivate",
            _ => false
        };
        if (!valid)
            throw BadRequest("DIRECTORY_SYNC_ACTION_MISMATCH", "The selected action does not match the current preview. Refresh the preview and select the suggested action.");
    }

    private async Task<Guid?> ApplyUserActionAsync(
        Guid tenantId,
        Guid actorId,
        DirectoryConnection connection,
        DirectoryUserRecord? source,
        DirectoryPreviewItemDto preview,
        string action,
        IReadOnlyCollection<DirectoryFieldMappingDto> mappings,
        DirectoryProvisioningPolicy policy,
        CancellationToken cancellationToken)
    {
        var link = await _context.Set<DirectoryUserLink>().FirstOrDefaultAsync(item =>
            item.TenantId == tenantId && item.DirectoryConnectionId == connection.Id
            && item.ExternalDirectoryId == preview.ExternalDirectoryId && !item.IsDeleted, cancellationToken);
        var userId = preview.MatchedUserId ?? link?.UserId;
        var user = userId.HasValue
            ? await _context.Set<User>().FirstOrDefaultAsync(item => item.TenantId == tenantId && item.Id == userId && !item.IsDeleted, cancellationToken)
            : null;

        if (action == "Deactivate")
        {
            if (user is not null)
            {
                user.IsActive = false;
                user.UpdatedBy = actorId.ToString("D");
                var linkedResources = await _context.Set<Resource>().Where(resource => resource.TenantId == tenantId && resource.UserId == user.Id && !resource.IsDeleted).ToListAsync(cancellationToken);
                foreach (var linkedResource in linkedResources) { linkedResource.IsActive = false; linkedResource.Status = "Inactive"; linkedResource.UpdatedBy = actorId.ToString("D"); }
                await RemoveDirectoryRoleGrantsAsync(tenantId, connection.Id, user.Id, actorId, cancellationToken);
            }
            if (link is not null)
            {
                link.IsDirectoryEnabled = false;
                link.LastSeenAt = DateTime.UtcNow;
                link.UpdatedBy = actorId.ToString("D");
            }
            return user?.Id ?? link?.UserId;
        }

        if (source is null)
            throw BadRequest("DIRECTORY_SYNC_SOURCE_MISSING", "The selected directory user is not available in the current provider response.");

        var mappedEmail = ReadMappedValue(source, mappings, "email") ?? source.Email ?? source.UserPrincipalName;
        if (string.IsNullOrWhiteSpace(mappedEmail) || !mappedEmail.Contains("@", StringComparison.Ordinal))
            throw BadRequest("DIRECTORY_USER_EMAIL_REQUIRED", "This directory user has no valid email address. Map a valid email field before creating or updating the NEXUS identity.");
        mappedEmail = mappedEmail.Trim();
        if (await _context.Set<User>().AnyAsync(existing => existing.TenantId == tenantId && !existing.IsDeleted && existing.Email == mappedEmail && existing.Id != (user == null ? Guid.Empty : user.Id), cancellationToken))
            throw BadRequest("DIRECTORY_IDENTITY_EMAIL_CONFLICT", "This directory email already belongs to another NEXUS account. Resolve the identity conflict before synchronization.");

        if (action == "Create")
        {
            if (user is not null)
                throw BadRequest("DIRECTORY_USER_ALREADY_LINKED", "The selected record is already linked to a NEXUS user. Refresh the preview.");
            var firstName = ReadMappedValue(source, mappings, "firstName") ?? ReadAttribute(source, "givenName", "firstName");
            var lastName = ReadMappedValue(source, mappings, "lastName") ?? ReadAttribute(source, "surname", "sn", "lastName");
            FillNameFromDisplayName(source.DisplayName, ref firstName, ref lastName);
            if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
                throw BadRequest("DIRECTORY_USER_NAME_REQUIRED", "This directory user needs both a first and last name before a NEXUS account can be created.");

            user = new User
            {
                TenantId = tenantId,
                Email = mappedEmail,
                FirstName = firstName.Trim(),
                LastName = lastName.Trim(),
                JobTitle = ReadMappedValue(source, mappings, "jobTitle") ?? source.JobTitle,
                IsActive = source.IsEnabled,
                // No directory password is imported. This high-entropy value is discarded after hashing;
                // account access must be established through the existing password-reset/SSO process.
                PasswordHash = _passwordHasher.HashPassword(new User(), Convert.ToBase64String(RandomNumberGenerator.GetBytes(48))),
                CreatedBy = actorId.ToString("D")
            };
            _context.Add(user);
            await _context.SaveChangesAsync(cancellationToken);
            userId = user.Id;
        }
        else if (user is null)
        {
            throw BadRequest("DIRECTORY_MATCHED_USER_NOT_FOUND", "The NEXUS account matched during preview is no longer available. Refresh the preview.");
        }

        if (action == "Update")
            await ApplyMappedUserFieldsAsync(tenantId, actorId, user!, source, mappings, cancellationToken);
        user!.UpdatedBy = actorId.ToString("D");
        var synchronizeAccountStatus = mappings.Any(mapping => mapping.TargetField == "accountStatus" && mapping.UpdatePolicy != DirectoryUpdatePolicies.Ignore);
        if (synchronizeAccountStatus) user.IsActive = source.IsEnabled;
        if (link is null)
        {
            link = new DirectoryUserLink
            {
                TenantId = tenantId,
                DirectoryConnectionId = connection.Id,
                ExternalDirectoryId = source.ExternalDirectoryId,
                UserId = user.Id,
                CreatedBy = actorId.ToString("D")
            };
            _context.Add(link);
        }
        link.UserId = user.Id;
        link.IsDirectoryEnabled = source.IsEnabled;
        link.LastSeenAt = DateTime.UtcNow;
        link.UserPrincipalName = source.UserPrincipalName;
        link.Username = HasMappedField(mappings, "username") ? ReadMappedValue(source, mappings, "username") : source.Username;
        link.DisplayName = source.DisplayName;
        link.Email = ReadMappedValue(source, mappings, "email") ?? source.Email;
        link.Department = HasMappedField(mappings, "organizationalUnit") ? ReadMappedValue(source, mappings, "organizationalUnit") : source.Department;
        link.JobTitle = HasMappedField(mappings, "jobTitle") ? ReadMappedValue(source, mappings, "jobTitle") : source.JobTitle;
        link.EmployeeId = HasMappedField(mappings, "employeeId") ? ReadMappedValue(source, mappings, "employeeId") : source.EmployeeId;
        link.ManagerExternalId = HasMappedField(mappings, "manager") ? ReadMappedValue(source, mappings, "manager") : source.ManagerExternalId;
        link.DistinguishedName = source.DistinguishedName;
        link.LastImportedValuesJson = null;
        link.UpdatedBy = actorId.ToString("D");

        if (source.GroupMembershipsLoaded)
        {
            var groups = await _context.Set<DirectoryGroupMapping>()
                .Where(mapping => mapping.TenantId == tenantId && mapping.DirectoryConnectionId == connection.Id && mapping.IsActive && !mapping.IsDeleted && mapping.RoleId.HasValue)
                .ToListAsync(cancellationToken);
            await ApplyDirectoryRoleGrantsAsync(tenantId, actorId, connection.Id, user.Id, source.GroupIds, groups, cancellationToken);
        }

        var resources = await _context.Set<Resource>().Where(resource => resource.TenantId == tenantId && resource.UserId == user.Id && !resource.IsDeleted).ToListAsync(cancellationToken);
        Resource? resource = resources.OrderByDescending(item => item.IsActive).FirstOrDefault();
        var createdResource = false;
        if (resource is null && policy.CreateResource)
        {
            createdResource = true;
            resource = new Resource
            {
                TenantId = tenantId,
                UserId = user.Id,
                Name = user.FullName,
                EmployeeId = ReadMappedValue(source, mappings, "employeeId"),
                JobTitle = user.JobTitle,
                Email = user.Email,
                Type = "Employee",
                Status = source.IsEnabled ? "Active" : "Inactive",
                EmploymentType = policy.DefaultEmploymentType,
                Seniority = policy.DefaultSeniority,
                BaseCalendarId = policy.DefaultBaseCalendarId,
                IsActive = source.IsEnabled,
                CreatedBy = actorId.ToString("D")
            };
            _context.Add(resource);
        }
        else if (resource is not null && action == "Update")
        {
            resource.Name = user.FullName;
            resource.Email = user.Email;
            resource.JobTitle = user.JobTitle;
            if (mappings.Any(mapping => mapping.TargetField == "employeeId" && mapping.UpdatePolicy != DirectoryUpdatePolicies.Ignore))
                resource.EmployeeId = ReadMappedValue(source, mappings, "employeeId");
            if (synchronizeAccountStatus)
            {
                resource.IsActive = user.IsActive;
                resource.Status = user.IsActive ? "Active" : "Inactive";
            }
            resource.UpdatedBy = actorId.ToString("D");
        }

        var warnings = new List<string>();
        if (resource is not null && (action is "Create" or "Update"))
        {
            if ((createdResource && policy.DefaultOrganizationalUnitId.HasValue)
                || mappings.Any(mapping => mapping.TargetField == "organizationalUnit" && mapping.UpdatePolicy != DirectoryUpdatePolicies.Ignore))
                await ApplyDirectoryOrganizationMembershipAsync(tenantId, actorId, connection.Id, ReadMappedValue(source, mappings, "organizationalUnit"), link, resource, createdResource ? policy.DefaultOrganizationalUnitId : null, warnings, cancellationToken);
            if (mappings.Any(mapping => mapping.TargetField == "manager" && mapping.UpdatePolicy != DirectoryUpdatePolicies.Ignore))
                await ApplyDirectoryReportingLineAsync(tenantId, actorId, connection.Id, ReadMappedValue(source, mappings, "manager"), link, resource, warnings, cancellationToken);
        }
        if (warnings.Count > 0)
            link.LastImportedValuesJson = JsonSerializer.Serialize(new { Warnings = warnings });
        return user.Id;
    }

    private async Task ApplyMappedUserFieldsAsync(
        Guid tenantId,
        Guid actorId,
        User user,
        DirectoryUserRecord source,
        IReadOnlyCollection<DirectoryFieldMappingDto> mappings,
        CancellationToken cancellationToken)
    {
        foreach (var mapping in mappings.Where(mapping => mapping.UpdatePolicy != DirectoryUpdatePolicies.Ignore))
        {
            var value = ReadMappedValue(source, mappings, mapping.TargetField);
            if (string.IsNullOrWhiteSpace(value)) continue;
            switch (mapping.TargetField)
            {
                case "firstName": user.FirstName = value; break;
                case "lastName": user.LastName = value; break;
                case "email": user.Email = value; break;
                case "jobTitle": user.JobTitle = value; break;
                case "organizationalUnit":
                case "manager":
                case "username":
                case "employeeId":
                case "accountStatus":
                    break;
            }
        }
        if (string.IsNullOrWhiteSpace(user.FirstName) || string.IsNullOrWhiteSpace(user.LastName))
            throw BadRequest("DIRECTORY_USER_NAME_REQUIRED", "A directory mapping cannot clear a NEXUS user's first or last name.");
        if (string.IsNullOrWhiteSpace(user.Email) || !user.Email.Contains("@", StringComparison.Ordinal))
            throw BadRequest("DIRECTORY_USER_EMAIL_REQUIRED", "A directory mapping cannot clear or invalidate the NEXUS user's email address.");
        var emailInUse = await _context.Set<User>().AnyAsync(existing => existing.TenantId == tenantId && !existing.IsDeleted && existing.Email == user.Email && existing.Id != user.Id, cancellationToken);
        if (emailInUse) throw BadRequest("DIRECTORY_IDENTITY_EMAIL_CONFLICT", "The mapped email already belongs to another NEXUS user.");
        user.UpdatedBy = actorId.ToString("D");
    }

    private async Task ApplyDirectoryRoleGrantsAsync(
        Guid tenantId,
        Guid actorId,
        Guid connectionId,
        Guid userId,
        IReadOnlyCollection<string> externalGroupIds,
        IReadOnlyCollection<DirectoryGroupMapping> mappings,
        CancellationToken cancellationToken)
    {
        var desired = mappings.Where(mapping => mapping.RoleId.HasValue && externalGroupIds.Contains(mapping.ExternalGroupId, StringComparer.OrdinalIgnoreCase))
            .Select(mapping => (mapping.ExternalGroupId, RoleId: mapping.RoleId!.Value))
            .Distinct().ToArray();
        var grants = await _context.Set<DirectoryRoleGrant>()
            .Where(grant => grant.TenantId == tenantId && grant.DirectoryConnectionId == connectionId && grant.UserId == userId && !grant.IsDeleted)
            .ToListAsync(cancellationToken);
        var assignments = await _context.Set<UserRole>()
            .Where(assignment => assignment.TenantId == tenantId && assignment.UserId == userId && !assignment.IsDeleted)
            .ToListAsync(cancellationToken);
        var allManagedGrants = await _context.Set<DirectoryRoleGrant>()
            .Where(grant => grant.TenantId == tenantId && grant.UserId == userId && !grant.IsDeleted)
            .ToListAsync(cancellationToken);
        var directoryOwnedAssignmentIds = allManagedGrants.Where(grant => grant.UserRoleId.HasValue).Select(grant => grant.UserRoleId!.Value).ToHashSet();
        var desiredRoles = desired.Select(item => item.RoleId).ToHashSet();

        foreach (var grant in grants.Where(grant => !desired.Any(item => item.RoleId == grant.RoleId && string.Equals(item.ExternalGroupId, grant.ExternalGroupId, StringComparison.OrdinalIgnoreCase))))
        {
            grant.IsDeleted = true;
            grant.UpdatedAt = DateTime.UtcNow;
            grant.UpdatedBy = actorId.ToString("D");
            allManagedGrants.Remove(grant);
            if (grant.UserRoleId is { } assignmentId && !desiredRoles.Contains(grant.RoleId)
                && !allManagedGrants.Any(other => other.UserRoleId == assignmentId))
            {
                var ownedAssignment = assignments.FirstOrDefault(assignment => assignment.Id == assignmentId);
                if (ownedAssignment is not null)
                {
                    ownedAssignment.IsDeleted = true;
                    ownedAssignment.UpdatedAt = DateTime.UtcNow;
                    ownedAssignment.UpdatedBy = actorId.ToString("D");
                    assignments.Remove(ownedAssignment);
                }
            }
        }

        foreach (var desiredGrant in desired)
        {
            if (grants.Any(grant => !grant.IsDeleted && grant.RoleId == desiredGrant.RoleId && string.Equals(grant.ExternalGroupId, desiredGrant.ExternalGroupId, StringComparison.OrdinalIgnoreCase))) continue;
            if (!await _context.Set<Role>().AnyAsync(role => role.TenantId == tenantId && role.Id == desiredGrant.RoleId && !role.IsDeleted, cancellationToken))
                throw BadRequest("DIRECTORY_ROLE_INVALID", "A mapped NEXUS role is no longer active in the current tenant. Refresh the role mappings.");

            var assignment = assignments.FirstOrDefault(item => item.RoleId == desiredGrant.RoleId && !item.IsDeleted);
            if (assignment is null)
            {
                assignment = new UserRole { TenantId = tenantId, UserId = userId, RoleId = desiredGrant.RoleId, CreatedBy = actorId.ToString("D") };
                _context.Add(assignment);
                assignments.Add(assignment);
                directoryOwnedAssignmentIds.Add(assignment.Id);
            }
            else if (!directoryOwnedAssignmentIds.Contains(assignment.Id))
            {
                // Existing role assignment is user/admin-owned. Do not claim or remove it.
                continue;
            }

            var newGrant = new DirectoryRoleGrant
            {
                TenantId = tenantId,
                DirectoryConnectionId = connectionId,
                ExternalGroupId = desiredGrant.ExternalGroupId,
                UserId = userId,
                RoleId = desiredGrant.RoleId,
                UserRoleId = assignment.Id,
                CreatedBy = actorId.ToString("D")
            };
            _context.Add(newGrant);
            grants.Add(newGrant);
            allManagedGrants.Add(newGrant);
        }
    }

    private async Task RemoveDirectoryRoleGrantsAsync(Guid tenantId, Guid connectionId, Guid userId, Guid actorId, CancellationToken cancellationToken)
    {
        var grants = await _context.Set<DirectoryRoleGrant>()
            .Where(grant => grant.TenantId == tenantId && grant.DirectoryConnectionId == connectionId && grant.UserId == userId && !grant.IsDeleted)
            .ToListAsync(cancellationToken);
        var assignments = await _context.Set<UserRole>()
            .Where(assignment => assignment.TenantId == tenantId && assignment.UserId == userId && !assignment.IsDeleted)
            .ToListAsync(cancellationToken);
        var otherGrants = await _context.Set<DirectoryRoleGrant>()
            .Where(grant => grant.TenantId == tenantId && grant.UserId == userId && grant.DirectoryConnectionId != connectionId && !grant.IsDeleted)
            .ToListAsync(cancellationToken);
        foreach (var grant in grants)
        {
            grant.IsDeleted = true;
            grant.UpdatedAt = DateTime.UtcNow;
            grant.UpdatedBy = actorId.ToString("D");
            if (grant.UserRoleId is { } assignmentId && !otherGrants.Any(other => other.UserRoleId == assignmentId))
            {
                var assignment = assignments.FirstOrDefault(item => item.Id == assignmentId);
                if (assignment is not null)
                {
                    assignment.IsDeleted = true;
                    assignment.UpdatedAt = DateTime.UtcNow;
                    assignment.UpdatedBy = actorId.ToString("D");
                }
            }
        }
    }

    private async Task ApplyGroupActionAsync(Guid tenantId, Guid actorId, Guid connectionId, DirectoryGroupRecord source, string action, CancellationToken cancellationToken)
    {
        var group = await _context.Set<DirectoryGroup>().FirstOrDefaultAsync(item =>
            item.TenantId == tenantId && item.DirectoryConnectionId == connectionId && item.ExternalDirectoryId == source.ExternalDirectoryId && !item.IsDeleted, cancellationToken);
        if (action == "Deactivate")
        {
            if (group is not null)
            {
                group.IsActive = false;
                group.LastSeenAt = DateTime.UtcNow;
                group.UpdatedBy = actorId.ToString("D");
            }
            return;
        }
        if (group is null)
        {
            if (action != "Create") throw BadRequest("DIRECTORY_GROUP_NOT_FOUND", "The selected directory group is no longer in the preview scope.");
            group = new DirectoryGroup { TenantId = tenantId, DirectoryConnectionId = connectionId, ExternalDirectoryId = source.ExternalDirectoryId, CreatedBy = actorId.ToString("D") };
            _context.Add(group);
        }
        group.Name = source.Name;
        group.Description = source.Description;
        group.IsActive = source.IsEnabled;
        group.LastSeenAt = DateTime.UtcNow;
        group.UpdatedBy = actorId.ToString("D");
    }

    private async Task ApplyDirectoryOrganizationMembershipAsync(
        Guid tenantId,
        Guid actorId,
        Guid connectionId,
        string? departmentValue,
        DirectoryUserLink link,
        Resource resource,
        Guid? defaultOrganizationalUnitId,
        ICollection<string> warnings,
        CancellationToken cancellationToken)
    {
        DirectoryOrganizationMapping? mapping = null;
        if (!string.IsNullOrWhiteSpace(departmentValue))
        {
            var availableMappings = await _context.Set<DirectoryOrganizationMapping>()
                .Where(item => item.TenantId == tenantId && item.DirectoryConnectionId == connectionId && !item.IsDeleted)
                .ToListAsync(cancellationToken);
            mapping = availableMappings.FirstOrDefault(item => string.Equals(item.DirectoryValue, departmentValue, StringComparison.OrdinalIgnoreCase));
            if (mapping is null) warnings.Add("Organization department has no configured NEXUS organizational-unit mapping.");
        }
        var targetUnitId = mapping?.OrganizationalUnitId ?? defaultOrganizationalUnitId;
        if (!targetUnitId.HasValue)
        {
            if (string.IsNullOrWhiteSpace(departmentValue) && link.ManagedOrganizationalMembershipId is { } managedId)
            {
                var managedMembership = await _context.Set<ResourceOrganizationalUnit>().FirstOrDefaultAsync(item =>
                    item.TenantId == tenantId && item.Id == managedId && item.ResourceId == resource.Id && !item.IsDeleted && item.IsActive, cancellationToken);
                if (managedMembership is not null)
                {
                    managedMembership.IsActive = false;
                    managedMembership.EffectiveTo = DateTime.UtcNow;
                    managedMembership.UpdatedBy = actorId.ToString("D");
                }
                link.ManagedOrganizationalMembershipId = null;
            }
            return;
        }
        var unit = await _context.Set<OrganizationalUnit>().FirstOrDefaultAsync(item => item.TenantId == tenantId && item.Id == targetUnitId.Value && item.IsActive && !item.IsDeleted, cancellationToken);
        if (unit is null) { warnings.Add("Mapped or default organizational unit is no longer active."); return; }

        var activeMemberships = await _context.Set<ResourceOrganizationalUnit>()
            .Where(item => item.TenantId == tenantId && item.ResourceId == resource.Id && item.IsActive && !item.IsDeleted && item.IsPrimary && (item.EffectiveTo == null || item.EffectiveTo > DateTime.UtcNow))
            .ToListAsync(cancellationToken);
        var managed = link.ManagedOrganizationalMembershipId is { } id ? activeMemberships.FirstOrDefault(item => item.Id == id) : null;
        if (managed is null)
        {
            if (activeMemberships.Any(item => item.OrganizationalUnitId == unit.Id)) return;
            if (activeMemberships.Count > 0) { warnings.Add("A manually-managed primary organizational-unit assignment was preserved."); return; }
            managed = new ResourceOrganizationalUnit
            {
                TenantId = tenantId, ResourceId = resource.Id, OrganizationalUnitId = unit.Id,
                IsPrimary = true, IsActive = true, EffectiveFrom = DateTime.UtcNow, CreatedBy = actorId.ToString("D")
            };
            _context.Add(managed);
            link.ManagedOrganizationalMembershipId = managed.Id;
            return;
        }
        if (managed.OrganizationalUnitId == unit.Id) return;
        managed.IsActive = false;
        managed.EffectiveTo = DateTime.UtcNow;
        managed.UpdatedBy = actorId.ToString("D");
        var replacement = new ResourceOrganizationalUnit
        {
            TenantId = tenantId, ResourceId = resource.Id, OrganizationalUnitId = unit.Id,
            IsPrimary = true, IsActive = true, EffectiveFrom = DateTime.UtcNow, CreatedBy = actorId.ToString("D")
        };
        _context.Add(replacement);
        link.ManagedOrganizationalMembershipId = replacement.Id;
    }

    private async Task ApplyDirectoryReportingLineAsync(
        Guid tenantId,
        Guid actorId,
        Guid connectionId,
        string? managerExternalId,
        DirectoryUserLink link,
        Resource resource,
        ICollection<string> warnings,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(managerExternalId))
        {
            if (link.ManagedReportingLineId is { } managedId)
            {
                var managedLine = await _context.Set<ResourceReportingLine>().FirstOrDefaultAsync(item =>
                    item.TenantId == tenantId && item.Id == managedId && item.ResourceId == resource.Id && !item.IsDeleted && item.IsActive, cancellationToken);
                if (managedLine is not null)
                {
                    managedLine.IsActive = false;
                    managedLine.EffectiveTo = DateTime.UtcNow;
                    managedLine.UpdatedBy = actorId.ToString("D");
                }
                link.ManagedReportingLineId = null;
            }
            return;
        }
        var managerLink = await _context.Set<DirectoryUserLink>().FirstOrDefaultAsync(item =>
            item.TenantId == tenantId && item.DirectoryConnectionId == connectionId
            && (item.ExternalDirectoryId == managerExternalId || item.DistinguishedName == managerExternalId
                || item.UserPrincipalName == managerExternalId || item.Email == managerExternalId)
            && item.UserId.HasValue && item.IsDirectoryEnabled && !item.IsDeleted, cancellationToken);
        if (managerLink?.UserId is not { } managerUserId) { warnings.Add("The directory manager is not linked to an active NEXUS identity/resource."); return; }
        var managerResource = await _context.Set<Resource>().FirstOrDefaultAsync(item => item.TenantId == tenantId && item.UserId == managerUserId && item.IsActive && !item.IsDeleted, cancellationToken);
        if (managerResource is null) { warnings.Add("The directory manager has no active NEXUS resource."); return; }
        if (managerResource.Id == resource.Id) { warnings.Add("A user cannot be their own reporting manager."); return; }

        var activeLines = await _context.Set<ResourceReportingLine>()
            .Where(item => item.TenantId == tenantId && item.IsActive && item.IsPrimary && !item.IsDeleted && (item.EffectiveTo == null || item.EffectiveTo > DateTime.UtcNow))
            .ToListAsync(cancellationToken);
        var managed = link.ManagedReportingLineId is { } lineId ? activeLines.FirstOrDefault(item => item.Id == lineId) : null;
        if (managed is null)
        {
            var manualLine = activeLines.FirstOrDefault(item => item.ResourceId == resource.Id);
            if (manualLine is not null)
            {
                if (manualLine.ManagerResourceId != managerResource.Id) warnings.Add("A manually-managed reporting line was preserved.");
                return;
            }
        }
        else if (managed.ManagerResourceId == managerResource.Id) return;

        if (WouldCreateReportingCycle(resource.Id, managerResource.Id, activeLines))
        {
            warnings.Add("The proposed manager would create a reporting cycle; the existing reporting structure was preserved.");
            return;
        }
        if (managed is not null)
        {
            managed.IsActive = false;
            managed.EffectiveTo = DateTime.UtcNow;
            managed.UpdatedBy = actorId.ToString("D");
        }
        var line = new ResourceReportingLine
        {
            TenantId = tenantId, ResourceId = resource.Id, ManagerResourceId = managerResource.Id,
            ReportingType = "LineManager", IsPrimary = true, IsActive = true, EffectiveFrom = DateTime.UtcNow,
            CreatedBy = actorId.ToString("D")
        };
        _context.Add(line);
        link.ManagedReportingLineId = line.Id;
    }

    private static bool WouldCreateReportingCycle(Guid resourceId, Guid managerResourceId, IReadOnlyCollection<ResourceReportingLine> lines)
    {
        var managerByResource = lines.Where(item => item.ResourceId != resourceId).GroupBy(item => item.ResourceId)
            .ToDictionary(group => group.Key, group => group.First().ManagerResourceId);
        var current = managerResourceId;
        var visited = new HashSet<Guid>();
        while (visited.Add(current))
        {
            if (current == resourceId) return true;
            if (!managerByResource.TryGetValue(current, out current)) return false;
        }
        return true;
    }

    private static string? ReadMappedValue(DirectoryUserRecord source, IReadOnlyCollection<DirectoryFieldMappingDto> mappings, string targetField)
    {
        var mapping = mappings.FirstOrDefault(item => item.TargetField == targetField && item.UpdatePolicy != DirectoryUpdatePolicies.Ignore);
        return mapping is not null && source.Attributes.TryGetValue(mapping.SourceAttribute, out var raw)
            ? TransformValue(raw, mapping.Transform)?.Trim()
            : null;
    }

    private static bool HasMappedField(IReadOnlyCollection<DirectoryFieldMappingDto> mappings, string targetField) =>
        mappings.Any(item => item.TargetField == targetField && item.UpdatePolicy != DirectoryUpdatePolicies.Ignore);

    private static string? ReadAttribute(DirectoryUserRecord source, params string[] keys)
    {
        foreach (var key in keys)
            if (source.Attributes.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)) return value.Trim();
        return null;
    }

    private static void FillNameFromDisplayName(string? displayName, ref string? firstName, ref string? lastName)
    {
        if ((!string.IsNullOrWhiteSpace(firstName) && !string.IsNullOrWhiteSpace(lastName)) || string.IsNullOrWhiteSpace(displayName)) return;
        var parts = displayName.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (string.IsNullOrWhiteSpace(firstName) && parts.Length > 0) firstName = parts[0];
        if (string.IsNullOrWhiteSpace(lastName) && parts.Length > 1) lastName = parts[1];
    }
}
