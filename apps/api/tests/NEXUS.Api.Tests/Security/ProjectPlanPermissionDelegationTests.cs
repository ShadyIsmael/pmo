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

public sealed class ProjectPlanPermissionDelegationTests
{
    private static readonly string[] ProjectPlanningKeys =
    [
        "Planning.View",
        "Planning.Edit",
        "ProjectPlan.Create"
    ];

    [Fact]
    public async Task Scoped_role_admin_can_persist_project_planning_grants_without_receiving_them()
    {
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        await using var db = CreateDatabase(tenantId);
        var actorPermissions = CreateRoleAdminPermissions();
        var service = CreateRoleService(db, tenantId, actorId, actorPermissions);

        var catalog = await service.GetAssignablePermissionsAsync(actorId, tenantId);
        Assert.True(catalog.IsSuccess, catalog.Error?.Message);
        foreach (var key in ProjectPlanningKeys)
        {
            Assert.True(catalog.Value!.Single(item => item.Name == key).CanDelegate);
            Assert.DoesNotContain(key, actorPermissions);
        }

        var created = await service.CreateAsync(new SaveRoleDto
        {
            Name = "Project Planning Editors",
            Category = RoleCategories.Management,
            PermissionIds = ProjectPlanningKeys
                .Select(key => Guid.Parse(PermissionCatalog.ByKey[key].SeedId))
                .ToList()
        }, actorId, tenantId);

        Assert.True(created.IsSuccess, created.Error?.Message);
        Assert.Equal(ProjectPlanningKeys.OrderBy(key => key, StringComparer.Ordinal),
            created.Value!.Permissions.Select(permission => permission.Name).OrderBy(key => key, StringComparer.Ordinal));

        var persisted = await db.Roles.IgnoreQueryFilters()
            .Include(role => role.Permissions)
            .SingleAsync(role => role.Id == created.Value.Id && role.TenantId == tenantId);
        Assert.Equal(ProjectPlanningKeys.OrderBy(key => key, StringComparer.Ordinal),
            persisted.Permissions.Select(permission => permission.Name).OrderBy(key => key, StringComparer.Ordinal));
        Assert.DoesNotContain(ProjectPlanningKeys, actorPermissions.Contains);
    }

    [Theory]
    [InlineData("Roles.ConfigurePermissions")]
    [InlineData("TenantAdministration.ManageScope")]
    public async Task First_grant_requires_both_role_configuration_and_tenant_scope_authority(string missingPermission)
    {
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        await using var db = CreateDatabase(tenantId);
        var actorPermissions = CreateRoleAdminPermissions();
        actorPermissions.Remove(missingPermission);
        var service = CreateRoleService(db, tenantId, actorId, actorPermissions);

        var catalog = await service.GetAssignablePermissionsAsync(actorId, tenantId);
        if (missingPermission == "Roles.ConfigurePermissions")
        {
            Assert.True(catalog.IsFailure);
        }
        else
        {
            Assert.True(catalog.IsSuccess, catalog.Error?.Message);
            Assert.All(ProjectPlanningKeys, key =>
                Assert.False(catalog.Value!.Single(item => item.Name == key).CanDelegate));
        }

        var rejected = await service.CreateAsync(new SaveRoleDto
        {
            Name = "Unauthorized Project Planning Editors",
            Category = RoleCategories.Management,
            PermissionIds = ProjectPlanningKeys
                .Select(key => Guid.Parse(PermissionCatalog.ByKey[key].SeedId))
                .ToList()
        }, actorId, tenantId);

        Assert.True(rejected.IsFailure);
        Assert.Equal(403, rejected.Error?.StatusCode);
        Assert.False(await db.Roles.IgnoreQueryFilters().AnyAsync(role => role.Name == "Unauthorized Project Planning Editors"));
    }

    [Theory]
    [InlineData("ProjectPlan.Create")]
    [InlineData("Planning.Edit")]
    [InlineData("Planning.View")]
    public async Task Active_deny_blocks_only_the_denied_bootstrap_key_and_its_dependencies(string deniedKey)
    {
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        await using var db = CreateDatabase(tenantId);
        var deniedPermission = await db.Permissions.IgnoreQueryFilters()
            .SingleAsync(permission => permission.TenantId == tenantId && permission.Name == deniedKey);
        db.UserPermissionOverrides.Add(new UserPermissionOverride
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = actorId,
            PermissionId = deniedPermission.Id,
            Permission = deniedPermission,
            Effect = PermissionOverrideEffect.Deny,
            CreatedBy = "test"
        });
        await db.SaveChangesAsync();

        var actorPermissions = CreateRoleAdminPermissions();
        var service = CreateRoleService(db, tenantId, actorId, actorPermissions);
        var catalog = await service.GetAssignablePermissionsAsync(actorId, tenantId);

        Assert.True(catalog.IsSuccess, catalog.Error?.Message);
        Assert.False(catalog.Value!.Single(item => item.Name == deniedKey).CanDelegate);
        var requestedKeys = deniedKey == "Planning.Edit"
            ? new[] { "Planning.View", "ProjectPlan.Create" }
            : ProjectPlanningKeys;
        if (deniedKey == "Planning.View")
        {
            Assert.False(catalog.Value.Single(item => item.Name == "Planning.Edit").CanDelegate);
            Assert.False(catalog.Value.Single(item => item.Name == "ProjectPlan.Create").CanDelegate);
        }

        var saved = await service.CreateAsync(new SaveRoleDto
        {
            Name = $"Project Planning role without {deniedKey.Replace('.', '-')}",
            Category = RoleCategories.Management,
            PermissionIds = requestedKeys
                .Select(key => Guid.Parse(PermissionCatalog.ByKey[key].SeedId))
                .ToList()
        }, actorId, tenantId);

        if (deniedKey == "Planning.Edit")
        {
            Assert.True(saved.IsSuccess, saved.Error?.Message);
            Assert.DoesNotContain(saved.Value!.Permissions, permission => permission.Name == "Planning.Edit");
        }
        else
        {
            Assert.True(saved.IsFailure);
            Assert.Equal(403, saved.Error?.StatusCode);
        }
    }

    [Fact]
    public async Task Project_plan_creation_is_management_only_while_plan_content_edit_allows_operation_roles()
    {
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        await using var db = CreateDatabase(tenantId);
        var service = CreateRoleService(db, tenantId, actorId, CreateRoleAdminPermissions());

        var operationEdit = await service.CreateAsync(new SaveRoleDto
        {
            Name = "Operations Plan Editor",
            Category = RoleCategories.Operation,
            PermissionIds = [PermissionId("Planning.View"), PermissionId("Planning.Edit")]
        }, actorId, tenantId);

        Assert.True(operationEdit.IsSuccess, operationEdit.Error?.Message);
        Assert.Contains(operationEdit.Value!.Permissions, permission => permission.Name == "Planning.Edit");

        var operationCreate = await service.CreateAsync(new SaveRoleDto
        {
            Name = "Operations Plan Creator",
            Category = RoleCategories.Operation,
            PermissionIds = [PermissionId("Planning.View"), PermissionId("ProjectPlan.Create")]
        }, actorId, tenantId);

        Assert.True(operationCreate.IsFailure);
        Assert.Equal("ROLE_PERMISSION_CATEGORY_NOT_ALLOWED", operationCreate.Error?.Code);
        Assert.Equal(new[] { RoleCategories.Management },
            Assert.IsAssignableFrom<IReadOnlyList<string>>(operationCreate.Error?.Details?["allowedCategories"]));
    }

    [Fact]
    public async Task Foreign_tenant_permission_row_cannot_be_granted_through_bootstrap()
    {
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var foreignTenantId = Guid.NewGuid();
        await using var db = CreateDatabase(tenantId);
        var foreignPermission = PermissionCatalog.CreateSeedPermissions(foreignTenantId)
            .Single(permission => permission.Name == "ProjectPlan.Create");
        // Seed permissions use stable catalog IDs across tenants; the Permission PK is global.
        foreignPermission.Id = Guid.NewGuid();
        db.Permissions.Add(foreignPermission);
        await db.SaveChangesAsync();

        var service = CreateRoleService(db, tenantId, actorId, CreateRoleAdminPermissions());
        var rejected = await service.CreateAsync(new SaveRoleDto
        {
            Name = "Foreign Project Plan Creator",
            Category = RoleCategories.Management,
            PermissionIds = [PermissionId("Planning.View"), foreignPermission.Id]
        }, actorId, tenantId);

        Assert.True(rejected.IsFailure);
        Assert.Equal(400, rejected.Error?.StatusCode);
        Assert.False(await db.Roles.IgnoreQueryFilters().AnyAsync(role => role.Name == "Foreign Project Plan Creator"));
    }

    private static Guid PermissionId(string key) => Guid.Parse(PermissionCatalog.ByKey[key].SeedId);

    private static HashSet<string> CreateRoleAdminPermissions() => new(
    [
        "Roles.View",
        "Roles.Create",
        "Roles.ConfigurePermissions",
        "TenantAdministration.ManageScope"
    ], StringComparer.Ordinal);

    private static AppDbContext CreateDatabase(Guid tenantId)
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Permissions.AddRange(PermissionCatalog.CreateSeedPermissions(tenantId));
        db.SaveChanges();
        return db;
    }

    private static RoleAdministrationService CreateRoleService(
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
