using System.Security.Claims;
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
    public async Task Effective_permissions_apply_direct_allow_and_deny_over_role_grants()
    {
        var (user, role) = AddUserWithRole(_tenantId, "member");
        var inherited = AddPermission(_tenantId, "projects", "read");
        var denied = AddPermission(_tenantId, "projects", "write");
        var directlyAllowed = AddPermission(_tenantId, "reports", "read");
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

        Assert.Contains("projects.read", effective);
        Assert.DoesNotContain("projects.write", effective);
        Assert.Contains("reports.read", effective);
    }

    [Fact]
    public async Task Effective_permissions_ignore_cross_tenant_role_assignments_and_inactive_users()
    {
        var (user, role) = AddUserWithRole(_tenantId, "member");
        var ownPermission = AddPermission(_tenantId, "projects", "read");
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
        Assert.Contains("projects.read", activePermissions);
        Assert.DoesNotContain("admin.write", activePermissions);

        user.IsActive = false;
        await _context.SaveChangesAsync();
        Assert.Empty(await permissionService.GetEffectivePermissionsAsync(user.Id, _tenantId));
    }

    [Fact]
    public async Task Middleware_replaces_stale_token_permission_claims_with_current_permissions()
    {
        var (user, role) = AddUserWithRole(_tenantId, "member");
        var current = AddPermission(_tenantId, "projects", "read");
        role.Permissions.Add(current);
        await _context.SaveChangesAsync();

        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim("tenantId", _tenantId.ToString()),
            new Claim("permission", "projects.delete")
        ], "test");
        var httpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
        var middleware = new UserPermissionClaimsMiddleware(context =>
        {
            Assert.Contains(context.User.FindAll("permission"), claim => claim.Value == "projects.read");
            Assert.DoesNotContain(context.User.FindAll("permission"), claim => claim.Value == "projects.delete");
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(httpContext, new UserEffectivePermissionService(_context));
    }

    [Fact]
    public async Task Permission_updates_require_actor_authority_and_audit_successful_changes()
    {
        var (actor, actorRole) = AddUserWithRole(_tenantId, "admin");
        var (target, targetRole) = AddUserWithRole(_tenantId, "member");
        var manageUsers = AddPermission(_tenantId, "users", "write");
        var editProjects = AddPermission(_tenantId, "projects", "edit");
        var unauthorizedPermission = AddPermission(_tenantId, "settings", "write");
        actorRole.Permissions.Add(manageUsers);
        actorRole.Permissions.Add(editProjects);
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
                [new Claim("tenantId", _tenantId.ToString())], "test"))
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
    public async Task User_role_assignments_are_audited_and_the_actor_cannot_change_their_own_roles()
    {
        var (actor, actorRole) = AddUserWithRole(_tenantId, "administrator");
        var (target, _) = AddUserWithRole(_tenantId, "member");
        var usersWrite = AddPermission(_tenantId, "users", "write");
        var projectRead = AddPermission(_tenantId, "projects", "read");
        actorRole.Permissions.Add(usersWrite);
        actorRole.Permissions.Add(projectRead);
        var replacementRole = new Role { TenantId = _tenantId, Name = "Project Analyst", CreatedBy = "test" };
        replacementRole.Permissions.Add(projectRead);
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
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("tenantId", _tenantId.ToString())], "test"))
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
        elevatedRole.Permissions.Add(AddPermission(_tenantId, "finance", "read"));
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
        var manageUsers = AddPermission(_tenantId, "users", "write");
        var projectRead = AddPermission(_tenantId, "projects", "read");
        var projectWrite = AddPermission(_tenantId, "projects", "write");
        actorRole.Permissions.Add(manageUsers);
        actorRole.Permissions.Add(projectRead);
        actorRole.Permissions.Add(projectWrite);
        await _context.SaveChangesAsync();

        var service = CreateRoleService();
        var created = await service.CreateAsync(new SaveRoleDto
        {
            Name = "Project Reader",
            Description = "Read project data",
            Category = "Projects",
            PermissionIds = [projectRead.Id]
        }, actor.Id, _tenantId);

        Assert.True(created.IsSuccess);
        Assert.Equal(_tenantId, (await _context.Roles.SingleAsync(role => role.Id == created.Value!.Id)).TenantId);
        Assert.Equal("Project Reader", created.Value!.Name);
        Assert.Equal(projectRead.Id, Assert.Single(created.Value.Permissions).Id);

        var updated = await service.UpdateAsync(created.Value.Id, new SaveRoleDto
        {
            Name = "Project Reader",
            Description = "Read and update project data",
            Category = "Projects",
            PermissionIds = [projectRead.Id, projectWrite.Id]
        }, actor.Id, _tenantId);

        Assert.True(updated.IsSuccess);
        Assert.Equal(2, updated.Value!.PermissionCount);
        Assert.Equal("Read and update project data", updated.Value.Description);
        Assert.Equal(2, await _context.AuditEntries.CountAsync(entry => entry.EntityType == "Role" && entry.EntityId == created.Value.Id));
    }

    [Fact]
    public async Task Role_management_is_tenant_scoped_and_limits_permissions_to_actor_effective_access()
    {
        var (actor, actorRole) = AddUserWithRole(_tenantId, "role-admin");
        var manageUsers = AddPermission(_tenantId, "users", "write");
        var projectRead = AddPermission(_tenantId, "projects", "read");
        var settingsWrite = AddPermission(_tenantId, "settings", "write");
        actorRole.Permissions.Add(manageUsers);
        actorRole.Permissions.Add(projectRead);
        var foreignTenantId = Guid.NewGuid();
        var foreignPermission = AddPermission(foreignTenantId, "projects", "delete");
        var foreignRole = new Role { TenantId = foreignTenantId, Name = "foreign-role", CreatedBy = "test" };
        foreignRole.Permissions.Add(foreignPermission);
        _context.Roles.Add(foreignRole);
        await _context.SaveChangesAsync();

        var service = CreateRoleService();
        var catalog = await service.GetAssignablePermissionsAsync(actor.Id, _tenantId);
        Assert.True(catalog.IsSuccess);
        Assert.Contains(catalog.Value!, item => item.Id == projectRead.Id);
        Assert.DoesNotContain(catalog.Value!, item => item.Id == settingsWrite.Id);
        Assert.DoesNotContain(catalog.Value!, item => item.Id == foreignPermission.Id);

        var crossTenantRole = await service.GetByIdAsync(foreignRole.Id, actor.Id, _tenantId);
        Assert.True(crossTenantRole.IsFailure);
        Assert.Equal(404, crossTenantRole.Error?.StatusCode);

        var escalation = await service.CreateAsync(new SaveRoleDto
        {
            Name = "Escalated role",
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
    public async Task System_roles_cannot_be_renamed_or_deleted_and_assigned_custom_roles_cannot_be_deleted()
    {
        var (actor, actorRole) = AddUserWithRole(_tenantId, "role-admin");
        var manageUsers = AddPermission(_tenantId, "users", "write");
        var projectRead = AddPermission(_tenantId, "projects", "read");
        actorRole.Permissions.Add(manageUsers);
        actorRole.Permissions.Add(projectRead);
        var systemRole = new Role { TenantId = _tenantId, Name = "system-role", IsSystem = true, CreatedBy = "test" };
        systemRole.Permissions.Add(projectRead);
        _context.Roles.Add(systemRole);
        var (assignedUser, assignedRole) = AddUserWithRole(_tenantId, "assigned");
        assignedRole.Name = "custom-role";
        assignedRole.Permissions.Add(projectRead);
        await _context.SaveChangesAsync();

        var service = CreateRoleService();
        var renameSystemRole = await service.UpdateAsync(systemRole.Id, new SaveRoleDto
        {
            Name = "renamed-system-role",
            PermissionIds = [projectRead.Id]
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
        var manageUsers = AddPermission(_tenantId, "users", "write");
        actorRole.Permissions.Add(manageUsers);
        await _context.SaveChangesAsync();

        var result = await CreateRoleService().UpdateAsync(actorRole.Id, new SaveRoleDto
        {
            Name = actorRole.Name,
            PermissionIds = []
        }, actor.Id, _tenantId);

        Assert.True(result.IsFailure);
        Assert.Equal(409, result.Error?.StatusCode);
        Assert.Contains((await _context.Roles.Include(role => role.Permissions).SingleAsync(role => role.Id == actorRole.Id)).Permissions,
            permission => permission.Name == "users.write");
    }

    [Fact]
    public async Task Unassigned_custom_role_is_soft_deleted_and_audited()
    {
        var (actor, actorRole) = AddUserWithRole(_tenantId, "role-admin");
        var manageUsers = AddPermission(_tenantId, "users", "write");
        var projectRead = AddPermission(_tenantId, "projects", "read");
        actorRole.Permissions.Add(manageUsers);
        actorRole.Permissions.Add(projectRead);
        var role = new Role { TenantId = _tenantId, Name = "unused-role", CreatedBy = "test" };
        role.Permissions.Add(projectRead);
        _context.Roles.Add(role);
        await _context.SaveChangesAsync();

        var result = await CreateRoleService().DeleteAsync(role.Id, actor.Id, _tenantId);

        Assert.True(result.IsSuccess);
        var deletedRole = await _context.Roles.IgnoreQueryFilters().SingleAsync(item => item.Id == role.Id);
        Assert.True(deletedRole.IsDeleted);
        var audit = await _context.AuditEntries.SingleAsync(entry => entry.EntityId == role.Id && entry.Action == "Deleted");
        Assert.Contains("projects.read", audit.OriginalValues);
    }

    private RoleAdministrationService CreateRoleService()
    {
        var audit = new Mock<IAuditService>();
        audit.Setup(service => service.AppendAsync(It.IsAny<AuditEntry>(), It.IsAny<CancellationToken>()))
            .Callback<AuditEntry, CancellationToken>((entry, _) => _context.AuditEntries.Add(entry))
            .ReturnsAsync(Result.Success());
        return new RoleAdministrationService(_context, new UserEffectivePermissionService(_context), audit.Object);
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
        modelBuilder.Entity<User>().Ignore(user => user.FullName).Ignore(user => user.Sessions);
        modelBuilder.Entity<UserSession>().Ignore(session => session.User);
        modelBuilder.Entity<Role>().HasMany(role => role.Permissions).WithMany(permission => permission.Roles);
        modelBuilder.Entity<UserRole>().HasOne(assignment => assignment.User).WithMany(user => user.UserRoles).HasForeignKey(assignment => assignment.UserId);
        modelBuilder.Entity<UserRole>().HasOne(assignment => assignment.Role).WithMany(role => role.UserRoles).HasForeignKey(assignment => assignment.RoleId);
        modelBuilder.Entity<UserPermissionOverride>().HasOne(item => item.User).WithMany(user => user.PermissionOverrides).HasForeignKey(item => item.UserId);
        modelBuilder.Entity<UserPermissionOverride>().HasOne(item => item.Permission).WithMany(permission => permission.UserOverrides).HasForeignKey(item => item.PermissionId);
        modelBuilder.Entity<AuditEntry>().Ignore(entry => entry.Activities);
    }
}
