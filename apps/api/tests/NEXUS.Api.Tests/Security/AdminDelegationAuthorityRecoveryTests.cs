using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.Identity.Application;
using NEXUS.Modules.Identity.Domain;
using NEXUS.Modules.Identity.Infrastructure;
using NEXUS.SharedKernel;
using Xunit;

namespace NEXUS.Api.Tests.Security;

public sealed class AdminDelegationAuthorityRecoveryTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private DelegationTestDbContext _context = null!;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _foreignTenantId = Guid.NewGuid();
    private readonly Guid _organizationId = Guid.NewGuid();

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        var options = new DbContextOptionsBuilder<DelegationTestDbContext>()
            .UseSqlite(_connection)
            .Options;
        _context = new DelegationTestDbContext(options);
        await _context.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task DEL01_Admin_creates_user_without_roles()
    {
        var admin = AddProtectedTenantAdmin();
        await _context.SaveChangesAsync();

        var result = await CreateUserService(admin.Id).CreateAsync(new CreateUserDto
        {
            Email = "new-user@test.local",
            FirstName = "New",
            LastName = "User",
            IsActive = true
        }, admin.Id.ToString("D"));

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.NotNull(await _context.Users.IgnoreQueryFilters().SingleOrDefaultAsync(u => u.Email == "new-user@test.local"));
    }

    [Fact]
    public async Task DEL02_Admin_creates_user_with_eligible_ordinary_role()
    {
        var admin = AddProtectedTenantAdmin();
        var projectView = EnsurePermission("Project.View");
        var ordinaryRole = AddCustomRole("Project Reader", projectView);
        await _context.SaveChangesAsync();

        var result = await CreateUserService(admin.Id).CreateAsync(new CreateUserDto
        {
            Email = "reader@test.local",
            FirstName = "Project",
            LastName = "Reader",
            IsActive = true,
            RoleIds = [ordinaryRole.Id]
        }, admin.Id.ToString("D"));

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Contains(result.Value!.User.Roles, role => role.Name == "Project Reader");
    }

    [Fact]
    public async Task DEL02A_Assignment_authority_lists_eligible_canonical_Project_Manager_without_Roles_View()
    {
        var admin = AddProtectedTenantAdmin();
        var adminRole = _context.Roles.Local.Single(role => role.SystemRoleKey == Role.TenantAdminSystemRoleKey);
        adminRole.Permissions.Remove(adminRole.Permissions.Single(permission => permission.Name == "Roles.View"));
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
        projectManager.Permissions.Add(EnsurePermission("Project.View"));
        projectManager.Permissions.Add(EnsurePermission("Project.Edit"));
        _context.Roles.Add(projectManager);
        await _context.SaveChangesAsync();

        var result = await CreateRoleService(admin.Id).GetAssignableRolesAsync(admin.Id, _tenantId);

        Assert.True(result.IsSuccess, result.Error?.Message);
        var selectedRole = Assert.Single(result.Value!, role => role.RoleKey == "tenant.project-manager");
        Assert.Equal(projectManager.Id, selectedRole.Id);
        Assert.Equal("Management", selectedRole.Category);
        Assert.False(selectedRole.IsSystem);
        Assert.False(selectedRole.IsProtected);
        Assert.DoesNotContain(result.Value!, role => role.RoleKey == Role.TenantAdminSystemRoleKey);
        Assert.DoesNotContain("Roles.View", await new UserEffectivePermissionService(_context)
            .GetEffectivePermissionsAsync(admin.Id, _tenantId));
    }

    [Fact]
    public async Task DEL03_Role_contains_optional_permission_Admin_does_not_possess()
    {
        var admin = AddProtectedTenantAdmin();
        var projectView = EnsurePermission("Project.View");
        var projectEdit = EnsurePermission("Project.Edit");
        var ordinaryRole = AddCustomRole("Project Editor", projectView, projectEdit);
        await _context.SaveChangesAsync();

        var result = await CreateUserService(admin.Id).CreateAsync(new CreateUserDto
        {
            Email = "editor@test.local",
            FirstName = "Project",
            LastName = "Editor",
            IsActive = true,
            RoleIds = [ordinaryRole.Id]
        }, admin.Id.ToString("D"));

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Contains(result.Value!.User.Roles, role => role.Name == "Project Editor");
    }

    [Fact]
    public async Task DEL03A_Protected_Tenant_Admin_assigns_canonical_Project_Manager_with_unheld_permission_without_receiving_it()
    {
        var admin = AddProtectedTenantAdmin();
        var projectView = EnsurePermission("Project.View");
        var projectEdit = EnsurePermission("Project.Edit");
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
        projectManager.Permissions.Add(projectView);
        projectManager.Permissions.Add(projectEdit);
        _context.Roles.Add(projectManager);
        await _context.SaveChangesAsync();

        var effectiveBefore = await new UserEffectivePermissionService(_context)
            .GetEffectivePermissionsAsync(admin.Id, _tenantId);
        Assert.DoesNotContain("Project.Edit", effectiveBefore);

        var result = await CreateUserService(admin.Id).CreateAsync(new CreateUserDto
        {
            Email = "project-manager@test.local",
            FirstName = "Project",
            LastName = "Manager",
            IsActive = true,
            RoleIds = [projectManager.Id]
        }, admin.Id.ToString("D"));

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Contains(result.Value!.User.Roles, role => role.Name == projectManager.Name);
        Assert.False(projectManager.IsSystem);
        Assert.Equal("tenant.project-manager", projectManager.RoleKey);
        Assert.Null(projectManager.SystemRoleKey);
        Assert.Equal(1, await _context.UserRoles.IgnoreQueryFilters().CountAsync(item =>
            item.TenantId == _tenantId
            && item.UserId == result.Value.User.Id
            && item.RoleId == projectManager.Id
            && !item.IsDeleted));

        var targetPermissions = await new UserEffectivePermissionService(_context)
            .GetEffectivePermissionsForOrganizationAsync(result.Value.User.Id, _tenantId, _organizationId);
        Assert.Contains("Project.Edit", targetPermissions);
        var effectiveAfter = await new UserEffectivePermissionService(_context)
            .GetEffectivePermissionsAsync(admin.Id, _tenantId);
        Assert.DoesNotContain("Project.Edit", effectiveAfter);
        Assert.Contains(await _context.AuditEntries.IgnoreQueryFilters().ToListAsync(), entry =>
            entry.EntityType == "User"
            && entry.EntityId == result.Value.User.Id
            && entry.UserId == admin.Id);
    }

    [Fact]
    public async Task DEL04_Admin_configures_ordinary_role_with_unheld_operational_permission()
    {
        var admin = AddProtectedTenantAdmin();
        var projectView = EnsurePermission("Project.View");
        var projectEdit = EnsurePermission("Project.Edit");
        await _context.SaveChangesAsync();

        var service = CreateRoleService(admin.Id);
        var result = await service.CreateAsync(new SaveRoleDto
        {
            Name = "Configured Without Possession",
            Category = RoleCategories.Management,
            OrganizationId = _organizationId,
            PermissionIds = [projectView.Id, projectEdit.Id]
        }, admin.Id, _tenantId);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Contains(result.Value!.Permissions, p => p.Name == "Project.Edit");
    }

    [Fact]
    public async Task DEL04A_Protected_Tenant_Admin_can_directly_delegate_an_unheld_eligible_permission_without_receiving_it()
    {
        var admin = AddProtectedTenantAdmin();
        var adminRole = _context.Roles.Local.Single(role => role.SystemRoleKey == Role.TenantAdminSystemRoleKey);
        adminRole.Permissions.Add(EnsurePermission("UserPermissions.Manage"));
        var target = AddUser("override-target");
        var projectView = EnsurePermission("Project.View");
        await _context.SaveChangesAsync();

        var result = await CreateUserService(admin.Id).UpdateUserPermissionsAsync(
            target.Id,
            _tenantId,
            new UpdateUserPermissionsDto
            {
                Overrides = [new UserPermissionOverrideInput
                {
                    PermissionId = projectView.Id,
                    Effect = PermissionOverrideEffect.Allow
                }]
            },
            admin.Id.ToString("D"));

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.True(result.Value!.Single(item => item.Id == projectView.Id).CanDelegate);
        var targetPermissions = await new UserEffectivePermissionService(_context)
            .GetEffectivePermissionsAsync(target.Id, _tenantId);
        Assert.Contains("Project.View", targetPermissions);
        var adminPermissions = await new UserEffectivePermissionService(_context)
            .GetEffectivePermissionsAsync(admin.Id, _tenantId);
        Assert.DoesNotContain("Project.View", adminPermissions);
        Assert.Equal(PermissionOverrideEffect.Allow, await _context.UserPermissionOverrides.IgnoreQueryFilters()
            .Where(item => item.TenantId == _tenantId && item.UserId == target.Id && item.PermissionId == projectView.Id && !item.IsDeleted)
            .Select(item => item.Effect)
            .SingleAsync());
    }

    [Fact]
    public async Task DEL04D_Direct_allow_for_action_without_effective_dependencies_is_denied_atomically()
    {
        var admin = AddProtectedTenantAdmin();
        var adminRole = _context.Roles.Local.Single(role => role.SystemRoleKey == Role.TenantAdminSystemRoleKey);
        adminRole.Permissions.Add(EnsurePermission("UserPermissions.Manage"));
        var target = AddUser("override-dependency-target");
        var projectEdit = EnsurePermission("Project.Edit");
        await _context.SaveChangesAsync();

        var result = await CreateUserService(admin.Id).UpdateUserPermissionsAsync(
            target.Id,
            _tenantId,
            new UpdateUserPermissionsDto
            {
                Overrides = [new UserPermissionOverrideInput
                {
                    PermissionId = projectEdit.Id,
                    Effect = PermissionOverrideEffect.Allow
                }]
            },
            admin.Id.ToString("D"));

        Assert.True(result.IsFailure);
        Assert.Equal(400, result.Error!.StatusCode);
        Assert.Contains("Project.View", result.Error.Message);
        Assert.Empty(await _context.UserPermissionOverrides.IgnoreQueryFilters()
            .Where(item => item.TenantId == _tenantId && item.UserId == target.Id && !item.IsDeleted)
            .ToListAsync());
        Assert.DoesNotContain("Project.Edit", await new UserEffectivePermissionService(_context)
            .GetEffectivePermissionsAsync(target.Id, _tenantId));
    }

    [Fact]
    public async Task DEL04E_Direct_allow_with_all_effective_dependencies_is_atomic_and_does_not_escalate_Admin()
    {
        var admin = AddProtectedTenantAdmin();
        var adminRole = _context.Roles.Local.Single(role => role.SystemRoleKey == Role.TenantAdminSystemRoleKey);
        adminRole.Permissions.Add(EnsurePermission("UserPermissions.Manage"));
        var target = AddUser("override-dependency-target");
        var projectView = EnsurePermission("Project.View");
        var projectEdit = EnsurePermission("Project.Edit");
        await _context.SaveChangesAsync();

        var result = await CreateUserService(admin.Id).UpdateUserPermissionsAsync(
            target.Id,
            _tenantId,
            new UpdateUserPermissionsDto
            {
                Overrides =
                [
                    new UserPermissionOverrideInput
                    {
                        PermissionId = projectView.Id,
                        Effect = PermissionOverrideEffect.Allow
                    },
                    new UserPermissionOverrideInput
                    {
                        PermissionId = projectEdit.Id,
                        Effect = PermissionOverrideEffect.Allow
                    }
                ]
            },
            admin.Id.ToString("D"));

        Assert.True(result.IsSuccess, result.Error?.Message);
        var targetPermissions = await new UserEffectivePermissionService(_context)
            .GetEffectivePermissionsAsync(target.Id, _tenantId);
        Assert.Contains("Project.View", targetPermissions);
        Assert.Contains("Project.Edit", targetPermissions);
        var adminPermissions = await new UserEffectivePermissionService(_context)
            .GetEffectivePermissionsAsync(admin.Id, _tenantId);
        Assert.DoesNotContain("Project.View", adminPermissions);
        Assert.DoesNotContain("Project.Edit", adminPermissions);
        Assert.Equal(2, await _context.UserPermissionOverrides.IgnoreQueryFilters().CountAsync(item =>
            item.TenantId == _tenantId && item.UserId == target.Id && !item.IsDeleted
            && item.Effect == PermissionOverrideEffect.Allow));
    }

    [Fact]
    public async Task DEL04F_Granting_a_missing_view_does_not_silently_reactivate_an_inherited_action()
    {
        var admin = AddProtectedTenantAdmin();
        var adminRole = _context.Roles.Local.Single(role => role.SystemRoleKey == Role.TenantAdminSystemRoleKey);
        adminRole.Permissions.Add(EnsurePermission("UserPermissions.Manage"));
        var target = AddUser("inherited-action-target");
        var projectView = EnsurePermission("Project.View");
        var projectEdit = EnsurePermission("Project.Edit");
        var targetRole = AddCustomRole("Inherited Project Editor", projectView, projectEdit);
        _context.UserRoles.Add(new UserRole
        {
            TenantId = _tenantId,
            UserId = target.Id,
            RoleId = targetRole.Id,
            CreatedBy = "test"
        });
        _context.UserPermissionOverrides.Add(new UserPermissionOverride
        {
            TenantId = _tenantId,
            UserId = target.Id,
            PermissionId = projectView.Id,
            Effect = PermissionOverrideEffect.Deny,
            CreatedBy = "test"
        });
        await _context.SaveChangesAsync();

        var effectiveBefore = await new UserEffectivePermissionService(_context)
            .GetEffectivePermissionsAsync(target.Id, _tenantId);
        Assert.DoesNotContain("Project.View", effectiveBefore);
        Assert.DoesNotContain("Project.Edit", effectiveBefore);

        var service = CreateUserService(admin.Id);
        var prerequisiteOnly = await service.UpdateUserPermissionsAsync(
            target.Id,
            _tenantId,
            new UpdateUserPermissionsDto
            {
                Overrides = [new UserPermissionOverrideInput
                {
                    PermissionId = projectView.Id,
                    Effect = PermissionOverrideEffect.Allow
                }]
            },
            admin.Id.ToString("D"));

        Assert.True(prerequisiteOnly.IsFailure);
        Assert.Equal(400, prerequisiteOnly.Error!.StatusCode);
        Assert.Contains("Project.Edit", prerequisiteOnly.Error.Message);
        Assert.Equal(PermissionOverrideEffect.Deny, await _context.UserPermissionOverrides.IgnoreQueryFilters()
            .Where(item => item.TenantId == _tenantId && item.UserId == target.Id
                && item.PermissionId == projectView.Id && !item.IsDeleted)
            .Select(item => item.Effect)
            .SingleAsync());
        Assert.DoesNotContain("Project.Edit", await new UserEffectivePermissionService(_context)
            .GetEffectivePermissionsAsync(target.Id, _tenantId));

        var explicitActionAndDependency = await service.UpdateUserPermissionsAsync(
            target.Id,
            _tenantId,
            new UpdateUserPermissionsDto
            {
                Overrides =
                [
                    new UserPermissionOverrideInput
                    {
                        PermissionId = projectView.Id,
                        Effect = PermissionOverrideEffect.Allow
                    },
                    new UserPermissionOverrideInput
                    {
                        PermissionId = projectEdit.Id,
                        Effect = PermissionOverrideEffect.Allow
                    }
                ]
            },
            admin.Id.ToString("D"));

        Assert.True(explicitActionAndDependency.IsSuccess, explicitActionAndDependency.Error?.Message);
        var finalPermissions = await new UserEffectivePermissionService(_context)
            .GetEffectivePermissionsAsync(target.Id, _tenantId);
        Assert.Contains("Project.View", finalPermissions);
        Assert.Contains("Project.Edit", finalPermissions);
    }

    [Fact]
    public async Task DEL04B_Protected_Tenant_Admin_cannot_directly_override_own_permission()
    {
        var admin = AddProtectedTenantAdmin();
        var adminRole = _context.Roles.Local.Single(role => role.SystemRoleKey == Role.TenantAdminSystemRoleKey);
        adminRole.Permissions.Add(EnsurePermission("UserPermissions.Manage"));
        var projectEdit = EnsurePermission("Project.Edit");
        await _context.SaveChangesAsync();

        var result = await CreateUserService(admin.Id).UpdateUserPermissionsAsync(
            admin.Id,
            _tenantId,
            new UpdateUserPermissionsDto
            {
                Overrides = [new UserPermissionOverrideInput
                {
                    PermissionId = projectEdit.Id,
                    Effect = PermissionOverrideEffect.Allow
                }]
            },
            admin.Id.ToString("D"));

        Assert.True(result.IsFailure);
        Assert.Equal(403, result.Error?.StatusCode);
        Assert.False(await _context.UserPermissionOverrides.IgnoreQueryFilters()
            .AnyAsync(item => item.TenantId == _tenantId && item.UserId == admin.Id && item.PermissionId == projectEdit.Id && !item.IsDeleted));
    }

    [Fact]
    public async Task DEL04C_Grantability_is_not_locked_by_the_legacy_Tenant_Admin_baseline()
    {
        var admin = AddProtectedTenantAdmin();
        var adminRole = _context.Roles.Local.Single(role => role.SystemRoleKey == Role.TenantAdminSystemRoleKey);
        adminRole.Permissions.Add(EnsurePermission("UserPermissions.Manage"));
        var target = AddUser("baseline-override-target");
        var protectedCore = EnsurePermission("Roles.View");
        await _context.SaveChangesAsync();
        var service = CreateUserService(admin.Id);

        var permissions = await service.GetUserPermissionsAsync(target.Id, _tenantId);
        Assert.True(permissions.IsSuccess, permissions.Error?.Message);
        var assignment = Assert.Single(permissions.Value!, item => item.Id == protectedCore.Id);
        Assert.True(assignment.IsAssignable);
        Assert.True(assignment.CanDelegate);

        var result = await service.UpdateUserPermissionsAsync(
            target.Id,
            _tenantId,
            new UpdateUserPermissionsDto
            {
                Overrides = [new UserPermissionOverrideInput
                {
                    PermissionId = protectedCore.Id,
                    Effect = PermissionOverrideEffect.Deny
                }]
            },
            admin.Id.ToString("D"));

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.True(await _context.UserPermissionOverrides.IgnoreQueryFilters()
            .AnyAsync(item => item.TenantId == _tenantId && item.UserId == target.Id
                && item.PermissionId == protectedCore.Id && !item.IsDeleted));
    }

    [Fact]
    public async Task DEL05_Ordinary_delegated_administrator_within_authority()
    {
        var (delegator, delegatorRole) = AddUserWithRole("delegator");
        delegatorRole.Permissions.Add(EnsurePermission("Users.ManageAccounts"));
        delegatorRole.Permissions.Add(EnsurePermission("Users.View"));
        delegatorRole.Permissions.Add(EnsurePermission("Users.AssignRoles"));
        delegatorRole.Permissions.Add(EnsurePermission("Roles.View"));
        delegatorRole.Permissions.Add(EnsurePermission("Roles.ConfigurePermissions"));
        var ordinaryRole = AddCustomRole("Delegated Reader", EnsurePermission("Project.View"));
        await _context.SaveChangesAsync();

        // Delegate role assignment authority first (without possessing Project.View).
        var result = await CreateUserService(delegator.Id).UpdateAsync(delegator.Id, new UpdateUserDto
        {
            Email = delegator.Email,
            FirstName = delegator.FirstName,
            LastName = delegator.LastName,
            IsActive = true,
            RoleIds = [delegatorRole.Id, ordinaryRole.Id]
        }, delegator.Id.ToString("D"));

        Assert.True(result.IsFailure);
        Assert.Equal(403, result.Error?.StatusCode);

        // Within authority: delegator with role-configuration authority can assign eligible ordinary custom role to another user.
        var target = AddUser("delegated-target");
        await _context.SaveChangesAsync();
        var delegated = await CreateUserService(delegator.Id).UpdateAsync(target.Id, new UpdateUserDto
        {
            Email = target.Email,
            FirstName = target.FirstName,
            LastName = target.LastName,
            IsActive = true,
            RoleIds = [ordinaryRole.Id]
        }, delegator.Id.ToString("D"));

        Assert.True(delegated.IsSuccess, delegated.Error?.Message);
        Assert.Contains(delegated.Value!.Roles, role => role.Name == ordinaryRole.Name);
    }

    [Fact]
    public async Task DEL06_Ordinary_actor_outside_delegation_authority()
    {
        var (actor, actorRole) = AddUserWithRole("limited");
        actorRole.Permissions.Add(EnsurePermission("Users.ManageAccounts"));
        actorRole.Permissions.Add(EnsurePermission("Users.View"));
        actorRole.Permissions.Add(EnsurePermission("Users.AssignRoles"));
        var ordinaryRole = AddCustomRole("Reader", EnsurePermission("Project.View"));
        await _context.SaveChangesAsync();

        var result = await CreateUserService(actor.Id).CreateAsync(new CreateUserDto
        {
            Email = "blocked@test.local",
            FirstName = "Blocked",
            LastName = "User",
            IsActive = true,
            RoleIds = [ordinaryRole.Id]
        }, actor.Id.ToString("D"));

        Assert.True(result.IsFailure);
        Assert.Equal(403, result.Error?.StatusCode);
    }

    [Fact]
    public async Task DEL07_Unauthorized_self_escalation_through_role_assignment_denied()
    {
        var admin = AddProtectedTenantAdmin();
        var projectView = EnsurePermission("Project.View");
        var projectEdit = EnsurePermission("Project.Edit");
        var ordinaryRole = AddCustomRole("Self Escalation", projectView, projectEdit);
        await _context.SaveChangesAsync();

        var result = await CreateUserService(admin.Id).UpdateAsync(admin.Id, new UpdateUserDto
        {
            Email = admin.Email,
            FirstName = admin.FirstName,
            LastName = admin.LastName,
            IsActive = true,
            RoleIds = [ordinaryRole.Id]
        }, admin.Id.ToString("D"));

        Assert.True(result.IsFailure);
        Assert.Equal(403, result.Error?.StatusCode);
    }

    [Fact]
    public async Task DEL08_Protected_Tenant_Admin_role_assignment_remains_denied()
    {
        var admin = AddProtectedTenantAdmin();
        var target = AddUser("target");
        var protectedAdminRole = AddSystemRole("admin", Role.TenantAdminSystemRoleKey);
        await _context.SaveChangesAsync();

        var result = await CreateUserService(admin.Id).UpdateAsync(target.Id, new UpdateUserDto
        {
            Email = target.Email,
            FirstName = target.FirstName,
            LastName = target.LastName,
            IsActive = true,
            RoleIds = [protectedAdminRole.Id]
        }, admin.Id.ToString("D"));

        Assert.True(result.IsFailure);
        Assert.Equal(403, result.Error?.StatusCode);
    }

    [Fact]
    public async Task DEL10_Cross_tenant_user_or_role_denied()
    {
        var admin = AddProtectedTenantAdmin();
        var foreignUser = AddUser("foreign", _foreignTenantId);
        var role = AddCustomRole("CrossTenantRole", EnsurePermission("Project.View"));
        await _context.SaveChangesAsync();

        var result = await CreateUserService(admin.Id).UpdateAsync(foreignUser.Id, new UpdateUserDto
        {
            Email = foreignUser.Email,
            FirstName = foreignUser.FirstName,
            LastName = foreignUser.LastName,
            IsActive = true,
            RoleIds = [role.Id]
        }, admin.Id.ToString("D"));

        Assert.True(result.IsFailure);
        Assert.True(result.Error?.StatusCode is 403 or 404);
    }

    [Fact]
    public async Task DEL11_Direct_deny_and_multi_role_effective_permissions_precedence()
    {
        var target = AddUser("target");
        var view = EnsurePermission("Project.View");
        var edit = EnsurePermission("Project.Edit");
        var roleA = AddCustomRole("RoleA", view);
        var roleB = AddCustomRole("RoleB", view, edit);
        _context.UserRoles.Add(new UserRole { TenantId = _tenantId, UserId = target.Id, RoleId = roleA.Id, CreatedBy = "test" });
        _context.UserRoles.Add(new UserRole { TenantId = _tenantId, UserId = target.Id, RoleId = roleB.Id, CreatedBy = "test" });
        _context.UserPermissionOverrides.Add(new UserPermissionOverride
        {
            TenantId = _tenantId,
            UserId = target.Id,
            PermissionId = edit.Id,
            Effect = PermissionOverrideEffect.Deny,
            CreatedBy = "test"
        });
        await _context.SaveChangesAsync();

        var effective = await new UserEffectivePermissionService(_context).GetEffectivePermissionsAsync(target.Id, _tenantId);
        Assert.Contains("Project.View", effective);
        Assert.DoesNotContain("Project.Edit", effective);
    }

    [Fact]
    public async Task DEL12_Last_admin_removal_or_loss_of_protected_core_denied()
    {
        var admin = AddProtectedTenantAdmin();
        var replacement = AddCustomRole("Replacement", EnsurePermission("Project.View"));
        await _context.SaveChangesAsync();

        var result = await CreateUserService(admin.Id).UpdateAsync(admin.Id, new UpdateUserDto
        {
            Email = admin.Email,
            FirstName = admin.FirstName,
            LastName = admin.LastName,
            IsActive = true,
            RoleIds = [replacement.Id]
        }, admin.Id.ToString("D"));

        Assert.True(result.IsFailure);
        Assert.True(result.Error?.StatusCode is 403 or 409);
    }

    [Fact]
    public async Task DEL13_Concurrent_authority_revocation_during_mutation_revalidated()
    {
        var (actor, actorRole) = AddUserWithRole("stale-assigner");
        actorRole.Permissions.Add(EnsurePermission("Users.ManageAccounts"));
        actorRole.Permissions.Add(EnsurePermission("Users.View"));
        actorRole.Permissions.Add(EnsurePermission("Users.AssignRoles"));
        var ordinaryRole = AddCustomRole("Reader", EnsurePermission("Project.View"));
        await _context.SaveChangesAsync();

        var effective = new Mock<IUserEffectivePermissionService>();
        effective.SetupSequence(s => s.GetEffectivePermissionsAsync(actor.Id, _tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>(["Users.ManageAccounts", "Users.View", "Users.AssignRoles"], StringComparer.Ordinal))
            .ReturnsAsync(new HashSet<string>(["Users.View"], StringComparer.Ordinal));
        effective.Setup(s => s.GetEffectivePermissionsAsync(It.Is<Guid>(id => id != actor.Id), _tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>(StringComparer.Ordinal));
        effective.Setup(s => s.GetEffectivePermissionsForOrganizationAsync(actor.Id, _tenantId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>(["Users.AssignRoles", "Users.View", "Users.ManageAccounts", "Project.View"], StringComparer.Ordinal));
        var audit = new Mock<IAuditService>();
        audit.Setup(s => s.AppendAsync(It.IsAny<AuditEntry>(), It.IsAny<CancellationToken>()))
            .Callback<AuditEntry, CancellationToken>((e, _) => _context.AuditEntries.Add(e))
            .ReturnsAsync(Result.Success());
        var service = CreateUserService(actor.Id, effective.Object, audit.Object);

        var result = await service.CreateAsync(new CreateUserDto
        {
            Email = "revoked@test.local",
            FirstName = "Revoked",
            LastName = "Authority",
            IsActive = true,
            RoleIds = [ordinaryRole.Id]
        }, actor.Id.ToString("D"));

        Assert.True(result.IsFailure);
        Assert.Null(await _context.Users.IgnoreQueryFilters().SingleOrDefaultAsync(u => u.Email == "revoked@test.local"));
    }

    [Fact]
    public async Task DEL14_Failed_assignment_causes_no_partial_role_records()
    {
        var admin = AddProtectedTenantAdmin();
        var target = AddUser("target");
        // Reserved/restricted-like denied case: attempt to assign a role whose permission is not delegable under policy.
        var restrictedRole = AddCustomRole("Restricted Attempt", EnsurePermission("Roles.View"));
        await _context.SaveChangesAsync();

        var beforeRoles = await _context.UserRoles.IgnoreQueryFilters().CountAsync(u => u.UserId == target.Id);
        var result = await CreateUserService(admin.Id).UpdateAsync(target.Id, new UpdateUserDto
        {
            Email = target.Email,
            FirstName = target.FirstName,
            LastName = target.LastName,
            IsActive = true,
            RoleIds = [restrictedRole.Id]
        }, admin.Id.ToString("D"));

        Assert.True(result.IsFailure);
        Assert.Equal(beforeRoles, await _context.UserRoles.IgnoreQueryFilters().CountAsync(u => u.UserId == target.Id));
    }

    [Fact]
    public async Task DEL15_Existing_legitimate_bootstrap_delegation_workflow_preserved()
    {
        var (actor, actorRole) = AddUserWithRole("bootstrap-admin");
        actorRole.Permissions.Add(EnsurePermission("Users.ManageAccounts"));
        actorRole.Permissions.Add(EnsurePermission("Users.View"));
        actorRole.Permissions.Add(EnsurePermission("Users.AssignRoles"));
        actorRole.Permissions.Add(EnsurePermission("Roles.View"));
        actorRole.Permissions.Add(EnsurePermission("Roles.ConfigurePermissions"));
        actorRole.Permissions.Add(EnsurePermission("TenantAdministration.ManageScope"));
        var planningView = EnsurePermission("Planning.View");
        var planCreate = EnsurePermission("ProjectPlan.Create");
        var bootstrapRole = AddCustomRole("Planning Bootstrap", planningView, planCreate);
        await _context.SaveChangesAsync();

        var result = await CreateUserService(actor.Id).CreateAsync(new CreateUserDto
        {
            Email = "planner@test.local",
            FirstName = "Planner",
            LastName = "User",
            IsActive = true,
            RoleIds = [bootstrapRole.Id]
        }, actor.Id.ToString("D"));

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Contains(result.Value!.User.Roles, role => role.Name == "Planning Bootstrap");
    }

    private UserAdministrationService CreateUserService(
        Guid actorId,
        IUserEffectivePermissionService? effectivePermissions = null,
        IAuditService? auditService = null)
    {
        if (auditService is null)
        {
            var audit = new Mock<IAuditService>();
            audit.Setup(s => s.AppendAsync(It.IsAny<AuditEntry>(), It.IsAny<CancellationToken>()))
                .Callback<AuditEntry, CancellationToken>((e, _) => _context.AuditEntries.Add(e))
                .ReturnsAsync(Result.Success());
            auditService = audit.Object;
        }
        var httpContext = CreateHttpContext(actorId);
        var permissions = effectivePermissions ?? new UserEffectivePermissionService(_context);
        var organizationDirectory = new Mock<IUserOrganizationDirectory>();
        organizationDirectory
            .Setup(directory => directory.GetActiveNamesAsync(_tenantId, It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid _, IEnumerable<Guid> organizationIds, CancellationToken _) =>
                (IReadOnlyDictionary<Guid, string>)organizationIds.Distinct().ToDictionary(id => id, id => id.ToString("D")));
        organizationDirectory.Setup(directory => directory.GetAssignableOptionsAsync(
                _tenantId, It.IsAny<IReadOnlySet<Guid>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid _, IReadOnlySet<Guid>? ids, CancellationToken _) =>
                (IReadOnlyList<UserOrganizationOption>)(ids ?? new HashSet<Guid> { _organizationId })
                    .Select(id => new UserOrganizationOption(id, "Test Organization")).ToArray());
        return new UserAdministrationService(
            _context,
            Mock.Of<IPasswordHasher<User>>(),
            httpContext,
            permissions,
            auditService,
            new RoleAssignmentAuthorizationService(_context, permissions, httpContext, organizationDirectory.Object),
            Options.Create(new LocalPasswordPolicyOptions()),
            organizationDirectory.Object,
            Mock.Of<IUserOrganizationMembershipIntegrity>());
    }

    private RoleAdministrationService CreateRoleService(Guid actorId)
    {
        var audit = new Mock<IAuditService>();
        audit.Setup(s => s.AppendAsync(It.IsAny<AuditEntry>(), It.IsAny<CancellationToken>()))
            .Callback<AuditEntry, CancellationToken>((e, _) => _context.AuditEntries.Add(e))
            .ReturnsAsync(Result.Success());
        var organizationDirectory = new Mock<IUserOrganizationDirectory>();
        organizationDirectory
            .Setup(directory => directory.GetActiveNamesAsync(_tenantId, It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid _, IEnumerable<Guid> organizationIds, CancellationToken _) =>
                (IReadOnlyDictionary<Guid, string>)organizationIds.Distinct().ToDictionary(id => id, id => id.ToString("D")));
        organizationDirectory.Setup(directory => directory.GetAssignableOptionsAsync(
                _tenantId, It.IsAny<IReadOnlySet<Guid>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid _, IReadOnlySet<Guid>? ids, CancellationToken _) =>
                (IReadOnlyList<UserOrganizationOption>)(ids ?? new HashSet<Guid> { _organizationId })
                    .Select(id => new UserOrganizationOption(id, "Test Organization")).ToArray());
        return new RoleAdministrationService(
            _context,
            new UserEffectivePermissionService(_context),
            audit.Object,
            CreateHttpContext(actorId),
            organizationDirectory.Object);
    }

    private HttpContextAccessor CreateHttpContext(Guid actorId) => new()
    {
        HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, actorId.ToString("D")),
                new Claim("tenantId", _tenantId.ToString("D"))
            ], "test"))
        }
    };

    private User AddProtectedTenantAdmin()
    {
        var admin = AddUser("admin");
        var adminRole = AddSystemRole("admin", Role.TenantAdminSystemRoleKey);
        EnsureAdminBaselinePermissions(adminRole);
        _context.UserRoles.Add(new UserRole { TenantId = _tenantId, UserId = admin.Id, RoleId = adminRole.Id, CreatedBy = "test" });
        return admin;
    }

    private void EnsureAdminBaselinePermissions(Role adminRole)
    {
        // Minimal identity-admin permissions needed for user/role administration flows and effective-admin checks.
        foreach (var key in new[]
        {
            "Users.ManageAccounts", "Users.View", "Users.AssignRoles", "Users.SetStatus",
            "Roles.View", "Roles.Create", "Roles.ConfigurePermissions", "Roles.EditDetails",
            "TenantAdministration.ManageScope"
        })
        {
            var permission = EnsurePermission(key);
            if (!adminRole.Permissions.Any(p => p.Id == permission.Id))
                adminRole.Permissions.Add(permission);
        }
    }

    private User AddUser(string prefix, Guid? tenantId = null)
    {
        var tid = tenantId ?? _tenantId;
        var user = new User
        {
            TenantId = tid,
            FirstName = prefix,
            LastName = "User",
            Email = $"{prefix}-{Guid.NewGuid():N}@test.local",
            IsActive = true,
            CreatedBy = "test"
        };
        _context.Users.Add(user);
        return user;
    }

    private (User User, Role Role) AddUserWithRole(string name)
    {
        var user = AddUser(name);
        var role = new Role { TenantId = _tenantId, Name = name, Category = RoleCategories.Business, CreatedBy = "test" };
        _context.Roles.Add(role);
        _context.UserRoles.Add(new UserRole { TenantId = _tenantId, UserId = user.Id, RoleId = role.Id, CreatedBy = "test" });
        return (user, role);
    }

    private Role AddCustomRole(string name, params Permission[] permissions)
    {
        var role = new Role { TenantId = _tenantId, Name = name, Category = RoleCategories.Business, IsSystem = false, CreatedBy = "test" };
        role.AssignOrganization(_organizationId);
        foreach (var p in permissions)
        {
            if (!role.Permissions.Any(x => x.Id == p.Id))
                role.Permissions.Add(p);
        }
        _context.Roles.Add(role);
        return role;
    }

    private Role AddSystemRole(string name, string systemRoleKey)
    {
        var role = new Role { TenantId = _tenantId, Name = name, Category = RoleCategories.System, IsSystem = true, CreatedBy = "test" };
        role.AssignSystemRoleKey(systemRoleKey, _tenantId);
        _context.Roles.Add(role);
        return role;
    }

    private Permission EnsurePermission(string key, Guid? tenantId = null)
    {
        if (!PermissionCatalog.ByKey.TryGetValue(key, out var definition))
            throw new InvalidOperationException($"Permission '{key}' is not in PermissionCatalog.");

        var tid = tenantId ?? _tenantId;
        var existing = _context.ChangeTracker.Entries<Permission>()
                .Select(e => e.Entity)
                .FirstOrDefault(p => p.TenantId == tid && p.Name == key)
            ?? _context.Permissions.IgnoreQueryFilters().FirstOrDefault(p => p.TenantId == tid && p.Name == key);
        if (existing is not null)
            return existing;

        var permission = new Permission
        {
            TenantId = tid,
            Name = definition.Key,
            Resource = definition.Resource,
            Action = definition.Action,
            Description = definition.DescriptionEn,
            CreatedBy = "test"
        };
        _context.Permissions.Add(permission);
        return permission;
    }
}

internal sealed class DelegationTestDbContext(DbContextOptions<DelegationTestDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<UserPermissionOverride> UserPermissionOverrides => Set<UserPermissionOverride>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>().Ignore(user => user.FullName);
        modelBuilder.Entity<Role>().HasMany(role => role.Permissions).WithMany(permission => permission.Roles);
        modelBuilder.Entity<UserRole>().HasOne(assignment => assignment.User).WithMany(user => user.UserRoles).HasForeignKey(assignment => assignment.UserId);
        modelBuilder.Entity<UserRole>().HasOne(assignment => assignment.Role).WithMany(role => role.UserRoles).HasForeignKey(assignment => assignment.RoleId);
        modelBuilder.Entity<UserPermissionOverride>().HasOne(item => item.User).WithMany(user => user.PermissionOverrides).HasForeignKey(item => item.UserId);
        modelBuilder.Entity<UserPermissionOverride>().HasOne(item => item.Permission).WithMany(permission => permission.UserOverrides).HasForeignKey(item => item.PermissionId);
        modelBuilder.Entity<AuditEntry>().Ignore(entry => entry.Activities);
    }
}
