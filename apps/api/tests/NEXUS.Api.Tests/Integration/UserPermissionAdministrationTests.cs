using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using Moq;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.Identity.Application;
using NEXUS.Modules.Identity.Domain;
using NEXUS.Modules.Identity.Infrastructure;
using NEXUS.SharedKernel;
using Xunit;

namespace NEXUS.Api.Tests.Integration;

public sealed class UserPermissionAdministrationTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private PermissionTestDbContext _context = null!;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _organizationId = Guid.NewGuid();

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        var options = new DbContextOptionsBuilder<PermissionTestDbContext>()
            .UseSqlite(_connection)
            .Options;
        _context = new PermissionTestDbContext(options);
        await _context.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Fact]
    public void Permission_catalog_exposes_all_selectable_categories_without_permission_specific_restrictions()
    {
        foreach (var permission in PermissionCatalog.Entries.Where(entry => entry.IsActive))
        {
            Assert.Equal(RoleCategories.Selectable, permission.AllowedCategories);
        }

        Assert.Equal(RoleCategories.Selectable, PermissionCatalog.ByKey["Project.View"].AllowedCategories);
        Assert.Equal(RoleCategories.Selectable, PermissionCatalog.ByKey["ProjectAnnualBudget.SubmitForReview"].AllowedCategories);
        Assert.Equal(RoleCategories.Selectable, PermissionCatalog.ByKey["ProjectAnnualBudget.Approve"].AllowedCategories);
    }

    [Fact]
    public async Task Role_category_does_not_restrict_permission_grants_or_retained_grants_on_category_change()
    {
        var (actor, actorRole) = AddUserWithRole(_tenantId, "category-admin", protectedTenantAdmin: true);
        actorRole.Permissions.Add(AddPermission(_tenantId, "Roles", "View"));
        actorRole.Permissions.Add(AddPermission(_tenantId, "Roles", "Create"));
        actorRole.Permissions.Add(AddPermission(_tenantId, "Roles", "ConfigurePermissions"));
        var projectView = AddPermission(_tenantId, "Project", "View");
        var projectEdit = AddPermission(_tenantId, "Project", "Edit");
        actorRole.Permissions.Add(projectView);
        actorRole.Permissions.Add(projectEdit);
        await _context.SaveChangesAsync();

        var service = CreateRoleService(actor.Id);
        var crossCategory = await service.CreateAsync(new SaveRoleDto
        {
            Name = "Operations project editor",
            OrganizationId = _organizationId,
            Category = RoleCategories.Operation,
            PermissionIds = [projectView.Id, projectEdit.Id]
        }, actor.Id, _tenantId);
        Assert.True(crossCategory.IsSuccess, crossCategory.Error?.Message);
        Assert.Contains(crossCategory.Value!.Permissions, permission => permission.Name == "Project.Edit");

        var changedCategory = await service.UpdateAsync(crossCategory.Value!.Id, new SaveRoleDto
        {
            Name = crossCategory.Value.Name,
            Category = RoleCategories.Business,
            PermissionIds = [projectView.Id, projectEdit.Id]
        }, actor.Id, _tenantId);
        Assert.True(changedCategory.IsSuccess, changedCategory.Error?.Message);
        Assert.Contains(changedCategory.Value!.Permissions, permission => permission.Name == "Project.Edit");

        var revoked = await service.UpdateAsync(crossCategory.Value.Id, new SaveRoleDto
        {
            Name = crossCategory.Value.Name,
            Category = RoleCategories.Business,
            PermissionIds = [projectView.Id]
        }, actor.Id, _tenantId);
        Assert.True(revoked.IsSuccess, revoked.Error?.Message);
        Assert.DoesNotContain(revoked.Value!.Permissions, permission => permission.Name == "Project.Edit");

        var legacyRole = new Role { TenantId = _tenantId, Name = "Legacy Operations", Category = "Operations", CreatedBy = "legacy-seed" };
        legacyRole.AssignOrganization(_organizationId);
        legacyRole.Permissions.Add(projectView);
        legacyRole.Permissions.Add(projectEdit);
        _context.Roles.Add(legacyRole);
        await _context.SaveChangesAsync();
        var legacyCleanup = await service.UpdateAsync(legacyRole.Id, new SaveRoleDto
        {
            Name = legacyRole.Name,
            Category = legacyRole.Category,
            PermissionIds = [projectView.Id]
        }, actor.Id, _tenantId);
        Assert.True(legacyCleanup.IsSuccess, legacyCleanup.Error?.Message);
        Assert.DoesNotContain(legacyCleanup.Value!.Permissions, permission => permission.Name == "Project.Edit");
    }

    [Fact]
    public async Task Tenant_owned_role_can_receive_default_grantable_permissions_and_they_are_effective_in_its_tenant()
    {
        var (actor, actorRole) = AddUserWithRole(_tenantId, "tenant-role-admin", protectedTenantAdmin: true);
        foreach (var key in new[]
        {
            "Roles.View", "Roles.Create", "Roles.ConfigurePermissions",
            "Users.AssignRoles", "Users.SetStatus"
        })
        {
            var definition = PermissionCatalog.ByKey[key];
            actorRole.Permissions.Add(AddPermission(_tenantId, definition.Resource, definition.Action));
        }

        var projectView = AddPermission(_tenantId, "Project", "View");
        var projectEdit = AddPermission(_tenantId, "Project", "Edit");
        await _context.SaveChangesAsync();

        var service = CreateRoleService(actor.Id);
        var catalog = await service.GetAssignablePermissionsAsync(actor.Id, _tenantId);
        Assert.True(catalog.IsSuccess, catalog.Error?.Message);
        Assert.True(catalog.Value!.Single(permission => permission.Name == "Project.View").IsAssignable);
        Assert.True(catalog.Value.Single(permission => permission.Name == "Project.Edit").IsAssignable);
        Assert.True(catalog.Value.Single(permission => permission.Name == "Project.Edit").CanDelegateToCustomRole);

        var createdRole = await service.CreateAsync(new SaveRoleDto
        {
            Name = "Tenant Project Editor",
            Category = RoleCategories.Business,
            PermissionIds = [projectView.Id, projectEdit.Id]
        }, actor.Id, _tenantId);

        Assert.True(createdRole.IsSuccess, createdRole.Error?.Message);
        Assert.Null(createdRole.Value!.OrganizationId);
        Assert.Equal("Tenant", createdRole.Value.Scope);
        Assert.Contains(createdRole.Value.Permissions, permission => permission.Name == "Project.View");
        Assert.Contains(createdRole.Value.Permissions, permission => permission.Name == "Project.Edit");

        var target = new User
        {
            TenantId = _tenantId,
            FirstName = "Tenant",
            LastName = "Project User",
            Email = $"tenant-project-{Guid.NewGuid():N}@test.local",
            CreatedBy = "test"
        };
        _context.Users.Add(target);
        _context.UserRoles.Add(new UserRole
        {
            TenantId = _tenantId,
            UserId = target.Id,
            RoleId = createdRole.Value.Id,
            CreatedBy = "test"
        });
        await _context.SaveChangesAsync();

        var effective = await new UserEffectivePermissionService(_context)
            .GetEffectivePermissionsAsync(target.Id, _tenantId);
        Assert.Contains("Project.View", effective);
        Assert.Contains("Project.Edit", effective);
        Assert.DoesNotContain("Roles.ConfigurePermissions", effective);
    }

    [Fact]
    public async Task Effective_permissions_apply_direct_allow_and_deny_over_role_grants()
    {
        var (user, role) = AddUserWithRole(_tenantId, "member");
        var inherited = AddPermission(_tenantId, "Project", "View");
        var denied = AddPermission(_tenantId, "Project", "Edit");
        var directlyAllowed = AddPermission(_tenantId, "Report", "View");
        role.Permissions.Add(inherited);
        role.Permissions.Add(denied);
        _context.UserPermissionOverrides.Add(new UserPermissionOverride
        {
            TenantId = _tenantId,
            UserId = user.Id,
            PermissionId = denied.Id,
            Effect = PermissionOverrideEffect.Deny,
            CreatedBy = "test"
        });
        _context.UserPermissionOverrides.Add(new UserPermissionOverride
        {
            TenantId = _tenantId,
            UserId = user.Id,
            PermissionId = directlyAllowed.Id,
            Effect = PermissionOverrideEffect.Allow,
            CreatedBy = "test"
        });
        await _context.SaveChangesAsync();

        var effective = await new UserEffectivePermissionService(_context)
            .GetEffectivePermissionsForOrganizationAsync(user.Id, _tenantId, _organizationId);

        Assert.Contains("Project.View", effective);
        Assert.DoesNotContain("Project.Edit", effective);
        Assert.Contains("Report.View", effective);
    }

    [Fact]
    public async Task Organization_role_can_save_reload_and_grant_operational_handover_permissions_only_in_its_owner_scope()
    {
        var (actor, actorRole) = AddUserWithRole(_tenantId, "organization-role-admin", protectedTenantAdmin: true);
        actorRole.Permissions.Add(AddPermission(_tenantId, "Roles", "Create"));
        actorRole.Permissions.Add(AddPermission(_tenantId, "Roles", "EditDetails"));
        var configurePermissions = AddPermission(_tenantId, "Roles", "ConfigurePermissions");
        actorRole.Permissions.Add(configurePermissions);
        actorRole.Permissions.Add(AddPermission(_tenantId, "Roles", "Delete"));
        var projectView = AddPermission(_tenantId, "Project", "View");
        var projectEdit = AddPermission(_tenantId, "Project", "Edit");
        var portfolioView = AddPermission(_tenantId, "Portfolio", "View");
        var handoverView = AddPermission(_tenantId, "Handover", "View");
        var handoverAdminInitiate = AddPermission(_tenantId, "Handover", "AdminInitiate");
        await _context.SaveChangesAsync();

        Assert.Equal(PermissionScope.Tenant, PermissionCatalog.ByKey["Project.View"].Scope);
        Assert.Equal(PermissionScope.Tenant, PermissionCatalog.ByKey["Project.Edit"].Scope);
        Assert.Equal(PermissionScope.Tenant, PermissionCatalog.ByKey["Handover.AdminInitiate"].Scope);
        var service = CreateRoleService(actor.Id);
        var catalog = await service.GetAssignablePermissionsAsync(actor.Id, _tenantId);
        Assert.True(catalog.IsSuccess, catalog.Error?.Message);
        Assert.True(catalog.Value!.Single(item => item.Name == "Project.View").CanGrantToOrganizationRole);
        Assert.True(catalog.Value.Single(item => item.Name == "Project.Edit").CanGrantToOrganizationRole);
        Assert.True(catalog.Value.Single(item => item.Name == "Handover.View").CanGrantToOrganizationRole);
        Assert.True(catalog.Value.Single(item => item.Name == "Handover.AdminInitiate").CanGrantToOrganizationRole);
        var createdProjectRole = await service.CreateAsync(new SaveRoleDto
        {
            Name = "NEXUS Group Project Viewer",
            OrganizationId = _organizationId,
            Category = RoleCategories.Management,
            PermissionIds = [projectView.Id, projectEdit.Id, handoverView.Id, handoverAdminInitiate.Id]
        }, actor.Id, _tenantId);
        Assert.True(createdProjectRole.IsSuccess, createdProjectRole.Error?.Message);
        Assert.Equal(_organizationId, createdProjectRole.Value!.OrganizationId);
        Assert.Contains(createdProjectRole.Value.Permissions, item => item.Name == "Project.View");
        Assert.Contains(createdProjectRole.Value.Permissions, item => item.Name == "Project.Edit");
        Assert.Contains(createdProjectRole.Value.Permissions, item => item.Name == "Handover.View");
        Assert.Contains(createdProjectRole.Value.Permissions, item => item.Name == "Handover.AdminInitiate");
        Assert.True(createdProjectRole.Value.CanEditDetails);
        Assert.True(createdProjectRole.Value.CanManagePermissions);
        Assert.True(createdProjectRole.Value.CanDelete);

        var forbiddenAdministrativeGrant = await service.CreateAsync(new SaveRoleDto
        {
            Name = "NEXUS Group Tenant Administration",
            OrganizationId = _organizationId,
            Category = RoleCategories.Management,
            PermissionIds = [configurePermissions.Id]
        }, actor.Id, _tenantId);
        Assert.True(forbiddenAdministrativeGrant.IsFailure);
        Assert.Equal(400, forbiddenAdministrativeGrant.Error?.StatusCode);

        var unrelatedDetailsUpdate = await service.UpdateAsync(createdProjectRole.Value.Id, new SaveRoleDto
        {
            Name = createdProjectRole.Value.Name,
            Description = "Updated description only",
            Category = createdProjectRole.Value.Category,
            PermissionIds = [projectView.Id, projectEdit.Id, handoverView.Id, handoverAdminInitiate.Id]
        }, actor.Id, _tenantId);
        Assert.True(unrelatedDetailsUpdate.IsSuccess, unrelatedDetailsUpdate.Error?.Message);
        Assert.Contains(unrelatedDetailsUpdate.Value!.Permissions, item => item.Name == "Project.View");
        Assert.Contains(unrelatedDetailsUpdate.Value.Permissions, item => item.Name == "Project.Edit");
        Assert.Contains(unrelatedDetailsUpdate.Value.Permissions, item => item.Name == "Handover.View");
        Assert.Contains(unrelatedDetailsUpdate.Value.Permissions, item => item.Name == "Handover.AdminInitiate");

        var secondOrganizationId = Guid.NewGuid();
        var createdOtherRole = await service.CreateAsync(new SaveRoleDto
        {
            Name = "Other Organization Portfolio Viewer",
            OrganizationId = secondOrganizationId,
            Category = RoleCategories.Management,
            PermissionIds = [portfolioView.Id]
        }, actor.Id, _tenantId);
        Assert.True(createdOtherRole.IsSuccess, createdOtherRole.Error?.Message);

        _context.ChangeTracker.Clear();
        var reloaded = await service.GetByIdAsync(createdProjectRole.Value.Id, actor.Id, _tenantId);
        Assert.True(reloaded.IsSuccess, reloaded.Error?.Message);
        Assert.Equal(_organizationId, reloaded.Value!.OrganizationId);
        Assert.Contains(reloaded.Value.Permissions, item => item.Name == "Project.View");
        Assert.Contains(reloaded.Value.Permissions, item => item.Name == "Project.Edit");
        Assert.Contains(reloaded.Value.Permissions, item => item.Name == "Handover.View");
        Assert.Contains(reloaded.Value.Permissions, item => item.Name == "Handover.AdminInitiate");
        Assert.True(reloaded.Value.CanEditDetails);
        Assert.True(reloaded.Value.CanManagePermissions);
        Assert.True(reloaded.Value.CanDelete);

        var (target, _) = AddUserWithRole(_tenantId, "multi-organization-viewer");
        _context.UserRoles.AddRange(
            new UserRole { TenantId = _tenantId, UserId = target.Id, RoleId = reloaded.Value.Id, CreatedBy = "test" },
            new UserRole { TenantId = _tenantId, UserId = target.Id, RoleId = createdOtherRole.Value!.Id, CreatedBy = "test" });
        await _context.SaveChangesAsync();

        var effective = new UserEffectivePermissionService(_context);
        var owningOrganizationPermissions = await effective.GetEffectivePermissionsForOrganizationAsync(
            target.Id, _tenantId, _organizationId);
        var otherOrganizationPermissions = await effective.GetEffectivePermissionsForOrganizationAsync(
            target.Id, _tenantId, secondOrganizationId);
        var unscopedPermissions = await effective.GetEffectivePermissionsAsync(target.Id, _tenantId);
        Assert.Contains("Project.View", owningOrganizationPermissions);
        Assert.Contains("Project.Edit", owningOrganizationPermissions);
        Assert.Contains("Handover.View", owningOrganizationPermissions);
        Assert.Contains("Handover.AdminInitiate", owningOrganizationPermissions);
        Assert.DoesNotContain("Portfolio.View", owningOrganizationPermissions);
        Assert.Contains("Portfolio.View", otherOrganizationPermissions);
        Assert.DoesNotContain("Project.View", otherOrganizationPermissions);
        Assert.DoesNotContain("Project.Edit", otherOrganizationPermissions);
        Assert.DoesNotContain("Handover.View", otherOrganizationPermissions);
        Assert.DoesNotContain("Handover.AdminInitiate", otherOrganizationPermissions);
        Assert.DoesNotContain("Project.View", unscopedPermissions);
        Assert.DoesNotContain("Project.Edit", unscopedPermissions);
        Assert.DoesNotContain("Handover.View", unscopedPermissions);
        Assert.DoesNotContain("Handover.AdminInitiate", unscopedPermissions);
        Assert.DoesNotContain("Portfolio.View", unscopedPermissions);

        _context.UserPermissionOverrides.Add(new UserPermissionOverride
        {
            TenantId = _tenantId,
            UserId = target.Id,
            PermissionId = projectView.Id,
            Effect = PermissionOverrideEffect.Deny,
            CreatedBy = "test"
        });
        _context.UserPermissionOverrides.Add(new UserPermissionOverride
        {
            TenantId = _tenantId,
            UserId = target.Id,
            PermissionId = handoverAdminInitiate.Id,
            Effect = PermissionOverrideEffect.Deny,
            CreatedBy = "test"
        });
        await _context.SaveChangesAsync();

        owningOrganizationPermissions = await effective.GetEffectivePermissionsForOrganizationAsync(
            target.Id, _tenantId, _organizationId);
        otherOrganizationPermissions = await effective.GetEffectivePermissionsForOrganizationAsync(
            target.Id, _tenantId, secondOrganizationId);
        Assert.DoesNotContain("Project.View", owningOrganizationPermissions);
        Assert.DoesNotContain("Project.Edit", owningOrganizationPermissions);
        Assert.Contains("Handover.View", owningOrganizationPermissions);
        Assert.DoesNotContain("Handover.AdminInitiate", owningOrganizationPermissions);
        Assert.Contains("Portfolio.View", otherOrganizationPermissions);
    }

    [Fact]
    public async Task Organization_roles_with_the_same_name_and_unresolved_legacy_roles_do_not_cross_scope()
    {
        var user = new User
        {
            TenantId = _tenantId,
            FirstName = "Scoped",
            LastName = "User",
            Email = $"scoped-{Guid.NewGuid():N}@test.local",
            CreatedBy = "test"
        };
        var organizationA = Guid.NewGuid();
        var organizationB = Guid.NewGuid();
        var organizationRoleA = new Role
        {
            TenantId = _tenantId,
            Name = "Department Manager",
            CreatedBy = "test"
        };
        organizationRoleA.AssignOrganization(organizationA);
        organizationRoleA.Permissions.Add(AddPermission(_tenantId, "ProjectAnnualBudget", "View"));

        var organizationRoleB = new Role
        {
            TenantId = _tenantId,
            Name = "Department Manager",
            CreatedBy = "test"
        };
        organizationRoleB.AssignOrganization(organizationB);
        organizationRoleB.Permissions.Add(AddPermission(_tenantId, "ProjectAnnualBudgetClosing", "View"));

        var unresolvedLegacyRole = new Role
        {
            TenantId = _tenantId,
            Name = "Unresolved Legacy Role",
            CreatedBy = "test"
        };
        unresolvedLegacyRole.Permissions.Add(AddPermission(_tenantId, "ProjectSettings", "View"));

        user.UserRoles.Add(new UserRole { TenantId = _tenantId, Role = organizationRoleA, CreatedBy = "test" });
        user.UserRoles.Add(new UserRole { TenantId = _tenantId, Role = organizationRoleB, CreatedBy = "test" });
        user.UserRoles.Add(new UserRole { TenantId = _tenantId, Role = unresolvedLegacyRole, CreatedBy = "test" });
        _context.Users.Add(user);
        _context.Roles.AddRange(organizationRoleA, organizationRoleB, unresolvedLegacyRole);
        await _context.SaveChangesAsync();

        var service = new UserEffectivePermissionService(_context);
        var permissionsA = await service.GetEffectivePermissionsForOrganizationAsync(user.Id, _tenantId, organizationA);
        var permissionsB = await service.GetEffectivePermissionsForOrganizationAsync(user.Id, _tenantId, organizationB);

        Assert.Contains("ProjectAnnualBudget.View", permissionsA);
        Assert.DoesNotContain("ProjectAnnualBudgetClosing.View", permissionsA);
        Assert.DoesNotContain("ProjectSettings.View", permissionsA);
        Assert.Contains("ProjectAnnualBudgetClosing.View", permissionsB);
        Assert.DoesNotContain("ProjectAnnualBudget.View", permissionsB);
        Assert.DoesNotContain("ProjectSettings.View", permissionsB);
    }

    [Fact]
    public async Task Canonical_tenant_admin_document_grants_are_hydrated_for_project_scoped_requests()
    {
        var user = new User
        {
            TenantId = _tenantId,
            FirstName = "Tenant",
            LastName = "Document Admin",
            Email = $"tenant-docs-{Guid.NewGuid():N}@test.local",
            CreatedBy = "test"
        };
        var role = new Role
        {
            TenantId = _tenantId,
            Name = "Tenant Administrator",
            Category = RoleCategories.System,
            IsSystem = true,
            CreatedBy = "test"
        };
        role.AssignSystemRoleKey(Role.TenantAdminRoleKey, _tenantId);
        role.Permissions.Add(AddPermission(_tenantId, "Document", "View"));
        role.Permissions.Add(AddPermission(_tenantId, "Document", "Create"));
        user.UserRoles.Add(new UserRole { TenantId = _tenantId, User = user, Role = role, CreatedBy = "test" });
        _context.Users.Add(user);
        _context.Roles.Add(role);
        var projectId = Guid.NewGuid();
        var sessionId = AddActiveSession(user, _tenantId);
        await _context.SaveChangesAsync();

        var projectLookup = new Mock<IProjectOrganizationLookup>();
        projectLookup.Setup(lookup => lookup.ResolveOrganizationIdAsync(
                _tenantId, projectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_organizationId);
        var tenantRegistry = new Mock<ITenantRegistryStatusValidator>();
        tenantRegistry.Setup(validator => validator.IsActiveAsync(_tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var httpContext = CreatePermissionContext(user.Id, sessionId, projectId);
        var middleware = new UserPermissionClaimsMiddleware(context =>
        {
            Assert.Contains(context.User.FindAll("permission"), claim => claim.Value == "Document.View");
            Assert.Contains(context.User.FindAll("permission"), claim => claim.Value == "Document.Create");
            Assert.Equal(_organizationId, context.Items[UserPermissionClaimsMiddleware.OrganizationScopeItemKey]);
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(httpContext, new UserEffectivePermissionService(_context),
            new UserSessionValidator(_context), tenantRegistry.Object, projectLookup.Object);

        Assert.Equal(StatusCodes.Status200OK, httpContext.Response.StatusCode);
        projectLookup.Verify(lookup => lookup.ResolveOrganizationIdAsync(
            _tenantId, projectId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Organization_owned_document_grants_stay_in_owner_scope_and_mismatched_scope_is_rejected()
    {
        var user = new User
        {
            TenantId = _tenantId,
            FirstName = "Organization",
            LastName = "Document Manager",
            Email = $"org-docs-{Guid.NewGuid():N}@test.local",
            CreatedBy = "test"
        };
        var organizationA = _organizationId;
        var organizationB = Guid.NewGuid();
        var role = new Role
        {
            TenantId = _tenantId,
            Name = "Portfolio Manager",
            Category = RoleCategories.Management,
            CreatedBy = "test"
        };
        role.AssignRoleKey("organization.portfolio-manager", _tenantId);
        role.AssignOrganization(organizationA);
        role.Permissions.Add(AddPermission(_tenantId, "Document", "View"));
        role.Permissions.Add(AddPermission(_tenantId, "Document", "Create"));
        user.UserRoles.Add(new UserRole { TenantId = _tenantId, User = user, Role = role, CreatedBy = "test" });
        _context.Users.Add(user);
        _context.Roles.Add(role);
        var projectA = Guid.NewGuid();
        var projectB = Guid.NewGuid();
        var sessionId = AddActiveSession(user, _tenantId);
        await _context.SaveChangesAsync();

        var projectLookup = new Mock<IProjectOrganizationLookup>();
        projectLookup.Setup(lookup => lookup.ResolveOrganizationIdAsync(
                _tenantId, projectA, It.IsAny<CancellationToken>()))
            .ReturnsAsync(organizationA);
        projectLookup.Setup(lookup => lookup.ResolveOrganizationIdAsync(
                _tenantId, projectB, It.IsAny<CancellationToken>()))
            .ReturnsAsync(organizationB);
        var tenantRegistry = new Mock<ITenantRegistryStatusValidator>();
        tenantRegistry.Setup(validator => validator.IsActiveAsync(_tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var ownerScopeContext = CreatePermissionContext(user.Id, sessionId, projectA);
        var ownerScopeMiddleware = new UserPermissionClaimsMiddleware(context =>
        {
            Assert.Contains(context.User.FindAll("permission"), claim => claim.Value == "Document.View");
            return Task.CompletedTask;
        });
        await ownerScopeMiddleware.InvokeAsync(ownerScopeContext, new UserEffectivePermissionService(_context),
            new UserSessionValidator(_context), tenantRegistry.Object, projectLookup.Object);

        var otherScopeContext = CreatePermissionContext(user.Id, sessionId, projectB);
        var otherScopeMiddleware = new UserPermissionClaimsMiddleware(context =>
        {
            Assert.DoesNotContain(context.User.FindAll("permission"), claim => claim.Value == "Document.View");
            Assert.DoesNotContain(context.User.FindAll("permission"), claim => claim.Value == "Document.Create");
            return Task.CompletedTask;
        });
        await otherScopeMiddleware.InvokeAsync(otherScopeContext, new UserEffectivePermissionService(_context),
            new UserSessionValidator(_context), tenantRegistry.Object, projectLookup.Object);

        var mismatchedContext = CreatePermissionContext(user.Id, sessionId, projectA, organizationB);
        var nextCalled = false;
        var mismatchedMiddleware = new UserPermissionClaimsMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        await mismatchedMiddleware.InvokeAsync(mismatchedContext, new UserEffectivePermissionService(_context),
            new UserSessionValidator(_context), tenantRegistry.Object, projectLookup.Object);

        Assert.Contains(ownerScopeContext.User.FindAll("permission"), claim => claim.Value == "Document.View");
        Assert.DoesNotContain(otherScopeContext.User.FindAll("permission"), claim => claim.Value == "Document.View");
        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status404NotFound, mismatchedContext.Response.StatusCode);
    }

    [Fact]
    public async Task Global_role_names_include_only_the_canonical_tenant_admin_identity()
    {
        var user = new User
        {
            TenantId = _tenantId,
            FirstName = "Workflow",
            LastName = "User",
            Email = $"workflow-{Guid.NewGuid():N}@test.local",
            CreatedBy = "test"
        };
        var tenantAdminRole = new Role
        {
            TenantId = _tenantId,
            Name = "Tenant Administrator",
            IsSystem = true,
            CreatedBy = "test"
        };
        tenantAdminRole.AssignSystemRoleKey(Role.TenantAdminRoleKey, _tenantId);
        var organizationAdminRole = new Role
        {
            TenantId = _tenantId,
            Name = "Organization Administrator",
            IsSystem = true,
            CreatedBy = "test"
        };
        organizationAdminRole.AssignSystemRoleKey(Role.OrganizationAdminRoleKey, _tenantId);
        var organizationRole = new Role
        {
            TenantId = _tenantId,
            Name = "Workflow Approver",
            CreatedBy = "test"
        };
        organizationRole.AssignOrganization(Guid.NewGuid());
        var unresolvedLegacyRole = new Role
        {
            TenantId = _tenantId,
            Name = "Legacy Workflow Approver",
            CreatedBy = "test"
        };

        user.UserRoles.Add(new UserRole { TenantId = _tenantId, Role = tenantAdminRole, CreatedBy = "test" });
        user.UserRoles.Add(new UserRole { TenantId = _tenantId, Role = organizationAdminRole, CreatedBy = "test" });
        user.UserRoles.Add(new UserRole { TenantId = _tenantId, Role = organizationRole, CreatedBy = "test" });
        user.UserRoles.Add(new UserRole { TenantId = _tenantId, Role = unresolvedLegacyRole, CreatedBy = "test" });
        _context.Users.Add(user);
        _context.Roles.AddRange(tenantAdminRole, organizationAdminRole, organizationRole, unresolvedLegacyRole);
        await _context.SaveChangesAsync();

        var names = await new UserEffectivePermissionService(_context).GetEffectiveRoleNamesAsync(user.Id, _tenantId);

        Assert.Equal(new[] { "Tenant Administrator" }, names);
    }

    [Fact]
    public async Task Create_and_edit_user_use_role_ids_and_derive_organizations_from_assigned_roles()
    {
        var (actor, actorRole) = AddUserWithRole(_tenantId, "tenant-admin", protectedTenantAdmin: true);
        var organizationId = Guid.NewGuid();
        var otherOrganizationId = Guid.NewGuid();
        var organizationRole = new Role
        {
            TenantId = _tenantId,
            Name = "Organization Contributor",
            CreatedBy = "test"
        };
        organizationRole.AssignOrganization(organizationId);
        _context.Roles.Add(organizationRole);
        await _context.SaveChangesAsync();

        var audit = new Mock<IAuditService>();
        audit.Setup(service => service.AppendAsync(It.IsAny<AuditEntry>(), It.IsAny<CancellationToken>()))
            .Callback<AuditEntry, CancellationToken>((entry, _) => _context.AuditEntries.Add(entry))
            .ReturnsAsync(Result.Success());
        var hasher = new Mock<Microsoft.AspNetCore.Identity.IPasswordHasher<User>>();
        hasher.Setup(passwordHasher => passwordHasher.HashPassword(It.IsAny<User>(), It.IsAny<string>()))
            .Returns("hashed-password");
        var service = CreateUserAdministrationService(actor.Id, audit.Object, hasher.Object);

        var created = await service.CreateAsync(new CreateUserDto
        {
            Email = "role-id-assignment@example.test",
            FirstName = "Role",
            LastName = "Assignment",
            IsActive = true,
            RoleIds = [organizationRole.Id]
        }, actor.Id.ToString("D"));

        Assert.True(created.IsSuccess, created.Error?.Message);
        Assert.Equal(organizationRole.Id, Assert.Single(created.Value!.User.Roles).Id);
        Assert.Equal(organizationId, Assert.Single(created.Value.User.Organizations).Id);
        var createdUserId = created.Value.User.Id;
        Assert.True(await _context.UserRoles.IgnoreQueryFilters()
            .AnyAsync(assignment => assignment.UserId == createdUserId
                && assignment.RoleId == organizationRole.Id && !assignment.IsDeleted));

        var unchanged = await service.UpdateAsync(createdUserId, new UpdateUserDto
        {
            Email = created.Value.User.Email,
            FirstName = created.Value.User.FirstName,
            LastName = created.Value.User.LastName,
            IsActive = true,
            RoleIds = [organizationRole.Id]
        }, actor.Id.ToString("D"));

        Assert.True(unchanged.IsSuccess, unchanged.Error?.Message);
        Assert.Equal(organizationId, Assert.Single(unchanged.Value!.Organizations).Id);
        Assert.DoesNotContain(unchanged.Value.Organizations, item => item.Id == otherOrganizationId);

        var removed = await service.UpdateAsync(createdUserId, new UpdateUserDto
        {
            Email = unchanged.Value.Email,
            FirstName = unchanged.Value.FirstName,
            LastName = unchanged.Value.LastName,
            IsActive = true,
            RoleIds = []
        }, actor.Id.ToString("D"));

        Assert.True(removed.IsSuccess, removed.Error?.Message);
        Assert.Empty(removed.Value!.Organizations);
        Assert.DoesNotContain(await _context.UserRoles.IgnoreQueryFilters()
                .Where(assignment => assignment.UserId == createdUserId).ToListAsync(),
            assignment => !assignment.IsDeleted);
    }

    [Fact]
    public async Task User_permission_explanation_reports_role_sources_direct_deny_and_catalog_scope()
    {
        var (actor, actorRole) = AddUserWithRole(_tenantId, "permission-explanation-admin", protectedTenantAdmin: true);
        actorRole.Permissions.Add(AddPermission(_tenantId, "Users", "View"));
        actor.IsActive = true;
        actorRole.Permissions.Add(AddPermission(_tenantId, "UserPermissions", "Manage"));
        var (target, primaryRole) = AddUserWithRole(_tenantId, "Project Manager");
        target.IsActive = true;
        primaryRole.Name = "Project Manager";
        var secondRole = new Role { TenantId = _tenantId, Name = "Finance Reviewer", CreatedBy = "test" };
        var permission = AddPermission(_tenantId, "Project", "View");
        primaryRole.Permissions.Add(permission);
        secondRole.Permissions.Add(permission);
        _context.Roles.Add(secondRole);
        _context.UserRoles.Add(new UserRole { TenantId = _tenantId, User = target, Role = secondRole, CreatedBy = "test" });
        _context.UserPermissionOverrides.Add(new UserPermissionOverride
        {
            TenantId = _tenantId,
            UserId = target.Id,
            PermissionId = permission.Id,
            Effect = PermissionOverrideEffect.Deny,
            CreatedBy = "test"
        });
        await _context.SaveChangesAsync();

        var result = await CreateUserAdministrationService(actor.Id).GetUserPermissionsAsync(target.Id, _tenantId);

        Assert.True(result.IsSuccess, result.Error?.Message);
        var assignment = Assert.Single(result.Value!, item => item.Id == permission.Id);
        Assert.Equal(PermissionCatalog.ByKey["Project.View"].Scope.ToString(), assignment.Scope);
        Assert.Equal(new[] { "Finance Reviewer", "Project Manager" }, assignment.RoleGrantSources);
        Assert.True(assignment.IsRoleGranted);
        Assert.Equal(PermissionOverrideEffect.Deny, assignment.OverrideEffect);
        Assert.False(assignment.IsEffective);
    }

    [Fact]
    public async Task User_administration_service_requires_effective_read_permission_and_binds_actor_identity()
    {
        var (actor, actorRole) = AddUserWithRole(_tenantId, "service-actor");
        await _context.SaveChangesAsync();

        var audit = new Mock<IAuditService>();
        audit.Setup(service => service.AppendAsync(It.IsAny<AuditEntry>(), It.IsAny<CancellationToken>()))
            .Callback<AuditEntry, CancellationToken>((entry, _) => _context.AuditEntries.Add(entry))
            .ReturnsAsync(Result.Success());
        var http = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, actor.Id.ToString()),
                    new Claim("tenantId", _tenantId.ToString())
                ], "test"))
            }
        };
        var service = CreateUserAdministrationService(http, audit.Object);

        var users = await service.GetPagedAsync(1, 20, null);
        var roles = await service.GetRolesAsync();
        Assert.True(users.IsFailure);
        Assert.Equal(403, users.Error?.StatusCode);
        Assert.True(roles.IsFailure);
        Assert.Equal(403, roles.Error?.StatusCode);

        actorRole.Permissions.Add(AddPermission(_tenantId, "Users", "View"));
        actorRole.Permissions.Add(AddPermission(_tenantId, "Users", "ManageAccounts"));
        await _context.SaveChangesAsync();
        var forgedActorCreate = await service.CreateAsync(new CreateUserDto
        {
            Email = "forged@example.test",
            FirstName = "Forged",
            LastName = "Actor",
            RoleIds = []
        }, Guid.NewGuid().ToString());

        Assert.True(forgedActorCreate.IsFailure);
        Assert.Equal(403, forgedActorCreate.Error?.StatusCode);
        Assert.DoesNotContain(await _context.Users.IgnoreQueryFilters().ToListAsync(), user => user.Email == "forged@example.test");
    }

    [Fact]
    public async Task Admin_permissions_come_from_persisted_grants_and_respect_active_deny()
    {
        var (forgedUser, _) = AddUserWithRole(_tenantId, "admin");
        var (protectedUser, protectedRole) = AddUserWithRole(_tenantId, "admin", protectedTenantAdmin: true);
        protectedRole.CreatedBy = "System";
        var protectedPermission = AddPermission(_tenantId, "Roles", "ConfigurePermissions");
        var explicitlyGrantedPermission = AddPermission(_tenantId, "Document", "View");
        protectedRole.Permissions.Add(protectedPermission);
        protectedRole.Permissions.Add(explicitlyGrantedPermission);
        _context.UserPermissionOverrides.Add(new UserPermissionOverride
        {
            TenantId = _tenantId,
            UserId = protectedUser.Id,
            PermissionId = protectedPermission.Id,
            Effect = PermissionOverrideEffect.Deny,
            CreatedBy = "test"
        });
        await _context.SaveChangesAsync();

        var resolver = new UserEffectivePermissionService(_context);
        var protectedPermissions = await resolver.GetEffectivePermissionsAsync(protectedUser.Id, _tenantId);

        Assert.DoesNotContain("Roles.ConfigurePermissions", protectedPermissions);
        Assert.Contains("Document.View", protectedPermissions);
        Assert.DoesNotContain("Users.AssignRoles", protectedPermissions);
        Assert.DoesNotContain("Roles.ConfigurePermissions",
            await resolver.GetEffectivePermissionsAsync(forgedUser.Id, _tenantId));
    }

    [Fact]
    public async Task Direct_deny_removes_an_admin_permission_without_removing_the_last_admin_identity()
    {
        var (actor, actorRole) = AddUserWithRole(_tenantId, "override-admin", protectedTenantAdmin: true);
        var (target, targetRole) = AddUserWithRole(_tenantId, "admin", protectedTenantAdmin: true);
        var (backupAdmin, backupAdminRole) = AddUserWithRole(_tenantId, "admin-backup");
        targetRole.CreatedBy = "System";
        var backupAssignment = backupAdmin.UserRoles.Single();
        backupAssignment.Role = targetRole;
        backupAssignment.RoleId = targetRole.Id;
        backupAdminRole.UserRoles.Clear();
        var manageOverrides = AddPermission(_tenantId, "UserPermissions", "Manage");
        var usersView = AddPermission(_tenantId, "Users", "View");
        var rolesView = AddPermission(_tenantId, "Roles", "View");
        var configureRoles = AddPermission(_tenantId, "Roles", "ConfigurePermissions");
        actorRole.Permissions.Add(manageOverrides);
        actorRole.Permissions.Add(usersView);
        actorRole.Permissions.Add(rolesView);
        actorRole.Permissions.Add(configureRoles);
        await _context.SaveChangesAsync();

        var audit = new Mock<IAuditService>();
        audit.Setup(service => service.AppendAsync(It.IsAny<AuditEntry>(), It.IsAny<CancellationToken>()))
            .Callback<AuditEntry, CancellationToken>((entry, _) => _context.AuditEntries.Add(entry))
            .ReturnsAsync(Result.Success());
        var http = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, actor.Id.ToString()), new Claim("tenantId", _tenantId.ToString()), new Claim("permission", "UserPermissions.Manage")], "test"))
            }
        };
        var permissionService = new UserEffectivePermissionService(_context);
        var service = CreateUserAdministrationService(http, audit.Object, permissionService);

        var updated = await service.UpdateUserPermissionsAsync(target.Id, _tenantId,
            new UpdateUserPermissionsDto
            {
                Overrides = [new UserPermissionOverrideInput
                {
                    PermissionId = configureRoles.Id,
                    Effect = PermissionOverrideEffect.Deny
                }]
            }, actor.Id.ToString());

        Assert.True(updated.IsSuccess, updated.Error?.Message);
        Assert.DoesNotContain("Roles.ConfigurePermissions",
            await permissionService.GetEffectivePermissionsAsync(target.Id, _tenantId));
        Assert.Contains("Roles.ConfigurePermissions",
            await permissionService.GetEffectivePermissionsAsync(backupAdmin.Id, _tenantId));
        var directDeny = await _context.UserPermissionOverrides.SingleAsync(
            item => item.UserId == target.Id && item.PermissionId == configureRoles.Id && !item.IsDeleted);
        Assert.Equal(PermissionOverrideEffect.Deny, directDeny.Effect);
    }

    [Fact]
    public async Task User_status_change_cannot_remove_the_last_active_tenant_admin_assignment()
    {
        var (actor, actorRole) = AddUserWithRole(_tenantId, "status-actor");
        var statusPermission = AddPermission(_tenantId, "Users", "SetStatus");
        actorRole.Permissions.Add(AddPermission(_tenantId, "Users", "View"));
        actorRole.Permissions.Add(statusPermission);
        _context.UserPermissionOverrides.Add(new UserPermissionOverride
        {
            TenantId = _tenantId,
            UserId = actor.Id,
            PermissionId = statusPermission.Id,
            Effect = PermissionOverrideEffect.Allow,
            CreatedBy = "test"
        });
        var (target, adminRole) = AddUserWithRole(_tenantId, "admin", protectedTenantAdmin: true);
        adminRole.CreatedBy = "System";
        await _context.SaveChangesAsync();

        var audit = new Mock<IAuditService>();
        audit.Setup(service => service.AppendAsync(It.IsAny<AuditEntry>(), It.IsAny<CancellationToken>()))
            .Callback<AuditEntry, CancellationToken>((entry, _) => _context.AuditEntries.Add(entry))
            .ReturnsAsync(Result.Success());
        var http = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, actor.Id.ToString()), new Claim("tenantId", _tenantId.ToString()), new Claim("permission", "Users.SetStatus")], "test"))
            }
        };
        var service = CreateUserAdministrationService(http, audit.Object);

        var rejected = await service.UpdateAsync(target.Id, new UpdateUserDto
        {
            Email = target.Email,
            FirstName = target.FirstName,
            LastName = target.LastName,
            IsActive = false,
            RoleIds = [adminRole.Id]
        }, actor.Id.ToString());

        Assert.True(rejected.IsFailure);
        Assert.Equal(409, rejected.Error?.StatusCode);
        Assert.True((await _context.Users.IgnoreQueryFilters().SingleAsync(user => user.Id == target.Id)).IsActive);
        Assert.Single(await _context.AuditEntries.Where(entry => entry.EntityId == target.Id
            && entry.Action == "TenantAdminMutationRejected").ToListAsync());
    }

    [Fact]
    public async Task Effective_permissions_ignore_cross_tenant_role_assignments_and_inactive_users()
    {
        var (user, role) = AddUserWithRole(_tenantId, "member");
        var ownPermission = AddPermission(_tenantId, "Project", "View");
        role.Permissions.Add(ownPermission);
        var (foreignUser, foreignRole) = AddUserWithRole(Guid.NewGuid(), "foreign");
        var foreignPermission = AddPermission(foreignUser.TenantId, "admin", "write");
        foreignRole.Permissions.Add(foreignPermission);
        _context.UserRoles.Add(new UserRole
        {
            TenantId = _tenantId,
            UserId = user.Id,
            RoleId = foreignRole.Id,
            CreatedBy = "test"
        });
        await _context.SaveChangesAsync();

        var permissionService = new UserEffectivePermissionService(_context);
        var activePermissions = await permissionService.GetEffectivePermissionsForOrganizationAsync(user.Id, _tenantId, _organizationId);
        Assert.Contains("Project.View", activePermissions);
        Assert.DoesNotContain("admin.write", activePermissions);

        user.IsActive = false;
        await _context.SaveChangesAsync();
        Assert.Empty(await permissionService.GetEffectivePermissionsAsync(user.Id, _tenantId));
    }

    [Fact]
    public async Task Direct_permission_administration_cannot_read_or_update_a_user_from_another_tenant()
    {
        var foreignTenantId = Guid.NewGuid();
        var (actor, actorRole) = AddUserWithRole(_tenantId, "override-actor", protectedTenantAdmin: true);
        actorRole.Permissions.Add(AddPermission(_tenantId, "Users", "View"));
        actorRole.Permissions.Add(AddPermission(_tenantId, "UserPermissions", "Manage"));
        var grantablePermission = AddPermission(_tenantId, "Project", "View");
        actorRole.Permissions.Add(grantablePermission);

        var (foreignUser, foreignRole) = AddUserWithRole(foreignTenantId, "foreign-target");
        foreignRole.Permissions.Add(AddPermission(foreignTenantId, "Project", "View"));
        foreignRole.Permissions.Add(AddPermission(foreignTenantId, "Project", "Edit"));
        await _context.SaveChangesAsync();

        var service = CreateUserAdministrationService(actor.Id);
        var read = await service.GetUserPermissionsAsync(foreignUser.Id, _tenantId);
        var update = await service.UpdateUserPermissionsAsync(
            foreignUser.Id,
            _tenantId,
            new UpdateUserPermissionsDto
            {
                Overrides =
                [
                    new UserPermissionOverrideInput
                    {
                        PermissionId = grantablePermission.Id,
                        Effect = PermissionOverrideEffect.Allow
                    }
                ]
            },
            actor.Id.ToString());

        Assert.True(read.IsFailure);
        Assert.Equal(404, read.Error?.StatusCode);
        Assert.True(update.IsFailure);
        Assert.Equal(404, update.Error?.StatusCode);
        Assert.Empty(await _context.UserPermissionOverrides.IgnoreQueryFilters()
            .Where(item => item.UserId == foreignUser.Id && item.TenantId == foreignTenantId && !item.IsDeleted)
            .ToListAsync());
        Assert.Contains("Project.Edit", await new UserEffectivePermissionService(_context)
            .GetEffectivePermissionsForOrganizationAsync(foreignUser.Id, foreignTenantId, foreignRole.OrganizationId!.Value));
    }

    [Fact]
    public async Task Middleware_replaces_stale_token_permission_claims_with_current_permissions()
    {
        var (user, role) = AddUserWithRole(_tenantId, "member");
        var current = AddPermission(_tenantId, "Project", "View");
        var directlyAllowed = AddPermission(_tenantId, "Report", "View");
        role.Permissions.Add(current);
        _context.UserPermissionOverrides.Add(new UserPermissionOverride
        {
            TenantId = _tenantId,
            UserId = user.Id,
            PermissionId = directlyAllowed.Id,
            Effect = PermissionOverrideEffect.Allow,
            CreatedBy = "test"
        });
        var sessionId = AddActiveSession(user, _tenantId);
        await _context.SaveChangesAsync();

        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim("tenantId", _tenantId.ToString()),
            new Claim("sid", sessionId.ToString()),
            new Claim(ClaimTypes.Role, "SystemAdministrator"),
            new Claim("permission", "Project.Delete")
        ], "test");
        var httpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
        var middleware = new UserPermissionClaimsMiddleware(context =>
        {
            Assert.Contains(context.User.FindAll("permission"), claim => claim.Value == "Report.View");
            Assert.DoesNotContain(context.User.FindAll("permission"), claim => claim.Value == "Project.View");
            Assert.DoesNotContain(context.User.FindAll("permission"), claim => claim.Value == "Project.Delete");
            Assert.DoesNotContain(context.User.FindAll(ClaimTypes.Role), claim => claim.Value == "member");
            Assert.DoesNotContain(context.User.FindAll(ClaimTypes.Role), claim => claim.Value == "SystemAdministrator");
            return Task.CompletedTask;
        });

        var tenantRegistry = new Mock<ITenantRegistryStatusValidator>();
        tenantRegistry.Setup(validator => validator.IsActiveAsync(_tenantId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        await middleware.InvokeAsync(httpContext, new UserEffectivePermissionService(_context), new UserSessionValidator(_context), tenantRegistry.Object);
        Assert.Equal(StatusCodes.Status200OK, httpContext.Response.StatusCode);
    }

    [Fact]
    public async Task Middleware_rejects_revoked_access_token_session()
    {
        var (user, _) = AddUserWithRole(_tenantId, "member");
        var sessionId = AddActiveSession(user, _tenantId);
        _context.UserSessions.Local.Single(session => session.Id == sessionId).RevokedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var nextCalled = false;
        var middleware = new UserPermissionClaimsMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim("tenantId", _tenantId.ToString()),
            new Claim("sid", sessionId.ToString()),
            new Claim("permission", "admin.write")
        ], "test");
        var httpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };

        var tenantRegistry = new Mock<ITenantRegistryStatusValidator>();
        tenantRegistry.Setup(validator => validator.IsActiveAsync(_tenantId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        await middleware.InvokeAsync(httpContext, new UserEffectivePermissionService(_context), new UserSessionValidator(_context), tenantRegistry.Object);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status401Unauthorized, httpContext.Response.StatusCode);
        Assert.DoesNotContain(httpContext.User.FindAll("permission"), claim => claim.Value == "admin.write");
    }

    [Fact]
    public async Task Middleware_skips_session_validation_for_anonymous_endpoint_without_a_session()
    {
        var sessionId = Guid.NewGuid();
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim("tenantId", _tenantId.ToString()),
            new Claim("sid", sessionId.ToString()),
            new Claim("permission", "Project.Delete")
        ], "test");
        var httpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
        httpContext.SetEndpoint(new Endpoint(
            null,
            new EndpointMetadataCollection(new AllowAnonymousAttribute()),
            "POST /api/auth/login"));

        var nextCalled = false;
        var middleware = new UserPermissionClaimsMiddleware(_ =>
        {
            Assert.DoesNotContain(httpContext.User.FindAll("permission"), claim => claim.Value == "Project.Delete");
            nextCalled = true;
            return Task.CompletedTask;
        });
        var tenantRegistry = new Mock<ITenantRegistryStatusValidator>();

        await middleware.InvokeAsync(
            httpContext,
            new UserEffectivePermissionService(_context),
            new UserSessionValidator(_context),
            tenantRegistry.Object);

        Assert.Empty(await _context.UserSessions.ToListAsync());
        Assert.True(nextCalled);
        Assert.Equal(StatusCodes.Status200OK, httpContext.Response.StatusCode);
    }

    [Fact]
    public async Task Session_validator_rejects_session_bound_to_different_tenant_identity()
    {
        var (user, _) = AddUserWithRole(_tenantId, "member");
        var sessionId = AddActiveSession(user, _tenantId);
        await _context.SaveChangesAsync();

        var validator = new UserSessionValidator(_context);

        Assert.False(await validator.IsActiveAsync(sessionId, user.Id, Guid.NewGuid()));
        Assert.False(await validator.IsActiveAsync(sessionId, Guid.NewGuid(), _tenantId));
    }

    [Fact]
    public async Task Session_validator_rejects_expired_sessions_and_inactive_users()
    {
        var (user, _) = AddUserWithRole(_tenantId, "member");
        var expiredSessionId = AddActiveSession(user, _tenantId);
        _context.UserSessions.Local.Single(session => session.Id == expiredSessionId).ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
        var inactiveSessionId = AddActiveSession(user, _tenantId);
        user.IsActive = false;
        await _context.SaveChangesAsync();

        var validator = new UserSessionValidator(_context);

        Assert.False(await validator.IsActiveAsync(expiredSessionId, user.Id, _tenantId));
        Assert.False(await validator.IsActiveAsync(inactiveSessionId, user.Id, _tenantId));
    }

    [Fact]
    public async Task Permission_updates_require_actor_authority_and_audit_successful_changes()
    {
        var (actor, actorRole) = AddUserWithRole(_tenantId, "admin", protectedTenantAdmin: true);
        var (target, targetRole) = AddUserWithRole(_tenantId, "member");
        var manageOverrides = AddPermission(_tenantId, "UserPermissions", "Manage");
        var usersRead = AddPermission(_tenantId, "Users", "View");
        var projectView = AddPermission(_tenantId, "Project", "View");
        var editProjects = AddPermission(_tenantId, "Project", "Edit");
        var unauthorizedPermission = AddPermission(_tenantId, "Settings", "ManageTenantConfiguration");
        var unflaggedAdminCorePermission = AddPermission(_tenantId, "Roles", "ConfigurePermissions");
        actorRole.Permissions.Add(manageOverrides);
        actorRole.Permissions.Add(usersRead);
        actorRole.Permissions.Add(projectView);
        actorRole.Permissions.Add(editProjects);
        targetRole.Permissions.Add(projectView);
        targetRole.Permissions.Add(editProjects);
        await _context.SaveChangesAsync();

        var audit = new Mock<IAuditService>();
        audit.Setup(service => service.AppendAsync(It.IsAny<AuditEntry>(), It.IsAny<CancellationToken>()))
            .Callback<AuditEntry, CancellationToken>((entry, _) => _context.AuditEntries.Add(entry))
            .ReturnsAsync(Result.Success());
        var http = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, actor.Id.ToString()), new Claim("tenantId", _tenantId.ToString()), new Claim("permission", "UserPermissions.Manage")], "test"))
            }
        };
        var service = CreateUserAdministrationService(http, audit.Object);

        var denied = await service.UpdateUserPermissionsAsync(
            target.Id,
            _tenantId,
            new UpdateUserPermissionsDto
            {
                Overrides = [new UserPermissionOverrideInput { PermissionId = editProjects.Id, Effect = PermissionOverrideEffect.Deny }]
            },
            actor.Id.ToString());

        Assert.True(denied.IsSuccess);
        Assert.False(denied.Value!.Single(item => item.Id == editProjects.Id).IsEffective);
        Assert.Equal(1, await _context.AuditEntries.CountAsync(entry => entry.EntityId == target.Id && entry.Action == "PermissionsUpdated"));

        var forbidden = await service.UpdateUserPermissionsAsync(
            target.Id,
            _tenantId,
            new UpdateUserPermissionsDto
            {
                Overrides = [new UserPermissionOverrideInput { PermissionId = unauthorizedPermission.Id, Effect = PermissionOverrideEffect.Allow }]
            },
            actor.Id.ToString());

        Assert.True(forbidden.IsFailure);
        Assert.Equal(403, forbidden.Error?.StatusCode);

        var protectedCoreGrant = await service.UpdateUserPermissionsAsync(
            target.Id,
            _tenantId,
            new UpdateUserPermissionsDto
            {
                Overrides = [new UserPermissionOverrideInput
                {
                    PermissionId = unflaggedAdminCorePermission.Id,
                    Effect = PermissionOverrideEffect.Allow
                }]
            },
            actor.Id.ToString());

        Assert.True(protectedCoreGrant.IsFailure);
        Assert.Equal(403, protectedCoreGrant.Error?.StatusCode);
    }

    [Fact]
    public async Task Inactive_direct_overrides_cannot_be_added_and_existing_ones_are_retired_on_reconciliation()
    {
        var (actor, actorRole) = AddUserWithRole(_tenantId, "admin", protectedTenantAdmin: true);
        var (target, _) = AddUserWithRole(_tenantId, "member");
        var manageOverrides = AddPermission(_tenantId, "UserPermissions", "Manage");
        var usersRead = AddPermission(_tenantId, "Users", "View");
        var projectView = AddPermission(_tenantId, "Project", "View");
        var editProjects = AddPermission(_tenantId, "Project", "Edit");
        var inactiveProgramManage = AddPermission(_tenantId, "Program", "Manage");
        actorRole.Permissions.Add(manageOverrides);
        actorRole.Permissions.Add(usersRead);
        actorRole.Permissions.Add(projectView);
        actorRole.Permissions.Add(editProjects);
        await _context.SaveChangesAsync();

        var audit = new Mock<IAuditService>();
        audit.Setup(service => service.AppendAsync(It.IsAny<AuditEntry>(), It.IsAny<CancellationToken>()))
            .Callback<AuditEntry, CancellationToken>((entry, _) => _context.AuditEntries.Add(entry))
            .ReturnsAsync(Result.Success());
        var http = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, actor.Id.ToString()), new Claim("tenantId", _tenantId.ToString()), new Claim("permission", "UserPermissions.Manage")], "test"))
            }
        };
        var service = CreateUserAdministrationService(http, audit.Object);

        var rejected = await service.UpdateUserPermissionsAsync(
            target.Id,
            _tenantId,
            new UpdateUserPermissionsDto
            {
                Overrides = [new UserPermissionOverrideInput { PermissionId = inactiveProgramManage.Id, Effect = PermissionOverrideEffect.Allow }]
            },
            actor.Id.ToString());

        Assert.True(rejected.IsFailure);
        Assert.Equal(400, rejected.Error?.StatusCode);

        _context.UserPermissionOverrides.Add(new UserPermissionOverride
        {
            TenantId = _tenantId,
            UserId = target.Id,
            PermissionId = inactiveProgramManage.Id,
            Effect = PermissionOverrideEffect.Allow,
            CreatedBy = "legacy-seed"
        });
        await _context.SaveChangesAsync();

        var updated = await service.UpdateUserPermissionsAsync(
            target.Id,
            _tenantId,
            new UpdateUserPermissionsDto
            {
                Overrides = [new UserPermissionOverrideInput { PermissionId = editProjects.Id, Effect = PermissionOverrideEffect.Deny }]
            },
            actor.Id.ToString());

        Assert.True(updated.IsSuccess);
        var retiredOverride = await _context.UserPermissionOverrides
            .IgnoreQueryFilters()
            .SingleAsync(item => item.UserId == target.Id && item.PermissionId == inactiveProgramManage.Id);
        Assert.True(retiredOverride.IsDeleted);
        Assert.Equal(PermissionOverrideEffect.Inherit, retiredOverride.Effect);
        Assert.DoesNotContain(updated.Value!, item => item.Id == inactiveProgramManage.Id);
    }

    [Fact]
    public async Task User_role_assignments_are_audited_and_the_actor_cannot_change_their_own_roles()
    {
        var (actor, actorRole) = AddUserWithRole(_tenantId, "administrator", protectedTenantAdmin: true);
        var (target, _) = AddUserWithRole(_tenantId, "member");
        var usersRead = AddPermission(_tenantId, "Users", "View");
        var manageAccounts = AddPermission(_tenantId, "Users", "ManageAccounts");
        var assignRoles = AddPermission(_tenantId, "Users", "AssignRoles");
        var projectView = AddPermission(_tenantId, "Project", "View");
        actorRole.Permissions.Add(usersRead);
        actorRole.Permissions.Add(manageAccounts);
        actorRole.Permissions.Add(assignRoles);
        actorRole.Permissions.Add(projectView);
        var replacementRole = new Role { TenantId = _tenantId, Name = "Project Analyst", CreatedBy = "test" };
        replacementRole.AssignOrganization(_organizationId);
        replacementRole.Permissions.Add(projectView);
        _context.Roles.Add(replacementRole);
        await _context.SaveChangesAsync();

        var audit = new Mock<IAuditService>();
        audit.Setup(service => service.AppendAsync(It.IsAny<AuditEntry>(), It.IsAny<CancellationToken>()))
            .Callback<AuditEntry, CancellationToken>((entry, _) => _context.AuditEntries.Add(entry))
            .ReturnsAsync(Result.Success());
        var http = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim(ClaimTypes.NameIdentifier, actor.Id.ToString()),
                        new Claim("tenantId", _tenantId.ToString()),
                        new Claim("permission", "Users.ManageAccounts"),
                        new Claim("permission", "Users.AssignRoles")
                    ], "test"))
            }
        };
        var service = CreateUserAdministrationService(http, audit.Object);

        var updated = await service.UpdateAsync(target.Id, new UpdateUserDto
        {
            Email = target.Email,
            FirstName = target.FirstName,
            LastName = target.LastName,
            IsActive = target.IsActive,
            RoleIds = [replacementRole.Id]
        }, actor.Id.ToString());

        Assert.True(updated.IsSuccess);
        Assert.Equal(new[] { replacementRole.Name }, updated.Value!.Roles.Select(role => role.Name));
        var auditEntry = await _context.AuditEntries.SingleAsync(entry => entry.EntityId == target.Id && entry.Action == "RolesUpdated");
        Assert.Contains("member", auditEntry.OriginalValues);
        Assert.Contains(replacementRole.Name, auditEntry.NewValues);

        var (elevatedTarget, elevatedRole) = AddUserWithRole(_tenantId, "elevated-target");
        elevatedRole.Permissions.Add(AddPermission(_tenantId, "finances", "read"));
        await _context.SaveChangesAsync();
        var profileOnlyUpdate = await service.UpdateAsync(elevatedTarget.Id, new UpdateUserDto
        {
            Email = elevatedTarget.Email,
            FirstName = "Updated",
            LastName = elevatedTarget.LastName,
            IsActive = elevatedTarget.IsActive,
            RoleIds = [elevatedRole.Id]
        }, actor.Id.ToString());

        Assert.True(profileOnlyUpdate.IsSuccess);
        Assert.Equal("Updated", profileOnlyUpdate.Value!.FirstName);
        Assert.Equal(new[] { elevatedRole.Name }, profileOnlyUpdate.Value.Roles.Select(role => role.Name));

        var selfUpdate = await service.UpdateAsync(actor.Id, new UpdateUserDto
        {
            Email = actor.Email,
            FirstName = actor.FirstName,
            LastName = actor.LastName,
            IsActive = actor.IsActive,
            RoleIds = []
        }, actor.Id.ToString());

        Assert.True(selfUpdate.IsFailure);
        Assert.Equal(403, selfUpdate.Error?.StatusCode);
        Assert.Equal(1, await _context.AuditEntries.CountAsync(entry => entry.Action == "RolesUpdated"));
    }

    [Fact]
    public async Task Role_create_and_update_save_permission_sets_and_audit_changes()
    {
        var (actor, actorRole) = AddUserWithRole(_tenantId, "role-admin", protectedTenantAdmin: true);
        var roleView = AddPermission(_tenantId, "Roles", "View");
        var roleCreate = AddPermission(_tenantId, "Roles", "Create");
        var roleEditDetails = AddPermission(_tenantId, "Roles", "EditDetails");
        var roleConfigure = AddPermission(_tenantId, "Roles", "ConfigurePermissions");
        var projectView = AddPermission(_tenantId, "Project", "View");
        var projectEdit = AddPermission(_tenantId, "Project", "Edit");
        actorRole.Permissions.Add(roleView);
        actorRole.Permissions.Add(roleCreate);
        actorRole.Permissions.Add(roleEditDetails);
        actorRole.Permissions.Add(roleConfigure);
        actorRole.Permissions.Add(projectView);
        actorRole.Permissions.Add(projectEdit);
        await _context.SaveChangesAsync();

        var service = CreateRoleService(actor.Id);
        var created = await service.CreateAsync(new SaveRoleDto
        {
            Name = "Project Reader",
            OrganizationId = _organizationId,
            Description = "Read project data",
            Category = RoleCategories.Management,
            PermissionIds = [projectView.Id]
        }, actor.Id, _tenantId);

        Assert.True(created.IsSuccess);
        Assert.Equal(_tenantId, (await _context.Roles.SingleAsync(role => role.Id == created.Value!.Id)).TenantId);
        Assert.Equal("Project Reader", created.Value!.Name);
        Assert.Equal(projectView.Id, Assert.Single(created.Value.Permissions).Id);

        var updated = await service.UpdateAsync(created.Value.Id, new SaveRoleDto
        {
            Name = "Project Reader",
            Description = "Read and update project data",
            Category = RoleCategories.Management,
            PermissionIds = [projectView.Id, projectEdit.Id]
        }, actor.Id, _tenantId);

        Assert.True(updated.IsSuccess, updated.Error?.Message);
        Assert.Equal(2, updated.Value!.PermissionCount);
        Assert.Equal("Read and update project data", updated.Value.Description);
        Assert.Equal(2, await _context.AuditEntries.CountAsync(entry => entry.EntityType == "Role" && entry.EntityId == created.Value.Id));
    }

    [Fact]
    public async Task Role_management_is_tenant_scoped_and_keeps_administration_grants_actor_scoped()
    {
        var (actor, actorRole) = AddUserWithRole(_tenantId, "role-admin", protectedTenantAdmin: true);
        var roleView = AddPermission(_tenantId, "Roles", "View");
        var roleCreate = AddPermission(_tenantId, "Roles", "Create");
        var roleConfigure = AddPermission(_tenantId, "Roles", "ConfigurePermissions");
        var projectView = AddPermission(_tenantId, "Project", "View");
        var settingsWrite = AddPermission(_tenantId, "Settings", "ManageTenantConfiguration");
        actorRole.Permissions.Add(roleView);
        actorRole.Permissions.Add(roleCreate);
        actorRole.Permissions.Add(roleConfigure);
        actorRole.Permissions.Add(projectView);
        var foreignTenantId = Guid.NewGuid();
        var foreignPermission = AddPermission(foreignTenantId, "Project", "Delete");
        var foreignRole = new Role { TenantId = foreignTenantId, Name = "foreign-role", CreatedBy = "test" };
        foreignRole.Permissions.Add(foreignPermission);
        _context.Roles.Add(foreignRole);
        await _context.SaveChangesAsync();

        var service = CreateRoleService(actor.Id);
        var catalog = await service.GetAssignablePermissionsAsync(actor.Id, _tenantId);
        Assert.True(catalog.IsSuccess);
        Assert.Contains(catalog.Value!, item => item.Id == projectView.Id);
        Assert.Contains(catalog.Value!, item => item.Id == settingsWrite.Id && item.CanDelegate == false);
        Assert.Contains(catalog.Value!, item => item.Id == settingsWrite.Id && item.CanDelegateToCustomRole == false);
        Assert.DoesNotContain(catalog.Value!, item => item.Scope is "Platform" or "CrossTenant" or "SystemOwner");
        Assert.DoesNotContain(catalog.Value!, item => item.Id == foreignPermission.Id);

        var crossTenantRole = await service.GetByIdAsync(foreignRole.Id, actor.Id, _tenantId);
        Assert.True(crossTenantRole.IsFailure);
        Assert.Equal(404, crossTenantRole.Error?.StatusCode);

        var escalation = await service.CreateAsync(new SaveRoleDto
        {
            Name = "Escalated role",
            OrganizationId = _organizationId,
            Category = RoleCategories.Management,
            PermissionIds = [settingsWrite.Id]
        }, actor.Id, _tenantId);
        Assert.True(escalation.IsFailure);
        Assert.Equal(403, escalation.Error?.StatusCode);

        var uncataloguedGlobalPermission = AddPermission(_tenantId, "Platform", "Manage");
        await _context.SaveChangesAsync();
        var uncataloguedGlobalGrant = await service.CreateAsync(new SaveRoleDto
        {
            Name = "Uncatalogued global grant",
            OrganizationId = _organizationId,
            PermissionIds = [uncataloguedGlobalPermission.Id]
        }, actor.Id, _tenantId);
        Assert.True(uncataloguedGlobalGrant.IsFailure);
        Assert.Equal(400, uncataloguedGlobalGrant.Error?.StatusCode);

        var foreignGrant = await service.CreateAsync(new SaveRoleDto
        {
            Name = "Foreign permission role",
            OrganizationId = _organizationId,
            PermissionIds = [foreignPermission.Id]
        }, actor.Id, _tenantId);
        Assert.True(foreignGrant.IsFailure);
        Assert.Equal(400, foreignGrant.Error?.StatusCode);
    }

    [Fact]
    public async Task Role_administrator_can_edit_custom_role_with_ordinary_permissions_outside_actor_access()
    {
        var (actor, actorRole) = AddUserWithRole(_tenantId, "tenant-admin", protectedTenantAdmin: true);
        var roleView = AddPermission(_tenantId, "Roles", "View");
        var roleConfigure = AddPermission(_tenantId, "Roles", "ConfigurePermissions");
        var projectView = AddPermission(_tenantId, "Project", "View");
        var projectEdit = AddPermission(_tenantId, "Project", "Edit");
        actorRole.Permissions.Add(roleView);
        actorRole.Permissions.Add(roleConfigure);
        actorRole.Permissions.Add(projectView);
        actorRole.Permissions.Add(projectEdit);

        var (target, targetRole) = AddUserWithRole(_tenantId, "permission-target");
        targetRole.Category = RoleCategories.Management;
        targetRole.Permissions.Add(projectView);
        await _context.SaveChangesAsync();

        var service = CreateRoleService(actor.Id);
        var manageable = await service.GetManageableRolesAsync(actor.Id, _tenantId);
        Assert.True(manageable.IsSuccess);
        Assert.Contains(manageable.Value!, role => role.Id == targetRole.Id);

        var catalog = await service.GetAssignablePermissionsAsync(actor.Id, _tenantId);
        Assert.True(catalog.IsSuccess);
        Assert.Contains(catalog.Value!, permission => permission.Id == projectEdit.Id);

        var saved = await service.UpdateAsync(targetRole.Id, new SaveRoleDto
        {
            Name = targetRole.Name,
            Category = targetRole.Category,
            PermissionIds = [projectView.Id, projectEdit.Id]
        }, actor.Id, _tenantId);

        Assert.True(saved.IsSuccess);
        var reloaded = await service.GetByIdAsync(targetRole.Id, actor.Id, _tenantId);
        Assert.True(reloaded.IsSuccess);
        Assert.Contains(reloaded.Value!.Permissions, permission => permission.Id == projectEdit.Id);
        Assert.Contains(await new UserEffectivePermissionService(_context).GetEffectivePermissionsAsync(target.Id, _tenantId),
            permission => permission == "Project.Edit");
        Assert.Single(await _context.AuditEntries.Where(entry => entry.EntityId == targetRole.Id && entry.Action == "Updated").ToListAsync());
    }

    [Fact]
    public async Task Authorized_role_creator_can_create_custom_role_with_ordinary_scoped_permissions()
    {
        var (actor, actorRole) = AddUserWithRole(_tenantId, "tenant-role-admin", protectedTenantAdmin: true);
        var roleView = AddPermission(_tenantId, "Roles", "View");
        var roleCreate = AddPermission(_tenantId, "Roles", "Create");
        var roleConfigure = AddPermission(_tenantId, "Roles", "ConfigurePermissions");
        var projectView = AddPermission(_tenantId, "Project", "View");
        var projectEdit = AddPermission(_tenantId, "Project", "Edit");
        var projectDelete = AddPermission(_tenantId, "Project", "Delete");
        actorRole.Permissions.Add(roleView);
        actorRole.Permissions.Add(roleCreate);
        actorRole.Permissions.Add(roleConfigure);
        actorRole.Permissions.Add(projectView);
        actorRole.Permissions.Add(projectEdit);
        await _context.SaveChangesAsync();

        var service = CreateRoleService(actor.Id);
        var catalog = await service.GetAssignablePermissionsAsync(actor.Id, _tenantId);
        Assert.True(catalog.IsSuccess, catalog.Error?.Message);
        Assert.True(catalog.Value!.Single(permission => permission.Id == projectView.Id).CanDelegate == true);
        Assert.True(catalog.Value.Single(permission => permission.Id == projectEdit.Id).CanDelegate == true);
        Assert.False(catalog.Value.Single(permission => permission.Id == projectDelete.Id).CanDelegate == true);
        Assert.True(catalog.Value.Single(permission => permission.Id == projectDelete.Id).CanDelegateToCustomRole == true);

        var created = await service.CreateAsync(new SaveRoleDto
        {
            Name = "Project Editor",
            OrganizationId = _organizationId,
            Category = RoleCategories.Management,
            PermissionIds = [projectView.Id, projectEdit.Id]
        }, actor.Id, _tenantId);

        Assert.True(created.IsSuccess, created.Error?.Message);
        Assert.Contains(created.Value!.Permissions, permission => permission.Id == projectView.Id);
        Assert.Contains(created.Value.Permissions, permission => permission.Id == projectEdit.Id);
        var persistedRole = await _context.Roles.Include(role => role.Permissions).SingleAsync(role => role.Id == created.Value.Id);
        Assert.Contains(persistedRole.Permissions, permission => permission.Id == projectView.Id);
        Assert.Contains(persistedRole.Permissions, permission => permission.Id == projectEdit.Id);

        var expandedRole = await service.CreateAsync(new SaveRoleDto
        {
            Name = "Project Deleter",
            OrganizationId = _organizationId,
            Category = RoleCategories.Management,
            PermissionIds = [projectView.Id, projectDelete.Id]
        }, actor.Id, _tenantId);
        Assert.True(expandedRole.IsSuccess, expandedRole.Error?.Message);
        Assert.Contains(expandedRole.Value!.Permissions, permission => permission.Id == projectDelete.Id);
    }

    [Fact]
    public async Task Budget_revision_actions_can_be_granted_saved_reloaded_and_revoked_across_role_categories()
    {
        var (actor, actorRole) = AddUserWithRole(_tenantId, "budget-revision-role-admin", protectedTenantAdmin: true);
        actorRole.Permissions.Add(AddPermission(_tenantId, "Roles", "View"));
        actorRole.Permissions.Add(AddPermission(_tenantId, "Roles", "Create"));
        actorRole.Permissions.Add(AddPermission(_tenantId, "Roles", "EditDetails"));
        actorRole.Permissions.Add(AddPermission(_tenantId, "Roles", "ConfigurePermissions"));
        actorRole.Permissions.Add(AddPermission(_tenantId, "Users", "ManageAccounts"));
        actorRole.Permissions.Add(AddPermission(_tenantId, "Users", "AssignRoles"));

        var budgetView = AddPermission(_tenantId, "BudgetRevision", "View");
        var budgetApprove = AddPermission(_tenantId, "BudgetRevision", "Approve");
        var budgetCreate = AddPermission(_tenantId, "BudgetRevision", "Create");
        var (target, targetRole) = AddUserWithRole(_tenantId, "budget-revision-recipient");
        targetRole.Category = RoleCategories.Operation;
        targetRole.Permissions.Add(budgetView);
        _context.UserPermissionOverrides.Add(new UserPermissionOverride
        {
            TenantId = _tenantId,
            UserId = target.Id,
            PermissionId = budgetApprove.Id,
            Effect = PermissionOverrideEffect.Deny,
            CreatedBy = "test"
        });
        await _context.SaveChangesAsync();

        var service = CreateRoleService(actor.Id);
        var catalog = await service.GetAssignablePermissionsAsync(actor.Id, _tenantId);
        Assert.True(catalog.IsSuccess, catalog.Error?.Message);
        foreach (var key in new[] { "BudgetRevision.Approve", "BudgetRevision.Create" })
        {
            var item = catalog.Value!.Single(permission => permission.Name == key);
            Assert.Equal(PermissionScope.Project.ToString(), item.Scope);
            Assert.False(item.CanDelegate);
            Assert.True(item.CanDelegateToCustomRole);
        }

        var missingDependency = await service.CreateAsync(new SaveRoleDto
        {
            Name = "Budget Revision Without View",
            OrganizationId = _organizationId,
            PermissionIds = [budgetApprove.Id]
        }, actor.Id, _tenantId);
        Assert.True(missingDependency.IsFailure);
        Assert.Equal(400, missingDependency.Error?.StatusCode);

        var saved = await service.UpdateAsync(targetRole.Id, new SaveRoleDto
        {
            Name = targetRole.Name,
            Category = RoleCategories.Business,
            PermissionIds = [budgetView.Id, budgetApprove.Id, budgetCreate.Id]
        }, actor.Id, _tenantId);
        Assert.True(saved.IsSuccess, saved.Error?.Message);
        Assert.Equal(RoleCategories.Business, saved.Value!.Category);
        Assert.Contains(saved.Value.Permissions, permission => permission.Name == "BudgetRevision.Approve");
        Assert.Contains(saved.Value.Permissions, permission => permission.Name == "BudgetRevision.Create");

        var reloaded = await service.GetByIdAsync(targetRole.Id, actor.Id, _tenantId);
        Assert.True(reloaded.IsSuccess, reloaded.Error?.Message);
        Assert.Contains(reloaded.Value!.Permissions, permission => permission.Name == "BudgetRevision.Create");
        var effective = await new UserEffectivePermissionService(_context).GetEffectivePermissionsForOrganizationAsync(target.Id, _tenantId, _organizationId);
        Assert.Contains("BudgetRevision.Create", effective);
        Assert.DoesNotContain("BudgetRevision.Approve", effective);

        var revoked = await service.UpdateAsync(targetRole.Id, new SaveRoleDto
        {
            Name = targetRole.Name,
            Category = RoleCategories.Business,
            PermissionIds = [budgetView.Id, budgetApprove.Id]
        }, actor.Id, _tenantId);
        Assert.True(revoked.IsSuccess, revoked.Error?.Message);
        Assert.DoesNotContain(revoked.Value!.Permissions, permission => permission.Name == "BudgetRevision.Create");
        effective = await new UserEffectivePermissionService(_context).GetEffectivePermissionsForOrganizationAsync(target.Id, _tenantId, _organizationId);
        Assert.DoesNotContain("BudgetRevision.Create", effective);
        Assert.DoesNotContain("BudgetRevision.Approve", effective);

        var additionalUser = new User
        {
            TenantId = _tenantId,
            FirstName = "Additional",
            LastName = "Recipient",
            Email = $"additional-recipient-{Guid.NewGuid():N}@test.local",
            CreatedBy = "test"
        };
        _context.Users.Add(additionalUser);
        await _context.SaveChangesAsync();
        var roleAssignment = await CreateUserAdministrationService(actor.Id).UpdateAsync(additionalUser.Id, new UpdateUserDto
        {
            Email = additionalUser.Email,
            FirstName = additionalUser.FirstName,
            LastName = additionalUser.LastName,
            IsActive = additionalUser.IsActive,
            RoleIds = [targetRole.Id]
        }, actor.Id.ToString("D"));
        Assert.True(roleAssignment.IsFailure);
        Assert.Equal(403, roleAssignment.Error?.StatusCode);
    }

    [Fact]
    public async Task Custom_role_permission_edit_cannot_escalate_actor_through_a_role_they_hold()
    {
        var (actor, actorRole) = AddUserWithRole(_tenantId, "self-escalation-admin");
        actorRole.Permissions.Add(AddPermission(_tenantId, "Roles", "View"));
        var roleConfigure = AddPermission(_tenantId, "Roles", "ConfigurePermissions");
        actorRole.Permissions.Add(roleConfigure);
        var budgetView = AddPermission(_tenantId, "BudgetRevision", "View");
        var budgetCreate = AddPermission(_tenantId, "BudgetRevision", "Create");
        actorRole.Permissions.Add(budgetView);
        await _context.SaveChangesAsync();

        var result = await CreateRoleService(actor.Id).UpdateAsync(actorRole.Id, new SaveRoleDto
        {
            Name = actorRole.Name,
            Category = actorRole.Category,
            PermissionIds = [
                actorRole.Permissions.Single(permission => permission.Resource == "Roles" && permission.Action == "View").Id,
                roleConfigure.Id,
                budgetView.Id,
                budgetCreate.Id
            ]
        }, actor.Id, _tenantId);

        Assert.True(result.IsFailure);
        Assert.Equal(403, result.Error?.StatusCode);
        Assert.DoesNotContain(await _context.Roles.Include(role => role.Permissions)
            .Where(role => role.Id == actorRole.Id)
            .SelectMany(role => role.Permissions)
            .ToListAsync(), permission => permission.Id == budgetCreate.Id);
    }

    [Fact]
    public async Task Tenant_administrator_scope_does_not_allow_role_creation_details_deletion_or_cross_tenant_access()
    {
        var (actor, actorRole) = AddUserWithRole(_tenantId, "tenant-admin");
        actorRole.Permissions.Add(AddPermission(_tenantId, "Settings", "View"));
        actorRole.Permissions.Add(AddPermission(_tenantId, "TenantAdministration", "ManageScope"));
        actorRole.Permissions.Add(AddPermission(_tenantId, "Roles", "View"));
        var projectView = AddPermission(_tenantId, "Project", "View");
        actorRole.Permissions.Add(projectView);
        var role = new Role { TenantId = _tenantId, Name = "delegated-role", CreatedBy = "test" };
        role.Permissions.Add(projectView);
        _context.Roles.Add(role);
        var foreignTenantId = Guid.NewGuid();
        var foreignRole = new Role { TenantId = foreignTenantId, Name = "foreign-role", CreatedBy = "test" };
        _context.Roles.Add(foreignRole);
        await _context.SaveChangesAsync();

        var service = CreateRoleService(actor.Id);
        var create = await service.CreateAsync(new SaveRoleDto { Name = "new-role", OrganizationId = _organizationId, PermissionIds = [] }, actor.Id, _tenantId);
        Assert.True(create.IsFailure);
        Assert.Equal(403, create.Error?.StatusCode);

        var editDetails = await service.UpdateAsync(role.Id, new SaveRoleDto
        {
            Name = "renamed-role",
            PermissionIds = [projectView.Id]
        }, actor.Id, _tenantId);
        Assert.True(editDetails.IsFailure);
        Assert.Equal(403, editDetails.Error?.StatusCode);

        var delete = await service.DeleteAsync(role.Id, actor.Id, _tenantId);
        Assert.True(delete.IsFailure);
        Assert.Equal(403, delete.Error?.StatusCode);

        var crossTenant = await service.GetByIdAsync(foreignRole.Id, actor.Id, _tenantId);
        Assert.True(crossTenant.IsFailure);
        Assert.Equal(404, crossTenant.Error?.StatusCode);
        Assert.False((await _context.Roles.SingleAsync(item => item.Id == foreignRole.Id)).IsDeleted);
    }

    [Fact]
    public async Task Administration_module_permissions_still_require_actor_effective_access()
    {
        var (actor, actorRole) = AddUserWithRole(_tenantId, "tenant-admin");
        actorRole.Permissions.Add(AddPermission(_tenantId, "Settings", "View"));
        actorRole.Permissions.Add(AddPermission(_tenantId, "TenantAdministration", "ManageScope"));
        actorRole.Permissions.Add(AddPermission(_tenantId, "Roles", "View"));
        actorRole.Permissions.Add(AddPermission(_tenantId, "Roles", "ConfigurePermissions"));
        var projectView = AddPermission(_tenantId, "Project", "View");
        actorRole.Permissions.Add(projectView);
        var settingsWrite = AddPermission(_tenantId, "Settings", "ManageTenantConfiguration");
        var targetRole = new Role { TenantId = _tenantId, Name = "delegated-role", CreatedBy = "test" };
        targetRole.Permissions.Add(projectView);
        _context.Roles.Add(targetRole);
        await _context.SaveChangesAsync();

        var result = await CreateRoleService(actor.Id).UpdateAsync(targetRole.Id, new SaveRoleDto
        {
            Name = targetRole.Name,
            PermissionIds = [projectView.Id, settingsWrite.Id]
        }, actor.Id, _tenantId);

        Assert.True(result.IsFailure);
        Assert.Equal(403, result.Error?.StatusCode);
        Assert.DoesNotContain(await _context.Roles.Include(role => role.Permissions)
            .Where(role => role.Id == targetRole.Id)
            .SelectMany(role => role.Permissions)
            .ToListAsync(), permission => permission.Id == settingsWrite.Id);
    }

    [Fact]
    public async Task Direct_deny_on_role_configuration_blocks_permission_changes()
    {
        var (actor, actorRole) = AddUserWithRole(_tenantId, "tenant-admin");
        actorRole.Permissions.Add(AddPermission(_tenantId, "Roles", "View"));
        var roleConfigure = AddPermission(_tenantId, "Roles", "ConfigurePermissions");
        actorRole.Permissions.Add(roleConfigure);
        _context.UserPermissionOverrides.Add(new UserPermissionOverride
        {
            TenantId = _tenantId,
            UserId = actor.Id,
            PermissionId = roleConfigure.Id,
            Effect = PermissionOverrideEffect.Deny,
            CreatedBy = "test"
        });
        await _context.SaveChangesAsync();

        var service = CreateRoleService(actor.Id);
        Assert.DoesNotContain("Roles.ConfigurePermissions",
            await new UserEffectivePermissionService(_context).GetEffectivePermissionsAsync(actor.Id, _tenantId));
        var roles = await service.GetManageableRolesAsync(actor.Id, _tenantId);
        var catalog = await service.GetAssignablePermissionsAsync(actor.Id, _tenantId);

        Assert.True(roles.IsSuccess);
        Assert.True(catalog.IsFailure);
        Assert.Equal(403, catalog.Error?.StatusCode);
    }

    [Fact]
    public async Task Read_only_role_viewer_can_read_roles_but_cannot_change_permission_grants()
    {
        var (actor, actorRole) = AddUserWithRole(_tenantId, "role-viewer");
        actorRole.Permissions.Add(AddPermission(_tenantId, "Roles", "View"));
        var projectView = AddPermission(_tenantId, "Project", "View");
        var projectEdit = AddPermission(_tenantId, "Project", "Edit");
        var targetRole = new Role { TenantId = _tenantId, Name = "viewer-target", CreatedBy = "test" };
        targetRole.AssignOrganization(_organizationId);
        targetRole.Permissions.Add(projectView);
        _context.Roles.Add(targetRole);
        await _context.SaveChangesAsync();

        var service = CreateRoleService(actor.Id);
        var manageable = await service.GetManageableRolesAsync(actor.Id, _tenantId);
        Assert.True(manageable.IsSuccess);
        Assert.Empty(manageable.Value!);
        Assert.True((await service.GetByIdAsync(targetRole.Id, actor.Id, _tenantId)).IsFailure);
        var update = await service.UpdateAsync(targetRole.Id, new SaveRoleDto
        {
            Name = targetRole.Name,
            PermissionIds = [projectView.Id, projectEdit.Id]
        }, actor.Id, _tenantId);

        Assert.True(update.IsFailure);
        Assert.Equal(403, update.Error?.StatusCode);
        Assert.DoesNotContain(await _context.Roles.Include(role => role.Permissions)
            .Where(role => role.Id == targetRole.Id)
            .SelectMany(role => role.Permissions)
            .ToListAsync(), permission => permission.Id == projectEdit.Id);
    }

    [Fact]
    public async Task Role_details_editor_without_configure_permission_keeps_effective_access_visibility_ceiling()
    {
        var (actor, actorRole) = AddUserWithRole(_tenantId, "role-details-editor");
        actorRole.Permissions.Add(AddPermission(_tenantId, "Roles", "View"));
        actorRole.Permissions.Add(AddPermission(_tenantId, "Roles", "EditDetails"));
        var projectView = AddPermission(_tenantId, "Project", "View");
        var projectEdit = AddPermission(_tenantId, "Project", "Edit");
        actorRole.Permissions.Add(projectView);

        var targetRole = new Role { TenantId = _tenantId, Name = "out-of-scope-custom-role", CreatedBy = "test" };
        targetRole.Permissions.Add(projectView);
        targetRole.Permissions.Add(projectEdit);
        _context.Roles.Add(targetRole);
        await _context.SaveChangesAsync();

        var service = CreateRoleService(actor.Id);
        var manageable = await service.GetManageableRolesAsync(actor.Id, _tenantId);
        Assert.True(manageable.IsSuccess);
        Assert.DoesNotContain(manageable.Value!, role => role.Id == targetRole.Id);

        var detail = await service.GetByIdAsync(targetRole.Id, actor.Id, _tenantId);
        Assert.True(detail.IsFailure);
        Assert.Equal(403, detail.Error?.StatusCode);

        var update = await service.UpdateAsync(targetRole.Id, new SaveRoleDto
        {
            Name = "renamed-out-of-scope-role",
            PermissionIds = [projectView.Id, projectEdit.Id]
        }, actor.Id, _tenantId);
        Assert.True(update.IsFailure);
        Assert.Equal(403, update.Error?.StatusCode);
    }

    [Fact]
    public async Task Role_service_rejects_forged_actor_and_tenant_arguments()
    {
        var (actor, actorRole) = AddUserWithRole(_tenantId, "bound-role-admin");
        actorRole.Permissions.Add(AddPermission(_tenantId, "Roles", "View"));
        actorRole.Permissions.Add(AddPermission(_tenantId, "Roles", "Create"));
        await _context.SaveChangesAsync();

        var service = CreateRoleService(actor.Id);
        var forgedActor = await service.CreateAsync(new SaveRoleDto { Name = "forged actor", PermissionIds = [] }, Guid.NewGuid(), _tenantId);
        var forgedTenant = await service.CreateAsync(new SaveRoleDto { Name = "forged tenant", PermissionIds = [] }, actor.Id, Guid.NewGuid());

        Assert.True(forgedActor.IsFailure);
        Assert.Equal(403, forgedActor.Error?.StatusCode);
        Assert.True(forgedTenant.IsFailure);
        Assert.Equal(403, forgedTenant.Error?.StatusCode);
        Assert.DoesNotContain(await _context.Roles.IgnoreQueryFilters().ToListAsync(), role => role.Name.StartsWith("forged ", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Role_detail_excludes_permissions_from_other_tenants_for_protected_admin()
    {
        var (actor, actorRole) = AddUserWithRole(_tenantId, "scoped-role-viewer", protectedTenantAdmin: true);
        actorRole.Permissions.Add(AddPermission(_tenantId, "Roles", "View"));

        var projectView = AddPermission(_tenantId, "Project", "View");
        var foreignPermission = AddPermission(Guid.NewGuid(), "Project", "Delete");
        var targetRole = new Role { TenantId = _tenantId, Name = "scoped-target", CreatedBy = "test" };
        targetRole.AssignOrganization(_organizationId);
        targetRole.Permissions.Add(projectView);
        targetRole.Permissions.Add(foreignPermission);
        _context.Roles.Add(targetRole);
        await _context.SaveChangesAsync();

        var detail = await CreateRoleService(actor.Id).GetByIdAsync(targetRole.Id, actor.Id, _tenantId);

        Assert.True(detail.IsSuccess);
        Assert.Equal(1, detail.Value!.PermissionCount);
        Assert.Contains(detail.Value.Permissions, permission => permission.Id == projectView.Id);
        Assert.DoesNotContain(detail.Value.Permissions, permission => permission.Id == foreignPermission.Id);
    }

    [Fact]
    public async Task System_roles_cannot_be_renamed_or_deleted_and_assigned_custom_roles_cannot_be_deleted()
    {
        var (actor, actorRole) = AddUserWithRole(_tenantId, "role-admin", protectedTenantAdmin: true);
        var roleView = AddPermission(_tenantId, "Roles", "View");
        var roleDelete = AddPermission(_tenantId, "Roles", "Delete");
        var roleConfigure = AddPermission(_tenantId, "Roles", "ConfigurePermissions");
        var projectView = AddPermission(_tenantId, "Project", "View");
        var projectCreate = AddPermission(_tenantId, "Project", "Create");
        var projectEdit = AddPermission(_tenantId, "Project", "Edit");
        actorRole.Permissions.Add(roleView);
        actorRole.Permissions.Add(roleDelete);
        actorRole.Permissions.Add(roleConfigure);
        actorRole.Permissions.Add(projectView);
        actorRole.Permissions.Add(projectCreate);
        var systemRole = new Role { TenantId = _tenantId, Name = "system-role", IsSystem = true, CreatedBy = "test" };
        systemRole.Permissions.Add(projectView);
        _context.Roles.Add(systemRole);
        var (assignedUser, assignedRole) = AddUserWithRole(_tenantId, "assigned");
        assignedRole.Name = "custom-role";
        assignedRole.Permissions.Add(projectView);
        await _context.SaveChangesAsync();

        var service = CreateRoleService(actor.Id);
        var catalog = await service.GetAssignablePermissionsAsync(actor.Id, _tenantId);
        Assert.True(catalog.IsSuccess, catalog.Error?.Message);
        Assert.False(catalog.Value!.Single(item => item.Id == projectEdit.Id).CanDelegate == true);
        Assert.True(catalog.Value.Single(item => item.Id == projectEdit.Id).CanDelegateToCustomRole == true);

        var unchangedSystemRolePermissions = await service.UpdateAsync(systemRole.Id, new SaveRoleDto
        {
            Name = systemRole.Name,
            PermissionIds = [projectView.Id]
        }, actor.Id, _tenantId);
        Assert.True(unchangedSystemRolePermissions.IsSuccess, unchangedSystemRolePermissions.Error?.Message);

        var actorOwnedSystemPermissionGrant = await service.UpdateAsync(systemRole.Id, new SaveRoleDto
        {
            Name = systemRole.Name,
            PermissionIds = [projectView.Id, projectCreate.Id]
        }, actor.Id, _tenantId);
        Assert.True(actorOwnedSystemPermissionGrant.IsSuccess, actorOwnedSystemPermissionGrant.Error?.Message);

        var systemRolePermissionGrant = await service.UpdateAsync(systemRole.Id, new SaveRoleDto
        {
            Name = systemRole.Name,
            PermissionIds = [projectView.Id, projectCreate.Id, projectEdit.Id]
        }, actor.Id, _tenantId);
        Assert.True(systemRolePermissionGrant.IsFailure);
        Assert.Equal(403, systemRolePermissionGrant.Error?.StatusCode);

        var renameSystemRole = await service.UpdateAsync(systemRole.Id, new SaveRoleDto
        {
            Name = "renamed-system-role",
            PermissionIds = [projectView.Id]
        }, actor.Id, _tenantId);
        Assert.True(renameSystemRole.IsFailure);
        Assert.Equal(400, renameSystemRole.Error?.StatusCode);

        var deleteSystemRole = await service.DeleteAsync(systemRole.Id, actor.Id, _tenantId);
        Assert.True(deleteSystemRole.IsFailure);
        Assert.Equal(403, deleteSystemRole.Error?.StatusCode);

        var deleteAssignedRole = await service.DeleteAsync(assignedRole.Id, actor.Id, _tenantId);
        Assert.True(deleteAssignedRole.IsFailure);
        Assert.Equal(409, deleteAssignedRole.Error?.StatusCode);
        Assert.False((await _context.Roles.SingleAsync(role => role.Id == assignedRole.Id)).IsDeleted);
        Assert.NotEqual(Guid.Empty, assignedUser.Id);
    }

    [Fact]
    public async Task Role_update_cannot_remove_the_actor_last_role_management_permission()
    {
        var (actor, actorRole) = AddUserWithRole(_tenantId, "role-admin", protectedTenantAdmin: true);
        var roleView = AddPermission(_tenantId, "Roles", "View");
        var roleCreate = AddPermission(_tenantId, "Roles", "Create");
        var roleConfigure = AddPermission(_tenantId, "Roles", "ConfigurePermissions");
        actorRole.Permissions.Add(roleView);
        actorRole.Permissions.Add(roleCreate);
        actorRole.Permissions.Add(roleConfigure);
        await _context.SaveChangesAsync();

        var result = await CreateRoleService(actor.Id).UpdateAsync(actorRole.Id, new SaveRoleDto
        {
            Name = actorRole.Name,
            PermissionIds = []
        }, actor.Id, _tenantId);

        Assert.True(result.IsFailure);
        Assert.Equal(409, result.Error?.StatusCode);
        Assert.Contains((await _context.Roles.Include(role => role.Permissions).SingleAsync(role => role.Id == actorRole.Id)).Permissions,
            permission => permission.Name == "Roles.ConfigurePermissions");
    }

    [Fact]
    public async Task Protected_system_admin_role_allows_removal_of_non_designated_permissions()
    {
        var (actor, adminRole) = AddUserWithRole(_tenantId, "admin", protectedTenantAdmin: true);
        adminRole.CreatedBy = "System";
        foreach (var key in new[]
        {
            "Roles.View", "Roles.Create", "Roles.ConfigurePermissions",
            "Users.AssignRoles", "Users.SetStatus", "DocumentCategory.View",
            "DocumentCategory.ActivateDeactivate"
        })
        {
            var definition = PermissionCatalog.ByKey[key];
            adminRole.Permissions.Add(AddPermission(_tenantId, definition.Resource, definition.Action));
        }
        await _context.SaveChangesAsync();

        var omittedPermission = adminRole.Permissions.Single(permission => permission.Name == "DocumentCategory.ActivateDeactivate");
        var requestedIds = adminRole.Permissions
            .Where(permission => permission.Id != omittedPermission.Id)
            .Select(permission => permission.Id)
            .ToList();

        var updated = await CreateRoleService(actor.Id).UpdateAsync(adminRole.Id, new SaveRoleDto
        {
            Name = adminRole.Name,
            PermissionIds = requestedIds
        }, actor.Id, _tenantId);

        Assert.True(updated.IsSuccess, updated.Error?.Message);
        Assert.DoesNotContain(updated.Value!.Permissions, permission => permission.Name == omittedPermission.Name);
        Assert.Contains(updated.Value.Permissions, permission => permission.Name == "Roles.ConfigurePermissions");
        Assert.Empty(await _context.AuditEntries.Where(entry => entry.EntityId == adminRole.Id
            && entry.Action == "ProtectedAdminBaselineMutationRejected").ToListAsync());
    }

    [Fact]
    public async Task Unassigned_custom_role_is_soft_deleted_and_audited()
    {
        var (actor, actorRole) = AddUserWithRole(_tenantId, "role-admin", protectedTenantAdmin: true);
        var roleView = AddPermission(_tenantId, "Roles", "View");
        var roleDelete = AddPermission(_tenantId, "Roles", "Delete");
        var projectView = AddPermission(_tenantId, "Project", "View");
        actorRole.Permissions.Add(roleView);
        actorRole.Permissions.Add(roleDelete);
        actorRole.Permissions.Add(projectView);
        var role = new Role { TenantId = _tenantId, Name = "unused-role", CreatedBy = "test" };
        role.Permissions.Add(projectView);
        _context.Roles.Add(role);
        await _context.SaveChangesAsync();

        var result = await CreateRoleService(actor.Id).DeleteAsync(role.Id, actor.Id, _tenantId);

        Assert.True(result.IsSuccess);
        var deletedRole = await _context.Roles.IgnoreQueryFilters().SingleAsync(item => item.Id == role.Id);
        Assert.True(deletedRole.IsDeleted);
        var audit = await _context.AuditEntries.SingleAsync(entry => entry.EntityId == role.Id && entry.Action == "Deleted");
        Assert.Contains("Project.View", audit.OriginalValues);
    }

    [Fact]
    public async Task Role_api_rejects_new_inactive_grants_and_removes_existing_legacy_grants_on_reconciliation()
    {
        var (actor, actorRole) = AddUserWithRole(_tenantId, "role-admin", protectedTenantAdmin: true);
        var roleView = AddPermission(_tenantId, "Roles", "View");
        var roleCreate = AddPermission(_tenantId, "Roles", "Create");
        var roleConfigure = AddPermission(_tenantId, "Roles", "ConfigurePermissions");
        var inactiveProgramManage = AddPermission(_tenantId, "Program", "Manage");
        actorRole.Permissions.Add(roleView);
        actorRole.Permissions.Add(roleCreate);
        actorRole.Permissions.Add(roleConfigure);
        await _context.SaveChangesAsync();

        var service = CreateRoleService(actor.Id);
        var rejected = await service.CreateAsync(new SaveRoleDto
        {
            Name = "New legacy program role",
            OrganizationId = _organizationId,
            Category = RoleCategories.Management,
            PermissionIds = [inactiveProgramManage.Id]
        }, actor.Id, _tenantId);

        Assert.True(rejected.IsFailure);
        Assert.Equal(400, rejected.Error?.StatusCode);

        var existingRole = new Role { TenantId = _tenantId, Name = "Existing legacy role", CreatedBy = "legacy-seed" };
        existingRole.AssignOrganization(_organizationId);
        existingRole.Permissions.Add(inactiveProgramManage);
        _context.Roles.Add(existingRole);
        await _context.SaveChangesAsync();

        var reconciled = await service.UpdateAsync(existingRole.Id, new SaveRoleDto
        {
            Name = existingRole.Name,
            PermissionIds = []
        }, actor.Id, _tenantId);

        Assert.True(reconciled.IsSuccess);
        Assert.DoesNotContain(reconciled.Value!.Permissions, item => item.Id == inactiveProgramManage.Id);
        Assert.DoesNotContain(await _context.Roles.Include(role => role.Permissions)
            .Where(role => role.Id == existingRole.Id)
            .SelectMany(role => role.Permissions)
            .ToListAsync(), permission => permission.Id == inactiveProgramManage.Id && !permission.IsDeleted);
    }

    [Fact]
    public async Task Authorized_role_admin_can_delegate_and_revoke_project_settings_without_bypassing_user_deny()
    {
        var (actor, actorRole) = AddUserWithRole(_tenantId, "delegation-admin", protectedTenantAdmin: true);
        var roleView = AddPermission(_tenantId, "Roles", "View");
        var roleCreate = AddPermission(_tenantId, "Roles", "Create");
        var roleConfigure = AddPermission(_tenantId, "Roles", "ConfigurePermissions");
        var tenantSettingsView = AddPermission(_tenantId, "Settings", "View");
        var tenantManageScope = AddPermission(_tenantId, "TenantAdministration", "ManageScope");
        var resourceView = AddPermission(_tenantId, "Resource", "View");
        var projectSettingsView = AddPermission(_tenantId, "ProjectSettings", "View");
        var projectSettingsEdit = AddPermission(_tenantId, "ProjectSettings", "Edit");
        var financialFoundationView = AddPermission(_tenantId, "ProjectFinancialFoundation", "View");
        var costUtilizationExport = AddPermission(_tenantId, "CostUtilization", "Export");
        var programRollupView = AddPermission(_tenantId, "ProgramFinancialRollup", "View");
        var portfolioRollupView = AddPermission(_tenantId, "PortfolioFinancialRollup", "View");
        actorRole.Permissions.Add(roleView);
        actorRole.Permissions.Add(roleCreate);
        actorRole.Permissions.Add(roleConfigure);
        actorRole.Permissions.Add(tenantSettingsView);
        actorRole.Permissions.Add(tenantManageScope);
        actorRole.Permissions.Add(resourceView);
        actorRole.Permissions.Add(projectSettingsView);
        actorRole.Permissions.Add(projectSettingsEdit);
        await _context.SaveChangesAsync();

        var service = CreateRoleService(actor.Id);
        var catalog = await service.GetAssignablePermissionsAsync(actor.Id, _tenantId);
        Assert.True(catalog.IsSuccess, catalog.Error?.Message);
        var expectedDelegable = new[]
        {
            "ProjectSettings.View", "ProjectSettings.Edit",
            "ProjectFinancialFoundation.View", "CostUtilization.Export",
            "ProgramFinancialRollup.View", "PortfolioFinancialRollup.View"
        };
        Assert.All(expectedDelegable, key => Assert.True(catalog.Value!.Single(item => item.Name == key).CanDelegate == true));

        var created = await service.CreateAsync(new SaveRoleDto
        {
            Name = "Project Settings Delegate",
            OrganizationId = _organizationId,
            Description = "Delegated project settings access",
            Category = RoleCategories.Management,
            PermissionIds = [projectSettingsView.Id, projectSettingsEdit.Id, financialFoundationView.Id]
        }, actor.Id, _tenantId);
        Assert.True(created.IsSuccess, created.Error?.Message);
        Assert.Contains(created.Value!.Permissions, item => item.Name == "ProjectSettings.Edit");
        Assert.Contains(created.Value.Permissions, item => item.Name == "ProjectFinancialFoundation.View");

        var recipient = new User
        {
            TenantId = _tenantId,
            FirstName = "Delegation",
            LastName = "Recipient",
            Email = $"delegation-recipient-{Guid.NewGuid():N}@test.local",
            CreatedBy = "test"
        };
        _context.Users.Add(recipient);
        _context.UserRoles.Add(new UserRole
        {
            TenantId = _tenantId,
            UserId = recipient.Id,
            RoleId = created.Value.Id,
            CreatedBy = "test"
        });
        await _context.SaveChangesAsync();

        var effectivePermissions = new UserEffectivePermissionService(_context);
        var recipientEffective = await effectivePermissions.GetEffectivePermissionsForOrganizationAsync(recipient.Id, _tenantId, _organizationId);
        Assert.Contains("ProjectSettings.View", recipientEffective);
        Assert.Contains("ProjectSettings.Edit", recipientEffective);
        Assert.Contains("ProjectFinancialFoundation.View", recipientEffective);

        _context.UserPermissionOverrides.Add(new UserPermissionOverride
        {
            TenantId = _tenantId,
            UserId = recipient.Id,
            PermissionId = projectSettingsEdit.Id,
            Effect = PermissionOverrideEffect.Deny,
            CreatedBy = "test"
        });
        await _context.SaveChangesAsync();
        recipientEffective = await effectivePermissions.GetEffectivePermissionsForOrganizationAsync(recipient.Id, _tenantId, _organizationId);
        Assert.Contains("ProjectSettings.View", recipientEffective);
        Assert.DoesNotContain("ProjectSettings.Edit", recipientEffective);

        var updated = await service.UpdateAsync(created.Value.Id, new SaveRoleDto
        {
            Name = created.Value.Name,
            Description = created.Value.Description,
            Category = created.Value.Category,
            PermissionIds = [projectSettingsView.Id]
        }, actor.Id, _tenantId);
        Assert.True(updated.IsSuccess, updated.Error?.Message);
        Assert.Contains(updated.Value!.Permissions, item => item.Name == "ProjectSettings.View");
        Assert.DoesNotContain(updated.Value.Permissions, item => item.Name == "ProjectSettings.Edit");
        Assert.DoesNotContain(updated.Value.Permissions, item => item.Name == "ProjectFinancialFoundation.View");

        recipientEffective = await effectivePermissions.GetEffectivePermissionsForOrganizationAsync(recipient.Id, _tenantId, _organizationId);
        Assert.Contains("ProjectSettings.View", recipientEffective);
        Assert.DoesNotContain("ProjectSettings.Edit", recipientEffective);
        Assert.DoesNotContain("ProjectFinancialFoundation.View", recipientEffective);
        Assert.Equal(2, await _context.AuditEntries.CountAsync(entry => entry.EntityType == "Role" && entry.EntityId == created.Value.Id));
    }

    [Fact]
    public async Task Role_permission_catalog_keeps_ordinary_scope_delegable_and_administration_actor_scoped()
    {
        var (actor, actorRole) = AddUserWithRole(_tenantId, "catalog-admin", protectedTenantAdmin: true);
        var visibleScopes = new HashSet<PermissionScope>
        {
            PermissionScope.Tenant,
            PermissionScope.Organization,
            PermissionScope.Portfolio,
            PermissionScope.Program,
            PermissionScope.Project
        };
        var expectedDefinitions = PermissionCatalog.Entries
            .Where(definition => definition.IsActive && visibleScopes.Contains(definition.Scope))
            .ToArray();
        var permissionRows = new Dictionary<string, Permission>(StringComparer.Ordinal);
        foreach (var definition in expectedDefinitions)
            permissionRows.Add(definition.Key, AddPermission(_tenantId, definition.Resource, definition.Action));

        var actorGrantKeys = new[]
        {
            "Roles.View",
            "Roles.ConfigurePermissions",
            "Settings.View",
            "TenantAdministration.ManageScope",
            "ProjectSettings.View",
            "ProjectSettings.Edit"
        };
        foreach (var key in actorGrantKeys)
            actorRole.Permissions.Add(permissionRows[key]);
        _context.UserPermissionOverrides.Add(new UserPermissionOverride
        {
            TenantId = _tenantId,
            UserId = actor.Id,
            PermissionId = permissionRows["ProjectSettings.Edit"].Id,
            Effect = PermissionOverrideEffect.Deny,
            CreatedBy = "test"
        });
        await _context.SaveChangesAsync();

        var service = CreateRoleService(actor.Id);
        var catalog = await service.GetAssignablePermissionsAsync(actor.Id, _tenantId);

        Assert.True(catalog.IsSuccess, catalog.Error?.Message);
        Assert.Equal(expectedDefinitions.Length, catalog.Value!.Count);
        Assert.Equal(
            expectedDefinitions.Select(definition => definition.Key).OrderBy(key => key, StringComparer.Ordinal),
            catalog.Value.Select(item => item.Name).OrderBy(key => key, StringComparer.Ordinal));

        var definitionsByKey = expectedDefinitions.ToDictionary(definition => definition.Key, StringComparer.Ordinal);
        Assert.All(catalog.Value, item =>
        {
            var definition = definitionsByKey[item.Name];
            Assert.Equal(definition.Scope.ToString(), item.Scope);
            Assert.Equal(definition.IsAssignable, item.IsAssignable);
            Assert.True(item.IsActive);
        });
        Assert.Equal(
            expectedDefinitions.Select(definition => definition.Scope.ToString()).Distinct().OrderBy(scope => scope, StringComparer.Ordinal),
            catalog.Value.Select(item => item.Scope).Distinct().OrderBy(scope => scope, StringComparer.Ordinal));

        Assert.True(catalog.Value.Single(item => item.Name == "Roles.View").CanDelegate == true);
        Assert.True(catalog.Value.Single(item => item.Name == "ProjectSettings.View").CanDelegate == true);
        Assert.False(catalog.Value.Single(item => item.Name == "ProjectSettings.Edit").CanDelegate == true);
        Assert.True(catalog.Value.Single(item => item.Name == "ProjectSettings.Edit").CanDelegateToCustomRole == true);
        Assert.True(catalog.Value.Single(item => item.Name == "ProjectFinancialFoundation.View").CanDelegate == true);
        Assert.True(catalog.Value.Single(item => item.Name == "ProjectFinancialFoundation.View").CanDelegateToCustomRole == true);
        Assert.False(catalog.Value.Single(item => item.Name == "Budget.Delete").CanDelegate == true);
        Assert.True(catalog.Value.Single(item => item.Name == "Budget.Delete").CanDelegateToCustomRole == true);
    }

    [Fact]
    public async Task Permissions_previously_hidden_by_Admin_allowlist_are_grantable_by_default()
    {
        var (actor, actorRole) = AddUserWithRole(_tenantId, "core-permission-delegator", protectedTenantAdmin: true);
        actorRole.Permissions.Add(AddPermission(_tenantId, "Roles", "View"));
        actorRole.Permissions.Add(AddPermission(_tenantId, "Roles", "Create"));
        actorRole.Permissions.Add(AddPermission(_tenantId, "Roles", "ConfigurePermissions"));
        var recoveryView = AddPermission(_tenantId, "Governance.AssignmentRecovery", "View");
        var recoveryManage = AddPermission(_tenantId, "Governance.AssignmentRecovery", "Manage");
        await _context.SaveChangesAsync();

        var service = CreateRoleService(actor.Id);
        var catalog = await service.GetAssignablePermissionsAsync(actor.Id, _tenantId);
        Assert.True(catalog.IsSuccess, catalog.Error?.Message);
        Assert.True(catalog.Value!.Single(item => item.Name == "Governance.AssignmentRecovery.View").CanDelegateToCustomRole == true);
        Assert.True(catalog.Value.Single(item => item.Name == "Governance.AssignmentRecovery.Manage").CanDelegateToCustomRole == true);

        var created = await service.CreateAsync(new SaveRoleDto
        {
            Name = "Recovery operator",
            OrganizationId = _organizationId,
            Category = RoleCategories.Management,
            PermissionIds = [recoveryView.Id, recoveryManage.Id]
        }, actor.Id, _tenantId);

        Assert.True(created.IsSuccess, created.Error?.Message);
        Assert.Contains(created.Value!.Permissions, permission => permission.Name == "Governance.AssignmentRecovery.View");
        Assert.Contains(created.Value.Permissions, permission => permission.Name == "Governance.AssignmentRecovery.Manage");
    }

    [Fact]
    public async Task Ordinary_scoped_permissions_are_delegable_without_manage_tenant_scope_and_foreign_ids_are_rejected()
    {
        var (actor, actorRole) = AddUserWithRole(_tenantId, "limited-delegation-admin", protectedTenantAdmin: true);
        var roleView = AddPermission(_tenantId, "Roles", "View");
        var roleCreate = AddPermission(_tenantId, "Roles", "Create");
        var roleConfigure = AddPermission(_tenantId, "Roles", "ConfigurePermissions");
        var tenantSettingsView = AddPermission(_tenantId, "Settings", "View");
        var resourceView = AddPermission(_tenantId, "Resource", "View");
        var projectSettingsView = AddPermission(_tenantId, "ProjectSettings", "View");
        var projectSettingsEdit = AddPermission(_tenantId, "ProjectSettings", "Edit");
        var financialFoundationView = AddPermission(_tenantId, "ProjectFinancialFoundation", "View");
        var costUtilizationExport = AddPermission(_tenantId, "CostUtilization", "Export");
        var programRollupView = AddPermission(_tenantId, "ProgramFinancialRollup", "View");
        var portfolioRollupView = AddPermission(_tenantId, "PortfolioFinancialRollup", "View");
        actorRole.Permissions.Add(roleView);
        actorRole.Permissions.Add(roleCreate);
        actorRole.Permissions.Add(roleConfigure);
        actorRole.Permissions.Add(tenantSettingsView);
        actorRole.Permissions.Add(resourceView);
        actorRole.Permissions.Add(projectSettingsView);
        actorRole.Permissions.Add(projectSettingsEdit);
        await _context.SaveChangesAsync();

        var service = CreateRoleService(actor.Id);
        var catalog = await service.GetAssignablePermissionsAsync(actor.Id, _tenantId);
        Assert.True(catalog.IsSuccess, catalog.Error?.Message);
        Assert.True(catalog.Value!.Single(item => item.Name == "ProjectSettings.View").CanDelegate == true);
        Assert.True(catalog.Value.Single(item => item.Name == "ProjectSettings.Edit").CanDelegate == true);
        Assert.False(catalog.Value.Single(item => item.Name == "ProjectFinancialFoundation.View").CanDelegate == true);
        Assert.False(catalog.Value.Single(item => item.Name == "CostUtilization.Export").CanDelegate == true);
        Assert.False(catalog.Value.Single(item => item.Name == "ProgramFinancialRollup.View").CanDelegate == true);
        Assert.False(catalog.Value.Single(item => item.Name == "PortfolioFinancialRollup.View").CanDelegate == true);
        Assert.All(new[]
        {
            "ProjectFinancialFoundation.View", "CostUtilization.Export",
            "ProgramFinancialRollup.View", "PortfolioFinancialRollup.View"
        }, key => Assert.True(catalog.Value.Single(item => item.Name == key).CanDelegateToCustomRole == true));

        var foreignPermission = AddPermission(Guid.NewGuid(), "ProjectFinancialFoundation", "View");
        await _context.SaveChangesAsync();
        var foreignGrant = await service.CreateAsync(new SaveRoleDto
        {
            Name = "Foreign finance grant",
            OrganizationId = _organizationId,
            PermissionIds = [foreignPermission.Id]
        }, actor.Id, _tenantId);
        Assert.True(foreignGrant.IsFailure);
        Assert.Equal(400, foreignGrant.Error?.StatusCode);
    }

    [Fact]
    public async Task Planning_bootstrap_assigns_a_custom_role_to_another_same_tenant_user()
    {
        var (actor, actorRole) = AddUserWithRole(_tenantId, "planning-delegator", protectedTenantAdmin: true);
        var (target, _) = AddUserWithRole(_tenantId, "planning-recipient");
        actorRole.Permissions.Add(AddPermission(_tenantId, "Users", "View"));
        actorRole.Permissions.Add(AddPermission(_tenantId, "Users", "AssignRoles"));
        actorRole.Permissions.Add(AddPermission(_tenantId, "Roles", "View"));
        actorRole.Permissions.Add(AddPermission(_tenantId, "Roles", "ConfigurePermissions"));
        actorRole.Permissions.Add(AddPermission(_tenantId, "TenantAdministration", "ManageScope"));

        var planningView = AddPermission(_tenantId, "Planning", "View");
        var planningEdit = AddPermission(_tenantId, "Planning", "Edit");
        var planCreate = AddPermission(_tenantId, "ProjectPlan", "Create");
        var customRole = new Role
        {
            TenantId = _tenantId,
            Name = "Planning Custom Role",
            Category = RoleCategories.Management,
            IsSystem = false,
            CreatedBy = actor.Id.ToString("D"),
            Permissions = [planningView, planningEdit, planCreate]
        };
        customRole.AssignOrganization(_organizationId);
        _context.Roles.Add(customRole);
        await _context.SaveChangesAsync();

        var service = CreateUserAdministrationService(actor.Id);
        var updated = await service.UpdateAsync(target.Id, new UpdateUserDto
        {
            Email = target.Email,
            FirstName = target.FirstName,
            LastName = target.LastName,
            IsActive = target.IsActive,
            RoleIds = [customRole.Id]
        }, actor.Id.ToString("D"));

        Assert.True(updated.IsSuccess, updated.Error?.Message);
        var assignment = await _context.UserRoles.IgnoreQueryFilters()
            .Include(item => item.Role)
                .ThenInclude(role => role.Permissions)
            .SingleAsync(item => item.UserId == target.Id && item.RoleId == customRole.Id && !item.IsDeleted);
        Assert.False(assignment.Role.IsSystem);
        Assert.Equal(
            new[] { "Planning.Edit", "Planning.View", "ProjectPlan.Create" },
            assignment.Role.Permissions.Select(permission => permission.Name).OrderBy(key => key, StringComparer.Ordinal));
    }

    [Fact]
    public async Task Role_configurator_can_assign_non_system_Project_Manager_without_Planning_Edit()
    {
        var (actor, actorRole) = AddUserWithRole(_tenantId, "planning-delegator", protectedTenantAdmin: true);
        var (target, _) = AddUserWithRole(_tenantId, "planning-recipient");
        actorRole.Permissions.Add(AddPermission(_tenantId, "Users", "AssignRoles"));
        actorRole.Permissions.Add(AddPermission(_tenantId, "Roles", "ConfigurePermissions"));
        actorRole.Permissions.Add(AddPermission(_tenantId, "TenantAdministration", "ManageScope"));

        var projectManager = new Role
        {
            TenantId = _tenantId,
            Name = "project.manager",
            Category = RoleCategories.Management,
            IsSystem = false,
            CreatedBy = "seed"
        };
        projectManager.AssignRoleKey("tenant.project-manager", _tenantId);
        projectManager.AssignOrganization(_organizationId);
        projectManager.Permissions.Add(AddPermission(_tenantId, "Planning", "View"));
        projectManager.Permissions.Add(AddPermission(_tenantId, "Planning", "Edit"));
        _context.Roles.Add(projectManager);
        await _context.SaveChangesAsync();

        var service = CreateUserAdministrationService(actor.Id);
        var assigned = await service.UpdateAsync(target.Id, new UpdateUserDto
        {
            Email = target.Email,
            FirstName = target.FirstName,
            LastName = target.LastName,
            IsActive = target.IsActive,
            RoleIds = [projectManager.Id]
        }, actor.Id.ToString("D"));

        Assert.True(assigned.IsSuccess, assigned.Error?.Message);
        Assert.True(await _context.UserRoles.IgnoreQueryFilters()
            .AnyAsync(item => item.UserId == target.Id && item.RoleId == projectManager.Id && !item.IsDeleted));
    }

    [Fact]
    public async Task Existing_Planning_Edit_holder_can_assign_non_system_Project_Manager()
    {
        var (actor, actorRole) = AddUserWithRole(_tenantId, "planning-delegator", protectedTenantAdmin: true);
        var (target, _) = AddUserWithRole(_tenantId, "planning-recipient");
        actorRole.Permissions.Add(AddPermission(_tenantId, "Users", "AssignRoles"));
        actorRole.Permissions.Add(AddPermission(_tenantId, "Planning", "View"));
        actorRole.Permissions.Add(AddPermission(_tenantId, "Planning", "Edit"));

        var projectManager = new Role
        {
            TenantId = _tenantId,
            Name = "project.manager",
            Category = RoleCategories.Management,
            IsSystem = false,
            CreatedBy = "seed"
        };
        projectManager.AssignRoleKey("tenant.project-manager", _tenantId);
        projectManager.AssignOrganization(_organizationId);
        projectManager.Permissions.Add(AddPermission(_tenantId, "Planning", "View"));
        projectManager.Permissions.Add(AddPermission(_tenantId, "Planning", "Edit"));
        _context.Roles.Add(projectManager);
        await _context.SaveChangesAsync();

        var service = CreateUserAdministrationService(actor.Id);
        var assigned = await service.UpdateAsync(target.Id, new UpdateUserDto
        {
            Email = target.Email,
            FirstName = target.FirstName,
            LastName = target.LastName,
            IsActive = target.IsActive,
            RoleIds = [projectManager.Id]
        }, actor.Id.ToString("D"));

        Assert.True(assigned.IsSuccess, assigned.Error?.Message);
        Assert.True(await _context.UserRoles.IgnoreQueryFilters()
            .AnyAsync(item => item.UserId == target.Id && item.RoleId == projectManager.Id && !item.IsDeleted));
    }

    [Fact]
    public async Task Role_creation_rechecks_create_permission_after_tenant_lock()
    {
        var (actor, _) = AddUserWithRole(_tenantId, "stale-role-actor");
        await _context.SaveChangesAsync();

        var effective = new Mock<IUserEffectivePermissionService>();
        effective.SetupSequence(service => service.GetEffectivePermissionsForOrganizationAsync(
                actor.Id, _tenantId, _organizationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>(["Roles.Create"], StringComparer.Ordinal))
            .ReturnsAsync(new HashSet<string>(StringComparer.Ordinal));
        var audit = new Mock<IAuditService>();
        audit.Setup(service => service.AppendAsync(It.IsAny<AuditEntry>(), It.IsAny<CancellationToken>()))
            .Callback<AuditEntry, CancellationToken>((entry, _) => _context.AuditEntries.Add(entry))
            .ReturnsAsync(Result.Success());
        var http = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, actor.Id.ToString()), new Claim("tenantId", _tenantId.ToString())], "test"))
            }
        };
        var organizationDirectory = new Mock<IUserOrganizationDirectory>();
        organizationDirectory
            .Setup(directory => directory.GetActiveNamesAsync(
                It.IsAny<Guid>(), It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid _, IEnumerable<Guid> organizationIds, CancellationToken _) =>
                (IReadOnlyDictionary<Guid, string>)organizationIds.Distinct()
                    .ToDictionary(id => id, id => id.ToString("D")));
        var service = new RoleAdministrationService(_context, effective.Object, audit.Object, http, organizationDirectory.Object);

        var result = await service.CreateAsync(new SaveRoleDto
        {
            Name = "Revoked During Creation",
            OrganizationId = _organizationId,
            Category = RoleCategories.Management,
            PermissionIds = []
        }, actor.Id, _tenantId);

        Assert.True(result.IsFailure);
        Assert.DoesNotContain(await _context.Roles.IgnoreQueryFilters().ToListAsync(), role => role.Name == "Revoked During Creation");
    }

    [Fact]
    public async Task Direct_override_update_rechecks_manage_permission_after_tenant_lock()
    {
        var (actor, actorRole) = AddUserWithRole(_tenantId, "stale-override-actor");
        var (target, _) = AddUserWithRole(_tenantId, "override-target");
        actorRole.Permissions.Add(AddPermission(_tenantId, "UserPermissions", "Manage"));
        var projectView = AddPermission(_tenantId, "Project", "View");
        await _context.SaveChangesAsync();

        var effective = new Mock<IUserEffectivePermissionService>();
        effective.SetupSequence(service => service.GetEffectivePermissionsAsync(actor.Id, _tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>(["UserPermissions.Manage"], StringComparer.Ordinal))
            .ReturnsAsync(new HashSet<string>(StringComparer.Ordinal));
        var audit = new Mock<IAuditService>();
        audit.Setup(service => service.AppendAsync(It.IsAny<AuditEntry>(), It.IsAny<CancellationToken>()))
            .Callback<AuditEntry, CancellationToken>((entry, _) => _context.AuditEntries.Add(entry))
            .ReturnsAsync(Result.Success());
        var http = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, actor.Id.ToString()), new Claim("tenantId", _tenantId.ToString())], "test"))
            }
        };
        var service = CreateUserAdministrationService(http, audit.Object, effective.Object);

        var result = await service.UpdateUserPermissionsAsync(target.Id, _tenantId, new UpdateUserPermissionsDto
        {
            Overrides = [new UserPermissionOverrideInput { PermissionId = projectView.Id, Effect = PermissionOverrideEffect.Allow }]
        }, actor.Id.ToString());

        Assert.True(result.IsFailure);
        Assert.Equal(403, result.Error?.StatusCode);
        Assert.Empty(await _context.UserPermissionOverrides.IgnoreQueryFilters().Where(item => item.UserId == target.Id).ToListAsync());
    }

    private RoleAdministrationService CreateRoleService(Guid actorId)
    {
        var audit = new Mock<IAuditService>();
        audit.Setup(service => service.AppendAsync(It.IsAny<AuditEntry>(), It.IsAny<CancellationToken>()))
            .Callback<AuditEntry, CancellationToken>((entry, _) => _context.AuditEntries.Add(entry))
            .ReturnsAsync(Result.Success());
        var http = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, actorId.ToString()),
                    new Claim("tenantId", _tenantId.ToString())
                ], "test"))
            }
        };
        var organizationDirectory = new Mock<IUserOrganizationDirectory>();
        organizationDirectory
            .Setup(directory => directory.GetActiveNamesAsync(
                It.IsAny<Guid>(), It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid _, IEnumerable<Guid> organizationIds, CancellationToken _) =>
                (IReadOnlyDictionary<Guid, string>)organizationIds.Where(id => id != Guid.Empty).Distinct()
                    .ToDictionary(id => id, id => id.ToString("D")));
        return new RoleAdministrationService(
            _context, new UserEffectivePermissionService(_context), audit.Object, http, organizationDirectory.Object);
    }

    private UserAdministrationService CreateUserAdministrationService(Guid actorId)
    {
        var audit = new Mock<IAuditService>();
        audit.Setup(service => service.AppendAsync(It.IsAny<AuditEntry>(), It.IsAny<CancellationToken>()))
            .Callback<AuditEntry, CancellationToken>((entry, _) => _context.AuditEntries.Add(entry))
            .ReturnsAsync(Result.Success());
        var http = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, actorId.ToString()),
                    new Claim("tenantId", _tenantId.ToString())
                ], "test"))
            }
        };
        return CreateUserAdministrationService(http, audit.Object);
    }

    private UserAdministrationService CreateUserAdministrationService(
        Guid actorId,
        IAuditService auditService,
        Microsoft.AspNetCore.Identity.IPasswordHasher<User>? passwordHasher = null)
    {
        var http = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, actorId.ToString("D")), new Claim("tenantId", _tenantId.ToString("D"))], "test"))
            }
        };
        return CreateUserAdministrationService(http, auditService, passwordHasher: passwordHasher);
    }

    private UserAdministrationService CreateUserAdministrationService(
        IHttpContextAccessor http,
        IAuditService auditService,
        IUserEffectivePermissionService? effectivePermissions = null,
        Microsoft.AspNetCore.Identity.IPasswordHasher<User>? passwordHasher = null)
    {
        var permissions = effectivePermissions ?? new UserEffectivePermissionService(_context);
        var organizationDirectory = new Mock<IUserOrganizationDirectory>();
        organizationDirectory
            .Setup(directory => directory.GetActiveNamesAsync(
                It.IsAny<Guid>(), It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid _, IEnumerable<Guid> organizationIds, CancellationToken _) =>
                (IReadOnlyDictionary<Guid, string>)organizationIds.Distinct()
                    .ToDictionary(id => id, id => id.ToString("D")));
        var membershipIntegrity = new Mock<IUserOrganizationMembershipIntegrity>();
        membershipIntegrity
            .Setup(integrity => integrity.GetOrganizationsBlockingRemovalAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Guid>());
        var assignmentAuthorization = new RoleAssignmentAuthorizationService(
            _context, permissions, http, organizationDirectory.Object);

        return new UserAdministrationService(
            _context,
            passwordHasher ?? Mock.Of<Microsoft.AspNetCore.Identity.IPasswordHasher<User>>(),
            http,
            permissions,
            auditService,
            assignmentAuthorization,
            Options.Create(new LocalPasswordPolicyOptions()),
            organizationDirectory.Object,
            membershipIntegrity.Object);
    }

    private (User User, Role Role) AddUserWithRole(Guid tenantId, string roleName, bool protectedTenantAdmin = false)
    {
        var user = new User { TenantId = tenantId, FirstName = roleName, LastName = "User", Email = $"{roleName}-{Guid.NewGuid():N}@test.local", CreatedBy = "test" };
        var role = new Role { TenantId = tenantId, Name = roleName, CreatedBy = protectedTenantAdmin ? "System" : "test", IsSystem = protectedTenantAdmin };
        if (protectedTenantAdmin)
            role.AssignSystemRoleKey(Role.TenantAdminSystemRoleKey, tenantId);
        else
            role.AssignOrganization(tenantId == _tenantId ? _organizationId : Guid.NewGuid());
        _context.Users.Add(user);
        _context.Roles.Add(role);
        _context.UserRoles.Add(new UserRole { TenantId = tenantId, User = user, Role = role, CreatedBy = "test" });
        return (user, role);
    }

    private Permission AddPermission(Guid tenantId, string resource, string action)
    {
        var permission = new Permission
        {
            TenantId = tenantId,
            Name = $"{resource}.{action}",
            Resource = resource,
            Action = action,
            CreatedBy = "test"
        };
        _context.Permissions.Add(permission);
        return permission;
    }

    private Guid AddActiveSession(User user, Guid tenantId)
    {
        var session = new UserSession
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = user.Id,
            User = user,
            Token = "test-token-hash",
            RefreshToken = "test-refresh-token-hash",
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            CreatedBy = "test"
        };
        _context.UserSessions.Add(session);
        return session.Id;
    }

    private DefaultHttpContext CreatePermissionContext(
        Guid userId,
        Guid sessionId,
        Guid projectId,
        Guid? organizationId = null)
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, userId.ToString("D")),
                new Claim("tenantId", _tenantId.ToString("D")),
                new Claim("sid", sessionId.ToString("D"))
            ], "test"))
        };
        context.Request.QueryString = new QueryString(
            $"?projectId={projectId:D}" + (organizationId.HasValue ? $"&organizationId={organizationId.Value:D}" : string.Empty));
        return context;
    }
}

internal sealed class PermissionTestDbContext(DbContextOptions<PermissionTestDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();
    public DbSet<UserPermissionOverride> UserPermissionOverrides => Set<UserPermissionOverride>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>().Ignore(user => user.FullName);
        modelBuilder.Entity<UserSession>().HasOne(session => session.User).WithMany(user => user.Sessions).HasForeignKey(session => session.UserId);
        modelBuilder.Entity<Role>().HasMany(role => role.Permissions).WithMany(permission => permission.Roles);
        modelBuilder.Entity<UserRole>().HasOne(assignment => assignment.User).WithMany(user => user.UserRoles).HasForeignKey(assignment => assignment.UserId);
        modelBuilder.Entity<UserRole>().HasOne(assignment => assignment.Role).WithMany(role => role.UserRoles).HasForeignKey(assignment => assignment.RoleId);

        modelBuilder.Entity<UserPermissionOverride>().HasOne(item => item.User).WithMany(user => user.PermissionOverrides).HasForeignKey(item => item.UserId);
        modelBuilder.Entity<UserPermissionOverride>().HasOne(item => item.Permission).WithMany(permission => permission.UserOverrides).HasForeignKey(item => item.PermissionId);
        modelBuilder.Entity<AuditEntry>().Ignore(entry => entry.Activities);
    }
}
