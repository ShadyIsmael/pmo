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

public sealed class FinancialPermissionDelegationTests
{
    [Fact]
    public async Task Catalog_assignable_metadata_does_not_expose_a_permission_unheld_by_the_actor()
    {
        const string candidateKey = "CostUtilization.Export";
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var catalogEntry = PermissionCatalog.ByKey[candidateKey];
        Assert.True(catalogEntry.IsActive);
        Assert.True(catalogEntry.IsAssignable);
        Assert.False(catalogEntry.IsTenantAdminBaseline);

        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Permissions.AddRange(PermissionCatalog.CreateSeedPermissions(tenantId));
        await db.SaveChangesAsync();

        var actorPermissions = new HashSet<string>(
            ["Roles.View", "Roles.ConfigurePermissions", "Resource.View"],
            StringComparer.Ordinal);
        var service = CreateService(db, tenantId, actorId, actorPermissions);

        var withoutInitialGrant = await service.GetAssignablePermissionsAsync(actorId, tenantId);
        Assert.True(withoutInitialGrant.IsSuccess);
        Assert.DoesNotContain(withoutInitialGrant.Value!, permission => permission.Name == candidateKey);

        actorPermissions.Add(candidateKey);
        service = CreateService(db, tenantId, actorId, actorPermissions);
        var withInitialGrant = await service.GetAssignablePermissionsAsync(actorId, tenantId);
        Assert.True(withInitialGrant.IsSuccess);
        Assert.Contains(withInitialGrant.Value!, permission => permission.Name == candidateKey && permission.IsAssignable);
    }

    [Fact]
    public async Task Annual_budget_configuration_can_be_delegated_by_scoped_role_admin_without_granting_it_to_the_actor()
    {
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        await using var db = CreateDatabase(tenantId);

        var actorPermissions = new HashSet<string>(
            ["Roles.View", "Roles.Create", "Roles.ConfigurePermissions", "TenantAdministration.ManageScope"],
            StringComparer.Ordinal);
        var service = CreateService(db, tenantId, actorId, actorPermissions);
        var catalog = await service.GetAssignablePermissionsAsync(actorId, tenantId);

        Assert.True(catalog.IsSuccess);
        Assert.Contains(catalog.Value!, permission => permission.Name == "ProjectAnnualBudget.View");
        Assert.Contains(catalog.Value!, permission => permission.Name == "ProjectAnnualBudget.ManageApprovalConfiguration");
        Assert.DoesNotContain("ProjectAnnualBudget.View", actorPermissions);
        Assert.DoesNotContain("ProjectAnnualBudget.ManageApprovalConfiguration", actorPermissions);

        var viewId = PermissionCatalog.ByKey["ProjectAnnualBudget.View"].SeedId;
        var manageId = PermissionCatalog.ByKey["ProjectAnnualBudget.ManageApprovalConfiguration"].SeedId;
        var created = await service.CreateAsync(new SaveRoleDto
        {
            Name = "Annual Budget Configurers",
            Category = RoleCategories.Management,
            PermissionIds = [Guid.Parse(viewId), Guid.Parse(manageId)]
        }, actorId, tenantId);

        Assert.True(created.IsSuccess, created.Error?.Message);
        Assert.Contains(created.Value!.Permissions, permission => permission.Name == "ProjectAnnualBudget.View");
        Assert.Contains(created.Value.Permissions, permission => permission.Name == "ProjectAnnualBudget.ManageApprovalConfiguration");
        Assert.DoesNotContain("ProjectAnnualBudget.View", actorPermissions);
        Assert.DoesNotContain("ProjectAnnualBudget.ManageApprovalConfiguration", actorPermissions);
    }

    [Fact]
    public async Task Annual_budget_configuration_delegation_requires_both_role_and_tenant_scope_authority()
    {
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        await using var db = CreateDatabase(tenantId);
        var actorPermissions = new HashSet<string>(
            ["Roles.View", "Roles.Create", "Roles.ConfigurePermissions"],
            StringComparer.Ordinal);
        var service = CreateService(db, tenantId, actorId, actorPermissions);

        var catalog = await service.GetAssignablePermissionsAsync(actorId, tenantId);
        Assert.True(catalog.IsSuccess);
        Assert.DoesNotContain(catalog.Value!, permission => permission.Name == "ProjectAnnualBudget.ManageApprovalConfiguration");

        var viewId = Guid.Parse(PermissionCatalog.ByKey["ProjectAnnualBudget.View"].SeedId);
        var manageId = Guid.Parse(PermissionCatalog.ByKey["ProjectAnnualBudget.ManageApprovalConfiguration"].SeedId);
        var created = await service.CreateAsync(new SaveRoleDto
        {
            Name = "Unauthorized Annual Budget Configurers",
            Category = RoleCategories.Management,
            PermissionIds = [viewId, manageId]
        }, actorId, tenantId);

        Assert.True(created.IsFailure);
        Assert.Equal(403, created.Error?.StatusCode);
    }

    [Theory]
    [InlineData("ProjectAnnualBudget.View")]
    [InlineData("ProjectAnnualBudget.ManageApprovalConfiguration")]
    public async Task Direct_active_deny_blocks_bootstrap_delegation_of_the_denied_key(string deniedKey)
    {
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        await using var db = CreateDatabase(tenantId);
        var deniedPermission = await db.Permissions.IgnoreQueryFilters().SingleAsync(permission =>
            permission.TenantId == tenantId && permission.Name == deniedKey);
        db.UserPermissionOverrides.Add(new UserPermissionOverride
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = actorId,
            PermissionId = deniedPermission.Id,
            Permission = deniedPermission,
            Effect = PermissionOverrideEffect.Deny
        });
        await db.SaveChangesAsync();

        var actorPermissions = new HashSet<string>(
            ["Roles.View", "Roles.Create", "Roles.ConfigurePermissions", "TenantAdministration.ManageScope"],
            StringComparer.Ordinal);
        var service = CreateService(db, tenantId, actorId, actorPermissions);
        var catalog = await service.GetAssignablePermissionsAsync(actorId, tenantId);

        Assert.True(catalog.IsSuccess);
        Assert.DoesNotContain(catalog.Value!, permission => permission.Name == deniedKey);
        if (deniedKey == "ProjectAnnualBudget.View")
            Assert.DoesNotContain(catalog.Value!, permission => permission.Name == "ProjectAnnualBudget.ManageApprovalConfiguration");
    }

    private static AppDbContext CreateDatabase(Guid tenantId)
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Permissions.AddRange(PermissionCatalog.CreateSeedPermissions(tenantId));
        db.SaveChanges();
        return db;
    }

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
