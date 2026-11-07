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
            Roles = [annualBudgetRole.Name]
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
            Roles = [role.Name]
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
            Roles = [role.Name]
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
            Roles = [foreignRole.Name]
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
            Roles = [role.Name]
        }, actorId.ToString());

        Assert.True(result.IsFailure);
        Assert.Equal(403, result.Error?.StatusCode);
        Assert.Empty(await db.UserRoles.IgnoreQueryFilters()
            .Where(userRole => userRole.TenantId == tenantId && userRole.UserId == actorId)
            .ToListAsync());
    }

    [Fact]
    public async Task Bootstrap_only_financial_permissions_cannot_be_added_to_a_system_role()
    {
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        await using var db = CreateDatabase(tenantId);
        var adminRole = new Role
        {
            TenantId = tenantId,
            Name = "admin",
            IsSystem = true,
            CreatedBy = "test"
        };
        adminRole.AssignSystemRoleKey(Role.TenantAdminSystemRoleKey, tenantId);
        foreach (var key in ProtectedTenantAdminPolicy.MandatoryPermissionKeys)
            adminRole.Permissions.Add(await db.Permissions.IgnoreQueryFilters().SingleAsync(permission =>
                permission.TenantId == tenantId && permission.Name == key));
        var actor = NewUser(tenantId, actorId, "System", "Admin");
        actor.UserRoles.Add(new UserRole { TenantId = tenantId, User = actor, Role = adminRole, CreatedBy = "test" });
        db.Users.Add(actor);
        db.Roles.Add(adminRole);
        await db.SaveChangesAsync();

        var actorPermissions = new HashSet<string>(
            ProtectedTenantAdminPolicy.MandatoryPermissionKeys
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

        Assert.True(result.IsFailure);
        Assert.Equal(403, result.Error?.StatusCode);
        Assert.DoesNotContain(adminRole.Permissions, permission => permission.Name == "ProjectAnnualBudget.View");
        Assert.DoesNotContain(adminRole.Permissions, permission => permission.Name == "ProjectAnnualBudget.ManageApprovalConfiguration");
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
                .SingleAsync(role => role.SystemRoleKey == "tenant.project-manager");

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

            await SeedDataProvider.SeedIdentityOnlyAsync(db);
            db.ChangeTracker.Clear();
            admin = await db.Roles.IgnoreQueryFilters().Include(role => role.Permissions)
                .SingleAsync(role => role.SystemRoleKey == Role.TenantAdminSystemRoleKey);
            projectManager = await db.Roles.IgnoreQueryFilters().Include(role => role.Permissions)
                .SingleAsync(role => role.SystemRoleKey == "tenant.project-manager");

            Assert.All(historicalAdminKeys, key => Assert.Contains(admin.Permissions, permission => permission.Name == key));
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
        return new UserAdministrationService(
            db,
            new PasswordHasher<User>(),
            CreateHttpContext(actorId, tenantId),
            permissionService.Object,
            audit.Object,
            Options.Create(new LocalPasswordPolicyOptions()));
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
