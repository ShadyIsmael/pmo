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
    public void Permission_catalog_has_explicit_valid_role_category_eligibility_for_every_active_permission()
    {
        Assert.Empty(PermissionRoleCategoryPolicy.UnmappedActivePermissionKeys());
        foreach (var permission in PermissionCatalog.Entries.Where(entry => entry.IsActive))
        {
            Assert.NotEmpty(permission.AllowedCategories);
            Assert.Equal(permission.AllowedCategories.Count, permission.AllowedCategories.Distinct(StringComparer.Ordinal).Count());
            Assert.All(permission.AllowedCategories, category => Assert.Contains(category, RoleCategories.Selectable));
        }

        Assert.Contains(RoleCategories.Management, PermissionCatalog.ByKey["Project.View"].AllowedCategories);
        Assert.Contains(RoleCategories.Operation, PermissionCatalog.ByKey["Project.View"].AllowedCategories);
        Assert.Contains(RoleCategories.Operation, PermissionCatalog.ByKey["ProjectAnnualBudget.SubmitForReview"].AllowedCategories);
        Assert.DoesNotContain(RoleCategories.Operation, PermissionCatalog.ByKey["ProjectAnnualBudget.Approve"].AllowedCategories);
        Assert.Equal(new[] { RoleCategories.Management }, PermissionCatalog.ByKey["ProjectAnnualBudget.Approve"].AllowedCategories);
    }

    [Fact]
    public async Task Role_category_policy_blocks_new_mismatched_grants_and_requires_explicit_removal_before_change()
    {
        var (actor, actorRole) = AddUserWithRole(_tenantId, "category-admin");
        actorRole.Permissions.Add(AddPermission(_tenantId, "Roles", "View"));
        actorRole.Permissions.Add(AddPermission(_tenantId, "Roles", "Create"));
        actorRole.Permissions.Add(AddPermission(_tenantId, "Roles", "ConfigurePermissions"));
        var projectView = AddPermission(_tenantId, "Project", "View");
        var projectEdit = AddPermission(_tenantId, "Project", "Edit");
        actorRole.Permissions.Add(projectView);
        actorRole.Permissions.Add(projectEdit);
        await _context.SaveChangesAsync();

        var service = CreateRoleService(actor.Id);
        var wrongCategory = await service.CreateAsync(new SaveRoleDto
        {
            Name = "Operations project editor",
            Category = RoleCategories.Operation,
            PermissionIds = [projectView.Id, projectEdit.Id]
        }, actor.Id, _tenantId);
        Assert.True(wrongCategory.IsFailure);
        Assert.Equal(400, wrongCategory.Error?.StatusCode);
        Assert.Equal("ROLE_PERMISSION_CATEGORY_NOT_ALLOWED", wrongCategory.Error?.Code);
        Assert.Equal("Project.Edit", wrongCategory.Error?.Details?["permissionKey"]);
        Assert.Equal(new[] { RoleCategories.Management },
            Assert.IsAssignableFrom<IReadOnlyList<string>>(wrongCategory.Error?.Details?["allowedCategories"]));

        var created = await service.CreateAsync(new SaveRoleDto
        {
            Name = "Management project editor",
            Category = RoleCategories.Management,
            PermissionIds = [projectView.Id, projectEdit.Id]
        }, actor.Id, _tenantId);
        Assert.True(created.IsSuccess, created.Error?.Message);

        var categoryConflict = await service.UpdateAsync(created.Value!.Id, new SaveRoleDto
        {
            Name = created.Value.Name,
            Category = RoleCategories.Operation,
            PermissionIds = [projectView.Id, projectEdit.Id]
        }, actor.Id, _tenantId);
        Assert.True(categoryConflict.IsFailure);
        Assert.Equal(409, categoryConflict.Error?.StatusCode);
        Assert.Equal("ROLE_CATEGORY_PERMISSION_CONFLICT", categoryConflict.Error?.Code);
        Assert.Equal(new[] { "Project.Edit" },
            Assert.IsAssignableFrom<IReadOnlyList<string>>(categoryConflict.Error?.Details?["permissionKeys"]));

        var removedConflict = await service.UpdateAsync(created.Value.Id, new SaveRoleDto
        {
            Name = created.Value.Name,
            Category = RoleCategories.Management,
            PermissionIds = [projectView.Id]
        }, actor.Id, _tenantId);
        Assert.True(removedConflict.IsSuccess, removedConflict.Error?.Message);
        Assert.DoesNotContain(removedConflict.Value!.Permissions, permission => permission.Name == "Project.Edit");

        var legacyRole = new Role { TenantId = _tenantId, Name = "Legacy Operations", Category = "Operations", CreatedBy = "legacy-seed" };
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
            .GetEffectivePermissionsAsync(user.Id, _tenantId);

        Assert.Contains("Project.View", effective);
        Assert.DoesNotContain("Project.Edit", effective);
        Assert.Contains("Report.View", effective);
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
        var service = new UserAdministrationService(
            _context,
            Mock.Of<Microsoft.AspNetCore.Identity.IPasswordHasher<User>>(),
            http,
            new UserEffectivePermissionService(_context),
            audit.Object,
            Options.Create(new LocalPasswordPolicyOptions()));

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
            Roles = []
        }, Guid.NewGuid().ToString());

        Assert.True(forgedActorCreate.IsFailure);
        Assert.Equal(403, forgedActorCreate.Error?.StatusCode);
        Assert.DoesNotContain(await _context.Users.IgnoreQueryFilters().ToListAsync(), user => user.Email == "forged@example.test");
    }

    [Fact]
    public async Task Protected_admin_baseline_is_effective_and_is_not_inferred_from_role_name_alone()
    {
        var (forgedUser, _) = AddUserWithRole(_tenantId, "admin");
        var (protectedUser, protectedRole) = AddUserWithRole(_tenantId, "admin");
        protectedRole.IsSystem = true;
        protectedRole.CreatedBy = "System";
        protectedRole.AssignSystemRoleKey(Role.TenantAdminSystemRoleKey, _tenantId);
        var protectedPermission = AddPermission(_tenantId, "Roles", "ConfigurePermissions");
        protectedRole.Permissions.Add(protectedPermission);
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

        Assert.Contains("Roles.ConfigurePermissions", protectedPermissions);
        Assert.Contains("Users.AssignRoles", protectedPermissions);
        Assert.DoesNotContain("Roles.ConfigurePermissions",
            await resolver.GetEffectivePermissionsAsync(forgedUser.Id, _tenantId));
    }

    [Fact]
    public async Task Direct_deny_cannot_remove_a_protected_admin_baseline_even_when_another_admin_remains()
    {
        var (actor, actorRole) = AddUserWithRole(_tenantId, "override-admin");
        var (target, targetRole) = AddUserWithRole(_tenantId, "admin");
        var (backupAdmin, backupAdminRole) = AddUserWithRole(_tenantId, "admin-backup");
        targetRole.IsSystem = true;
        targetRole.CreatedBy = "System";
        targetRole.AssignSystemRoleKey(Role.TenantAdminSystemRoleKey, _tenantId);
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
        var service = new UserAdministrationService(
            _context,
            Mock.Of<Microsoft.AspNetCore.Identity.IPasswordHasher<User>>(),
            http,
            permissionService,
            audit.Object,
            Options.Create(new LocalPasswordPolicyOptions()));

        var updated = await service.UpdateUserPermissionsAsync(target.Id, _tenantId,
            new UpdateUserPermissionsDto
            {
                Overrides = [new UserPermissionOverrideInput
                {
                    PermissionId = configureRoles.Id,
                    Effect = PermissionOverrideEffect.Deny
                }]
            }, actor.Id.ToString());

        Assert.True(updated.IsFailure);
        Assert.Equal(409, updated.Error?.StatusCode);
        Assert.Contains("Roles.ConfigurePermissions",
            await permissionService.GetEffectivePermissionsAsync(target.Id, _tenantId));
        Assert.Contains("Roles.ConfigurePermissions",
            await permissionService.GetEffectivePermissionsAsync(backupAdmin.Id, _tenantId));
        Assert.Empty(await _context.UserPermissionOverrides.Where(item => item.UserId == target.Id && !item.IsDeleted).ToListAsync());
    }

    [Fact]
    public async Task User_status_change_cannot_remove_the_last_effective_tenant_admin()
    {
        var (actor, actorRole) = AddUserWithRole(_tenantId, "status-actor");
        actorRole.Permissions.Add(AddPermission(_tenantId, "Users", "View"));
        actorRole.Permissions.Add(AddPermission(_tenantId, "Users", "SetStatus"));
        var (target, adminRole) = AddUserWithRole(_tenantId, "admin");
        adminRole.IsSystem = true;
        adminRole.CreatedBy = "System";
        adminRole.AssignSystemRoleKey(Role.TenantAdminSystemRoleKey, _tenantId);
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
        var service = new UserAdministrationService(
            _context,
            Mock.Of<Microsoft.AspNetCore.Identity.IPasswordHasher<User>>(),
            http,
            new UserEffectivePermissionService(_context),
            audit.Object,
            Options.Create(new LocalPasswordPolicyOptions()));

        var rejected = await service.UpdateAsync(target.Id, new UpdateUserDto
        {
            Email = target.Email,
            FirstName = target.FirstName,
            LastName = target.LastName,
            IsActive = false,
            Roles = [adminRole.Name]
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
        var activePermissions = await permissionService.GetEffectivePermissionsAsync(user.Id, _tenantId);
        Assert.Contains("Project.View", activePermissions);
        Assert.DoesNotContain("admin.write", activePermissions);

        user.IsActive = false;
        await _context.SaveChangesAsync();
        Assert.Empty(await permissionService.GetEffectivePermissionsAsync(user.Id, _tenantId));
    }

    [Fact]
    public async Task Middleware_replaces_stale_token_permission_claims_with_current_permissions()
    {
        var (user, role) = AddUserWithRole(_tenantId, "member");
        var current = AddPermission(_tenantId, "Project", "View");
        role.Permissions.Add(current);
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
            Assert.Contains(context.User.FindAll("permission"), claim => claim.Value == "Project.View");
            Assert.DoesNotContain(context.User.FindAll("permission"), claim => claim.Value == "Project.Delete");
            Assert.Contains(context.User.FindAll(ClaimTypes.Role), claim => claim.Value == "member");
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
        var (actor, actorRole) = AddUserWithRole(_tenantId, "admin");
        var (target, targetRole) = AddUserWithRole(_tenantId, "member");
        var manageOverrides = AddPermission(_tenantId, "UserPermissions", "Manage");
        var usersRead = AddPermission(_tenantId, "Users", "View");
        var projectView = AddPermission(_tenantId, "Project", "View");
        var editProjects = AddPermission(_tenantId, "Project", "Edit");
        var unauthorizedPermission = AddPermission(_tenantId, "Settings", "ManageTenantConfiguration");
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
        var service = new UserAdministrationService(
            _context,
            Mock.Of<Microsoft.AspNetCore.Identity.IPasswordHasher<User>>(),
            http,
            new UserEffectivePermissionService(_context),
            audit.Object,
            Options.Create(new LocalPasswordPolicyOptions()));

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
    }

    [Fact]
    public async Task Inactive_direct_overrides_cannot_be_added_and_existing_ones_are_retired_on_reconciliation()
    {
        var (actor, actorRole) = AddUserWithRole(_tenantId, "admin");
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
        var service = new UserAdministrationService(
            _context,
            Mock.Of<Microsoft.AspNetCore.Identity.IPasswordHasher<User>>(),
            http,
            new UserEffectivePermissionService(_context),
            audit.Object,
            Options.Create(new LocalPasswordPolicyOptions()));

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
        var (actor, actorRole) = AddUserWithRole(_tenantId, "administrator");
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
        var service = new UserAdministrationService(
            _context,
            Mock.Of<Microsoft.AspNetCore.Identity.IPasswordHasher<User>>(),
            http,
            new UserEffectivePermissionService(_context),
            audit.Object,
            Options.Create(new LocalPasswordPolicyOptions()));

        var updated = await service.UpdateAsync(target.Id, new UpdateUserDto
        {
            Email = target.Email,
            FirstName = target.FirstName,
            LastName = target.LastName,
            IsActive = target.IsActive,
            Roles = [replacementRole.Name]
        }, actor.Id.ToString());

        Assert.True(updated.IsSuccess);
        Assert.Equal(new[] { replacementRole.Name }, updated.Value!.Roles);
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
            Roles = [elevatedRole.Name]
        }, actor.Id.ToString());

        Assert.True(profileOnlyUpdate.IsSuccess);
        Assert.Equal("Updated", profileOnlyUpdate.Value!.FirstName);
        Assert.Equal(new[] { elevatedRole.Name }, profileOnlyUpdate.Value.Roles);

        var selfUpdate = await service.UpdateAsync(actor.Id, new UpdateUserDto
        {
            Email = actor.Email,
            FirstName = actor.FirstName,
            LastName = actor.LastName,
            IsActive = actor.IsActive,
            Roles = []
        }, actor.Id.ToString());

        Assert.True(selfUpdate.IsFailure);
        Assert.Equal(403, selfUpdate.Error?.StatusCode);
        Assert.Equal(1, await _context.AuditEntries.CountAsync(entry => entry.Action == "RolesUpdated"));
    }

    [Fact]
    public async Task Role_create_and_update_save_permission_sets_and_audit_changes()
    {
        var (actor, actorRole) = AddUserWithRole(_tenantId, "role-admin");
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
    public async Task Role_management_is_tenant_scoped_and_limits_permissions_to_actor_effective_access()
    {
        var (actor, actorRole) = AddUserWithRole(_tenantId, "role-admin");
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
        Assert.DoesNotContain(catalog.Value!, item => item.Id == foreignPermission.Id);

        var crossTenantRole = await service.GetByIdAsync(foreignRole.Id, actor.Id, _tenantId);
        Assert.True(crossTenantRole.IsFailure);
        Assert.Equal(404, crossTenantRole.Error?.StatusCode);

        var escalation = await service.CreateAsync(new SaveRoleDto
        {
            Name = "Escalated role",
            Category = RoleCategories.Management,
            PermissionIds = [settingsWrite.Id]
        }, actor.Id, _tenantId);
        Assert.True(escalation.IsFailure);
        Assert.Equal(403, escalation.Error?.StatusCode);

        var foreignGrant = await service.CreateAsync(new SaveRoleDto
        {
            Name = "Foreign permission role",
            PermissionIds = [foreignPermission.Id]
        }, actor.Id, _tenantId);
        Assert.True(foreignGrant.IsFailure);
        Assert.Equal(400, foreignGrant.Error?.StatusCode);
    }

    [Fact]
    public async Task Role_administrator_can_configure_role_permissions_within_its_delegation_ceiling()
    {
        var (actor, actorRole) = AddUserWithRole(_tenantId, "tenant-admin");
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
    public async Task Authorized_role_creator_can_create_role_with_delegable_view_and_action_without_escalating()
    {
        var (actor, actorRole) = AddUserWithRole(_tenantId, "tenant-role-admin");
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

        var created = await service.CreateAsync(new SaveRoleDto
        {
            Name = "Project Editor",
            Category = RoleCategories.Management,
            PermissionIds = [projectView.Id, projectEdit.Id]
        }, actor.Id, _tenantId);

        Assert.True(created.IsSuccess, created.Error?.Message);
        Assert.Contains(created.Value!.Permissions, permission => permission.Id == projectView.Id);
        Assert.Contains(created.Value.Permissions, permission => permission.Id == projectEdit.Id);
        var persistedRole = await _context.Roles.Include(role => role.Permissions).SingleAsync(role => role.Id == created.Value.Id);
        Assert.Contains(persistedRole.Permissions, permission => permission.Id == projectView.Id);
        Assert.Contains(persistedRole.Permissions, permission => permission.Id == projectEdit.Id);

        var escalation = await service.CreateAsync(new SaveRoleDto
        {
            Name = "Unauthorized Project Role",
            Category = RoleCategories.Management,
            PermissionIds = [projectView.Id, projectDelete.Id]
        }, actor.Id, _tenantId);
        Assert.True(escalation.IsFailure);
        Assert.Equal(403, escalation.Error?.StatusCode);
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
        var create = await service.CreateAsync(new SaveRoleDto { Name = "new-role", PermissionIds = [] }, actor.Id, _tenantId);
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
    public async Task Tenant_administrator_scope_cannot_delegate_permissions_outside_the_effective_set()
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
        targetRole.Permissions.Add(projectView);
        _context.Roles.Add(targetRole);
        await _context.SaveChangesAsync();

        var service = CreateRoleService(actor.Id);
        Assert.True((await service.GetManageableRolesAsync(actor.Id, _tenantId)).IsSuccess);
        Assert.True((await service.GetByIdAsync(targetRole.Id, actor.Id, _tenantId)).IsSuccess);
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
    public async Task Read_only_role_detail_excludes_permissions_from_other_tenants()
    {
        var (actor, actorRole) = AddUserWithRole(_tenantId, "scoped-role-viewer");
        actorRole.Permissions.Add(AddPermission(_tenantId, "Roles", "View"));

        var projectView = AddPermission(_tenantId, "Project", "View");
        var foreignPermission = AddPermission(Guid.NewGuid(), "Project", "Delete");
        var targetRole = new Role { TenantId = _tenantId, Name = "scoped-target", CreatedBy = "test" };
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
        var (actor, actorRole) = AddUserWithRole(_tenantId, "role-admin");
        var roleView = AddPermission(_tenantId, "Roles", "View");
        var roleDelete = AddPermission(_tenantId, "Roles", "Delete");
        var projectView = AddPermission(_tenantId, "Project", "View");
        actorRole.Permissions.Add(roleView);
        actorRole.Permissions.Add(roleDelete);
        actorRole.Permissions.Add(projectView);
        var systemRole = new Role { TenantId = _tenantId, Name = "system-role", IsSystem = true, CreatedBy = "test" };
        systemRole.Permissions.Add(projectView);
        _context.Roles.Add(systemRole);
        var (assignedUser, assignedRole) = AddUserWithRole(_tenantId, "assigned");
        assignedRole.Name = "custom-role";
        assignedRole.Permissions.Add(projectView);
        await _context.SaveChangesAsync();

        var service = CreateRoleService(actor.Id);
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
        var (actor, actorRole) = AddUserWithRole(_tenantId, "role-admin");
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
    public async Task Protected_system_admin_role_rejects_and_audits_mandatory_permission_removal()
    {
        var (actor, adminRole) = AddUserWithRole(_tenantId, "admin");
        adminRole.IsSystem = true;
        adminRole.CreatedBy = "System";
        adminRole.AssignSystemRoleKey(Role.TenantAdminSystemRoleKey, _tenantId);
        var mandatoryPermissions = ProtectedTenantAdminPolicy.MandatoryPermissionKeys
            .ToDictionary(key => key, key =>
            {
                var definition = PermissionCatalog.ByKey[key];
                return AddPermission(_tenantId, definition.Resource, definition.Action);
            }, StringComparer.Ordinal);
        foreach (var permission in mandatoryPermissions.Values)
            adminRole.Permissions.Add(permission);
        await _context.SaveChangesAsync();

        var omittedPermission = mandatoryPermissions["Roles.Delete"];
        var requestedIds = mandatoryPermissions.Values
            .Where(permission => permission.Id != omittedPermission.Id)
            .Select(permission => permission.Id)
            .ToList();

        var rejected = await CreateRoleService(actor.Id).UpdateAsync(adminRole.Id, new SaveRoleDto
        {
            Name = adminRole.Name,
            PermissionIds = requestedIds
        }, actor.Id, _tenantId);

        Assert.True(rejected.IsFailure);
        Assert.Equal(409, rejected.Error?.StatusCode);
        Assert.Contains(await _context.Roles.Include(role => role.Permissions)
            .Where(role => role.Id == adminRole.Id).SelectMany(role => role.Permissions).ToListAsync(),
            permission => permission.Id == omittedPermission.Id);
        Assert.Single(await _context.AuditEntries.Where(entry => entry.EntityId == adminRole.Id
            && entry.Action == "ProtectedAdminBaselineMutationRejected").ToListAsync());
    }

    [Fact]
    public async Task Unassigned_custom_role_is_soft_deleted_and_audited()
    {
        var (actor, actorRole) = AddUserWithRole(_tenantId, "role-admin");
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
        var (actor, actorRole) = AddUserWithRole(_tenantId, "role-admin");
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
            Category = RoleCategories.Management,
            PermissionIds = [inactiveProgramManage.Id]
        }, actor.Id, _tenantId);

        Assert.True(rejected.IsFailure);
        Assert.Equal(400, rejected.Error?.StatusCode);

        var existingRole = new Role { TenantId = _tenantId, Name = "Existing legacy role", CreatedBy = "legacy-seed" };
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
        var (actor, actorRole) = AddUserWithRole(_tenantId, "delegation-admin");
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
        var recipientEffective = await effectivePermissions.GetEffectivePermissionsAsync(recipient.Id, _tenantId);
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
        recipientEffective = await effectivePermissions.GetEffectivePermissionsAsync(recipient.Id, _tenantId);
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

        recipientEffective = await effectivePermissions.GetEffectivePermissionsAsync(recipient.Id, _tenantId);
        Assert.Contains("ProjectSettings.View", recipientEffective);
        Assert.DoesNotContain("ProjectSettings.Edit", recipientEffective);
        Assert.DoesNotContain("ProjectFinancialFoundation.View", recipientEffective);
        Assert.Equal(2, await _context.AuditEntries.CountAsync(entry => entry.EntityType == "Role" && entry.EntityId == created.Value.Id));
    }

    [Fact]
    public async Task Role_permission_catalog_returns_every_active_tenant_role_permission_with_scope_and_actor_delegation()
    {
        var (actor, actorRole) = AddUserWithRole(_tenantId, "catalog-admin");
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
        Assert.True(catalog.Value.Single(item => item.Name == "ProjectFinancialFoundation.View").CanDelegate == true);
        Assert.False(catalog.Value.Single(item => item.Name == "Budget.Delete").CanDelegate == true);
    }

    [Fact]
    public async Task Finance_bootstrap_requires_manage_tenant_scope_and_rejects_foreign_permission_ids()
    {
        var (actor, actorRole) = AddUserWithRole(_tenantId, "limited-delegation-admin");
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

        var foreignPermission = AddPermission(Guid.NewGuid(), "ProjectFinancialFoundation", "View");
        await _context.SaveChangesAsync();
        var foreignGrant = await service.CreateAsync(new SaveRoleDto
        {
            Name = "Foreign finance grant",
            PermissionIds = [foreignPermission.Id]
        }, actor.Id, _tenantId);
        Assert.True(foreignGrant.IsFailure);
        Assert.Equal(400, foreignGrant.Error?.StatusCode);
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
        return new RoleAdministrationService(_context, new UserEffectivePermissionService(_context), audit.Object, http);
    }

    private (User User, Role Role) AddUserWithRole(Guid tenantId, string roleName)
    {
        var user = new User { TenantId = tenantId, FirstName = roleName, LastName = "User", Email = $"{roleName}-{Guid.NewGuid():N}@test.local", CreatedBy = "test" };
        var role = new Role { TenantId = tenantId, Name = roleName, CreatedBy = "test" };
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
