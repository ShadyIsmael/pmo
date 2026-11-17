using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NEXUS.Modules.Identity.Application;
using NEXUS.Modules.Identity.Domain;
using NEXUS.Modules.Organization.Application.Settings;
using NEXUS.Modules.ResourceManagement.Domain;
using NEXUS.Modules.RiskManagement.Domain;
using NEXUS.Persistence;
using Xunit;

namespace NEXUS.Modules.ResourceManagement.Tests;

public class SeedDataProviderTests : IDisposable
{
    private readonly string? _originalEnvironment;
    private readonly string? _originalBootstrapPassword;
    private readonly string? _originalProjectSeedReset;

    public SeedDataProviderTests()
    {
        _originalEnvironment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        _originalBootstrapPassword = Environment.GetEnvironmentVariable("NEXUS_ADMIN_BOOTSTRAP_PASSWORD");
        _originalProjectSeedReset = Environment.GetEnvironmentVariable("NEXUS_RESET_PROJECT_SEED");

        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        Environment.SetEnvironmentVariable("NEXUS_ADMIN_BOOTSTRAP_PASSWORD",
            Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)));
        Environment.SetEnvironmentVariable("NEXUS_RESET_PROJECT_SEED", null);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", _originalEnvironment);
        Environment.SetEnvironmentVariable("NEXUS_ADMIN_BOOTSTRAP_PASSWORD", _originalBootstrapPassword);
        Environment.SetEnvironmentVariable("NEXUS_RESET_PROJECT_SEED", _originalProjectSeedReset);
    }

    [Fact]
    public void Permission_protection_uses_only_explicit_central_policy_keys()
    {
        var protectedKey = PermissionCatalog.ByKey["Governance.AssignmentRecovery.View"].Key;
        var policy = new PermissionProtectionPolicy(Options.Create(new PermissionProtectionOptions
        {
            ProtectedPermissionKeys = [protectedKey]
        }));

        PermissionProtectionPolicy.Empty.ProtectedPermissionKeys.Should().BeEmpty();
        PermissionCatalog.Entries.Where(entry => entry.IsActive)
            .Should().OnlyContain(entry => PermissionProtectionPolicy.Empty.IsGrantable(entry));
        policy.IsProtected(protectedKey).Should().BeTrue();
        policy.IsGrantable(PermissionCatalog.ByKey[protectedKey]).Should().BeFalse();
        policy.IsGrantable(PermissionCatalog.ByKey["Workflow.View"]).Should().BeTrue();
    }

    [Fact]
    public async Task SeedIdentityOnlyAsync_NewAdminRole_SeedsProtectedCoreWithoutAllAssignableOperationalPermissions()
    {
        var originalEnvironment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");

        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            await using var context = new AppDbContext(options);
            await SeedDataProvider.SeedIdentityOnlyAsync(context);

            var activeCatalogKeys = PermissionCatalog.Entries
                .Where(item => item.IsActive)
                .Select(item => item.Key)
                .ToArray();
            var seededPermissionRows = await context.Permissions
                .IgnoreQueryFilters()
                .Where(item => item.TenantId == SeedDataProvider.TenantId)
                .ToListAsync();
            seededPermissionRows.Select(item => item.Name)
                .Should().BeEquivalentTo(activeCatalogKeys,
                    "fresh identity seeding must persist every active canonical permission and no retired catalog entry");
            seededPermissionRows.Select(item => $"{item.Resource}|{item.Action}")
                .Should().OnlyHaveUniqueItems("one persisted permission row represents one canonical capability");

            var role = await context.Roles
                .IgnoreQueryFilters()
                .Include(item => item.Permissions)
                .SingleAsync(item => item.TenantId == SeedDataProvider.TenantId
                    && item.SystemRoleKey == Role.TenantAdminSystemRoleKey);
            role.SystemRoleKey.Should().Be(Role.TenantAdminSystemRoleKey,
                "the protected tenant Admin must be identified by a stable system key, not its display name");
            var permissionKeys = role.Permissions.Select(item => $"{item.Resource}.{item.Action}").ToHashSet(StringComparer.Ordinal);

            var seededSystemRoles = await context.Roles.IgnoreQueryFilters()
                .Where(item => item.TenantId == SeedDataProvider.TenantId && item.IsSystem)
                .ToArrayAsync();
            seededSystemRoles.Should().OnlyContain(item => !string.IsNullOrWhiteSpace(item.SystemRoleKey));
            seededSystemRoles.Select(item => item.SystemRoleKey).Should().OnlyHaveUniqueItems();
            seededSystemRoles.Select(item => item.SystemRoleKey).Should().BeEquivalentTo(
                new[] { Role.TenantAdminSystemRoleKey, Role.OrganizationAdminSystemRoleKey },
                "only Tenant Admin and Organization Admin are System Roles");

            var protectedKeys = PermissionProtectionPolicy.Empty.ProtectedPermissionKeys;
            protectedKeys.Should().BeEmpty("the Product Owner has not designated any protected permission keys");
            PermissionCatalog.Entries.Where(entry => entry.IsActive)
                .Should().OnlyContain(entry => PermissionProtectionPolicy.Empty.IsGrantable(entry));
            var assignableOperationalKeys = PermissionCatalog.Entries
                .Where(entry => entry.IsActive
                    && entry.IsAssignable
                    && entry.Scope is PermissionScope.Tenant or PermissionScope.Organization or PermissionScope.Portfolio
                        or PermissionScope.Program or PermissionScope.Project
                    && !protectedKeys.Contains(entry.Key))
                .Select(entry => entry.Key)
                .ToHashSet(StringComparer.Ordinal);
            assignableOperationalKeys.Except(permissionKeys, StringComparer.Ordinal).Should().NotBeEmpty(
                "the protected Admin does not automatically receive every assignable operational permission");
            permissionKeys.Should().Contain([
                "Governance.AssignmentRecovery.View",
                "Governance.AssignmentRecovery.Initiate",
                "Governance.AssignmentRecovery.Manage",
                "Workflow.View",
                "Workflow.Manage",
                "Workflow.Publish"
            ]);
            permissionKeys.Should().Contain("Roles.Delete");
            permissionKeys.Should().NotContain("Program.CreateAcrossPortfolios", "cross-Portfolio creation stays an explicit opt-in permission");
            permissionKeys.Should().NotContain("ProjectAnnualBudget.Approve", "financial approval is not required for protected Tenant Admin delegation");
            permissionKeys.Should().NotContain("ProjectFinancialCeiling.ApproveChange", "financial approval is not required for protected Tenant Admin delegation");
            permissionKeys.Should().NotContain(key => key.StartsWith("Platform", StringComparison.Ordinal), "tenant Admin has no platform authority");
            permissionKeys.Should().Contain("Users.ManageAccounts", "the existing explicit seed grant is preserved without making it a protection rule");

            var pmoAdminRole = await context.Roles
                .IgnoreQueryFilters()
                .Include(item => item.Permissions)
                .SingleAsync(item => item.Name == "pmo.admin" && item.TenantId == SeedDataProvider.TenantId);
            var pmoAdminPermissionKeys = pmoAdminRole.Permissions
                .Select(item => $"{item.Resource}.{item.Action}")
                .ToHashSet(StringComparer.Ordinal);
            pmoAdminPermissionKeys.Should().Contain(["Roles.View", "Roles.Create", "Roles.EditDetails", "Roles.ConfigurePermissions"]);
            pmoAdminPermissionKeys.Should().NotContain("Roles.Delete");
            pmoAdminPermissionKeys.Should().NotContain("TenantAdministration.ManageScope");
            pmoAdminPermissionKeys.Should().NotContain("Settings.ManageTenantConfiguration");
            pmoAdminPermissionKeys.Should().NotContain("ReferenceData.Manage");
            pmoAdminPermissionKeys.Should().NotContain(["ProjectSettings.View", "ProjectSettings.Edit"],
                "the protected Project Settings grant is bound to the stable tenant Admin system role, not its display name");

            var portfolioManagerRole = await context.Roles
                .IgnoreQueryFilters()
                .Include(item => item.Permissions)
                .SingleAsync(item => item.Name == "portfolio.manager" && item.TenantId == SeedDataProvider.TenantId);
            var portfolioManagerPermissionKeys = portfolioManagerRole.Permissions
                .Select(item => $"{item.Resource}.{item.Action}")
                .ToHashSet(StringComparer.Ordinal);
            portfolioManagerPermissionKeys.Should().Contain(["StatusReport.View", "StatusReport.Export"]);

            var projectManagerRole = await context.Roles
                .IgnoreQueryFilters()
                .Include(item => item.Permissions)
                .SingleAsync(item => item.Name == "project.manager" && item.TenantId == SeedDataProvider.TenantId);
            projectManagerRole.RoleKey.Should().Be("tenant.project-manager");
            projectManagerRole.SystemRoleKey.Should().BeNull();
            projectManagerRole.IsSystem.Should().BeFalse();
            projectManagerRole.Category.Should().Be(RoleCategories.Management);
            var projectManagerPermissionKeys = projectManagerRole.Permissions
                .Select(item => $"{item.Resource}.{item.Action}")
                .ToHashSet(StringComparer.Ordinal);
            projectManagerPermissionKeys.Should().Contain([
                "StatusReport.View", "StatusReport.Create", "StatusReport.Edit",
                "StatusReport.Export", "StatusReport.ManageConfiguration"
            ]);

            var organizationAdmin = await context.Roles
                .IgnoreQueryFilters()
                .Include(item => item.Permissions)
                .SingleAsync(item => item.TenantId == SeedDataProvider.TenantId
                    && item.RoleKey == Role.OrganizationAdminSystemRoleKey);
            organizationAdmin.IsSystem.Should().BeTrue();
            organizationAdmin.SystemRoleKey.Should().Be(Role.OrganizationAdminSystemRoleKey);
            organizationAdmin.RoleKey.Should().Be(Role.OrganizationAdminSystemRoleKey);
            organizationAdmin.Category.Should().Be(RoleCategories.Organization);
            organizationAdmin.Permissions.Select(item => item.Name).Should().BeEquivalentTo(
                new[] { "OrganizationConfiguration.View", "OrganizationConfiguration.Manage" },
                "Organization Admin receives only canonical Organization Configuration permissions");
            foreach (var permission in organizationAdmin.Permissions)
            {
                PermissionCatalog.ByKey.ContainsKey(permission.Name).Should().BeTrue();
                var definition = PermissionCatalog.ByKey[permission.Name];
                definition.Scope.Should().Be(PermissionScope.Organization);
                definition.IsAssignable.Should().BeFalse();
            }
            PermissionCatalog.ByKey["OrganizationConfiguration.Manage"].RequiredPermissionKeys
                .Should().Contain("OrganizationConfiguration.View");
            (await context.UserRoles.IgnoreQueryFilters()
                .AnyAsync(item => item.TenantId == SeedDataProvider.TenantId && item.RoleId == organizationAdmin.Id))
                .Should().BeFalse("seeding must not appoint an Organization Admin without its approved Organization-scoped assignment policy");

            var seededRoleGrants = await context.Roles
                .IgnoreQueryFilters()
                .Include(item => item.Permissions)
                .ToListAsync();
            seededRoleGrants.Should().OnlyContain(role =>
                role.Permissions.Select(permission => permission.Id).Distinct().Count() == role.Permissions.Count,
                "repeated entries in deterministic seed arrays must not create duplicate role grants");
        }
        finally
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", originalEnvironment);
        }
    }

    [Fact]
    public void SystemRoleIdentity_CanBeAssignedOnceAndCannotBeReplaced()
    {
        var role = new Role
        {
            TenantId = SeedDataProvider.TenantId,
            IsSystem = true,
            CreatedBy = "tenant-provisioning-actor"
        };

        role.AssignSystemRoleKey(Role.TenantAdminSystemRoleKey, SeedDataProvider.TenantId);
        role.SystemRoleKey.Should().Be(Role.TenantAdminSystemRoleKey);
        var assignDifferentKey = () => role.AssignSystemRoleKey("tenant.attacker-controlled-key", SeedDataProvider.TenantId);
        assignDifferentKey.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public async Task SeedIdentityOnlyAsync_Rerun_PreservesExistingGrants_AndDoesNotRestoreRemovedGrants()
    {
        var originalEnvironment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");

        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            await using var context = new AppDbContext(options);
            await SeedDataProvider.SeedIdentityOnlyAsync(context);

            var role = await context.Roles
                .IgnoreQueryFilters()
                .Include(item => item.Permissions)
                .SingleAsync(item => item.TenantId == SeedDataProvider.TenantId
                    && item.SystemRoleKey == Role.TenantAdminSystemRoleKey);
            var projectSettingsKeys = new HashSet<string>(["ProjectSettings.View", "ProjectSettings.Edit"], StringComparer.Ordinal);
            var removedExistingGrantKey = "Document.View";
            var reconciliationKeys = projectSettingsKeys.Append(removedExistingGrantKey).ToHashSet(StringComparer.Ordinal);
            foreach (var permission in role.Permissions
                .Where(item => reconciliationKeys.Contains($"{item.Resource}.{item.Action}"))
                .ToList())
            {
                role.Permissions.Remove(permission);
            }

            var preservedCustomGrant = await context.Permissions.IgnoreQueryFilters()
                .SingleAsync(item => item.Name == "Program.CreateAcrossPortfolios");
            role.Permissions.Add(preservedCustomGrant);

            var adminUserId = await context.UserRoles.IgnoreQueryFilters()
                .Where(item => item.TenantId == SeedDataProvider.TenantId && item.RoleId == role.Id && !item.IsDeleted)
                .Select(item => item.UserId)
                .SingleAsync();
            var settingsEditPermission = await context.Permissions.IgnoreQueryFilters()
                .SingleAsync(item => item.TenantId == SeedDataProvider.TenantId && item.Name == "ProjectSettings.Edit");
            var preservedDeny = new UserPermissionOverride
            {
                TenantId = SeedDataProvider.TenantId,
                UserId = adminUserId,
                PermissionId = settingsEditPermission.Id,
                Effect = PermissionOverrideEffect.Deny,
                CreatedBy = "SeedDataProviderTests"
            };
            context.UserPermissionOverrides.Add(preservedDeny);

            await context.SaveChangesAsync();

            await SeedDataProvider.SeedIdentityOnlyAsync(context);
            await SeedDataProvider.SeedIdentityOnlyAsync(context);

            context.ChangeTracker.Clear();
            var reloadedRole = await context.Roles
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Include(item => item.Permissions)
                .SingleAsync(item => item.Id == role.Id);
            var permissionKeys = reloadedRole.Permissions.Select(item => $"{item.Resource}.{item.Action}").ToHashSet(StringComparer.Ordinal);
            permissionKeys.Should().Contain("Program.CreateAcrossPortfolios", "existing explicit opt-in grants are preserved");
            permissionKeys.Should().NotContain(projectSettingsKeys, "Project Settings grants are not added by reseeding");
            permissionKeys.Should().NotContain(removedExistingGrantKey, "reseeding never restores an existing Admin grant removed by an authorized change");
            var preservedSettingsDeny = await context.UserPermissionOverrides.IgnoreQueryFilters()
                .AsNoTracking()
                .SingleAsync(item => item.Id == preservedDeny.Id);
            preservedSettingsDeny.Effect.Should().Be(PermissionOverrideEffect.Deny);
            preservedSettingsDeny.IsDeleted.Should().BeFalse();
            EffectivePermissionResolver.Resolve(
                    permissionKeys.Append("ProjectSettings.Edit"),
                    [("ProjectSettings.Edit", preservedSettingsDeny.Effect)])
                .Should().NotContain("ProjectSettings.Edit", "seed reconciliation must not erase a user's explicit Deny");
            (await context.Roles.IgnoreQueryFilters()
                .CountAsync(item => item.TenantId == SeedDataProvider.TenantId
                    && item.SystemRoleKey == Role.TenantAdminSystemRoleKey && !item.IsDeleted))
                .Should().Be(1, "repeated seed execution must not create a second protected Admin role");
        }
        finally
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", originalEnvironment);
        }
    }

    [Fact]
    public async Task SeedIdentityOnlyAsync_SetsBootstrapExpiryAndPreservesExistingAdminCredentialsOnRerun()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var context = new AppDbContext(options);
        var bootstrapPassword = Environment.GetEnvironmentVariable("NEXUS_ADMIN_BOOTSTRAP_PASSWORD")!;
        var passwordHasher = new PasswordHasher<User>();

        await SeedDataProvider.SeedIdentityOnlyAsync(context, temporaryPasswordExpiryHours: 6);

        var seededAdmin = await context.Users.IgnoreQueryFilters()
            .SingleAsync(user => user.TenantId == SeedDataProvider.TenantId && user.Email == "admin@nexus.com");
        seededAdmin.MustChangePassword.Should().BeTrue();
        seededAdmin.TemporaryPasswordExpiresAt.Should().BeAfter(DateTime.UtcNow.AddHours(5.9))
            .And.BeBefore(DateTime.UtcNow.AddHours(6.1));
        passwordHasher.VerifyHashedPassword(seededAdmin, seededAdmin.PasswordHash!, bootstrapPassword)
            .Should().Be(PasswordVerificationResult.Success);

        var adminRole = await context.Roles.IgnoreQueryFilters()
            .Include(role => role.Permissions)
            .SingleAsync(role => role.TenantId == SeedDataProvider.TenantId
                && role.SystemRoleKey == Role.TenantAdminSystemRoleKey);
        var existingRoleId = await context.UserRoles.IgnoreQueryFilters()
            .Where(link => link.TenantId == SeedDataProvider.TenantId && link.UserId == seededAdmin.Id)
            .Select(link => link.RoleId)
            .SingleAsync();
        var existingGrantIds = adminRole.Permissions.Select(permission => permission.Id).ToHashSet();

        var strandedTemporaryPassword = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        seededAdmin.PasswordHash = passwordHasher.HashPassword(seededAdmin, strandedTemporaryPassword);
        seededAdmin.MustChangePassword = true;
        seededAdmin.TemporaryPasswordExpiresAt = null;
        await context.SaveChangesAsync();
        var strandedTemporaryHash = seededAdmin.PasswordHash;

        Environment.SetEnvironmentVariable("NEXUS_ADMIN_BOOTSTRAP_PASSWORD",
            Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)));
        await SeedDataProvider.SeedIdentityOnlyAsync(context, temporaryPasswordExpiryHours: 1);

        context.ChangeTracker.Clear();
        seededAdmin = await context.Users.IgnoreQueryFilters()
            .SingleAsync(user => user.TenantId == SeedDataProvider.TenantId && user.Email == "admin@nexus.com");
        seededAdmin.PasswordHash.Should().Be(strandedTemporaryHash);
        seededAdmin.MustChangePassword.Should().BeTrue();
        seededAdmin.TemporaryPasswordExpiresAt.Should().BeNull();

        var permanentPassword = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        seededAdmin.PasswordHash = passwordHasher.HashPassword(seededAdmin, permanentPassword);
        seededAdmin.MustChangePassword = false;
        seededAdmin.TemporaryPasswordExpiresAt = null;
        seededAdmin.PasswordChangedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
        var permanentHash = seededAdmin.PasswordHash;
        var passwordChangedAt = seededAdmin.PasswordChangedAt;

        Environment.SetEnvironmentVariable("NEXUS_ADMIN_BOOTSTRAP_PASSWORD",
            Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)));
        await SeedDataProvider.SeedIdentityOnlyAsync(context, temporaryPasswordExpiryHours: 1);

        context.ChangeTracker.Clear();
        var reseededAdmin = await context.Users.IgnoreQueryFilters()
            .SingleAsync(user => user.TenantId == SeedDataProvider.TenantId && user.Email == "admin@nexus.com");
        reseededAdmin.PasswordHash.Should().Be(permanentHash);
        reseededAdmin.MustChangePassword.Should().BeFalse();
        reseededAdmin.TemporaryPasswordExpiresAt.Should().BeNull();
        reseededAdmin.PasswordChangedAt.Should().Be(passwordChangedAt);
        passwordHasher.VerifyHashedPassword(reseededAdmin, reseededAdmin.PasswordHash!, permanentPassword)
            .Should().Be(PasswordVerificationResult.Success);

        (await context.UserRoles.IgnoreQueryFilters()
            .Where(link => link.TenantId == SeedDataProvider.TenantId && link.UserId == reseededAdmin.Id)
            .Select(link => link.RoleId)
            .SingleAsync()).Should().Be(existingRoleId);
        (await context.Roles.IgnoreQueryFilters()
            .Include(role => role.Permissions)
            .SingleAsync(role => role.Id == existingRoleId))
            .Permissions.Select(permission => permission.Id)
            .ToHashSet()
            .Should().BeEquivalentTo(existingGrantIds);
    }

    [Fact]
    public async Task SeedIdentityOnlyAsync_Rerun_SeedsMissingCatalogRowsAndPreservesOtherRoles()
    {
        var originalEnvironment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");

        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            await using var context = new AppDbContext(options);
            await SeedDataProvider.SeedIdentityOnlyAsync(context);

            var adminRoleId = await context.Roles.IgnoreQueryFilters()
                .Where(role => role.TenantId == SeedDataProvider.TenantId
                    && role.SystemRoleKey == Role.TenantAdminSystemRoleKey && !role.IsDeleted)
                .Select(role => role.Id)
                .SingleAsync();
            var missingKeys = new HashSet<string>(StringComparer.Ordinal)
            {
                "TenantAdministration.ManageScope", "Settings.ManageTenantConfiguration",
                "Directory.View", "Directory.ManageConnection", "Directory.TestConnection",
                "Directory.PreviewSync", "Directory.ExecuteSync", "Directory.ManageMappings",
                "Directory.ManageProvisioningPolicy",
                "ReferenceData.Create", "ReferenceData.Edit", "ReferenceData.Delete"
            };
            var otherRoleGrantsBefore = await context.Roles.IgnoreQueryFilters()
                .Where(role => role.TenantId == SeedDataProvider.TenantId
                    && role.SystemRoleKey != Role.TenantAdminSystemRoleKey)
                .Include(role => role.Permissions)
                .ToDictionaryAsync(role => role.Id,
                    role => role.Permissions.Select(permission => $"{permission.Resource}.{permission.Action}")
                        .ToHashSet(StringComparer.Ordinal));
            var membershipsBefore = await context.UserRoles.IgnoreQueryFilters()
                .Where(item => item.TenantId == SeedDataProvider.TenantId)
                .Select(item => new { item.Id, item.UserId, item.RoleId })
                .ToArrayAsync();
            var removedRows = await context.Permissions.IgnoreQueryFilters()
                .Where(permission => permission.TenantId == SeedDataProvider.TenantId && missingKeys.Contains(permission.Name))
                .ToListAsync();
            removedRows.Should().HaveCount(12);
            context.Permissions.RemoveRange(removedRows);
            await context.SaveChangesAsync();
            context.ChangeTracker.Clear();

            await SeedDataProvider.SeedIdentityOnlyAsync(context);
            await SeedDataProvider.SeedIdentityOnlyAsync(context);
            context.ChangeTracker.Clear();

            var persistedKeys = await context.Permissions.IgnoreQueryFilters()
                .Where(permission => permission.TenantId == SeedDataProvider.TenantId && !permission.IsDeleted)
                .Select(permission => permission.Name)
                .ToArrayAsync();
            persistedKeys.Should().BeEquivalentTo(PermissionCatalog.Entries
                .Where(item => item.IsActive)
                .Select(item => item.Key));

            var adminRole = await context.Roles.IgnoreQueryFilters().AsNoTracking()
                .Include(role => role.Permissions)
                .SingleAsync(role => role.Id == adminRoleId);
            adminRole.Permissions.Select(permission => $"{permission.Resource}.{permission.Action}")
                .Should().Contain("Document.View", "new seed setup has explicit grants, but existing roles are not force-reconciled with protection lists");

            var otherRoleGrantsAfter = await context.Roles.IgnoreQueryFilters()
                .Where(role => role.TenantId == SeedDataProvider.TenantId
                    && role.SystemRoleKey != Role.TenantAdminSystemRoleKey)
                .Include(role => role.Permissions)
                .ToDictionaryAsync(role => role.Id,
                    role => role.Permissions.Select(permission => $"{permission.Resource}.{permission.Action}")
                        .ToHashSet(StringComparer.Ordinal));
            otherRoleGrantsAfter.Should().BeEquivalentTo(otherRoleGrantsBefore);

            var membershipsAfter = await context.UserRoles.IgnoreQueryFilters()
                .Where(item => item.TenantId == SeedDataProvider.TenantId)
                .Select(item => new { item.Id, item.UserId, item.RoleId })
                .ToArrayAsync();
            membershipsAfter.Should().BeEquivalentTo(membershipsBefore);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", originalEnvironment);
        }
    }

    [Fact]
    public async Task SeedIdentityOnlyAsync_RejectsUnexpectedOrganizationAdminGrantWithoutRemovingExistingGrants()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var context = new AppDbContext(options);
        await SeedDataProvider.SeedIdentityOnlyAsync(context);

        var role = await context.Roles.IgnoreQueryFilters()
            .Include(item => item.Permissions)
            .SingleAsync(item => item.TenantId == SeedDataProvider.TenantId
                && item.RoleKey == Role.OrganizationAdminRoleKey && !item.IsDeleted);
        var projectEdit = await context.Permissions.IgnoreQueryFilters()
            .SingleAsync(item => item.TenantId == SeedDataProvider.TenantId
                && item.Name == "Project.Edit" && !item.IsDeleted);
        role.Permissions.Add(projectEdit);
        await context.SaveChangesAsync();

        var roleId = role.Id;
        var grantsBefore = role.Permissions.Where(item => !item.IsDeleted)
            .Select(item => item.Name).ToArray();
        var membershipsBefore = await context.UserRoles.IgnoreQueryFilters()
            .Where(item => item.TenantId == SeedDataProvider.TenantId && item.RoleId == roleId)
            .Select(item => new { item.Id, item.UserId, item.RoleId })
            .ToArrayAsync();
        context.ChangeTracker.Clear();

        var act = async () => await SeedDataProvider.SeedIdentityOnlyAsync(context);
        var exception = await act.Should().ThrowAsync<InvalidOperationException>();
        exception.Which.Message.Should().Contain("Project.Edit");
        exception.Which.Message.Should().Contain("No existing grants were removed");

        context.ChangeTracker.Clear();
        var persistedRole = await context.Roles.IgnoreQueryFilters().AsNoTracking()
            .Include(item => item.Permissions)
            .SingleAsync(item => item.Id == roleId);
        persistedRole.Permissions.Where(item => !item.IsDeleted)
            .Select(item => item.Name).ToArray().Should().BeEquivalentTo(grantsBefore);
        (await context.UserRoles.IgnoreQueryFilters()
            .Where(item => item.TenantId == SeedDataProvider.TenantId && item.RoleId == roleId)
            .Select(item => new { item.Id, item.UserId, item.RoleId })
            .ToArrayAsync()).Should().BeEquivalentTo(membershipsBefore);
    }

    [Fact]
    public async Task SeedIdentityOnlyAsync_RestoresOnlyApprovedOrganizationAdminGrantsIdempotently()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var context = new AppDbContext(options);
        await SeedDataProvider.SeedIdentityOnlyAsync(context);

        var role = await context.Roles.IgnoreQueryFilters()
            .Include(item => item.Permissions)
            .SingleAsync(item => item.TenantId == SeedDataProvider.TenantId
                && item.RoleKey == Role.OrganizationAdminRoleKey && !item.IsDeleted);
        var roleId = role.Id;
        role.Permissions.Remove(role.Permissions.Single(item => item.Name == "OrganizationConfiguration.Manage"));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        await SeedDataProvider.SeedIdentityOnlyAsync(context);
        await SeedDataProvider.SeedIdentityOnlyAsync(context);
        context.ChangeTracker.Clear();

        var restoredRole = await context.Roles.IgnoreQueryFilters().AsNoTracking()
            .Include(item => item.Permissions)
            .SingleAsync(item => item.Id == roleId);
        restoredRole.Permissions.Where(item => !item.IsDeleted)
            .Select(item => item.Name).ToArray().Should().BeEquivalentTo(new[]
            {
                "OrganizationConfiguration.View",
                "OrganizationConfiguration.Manage"
            });
        (await context.UserRoles.IgnoreQueryFilters()
            .AnyAsync(item => item.TenantId == SeedDataProvider.TenantId && item.RoleId == roleId))
            .Should().BeFalse("seed reconciliation must not appoint an Organization Admin");
    }

    [Fact]
    public async Task SeedIdentityOnlyAsync_Rerun_RestoresAdminBaselineWithoutRewritingMemberships()
    {
        var originalEnvironment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");

        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            await using var context = new AppDbContext(options);
            await SeedDataProvider.SeedIdentityOnlyAsync(context);

            var role = await context.Roles
                .IgnoreQueryFilters()
                .Include(item => item.Permissions)
                .SingleAsync(item => item.TenantId == SeedDataProvider.TenantId
                    && item.SystemRoleKey == Role.TenantAdminSystemRoleKey);
            role.Permissions.Remove(role.Permissions.Single(item => item.Name == "Roles.Delete"));
            var additionalUser = new User
            {
                TenantId = SeedDataProvider.TenantId,
                Email = "additional-admin-member@nexus.test",
                PasswordHash = "not-used-by-this-test",
                FirstName = "Additional",
                LastName = "Member",
                IsActive = true,
                CreatedBy = "SeedDataProviderTests"
            };
            context.Users.Add(additionalUser);
            context.UserRoles.Add(new UserRole
            {
                TenantId = SeedDataProvider.TenantId,
                UserId = additionalUser.Id,
                RoleId = role.Id,
                CreatedBy = "SeedDataProviderTests"
            });
            await context.SaveChangesAsync();

            context.ChangeTracker.Clear();
            await SeedDataProvider.SeedIdentityOnlyAsync(context);
            await SeedDataProvider.SeedIdentityOnlyAsync(context);

            context.ChangeTracker.Clear();
            var reloadedRole = await context.Roles
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Include(item => item.Permissions)
                .SingleAsync(item => item.Id == role.Id);
            reloadedRole.Permissions.Select(item => $"{item.Resource}.{item.Action}")
                .Should().Contain("Roles.Delete");
            (await context.UserRoles.IgnoreQueryFilters().CountAsync(item => item.RoleId == role.Id && !item.IsDeleted))
                .Should().Be(2, "repeated seed execution must not alter role membership");
        }
        finally
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", originalEnvironment);
        }
    }

    [Fact]
    public async Task SeedAsync_Rerun_ShouldProduceExactlyTenDeterministicProjects()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new AppDbContext(options);

        await SeedDataProvider.SeedAsync(context);
        await SeedDataProvider.SeedAsync(context);

        var projects = await context.Projects
            .IgnoreQueryFilters()
            .Where(p => p.TenantId == SeedDataProvider.TenantId)
            .ToListAsync();

        projects.Should().HaveCount(10);
        projects.Select(p => p.Code).Should().OnlyHaveUniqueItems();
        projects.Select(p => p.Id).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task SeedAsync_Rerun_DoesNotDeleteUserCreatedRisks()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new AppDbContext(options);

        await SeedDataProvider.SeedAsync(context);
        var projectId = Guid.Parse("e0000001-0000-0000-0000-000000000001");
        var risk = new Risk
        {
            TenantId = SeedDataProvider.TenantId,
            ProjectId = projectId,
            Title = "User-created risk",
            Description = "Must survive a development restart",
            RiskLevel = RiskLevel.Medium,
            Status = RiskStatus.Identified,
            CreatedBy = "user"
        };
        context.Risks.Add(risk);
        await context.SaveChangesAsync();

        await SeedDataProvider.SeedAsync(context);

        var persisted = await context.Risks.IgnoreQueryFilters().SingleOrDefaultAsync(item => item.Id == risk.Id);
        persisted.Should().NotBeNull();
        persisted!.Title.Should().Be("User-created risk");
    }

    [Fact]
    public async Task SeedAsync_Rerun_ShouldKeepOnlyTheAdministratorAccount()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new AppDbContext(options);

        await SeedDataProvider.SeedAsync(context);
        await SeedDataProvider.SeedAsync(context);

        var users = await context.Users
            .IgnoreQueryFilters()
            .Where(u => u.TenantId == SeedDataProvider.TenantId)
            .OrderBy(u => u.Email)
            .ToListAsync();

        // Demo accounts (the five named PMs, the per-role accounts, the 80 generated
        // accounts and the legacy pmNN@nexus.com shape) are purged, so a fresh tenant
        // starts with the administrator only.
        users.Should().HaveCount(1);
        users[0].Email.Should().Be("admin@nexus.com");
        users.Select(u => u.Email).Should().NotContain(e => e.StartsWith("pm") && e.EndsWith("@nexus.com"));
        users.Select(u => u.Id).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task SeedAsync_Rerun_ShouldNotSeedVendorsOrOutsourcedResources()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new AppDbContext(options);

        await SeedDataProvider.SeedAsync(context);
        await SeedDataProvider.SeedAsync(context);

        var vendors = await context.Vendors
            .IgnoreQueryFilters()
            .Where(v => v.TenantId == SeedDataProvider.TenantId)
            .ToListAsync();
        vendors.Should().BeEmpty("the CloudNova and DataWorks demo vendors are purged");

        var resources = await context.Resources
            .IgnoreQueryFilters()
            .Where(r => r.TenantId == SeedDataProvider.TenantId)
            .ToListAsync();
        resources.Should().BeEmpty("the demo employee and outsourced resources are purged");
        resources.Should().NotContain(r => r.Type == "Outsourced");
    }

    [Fact]
    public async Task SeedAsync_Rerun_ShouldPreserveUserCreatedVendorsAndResources()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new AppDbContext(options);

        await SeedDataProvider.SeedAsync(context);

        var vendor = new Vendor
        {
            TenantId = SeedDataProvider.TenantId,
            Name = "Acme Consulting",
            Status = "Active",
            IsActive = true,
            CreatedBy = "user"
        };
        var resource = new Resource
        {
            TenantId = SeedDataProvider.TenantId,
            Name = "Contract Engineer",
            Type = "Outsourced",
            Status = "Active",
            IsActive = true,
            VendorId = vendor.Id,
            CreatedBy = "user"
        };
        context.Vendors.Add(vendor);
        context.Resources.Add(resource);
        await context.SaveChangesAsync();

        // Re-seeding must not touch rows the purge cannot recognise as demo data.
        await SeedDataProvider.SeedAsync(context);

        var persistedVendor = await context.Vendors.IgnoreQueryFilters().SingleOrDefaultAsync(v => v.Id == vendor.Id);
        persistedVendor.Should().NotBeNull();
        persistedVendor!.Name.Should().Be("Acme Consulting");

        var persistedResource = await context.Resources.IgnoreQueryFilters().SingleOrDefaultAsync(r => r.Id == resource.Id);
        persistedResource.Should().NotBeNull();
        persistedResource!.VendorId.Should().Be(vendor.Id);
    }

    [Fact]
    public async Task SeedAsync_Rerun_ShouldSeedVendorCategoriesAsReferenceData()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new AppDbContext(options);

        await SeedDataProvider.SeedAsync(context);
        await SeedDataProvider.SeedAsync(context);

        var categories = await context.Lookups
            .IgnoreQueryFilters()
            .Where(l => l.TenantId == SeedDataProvider.TenantId && l.Category == NEXUS.SharedKernel.ReferenceDataCategories.VendorCategory && !l.IsDeleted)
            .ToListAsync();

        // Categories are reference data and are deliberately kept, unlike the demo
        // vendors that used to point at them.
        categories.Should().HaveCount(5);
        categories.Select(c => c.Code).Should().OnlyHaveUniqueItems();
        categories.Should().OnlyContain(c => c.IsActive);

        var vendors = await context.Vendors
            .IgnoreQueryFilters()
            .Where(v => v.TenantId == SeedDataProvider.TenantId)
            .ToListAsync();
        vendors.Should().BeEmpty();
    }

    [Fact]
    public async Task SeedAsync_Rerun_ShouldSeedSkillCatalogWithoutContractsOrResourceLinks()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new AppDbContext(options);

        await SeedDataProvider.SeedAsync(context);
        await SeedDataProvider.SeedAsync(context);

        var skills = await context.Skills
            .IgnoreQueryFilters()
            .Where(s => s.TenantId == SeedDataProvider.TenantId)
            .ToListAsync();
        skills.Should().HaveCount(10, "the skill catalog is reference data and is deliberately kept");

        var contracts = await context.Contracts
            .IgnoreQueryFilters()
            .Where(c => c.TenantId == SeedDataProvider.TenantId)
            .ToListAsync();
        contracts.Should().BeEmpty("demo contracts belonged to the purged demo resources");

        var resourceSkills = await context.ResourceSkills
            .IgnoreQueryFilters()
            .Where(rs => rs.TenantId == SeedDataProvider.TenantId)
            .ToListAsync();
        resourceSkills.Should().BeEmpty("resource-skill links belonged to the purged demo resources");
    }

    [Fact]
    public async Task SeedMinimalAsync_Twice_SeedsSystemBaselineAndLeavesBusinessDataEmpty()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new AppDbContext(options);

        await SeedDataProvider.SeedMinimalAsync(context);
        var firstPass = await ReadMinimalSeedSnapshotAsync(context);

        await SeedDataProvider.SeedMinimalAsync(context);
        var secondPass = await ReadMinimalSeedSnapshotAsync(context);

        secondPass.Should().BeEquivalentTo(firstPass,
            "the second Minimal Seed run must not add duplicate system rows or business data");
        firstPass.Tenants.Should().Be(1);
        firstPass.Organizations.Should().Be(1);
        firstPass.Users.Should().Be(1, "the fresh baseline contains only the seeded Admin, including soft-deleted rows");
        new[]
        {
            secondPass.Teams,
            secondPass.Projects,
            secondPass.Portfolios,
            secondPass.Programs,
            secondPass.ProjectHandoverRequests,
            secondPass.ProgramHandoverRequests,
            secondPass.PortfolioHandoverRequests,
            secondPass.Budgets,
            secondPass.ProjectAnnualBudgets,
            secondPass.PortfolioBudgets,
            secondPass.BudgetRevisions,
            secondPass.ProjectAnnualBudgetClosings,
            secondPass.ProjectPaymentRequestFundingTransfers,
            secondPass.ProjectPaymentRequestDetails,
            secondPass.PaymentRequestApproverAssignments,
            secondPass.CostEntries,
            secondPass.CostApprovals,
            secondPass.PurchaseCommitments,
            secondPass.Invoices,
            secondPass.PaymentObligations,
            secondPass.PaymentSettlements,
            secondPass.PaymentNotificationDispatches,
            secondPass.Resources,
            secondPass.Vendors,
            secondPass.Contracts,
            secondPass.ResourceAllocations,
            secondPass.ResourceRequests,
            secondPass.ResourceCostProfiles,
            secondPass.WorkItems,
            secondPass.Risks,
            secondPass.Issues,
            secondPass.Meetings,
            secondPass.Documents
        }.Should().OnlyContain(count => count == 0,
            "Minimal Seed must not create demo or business records");

        var tenant = await context.Tenants.IgnoreQueryFilters()
            .SingleAsync(item => item.Id == SeedDataProvider.TenantId);
        tenant.Status.Should().Be(NEXUS.Modules.Organization.Domain.TenantLifecycleStatus.Active);

        var organization = await context.Organizations.IgnoreQueryFilters()
            .SingleAsync(item => item.TenantId == SeedDataProvider.TenantId);
        organization.Should().NotBeNull("Minimal Seed includes the structural Organization required by the Tenant");

        var users = await context.Users.IgnoreQueryFilters().ToListAsync();
        users.Should().ContainSingle(item => item.Email == "admin@nexus.com" && item.IsActive && !item.IsDeleted);
        users[0].PasswordHash.Should().NotBeNullOrWhiteSpace();
        users[0].MustChangePassword.Should().BeTrue();
        users[0].TemporaryPasswordExpiresAt.Should().BeAfter(DateTime.UtcNow);

        var adminRole = await context.Roles.IgnoreQueryFilters()
            .Include(role => role.Permissions)
            .SingleAsync(role => role.TenantId == SeedDataProvider.TenantId
                && role.SystemRoleKey == Role.TenantAdminSystemRoleKey);
        var systemRoleKeys = await context.Roles.IgnoreQueryFilters()
            .Where(role => role.TenantId == SeedDataProvider.TenantId && role.IsSystem && !role.IsDeleted)
            .Select(role => role.SystemRoleKey)
            .ToListAsync();
        systemRoleKeys.Should().NotBeEmpty();
        systemRoleKeys.Should().OnlyContain(key => !string.IsNullOrWhiteSpace(key));
        systemRoleKeys.Should().OnlyHaveUniqueItems();
        var adminMemberships = await context.UserRoles.IgnoreQueryFilters()
            .Where(item => item.TenantId == SeedDataProvider.TenantId
                && item.UserId == users[0].Id
                && !item.IsDeleted)
            .ToListAsync();
        adminMemberships.Should().ContainSingle(item => item.RoleId == adminRole.Id);
        adminRole.Permissions.Select(item => $"{item.Resource}.{item.Action}")
            .Should().Contain("Document.View", "the initial Admin seed keeps its explicit grant set independent of protection policy");

        var activePermissionKeys = await context.Permissions.IgnoreQueryFilters()
            .Where(item => item.TenantId == SeedDataProvider.TenantId && !item.IsDeleted)
            .Select(item => item.Name)
            .ToListAsync();
        activePermissionKeys.Should().BeEquivalentTo(PermissionCatalog.Entries
            .Where(item => item.IsActive)
            .Select(item => item.Key));

        new SettingDefinitionCatalog().All.Should().NotBeEmpty(
            "system setting defaults are code-defined and need no per-tenant override rows");
        (await context.Lookups.IgnoreQueryFilters().CountAsync(item => item.TenantId == SeedDataProvider.TenantId
            && item.Category == NEXUS.SharedKernel.ReferenceDataCategories.WorkItemStatus && !item.IsDeleted))
            .Should().BeGreaterThan(0);
        (await context.Lookups.IgnoreQueryFilters().CountAsync(item => item.TenantId == SeedDataProvider.TenantId
            && item.Category == NEXUS.SharedKernel.ReferenceDataCategories.VendorCategory && !item.IsDeleted))
            .Should().BeGreaterThan(0);
        (await context.Currencies.IgnoreQueryFilters().CountAsync(item => item.TenantId == SeedDataProvider.TenantId && !item.IsDeleted))
            .Should().BeGreaterThan(0);
        (await context.DocumentCategories.IgnoreQueryFilters().CountAsync(item => item.TenantId == SeedDataProvider.TenantId && !item.IsDeleted))
            .Should().BeGreaterThan(0);
        (await context.Skills.IgnoreQueryFilters().CountAsync(item => item.TenantId == SeedDataProvider.TenantId && !item.IsDeleted))
            .Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task SeedMinimalAsync_RefusesExistingBusinessDataWithoutChangingIt()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new AppDbContext(options);
        var existingProject = new NEXUS.Modules.ProjectManagement.Domain.Project
        {
            TenantId = SeedDataProvider.TenantId,
            Name = "Existing project",
            Code = "EXISTING-1",
            CreatedBy = "SeedDataProviderTests"
        };
        context.Projects.Add(existingProject);
        await context.SaveChangesAsync();

        Func<Task> seed = () => SeedDataProvider.SeedMinimalAsync(context);
        await seed.Should().ThrowAsync<InvalidOperationException>();

        (await context.Projects.IgnoreQueryFilters().CountAsync()).Should().Be(1);
        (await context.Users.IgnoreQueryFilters().CountAsync()).Should().Be(0);
        (await context.Roles.IgnoreQueryFilters().CountAsync()).Should().Be(0);
        (await context.Permissions.IgnoreQueryFilters().CountAsync()).Should().Be(0);
        (await context.Tenants.IgnoreQueryFilters().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task SeedMinimalAsync_RefusesUnexpectedUserWithoutCreatingAdminOrRoles()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new AppDbContext(options);
        context.Users.Add(new User
        {
            TenantId = SeedDataProvider.TenantId,
            Email = "pre-existing@nexus.test",
            PasswordHash = "test-only-hash",
            FirstName = "Existing",
            LastName = "User",
            IsActive = true,
            CreatedBy = "SeedDataProviderTests"
        });
        await context.SaveChangesAsync();

        Func<Task> seed = () => SeedDataProvider.SeedMinimalAsync(context);
        await seed.Should().ThrowAsync<InvalidOperationException>();

        (await context.Users.IgnoreQueryFilters().CountAsync()).Should().Be(1);
        (await context.Users.IgnoreQueryFilters().CountAsync(item => item.Email == "admin@nexus.com")).Should().Be(0);
        (await context.Roles.IgnoreQueryFilters().CountAsync()).Should().Be(0);
        (await context.Permissions.IgnoreQueryFilters().CountAsync()).Should().Be(0);
        (await context.Tenants.IgnoreQueryFilters().CountAsync()).Should().Be(0);
    }

    private static async Task<MinimalSeedSnapshot> ReadMinimalSeedSnapshotAsync(AppDbContext context) => new(
        await context.Tenants.IgnoreQueryFilters().CountAsync(),
        await context.Organizations.IgnoreQueryFilters().CountAsync(),
        await context.Users.IgnoreQueryFilters().CountAsync(),
        await context.Roles.IgnoreQueryFilters().CountAsync(),
        await context.Permissions.IgnoreQueryFilters().CountAsync(),
        await context.UserRoles.IgnoreQueryFilters().CountAsync(),
        await context.Lookups.IgnoreQueryFilters().CountAsync(),
        await context.Currencies.IgnoreQueryFilters().CountAsync(),
        await context.DocumentCategories.IgnoreQueryFilters().CountAsync(),
        await context.Skills.IgnoreQueryFilters().CountAsync(),
        await context.Teams.IgnoreQueryFilters().CountAsync(),
        await context.Projects.IgnoreQueryFilters().CountAsync(),
        await context.Portfolios.IgnoreQueryFilters().CountAsync(),
        await context.Programs.IgnoreQueryFilters().CountAsync(),
        await context.ProjectHandoverRequests.IgnoreQueryFilters().CountAsync(),
        await context.ProgramHandoverRequests.IgnoreQueryFilters().CountAsync(),
        await context.PortfolioHandoverRequests.IgnoreQueryFilters().CountAsync(),
        await context.Budgets.IgnoreQueryFilters().CountAsync(),
        await context.ProjectAnnualBudgets.IgnoreQueryFilters().CountAsync(),
        await context.PortfolioBudgets.IgnoreQueryFilters().CountAsync(),
        await context.BudgetRevisions.IgnoreQueryFilters().CountAsync(),
        await context.ProjectAnnualBudgetClosings.IgnoreQueryFilters().CountAsync(),
        await context.ProjectPaymentRequestFundingTransfers.IgnoreQueryFilters().CountAsync(),
        await context.ProjectPaymentRequestDetails.IgnoreQueryFilters().CountAsync(),
        await context.PaymentRequestApproverAssignments.IgnoreQueryFilters().CountAsync(),
        await context.CostEntries.IgnoreQueryFilters().CountAsync(),
        await context.CostApprovals.IgnoreQueryFilters().CountAsync(),
        await context.PurchaseCommitments.IgnoreQueryFilters().CountAsync(),
        await context.Invoices.IgnoreQueryFilters().CountAsync(),
        await context.PaymentObligations.IgnoreQueryFilters().CountAsync(),
        await context.PaymentSettlements.IgnoreQueryFilters().CountAsync(),
        await context.PaymentNotificationDispatches.IgnoreQueryFilters().CountAsync(),
        await context.Resources.IgnoreQueryFilters().CountAsync(),
        await context.Vendors.IgnoreQueryFilters().CountAsync(),
        await context.Contracts.IgnoreQueryFilters().CountAsync(),
        await context.ResourceAllocations.IgnoreQueryFilters().CountAsync(),
        await context.ResourceRequests.IgnoreQueryFilters().CountAsync(),
        await context.ResourceCostProfiles.IgnoreQueryFilters().CountAsync(),
        await context.WorkItems.IgnoreQueryFilters().CountAsync(),
        await context.Risks.IgnoreQueryFilters().CountAsync(),
        await context.Issues.IgnoreQueryFilters().CountAsync(),
        await context.Meetings.IgnoreQueryFilters().CountAsync(),
        await context.Documents.IgnoreQueryFilters().CountAsync());

    private sealed record MinimalSeedSnapshot(
        int Tenants,
        int Organizations,
        int Users,
        int Roles,
        int Permissions,
        int UserRoles,
        int Lookups,
        int Currencies,
        int DocumentCategories,
        int Skills,
        int Teams,
        int Projects,
        int Portfolios,
        int Programs,
        int ProjectHandoverRequests,
        int ProgramHandoverRequests,
        int PortfolioHandoverRequests,
        int Budgets,
        int ProjectAnnualBudgets,
        int PortfolioBudgets,
        int BudgetRevisions,
        int ProjectAnnualBudgetClosings,
        int ProjectPaymentRequestFundingTransfers,
        int ProjectPaymentRequestDetails,
        int PaymentRequestApproverAssignments,
        int CostEntries,
        int CostApprovals,
        int PurchaseCommitments,
        int Invoices,
        int PaymentObligations,
        int PaymentSettlements,
        int PaymentNotificationDispatches,
        int Resources,
        int Vendors,
        int Contracts,
        int ResourceAllocations,
        int ResourceRequests,
        int ResourceCostProfiles,
        int WorkItems,
        int Risks,
        int Issues,
        int Meetings,
        int Documents);
}
