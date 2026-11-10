using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Data;
using System.Data.Common;
using System.IO;
using System.Reflection;
using System.Text.Json;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.Approvals.Domain;
using NEXUS.Modules.CostManagement.Domain;
using NEXUS.Modules.Documents.Application;
using NEXUS.Modules.Documents.Domain;
using NEXUS.Modules.Identity.Application;
using NEXUS.Modules.Identity.Domain;
using NEXUS.Modules.Identity.Infrastructure;
using NEXUS.Modules.Integrations.Application;
using NEXUS.Modules.IssueManagement.Domain;
using NEXUS.Modules.Meetings.Domain;
using NEXUS.Modules.Meetings.Application;
using NEXUS.Modules.Notifications.Domain;
using NEXUS.Modules.Organization.Application.Settings;
using NEXUS.Modules.Organization.Domain;
using NEXUS.Modules.Portfolio.Domain;
using NEXUS.Modules.ProjectManagement.Application.DTOs;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.Modules.ResourceManagement.Domain;
using NEXUS.Modules.RiskManagement.Domain;
using NEXUS.Modules.WorkItems.Domain;
using NEXUS.SharedKernel;

namespace NEXUS.Persistence;

public static class SeedDataProvider
{
    /// <summary>
    /// Demo tenant used by all seed data. Represents the single-tenant development
    /// deployment (no separate Tenant table exists; tenancy is expressed via TenantId).
    /// </summary>
    public static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateTime SeedTimestampUtc = new(2026, 11, 2, 0, 0, 0, DateTimeKind.Utc);

    public sealed record CanonicalPermissionRebuildResult(
        int AddedCatalogPermissions,
        int NormalizedCatalogPermissions,
        int RetiredLegacyPermissions,
        int RemovedLegacyRoleGrants,
        int RetiredLegacyOverrides,
        int AddedProtectedAdminRoleGrants,
        int RemovedProtectedAdminDenyOverrides,
        string? VerifiedBackupPath);

    public sealed record InitialDelegationPermissionBootstrapResult(
        string Server,
        string Database,
        int DatabaseId,
        int AddedCatalogPermissions,
        int AddedProjectSettingsAdminGrants,
        int AuthorizedAdminUsers,
        Guid AuthorizedAdminUserId);

    public sealed record ProjectPlanPermissionCatalogSyncResult(
        string Server,
        string Database,
        int DatabaseId,
        int AddedCatalogPermissions,
        int NormalizedCatalogPermissions,
        IReadOnlyList<string> ChangedKeys,
        IReadOnlyList<string> UnchangedKeys);

    /// <summary>
    /// Synchronizes only the three canonical Project Planning catalog rows needed for the
    /// create/edit permission split. This operation never changes role grants, memberships,
    /// user permission overrides, or protected system roles.
    /// </summary>
    public static async Task<ProjectPlanPermissionCatalogSyncResult> SynchronizeProjectPlanPermissionsAsync(
        AppDbContext context,
        bool isDevelopment,
        string expectedServer,
        string expectedDatabase,
        CancellationToken cancellationToken = default)
    {
        if (!isDevelopment || !string.Equals(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"), "Development", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Project Plan permission catalog synchronization is available only in the Development environment.");
        if (!string.Equals(expectedServer, "Shady", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(expectedDatabase, "NexusPmo", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Project Plan permission catalog synchronization is restricted to the verified Development Shady/NexusPmo database.");
        if (context.Database.ProviderName != "Microsoft.EntityFrameworkCore.SqlServer")
            throw new InvalidOperationException("Project Plan permission catalog synchronization requires the SQL Server provider.");
        if (context.Database.HasPendingModelChanges())
            throw new InvalidOperationException("The EF model has pending migration changes; run the repository database gate first.");

        var pendingMigrations = (await context.Database.GetPendingMigrationsAsync(cancellationToken)).ToArray();
        if (pendingMigrations.Length > 0)
            throw new InvalidOperationException($"Development schema migrations are pending; run the database gate first: {string.Join(", ", pendingMigrations)}.");

        var connection = context.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere)
            await connection.OpenAsync(cancellationToken);

        string actualServer;
        string actualDatabase;
        int databaseId;
        try
        {
            await using var identityCommand = connection.CreateCommand();
            identityCommand.CommandText = "SELECT CAST(SERVERPROPERTY('ServerName') AS nvarchar(128)), DB_NAME(), DB_ID();";
            await using var reader = await identityCommand.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                throw new InvalidOperationException("SQL Server did not return the connected Development database identity.");

            actualServer = reader.GetString(0);
            actualDatabase = reader.GetString(1);
            databaseId = Convert.ToInt32(reader.GetValue(2));
            if (!string.Equals(actualServer, expectedServer, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(actualDatabase, expectedDatabase, StringComparison.OrdinalIgnoreCase)
                || databaseId <= 0)
                throw new InvalidOperationException($"Connected SQL target '{actualServer}/{actualDatabase}' (database ID {databaseId}) does not match the explicitly verified Development target '{expectedServer}/{expectedDatabase}'.");
        }
        finally
        {
            if (openedHere)
                await connection.CloseAsync();
        }

        var requiredKeys = new HashSet<string>(StringComparer.Ordinal)
        {
            "Planning.View",
            "Planning.Edit",
            "ProjectPlan.Create"
        };
        var planningView = PermissionCatalog.ByKey["Planning.View"];
        var planningEdit = PermissionCatalog.ByKey["Planning.Edit"];
        var projectPlanCreate = PermissionCatalog.ByKey["ProjectPlan.Create"];
        if (!requiredKeys.All(key => PermissionCatalog.ByKey.TryGetValue(key, out var definition) && definition.IsActive && definition.IsAssignable))
            throw new InvalidOperationException("The canonical Project Plan permission set is incomplete, inactive, or not assignable.");
        if (planningEdit.Scope != PermissionScope.Project
            || projectPlanCreate.Scope != PermissionScope.Project
            || !planningEdit.RequiredPermissionKeys.Contains("Planning.View", StringComparer.Ordinal)
            || !projectPlanCreate.RequiredPermissionKeys.Contains("Planning.View", StringComparer.Ordinal))
            throw new InvalidOperationException("Planning.Edit and ProjectPlan.Create must remain Project-scoped and depend on Planning.View.");

        var targetPermissions = PermissionCatalog.CreateSeedPermissions(TenantId)
            .Where(permission => requiredKeys.Contains(permission.Name))
            .ToArray();
        if (targetPermissions.Length != requiredKeys.Count
            || targetPermissions.Select(permission => permission.Name).Distinct(StringComparer.Ordinal).Count() != requiredKeys.Count)
            throw new InvalidOperationException("Canonical seed rows do not exactly match the three approved Project Plan permission keys.");

        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        await AcquireSqlServerAdminMutationLockAsync(context, transaction, TenantId, cancellationToken);

        var targetIds = targetPermissions.Select(permission => permission.Id).ToArray();
        var candidateRows = await context.Permissions
            .IgnoreQueryFilters()
            .Where(permission => targetIds.Contains(permission.Id)
                || requiredKeys.Contains(permission.Name)
                || (permission.Resource == "Planning" && (permission.Action == "View" || permission.Action == "Edit"))
                || (permission.Resource == "ProjectPlan" && permission.Action == "Create"))
            .ToListAsync(cancellationToken);

        var changed = new List<Permission>();
        var originalValues = new List<object>();
        var addedKeys = new List<string>();
        var addedCatalogPermissions = 0;
        var normalizedCatalogPermissions = 0;
        foreach (var seedPermission in targetPermissions.OrderBy(permission => permission.Name, StringComparer.Ordinal))
        {
            var matches = candidateRows
                .Where(permission => permission.Id == seedPermission.Id
                    || string.Equals(permission.Name, seedPermission.Name, StringComparison.Ordinal)
                    || (permission.Resource == seedPermission.Resource && permission.Action == seedPermission.Action))
                .DistinctBy(permission => permission.Id)
                .ToArray();
            if (matches.Length > 1)
                throw new InvalidOperationException($"Duplicate or conflicting persisted catalog rows were found for '{seedPermission.Name}'; no changes were made.");

            if (matches.Length == 0)
            {
                context.Permissions.Add(seedPermission);
                candidateRows.Add(seedPermission);
                changed.Add(seedPermission);
                addedKeys.Add(seedPermission.Name);
                addedCatalogPermissions++;
                continue;
            }

            var persisted = matches[0];
            var persistedKey = $"{persisted.Resource}.{persisted.Action}";
            if (persisted.Id != seedPermission.Id
                || persisted.TenantId != TenantId
                || !string.Equals(persistedKey, seedPermission.Name, StringComparison.Ordinal))
                throw new InvalidOperationException($"Persisted catalog identity for '{seedPermission.Name}' conflicts with the canonical Development entry; no changes were made.");
            if (persisted.IsDeleted)
                throw new InvalidOperationException($"Canonical permission '{seedPermission.Name}' is soft-deleted; restoration requires explicit review.");

            if (!string.Equals(persisted.Name, seedPermission.Name, StringComparison.Ordinal)
                || !string.Equals(persisted.Description, seedPermission.Description, StringComparison.Ordinal))
            {
                originalValues.Add(new { persisted.Id, persisted.Name, persisted.Description });
                persisted.Name = seedPermission.Name;
                persisted.Description = seedPermission.Description;
                persisted.UpdatedAt = DateTime.UtcNow;
                persisted.UpdatedBy = "System";
                changed.Add(persisted);
                normalizedCatalogPermissions++;
            }
        }

        var changedKeys = changed.Select(permission => permission.Name).OrderBy(key => key, StringComparer.Ordinal).ToArray();
        var unchangedKeys = requiredKeys.Except(changedKeys, StringComparer.Ordinal).OrderBy(key => key, StringComparer.Ordinal).ToArray();
        if (changed.Count > 0)
        {
            context.AuditEntries.Add(new AuditEntry
            {
                TenantId = TenantId,
                EntityType = "PermissionCatalog",
                EntityId = changed[0].Id,
                Action = "PermissionCatalog.ProjectPlanEntriesSynchronized",
                OriginalValues = JsonSerializer.Serialize(new { Permissions = originalValues }),
                NewValues = JsonSerializer.Serialize(new { Added = addedKeys.OrderBy(key => key, StringComparer.Ordinal), Synchronized = changedKeys }),
                ChangedFields = "PermissionCatalog",
                Description = "Synchronized only Planning.View, Planning.Edit, and ProjectPlan.Create catalog entries; role grants, memberships, user overrides, and protected system roles were not modified.",
                Source = AuditSources.System,
                UserName = "Scoped Development Project Plan permission catalog synchronization",
                CreatedBy = "System"
            });
            await context.SaveChangesAsync(cancellationToken);
        }

        var verifiedRows = await context.Permissions
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(permission => targetIds.Contains(permission.Id)
                || requiredKeys.Contains(permission.Name)
                || (permission.Resource == "Planning" && (permission.Action == "View" || permission.Action == "Edit"))
                || (permission.Resource == "ProjectPlan" && permission.Action == "Create"))
            .ToArrayAsync(cancellationToken);
        foreach (var seedPermission in targetPermissions)
        {
            var matches = verifiedRows.Where(permission => permission.Name == seedPermission.Name
                    || (permission.Resource == seedPermission.Resource && permission.Action == seedPermission.Action))
                .ToArray();
            if (matches.Length != 1
                || matches[0].Id != seedPermission.Id
                || matches[0].TenantId != TenantId
                || matches[0].IsDeleted
                || !string.Equals(matches[0].Name, seedPermission.Name, StringComparison.Ordinal)
                || !string.Equals(matches[0].Description, seedPermission.Description, StringComparison.Ordinal))
                throw new InvalidOperationException($"Post-sync verification failed for canonical permission '{seedPermission.Name}'.");
        }

        await transaction.CommitAsync(cancellationToken);
        return new ProjectPlanPermissionCatalogSyncResult(
            actualServer,
            actualDatabase,
            databaseId,
            addedCatalogPermissions,
            normalizedCatalogPermissions,
            changedKeys,
            unchangedKeys);
    }

    /// <summary>
    /// Adds only missing permission catalog rows needed by Project Settings and the explicit
    /// financial delegation bootstrap, then grants ProjectSettings.View/Edit additively to
    /// the protected Development Admin role. Financial and annual-budget permissions are
    /// never granted by this operation; authorized role administrators may delegate the
    /// exact allowlisted capabilities through RoleAdministrationService.
    /// </summary>
    public static async Task<InitialDelegationPermissionBootstrapResult> BootstrapInitialDelegationPermissionsAsync(
        AppDbContext context,
        bool isDevelopment,
        string expectedServer,
        string expectedDatabase,
        CancellationToken cancellationToken = default)
    {
        if (!isDevelopment || !string.Equals(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"), "Development", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Initial permission delegation bootstrap is available only in the Development environment.");
        if (string.IsNullOrWhiteSpace(expectedServer) || string.IsNullOrWhiteSpace(expectedDatabase))
            throw new InvalidOperationException("Pass the SQL Server and database identities verified by the Development EF gate.");
        if (context.Database.ProviderName != "Microsoft.EntityFrameworkCore.SqlServer")
            throw new InvalidOperationException("Initial permission delegation bootstrap requires the SQL Server provider.");
        if (context.Database.HasPendingModelChanges())
            throw new InvalidOperationException("The EF model has pending migration changes; run the repository database gate first.");

        var pendingMigrations = (await context.Database.GetPendingMigrationsAsync(cancellationToken)).ToArray();
        if (pendingMigrations.Length > 0)
            throw new InvalidOperationException($"Development schema migrations are pending; run the database gate first: {string.Join(", ", pendingMigrations)}.");

        var connection = context.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere)
            await connection.OpenAsync(cancellationToken);

        string actualServer;
        string actualDatabase;
        int databaseId;
        try
        {
            await using var identityCommand = connection.CreateCommand();
            identityCommand.CommandText = "SELECT CAST(SERVERPROPERTY('ServerName') AS nvarchar(128)), DB_NAME(), DB_ID();";
            await using var reader = await identityCommand.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                throw new InvalidOperationException("SQL Server did not return the connected Development database identity.");

            actualServer = reader.GetString(0);
            actualDatabase = reader.GetString(1);
            databaseId = Convert.ToInt32(reader.GetValue(2));
            if (!string.Equals(actualServer, expectedServer, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(actualDatabase, expectedDatabase, StringComparison.OrdinalIgnoreCase)
                || databaseId <= 0)
                throw new InvalidOperationException($"Connected SQL target '{actualServer}/{actualDatabase}' (database ID {databaseId}) does not match the explicitly verified Development target '{expectedServer}/{expectedDatabase}'.");
        }
        finally
        {
            if (openedHere)
                await connection.CloseAsync();
        }

        var requiredKeys = new HashSet<string>(StringComparer.Ordinal)
        {
            "ProjectSettings.View",
            "ProjectSettings.Edit",
            "ProjectAnnualBudget.View",
            "ProjectAnnualBudget.ManageApprovalConfiguration",
            "ProjectFinancialFoundation.View",
            "CostUtilization.Export",
            "ProgramFinancialRollup.View",
            "PortfolioFinancialRollup.View"
        };
        var settingsKeys = new HashSet<string>(StringComparer.Ordinal)
        {
            "ProjectSettings.View",
            "ProjectSettings.Edit"
        };
        foreach (var key in requiredKeys)
        {
            if (!PermissionCatalog.ByKey.TryGetValue(key, out var definition)
                || !definition.IsActive
                || !definition.IsAssignable)
                throw new InvalidOperationException($"Required delegable permission '{key}' is missing, inactive, or not assignable in the canonical catalog.");
        }
        if (PermissionCatalog.ByKey["ProjectSettings.View"].Scope != PermissionScope.Project
            || PermissionCatalog.ByKey["ProjectSettings.Edit"].Scope != PermissionScope.Project
            || !PermissionCatalog.ByKey["ProjectSettings.Edit"].RequiredPermissionKeys.Contains("ProjectSettings.View", StringComparer.Ordinal))
            throw new InvalidOperationException("Project Settings permissions must remain Project-scoped, with Edit depending on View.");
        if (PermissionCatalog.ByKey["ProjectAnnualBudget.View"].Scope != PermissionScope.Project
            || PermissionCatalog.ByKey["ProjectAnnualBudget.ManageApprovalConfiguration"].Scope != PermissionScope.Project
            || !PermissionCatalog.ByKey["ProjectAnnualBudget.ManageApprovalConfiguration"].RequiredPermissionKeys.Contains("ProjectAnnualBudget.View", StringComparer.Ordinal))
            throw new InvalidOperationException("Annual Budget approval configuration permissions must remain Project-scoped, with configuration management depending on the Annual Budget View permission.");
        if (requiredKeys.Except(settingsKeys, StringComparer.Ordinal).Any(key => ProtectedTenantAdminPolicy.MandatoryPermissionKeys.Contains(key)))
            throw new InvalidOperationException("Sensitive financial permissions cannot be included in the protected tenant Admin baseline.");

        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        await AcquireSqlServerAdminMutationLockAsync(context, transaction, TenantId, cancellationToken);

        var targetPermissions = PermissionCatalog.CreateSeedPermissions(TenantId)
            .Where(permission => requiredKeys.Contains($"{permission.Resource}.{permission.Action}"))
            .ToArray();
        if (targetPermissions.Length != requiredKeys.Count)
            throw new InvalidOperationException("The canonical permission seed rows do not exactly match the requested delegation bootstrap keys.");

        var targetPermissionIds = targetPermissions.Select(permission => permission.Id).ToArray();
        var conflictingSeedRows = await context.Permissions
            .IgnoreQueryFilters()
            .Where(permission => targetPermissionIds.Contains(permission.Id))
            .Select(permission => new { permission.Id, permission.TenantId, permission.Resource, permission.Action })
            .ToArrayAsync(cancellationToken);
        var expectedById = targetPermissions.ToDictionary(permission => permission.Id, permission => $"{permission.Resource}.{permission.Action}");
        if (conflictingSeedRows.Any(row => row.TenantId != TenantId
            || !string.Equals($"{row.Resource}.{row.Action}", expectedById[row.Id], StringComparison.Ordinal)))
            throw new InvalidOperationException("A canonical permission seed ID is already used by another tenant or permission key; no changes were made.");

        var targetResources = targetPermissions.Select(permission => permission.Resource).Distinct(StringComparer.Ordinal).ToArray();
        var tenantRows = (await context.Permissions
            .IgnoreQueryFilters()
            .Where(permission => permission.TenantId == TenantId && targetResources.Contains(permission.Resource))
            .ToListAsync(cancellationToken))
            .Where(permission => requiredKeys.Contains($"{permission.Resource}.{permission.Action}") || !permission.IsDeleted)
            .ToList();
        var duplicateKeys = tenantRows
            .GroupBy(permission => $"{permission.Resource}.{permission.Action}", StringComparer.Ordinal)
            .Where(group => requiredKeys.Contains(group.Key) && group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();
        if (duplicateKeys.Length > 0)
            throw new InvalidOperationException($"Duplicate target permission rows were found; no changes were made: {string.Join(", ", duplicateKeys)}.");
        if (tenantRows.Any(permission => requiredKeys.Contains($"{permission.Resource}.{permission.Action}") && permission.IsDeleted))
            throw new InvalidOperationException("A target canonical permission is soft-deleted; restoring it requires explicit review.");

        var persistedByKey = tenantRows
            .Where(permission => requiredKeys.Contains($"{permission.Resource}.{permission.Action}") && !permission.IsDeleted)
            .ToDictionary(permission => $"{permission.Resource}.{permission.Action}", StringComparer.Ordinal);
        var addedPermissions = new List<Permission>();
        foreach (var seedPermission in targetPermissions)
        {
            var key = $"{seedPermission.Resource}.{seedPermission.Action}";
            if (persistedByKey.ContainsKey(key))
                continue;
            context.Permissions.Add(seedPermission);
            persistedByKey.Add(key, seedPermission);
            addedPermissions.Add(seedPermission);
        }

        var adminRoles = await context.Roles
            .IgnoreQueryFilters()
            .Where(role => role.TenantId == TenantId && !role.IsDeleted
                && role.IsSystem && role.SystemRoleKey == Role.TenantAdminSystemRoleKey)
            .Include(role => role.Permissions)
            .ToArrayAsync(cancellationToken);
        if (adminRoles.Length != 1)
            throw new InvalidOperationException("Initial delegation bootstrap requires exactly one active protected Development Admin role.");

        var adminRole = adminRoles[0];
        var originalRoleKeys = adminRole.Permissions
            .Where(permission => permission.TenantId == TenantId && !permission.IsDeleted)
            .Select(permission => $"{permission.Resource}.{permission.Action}")
            .Distinct(StringComparer.Ordinal)
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();
        var addedProjectSettingsKeys = new List<string>();
        foreach (var key in settingsKeys.OrderBy(key => key, StringComparer.Ordinal))
        {
            var permission = persistedByKey[key];
            if (adminRole.Permissions.Any(linked => linked.Id == permission.Id))
                continue;
            adminRole.Permissions.Add(permission);
            addedProjectSettingsKeys.Add(key);
        }

        if (addedPermissions.Count > 0)
        {
            context.AuditEntries.Add(new AuditEntry
            {
                TenantId = TenantId,
                EntityType = "PermissionCatalog",
                EntityId = addedPermissions[0].Id,
                Action = "PermissionCatalog.InitialDelegationBootstrap",
                OriginalValues = JsonSerializer.Serialize(new { PermissionKeys = Array.Empty<string>() }),
                NewValues = JsonSerializer.Serialize(new { PermissionKeys = addedPermissions.Select(permission => $"{permission.Resource}.{permission.Action}").OrderBy(key => key, StringComparer.Ordinal).ToArray() }),
                ChangedFields = "Permissions",
                Description = "Inserted only missing canonical Project Settings and explicitly delegable financial permission catalog rows.",
                Source = AuditSources.System,
                UserName = "Scoped Development permission delegation bootstrap",
                CreatedBy = "System"
            });
        }
        if (addedProjectSettingsKeys.Count > 0)
        {
            context.AuditEntries.Add(new AuditEntry
            {
                TenantId = TenantId,
                EntityType = "Role",
                EntityId = adminRole.Id,
                Action = "ProtectedAdminProjectSettingsGrants.Bootstrapped",
                OriginalValues = JsonSerializer.Serialize(new { PermissionKeys = originalRoleKeys }),
                NewValues = JsonSerializer.Serialize(new { PermissionKeys = originalRoleKeys.Concat(addedProjectSettingsKeys).Distinct(StringComparer.Ordinal).OrderBy(key => key, StringComparer.Ordinal).ToArray(), Added = addedProjectSettingsKeys.ToArray() }),
                ChangedFields = "Permissions",
                Description = "Added only the approved Project Settings View/Edit role grants; preserved financial operation permissions, other role grants, and user-level Deny overrides.",
                Source = AuditSources.System,
                UserName = "Scoped Development permission delegation bootstrap",
                CreatedBy = "System"
            });
        }

        await context.SaveChangesAsync(cancellationToken);

        var persistedKeys = await context.Permissions
            .IgnoreQueryFilters()
            .Where(permission => permission.TenantId == TenantId && !permission.IsDeleted
                && targetResources.Contains(permission.Resource))
            .Select(permission => permission.Resource + "." + permission.Action)
            .Distinct()
            .ToArrayAsync(cancellationToken);
        if (requiredKeys.Except(persistedKeys, StringComparer.Ordinal).Any())
            throw new InvalidOperationException("Post-save verification did not find every required canonical permission row; the transaction was rolled back.");

        var adminUserIds = await context.UserRoles
            .IgnoreQueryFilters()
            .Where(assignment => assignment.TenantId == TenantId && assignment.RoleId == adminRole.Id && !assignment.IsDeleted
                && assignment.User.TenantId == TenantId && !assignment.User.IsDeleted && assignment.User.IsActive)
            .Select(assignment => assignment.UserId)
            .Distinct()
            .ToArrayAsync(cancellationToken);
        var effectivePermissions = new UserEffectivePermissionService(context);
        var authorizedAdminUsers = 0;
        var authorizedAdminUserId = Guid.Empty;
        foreach (var adminUserId in adminUserIds)
        {
            var permissions = await effectivePermissions.GetEffectivePermissionsAsync(adminUserId, TenantId, cancellationToken);
            if (permissions.Contains("ProjectSettings.View")
                && permissions.Contains("ProjectSettings.Edit")
                && permissions.Contains("Roles.ConfigurePermissions")
                && permissions.Contains("TenantAdministration.ManageScope"))
            {
                authorizedAdminUsers++;
                if (authorizedAdminUserId == Guid.Empty)
                    authorizedAdminUserId = adminUserId;
            }
        }
        if (authorizedAdminUsers == 0)
            throw new InvalidOperationException("No active protected Admin user has effective Project Settings View/Edit, Roles.ConfigurePermissions, and TenantAdministration.ManageScope after preserving all Active Deny overrides; no changes were committed.");

        await transaction.CommitAsync(cancellationToken);
        return new InitialDelegationPermissionBootstrapResult(
            actualServer,
            actualDatabase,
            databaseId,
            addedPermissions.Count,
            addedProjectSettingsKeys.Count,
            authorizedAdminUsers,
            authorizedAdminUserId);
    }

    public static async Task VerifyDevelopmentAdminRecoveryTargetAsync(
        AppDbContext context,
        bool isDevelopment,
        string expectedServer,
        string expectedDatabase,
        CancellationToken cancellationToken = default)
    {
        if (!isDevelopment)
            throw new InvalidOperationException("Development Admin password recovery is available only in the Development environment.");
        if (string.IsNullOrWhiteSpace(expectedServer) || string.IsNullOrWhiteSpace(expectedDatabase))
            throw new InvalidOperationException("Pass the expected SQL Server and database returned by the Development EF gate.");
        if (context.Database.ProviderName != "Microsoft.EntityFrameworkCore.SqlServer")
            throw new InvalidOperationException("Development Admin password recovery requires the SQL Server provider.");
        if (context.Database.HasPendingModelChanges())
            throw new InvalidOperationException("The EF model has pending migration changes; run the repository database gate first.");

        var pendingMigrations = (await context.Database.GetPendingMigrationsAsync(cancellationToken)).ToArray();
        if (pendingMigrations.Length > 0)
            throw new InvalidOperationException($"Development schema migrations are pending; run the database gate before recovery: {string.Join(", ", pendingMigrations)}.");

        var connection = context.Database.GetDbConnection();
        var openedHere = connection.State != System.Data.ConnectionState.Open;
        if (openedHere)
            await connection.OpenAsync(cancellationToken);

        try
        {
            await using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT CAST(SERVERPROPERTY('ServerName') AS nvarchar(128)), DB_NAME(), DB_ID();";
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                if (!await reader.ReadAsync(cancellationToken))
                    throw new InvalidOperationException("SQL Server did not return the Development database identity.");

                var actualServer = reader.GetString(0);
                var actualDatabase = reader.GetString(1);
                var databaseId = Convert.ToInt32(reader.GetValue(2));
                if (!string.Equals(actualServer, expectedServer, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(actualDatabase, expectedDatabase, StringComparison.OrdinalIgnoreCase)
                    || databaseId <= 0)
                    throw new InvalidOperationException($"Connected SQL target '{actualServer}/{actualDatabase}' (database ID {databaseId}) does not match the explicitly verified Development target '{expectedServer}/{expectedDatabase}'.");
            }
        }
        finally
        {
            if (openedHere)
                await connection.CloseAsync();
        }
    }

    public static Task<string?> CreateOptionalDevelopmentAdminRecoveryBackupAsync(
        AppDbContext context,
        string expectedServer,
        string expectedDatabase,
        string? backupDirectory,
        CancellationToken cancellationToken = default)
    {
        return CreateAndVerifyDevelopmentBackupAsync(
            context,
            expectedServer,
            expectedDatabase,
            backupDirectory,
            cancellationToken,
            "admin-recovery");
    }

    /// <summary>
    /// Explicit Development-only reconciliation of the demo tenant's persisted
    /// permission catalog. Canonical grants already assigned to roles or users are
    /// preserved. Unknown/retired grants were already ineffective because runtime
    /// authorization accepts active catalog keys only; their rows are archived.
    /// </summary>
    public static async Task<CanonicalPermissionRebuildResult> RebuildCanonicalPermissionsAsync(
        AppDbContext context,
        string expectedServer,
        string expectedDatabase,
        string? backupDirectory,
        CancellationToken cancellationToken = default)
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"), "Development", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Canonical permission reconciliation is available only in the Development environment.");
        if (string.IsNullOrWhiteSpace(expectedServer) || string.IsNullOrWhiteSpace(expectedDatabase))
            throw new InvalidOperationException("Pass the SQL Server and database identities returned by the Development EF gate.");
        if (context.Database.ProviderName != "Microsoft.EntityFrameworkCore.SqlServer")
            throw new InvalidOperationException("Canonical permission reconciliation requires the SQL Server provider.");

        if (context.Database.HasPendingModelChanges())
            throw new InvalidOperationException("The EF model has pending migration changes; run the repository database gate before permission reconciliation.");
        var pendingMigrations = (await context.Database.GetPendingMigrationsAsync(cancellationToken)).ToArray();
        if (pendingMigrations.Length > 0)
            throw new InvalidOperationException($"Development schema migrations are pending; apply only after the database gate: {string.Join(", ", pendingMigrations)}.");

        var verifiedBackupPath = await CreateAndVerifyDevelopmentBackupAsync(
            context, expectedServer, expectedDatabase, backupDirectory, cancellationToken);

        var activeCatalog = PermissionCatalog.Entries
            .Where(entry => entry.IsActive)
            .ToDictionary(entry => $"{entry.Resource}|{entry.Action}", StringComparer.Ordinal);
        if (activeCatalog.Count == 0)
            throw new InvalidOperationException("The active permission catalog is empty; reconciliation was stopped.");

        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        await AcquireSqlServerAdminMutationLockAsync(context, transaction, TenantId, cancellationToken);
        var tenantPermissions = await context.Permissions
            .IgnoreQueryFilters()
            .Where(permission => permission.TenantId == TenantId)
            .ToListAsync(cancellationToken);

        var duplicatePairs = tenantPermissions
            .GroupBy(permission => $"{permission.Resource}|{permission.Action}", StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();
        if (duplicatePairs.Length > 0)
            throw new InvalidOperationException($"Canonical permission reconciliation found duplicate Tenant permission rows: {string.Join(", ", duplicatePairs)}.");

        var catalogSeedPermissions = PermissionCatalog.CreateSeedPermissions(TenantId)
            .Where(permission => activeCatalog.ContainsKey($"{permission.Resource}|{permission.Action}"))
            .ToArray();
        var seedIds = catalogSeedPermissions.Select(permission => permission.Id).ToArray();
        var conflictingSeedRows = await context.Permissions
            .IgnoreQueryFilters()
            .Where(permission => seedIds.Contains(permission.Id))
            .Select(permission => new { permission.Id, permission.TenantId, permission.Resource, permission.Action })
            .ToArrayAsync(cancellationToken);
        var expectedPairsBySeedId = catalogSeedPermissions.ToDictionary(
            permission => permission.Id,
            permission => $"{permission.Resource}|{permission.Action}");
        var conflictingSeedIds = conflictingSeedRows
            .Where(row => row.TenantId != TenantId
                || !string.Equals($"{row.Resource}|{row.Action}", expectedPairsBySeedId[row.Id], StringComparison.Ordinal))
            .Select(row => row.Id)
            .Distinct()
            .ToArray();
        if (conflictingSeedIds.Length > 0)
            throw new InvalidOperationException($"Canonical permission seed IDs already belong to another Tenant or catalog entry: {string.Join(", ", conflictingSeedIds)}.");

        var persistedByPair = tenantPermissions.ToDictionary(
            permission => $"{permission.Resource}|{permission.Action}", StringComparer.Ordinal);
        var addedCatalogPermissions = 0;
        var normalizedCatalogPermissions = 0;
        foreach (var seedPermission in catalogSeedPermissions)
        {
            var pair = $"{seedPermission.Resource}|{seedPermission.Action}";
            if (persistedByPair.TryGetValue(pair, out var persisted))
            {
                if (persisted.IsDeleted)
                    throw new InvalidOperationException($"Active catalog permission '{seedPermission.Name}' is soft-deleted; restoration requires explicit review.");

                if (!string.Equals(persisted.Name, seedPermission.Name, StringComparison.Ordinal)
                    || !string.Equals(persisted.Description, seedPermission.Description, StringComparison.Ordinal))
                {
                    persisted.Name = seedPermission.Name;
                    persisted.Description = seedPermission.Description;
                    persisted.UpdatedAt = DateTime.UtcNow;
                    persisted.UpdatedBy = "System";
                    normalizedCatalogPermissions++;
                }
                continue;
            }

            context.Permissions.Add(seedPermission);
            persistedByPair.Add(pair, seedPermission);
            tenantPermissions.Add(seedPermission);
            addedCatalogPermissions++;
        }

        var activePairs = activeCatalog.Keys.ToHashSet(StringComparer.Ordinal);
        var retiredPermissions = tenantPermissions
            .Where(permission => !activePairs.Contains($"{permission.Resource}|{permission.Action}") && !permission.IsDeleted)
            .ToArray();
        var retiredPermissionIds = retiredPermissions.Select(permission => permission.Id).ToHashSet();

        var roles = await context.Roles
            .IgnoreQueryFilters()
            .Where(role => role.TenantId == TenantId)
            .Include(role => role.Permissions)
            .ToListAsync(cancellationToken);
        var protectedAdminRoles = roles
            .Where(role => role.TenantId == TenantId
                && ProtectedTenantAdminPolicy.IsProtectedSystemAdminRole(role)
                && !role.IsDeleted)
            .ToArray();
        if (protectedAdminRoles.Length != 1)
            throw new InvalidOperationException("Canonical permission reconciliation requires exactly one active, trusted tenant Admin role.");

        var protectedAdminRole = protectedAdminRoles[0];
        var mandatoryPermissionRows = ProtectedTenantAdminPolicy.MandatoryPermissionKeys
            .OrderBy(key => key, StringComparer.Ordinal)
            .Select(key =>
            {
                var definition = PermissionCatalog.ByKey[key];
                var pair = $"{definition.Resource}|{definition.Action}";
                if (!persistedByPair.TryGetValue(pair, out var permission) || permission.IsDeleted)
                    throw new InvalidOperationException($"Protected Admin baseline permission '{key}' is missing or retired in the active Tenant catalog.");
                return (Key: key, Permission: permission);
            })
            .ToArray();
        var protectedAdminProjectSettingsPermissionRows = ProtectedAdminProjectSettingsPermissionKeys
            .OrderBy(key => key, StringComparer.Ordinal)
            .Select(key =>
            {
                if (!PermissionCatalog.ByKey.TryGetValue(key, out var definition)
                    || !definition.IsActive
                    || definition.Scope != PermissionScope.Project)
                    throw new InvalidOperationException($"Supplemental protected Admin permission '{key}' must be an active Project-scoped catalog permission.");
                var pair = $"{definition.Resource}|{definition.Action}";
                if (!persistedByPair.TryGetValue(pair, out var permission) || permission.IsDeleted)
                    throw new InvalidOperationException($"Supplemental protected Admin permission '{key}' is missing or retired in the active Tenant catalog.");
                return (Key: key, Permission: permission);
            })
            .ToArray();
        var originalProtectedAdminKeys = protectedAdminRole.Permissions
            .Where(permission => permission.TenantId == TenantId && !permission.IsDeleted)
            .Select(permission => $"{permission.Resource}.{permission.Action}")
            .Distinct(StringComparer.Ordinal)
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();
        var originalProtectedAdminKeySet = originalProtectedAdminKeys.ToHashSet(StringComparer.Ordinal);
        var addedProtectedAdminKeys = mandatoryPermissionRows
            .Where(item => !originalProtectedAdminKeySet.Contains(item.Key))
            .Select(item => item.Key)
            .ToArray();
        var addedProtectedAdminProjectSettingsKeys = protectedAdminProjectSettingsPermissionRows
            .Where(item => !originalProtectedAdminKeySet.Contains(item.Key))
            .Select(item => item.Key)
            .ToArray();
        foreach (var item in mandatoryPermissionRows.Where(item => addedProtectedAdminKeys.Contains(item.Key, StringComparer.Ordinal))
                     .Concat(protectedAdminProjectSettingsPermissionRows.Where(item => addedProtectedAdminProjectSettingsKeys.Contains(item.Key, StringComparer.Ordinal))))
        {
            if (!protectedAdminRole.Permissions.Any(linked => linked.Id == item.Permission.Id))
                protectedAdminRole.Permissions.Add(item.Permission);
        }

        var allAddedProtectedAdminKeys = addedProtectedAdminKeys
            .Concat(addedProtectedAdminProjectSettingsKeys)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();
        if (allAddedProtectedAdminKeys.Length > 0)
        {
            var updatedProtectedAdminKeys = originalProtectedAdminKeys
                .Concat(allAddedProtectedAdminKeys)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(key => key, StringComparer.Ordinal)
                .ToArray();
            context.AuditEntries.Add(new AuditEntry
            {
                TenantId = TenantId,
                EntityType = "Role",
                EntityId = protectedAdminRole.Id,
                Action = "ProtectedAdminPermissions.Reconciled",
                OriginalValues = JsonSerializer.Serialize(new { PermissionKeys = originalProtectedAdminKeys }),
                NewValues = JsonSerializer.Serialize(new { PermissionKeys = updatedProtectedAdminKeys, Added = allAddedProtectedAdminKeys }),
                ChangedFields = "Permissions",
                Description = "Reconciled the protected tenant Admin baseline and approved additive Project Settings grants without changing unrelated grants.",
                Source = AuditSources.System,
                UserName = "Canonical Development permission reconciliation",
                CreatedBy = "System"
            });
        }

        var protectedAdminMemberIds = await context.UserRoles
            .IgnoreQueryFilters()
            .Where(assignment => assignment.TenantId == TenantId
                && assignment.RoleId == protectedAdminRole.Id
                && !assignment.IsDeleted
                && assignment.User.TenantId == TenantId
                && !assignment.User.IsDeleted)
            .Select(assignment => assignment.UserId)
            .Distinct()
            .ToArrayAsync(cancellationToken);
        var mandatoryPermissionIds = mandatoryPermissionRows.Select(item => item.Permission.Id).ToArray();
        var protectedAdminDenies = protectedAdminMemberIds.Length == 0
            ? new List<UserPermissionOverride>()
            : await context.UserPermissionOverrides
                .IgnoreQueryFilters()
                .Where(item => item.TenantId == TenantId
                    && !item.IsDeleted
                    && protectedAdminMemberIds.Contains(item.UserId)
                    && mandatoryPermissionIds.Contains(item.PermissionId)
                    && item.Effect == PermissionOverrideEffect.Deny)
                .ToListAsync(cancellationToken);
        foreach (var denial in protectedAdminDenies)
        {
            denial.Effect = PermissionOverrideEffect.Inherit;
            denial.IsDeleted = true;
            denial.UpdatedAt = DateTime.UtcNow;
            denial.UpdatedBy = "System";
            context.AuditEntries.Add(new AuditEntry
            {
                TenantId = TenantId,
                EntityType = "UserPermissionOverride",
                EntityId = denial.Id,
                Action = "PermissionOverride.ProtectedAdminDenyReconciled",
                OriginalValues = JsonSerializer.Serialize(new { denial.UserId, denial.PermissionId, Effect = PermissionOverrideEffect.Deny.ToString() }),
                NewValues = JsonSerializer.Serialize(new { denial.IsDeleted, Effect = PermissionOverrideEffect.Inherit.ToString(), ProtectedAdminBaseline = true }),
                ChangedFields = "Effect,IsDeleted",
                Description = "Archived a conflicting Deny override for a mandatory protected tenant Admin permission.",
                Source = AuditSources.System,
                UserName = "Canonical Development permission reconciliation",
                CreatedBy = "System"
            });
        }

        var removedLegacyRoleGrants = 0;
        foreach (var role in roles)
        {
            var legacyGrants = role.Permissions
                .Where(permission => permission.TenantId == TenantId && retiredPermissionIds.Contains(permission.Id))
                .ToArray();
            if (legacyGrants.Length == 0)
                continue;

            var removedKeys = legacyGrants
                .Select(permission => $"{permission.Resource}.{permission.Action}")
                .OrderBy(key => key, StringComparer.Ordinal)
                .ToArray();
            foreach (var permission in legacyGrants)
            {
                role.Permissions.Remove(permission);
                removedLegacyRoleGrants++;
            }

            context.AuditEntries.Add(new AuditEntry
            {
                TenantId = TenantId,
                EntityType = "Role",
                EntityId = role.Id,
                Action = "Role.RetiredPermissionGrantsRemoved",
                OriginalValues = JsonSerializer.Serialize(new { RoleName = role.Name, Permissions = removedKeys }),
                NewValues = JsonSerializer.Serialize(new { RoleName = role.Name, RemainingLegacyPermissionGrants = 0 }),
                ChangedFields = "Permissions",
                Description = "Removed role links to retired permissions that were excluded from effective authorization by the active catalog.",
                Source = AuditSources.System,
                UserName = "Canonical Development permission reconciliation",
                CreatedBy = "System"
            });
        }

        var retiredOverrides = retiredPermissionIds.Count == 0
            ? new List<UserPermissionOverride>()
            : await context.UserPermissionOverrides
                .IgnoreQueryFilters()
                .Where(item => item.TenantId == TenantId && retiredPermissionIds.Contains(item.PermissionId) && !item.IsDeleted)
                .ToListAsync(cancellationToken);
        foreach (var item in retiredOverrides)
        {
            var originalEffect = item.Effect;
            item.Effect = PermissionOverrideEffect.Inherit;
            item.IsDeleted = true;
            item.UpdatedAt = DateTime.UtcNow;
            item.UpdatedBy = "System";
            context.AuditEntries.Add(new AuditEntry
            {
                TenantId = TenantId,
                EntityType = "UserPermissionOverride",
                EntityId = item.Id,
                Action = "PermissionOverride.CanonicalCatalogReconciled",
                OriginalValues = JsonSerializer.Serialize(new { item.UserId, item.PermissionId, Effect = originalEffect.ToString() }),
                NewValues = JsonSerializer.Serialize(new { item.IsDeleted, Effect = PermissionOverrideEffect.Inherit.ToString() }),
                ChangedFields = "Effect,IsDeleted",
                Description = "Archived a direct override for a retired permission catalog entry.",
                Source = AuditSources.System,
                UserName = "Canonical Development permission reconciliation",
                CreatedBy = "System"
            });
        }

        foreach (var permission in retiredPermissions)
        {
            permission.IsDeleted = true;
            permission.UpdatedAt = DateTime.UtcNow;
            permission.UpdatedBy = "System";
            context.AuditEntries.Add(new AuditEntry
            {
                TenantId = TenantId,
                EntityType = "Permission",
                EntityId = permission.Id,
                Action = "Permission.CatalogRetired",
                OriginalValues = JsonSerializer.Serialize(new { permission.Name, permission.Resource, permission.Action }),
                NewValues = JsonSerializer.Serialize(new { permission.IsDeleted, CatalogActive = false }),
                ChangedFields = "IsDeleted",
                Description = "Soft-deleted a Tenant permission that is not present in the active canonical catalog.",
                Source = AuditSources.System,
                UserName = "Canonical Development permission reconciliation",
                CreatedBy = "System"
            });
        }

        if (addedCatalogPermissions + normalizedCatalogPermissions + retiredPermissions.Length
            + removedLegacyRoleGrants + retiredOverrides.Count + allAddedProtectedAdminKeys.Length
            + protectedAdminDenies.Count > 0)
            await context.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return new CanonicalPermissionRebuildResult(
            addedCatalogPermissions,
            normalizedCatalogPermissions,
            retiredPermissions.Length,
            removedLegacyRoleGrants,
            retiredOverrides.Count,
            allAddedProtectedAdminKeys.Length,
            protectedAdminDenies.Count,
            verifiedBackupPath);
    }

    private static async Task<string?> CreateAndVerifyDevelopmentBackupAsync(
        AppDbContext context,
        string expectedServer,
        string expectedDatabase,
        string? backupDirectory,
        CancellationToken cancellationToken,
        string backupPurpose = "permissions")
    {
        var connection = context.Database.GetDbConnection();
        var openedHere = connection.State != System.Data.ConnectionState.Open;
        if (openedHere)
            await connection.OpenAsync(cancellationToken);

        try
        {
            await using (var identityCommand = connection.CreateCommand())
            {
                identityCommand.CommandText = "SELECT CAST(SERVERPROPERTY('ServerName') AS nvarchar(128)), DB_NAME(), DB_ID();";
                await using var reader = await identityCommand.ExecuteReaderAsync(cancellationToken);
                if (!await reader.ReadAsync(cancellationToken))
                    throw new InvalidOperationException("SQL Server did not return the connected Development identity.");

                var actualServer = reader.GetString(0);
                var actualDatabase = reader.GetString(1);
                var databaseId = reader.GetInt32(2);
                if (!string.Equals(actualServer, expectedServer, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(actualDatabase, expectedDatabase, StringComparison.OrdinalIgnoreCase)
                    || databaseId <= 0)
                    throw new InvalidOperationException($"Connected SQL target '{actualServer}/{actualDatabase}' (database ID {databaseId}) does not match the explicitly verified Development target '{expectedServer}/{expectedDatabase}'.");
            }

            if (string.IsNullOrWhiteSpace(backupDirectory))
                return null;
            if (!Path.IsPathFullyQualified(backupDirectory))
                throw new InvalidOperationException("An optional backup directory must be an absolute SQL Server-accessible path.");

            var backupPath = Path.Combine(backupDirectory,
                $"NexusPmo-{backupPurpose}-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.bak");
            await ExecuteSqlServerBackupAsync(connection, backupPath, verifyOnly: false, cancellationToken);
            await ExecuteSqlServerBackupAsync(connection, backupPath, verifyOnly: true, cancellationToken);
            return backupPath;
        }
        finally
        {
            if (openedHere)
                await connection.CloseAsync();
        }
    }

    private static async Task ExecuteSqlServerBackupAsync(
        DbConnection connection,
        string backupPath,
        bool verifyOnly,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        var pathParameter = command.CreateParameter();
        pathParameter.ParameterName = "@backupPath";
        pathParameter.DbType = DbType.String;
        pathParameter.Size = 4000;
        pathParameter.Value = backupPath;
        command.Parameters.Add(pathParameter);
        command.CommandText = verifyOnly
            ? "RESTORE VERIFYONLY FROM DISK = @backupPath WITH CHECKSUM;"
            : "DECLARE @backupSql nvarchar(max) = N'BACKUP DATABASE ' + QUOTENAME(DB_NAME()) + N' TO DISK = @backupPath WITH COPY_ONLY, CHECKSUM, INIT'; EXEC sys.sp_executesql @backupSql, N'@backupPath nvarchar(4000)', @backupPath = @backupPath;";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static readonly Guid AdminUserId = SeededDevelopmentAdmin.UserId;
    private static readonly Guid ExecutiveUserId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid PortfolioManagerUserId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private static readonly Guid ProjectManagerUserId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
    private static readonly Guid TeamMemberUserId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
    private static readonly Guid PmoAdminUserId = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");
    private static readonly Guid FinanceControllerUserId = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly Guid ResourceManagerUserId = Guid.Parse("22222222-3333-4444-5555-666666666666");

    private static readonly Guid SeedProjectMarkerId = Guid.Parse("e0000001-0000-0000-0000-000000000001");

    private static readonly Guid AdminRoleId = Guid.Parse("a0000001-0000-0000-0000-000000000001");
    private static readonly Guid ExecutiveRoleId = Guid.Parse("a0000002-0000-0000-0000-000000000002");
    private static readonly Guid PortfolioManagerRoleId = Guid.Parse("a0000003-0000-0000-0000-000000000003");
    private static readonly Guid ProjectManagerRoleId = Guid.Parse("a0000004-0000-0000-0000-000000000004");
    private static readonly Guid TeamMemberRoleId = Guid.Parse("a0000005-0000-0000-0000-000000000005");
    private static readonly Guid PmoAdminRoleId = Guid.Parse("a0000006-0000-0000-0000-000000000006");
    private static readonly Guid FinanceControllerRoleId = Guid.Parse("a0000007-0000-0000-0000-000000000007");
    private static readonly Guid ResourceManagerRoleId = Guid.Parse("a0000008-0000-0000-0000-000000000008");
    private static readonly HashSet<Type> MinimalSeedAllowedEntityTypes =
    [
        typeof(Tenant),
        typeof(Organization),
        typeof(User),
        typeof(Role),
        typeof(Permission),
        typeof(UserRole),
        typeof(Lookup),
        typeof(Currency),
        typeof(Skill),
        typeof(DocumentCategory),
        typeof(ConfigurationSetting),
        typeof(AuditEntry)
    ];
    private static readonly MethodInfo HasAnyRowsAsyncMethod = typeof(SeedDataProvider)
        .GetMethod(nameof(HasAnyRowsAsync), BindingFlags.NonPublic | BindingFlags.Static)!;

    // These Project-scoped grants are additive to the tenant Admin's protected
    // Administration baseline. They are not tenant-wide baseline permissions and
    // never suppress an explicit user Deny.
    private static readonly string[] ProtectedAdminProjectSettingsPermissionKeys =
    [
        "ProjectSettings.Edit",
        "ProjectSettings.View"
    ];

    // Approved supplementary Tenant Admin grants outside the dynamic protected baseline. The Administration and Portfolio/Program baseline is catalog-derived, so future eligible entries require no seed-map edit.
    private static readonly string[] AdminRoleSeedPermissionKeys =
    [
        "Audit|View",
        "BudgetRevision|View",
        "ChangeRequest|Approve",
        "ChangeRequest|Manage",
        "ChangeRequest|View",
        "Dashboard|View",
        "Directory|ExecuteSync",
        "Directory|ManageMappings",
        "Directory|ManageProvisioningPolicy",
        "Directory|PreviewSync",
        "Directory|TestConnection",
        "Directory|View",
        "Document|Archive",
        "Document|Create",
        "Document|Edit",
        "Document|TemplateManage",
        "Document|View",
        "ProjectFinance|View",
        "Governance.AssignmentRecovery|Initiate",
        "Governance.AssignmentRecovery|Manage",
        "Governance.AssignmentRecovery|View",
        "Handover|AdminInitiate",
        "Handover|Approve",
        "Handover|Manage",
        "Handover|View",
        "Meeting|Create",
        "Meeting|Delete",
        "Meeting|Edit",
        "Meeting|Manage",
        "Meeting|View",
        "Planning|Lifecycle",
        "Planning|View",
        "Portfolio|Archive",
        "Portfolio|Create",
        "Portfolio|Edit",
        "Portfolio|Reactivate",
        "Portfolio|View",
        "Program|Archive",
        "Program|Create",
        "Program|Edit",
        "Program|Reactivate",
        "Program|View",
        "Project|Create",
        "Project|Delete",
        "Project|Edit",
        "Project|View",
        "ProjectAnnualBudgetClosing|View",
        "ProjectPaymentRequest|Cancel",
        "ProjectPaymentRequest|Create",
        "ProjectPaymentRequest|EditDraft",
        "ProjectPaymentRequest|Submit",
        "ProjectPaymentRequest|View",
        "ReferenceData|View",
        "Report|View",

        "Resource|Assign",
        "Resource|Create",
        "Resource|Delete",
        "Resource|Edit",
        "Resource|EditCostRate",

        "Resource|View",
        "Resource|ViewCostRate",
        "Risk|Create",
        "Risk|Delete",
        "Risk|Edit",
        "Risk|Mitigate",
        "Risk|View",
        "Roles|ConfigurePermissions",
        "Roles|Create",
        "Roles|Delete",
        "Roles|EditDetails",
        "Roles|View",
        "Settings|ManageTenantConfiguration",
        "Settings|View",
        "Stakeholders|Manage",
        "Stakeholders|Read",
        "StatusReport|Create",
        "StatusReport|Edit",
        "StatusReport|Export",
        "StatusReport|ManageConfiguration",
        "StatusReport|Publish",
        "StatusReport|View",
        "Team|Create",
        "Team|Delete",
        "Team|Edit",
        "Team|View",
        "TenantAdministration|ManageScope",
        "TimeTracking|ManageOwn",
        "TimeTracking|ManageTeam",
        "TimeTracking|ViewOwn",
        "TimeTracking|ViewTeam",
        "UserPermissions|Manage",
        "Users|AssignRoles",
        "Users|ManageAccounts",
        "Users|View",
        "Users|SetStatus",
        "Workflow|Manage",
        "Workflow|Publish",
        "Workflow|View",
        "WorkItem|Create",
        "WorkItem|Delete",
        "WorkItem|Edit",
        "WorkItem|View"
    ];

private static readonly Guid Portfolio1Id = Guid.Parse("a0000001-0000-0000-0000-000000000001");
    private static readonly Guid Portfolio2Id = Guid.Parse("a0000001-0000-0000-0000-000000000002");
    private static readonly Guid Portfolio3Id = Guid.Parse("a0000001-0000-0000-0000-000000000003");
    private static readonly Guid Portfolio4Id = Guid.Parse("a0000001-0000-0000-0000-000000000004");

    public static async Task SeedAsync(AppDbContext context, int temporaryPasswordExpiryHours = 24)
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"), "Development", StringComparison.OrdinalIgnoreCase))
            return;
        ValidateTemporaryPasswordExpiryHours(temporaryPasswordExpiryHours);

        // Seed helpers save in several batches. Keep the complete initialization
        // atomic on relational stores so a failed seed cannot leave a half-built
        // tenant, role catalog, or project graph. In-memory unit contexts do not
        // support transactions and continue to execute the same deterministic path.
        await using var transaction = context.Database.IsRelational()
            ? await context.Database.BeginTransactionAsync(IsolationLevel.Serializable)
            : null;
        try
        {
            await SeedIdentityAsync(context, temporaryPasswordExpiryHours);

            await EnsurePortfolioAndProgramSeedDataAsync(context);
            await EnsureOrganizationSeedDataAsync(context);
            await EnsureWorkItemStatusSeedDataAsync(context);
            await EnsureCurrencySeedDataAsync(context);
            await EnsureVendorAndOutsourcedSeedDataAsync(context);
            await EnsureDocumentCatalogAsync(context);
            // Project seed data used to be reset on every Development startup. That
            // reset cascaded into user-created Risks (and their workflow children),
            // leaving previously returned Risk IDs permanently stale after a restart.
            // Seed project data once and leave it, including user-owned records, intact.
            // An explicit opt-in remains available for rebuilding a disposable demo DB.
            var resetRequested = string.Equals(Environment.GetEnvironmentVariable("NEXUS_RESET_PROJECT_SEED"), "true", StringComparison.OrdinalIgnoreCase);
            var hasSeedProject = await context.Projects.IgnoreQueryFilters()
                .AnyAsync(project => project.TenantId == TenantId && project.Id == SeedProjectMarkerId);
            if (hasSeedProject && !resetRequested)
            {
                if (transaction is not null)
                    await transaction.CommitAsync();
                return;
            }

            if (resetRequested && hasSeedProject)
                await ResetProjectSeedDataAsync(context);

            var activeSeedManagerIds = await context.Users.IgnoreQueryFilters()
                .Where(user => user.TenantId == TenantId && user.IsActive && !user.IsDeleted)
                .Select(user => user.Id)
                .ToHashSetAsync();
            Guid? seededProjectManagerId = activeSeedManagerIds.Contains(ProjectManagerUserId)
                ? ProjectManagerUserId
                : null;

            var projectDefinitions = new[]
            {
                new { Id = Guid.Parse("e0000001-0000-0000-0000-000000000001"), Code = "PRJ-1001", Name = "Core Banking Modernization", Status = "InProgress", Priority = "Critical", Phase = "Implementation", Health = "Amber", RiskRating = "High", ConfidenceLevel = "Amber", StartDate = new DateTime(2026, 1, 15), EndDate = new DateTime(2026, 12, 15), Budget = 1800000m, Progress = 62m, Description = "Modernize the retail banking platform and replace legacy transaction services.", BusinessCase = "Increase processing resilience and reduce manual reconciliation across core ledgers.", Notes = "Critical regulatory timeline with executive oversight.", PortfolioId = Portfolio1Id, ProgramId = Guid.Parse("d0000001-0000-0000-0000-000000000001"), DepartmentId = Guid.Parse("a1000001-0000-0000-0000-000000000001") },
                new { Id = Guid.Parse("e0000002-0000-0000-0000-000000000002"), Code = "PRJ-1002", Name = "Enterprise ERP Implementation", Status = "Planning", Priority = "High", Phase = "Planning", Health = "Green", RiskRating = "Medium", ConfidenceLevel = "Green", StartDate = new DateTime(2026, 2, 1), EndDate = new DateTime(2027, 2, 28), Budget = 1450000m, Progress = 24m, Description = "Deploy a global ERP platform across finance, procurement, and supply chain.", BusinessCase = "Standardize operations and improve reporting consistency across business units.", Notes = "Scope prioritization currently underway.", PortfolioId = Portfolio1Id, ProgramId = Guid.Parse("d0000002-0000-0000-0000-000000000002"), DepartmentId = Guid.Parse("a1000002-0000-0000-0000-000000000002") },
                new { Id = Guid.Parse("e0000003-0000-0000-0000-000000000003"), Code = "PRJ-1003", Name = "Customer Mobile Application", Status = "InProgress", Priority = "High", Phase = "Implementation", Health = "Green", RiskRating = "Medium", ConfidenceLevel = "Green", StartDate = new DateTime(2026, 3, 15), EndDate = new DateTime(2026, 10, 30), Budget = 980000m, Progress = 58m, Description = "Launch the next-generation mobile experience for retail and small business customers.", BusinessCase = "Improve digital adoption and reduce customer servicing costs.", Notes = "Release candidate is scheduled for August.", PortfolioId = Portfolio3Id, ProgramId = Guid.Parse("d0000006-0000-0000-0000-000000000006"), DepartmentId = Guid.Parse("a1000003-0000-0000-0000-000000000003") },
                new { Id = Guid.Parse("e0000004-0000-0000-0000-000000000004"), Code = "PRJ-1004", Name = "Cloud Infrastructure Migration", Status = "Completed", Priority = "Medium", Phase = "Deployment", Health = "Green", RiskRating = "Low", ConfidenceLevel = "Green", StartDate = new DateTime(2025, 11, 1), EndDate = new DateTime(2026, 6, 30), Budget = 760000m, Progress = 100m, Description = "Migrate core workloads to the new cloud landing zone and modernize network services.", BusinessCase = "Reduce operational cost and improve disaster recovery readiness.", Notes = "Post-cutover stabilization completed successfully.", PortfolioId = Portfolio2Id, ProgramId = Guid.Parse("d0000004-0000-0000-0000-000000000004"), DepartmentId = Guid.Parse("a1000004-0000-0000-0000-000000000004") },
                new { Id = Guid.Parse("e0000005-0000-0000-0000-000000000005"), Code = "PRJ-1005", Name = "Cybersecurity Improvement Program", Status = "OnHold", Priority = "Critical", Phase = "AnalysisAndDesign", Health = "Red", RiskRating = "High", ConfidenceLevel = "Red", StartDate = new DateTime(2026, 4, 1), EndDate = new DateTime(2027, 4, 15), Budget = 1250000m, Progress = 33m, Description = "Upgrade identity controls, endpoint protection, and security monitoring capabilities.", BusinessCase = "Strengthen compliance posture and reduce cyber exposure across the enterprise.", Notes = "Vendor onboarding is pending executive approval.", PortfolioId = Portfolio4Id, ProgramId = Guid.Parse("d0000007-0000-0000-0000-000000000007"), DepartmentId = Guid.Parse("a1000004-0000-0000-0000-000000000004") },
                new { Id = Guid.Parse("e0000006-0000-0000-0000-000000000006"), Code = "PRJ-1006", Name = "Data Warehouse and Business Intelligence", Status = "InProgress", Priority = "High", Phase = "Implementation", Health = "Amber", RiskRating = "Medium", ConfidenceLevel = "Amber", StartDate = new DateTime(2026, 5, 1), EndDate = new DateTime(2027, 2, 15), Budget = 1100000m, Progress = 47m, Description = "Consolidate reporting datasets and deliver executive analytics using a modern warehouse platform.", BusinessCase = "Improve decision quality through trusted data and self-service analytics.", Notes = "Data quality remediation remains the main dependency.", PortfolioId = Portfolio1Id, ProgramId = Guid.Parse("d0000003-0000-0000-0000-000000000003"), DepartmentId = Guid.Parse("a1000002-0000-0000-0000-000000000002") },
                new { Id = Guid.Parse("e0000007-0000-0000-0000-000000000007"), Code = "PRJ-1007", Name = "Digital Document Management", Status = "Planning", Priority = "Medium", Phase = "Planning", Health = "Green", RiskRating = "Low", ConfidenceLevel = "Green", StartDate = new DateTime(2026, 6, 1), EndDate = new DateTime(2026, 11, 30), Budget = 620000m, Progress = 18m, Description = "Implement a centralized document repository with workflow automation and retention controls.", BusinessCase = "Improve compliance and reduce storage and retrieval costs.", Notes = "Current focus is policy alignment and integration strategy.", PortfolioId = Portfolio1Id, ProgramId = Guid.Parse("d0000002-0000-0000-0000-000000000002"), DepartmentId = Guid.Parse("a1000003-0000-0000-0000-000000000003") },
                new { Id = Guid.Parse("e0000008-0000-0000-0000-000000000008"), Code = "PRJ-1008", Name = "Contact Center Transformation", Status = "InProgress", Priority = "High", Phase = "Testing", Health = "Amber", RiskRating = "Medium", ConfidenceLevel = "Amber", StartDate = new DateTime(2026, 7, 1), EndDate = new DateTime(2027, 1, 31), Budget = 890000m, Progress = 71m, Description = "Upgrade the contact center with omnichannel routing and workforce management integrations.", BusinessCase = "Improve service levels and lower average handle times.", Notes = "Pilot group feedback is being incorporated into final workflows.", PortfolioId = Portfolio2Id, ProgramId = Guid.Parse("d0000005-0000-0000-0000-000000000005"), DepartmentId = Guid.Parse("a1000001-0000-0000-0000-000000000001") },
                new { Id = Guid.Parse("e0000009-0000-0000-0000-000000000009"), Code = "PRJ-1009", Name = "HR Management System", Status = "Closed", Priority = "Medium", Phase = "Closure", Health = "Green", RiskRating = "Low", ConfidenceLevel = "Green", StartDate = new DateTime(2025, 9, 1), EndDate = new DateTime(2026, 3, 31), Budget = 540000m, Progress = 100m, Description = "Deploy an integrated HR system for employee lifecycle management and analytics.", BusinessCase = "Streamline process automation and improve reporting across HR operations.", Notes = "Benefits tracking is in place and lessons learned were captured.", PortfolioId = Portfolio3Id, ProgramId = Guid.Parse("d0000006-0000-0000-0000-000000000006"), DepartmentId = Guid.Parse("a1000002-0000-0000-0000-000000000002") },
                new { Id = Guid.Parse("e0000010-0000-0000-0000-000000000010"), Code = "PRJ-1010", Name = "Corporate Network Upgrade", Status = "InProgress", Priority = "High", Phase = "Implementation", Health = "Amber", RiskRating = "Medium", ConfidenceLevel = "Amber", StartDate = new DateTime(2026, 8, 1), EndDate = new DateTime(2027, 5, 31), Budget = 1320000m, Progress = 39m, Description = "Upgrade campus and branch networking, security controls, and remote access connectivity.", BusinessCase = "Improve uptime, simplify support, and strengthen remote user experience.", Notes = "Hardware procurement is in progress and will gate the next wave.", PortfolioId = Portfolio2Id, ProgramId = Guid.Parse("d0000004-0000-0000-0000-000000000004"), DepartmentId = Guid.Parse("a1000004-0000-0000-0000-000000000004") }
            };

            var projects = projectDefinitions.Select(definition => new Project
            {
                Id = definition.Id,
                TenantId = TenantId,
                Name = definition.Name,
                Code = definition.Code,
                Description = definition.Description,
                Status = definition.Status,
                Priority = definition.Priority,
                ProgramId = definition.ProgramId,
                PortfolioId = definition.PortfolioId,
                DepartmentId = definition.DepartmentId,
                ProjectManagerId = seededProjectManagerId,
                SponsorId = ExecutiveUserId,
                StartDate = definition.StartDate,
                EndDate = definition.EndDate,
                Budget = definition.Budget,
                Currency = "GBP",
                Progress = definition.Progress,
                Health = definition.Health,
                RagStatus = definition.Health,
                ConfidenceLevel = definition.ConfidenceLevel,
                Phase = definition.Phase,
                RiskRating = definition.RiskRating,
                BusinessCase = definition.BusinessCase,
                Notes = definition.Notes,
                CreatedBy = "System"
            }).ToList();

            context.Projects.AddRange(projects);

            var projectManagerAssignments = new List<ProjectManagerAssignment>();
            var handoverProjects = new[]
            {
                new { Index = 0, PreviousManagerId = Guid.Parse("90000001-0000-0000-0000-000000000000"), HandoverDate = new DateTime(2026, 4, 1), Reason = "Reassigned to lead ERP program delivery" },
                new { Index = 3, PreviousManagerId = Guid.Parse("90000001-0001-0000-0000-000000000000"), HandoverDate = new DateTime(2026, 2, 1), Reason = "Transitioned to the infrastructure programme portfolio" }
            };

            for (var index = 0; index < projects.Count; index++)
            {
                var project = projects[index];
                var handover = handoverProjects.FirstOrDefault(h => h.Index == index);
                if (!project.ProjectManagerId.HasValue)
                    continue;

                if (handover is null || !activeSeedManagerIds.Contains(handover.PreviousManagerId))
                {
                    projectManagerAssignments.Add(new ProjectManagerAssignment
                    {
                        Id = CreateDeterministicGuid("5f000001", index, 0, 5),
                        TenantId = TenantId,
                        ProjectId = project.Id,
                        ProjectManagerId = project.ProjectManagerId!.Value,
                        StartDate = project.StartDate!.Value,
                        IsCurrent = true,
                        CreatedBy = "System"
                    });
                }
                else
                {
                    projectManagerAssignments.Add(new ProjectManagerAssignment
                    {
                        Id = CreateDeterministicGuid("5f000001", index, 0, 5),
                        TenantId = TenantId,
                        ProjectId = project.Id,
                        ProjectManagerId = handover.PreviousManagerId,
                        StartDate = project.StartDate!.Value,
                        EndDate = handover.HandoverDate,
                        IsCurrent = false,
                        CreatedBy = "System"
                    });

                    projectManagerAssignments.Add(new ProjectManagerAssignment
                    {
                        Id = CreateDeterministicGuid("5f000001", index, 1, 5),
                        TenantId = TenantId,
                        ProjectId = project.Id,
                        ProjectManagerId = project.ProjectManagerId!.Value,
                        StartDate = handover.HandoverDate,
                        IsCurrent = true,
                        HandoverFromProjectManagerId = handover.PreviousManagerId,
                        HandoverDate = handover.HandoverDate,
                        HandoverReason = handover.Reason,
                        CreatedBy = "System"
                    });
                }
            }

            // Project team seeding is best-effort: an empty catalog is a valid state, so
            // skip members/allocations instead of failing the whole seed.
            var resourceCatalog = await context.Resources.IgnoreQueryFilters().Where(resource => resource.TenantId == TenantId).OrderBy(resource => resource.Name).ToListAsync();
            var hasResourceCatalog = resourceCatalog.Count > 0;

            var projectTeamTemplates = new[]
            {
                new[] { ("Project Manager", 100m, 40m), ("Business Analyst", 60m, 20m), ("Technical Lead", 80m, 25m), ("Developer", 70m, 30m), ("QA Engineer", 40m, 15m) },
                new[] { ("Project Manager", 90m, 35m), ("Business Analyst", 55m, 18m), ("Technical Lead", 75m, 24m), ("Developer", 65m, 28m), ("QA Engineer", 35m, 12m) },
                new[] { ("Project Manager", 95m, 38m), ("Business Analyst", 50m, 18m), ("Technical Lead", 70m, 22m), ("Developer", 60m, 26m), ("QA Engineer", 30m, 10m) },
                new[] { ("Project Manager", 85m, 32m), ("Business Analyst", 45m, 16m), ("Technical Lead", 65m, 20m), ("Developer", 55m, 24m), ("QA Engineer", 30m, 10m) },
                new[] { ("Project Manager", 80m, 30m), ("Business Analyst", 40m, 14m), ("Technical Lead", 60m, 18m), ("Developer", 50m, 22m), ("QA Engineer", 25m, 8m) },
                new[] { ("Project Manager", 75m, 28m), ("Business Analyst", 35m, 12m), ("Technical Lead", 55m, 16m), ("Developer", 45m, 20m), ("QA Engineer", 20m, 8m) },
                new[] { ("Project Manager", 70m, 26m), ("Business Analyst", 30m, 12m), ("Technical Lead", 50m, 16m), ("Developer", 40m, 18m), ("QA Engineer", 25m, 8m) },
                new[] { ("Project Manager", 65m, 24m), ("Business Analyst", 25m, 10m), ("Technical Lead", 45m, 14m), ("Developer", 35m, 16m), ("QA Engineer", 20m, 8m) },
                new[] { ("Project Manager", 60m, 22m), ("Business Analyst", 20m, 8m), ("Technical Lead", 40m, 12m), ("Developer", 30m, 14m), ("QA Engineer", 15m, 6m) },
                new[] { ("Project Manager", 55m, 20m), ("Business Analyst", 20m, 8m), ("Technical Lead", 35m, 12m), ("Developer", 25m, 12m), ("QA Engineer", 15m, 6m) }
            };

            var projectMembers = new List<ProjectMember>();
            var projectAllocations = new List<ResourceAllocation>();
            var projectPhases = new List<ProjectPhase>();
            var projectMilestones = new List<Milestone>();

            for (var index = 0; index < projects.Count; index++)
            {
                var project = projects[index];
                var template = projectTeamTemplates[index];
                for (var memberIndex = 0; hasResourceCatalog && memberIndex < template.Length; memberIndex++)
                {
                    var (roleName, allocationPercentage, hoursPerWeek) = template[memberIndex];
                    var resource = resourceCatalog[(index + memberIndex) % resourceCatalog.Count];
                    projectMembers.Add(new ProjectMember
                    {
                        Id = CreateDeterministicGuid("f0000001", index, memberIndex, 1),
                        TenantId = TenantId,
                        ProjectId = project.Id,
                        ResourceId = resource.Id,
                        Role = roleName,
                        AllocationPercentage = allocationPercentage,
                        HoursPerWeek = hoursPerWeek,
                        StartDate = project.StartDate!.Value,
                        EndDate = project.EndDate!.Value,
                        Status = allocationPercentage >= 60m ? "Active" : "Tentative",
                        IsBillable = true,
                        Notes = $"{roleName} allocation for {project.Name}",
                        CreatedBy = "System"
                    });

                    projectAllocations.Add(new ResourceAllocation
    {
                            Id = CreateDeterministicGuid("9f000001", index, memberIndex, 2),
                            TenantId = TenantId,
                            ResourceId = resource.Id,
                            ProjectId = project.Id,
                            ProjectName = project.Name,
                            ProjectRoleId = null,
                            AllocationPercentage = allocationPercentage,
                            HoursPerWeek = hoursPerWeek,
                            StartDate = project.StartDate!.Value,
                            EndDate = project.EndDate!.Value,
                            Status = ResourceAllocation.StatusConfirmed,
                            IsBillable = true,
                            Notes = $"Allocation for {project.Name}",
                            CreatedBy = "System"
                        });
                }

                var phaseNames = new[] { "Initiation", "Planning", "Analysis and Design", "Implementation", "Testing", "Deployment", "Closure" };
                for (var phaseIndex = 0; phaseIndex < 4; phaseIndex++)
                {
                    projectPhases.Add(new ProjectPhase
                    {
                        Id = CreateDeterministicGuid("8f000001", index, phaseIndex, 3),
                        TenantId = TenantId,
                        ProjectId = project.Id,
                        Name = phaseNames[phaseIndex],
                        Description = $"{project.Name} phase {phaseNames[phaseIndex]}",
                        SortOrder = phaseIndex + 1,
                        Status = phaseIndex < 3 ? "Complete" : "InProgress",
                        StartDate = project.StartDate,
                        EndDate = project.EndDate,
                        Progress = phaseIndex < 3 ? 100m : project.Progress,
                        CreatedBy = "System"
                    });
                }

                var milestoneNames = new[] { "Kickoff", "Design Approval", "Go Live" };
                foreach (var milestoneName in milestoneNames)
                {
                    projectMilestones.Add(new Milestone
                    {
                        Id = CreateDeterministicGuid("7f000001", index, milestoneNames.ToList().IndexOf(milestoneName), 4),
                        TenantId = TenantId,
                        ProjectId = project.Id,
                        Name = $"{project.Name} - {milestoneName}",
                        DueDate = project.EndDate!.Value.AddDays(-30 + (projectMilestones.Count % 3) * 10),
                        Status = milestoneName == "Go Live" && project.Status == "Completed" ? "Completed" : milestoneName == "Go Live" ? "Planned" : "Completed",
                        Category = milestoneName == "Kickoff" ? "Gateway" : milestoneName == "Design Approval" ? "Delivery" : "Delivery",
                        Progress = milestoneName == "Go Live" ? project.Progress : 100m,
                        CreatedBy = "System"
                    });
                }
            }

            // Ensure Alex Admin's canonical Resource (Mina Patel) has at least one Active ProjectMember
            // for the current work date so timer finalization can succeed.
            var adminResource = resourceCatalog.FirstOrDefault(r => r.UserId == AdminUserId);
            if (adminResource is not null)
            {
            var activeProject = projects.FirstOrDefault(p => p.Status == "InProgress" && p.StartDate <= SeedTimestampUtc && p.EndDate >= SeedTimestampUtc)
                                   ?? projects.FirstOrDefault(p => p.Status == "InProgress")
                                   ?? projects.FirstOrDefault();
                if (activeProject is not null)
                {
                    var existingAdminMember = projectMembers.FirstOrDefault(m => m.ResourceId == adminResource.Id && m.ProjectId == activeProject.Id);
                    if (existingAdminMember is null)
                    {
                        projectMembers.Add(new ProjectMember
                        {
                            Id = CreateDeterministicGuid("f0000100", 0, 0, 0),
                            TenantId = TenantId,
                            ProjectId = activeProject.Id,
                            ResourceId = adminResource.Id,
                            Role = "Project Administrator",
                            AllocationPercentage = 100m,
                            HoursPerWeek = 40m,
                            StartDate = activeProject.StartDate!.Value,
                            EndDate = activeProject.EndDate!.Value,
                            Status = "Active",
                            IsBillable = true,
                            Notes = "Admin resource membership for time tracking UAT",
                            CreatedBy = "System"
                        });
                    }
                    else
                    {
                        existingAdminMember.Status = "Active";
                        existingAdminMember.AllocationPercentage = 100m;
                        existingAdminMember.HoursPerWeek = 40m;
                    }
                }
            }

            context.ProjectMembers.AddRange(projectMembers);
            context.ResourceAllocations.AddRange(projectAllocations);
            context.ProjectPhases.AddRange(projectPhases);
            context.Milestones.AddRange(projectMilestones);
            context.ProjectManagerAssignments.AddRange(projectManagerAssignments);

            var projectSnapshots = new List<ProjectSnapshot>();
            foreach (var handover in handoverProjects)
            {
                var project = projects[handover.Index];
                if (!project.ProjectManagerId.HasValue || !activeSeedManagerIds.Contains(handover.PreviousManagerId))
                    continue;
                var projectMembersForProject = projectMembers.Where(m => m.ProjectId == project.Id).ToList();
                var allocationsForProject = projectAllocations.Where(a => a.ProjectId == project.Id).ToList();
                var phasesForProject = projectPhases.Where(p => p.ProjectId == project.Id).ToList();
                var milestonesForProject = projectMilestones.Where(m => m.ProjectId == project.Id).ToList();

                var resourceNames = resourceCatalog.ToDictionary(r => r.Id, r => r.Name);

                projectSnapshots.Add(new ProjectSnapshot
                {
                    Id = CreateDeterministicGuid("6f000001", handover.Index, 0, 6),
                    TenantId = TenantId,
                    ProjectId = project.Id,
                    SnapshotNumber = 1,
                    SnapshotName = "Handover Snapshot #1",
                    SnapshotType = "ManagerHandover",
                    SnapshotDate = handover.HandoverDate,
                    EffectiveEndDate = handover.HandoverDate,
                    PreviousProjectManagerId = handover.PreviousManagerId,
                    NewProjectManagerId = project.ProjectManagerId!.Value,
                    HandoverReason = handover.Reason,
                    HandoverNotes = $"Seeded handover snapshot captured at {handover.HandoverDate:yyyy-MM-dd} when {handover.Reason}.",
                    Name = project.Name,
                    Code = project.Code,
                    Description = project.Description,
                    ProgramId = project.ProgramId,
                    Status = project.Status,
                    Priority = project.Priority,
                    Category = project.Category,
                    SponsorId = project.SponsorId,
                    PortfolioId = project.PortfolioId,
                    DepartmentId = project.DepartmentId,
                    StartDate = project.StartDate,
                    EndDate = project.EndDate,
                    ActualStartDate = project.ActualStartDate,
                    ActualEndDate = project.ActualEndDate,
                    Budget = project.Budget,
                    Currency = project.Currency,
                    ActualCost = project.ActualCost,
                    PlannedValue = project.PlannedValue,
                    EarnedValue = project.EarnedValue,
                    CostVariance = null,
                    ScheduleVariance = null,
                    Progress = project.Progress,
                    Health = project.Health,
                    RagStatus = project.RagStatus,
                    ConfidenceLevel = project.ConfidenceLevel,
                    Phase = project.Phase,
                    RiskRating = project.RiskRating,
                    BusinessCase = project.BusinessCase,
                    Notes = project.Notes,
                    Data = BuildSeedSnapshotData(project, projectMembersForProject, allocationsForProject, phasesForProject, milestonesForProject, resourceNames),
                    CreatedBy = "System"
                });
            }
            context.ProjectSnapshots.AddRange(projectSnapshots);

            // Seed a single pending handover request on a project that does not already have a snapshot,
            // demonstrating the approval-based handover flow. The selected new manager is a seeded
            // project manager account; no account password is stored in seed source.
            var pendingRequestProjectIndex = 2;
            var pendingProject = projects[pendingRequestProjectIndex];
            var pendingNewManagerId = Guid.Parse("90000001-0002-0000-0000-000000000000");
            var pendingRequestExists = await context.ProjectHandoverRequests.IgnoreQueryFilters()
                .AnyAsync(r => r.ProjectId == pendingProject.Id, cancellationToken: default);
            if (!pendingRequestExists
                && pendingProject.ProjectManagerId.HasValue
                && activeSeedManagerIds.Contains(pendingNewManagerId)
                && activeSeedManagerIds.Contains(PmoAdminUserId))
            {
                var pendingRequest = new ProjectHandoverRequest
                {
                    Id = CreateDeterministicGuid("5f000002", pendingRequestProjectIndex, 0, 6),
                    TenantId = TenantId,
                    ProjectId = pendingProject.Id,
                    CurrentProjectManagerId = pendingProject.ProjectManagerId!.Value,
                    NewProjectManagerId = pendingNewManagerId,
                    EffectiveHandoverDate = new DateTime(2026, 9, 1),
                    Reason = "Transitioning to a dedicated digital delivery lead",
                    Notes = "Seeded pending handover request awaiting the new manager's acceptance.",
                    Status = ProjectHandoverRequest.Statuses.Pending,
                    RequestedBy = PmoAdminUserId,
                    RequestedAt = SeedTimestampUtc,
                    CreatedBy = "System"
                };
                context.ProjectHandoverRequests.Add(pendingRequest);
            }

            await context.SaveChangesAsync();
            if (transaction is not null)
                await transaction.CommitAsync();
        }
        catch
        {
            if (transaction is not null)
                await transaction.RollbackAsync();
            throw;
        }
    }

    private static async Task EnsureWorkItemStatusSeedDataAsync(AppDbContext context)
    {
        var definitions = new[]
        {
            new { Code = "Backlog", NameEn = "Backlog", NameAr = "المتراكم", DisplayOrder = 10, Color = "slate", IsDefault = false },
            new { Code = "ToDo", NameEn = "To Do", NameAr = "للتنفيذ", DisplayOrder = 20, Color = "blue", IsDefault = true },
            new { Code = "InProgress", NameEn = "In Progress", NameAr = "قيد التنفيذ", DisplayOrder = 30, Color = "amber", IsDefault = false },
            new { Code = "InReview", NameEn = "In Review", NameAr = "قيد المراجعة", DisplayOrder = 40, Color = "violet", IsDefault = false },
            new { Code = "Done", NameEn = "Done", NameAr = "مكتمل", DisplayOrder = 50, Color = "emerald", IsDefault = false },
            new { Code = "Cancelled", NameEn = "Cancelled", NameAr = "ملغى", DisplayOrder = 60, Color = "rose", IsDefault = false },
            new { Code = "OnHold", NameEn = "On Hold", NameAr = "معلّق", DisplayOrder = 70, Color = "orange", IsDefault = false }
        };

        var statuses = await context.Lookups.IgnoreQueryFilters()
            .Where(lookup => lookup.TenantId == TenantId && lookup.Category == ReferenceDataCategories.WorkItemStatus && !lookup.IsDeleted)
            .ToListAsync();

        foreach (var definition in definitions)
        {
            var status = statuses.FirstOrDefault(candidate => candidate.Code == definition.Code);
            if (status is null)
            {
                context.Lookups.Add(new Lookup
                {
                    Id = CreateDeterministicGuid("b0000001", Array.IndexOf(definitions, definition), 0, 7),
                    TenantId = TenantId,
                    Category = ReferenceDataCategories.WorkItemStatus,
                    Code = definition.Code,
                    Name = definition.NameEn,
                    NameEn = definition.NameEn,
                    NameAr = definition.NameAr,
                    DisplayOrder = definition.DisplayOrder,
                    IsActive = true,
                    Color = definition.Color,
                    IsDefault = definition.IsDefault,
                    CreatedBy = "System"
                });
            }
            else
            {
                if (string.IsNullOrWhiteSpace(status.NameEn)) status.NameEn = status.Name;
                if (string.IsNullOrWhiteSpace(status.NameAr)) status.NameAr = status.NameEn;
            }
        }

        await context.SaveChangesAsync();

        var activeStatuses = await context.Lookups
            .Where(lookup => lookup.Category == ReferenceDataCategories.WorkItemStatus && lookup.IsActive && !lookup.IsDeleted)
            .OrderBy(lookup => lookup.DisplayOrder)
            .ThenBy(lookup => lookup.Code)
            .ToListAsync();
        var defaultStatus = activeStatuses.FirstOrDefault(status => status.IsDefault) ?? activeStatuses.FirstOrDefault(status => status.Code == "ToDo") ?? activeStatuses.FirstOrDefault();
        if (defaultStatus is not null)
        {
            foreach (var status in activeStatuses) status.IsDefault = status.Id == defaultStatus.Id;
            await context.SaveChangesAsync();
        }
    }

    private static string BuildSeedSnapshotData(
        Project project,
        List<ProjectMember> members,
        List<ResourceAllocation> allocations,
        List<ProjectPhase> phases,
        List<Milestone> milestones,
        Dictionary<Guid, string> resourceNames)
    {
        var data = new ProjectSnapshotDataDto
        {
            Members = members.Select(m => new SnapshotMemberDto
            {
                OriginalId = m.Id,
                ResourceId = m.ResourceId,
                ResourceName = m.ResourceId.HasValue && resourceNames.TryGetValue(m.ResourceId.Value, out var name) ? name : null,
                Role = m.Role,
                AllocationPercentage = m.AllocationPercentage,
                HoursPerWeek = m.HoursPerWeek,
                StartDate = m.StartDate,
                EndDate = m.EndDate,
                Status = m.Status,
                IsBillable = m.IsBillable,
                Notes = m.Notes
            }).ToList(),
            Allocations = allocations.Select(a => new SnapshotAllocationDto
            {
                OriginalId = a.Id,
                ResourceId = a.ResourceId,
                ResourceName = resourceNames.TryGetValue(a.ResourceId, out var name) ? name : null,
                ProjectRoleId = a.ProjectRoleId,
                AllocationPercentage = a.AllocationPercentage,
                HoursPerWeek = a.HoursPerWeek,
                StartDate = a.StartDate,
                EndDate = a.EndDate,
                Status = a.Status,
                IsBillable = a.IsBillable,
                Notes = a.Notes
            }).ToList(),
            Phases = phases.Select(p => new SnapshotPhaseDto
            {
                OriginalId = p.Id,
                Name = p.Name,
                Description = p.Description,
                SortOrder = p.SortOrder,
                Status = p.Status,
                StartDate = p.StartDate,
                EndDate = p.EndDate,
                Progress = p.Progress
            }).ToList(),
            Milestones = milestones.Select(m => new SnapshotMilestoneDto
            {
                OriginalId = m.Id,
                Name = m.Name,
                DueDate = m.DueDate,
                CompletedDate = m.CompletedDate,
                Status = m.Status,
                Progress = m.Progress,
                Category = m.Category,
                AssigneeId = m.AssigneeId
            }).ToList()
        };

        return System.Text.Json.JsonSerializer.Serialize(data, new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
        });
    }

    private static Guid CreateDeterministicGuid(string prefix, int primary, int secondary, int tertiary)
    {
        var suffix = (primary + 1) * 100000 + (secondary + 1) * 1000 + tertiary;
        return Guid.Parse($"{prefix}-{primary:D4}-{secondary:D4}-0000-{suffix:D12}");
    }

    private static async Task ResetProjectSeedDataAsync(AppDbContext context)
    {
        var projectIds = await context.Projects.IgnoreQueryFilters()
            .Where(project => project.TenantId == TenantId)
            .Select(project => project.Id)
            .ToListAsync();

        if (projectIds.Count == 0)
        {
            return;
        }

        var projectAllocations = await context.ResourceAllocations.IgnoreQueryFilters()
            .Where(allocation => allocation.TenantId == TenantId && allocation.ProjectId.HasValue && projectIds.Contains(allocation.ProjectId.Value))
            .ToListAsync();
        if (projectAllocations.Count > 0)
        {
            context.ResourceAllocations.RemoveRange(projectAllocations);
        }

        var workItems = await context.WorkItems.IgnoreQueryFilters()
            .Where(workItem => workItem.TenantId == TenantId && workItem.ProjectId.HasValue && projectIds.Contains(workItem.ProjectId.Value))
            .ToListAsync();
        if (workItems.Count > 0)
        {
            context.WorkItems.RemoveRange(workItems);
        }

        var projectMembers = await context.ProjectMembers.IgnoreQueryFilters()
            .Where(member => member.TenantId == TenantId && projectIds.Contains(member.ProjectId))
            .ToListAsync();
        if (projectMembers.Count > 0)
        {
            context.ProjectMembers.RemoveRange(projectMembers);
        }

        var managerAssignments = await context.ProjectManagerAssignments.IgnoreQueryFilters()
            .Where(assignment => assignment.TenantId == TenantId && projectIds.Contains(assignment.ProjectId))
            .ToListAsync();
        if (managerAssignments.Count > 0)
        {
            context.ProjectManagerAssignments.RemoveRange(managerAssignments);
        }

        var projectSnapshots = await context.ProjectSnapshots.IgnoreQueryFilters()
            .Where(snapshot => snapshot.TenantId == TenantId && projectIds.Contains(snapshot.ProjectId))
            .ToListAsync();
        if (projectSnapshots.Count > 0)
        {
            context.ProjectSnapshots.RemoveRange(projectSnapshots);
        }

        var projectHandoverRequests = await context.ProjectHandoverRequests.IgnoreQueryFilters()
            .Where(request => request.TenantId == TenantId && projectIds.Contains(request.ProjectId))
            .ToListAsync();
        if (projectHandoverRequests.Count > 0)
        {
            context.ProjectHandoverRequests.RemoveRange(projectHandoverRequests);
        }

        var projectPhases = await context.ProjectPhases.IgnoreQueryFilters()
            .Where(phase => phase.TenantId == TenantId && projectIds.Contains(phase.ProjectId))
            .ToListAsync();
        if (projectPhases.Count > 0)
        {
            context.ProjectPhases.RemoveRange(projectPhases);
        }

        var milestones = await context.Milestones.IgnoreQueryFilters()
            .Where(milestone => milestone.TenantId == TenantId && projectIds.Contains(milestone.ProjectId))
            .ToListAsync();
        if (milestones.Count > 0)
        {
            context.Milestones.RemoveRange(milestones);
        }

        var projectStakeholders = await context.ProjectStakeholders.IgnoreQueryFilters()
            .Where(stakeholder => stakeholder.TenantId == TenantId && projectIds.Contains(stakeholder.ProjectId))
            .ToListAsync();
        if (projectStakeholders.Count > 0)
        {
            context.ProjectStakeholders.RemoveRange(projectStakeholders);
        }

        var projectSchedules = await context.ProjectSchedules.IgnoreQueryFilters()
            .Where(schedule => schedule.TenantId == TenantId && projectIds.Contains(schedule.ProjectId))
            .ToListAsync();
        if (projectSchedules.Count > 0)
        {
            context.ProjectSchedules.RemoveRange(projectSchedules);
        }

        var workBreakdownStructures = await context.WorkBreakdownStructures.IgnoreQueryFilters()
            .Where(wbs => wbs.TenantId == TenantId && projectIds.Contains(wbs.ProjectId))
            .ToListAsync();
        if (workBreakdownStructures.Count > 0)
        {
            context.WorkBreakdownStructures.RemoveRange(workBreakdownStructures);
        }

        var baselines = await context.Baselines.IgnoreQueryFilters()
            .Where(baseline => baseline.TenantId == TenantId && projectIds.Contains(baseline.ProjectId))
            .ToListAsync();
        if (baselines.Count > 0)
        {
            context.Baselines.RemoveRange(baselines);
        }

        var deliverables = await context.Deliverables.IgnoreQueryFilters()
            .Where(deliverable => deliverable.TenantId == TenantId && projectIds.Contains(deliverable.ProjectId))
            .ToListAsync();
        if (deliverables.Count > 0)
        {
            context.Deliverables.RemoveRange(deliverables);
        }

        var statusReports = await context.ProjectStatusReports.IgnoreQueryFilters()
            .Where(report => report.TenantId == TenantId && projectIds.Contains(report.ProjectId))
            .ToListAsync();
        if (statusReports.Count > 0)
        {
            context.ProjectStatusReports.RemoveRange(statusReports);
        }

        var issues = await context.Issues.IgnoreQueryFilters()
            .Where(issue => issue.TenantId == TenantId && issue.ProjectId.HasValue && projectIds.Contains(issue.ProjectId.Value))
            .ToListAsync();
        if (issues.Count > 0)
        {
            context.Issues.RemoveRange(issues);
        }

        var risks = await context.Risks.IgnoreQueryFilters()
            .Where(risk => risk.TenantId == TenantId && projectIds.Contains(risk.ProjectId))
            .ToListAsync();
        if (risks.Count > 0)
        {
            context.Risks.RemoveRange(risks);
        }

        var meetings = await context.Meetings.IgnoreQueryFilters()
            .Where(meeting => meeting.TenantId == TenantId && meeting.ProjectId.HasValue && projectIds.Contains(meeting.ProjectId.Value))
            .ToListAsync();
        if (meetings.Count > 0)
        {
            context.Meetings.RemoveRange(meetings);
        }

        var budgets = await context.Budgets.IgnoreQueryFilters()
            .Where(budget => budget.TenantId == TenantId && budget.ProjectId.HasValue && projectIds.Contains(budget.ProjectId.Value))
            .ToListAsync();
        if (budgets.Count > 0)
        {
            context.Budgets.RemoveRange(budgets);
        }

        var costEntries = await context.CostEntries.IgnoreQueryFilters()
            .Where(entry => entry.TenantId == TenantId && entry.ProjectId.HasValue && projectIds.Contains(entry.ProjectId.Value))
            .ToListAsync();
        if (costEntries.Count > 0)
        {
            context.CostEntries.RemoveRange(costEntries);
        }

        var documents = await context.Documents.IgnoreQueryFilters()
            .Where(document => document.TenantId == TenantId && document.ProjectId.HasValue && projectIds.Contains(document.ProjectId.Value))
            .ToListAsync();
        if (documents.Count > 0)
        {
            context.Documents.RemoveRange(documents);
        }

        var projectEntities = await context.Projects.IgnoreQueryFilters()
            .Where(project => project.TenantId == TenantId && projectIds.Contains(project.Id))
            .ToListAsync();
        if (projectEntities.Count > 0)
        {
            context.Projects.RemoveRange(projectEntities);
        }

        await context.SaveChangesAsync();
    }

    private static async Task EnsurePortfolioAndProgramSeedDataAsync(AppDbContext context)
    {
        var seededProjectManagerId = await context.Users.IgnoreQueryFilters()
            .Where(user => user.TenantId == TenantId && user.Id == ProjectManagerUserId && user.IsActive && !user.IsDeleted)
            .Select(user => (Guid?)user.Id)
            .FirstOrDefaultAsync();

        if (!await context.Portfolios.IgnoreQueryFilters().AnyAsync(portfolio => portfolio.Id == Portfolio1Id && portfolio.TenantId == TenantId))
        {
            var portfolios = new List<Portfolio>
            {
                new() { Id = Portfolio1Id, TenantId = TenantId, Name = "Digital Transformation", Code = "DIGITAL", Description = "Digital transformation initiatives", Status = "Active", OwnerId = PortfolioManagerUserId, TotalBudget = 5000000m, ActualSpend = 1250000m, Health = "Green", Priority = "Critical", CreatedBy = "System" },
                new() { Id = Portfolio2Id, TenantId = TenantId, Name = "Infrastructure & Operations", Code = "INFRA", Description = "Infrastructure and operations projects", Status = "Active", OwnerId = PortfolioManagerUserId, TotalBudget = 3500000m, ActualSpend = 2100000m, Health = "Amber", Priority = "High", CreatedBy = "System" },
                new() { Id = Portfolio3Id, TenantId = TenantId, Name = "Product Innovation", Code = "INNOVATE", Description = "New product development and innovation", Status = "Active", OwnerId = PortfolioManagerUserId, TotalBudget = 2500000m, ActualSpend = 800000m, Health = "Green", Priority = "High", CreatedBy = "System" },
                new() { Id = Portfolio4Id, TenantId = TenantId, Name = "Compliance & Security", Code = "COMPLY", Description = "Regulatory compliance and security", Status = "Active", OwnerId = PortfolioManagerUserId, TotalBudget = 1800000m, ActualSpend = 950000m, Health = "Red", Priority = "Critical", CreatedBy = "System" }
            };
            context.Portfolios.AddRange(portfolios);
        }

        if (!await context.Set<global::NEXUS.Modules.Portfolio.Domain.Program>().IgnoreQueryFilters().AnyAsync(program => program.Id == Guid.Parse("d0000001-0000-0000-0000-000000000001") && program.TenantId == TenantId))
        {
            var programs = new List<global::NEXUS.Modules.Portfolio.Domain.Program>
            {
                new() { Id = Guid.Parse("d0000001-0000-0000-0000-000000000001"), TenantId = TenantId, Name = "Cloud Migration Program", PortfolioId = Portfolio1Id, Status = "Active", ProgramManagerId = seededProjectManagerId, Budget = 2000000m, ActualSpend = 450000m, Health = "Green", CreatedBy = "System" },
                new() { Id = Guid.Parse("d0000002-0000-0000-0000-000000000002"), TenantId = TenantId, Name = "ERP Modernisation", PortfolioId = Portfolio1Id, Status = "Active", ProgramManagerId = seededProjectManagerId, Budget = 1800000m, ActualSpend = 600000m, Health = "Amber", CreatedBy = "System" },
                new() { Id = Guid.Parse("d0000003-0000-0000-0000-000000000003"), TenantId = TenantId, Name = "Data & Analytics", PortfolioId = Portfolio1Id, Status = "Active", ProgramManagerId = seededProjectManagerId, Budget = 1200000m, ActualSpend = 200000m, Health = "Green", CreatedBy = "System" },
                new() { Id = Guid.Parse("d0000004-0000-0000-0000-000000000004"), TenantId = TenantId, Name = "Network Infrastructure", PortfolioId = Portfolio2Id, Status = "Active", ProgramManagerId = seededProjectManagerId, Budget = 1500000m, ActualSpend = 900000m, Health = "Amber", CreatedBy = "System" },
                new() { Id = Guid.Parse("d0000005-0000-0000-0000-000000000005"), TenantId = TenantId, Name = "Office Modernisation", PortfolioId = Portfolio2Id, Status = "Active", ProgramManagerId = seededProjectManagerId, Budget = 2000000m, ActualSpend = 1200000m, Health = "Red", CreatedBy = "System" },
                new() { Id = Guid.Parse("d0000006-0000-0000-0000-000000000006"), TenantId = TenantId, Name = "Product Launch Program", PortfolioId = Portfolio3Id, Status = "Planning", ProgramManagerId = seededProjectManagerId, Budget = 2500000m, ActualSpend = 800000m, Health = "Green", CreatedBy = "System" },
                new() { Id = Guid.Parse("d0000007-0000-0000-0000-000000000007"), TenantId = TenantId, Name = "Security Enhancement Program", PortfolioId = Portfolio4Id, Status = "Active", ProgramManagerId = seededProjectManagerId, Budget = 1800000m, ActualSpend = 950000m, Health = "Red", CreatedBy = "System" }
            };
            context.Set<global::NEXUS.Modules.Portfolio.Domain.Program>().AddRange(programs);
        }

        var seededPortfolios = await context.Portfolios.IgnoreQueryFilters().Where(x => x.TenantId == TenantId && x.OwnerId.HasValue && !x.IsDeleted).ToListAsync();
        foreach (var portfolio in seededPortfolios)
        {
            if (!await context.Set<global::NEXUS.Modules.Portfolio.Domain.PortfolioManagerAssignment>().IgnoreQueryFilters().AnyAsync(x => x.TenantId == TenantId && x.PortfolioId == portfolio.Id && x.IsCurrent))
                context.Set<global::NEXUS.Modules.Portfolio.Domain.PortfolioManagerAssignment>().Add(new global::NEXUS.Modules.Portfolio.Domain.PortfolioManagerAssignment { TenantId = TenantId, PortfolioId = portfolio.Id, PortfolioManagerId = portfolio.OwnerId!.Value, StartDate = portfolio.StartDate ?? portfolio.CreatedAt, IsCurrent = true, CreatedBy = "System" });
        }

        var seededPrograms = await context.Set<global::NEXUS.Modules.Portfolio.Domain.Program>().IgnoreQueryFilters().Where(x => x.TenantId == TenantId && x.ProgramManagerId.HasValue && !x.IsDeleted).ToListAsync();
        foreach (var program in seededPrograms)
        {
            if (!await context.Set<global::NEXUS.Modules.Portfolio.Domain.ProgramManagerAssignment>().IgnoreQueryFilters().AnyAsync(x => x.TenantId == TenantId && x.ProgramId == program.Id && x.IsCurrent))
                context.Set<global::NEXUS.Modules.Portfolio.Domain.ProgramManagerAssignment>().Add(new global::NEXUS.Modules.Portfolio.Domain.ProgramManagerAssignment { TenantId = TenantId, ProgramId = program.Id, ProgramManagerId = program.ProgramManagerId!.Value, StartDate = program.StartDate ?? program.CreatedAt, IsCurrent = true, CreatedBy = "System" });
        }

        await context.SaveChangesAsync();
    }

    private static async Task EnsureOrganizationSeedDataAsync(AppDbContext context)
    {
        var organizationId = Guid.Parse("90000001-0000-0000-0000-000000000001");
        var businessUnitId = Guid.Parse("90000002-0000-0000-0000-000000000001");
        var departmentIds = new[]
        {
            Guid.Parse("a1000001-0000-0000-0000-000000000001"),
            Guid.Parse("a1000002-0000-0000-0000-000000000002"),
            Guid.Parse("a1000003-0000-0000-0000-000000000003"),
            Guid.Parse("a1000004-0000-0000-0000-000000000004")
        };

        if (!await context.Organizations.IgnoreQueryFilters().AnyAsync(organization => organization.Id == organizationId && organization.TenantId == TenantId))
        {
            context.Organizations.Add(new Organization
            {
                Id = organizationId,
                TenantId = TenantId,
                Name = "NEXUS Group",
                Code = "NEXUS",
                Description = "Enterprise operating organization",
                CreatedBy = "System"
            });
        }

        if (!await context.BusinessUnits.IgnoreQueryFilters().AnyAsync(businessUnit => businessUnit.Id == businessUnitId && businessUnit.TenantId == TenantId))
        {
            context.BusinessUnits.Add(new BusinessUnit
            {
                Id = businessUnitId,
                TenantId = TenantId,
                OrganizationId = organizationId,
                Name = "Technology",
                Code = "TECH",
                Description = "Technology and transformation business unit",
                CreatedBy = "System"
            });
        }

        if (!await context.Departments.IgnoreQueryFilters().AnyAsync(department => department.Id == departmentIds[0] && department.TenantId == TenantId))
        {
            context.Departments.AddRange(new[]
            {
                new Department { Id = departmentIds[0], TenantId = TenantId, BusinessUnitId = businessUnitId, Name = "Engineering", Code = "ENG", Description = "Engineering and delivery", CreatedBy = "System" },
                new Department { Id = departmentIds[1], TenantId = TenantId, BusinessUnitId = businessUnitId, Name = "Operations", Code = "OPS", Description = "Operations and service delivery", CreatedBy = "System" },
                new Department { Id = departmentIds[2], TenantId = TenantId, BusinessUnitId = businessUnitId, Name = "Product", Code = "PRD", Description = "Product and customer experience", CreatedBy = "System" },
                new Department { Id = departmentIds[3], TenantId = TenantId, BusinessUnitId = businessUnitId, Name = "Security", Code = "SEC", Description = "Security and compliance", CreatedBy = "System" }
            });
        }

        await context.SaveChangesAsync();
    }

    private static async Task EnsureCurrencySeedDataAsync(AppDbContext context)
    {
        var currencyDefinitions = new[]
        {
            new { Id = Guid.Parse("90000003-0000-0000-0000-000000000001"), Code = "GBP", Name = "Pound Sterling", Symbol = "£", IsBase = true },
            new { Id = Guid.Parse("90000003-0000-0000-0000-000000000002"), Code = "USD", Name = "US Dollar", Symbol = "$", IsBase = false },
            new { Id = Guid.Parse("90000003-0000-0000-0000-000000000003"), Code = "EUR", Name = "Euro", Symbol = "€", IsBase = false },
            new { Id = Guid.Parse("90000003-0000-0000-0000-000000000004"), Code = "AED", Name = "UAE Dirham", Symbol = "د.إ", IsBase = false },
            new { Id = Guid.Parse("90000003-0000-0000-0000-000000000005"), Code = "EGP", Name = "Egyptian Pound", Symbol = "ج.م", IsBase = false }
        };

        foreach (var definition in currencyDefinitions)
        {
            if (await context.Currencies.IgnoreQueryFilters().AnyAsync(currency => currency.Id == definition.Id && currency.TenantId == TenantId))
                continue;

            context.Currencies.Add(new Currency
            {
                Id = definition.Id,
                TenantId = TenantId,
                Code = definition.Code,
                Name = definition.Name,
                Symbol = definition.Symbol,
                ExchangeRate = 1m,
                IsBase = definition.IsBase,
                CreatedBy = "System"
            });
        }

        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Seeds the canonical VendorCategory reference-data set (Supplier, Consultant,
    /// Technology, Outsourcing, Other) using deterministic ids. Idempotent: re-running
    /// the seed never duplicates or overwrites categories managed through Reference Data.
    /// </summary>
    private static async Task EnsureVendorCategorySeedDataAsync(AppDbContext context)
    {
        var definitions = new[]
        {
            new { Code = "SUP", NameEn = "Supplier", NameAr = "مورّد", DisplayOrder = 10 },
            new { Code = "CON", NameEn = "Consultant", NameAr = "مستشار", DisplayOrder = 20 },
            new { Code = "TEC", NameEn = "Technology", NameAr = "تقنية", DisplayOrder = 30 },
            new { Code = "OUT", NameEn = "Outsourcing", NameAr = "تعاقد خارجي", DisplayOrder = 40 },
            new { Code = "OTH", NameEn = "Other", NameAr = "أخرى", DisplayOrder = 50 }
        };

        var existing = await context.Lookups.IgnoreQueryFilters()
            .Where(lookup => lookup.TenantId == TenantId && lookup.Category == ReferenceDataCategories.VendorCategory && !lookup.IsDeleted)
            .ToListAsync();

        for (var index = 0; index < definitions.Length; index++)
        {
            var definition = definitions[index];
            if (existing.Any(candidate => candidate.Code == definition.Code))
                continue;

            context.Lookups.Add(new Lookup
            {
                Id = CreateDeterministicGuid("b0000002", index, 0, 7),
                TenantId = TenantId,
                Category = ReferenceDataCategories.VendorCategory,
                Code = definition.Code,
                Name = definition.NameEn,
                NameEn = definition.NameEn,
                NameAr = definition.NameAr,
                DisplayOrder = definition.DisplayOrder,
                IsActive = true,
                CreatedBy = "System"
            });
        }

        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Seeds vendor reference data only: the canonical VendorCategory set and the Skill
    /// catalog. No demo vendors, outsourced resources, contracts, or resource-skill links are
    /// created, so a fresh tenant starts with an empty vendor and resource list. Rows left
    /// behind by an earlier seed revision are purged so the demo data cannot come back.
    /// Idempotent: reference data is only added when missing.
    /// </summary>
    private static async Task EnsureVendorAndOutsourcedSeedDataAsync(AppDbContext context)
    {
        await EnsureVendorCategorySeedDataAsync(context);
        await EnsureSkillSeedDataAsync(context);

        await PurgeSeededDemoDataAsync(context);
    }

    /// <summary>
    /// Hard-deletes the demo rows created by earlier seed revisions: the CloudNova and
    /// DataWorks vendors, the six internal employee resources, the ten outsourced resources,
    /// and the contracts, allocations, project memberships and resource-skill links that
    /// pointed at them. Matching is done on the deterministic seed ids only, so resources
    /// and vendors created by users are never touched.
    /// </summary>
    private static async Task PurgeSeededDemoDataAsync(AppDbContext context)
    {
        var seededVendorIds = new[]
        {
            Guid.Parse("b3000001-0000-0000-0000-000000000001"),
            Guid.Parse("b3000001-0000-0000-0000-000000000002")
        };

        var seededResourceIds = Enumerable.Range(1, 6)
            .Select(i => Guid.Parse($"70000001-0000-0000-0000-{i:D12}"))
            .Concat(Enumerable.Range(1, 10).Select(i => Guid.Parse($"70000002-0000-0000-0000-{i:D12}")))
            .ToList();

        var resources = await context.Resources.IgnoreQueryFilters()
            .Where(r => r.TenantId == TenantId && seededResourceIds.Contains(r.Id))
            .ToListAsync();

        var seededResourceIdSet = seededResourceIds.ToHashSet();

        var skillLinks = await context.ResourceSkills.IgnoreQueryFilters()
            .Where(rs => rs.TenantId == TenantId && seededResourceIdSet.Contains(rs.ResourceId))
            .ToListAsync();
        context.ResourceSkills.RemoveRange(skillLinks);

        var contracts = await context.Contracts.IgnoreQueryFilters()
            .Where(c => c.TenantId == TenantId && seededResourceIdSet.Contains(c.ResourceId))
            .ToListAsync();
        context.Contracts.RemoveRange(contracts);

        var allocations = await context.ResourceAllocations.IgnoreQueryFilters()
            .Where(a => a.TenantId == TenantId && seededResourceIdSet.Contains(a.ResourceId))
            .ToListAsync();
        context.ResourceAllocations.RemoveRange(allocations);

        var members = await context.ProjectMembers.IgnoreQueryFilters()
            .Where(m => m.TenantId == TenantId && m.ResourceId.HasValue && seededResourceIdSet.Contains(m.ResourceId.Value))
            .ToListAsync();
        context.ProjectMembers.RemoveRange(members);

        context.Resources.RemoveRange(resources);

        var vendors = await context.Vendors.IgnoreQueryFilters()
            .Where(v => v.TenantId == TenantId && seededVendorIds.Contains(v.Id))
            .ToListAsync();
        context.Vendors.RemoveRange(vendors);

        if (skillLinks.Count > 0 || contracts.Count > 0 || allocations.Count > 0 || members.Count > 0 || resources.Count > 0 || vendors.Count > 0)
            await context.SaveChangesAsync();
    }

    private static async Task EnsureSkillSeedDataAsync(AppDbContext context)
    {
        var skillDefinitions = new[]
        {
            new { Id = Guid.Parse("c1000001-0000-0000-0000-000000000001"), Name = "Cloud Architecture", Category = "Infrastructure", Description = "Design of scalable cloud solutions" },
            new { Id = Guid.Parse("c1000001-0000-0000-0000-000000000002"), Name = "Kubernetes", Category = "Infrastructure", Description = "Container orchestration" },
            new { Id = Guid.Parse("c1000001-0000-0000-0000-000000000003"), Name = "Terraform", Category = "Infrastructure", Description = "Infrastructure as code" },
            new { Id = Guid.Parse("c1000001-0000-0000-0000-000000000004"), Name = "CI/CD", Category = "Engineering", Description = "Continuous integration and delivery pipelines" },
            new { Id = Guid.Parse("c1000001-0000-0000-0000-000000000005"), Name = "SQL", Category = "Data", Description = "Relational database querying and modelling" },
            new { Id = Guid.Parse("c1000001-0000-0000-0000-000000000006"), Name = "Data Engineering", Category = "Data", Description = "Building and maintaining data pipelines" },
            new { Id = Guid.Parse("c1000001-0000-0000-0000-000000000007"), Name = "ETL", Category = "Data", Description = "Extract, transform, load processes" },
            new { Id = Guid.Parse("c1000001-0000-0000-0000-000000000008"), Name = "Power BI", Category = "Data", Description = "Business intelligence reporting" },
            new { Id = Guid.Parse("c1000001-0000-0000-0000-000000000009"), Name = "Python", Category = "Development", Description = "General purpose programming language" },
            new { Id = Guid.Parse("c1000001-0000-0000-0000-000000000010"), Name = "Networking", Category = "Infrastructure", Description = "Network design and operations" }
        };

        var existingSkills = await context.Skills.IgnoreQueryFilters().Where(s => s.TenantId == TenantId).ToListAsync();
        foreach (var definition in skillDefinitions)
        {
            if (existingSkills.Any(s => s.Name == definition.Name))
                continue;

            context.Skills.Add(new Skill
            {
                Id = definition.Id,
                TenantId = TenantId,
                Name = definition.Name,
                Category = definition.Category,
                Description = definition.Description,
                IsActive = true,
                CreatedBy = "System"
            });
        }
        await context.SaveChangesAsync();

    }

    public static async Task SeedIdentityOnlyAsync(AppDbContext context, int temporaryPasswordExpiryHours = 24)
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"), "Development", StringComparison.OrdinalIgnoreCase))
            return;
        ValidateTemporaryPasswordExpiryHours(temporaryPasswordExpiryHours);

        await using var transaction = context.Database.IsRelational()
            ? await context.Database.BeginTransactionAsync(IsolationLevel.Serializable)
            : null;
        try
        {
            await SeedIdentityAsync(context, temporaryPasswordExpiryHours);
            await EnsureMinimalDevelopmentOrganizationAsync(context);
            if (transaction is not null)
                await transaction.CommitAsync();
        }
        catch
        {
            if (transaction is not null)
                await transaction.RollbackAsync();
            throw;
        }
    }

    /// <summary>
    /// Seeds the minimal Development system baseline. Unlike SeedAsync, this path never
    /// creates demo projects, portfolios, programs, users, teams, work items, or finance
    /// records. It adds only the deterministic tenant identity, structural organization,
    /// identity catalogs, approved reference catalogs, and the single protected Admin.
    /// Setting defaults are defined by SettingDefinitionCatalog and do not require
    /// persisted ConfigurationSettings override rows.
    /// </summary>
    public static async Task SeedMinimalAsync(AppDbContext context, int temporaryPasswordExpiryHours = 24)
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"), "Development", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Minimal Development seeding is available only when ASPNETCORE_ENVIRONMENT=Development.");
        ValidateTemporaryPasswordExpiryHours(temporaryPasswordExpiryHours);

        // Setting defaults are executable system configuration. Validate the catalog,
        // but do not persist override rows that would mask those defaults.
        _ = new SettingDefinitionCatalog();

        await using var transaction = context.Database.IsRelational()
            ? await context.Database.BeginTransactionAsync(IsolationLevel.Serializable)
            : null;
        try
        {
            await EnsureMinimalSeedInputAsync(context);
            await SeedIdentityAsync(context, temporaryPasswordExpiryHours);
            await EnsureMinimalDevelopmentOrganizationAsync(context);

            // Reference-only helpers. Do not replace these calls with the broader
            // organization, vendor/outsourced, portfolio/program, or demo seed helpers.
            await EnsureWorkItemStatusSeedDataAsync(context);
            await EnsureCurrencySeedDataAsync(context);
            await EnsureVendorCategorySeedDataAsync(context);
            await EnsureSkillSeedDataAsync(context);
            await EnsureDocumentCatalogAsync(context);

            await EnsureMinimalSeedOutputAsync(context);
            if (transaction is not null)
                await transaction.CommitAsync();
        }
        catch
        {
            if (transaction is not null)
                await transaction.RollbackAsync();
            throw;
        }
    }

    /// <summary>
    /// Independently verifies the canonical minimal Development seed output after
    /// the seed process has completed. The EF database gate uses this to confirm
    /// the sole Admin, protected permission baseline, and absence of business rows.
    /// </summary>
    public static Task VerifyMinimalDevelopmentSeedOutputAsync(AppDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return EnsureMinimalSeedOutputAsync(context);
    }

    private static async Task EnsureMinimalSeedInputAsync(AppDbContext context)
    {
        var users = await context.Users.IgnoreQueryFilters().ToListAsync();
        if (users.Count > 1
            || users.Any(user => user.IsDeleted
                || user.Id != AdminUserId
                || !string.Equals(user.Email, SeededDevelopmentAdmin.Email, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Minimal Development seed requires an empty user table or the single canonical Admin account; it will not remove or overwrite other users.");

        if (await context.Tenants.IgnoreQueryFilters().AnyAsync(tenant => tenant.Id != TenantId)
            || await context.Organizations.IgnoreQueryFilters().AnyAsync(organization => organization.Id != Guid.Parse("90000001-0000-0000-0000-000000000001") || organization.TenantId != TenantId)
            || await context.Set<ConfigurationSetting>().IgnoreQueryFilters().AnyAsync())
            throw new InvalidOperationException("Minimal Development seed requires a fresh system-only database with no stored setting overrides or business records; it will not modify an existing data set.");

        await EnsureNoUnexpectedMappedSeedRowsAsync(context);
    }

    private static async Task EnsureMinimalSeedOutputAsync(AppDbContext context)
    {
        var users = await context.Users.IgnoreQueryFilters().ToListAsync();
        if (users.Count != 1
            || users[0].Id != AdminUserId
            || !string.Equals(users[0].Email, SeededDevelopmentAdmin.Email, StringComparison.OrdinalIgnoreCase)
            || !users[0].IsActive
            || users[0].IsDeleted)
            throw new InvalidOperationException("Minimal Development seed must produce exactly one active canonical Admin user.");

        if (await context.Tenants.IgnoreQueryFilters().CountAsync() != 1
            || !await context.Tenants.IgnoreQueryFilters().AnyAsync(tenant => tenant.Id == TenantId
                && tenant.Code == "FOPX-DEMO"
                && tenant.DisplayName == "FOPX Development Demo Tenant"
                && tenant.Status == TenantLifecycleStatus.Active)
            || await context.Organizations.IgnoreQueryFilters().CountAsync() != 1
            || !await context.Organizations.IgnoreQueryFilters().AnyAsync(organization => organization.Id == Guid.Parse("90000001-0000-0000-0000-000000000001")
                && organization.TenantId == TenantId
                && organization.Code == "NEXUS"
                && organization.Name == "NEXUS Group"))
            throw new InvalidOperationException("Minimal Development seed must contain exactly the required Tenant and structural Organization.");

        var expectedSystemRoleKeys = new HashSet<string>(StringComparer.Ordinal)
        {
            Role.TenantAdminSystemRoleKey,
            "tenant.executive",
            "tenant.portfolio-manager",
            "tenant.project-manager",
            "tenant.team-member",
            "tenant.pmo-admin",
            "tenant.finance-controller",
            "tenant.resource-manager"
        };
        var actualRoles = await context.Roles.IgnoreQueryFilters().ToListAsync();
        if (actualRoles.Count != expectedSystemRoleKeys.Count
            || !actualRoles.Select(role => role.SystemRoleKey).ToHashSet(StringComparer.Ordinal).SetEquals(expectedSystemRoleKeys)
            || actualRoles.Any(role => role.TenantId != TenantId
                || !role.IsSystem
                || role.IsDeleted
                || role.SystemRoleKey is null
                || !expectedSystemRoleKeys.Contains(role.SystemRoleKey)))
            throw new InvalidOperationException("Minimal Development seed must contain only the canonical Tenant system roles.");

        var adminRole = await context.Roles.IgnoreQueryFilters()
            .Include(role => role.Permissions)
            .SingleOrDefaultAsync(role => role.TenantId == TenantId
                && role.SystemRoleKey == Role.TenantAdminSystemRoleKey
                && role.IsSystem
                && !role.IsDeleted);
        if (adminRole is null || !ProtectedTenantAdminPolicy.MandatoryPermissionKeys.All(key =>
                adminRole.Permissions.Any(permission => permission.TenantId == TenantId
                    && !permission.IsDeleted
                    && string.Equals(permission.Name, key, StringComparison.Ordinal))))
            throw new InvalidOperationException("Minimal Development seed did not establish the complete protected Tenant Admin permission baseline.");

        var activeAdminMemberships = await context.UserRoles.IgnoreQueryFilters()
            .Include(assignment => assignment.Role)
            .Where(assignment => assignment.UserId == AdminUserId && !assignment.IsDeleted)
            .ToListAsync();
        if (activeAdminMemberships.Count != 1
            || activeAdminMemberships[0].Role.TenantId != TenantId
            || activeAdminMemberships[0].Role.SystemRoleKey != Role.TenantAdminSystemRoleKey
            || activeAdminMemberships[0].Role.IsDeleted)
            throw new InvalidOperationException("Minimal Development seed must assign the sole Admin user only to the protected Tenant Admin system role.");

        var expectedPermissions = PermissionCatalog.CreateSeedPermissions(TenantId)
            .Select(permission => $"{permission.Resource}|{permission.Action}")
            .ToHashSet(StringComparer.Ordinal);
        var persistedPermissions = await context.Permissions.IgnoreQueryFilters().ToListAsync();
        var actualPermissions = persistedPermissions
            .Where(permission => permission.TenantId == TenantId)
            .Select(permission => $"{permission.Resource}|{permission.Action}")
            .ToList();
        if (persistedPermissions.Count != expectedPermissions.Count
            || persistedPermissions.Any(permission => permission.TenantId != TenantId)
            || actualPermissions.Count != expectedPermissions.Count
            || actualPermissions.Distinct(StringComparer.Ordinal).Count() != expectedPermissions.Count
            || actualPermissions.Any(key => !expectedPermissions.Contains(key)))
            throw new InvalidOperationException("Minimal Development seed must contain exactly the canonical Tenant permission catalog with no duplicate or legacy keys.");

        await EnsureNoUnexpectedMappedSeedRowsAsync(context);
    }

    private static async Task EnsureNoUnexpectedMappedSeedRowsAsync(AppDbContext context)
    {
        var unexpectedTables = new List<string>();
        foreach (var entityType in context.Model.GetEntityTypes()
                     .Where(entityType => !entityType.IsOwned()
                         && entityType.FindPrimaryKey() is not null
                         && !entityType.ClrType.IsAbstract))
        {
            var tableName = entityType.GetTableName() ?? entityType.Name;
            if (MinimalSeedAllowedEntityTypes.Contains(entityType.ClrType))
                continue;

            // Permission links are the only expected shared-type entity. Any future
            // shared/property-bag join must be explicitly reviewed before Minimal Seed
            // can run, because it may represent business data.
            if (entityType.IsPropertyBag)
            {
                if (string.Equals(tableName, "RolePermissions", StringComparison.Ordinal))
                    continue;
                throw new InvalidOperationException($"Minimal Development seed refuses unreviewed shared entity/table '{entityType.Name}' / '{tableName}'.");
            }

            var existsTask = (Task<bool>)HasAnyRowsAsyncMethod
                .MakeGenericMethod(entityType.ClrType)
                .Invoke(null, [context])!;
            if (await existsTask)
                unexpectedTables.Add($"{entityType.GetSchema() ?? "dbo"}.{tableName}");
        }

        if (unexpectedTables.Count > 0)
            throw new InvalidOperationException($"Minimal Development seed refuses nonempty non-system tables: {string.Join(", ", unexpectedTables.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(name => name, StringComparer.OrdinalIgnoreCase))}.");
    }

    private static Task<bool> HasAnyRowsAsync<TEntity>(AppDbContext context) where TEntity : class =>
        context.Set<TEntity>().IgnoreQueryFilters().AnyAsync();

    private static async Task SeedIdentityAsync(AppDbContext context, int temporaryPasswordExpiryHours)
    {
        await EnsureDevelopmentTenantRegistryAsync(context);

        var passwordHasher = new PasswordHasher<User>();
        var existingRoles = await context.Roles.Include(r => r.Permissions).ToListAsync();
        var existingPermissions = await context.Permissions.ToListAsync();
        var existingUsers = await context.Users.ToListAsync();
        var existingAssignments = await context.UserRoles.ToListAsync();
        var existingAdmin = existingUsers.FirstOrDefault(user => user.Id == AdminUserId)
            ?? existingUsers.FirstOrDefault(user => string.Equals(user.Email, SeededDevelopmentAdmin.Email, StringComparison.OrdinalIgnoreCase));
        var seededAdminUserId = existingAdmin?.Id ?? AdminUserId;
        var requiresBootstrapPassword = existingAdmin is null
            || string.IsNullOrEmpty(existingAdmin.PasswordHash)
            || existingAdmin.PasswordHash.StartsWith("$2a$", StringComparison.Ordinal)
            || existingAdmin.PasswordHash.StartsWith("$2b$", StringComparison.Ordinal)
            || existingAdmin.PasswordHash.StartsWith("$2y$", StringComparison.Ordinal);
        var adminBootstrapPassword = requiresBootstrapPassword
            ? Environment.GetEnvironmentVariable("NEXUS_ADMIN_BOOTSTRAP_PASSWORD")
            : null;
        if (requiresBootstrapPassword && (string.IsNullOrWhiteSpace(adminBootstrapPassword) || adminBootstrapPassword.Length < 12))
            throw new InvalidOperationException("Set NEXUS_ADMIN_BOOTSTRAP_PASSWORD through a secure Development secret source (minimum 12 characters) before seeding a new or legacy-hash Admin account.");
        var newAdminPasswordHash = existingAdmin is null
            ? passwordHasher.HashPassword(new User(), adminBootstrapPassword!)
            : null;
        var bootstrapTimestampUtc = DateTime.UtcNow;

        // The code-defined canonical catalog is the single seed source for permissions.
        var permissions = PermissionCatalog.CreateSeedPermissions(TenantId);

        var permissionsByKey = permissions.ToDictionary(p => $"{p.Resource}|{p.Action}");
        foreach (var permission in permissions)
        {
            if (!existingPermissions.Any(e => e.Resource == permission.Resource && e.Action == permission.Action))
                context.Permissions.Add(permission);
        }

        Role CreateSystemRole(Guid id, string systemRoleKey, string name, string description, string category)
        {
            var role = new Role
            {
                Id = id,
                TenantId = TenantId,
                Name = name,
                Description = description,
                Category = category,
                IsSystem = true,
                CreatedBy = "System",
                Permissions = new List<Permission>()
            };
            role.AssignSystemRoleKey(systemRoleKey, TenantId);
            return role;
        }

        // Roles and their stable, code-defined identities are seed metadata. Display
        // names remain editable and are not used to identify persisted system roles.
        var roles = new List<Role>
        {
            CreateSystemRole(AdminRoleId, Role.TenantAdminSystemRoleKey, "admin", "Tenant-scoped administration and approved PMO capabilities", "System"),
            CreateSystemRole(ExecutiveRoleId, "tenant.executive", "executive", "Executive leadership access", "Leadership"),
            CreateSystemRole(PortfolioManagerRoleId, "tenant.portfolio-manager", "portfolio.manager", "Portfolio management access", "Management"),
            CreateSystemRole(ProjectManagerRoleId, "tenant.project-manager", "project.manager", "Project management access", "Management"),
            CreateSystemRole(TeamMemberRoleId, "tenant.team-member", "team.member", "Team member access", "Operations"),
            CreateSystemRole(PmoAdminRoleId, "tenant.pmo-admin", "pmo.admin", "PMO administration access", "PMO"),
            CreateSystemRole(FinanceControllerRoleId, "tenant.finance-controller", "finance.controller", "Financial management access", "Finance"),
            CreateSystemRole(ResourceManagerRoleId, "tenant.resource-manager", "resource.manager", "Resource management access", "Management")
        };

        // Permission-to-role mapping by immutable system-role identity.
        var rolePermissionKeys = new Dictionary<string, List<string>>(StringComparer.Ordinal)
        {
            [Role.TenantAdminSystemRoleKey] = AdminRoleSeedPermissionKeys
                .Concat(ProtectedAdminProjectSettingsPermissionKeys
                    .Select(key => PermissionCatalog.ByKey[key])
                    .Select(permission => $"{permission.Resource}|{permission.Action}"))
                .Concat(ProtectedTenantAdminPolicy.MandatoryPermissionKeys
                    .Select(key => PermissionCatalog.ByKey[key])
                    .Select(permission => $"{permission.Resource}|{permission.Action}"))
                .Distinct(StringComparer.Ordinal)
                .ToList(),
            ["tenant.portfolio-manager"] = new() { "Portfolio|View", "Project|View", "Report|View", "Project|View", "Project|Edit", "Stakeholders|Read", "Stakeholders|Manage", "ChangeRequest|View", "Handover|View", "Handover|Manage", "Handover|Approve", "Planning|View", "Risk|View", "Portfolio|View", "Portfolio|Create", "Portfolio|Edit", "Portfolio|Archive", "Portfolio|Reactivate", "Program|View", "Program|Create", "Program|Edit", "Program|Archive", "Program|Reactivate", "WorkItem|View", "WorkItem|Create", "WorkItem|Edit", "TimeTracking|ViewOwn", "TimeTracking|ManageOwn", "TimeTracking|ViewTeam", "TimeTracking|ManageTeam", MeetingPermissions.ViewKey, MeetingPermissions.CreateKey, MeetingPermissions.EditKey, DocumentPermissions.ViewKey, DocumentPermissions.CreateKey, DocumentPermissions.EditKey, "StatusReport|View", "StatusReport|Export" },
            ["tenant.project-manager"] = new() { "Project|View", "Resource|View", "Report|View", "Resource|View", "Resource|Assign", "Resource|Create", "Resource|Edit", "Project|View", "Project|Create", "Project|Edit", "Project|Delete", "Stakeholders|Read", "Stakeholders|Manage", "ChangeRequest|View", "ChangeRequest|Manage", "Handover|View", "Handover|Manage", "Handover|Approve", "Risk|View", "Risk|Create", "Risk|Edit", "Risk|Delete", "Risk|Mitigate", "Planning|View", "Planning|Edit", "Program|View", "WorkItem|View", "WorkItem|Create", "WorkItem|Edit", "TimeTracking|ViewOwn", "TimeTracking|ManageOwn", "TimeTracking|ViewTeam", "TimeTracking|ManageTeam", MeetingPermissions.ViewKey, MeetingPermissions.CreateKey, MeetingPermissions.EditKey, MeetingPermissions.ManageKey, MeetingPermissions.DeleteKey, DocumentPermissions.ViewKey, DocumentPermissions.CreateKey, DocumentPermissions.EditKey, "StatusReport|View", "StatusReport|Create", "StatusReport|Edit", "StatusReport|Export", "StatusReport|ManageConfiguration" },
            ["tenant.team-member"] = new() { "Project|View", "Project|View", "Stakeholders|Read", "Risk|View", "Planning|View", "Portfolio|View", "Program|View", "WorkItem|View", "WorkItem|Create", "WorkItem|Edit", "TimeTracking|ViewOwn", "TimeTracking|ManageOwn", MeetingPermissions.ViewKey, DocumentPermissions.ViewKey, DocumentPermissions.CreateKey },
            ["tenant.executive"] = new() { "Project|View", "Project|Delete", "Portfolio|View", "Resource|View", "Users|View", "ProjectFinance|View", "Report|View", "Resource|Assign", "Resource|ViewCostRate", "Resource|EditCostRate", "Project|Create", "Project|Edit", "Stakeholders|Read", "Stakeholders|Manage", "ChangeRequest|View", "ChangeRequest|Manage", "ChangeRequest|Approve", "Handover|View", "Handover|Manage", "Handover|Approve", "Risk|View", "Risk|Create", "Risk|Edit", "Risk|Delete", "Risk|Mitigate", "Planning|View", "Planning|Edit", "Planning|Lifecycle", "Portfolio|Create", "Portfolio|Edit", "Portfolio|Archive", "Portfolio|Reactivate", "Program|View", "Program|Create", "Program|Edit", "Program|Archive", "Program|Reactivate", "WorkItem|View", "TimeTracking|ViewOwn", "TimeTracking|ManageOwn", "TimeTracking|ViewTeam", "TimeTracking|ManageTeam", MeetingPermissions.ViewKey, DocumentPermissions.ViewKey, "StatusReport|View", "StatusReport|Export" },
            ["tenant.pmo-admin"] = new() { "Project|View", "Project|Delete", "Portfolio|View", "Resource|View", "Users|View", "ProjectFinance|View", "Report|View", "Settings|View", "Resource|Assign", "Resource|ViewCostRate", "Resource|EditCostRate", "Project|Create", "Project|Edit", "Stakeholders|Read", "Stakeholders|Manage", "ChangeRequest|View", "ChangeRequest|Manage", "ChangeRequest|Approve", "Handover|View", "Handover|Manage", "Handover|Approve", "Handover|AdminInitiate", "Governance.AssignmentRecovery|View", "Governance.AssignmentRecovery|Initiate", "Governance.AssignmentRecovery|Manage", "Risk|View", "Risk|Create", "Risk|Edit", "Risk|Delete", "Risk|Mitigate", "Planning|View", "Planning|Edit", "Planning|Lifecycle", "Portfolio|Create", "Portfolio|Edit", "Portfolio|Archive", "Portfolio|Reactivate", "Program|View", "Program|Create", "Program|Edit", "Program|Archive", "Program|Reactivate", "WorkItem|View", "WorkItem|Create", "WorkItem|Edit", "WorkItem|Delete", "Workflow|View", "Workflow|Manage", "Workflow|Publish", "TimeTracking|ViewOwn", "TimeTracking|ManageOwn", "TimeTracking|ViewTeam", "TimeTracking|ManageTeam", MeetingPermissions.ViewKey, MeetingPermissions.CreateKey, MeetingPermissions.EditKey, MeetingPermissions.ManageKey, MeetingPermissions.DeleteKey, DocumentPermissions.ViewKey, DocumentPermissions.CreateKey, DocumentPermissions.EditKey, DocumentPermissions.ArchiveKey, DocumentPermissions.TemplateManageKey, "StatusReport|View", "StatusReport|Create", "StatusReport|Edit", "StatusReport|Publish", "StatusReport|Export", "StatusReport|ManageConfiguration", "Audit|View" },
            ["tenant.finance-controller"] = new() { "ProjectFinance|View", "Budget|View", "Budget|Create", "Budget|Edit", "Budget|Delete", "BudgetRevision|View", "BudgetRevision|Create", "BudgetRevision|EditDraft", "BudgetRevision|SubmitForReview", "BudgetRevision|Approve", "BudgetRevision|Cancel", "BudgetRevision|Delete", "Cost|View", "Cost|Create", "Cost|Edit", "Cost|Delete", "Commitment|View", "Commitment|Create", "Commitment|Edit", "Commitment|Close", "Commitment|Cancel", "Commitment|Delete", "PaymentObligation|View", "PaymentObligation|Create", "PaymentObligation|Edit", "PaymentObligation|Delete", "PaymentObligationSettlement|Record", "Report|View", "Project|View", "Stakeholders|Read", "Portfolio|View", "Program|View", DocumentPermissions.ViewKey },
            ["tenant.resource-manager"] = new() { "Resource|View", "Project|View", "Resource|View", "Resource|Assign", "Resource|ViewCostRate", "Resource|EditCostRate", "Project|View", "Stakeholders|Read", "Risk|View", "Planning|View", "WorkItem|View", "TimeTracking|ViewOwn", "TimeTracking|ManageOwn", "TimeTracking|ViewTeam", "TimeTracking|ManageTeam", MeetingPermissions.ViewKey, DocumentPermissions.ViewKey }
        };

        // Preserve existing route access while assigning the new explicit read capabilities.
        foreach (var roleKeys in rolePermissionKeys.Values)
        {
            if (roleKeys.Contains("WorkItem|View", StringComparer.Ordinal))
                roleKeys.Add("Issue|View");
            if (roleKeys.Contains("Settings|View", StringComparer.Ordinal))
                roleKeys.Add("Plugin|View");
        }

        var invalidRolePermissionKeys = rolePermissionKeys
            .SelectMany(pair => pair.Value.Distinct(StringComparer.Ordinal)
                .Where(key => !permissionsByKey.ContainsKey(key))
                .Select(key => $"{pair.Key}: {key}"))
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();
        if (invalidRolePermissionKeys.Length > 0)
            throw new InvalidOperationException($"Development role seed references missing or inactive canonical permissions: {string.Join(", ", invalidRolePermissionKeys)}.");

        var resolvedRoleIdsBySystemKey = new Dictionary<string, Guid>(StringComparer.Ordinal);
        foreach (var role in roles)
        {
            var expectedSystemRoleKey = role.SystemRoleKey
                ?? throw new InvalidOperationException($"Seed role '{role.Id}' has no stable system identity.");
            var existing = existingRoles.FirstOrDefault(r => r.TenantId == role.TenantId
                    && string.Equals(r.SystemRoleKey, expectedSystemRoleKey, StringComparison.Ordinal))
                ?? existingRoles.FirstOrDefault(r => r.Id == role.Id && r.TenantId == role.TenantId);
            if (existing is null && existingRoles.Any(r => r.TenantId == role.TenantId
                    && string.Equals(r.Name, role.Name, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException($"A conflicting Role already uses the seeded system-role name '{role.Name}' in this Tenant.");

            var roleEntity = existing ?? role;
            roleEntity.AssignSystemRoleKey(expectedSystemRoleKey, role.TenantId);
            resolvedRoleIdsBySystemKey.Add(expectedSystemRoleKey, roleEntity.Id);

            if (existing is null)
                context.Roles.Add(role);

            // Seed grants only when defining a new role. The protected tenant Admin
            // also receives its catalog-derived mandatory baseline and the narrowly
            // approved, append-only Project Settings grants below; all other existing
            // role grants remain untouched.
            var keys = rolePermissionKeys[expectedSystemRoleKey].Distinct(StringComparer.Ordinal);
            var isProtectedExistingAdmin = existing is not null
                && ProtectedTenantAdminPolicy.IsProtectedSystemAdminRole(roleEntity)
                && roleEntity.TenantId == TenantId
                && !roleEntity.IsDeleted;
            var originalProtectedAdminKeys = isProtectedExistingAdmin
                ? roleEntity.Permissions
                    .Where(permission => permission.TenantId == TenantId && !permission.IsDeleted)
                    .Select(permission => $"{permission.Resource}.{permission.Action}")
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(key => key, StringComparer.Ordinal)
                    .ToArray()
                : [];
            var addedProtectedAdminProjectSettingsKeys = new List<string>();

            foreach (var key in keys)
            {
                var permission = permissionsByKey[key];

                var permissionEntity = existingPermissions.FirstOrDefault(e => e.Resource == permission.Resource && e.Action == permission.Action)
                    ?? permission;

                if (existing is null)
                    roleEntity.Permissions.Add(permissionEntity);
                else if (isProtectedExistingAdmin
                    && ProtectedTenantAdminPolicy.MandatoryPermissionKeys.Contains(permission.Name)
                    && !roleEntity.Permissions.Any(linked => linked.Id == permissionEntity.Id))
                    roleEntity.Permissions.Add(permissionEntity);
                else if (isProtectedExistingAdmin
                    && ProtectedAdminProjectSettingsPermissionKeys.Contains(permission.Name, StringComparer.Ordinal)
                    && !roleEntity.Permissions.Any(linked => linked.Id == permissionEntity.Id))
                {
                    roleEntity.Permissions.Add(permissionEntity);
                    addedProtectedAdminProjectSettingsKeys.Add(permission.Name);
                }
            }

            if (addedProtectedAdminProjectSettingsKeys.Count > 0)
            {
                var updatedProtectedAdminKeys = roleEntity.Permissions
                    .Where(permission => permission.TenantId == TenantId && !permission.IsDeleted)
                    .Select(permission => $"{permission.Resource}.{permission.Action}")
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(key => key, StringComparer.Ordinal)
                    .ToArray();
                context.AuditEntries.Add(new AuditEntry
                {
                    TenantId = TenantId,
                    EntityType = "Role",
                    EntityId = roleEntity.Id,
                    Action = "ProtectedAdminProjectSettingsGrants.Reconciled",
                    OriginalValues = JsonSerializer.Serialize(new { PermissionKeys = originalProtectedAdminKeys }),
                    NewValues = JsonSerializer.Serialize(new
                    {
                        PermissionKeys = updatedProtectedAdminKeys,
                        Added = addedProtectedAdminProjectSettingsKeys.OrderBy(key => key, StringComparer.Ordinal).ToArray()
                    }),
                    ChangedFields = "Permissions",
                    Description = "Added the approved Project Settings permissions to the stable protected tenant Admin role without changing unrelated grants or user Deny overrides.",
                    Source = AuditSources.System,
                    UserName = "Development seed reconciliation",
                    CreatedBy = "System"
                });
            }
        }

        await context.SaveChangesAsync();

        // Users: only the single administrator account. Demo role accounts (executive,
        // portfolio.manager, project.manager, team.member, pmo.admin, finance.controller,
        // resource.manager), the named Project Manager accounts and the generated
        // user1..user80 accounts are intentionally not seeded, so a fresh tenant has no
        // user accounts beyond the administrator.
        var users = new List<User>
        {
            new() { Id = AdminUserId, TenantId = TenantId, Email = SeededDevelopmentAdmin.Email, PasswordHash = newAdminPasswordHash!, FirstName = "Alex", LastName = "Admin", IsActive = true, MustChangePassword = true, TemporaryPasswordExpiresAt = bootstrapTimestampUtc.AddHours(temporaryPasswordExpiryHours), PasswordChangedAt = bootstrapTimestampUtc, CreatedBy = "System" }
        };

        foreach (var user in users)
        {
            // Match by deterministic Id first so re-seeding is idempotent even if the
            // email changed between seed revisions; fall back to email for legacy rows.
            var existing = existingUsers.FirstOrDefault(u => u.Id == user.Id)
                ?? existingUsers.FirstOrDefault(u => u.Email == user.Email);
            if (existing is null)
            {
                context.Users.Add(user);
                continue;
            }

            // Keep deterministic seed values authoritative when re-seeding.
            existing.Email = user.Email;
            existing.FirstName = user.FirstName;
            existing.LastName = user.LastName;
            existing.IsActive = true;

            // Bootstrap a new or legacy BCrypt-hash account from a secure environment
            // secret, then require the administrator to choose a new password.
            var currentHash = existing.PasswordHash;
            if (string.IsNullOrEmpty(currentHash) ||
                currentHash.StartsWith("$2a$", StringComparison.Ordinal) ||
                currentHash.StartsWith("$2b$", StringComparison.Ordinal) ||
                currentHash.StartsWith("$2y$", StringComparison.Ordinal))
            {
                existing.PasswordHash = passwordHasher.HashPassword(existing, adminBootstrapPassword!);
                existing.MustChangePassword = true;
                existing.TemporaryPasswordExpiresAt = bootstrapTimestampUtc.AddHours(temporaryPasswordExpiryHours);
                existing.PasswordChangedAt = bootstrapTimestampUtc;
                existing.UpdatedAt = bootstrapTimestampUtc;
            }
        }

        await context.SaveChangesAsync();

        // User-role assignments (deterministic IDs, idempotent). Only the administrator
        // gets a seeded role; the other roles stay defined so they can be assigned later.
        var assignments = new List<(Guid id, Guid userId, Guid roleId)>
        {
            (Guid.Parse("f1000001-0000-0000-0000-000000000001"), seededAdminUserId, resolvedRoleIdsBySystemKey[Role.TenantAdminSystemRoleKey])
        };

        foreach (var (id, userId, roleId) in assignments)
        {
            if (existingAssignments.Any(a => a.UserId == userId && a.RoleId == roleId))
                continue;

            context.UserRoles.Add(new UserRole
            {
                Id = id,
                TenantId = TenantId,
                UserId = userId,
                RoleId = roleId,
                CreatedBy = "System"
            });
        }

        await context.SaveChangesAsync();

        // Purge every account created by an earlier seed revision so demo users cannot
        // survive a re-seed. Matching is done on the deterministic seed id ranges
        // (80000001-* generated, 90000001-* named PMs) and the legacy "pmNN@nexus.com"
        // shape. The administrator is always kept, and the pattern for legacy accounts
        // is anchored so pmo.admin@nexus.com is never matched.
        var legacyPmEmailPattern = new System.Text.RegularExpressions.Regex(@"^pm\d+@nexus\.com$", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);

        var demoRoleUserIds = new[]
        {
            ExecutiveUserId, PortfolioManagerUserId, ProjectManagerUserId, TeamMemberUserId,
            PmoAdminUserId, FinanceControllerUserId, ResourceManagerUserId
        };

        var namedPmUserIds = Enumerable.Range(0, 5)
            .Select(i => Guid.Parse($"90000001-{i:D4}-0000-0000-000000000000"))
            .ToList();

        var generatedUserIds = Enumerable.Range(0, 80)
            .Select(i => Guid.Parse($"80000001-{i:D4}-0000-0000-000000000000"))
            .ToList();

        var demoUsers = existingUsers
            .Where(u => u.Id != AdminUserId &&
                        (demoRoleUserIds.Contains(u.Id) || namedPmUserIds.Contains(u.Id) || generatedUserIds.Contains(u.Id) || legacyPmEmailPattern.IsMatch(u.Email)))
            .ToList();

        foreach (var demo in demoUsers)
        {
            var stale = existingAssignments.Where(a => a.UserId == demo.Id).ToList();
            context.UserRoles.RemoveRange(stale);
            context.Users.Remove(demo);
        }

        if (demoUsers.Count > 0)
            await context.SaveChangesAsync();
    }

    private static async Task AcquireSqlServerAdminMutationLockAsync(
        AppDbContext context,
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.Transaction = transaction.GetDbTransaction();
        command.CommandText = "DECLARE @result int; EXEC @result = sys.sp_getapplock @Resource = @resource, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 15000; SELECT @result;";
        var resource = command.CreateParameter();
        resource.ParameterName = "@resource";
        resource.DbType = System.Data.DbType.String;
        resource.Value = $"NEXUS:TenantAdminMutation:{tenantId:N}";
        command.Parameters.Add(resource);
        var result = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
        if (result < 0)
            throw new InvalidOperationException($"Could not acquire the tenant administration mutation lock (SQL Server result {result}).");
    }

    private static async Task EnsureDocumentCatalogAsync(AppDbContext context)
    {
        var definitions = new[]
        {
            new { Id = Guid.Parse("d0000201-0000-0000-0000-000000000001"), Code = "GOV", Name = "Governance", Description = "Charters, decisions, and governance records" },
            new { Id = Guid.Parse("d0000201-0000-0000-0000-000000000002"), Code = "TECH", Name = "Technical", Description = "Architecture, design, and engineering records" },
            new { Id = Guid.Parse("d0000201-0000-0000-0000-000000000003"), Code = "FIN", Name = "Financial", Description = "Budgets, forecasts, and financial records" },
            new { Id = Guid.Parse("d0000201-0000-0000-0000-000000000004"), Code = "MEET", Name = "Meeting", Description = "Agendas, minutes, and meeting outputs" },
            new { Id = Guid.Parse("d0000201-0000-0000-0000-000000000005"), Code = "RISK", Name = "Risk", Description = "Risk and assurance records" }
        };
        var existing = await context.DocumentCategories.IgnoreQueryFilters().Where(c => c.TenantId == TenantId).ToListAsync();
        foreach (var definition in definitions)
        {
            if (existing.Any(c => c.Id == definition.Id)) continue;
            context.DocumentCategories.Add(new DocumentCategory { Id = definition.Id, TenantId = TenantId, Code = definition.Code, Name = definition.Name, Description = definition.Description, SortOrder = Array.IndexOf(definitions, definition), CreatedBy = "System" });
        }
        await context.SaveChangesAsync();
    }

    private static async Task EnsureDevelopmentTenantRegistryAsync(AppDbContext context)
    {
        if (await context.Tenants.AnyAsync(tenant => tenant.Id == TenantId))
            return;

        context.Tenants.Add(new Tenant
        {
            Id = TenantId,
            Code = "FOPX-DEMO",
            DisplayName = "FOPX Development Demo Tenant",
            Status = TenantLifecycleStatus.Active,
            CreatedAt = SeedTimestampUtc,
            CreatedBy = "Development seed"
        });
        await context.SaveChangesAsync();
    }

    private static async Task EnsureMinimalDevelopmentOrganizationAsync(AppDbContext context)
    {
        var organizationId = Guid.Parse("90000001-0000-0000-0000-000000000001");
        if (await context.Organizations.IgnoreQueryFilters()
                .AnyAsync(organization => organization.Id == organizationId && organization.TenantId == TenantId))
            return;

        context.Organizations.Add(new Organization
        {
            Id = organizationId,
            TenantId = TenantId,
            Name = "NEXUS Group",
            Code = "NEXUS",
            Description = "Enterprise operating organization",
            CreatedBy = "System"
        });
        await context.SaveChangesAsync();
    }

    private static void ValidateTemporaryPasswordExpiryHours(int hours)
    {
        if (hours is < 1 or > 168)
            throw new ArgumentOutOfRangeException(nameof(hours), "Temporary password expiry must be between 1 and 168 hours.");
    }
}
