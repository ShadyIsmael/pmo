using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.Identity.Application;
using NEXUS.Modules.Identity.Domain;
using NEXUS.Modules.Identity.Infrastructure;
using NEXUS.Persistence;
using NEXUS.SharedKernel;
using Xunit;

namespace NEXUS.Api.Tests.Security;

public sealed class FinancialApprovalDelegationBoundaryTests
{
    private static readonly SemaphoreSlim SeedEnvironmentLock = new(1, 1);
    private static readonly string[] InitialAnnualBudgetKeys =
    [
        "ProjectAnnualBudget.View",
        "ProjectAnnualBudget.Create",
        "ProjectAnnualBudget.EditDraft",
        "ProjectAnnualBudget.SubmitForReview",
        "ProjectAnnualBudget.ManageApprovalConfiguration",
        "ProjectAnnualBudget.Approve",
        "ProjectApproval.Override"
    ];

    [Fact]
    public async Task Scoped_role_admin_can_create_and_assign_custom_annual_budget_role_to_another_user()
    {
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        await using var db = CreateDatabase(tenantId);
        using var tenantScope = TrustedTenantExecutionContext.Enter(tenantId);
        db.Users.Add(NewUser(tenantId, actorId, "Delegation", "Admin"));
        await db.SaveChangesAsync();

        var actorPermissions = RoleAndUserDelegationPermissions();
        var roles = CreateRoleService(db, tenantId, actorId, actorPermissions);
        var assignable = await roles.GetAssignablePermissionsAsync(actorId, tenantId);

        Assert.True(assignable.IsSuccess, assignable.Error?.Message);
        Assert.Contains(assignable.Value!, permission => permission.Name == "ProjectAnnualBudget.View");
        Assert.Contains(assignable.Value!, permission => permission.Name == "ProjectAnnualBudget.ManageApprovalConfiguration");
        Assert.DoesNotContain("ProjectAnnualBudget.View", actorPermissions);
        Assert.DoesNotContain("ProjectAnnualBudget.ManageApprovalConfiguration", actorPermissions);

        var annualBudgetRole = await CreateAnnualBudgetRoleAsync(roles, db, actorId, tenantId, "Annual Budget Configurers");
        var userAdministration = CreateUserService(db, tenantId, actorId, actorPermissions);
        var created = await userAdministration.CreateAsync(new CreateUserDto
        {
            Email = $"budget-configurer-{Guid.NewGuid():N}@test.local",
            FirstName = "Budget",
            LastName = "Configurer",
            IsActive = true,
            RoleIds = [annualBudgetRole.Id]
        }, actorId.ToString());

        Assert.True(created.IsSuccess, created.Error?.Message);
        var targetRoleAssignment = await db.UserRoles.IgnoreQueryFilters().SingleAsync(item =>
            item.UserId == created.Value!.User.Id && item.RoleId == annualBudgetRole.Id);
        Assert.Equal(tenantId, targetRoleAssignment.TenantId);

        using var tenantContext = TrustedTenantExecutionContext.Enter(tenantId);
        var effective = new UserEffectivePermissionService(db);
        var targetPermissions = await effective.GetEffectivePermissionsAsync(created.Value.User.Id, tenantId);
        Assert.Contains("ProjectAnnualBudget.View", targetPermissions);
        Assert.Contains("ProjectAnnualBudget.ManageApprovalConfiguration", targetPermissions);

        var managePermission = await db.Permissions.IgnoreQueryFilters().SingleAsync(permission =>
            permission.TenantId == tenantId && permission.Name == "ProjectAnnualBudget.ManageApprovalConfiguration");
        db.UserPermissionOverrides.Add(new UserPermissionOverride
        {
            TenantId = tenantId,
            UserId = created.Value.User.Id,
            PermissionId = managePermission.Id,
            Effect = PermissionOverrideEffect.Deny,
            CreatedBy = "test"
        });
        await db.SaveChangesAsync();

        targetPermissions = await effective.GetEffectivePermissionsAsync(created.Value.User.Id, tenantId);
        Assert.Contains("ProjectAnnualBudget.View", targetPermissions);
        Assert.DoesNotContain("ProjectAnnualBudget.ManageApprovalConfiguration", targetPermissions);
    }

    [Fact]
    public async Task Role_assignment_bootstrap_requires_both_configuration_and_tenant_scope_authority()
    {
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        await using var db = CreateDatabase(tenantId);
        using var tenantScope = TrustedTenantExecutionContext.Enter(tenantId);
        db.Users.Add(NewUser(tenantId, actorId, "Limited", "Admin"));
        var role = await AddAnnualBudgetRoleAsync(db, tenantId, "Annual Budget Configurers");
        var actorPermissions = RoleAndUserDelegationPermissions();
        actorPermissions.Remove("TenantAdministration.ManageScope");
        await db.SaveChangesAsync();

        var service = CreateUserService(db, tenantId, actorId, actorPermissions);
        var result = await service.CreateAsync(new CreateUserDto
        {
            Email = $"missing-scope-{Guid.NewGuid():N}@test.local",
            FirstName = "Missing",
            LastName = "Scope",
            RoleIds = [role.Id]
        }, actorId.ToString());

        Assert.True(result.IsFailure);
        Assert.Equal(403, result.Error?.StatusCode);
        Assert.Empty(await db.Users.IgnoreQueryFilters()
            .Where(user => user.TenantId == tenantId && user.FirstName == "Missing")
            .ToListAsync());
    }

    [Theory]
    [InlineData("ProjectAnnualBudget.View")]
    [InlineData("ProjectAnnualBudget.ManageApprovalConfiguration")]
    public async Task Role_assignment_bootstrap_respects_actor_active_deny(string deniedKey)
    {
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        await using var db = CreateDatabase(tenantId);
        using var tenantScope = TrustedTenantExecutionContext.Enter(tenantId);
        db.Users.Add(NewUser(tenantId, actorId, "Denied", "Admin"));
        var role = await AddAnnualBudgetRoleAsync(db, tenantId, "Annual Budget Configurers");
        var deniedPermission = await db.Permissions.IgnoreQueryFilters().SingleAsync(permission =>
            permission.TenantId == tenantId && permission.Name == deniedKey);
        db.UserPermissionOverrides.Add(new UserPermissionOverride
        {
            TenantId = tenantId,
            UserId = actorId,
            PermissionId = deniedPermission.Id,
            Effect = PermissionOverrideEffect.Deny,
            CreatedBy = "test"
        });
        await db.SaveChangesAsync();

        var service = CreateUserService(db, tenantId, actorId, RoleAndUserDelegationPermissions());
        var result = await service.CreateAsync(new CreateUserDto
        {
            Email = $"actor-deny-{Guid.NewGuid():N}@test.local",
            FirstName = "Denied",
            LastName = "Delegation",
            RoleIds = [role.Id]
        }, actorId.ToString());

        Assert.True(result.IsFailure);
        Assert.Equal(403, result.Error?.StatusCode);
        Assert.Empty(await db.Users.IgnoreQueryFilters()
            .Where(user => user.TenantId == tenantId && user.FirstName == "Denied" && user.LastName == "Delegation")
            .ToListAsync());
    }

    [Fact]
    public async Task Role_assignment_bootstrap_rejects_a_role_from_another_tenant()
    {
        var tenantId = Guid.NewGuid();
        var foreignTenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        await using var db = CreateDatabase(tenantId);
        using var tenantScope = TrustedTenantExecutionContext.Enter(tenantId);
        db.Users.Add(NewUser(tenantId, actorId, "Scope", "Admin"));

        var foreignView = new Permission
        {
            TenantId = foreignTenantId,
            Name = "ProjectAnnualBudget.View",
            Resource = "ProjectAnnualBudget",
            Action = "View",
            CreatedBy = "test"
        };
        var foreignManage = new Permission
        {
            TenantId = foreignTenantId,
            Name = "ProjectAnnualBudget.ManageApprovalConfiguration",
            Resource = "ProjectAnnualBudget",
            Action = "ManageApprovalConfiguration",
            CreatedBy = "test"
        };
        var foreignRole = new Role
        {
            TenantId = foreignTenantId,
            Name = "Foreign Annual Budget Configurers",
            CreatedBy = "test",
            Permissions = [foreignView, foreignManage]
        };
        db.Roles.Add(foreignRole);
        await db.SaveChangesAsync();

        var service = CreateUserService(db, tenantId, actorId, RoleAndUserDelegationPermissions());
        var result = await service.CreateAsync(new CreateUserDto
        {
            Email = $"foreign-role-{Guid.NewGuid():N}@test.local",
            FirstName = "Foreign",
            LastName = "Role",
            RoleIds = [foreignRole.Id]
        }, actorId.ToString());

        Assert.True(result.IsFailure);
        Assert.Equal(400, result.Error?.StatusCode);
        Assert.Empty(await db.Users.IgnoreQueryFilters()
            .Where(user => user.TenantId == tenantId && user.FirstName == "Foreign")
            .ToListAsync());
    }

    [Fact]
    public async Task Role_assignment_bootstrap_rejects_self_assignment()
    {
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        await using var db = CreateDatabase(tenantId);
        using var tenantScope = TrustedTenantExecutionContext.Enter(tenantId);
        db.Users.Add(NewUser(tenantId, actorId, "Self", "Admin"));
        var role = await AddAnnualBudgetRoleAsync(db, tenantId, "Annual Budget Configurers");
        await db.SaveChangesAsync();

        var service = CreateUserService(db, tenantId, actorId, RoleAndUserDelegationPermissions());
        var result = await service.UpdateAsync(actorId, new UpdateUserDto
        {
            Email = $"self-{actorId:N}@test.local",
            FirstName = "Self",
            LastName = "Admin",
            IsActive = true,
            RoleIds = [role.Id]
        }, actorId.ToString());

        Assert.True(result.IsFailure);
        Assert.Equal(403, result.Error?.StatusCode);
        Assert.Empty(await db.UserRoles.IgnoreQueryFilters()
            .Where(userRole => userRole.TenantId == tenantId && userRole.UserId == actorId)
            .ToListAsync());
    }

    [Fact]
    public async Task Financial_permissions_can_be_granted_to_a_system_role_by_an_authorized_actor()
    {
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        await using var db = CreateDatabase(tenantId);
        using var tenantScope = TrustedTenantExecutionContext.Enter(tenantId);
        var adminRole = new Role
        {
            TenantId = tenantId,
            Name = "admin",
            IsSystem = true,
            CreatedBy = "test"
        };
        adminRole.AssignSystemRoleKey(Role.TenantAdminSystemRoleKey, tenantId);
        foreach (var key in PermissionProtectionPolicy.Empty.ProtectedPermissionKeys)
            adminRole.Permissions.Add(await db.Permissions.IgnoreQueryFilters().SingleAsync(permission =>
                permission.TenantId == tenantId && permission.Name == key));
        var actor = NewUser(tenantId, actorId, "System", "Admin");
        actor.UserRoles.Add(new UserRole { TenantId = tenantId, User = actor, Role = adminRole, CreatedBy = "test" });
        db.Users.Add(actor);
        db.Roles.Add(adminRole);
        await db.SaveChangesAsync();

        var actorPermissions = new HashSet<string>(
            PermissionProtectionPolicy.Empty.ProtectedPermissionKeys
                .Concat(["Roles.Create", "Roles.ConfigurePermissions", "TenantAdministration.ManageScope"]),
            StringComparer.Ordinal);
        var service = CreateRoleService(db, tenantId, actorId, actorPermissions);
        var selectedIds = adminRole.Permissions.Select(permission => permission.Id)
            .Append(Guid.Parse(PermissionCatalog.ByKey["ProjectAnnualBudget.View"].SeedId))
            .Append(Guid.Parse(PermissionCatalog.ByKey["ProjectAnnualBudget.ManageApprovalConfiguration"].SeedId))
            .ToList();

        var result = await service.UpdateAsync(adminRole.Id, new SaveRoleDto
        {
            Name = adminRole.Name,
            PermissionIds = selectedIds
        }, actorId, tenantId);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Contains(result.Value!.Permissions, permission => permission.Name == "ProjectAnnualBudget.View");
        Assert.Contains(result.Value.Permissions, permission => permission.Name == "ProjectAnnualBudget.ManageApprovalConfiguration");
    }

    [Fact]
    public async Task Fresh_identity_seed_omits_financial_auto_grants_and_reseed_preserves_historical_role_grants()
    {
        await SeedEnvironmentLock.WaitAsync();
        var previousEnvironment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        var previousBootstrapPassword = Environment.GetEnvironmentVariable("NEXUS_ADMIN_BOOTSTRAP_PASSWORD");
        try
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
            Environment.SetEnvironmentVariable("NEXUS_ADMIN_BOOTSTRAP_PASSWORD", "test-only-" + Guid.NewGuid().ToString("N"));
            await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
            using var trustedTenant = TrustedTenantExecutionContext.Enter(SeedDataProvider.TenantId);

            await SeedDataProvider.SeedIdentityOnlyAsync(db);
            var admin = await db.Roles.IgnoreQueryFilters().Include(role => role.Permissions)
                .SingleAsync(role => role.SystemRoleKey == Role.TenantAdminSystemRoleKey);
            var projectManager = await db.Roles.IgnoreQueryFilters().Include(role => role.Permissions)
                .SingleAsync(role => role.RoleKey == "tenant.project-manager");

            var categoryManagementKeys = new[]
            {
                "DocumentCategory.View", "DocumentCategory.Create", "DocumentCategory.Edit",
                "DocumentCategory.ActivateDeactivate"
            };
            Assert.All(categoryManagementKeys, key => Assert.Contains(admin.Permissions, permission => permission.Name == key));
            Assert.DoesNotContain(projectManager.Permissions, permission => categoryManagementKeys.Contains(permission.Name, StringComparer.Ordinal));
            var seededAdminUser = await db.Users.IgnoreQueryFilters().Include(user => user.UserRoles)
                .ThenInclude(assignment => assignment.Role)
                .SingleAsync(user => user.Id == SeededDevelopmentAdmin.UserId);
            Assert.Contains(seededAdminUser.UserRoles, assignment =>
                assignment.Role?.SystemRoleKey == Role.TenantAdminSystemRoleKey && assignment.Role.IsSystem);
            var effectiveAdminPermissions = await new UserEffectivePermissionService(db)
                .GetEffectivePermissionsAsync(seededAdminUser.Id, SeedDataProvider.TenantId);
            Assert.All(categoryManagementKeys, key => Assert.Contains(key, effectiveAdminPermissions));

            Assert.DoesNotContain(admin.Permissions, permission => InitialAnnualBudgetKeys.Contains(permission.Name, StringComparer.Ordinal));
            Assert.DoesNotContain(projectManager.Permissions, permission => InitialAnnualBudgetKeys.Contains(permission.Name, StringComparer.Ordinal));

            var historicalAdminKeys = new[]
            {
                "ProjectAnnualBudget.View", "ProjectAnnualBudget.Create", "ProjectAnnualBudget.EditDraft",
                "ProjectAnnualBudget.SubmitForReview", "ProjectAnnualBudget.ManageApprovalConfiguration"
            };
            foreach (var key in historicalAdminKeys)
            {
                admin.Permissions.Add(await db.Permissions.IgnoreQueryFilters().SingleAsync(permission =>
                    permission.TenantId == SeedDataProvider.TenantId && permission.Name == key));
            }
            projectManager.Permissions.Add(await db.Permissions.IgnoreQueryFilters().SingleAsync(permission =>
                permission.TenantId == SeedDataProvider.TenantId && permission.Name == "ProjectAnnualBudget.View"));
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();

            var categoryViewPermission = await db.Permissions.IgnoreQueryFilters().SingleAsync(permission =>
                permission.TenantId == SeedDataProvider.TenantId && permission.Name == "DocumentCategory.View");
            db.UserPermissionOverrides.Add(new UserPermissionOverride
            {
                TenantId = SeedDataProvider.TenantId,
                UserId = SeededDevelopmentAdmin.UserId,
                PermissionId = categoryViewPermission.Id,
                Effect = PermissionOverrideEffect.Deny,
                CreatedBy = "regression-test"
            });
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();

            await SeedDataProvider.SeedIdentityOnlyAsync(db);
            db.ChangeTracker.Clear();
            admin = await db.Roles.IgnoreQueryFilters().Include(role => role.Permissions)
                .SingleAsync(role => role.SystemRoleKey == Role.TenantAdminSystemRoleKey);
            projectManager = await db.Roles.IgnoreQueryFilters().Include(role => role.Permissions)
                .SingleAsync(role => role.RoleKey == "tenant.project-manager");
            Assert.False(projectManager.IsSystem);
            Assert.Null(projectManager.SystemRoleKey);

            Assert.All(historicalAdminKeys, key => Assert.Contains(admin.Permissions, permission => permission.Name == key));
            Assert.All(categoryManagementKeys, key => Assert.Contains(admin.Permissions, permission => permission.Name == key));
            Assert.DoesNotContain(await db.UserPermissionOverrides.IgnoreQueryFilters()
                .Where(item => item.UserId == SeededDevelopmentAdmin.UserId && item.PermissionId == categoryViewPermission.Id)
                .ToListAsync(), item => item.Effect == PermissionOverrideEffect.Deny);
            Assert.Contains(projectManager.Permissions, permission => permission.Name == "ProjectAnnualBudget.View");
            Assert.DoesNotContain(projectManager.Permissions, permission =>
                permission.Name is "ProjectAnnualBudget.Create" or "ProjectAnnualBudget.EditDraft" or "ProjectAnnualBudget.SubmitForReview"
                    or "ProjectAnnualBudget.ManageApprovalConfiguration" or "ProjectAnnualBudget.Approve" or "ProjectApproval.Override");
        }
        finally
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", previousEnvironment);
            Environment.SetEnvironmentVariable("NEXUS_ADMIN_BOOTSTRAP_PASSWORD", previousBootstrapPassword);
            SeedEnvironmentLock.Release();
        }
    }

    private static AppDbContext CreateDatabase(Guid tenantId)
    {
        var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        using var tenantScope = TrustedTenantExecutionContext.Enter(tenantId);
        context.Permissions.AddRange(PermissionCatalog.CreateSeedPermissions(tenantId));
        context.SaveChanges();
        return context;
    }

    private static HashSet<string> RoleAndUserDelegationPermissions() =>
        ["Roles.View", "Roles.Create", "Roles.ConfigurePermissions", "TenantAdministration.ManageScope", "Users.ManageAccounts", "Users.AssignRoles"];

    private static User NewUser(Guid tenantId, Guid userId, string firstName, string lastName) => new()
    {
        Id = userId,
        TenantId = tenantId,
        Email = $"{firstName.ToLowerInvariant()}-{userId:N}@test.local",
        FirstName = firstName,
        LastName = lastName,
        IsActive = true,
        CreatedBy = "test"
    };

    private static async Task<Role> CreateAnnualBudgetRoleAsync(
        RoleAdministrationService service,
        AppDbContext db,
        Guid actorId,
        Guid tenantId,
        string name)
    {
        var permissionIds = new[]
        {
            Guid.Parse(PermissionCatalog.ByKey["ProjectAnnualBudget.View"].SeedId),
            Guid.Parse(PermissionCatalog.ByKey["ProjectAnnualBudget.ManageApprovalConfiguration"].SeedId)
        }.ToList();
        var result = await service.CreateAsync(new SaveRoleDto
        {
            Name = name,
            Category = RoleCategories.Management,
            PermissionIds = permissionIds
        }, actorId, tenantId);
        Assert.True(result.IsSuccess, result.Error?.Message);
        return await db.Roles.IgnoreQueryFilters().Include(role => role.Permissions)
            .SingleAsync(role => role.Id == result.Value!.Id && role.TenantId == tenantId);
    }

    private static async Task<Role> AddAnnualBudgetRoleAsync(AppDbContext db, Guid tenantId, string name)
    {
        var view = await db.Permissions.IgnoreQueryFilters().SingleAsync(permission =>
            permission.TenantId == tenantId && permission.Name == "ProjectAnnualBudget.View");
        var manage = await db.Permissions.IgnoreQueryFilters().SingleAsync(permission =>
            permission.TenantId == tenantId && permission.Name == "ProjectAnnualBudget.ManageApprovalConfiguration");
        var role = new Role
        {
            TenantId = tenantId,
            Name = name,
            CreatedBy = "test",
            Permissions = [view, manage]
        };
        role.AssignOrganization(Guid.NewGuid());
        db.Roles.Add(role);
        await db.SaveChangesAsync();
        return role;
    }

    private static RoleAdministrationService CreateRoleService(
        AppDbContext db,
        Guid tenantId,
        Guid actorId,
        IReadOnlySet<string> effectivePermissions)
    {
        var permissionService = new Mock<IUserEffectivePermissionService>();
        permissionService.Setup(service => service.GetEffectivePermissionsAsync(actorId, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(effectivePermissions);
        var httpContext = CreateHttpContext(actorId, tenantId);
        var audit = new Mock<IAuditService>();
        audit.Setup(service => service.AppendAsync(It.IsAny<AuditEntry>(), It.IsAny<CancellationToken>()))
            .Callback<AuditEntry, CancellationToken>((entry, _) => db.AuditEntries.Add(entry))
            .ReturnsAsync(Result.Success());
        return new RoleAdministrationService(db, permissionService.Object, audit.Object, httpContext);
    }

    private static UserAdministrationService CreateUserService(
        AppDbContext db,
        Guid tenantId,
        Guid actorId,
        IReadOnlySet<string> effectivePermissions)
    {
        var permissionService = new Mock<IUserEffectivePermissionService>();
        permissionService.Setup(service => service.GetEffectivePermissionsAsync(actorId, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(effectivePermissions);
        var audit = new Mock<IAuditService>();
        audit.Setup(service => service.AppendAsync(It.IsAny<AuditEntry>(), It.IsAny<CancellationToken>()))
            .Callback<AuditEntry, CancellationToken>((entry, _) => db.AuditEntries.Add(entry))
            .ReturnsAsync(Result.Success());
        var httpContext = CreateHttpContext(actorId, tenantId);
        var organizationDirectory = new Mock<IUserOrganizationDirectory>();
        organizationDirectory
            .Setup(directory => directory.GetActiveNamesAsync(tenantId, It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid _, IEnumerable<Guid> organizationIds, CancellationToken _) =>
                (IReadOnlyDictionary<Guid, string>)organizationIds.Distinct().ToDictionary(id => id, id => id.ToString("D")));
        permissionService.Setup(service => service.GetEffectivePermissionsForOrganizationAsync(
                actorId, tenantId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(effectivePermissions);
        var assignmentAuthorization = new RoleAssignmentAuthorizationService(
            db, permissionService.Object, httpContext, organizationDirectory.Object);
        return new UserAdministrationService(
            db, new PasswordHasher<User>(), httpContext, permissionService.Object, audit.Object,
            assignmentAuthorization, Options.Create(new LocalPasswordPolicyOptions()), organizationDirectory.Object,
            Mock.Of<IUserOrganizationMembershipIntegrity>());
    }

    private static HttpContextAccessor CreateHttpContext(Guid actorId, Guid tenantId) => new()
    {
        HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, actorId.ToString()),
                new Claim("tenantId", tenantId.ToString())
            ], "test"))
        }
    };
}
