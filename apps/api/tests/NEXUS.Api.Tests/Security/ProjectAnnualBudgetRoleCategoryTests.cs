using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
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

public sealed class ProjectAnnualBudgetRoleCategoryTests
{
    private static readonly string[] AnnualBudgetWorkflowPermissions =
    [
        "ProjectAnnualBudget.View",
        "ProjectAnnualBudget.Create",
        "ProjectAnnualBudget.EditDraft",
        "ProjectAnnualBudget.ConfigureDraftApproval",
        "ProjectAnnualBudget.SubmitForReview",
        "ProjectAnnualBudget.Approve",
        "ProjectAnnualBudget.Delete",
        "ProjectAnnualBudget.Withdraw",
        "ProjectAnnualBudget.ReviseApproved",
        "ProjectAnnualBudget.ManageApprovalConfiguration",
        "ProjectAnnualBudgetClosing.View",
        "ProjectAnnualBudget.Close",
        "ProjectAnnualBudget.CloseApprove",
        "ProjectAnnualBudget.CarryForward"
    ];

    private static readonly string[] ManagementRoleAnnualBudgetPermissions =
    [
        "ProjectAnnualBudget.View",
        "ProjectAnnualBudget.Create",
        "ProjectAnnualBudget.EditDraft",
        "ProjectAnnualBudget.ConfigureDraftApproval",
        "ProjectAnnualBudget.SubmitForReview",
        "ProjectAnnualBudget.ReviseApproved",
        "ProjectAnnualBudget.Approve",
        "ProjectAnnualBudget.Delete",
        "ProjectAnnualBudget.Withdraw",
        "ProjectAnnualBudget.ManageApprovalConfiguration",
        "ProjectAnnualBudgetClosing.View",
        "ProjectAnnualBudget.Close",
        "ProjectAnnualBudget.CloseApprove",
        "ProjectAnnualBudget.CarryForward",
        "ProjectApproval.Override",
        "ProjectFinancialFoundation.View"
    ];

    [Fact]
    public async Task Management_role_can_select_and_save_annual_budget_workflow_permissions()
    {
        Assert.Equal(
            PermissionCatalog.Entries.Count,
            PermissionCatalog.Entries.Select(permission => permission.SeedId).Distinct(StringComparer.Ordinal).Count());

        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        await using var db = CreateDatabase(tenantId);
        var actorPermissions = new HashSet<string>(
            [
                "Roles.View", "Roles.Create", "Roles.ConfigurePermissions", "TenantAdministration.ManageScope",
                .. ManagementRoleAnnualBudgetPermissions
            ],
            StringComparer.Ordinal);
        var service = CreateService(db, tenantId, actorId, actorPermissions);

        var catalog = await service.GetAssignablePermissionsAsync(actorId, tenantId);

        Assert.True(catalog.IsSuccess);
        foreach (var key in ManagementRoleAnnualBudgetPermissions)
        {
            var permission = Assert.Single(catalog.Value!, item => item.Name == key);
            Assert.True(permission.IsActive);
            Assert.True(permission.IsAssignable);
            Assert.Equal(PermissionScope.Project.ToString(), permission.Scope);
            Assert.Equal(RoleCategories.Selectable, permission.AllowedCategories);
            Assert.True(permission.CanDelegate);
        }

        var created = await service.CreateAsync(new SaveRoleDto
        {
            Name = "Annual Budget Management",
            Category = RoleCategories.Management,
            PermissionIds = ManagementRoleAnnualBudgetPermissions
                .Select(key => Guid.Parse(PermissionCatalog.ByKey[key].SeedId))
                .ToList()
        }, actorId, tenantId);

        Assert.True(created.IsSuccess, created.Error?.Message);
        Assert.Equal(
            ManagementRoleAnnualBudgetPermissions.OrderBy(key => key, StringComparer.Ordinal),
            created.Value!.Permissions.Select(permission => permission.Name).OrderBy(key => key, StringComparer.Ordinal));
        Assert.DoesNotContain(created.Value.Permissions, permission => permission.Name.StartsWith("ProjectPaymentRequest.", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Scoped_role_admin_can_first_grant_annual_budget_workflow_and_effective_deny_is_respected()
    {
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        using var trustedTenant = TrustedTenantExecutionContext.Enter(tenantId);
        await using var db = CreateDatabase(tenantId);
        var actorPermissions = new HashSet<string>(
            ["Roles.View", "Roles.Create", "Roles.ConfigurePermissions", "TenantAdministration.ManageScope"],
            StringComparer.Ordinal);
        var service = CreateService(db, tenantId, actorId, actorPermissions);

        var catalog = await service.GetAssignablePermissionsAsync(actorId, tenantId);

        Assert.True(catalog.IsSuccess, catalog.Error?.Message);
        foreach (var key in AnnualBudgetWorkflowPermissions)
        {
            var permission = Assert.Single(catalog.Value!, item => item.Name == key);
            Assert.True(permission.CanDelegate);
            Assert.Equal(PermissionScope.Project.ToString(), permission.Scope);
            Assert.DoesNotContain(key, actorPermissions);
        }
        Assert.False(catalog.Value!.Single(item => item.Name == "ProjectApproval.Override").CanDelegate);
        Assert.True(catalog.Value.Single(item => item.Name == "ProjectApproval.Override").CanDelegateToCustomRole);
        Assert.False(catalog.Value.Single(item => item.Name == "ProjectPaymentRequest.Create").CanDelegate);
        Assert.True(catalog.Value.Single(item => item.Name == "ProjectPaymentRequest.Create").CanDelegateToCustomRole);

        var created = await service.CreateAsync(new SaveRoleDto
        {
            Name = "Annual Budget Management",
            Category = RoleCategories.Management,
            PermissionIds = AnnualBudgetWorkflowPermissions.Select(PermissionId).ToList()
        }, actorId, tenantId);

        Assert.True(created.IsSuccess, created.Error?.Message);
        Assert.Equal(AnnualBudgetWorkflowPermissions.OrderBy(key => key, StringComparer.Ordinal),
            created.Value!.Permissions.Select(permission => permission.Name).OrderBy(key => key, StringComparer.Ordinal));
        Assert.DoesNotContain(AnnualBudgetWorkflowPermissions, actorPermissions.Contains);

        db.Users.Add(new User
        {
            Id = recipientId,
            TenantId = tenantId,
            Email = "annual-budget-recipient@example.test",
            FirstName = "Annual Budget",
            LastName = "Recipient",
            IsActive = true
        });
        db.UserRoles.Add(new UserRole
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = recipientId,
            RoleId = created.Value.Id
        });
        await db.SaveChangesAsync();

        var effectivePermissionService = new UserEffectivePermissionService(db);
        var effectivePermissions = await effectivePermissionService.GetEffectivePermissionsAsync(recipientId, tenantId);
        Assert.Contains("ProjectAnnualBudget.View", effectivePermissions);
        Assert.Contains("ProjectAnnualBudget.Create", effectivePermissions);
        Assert.Contains("ProjectAnnualBudget.Approve", effectivePermissions);
        Assert.Contains("ProjectAnnualBudget.Close", effectivePermissions);

        var deniedCreate = await db.Permissions.SingleAsync(permission => permission.TenantId == tenantId && permission.Name == "ProjectAnnualBudget.Create");
        db.UserPermissionOverrides.Add(new UserPermissionOverride
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = recipientId,
            PermissionId = deniedCreate.Id,
            Permission = deniedCreate,
            Effect = PermissionOverrideEffect.Deny,
            CreatedBy = "test"
        });
        await db.SaveChangesAsync();

        effectivePermissions = await effectivePermissionService.GetEffectivePermissionsAsync(recipientId, tenantId);
        Assert.DoesNotContain("ProjectAnnualBudget.Create", effectivePermissions);
        Assert.Contains("ProjectAnnualBudget.View", effectivePermissions);
    }

    [Fact]
    public async Task Tenant_admin_role_can_receive_any_unprotected_project_permissions()
    {
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        using var trustedTenant = TrustedTenantExecutionContext.Enter(tenantId);
        await using var db = CreateDatabase(tenantId);
        Assert.Empty(PermissionProtectionPolicy.Empty.ProtectedPermissionKeys);
        var baseline = new[]
        {
            "Roles.ConfigurePermissions", "Users.AssignRoles", "Users.SetStatus"
        }.Select(key => db.Permissions.Local.Single(permission => permission.Name == key)).ToList();

        var adminRole = new Role
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "System Admin",
            Category = RoleCategories.System,
            IsSystem = true,
            Permissions = baseline
        };
        adminRole.AssignSystemRoleKey(Role.TenantAdminSystemRoleKey, tenantId);
        var admin = new User
        {
            Id = actorId,
            TenantId = tenantId,
            Email = "system-admin@example.test",
            FirstName = "System",
            LastName = "Admin",
            IsActive = true
        };
        db.Roles.Add(adminRole);
        db.Users.Add(admin);
        db.UserRoles.Add(new UserRole
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = actorId,
            RoleId = adminRole.Id,
            Role = adminRole,
            User = admin
        });
        await db.SaveChangesAsync();

        var actorPermissions = new HashSet<string>(
            baseline.Select(permission => permission.Name)
                .Concat(["Roles.Create", "Roles.EditDetails", "TenantAdministration.ManageScope"]),
            StringComparer.Ordinal);
        var service = CreateService(db, tenantId, actorId, actorPermissions);
        var permissionIds = baseline.Select(permission => permission.Id)
            .Concat(AnnualBudgetWorkflowPermissions.Select(PermissionId))
            .ToList();
        var saved = await service.UpdateAsync(adminRole.Id, new SaveRoleDto
        {
            Name = adminRole.Name,
            Category = adminRole.Category,
            PermissionIds = permissionIds
        }, actorId, tenantId);

        Assert.True(saved.IsSuccess, saved.Error?.Message);
        Assert.All(AnnualBudgetWorkflowPermissions,
            key => Assert.Contains(saved.Value!.Permissions, permission => permission.Name == key));

        var removedOrdinaryAdminPermission = await service.UpdateAsync(adminRole.Id, new SaveRoleDto
        {
            Name = adminRole.Name,
            Category = adminRole.Category,
            PermissionIds = permissionIds.Where(id => id != PermissionId("Roles.ConfigurePermissions")).ToList()
        }, actorId, tenantId);
        Assert.True(removedOrdinaryAdminPermission.IsSuccess, removedOrdinaryAdminPermission.Error?.Message);
        Assert.DoesNotContain(removedOrdinaryAdminPermission.Value!.Permissions,
            permission => permission.Name == "Roles.ConfigurePermissions");
        Assert.True(ProtectedTenantAdminPolicy.IsProtectedSystemAdminRole(adminRole));

        var administrationGrant = await db.Permissions.SingleAsync(permission => permission.TenantId == tenantId && permission.Name == "Roles.ConfigurePermissions");
        var nonMandatoryBudgetGrant = await db.Permissions.SingleAsync(permission => permission.TenantId == tenantId && permission.Name == "ProjectAnnualBudget.Create");
        db.UserPermissionOverrides.AddRange(
            new UserPermissionOverride
            {
                Id = Guid.NewGuid(), TenantId = tenantId, UserId = actorId, PermissionId = administrationGrant.Id,
                Permission = administrationGrant, Effect = PermissionOverrideEffect.Deny, CreatedBy = "test"
            },
            new UserPermissionOverride
            {
                Id = Guid.NewGuid(), TenantId = tenantId, UserId = actorId, PermissionId = nonMandatoryBudgetGrant.Id,
                Permission = nonMandatoryBudgetGrant, Effect = PermissionOverrideEffect.Deny, CreatedBy = "test"
            });
        await db.SaveChangesAsync();

        var effective = await new UserEffectivePermissionService(db).GetEffectivePermissionsAsync(actorId, tenantId);
        Assert.DoesNotContain("Roles.ConfigurePermissions", effective);
        Assert.Contains("Users.AssignRoles", effective);
        Assert.DoesNotContain("ProjectAnnualBudget.Create", effective);
    }

    [Fact]
    public async Task Annual_budget_approval_can_be_granted_to_an_operation_category_role()
    {
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        await using var db = CreateDatabase(tenantId);
        var actorPermissions = new HashSet<string>(
            ["Roles.View", "Roles.Create", "Roles.ConfigurePermissions", "TenantAdministration.ManageScope", "ProjectAnnualBudget.View", "ProjectAnnualBudget.Approve"],
            StringComparer.Ordinal);
        var service = CreateService(db, tenantId, actorId, actorPermissions);

        var created = await service.CreateAsync(new SaveRoleDto
        {
            Name = "Operations Budget Approvers",
            Category = RoleCategories.Operation,
            PermissionIds =
            [
                Guid.Parse(PermissionCatalog.ByKey["ProjectAnnualBudget.View"].SeedId),
                Guid.Parse(PermissionCatalog.ByKey["ProjectAnnualBudget.Approve"].SeedId)
            ]
        }, actorId, tenantId);

        Assert.True(created.IsSuccess, created.Error?.Message);
        Assert.Contains(created.Value!.Permissions, permission => permission.Name == "ProjectAnnualBudget.Approve");
    }

    [Fact]
    public async Task Active_deny_and_missing_view_dependency_still_block_annual_budget_first_grants()
    {
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        using var trustedTenant = TrustedTenantExecutionContext.Enter(tenantId);
        await using var db = CreateDatabase(tenantId);
        var deniedCreate = await db.Permissions.SingleAsync(permission => permission.TenantId == tenantId && permission.Name == "ProjectAnnualBudget.Create");
        db.UserPermissionOverrides.Add(new UserPermissionOverride
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = actorId,
            PermissionId = deniedCreate.Id,
            Permission = deniedCreate,
            Effect = PermissionOverrideEffect.Deny,
            CreatedBy = "test"
        });
        await db.SaveChangesAsync();

        var actorPermissions = new HashSet<string>(
            ["Roles.View", "Roles.Create", "Roles.ConfigurePermissions", "TenantAdministration.ManageScope"],
            StringComparer.Ordinal);
        var service = CreateService(db, tenantId, actorId, actorPermissions);
        var catalog = await service.GetAssignablePermissionsAsync(actorId, tenantId);

        Assert.True(catalog.IsSuccess, catalog.Error?.Message);
        Assert.True(catalog.Value!.Single(permission => permission.Name == "ProjectAnnualBudget.View").CanDelegate);
        Assert.False(catalog.Value.Single(permission => permission.Name == "ProjectAnnualBudget.Create").CanDelegate);
        Assert.True(catalog.Value.Single(permission => permission.Name == "ProjectAnnualBudget.Approve").CanDelegate);

        var missingView = await service.CreateAsync(new SaveRoleDto
        {
            Name = "Annual Budget Draft Creators Without View",
            Category = RoleCategories.Management,
            PermissionIds = [PermissionId("ProjectAnnualBudget.Create")]
        }, actorId, tenantId);
        Assert.True(missingView.IsFailure);
        Assert.Equal(400, missingView.Error?.StatusCode);

        var deniedGrant = await service.CreateAsync(new SaveRoleDto
        {
            Name = "Annual Budget Creators Denied by Actor Override",
            Category = RoleCategories.Management,
            PermissionIds = [PermissionId("ProjectAnnualBudget.View"), PermissionId("ProjectAnnualBudget.Create")]
        }, actorId, tenantId);
        Assert.True(deniedGrant.IsFailure);
        Assert.Equal(403, deniedGrant.Error?.StatusCode);
        Assert.False(await db.Roles.IgnoreQueryFilters().AnyAsync(role => role.Name == "Annual Budget Creators Denied by Actor Override"));
    }

    [Fact]
    public void Draft_actions_and_approval_configuration_are_category_independent_with_project_view_dependency()
    {
        var managementOnlyKeys = new[]
        {
            "ProjectAnnualBudget.Delete",
            "ProjectAnnualBudget.Withdraw",
            "ProjectAnnualBudget.ManageApprovalConfiguration"
        };

        foreach (var key in managementOnlyKeys)
        {
            var permission = PermissionCatalog.ByKey[key];
            Assert.True(permission.IsActive);
            Assert.True(permission.IsAssignable);
            Assert.Equal(PermissionScope.Project, permission.Scope);
            Assert.Contains("ProjectAnnualBudget.View", permission.RequiredPermissionKeys);
            Assert.False(PermissionProtectionPolicy.Empty.IsProtected(permission.Key));
            Assert.Equal(RoleCategories.Selectable, permission.AllowedCategories);
        }

        var draftApprovalConfiguration = PermissionCatalog.ByKey["ProjectAnnualBudget.ConfigureDraftApproval"];
        Assert.True(draftApprovalConfiguration.IsActive);
        Assert.True(draftApprovalConfiguration.IsAssignable);
        Assert.Equal(PermissionScope.Project, draftApprovalConfiguration.Scope);
        Assert.Contains("ProjectAnnualBudget.View", draftApprovalConfiguration.RequiredPermissionKeys);
        Assert.Contains("ProjectAnnualBudget.EditDraft", draftApprovalConfiguration.RequiredPermissionKeys);
        Assert.False(PermissionProtectionPolicy.Empty.IsProtected(draftApprovalConfiguration.Key));
        Assert.Equal(RoleCategories.Selectable, draftApprovalConfiguration.AllowedCategories);

        foreach (var key in new[] { "ProjectAnnualBudget.EditDraft", "ProjectAnnualBudget.SubmitForReview" })
        {
            var permission = PermissionCatalog.ByKey[key];
            Assert.True(permission.IsActive);
            Assert.True(permission.IsAssignable);
            Assert.Equal(PermissionScope.Project, permission.Scope);
            Assert.Contains("ProjectAnnualBudget.View", permission.RequiredPermissionKeys);
            Assert.Equal(RoleCategories.Selectable, permission.AllowedCategories);
        }
    }

    [Fact]
    public async Task Foreign_tenant_annual_budget_permission_cannot_be_granted_through_bootstrap()
    {
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var foreignTenantId = Guid.NewGuid();
        using var trustedTenant = TrustedTenantExecutionContext.Enter(tenantId);
        await using var db = CreateDatabase(tenantId);
        var foreignPermission = PermissionCatalog.CreateSeedPermissions(foreignTenantId)
            .Single(permission => permission.Name == "ProjectAnnualBudget.Create");
        foreignPermission.Id = Guid.NewGuid();
        using (TrustedTenantExecutionContext.Enter(foreignTenantId))
        {
            db.Permissions.Add(foreignPermission);
            await db.SaveChangesAsync();
        }

        var actorPermissions = new HashSet<string>(
            ["Roles.View", "Roles.Create", "Roles.ConfigurePermissions", "TenantAdministration.ManageScope"],
            StringComparer.Ordinal);
        var service = CreateService(db, tenantId, actorId, actorPermissions);
        var rejected = await service.CreateAsync(new SaveRoleDto
        {
            Name = "Foreign Annual Budget Creator",
            Category = RoleCategories.Management,
            PermissionIds = [PermissionId("ProjectAnnualBudget.View"), foreignPermission.Id]
        }, actorId, tenantId);

        Assert.True(rejected.IsFailure);
        Assert.Equal(400, rejected.Error?.StatusCode);
        Assert.False(await db.Roles.IgnoreQueryFilters().AnyAsync(role => role.Name == "Foreign Annual Budget Creator"));
    }

    private static AppDbContext CreateDatabase(Guid tenantId)
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Permissions.AddRange(PermissionCatalog.CreateSeedPermissions(tenantId));
        using (TrustedTenantExecutionContext.Enter(tenantId))
            db.SaveChanges();
        return db;
    }

    private static Guid PermissionId(string key) => Guid.Parse(PermissionCatalog.ByKey[key].SeedId);

    private static RoleAdministrationService CreateService(
        AppDbContext db,
        Guid tenantId,
        Guid actorId,
        IReadOnlySet<string> effectivePermissions)
    {
        var permissions = new Mock<IUserEffectivePermissionService>();
        permissions.Setup(service => service.GetEffectivePermissionsAsync(actorId, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(effectivePermissions);
        var audit = new Mock<IAuditService>();
        audit.Setup(service => service.AppendAsync(It.IsAny<AuditEntry>(), It.IsAny<CancellationToken>()))
            .Callback<AuditEntry, CancellationToken>((entry, _) => db.Set<AuditEntry>().Add(entry))
            .ReturnsAsync(Result.Success());
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, actorId.ToString()),
            new Claim("tenantId", tenantId.ToString())
        ], "test"));
        var httpContext = new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = principal } };
        return new RoleAdministrationService(db, permissions.Object, audit.Object, httpContext);
    }
}
