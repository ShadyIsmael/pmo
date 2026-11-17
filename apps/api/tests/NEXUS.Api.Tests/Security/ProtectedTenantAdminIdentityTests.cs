using NEXUS.Modules.Identity.Application;
using NEXUS.Modules.Identity.Domain;
using NEXUS.Modules.Identity.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NEXUS.Api.Tests.Testing;
using NEXUS.Modules.Organization.Domain;
using NEXUS.Persistence;
using NEXUS.SharedKernel;
using Xunit;

namespace NEXUS.Api.Tests.Security;

public sealed class ProtectedTenantAdminIdentityTests
{
    private static readonly Guid TenantA = Guid.Parse("a1100000-0000-0000-0000-000000000001");

    [Fact]
    public void Protected_admin_authority_requires_the_code_defined_system_identity_in_a_tenant()
    {
        var protectedAdmin = new Role
        {
            TenantId = TenantA,
            Name = "Tenant administrator",
            IsSystem = true
        };
        protectedAdmin.AssignSystemRoleKey(Role.TenantAdminSystemRoleKey, TenantA);

        var displayNameOnly = new Role
        {
            TenantId = TenantA,
            Name = "admin",
            IsSystem = false
        };
        var systemRoleWithoutIdentity = new Role
        {
            TenantId = TenantA,
            Name = "Tenant administrator",
            IsSystem = true
        };
        var otherSystemRole = new Role
        {
            TenantId = TenantA,
            Name = "Tenant administrator",
            IsSystem = true
        };
        var unsupportedSystemRoleIdentity = Assert.Throws<ArgumentException>(() =>
            otherSystemRole.AssignSystemRoleKey("tenant.operator", TenantA));

        Assert.True(ProtectedTenantAdminPolicy.IsProtectedSystemAdminRole(protectedAdmin));
        Assert.False(ProtectedTenantAdminPolicy.IsProtectedSystemAdminRole(displayNameOnly));
        Assert.False(ProtectedTenantAdminPolicy.IsProtectedSystemAdminRole(systemRoleWithoutIdentity));
        Assert.False(ProtectedTenantAdminPolicy.IsProtectedSystemAdminRole(otherSystemRole));
        Assert.Contains("Only Tenant Admin and Organization Admin", unsupportedSystemRoleIdentity.Message);

        protectedAdmin.TenantId = Guid.Empty;
        Assert.False(ProtectedTenantAdminPolicy.IsProtectedSystemAdminRole(protectedAdmin));
    }

    [Fact]
    public void System_role_identity_cannot_be_assigned_outside_its_explicit_tenant()
    {
        var systemRole = new Role
        {
            TenantId = TenantA,
            Name = "Tenant administrator",
            IsSystem = true
        };

        Assert.Throws<InvalidOperationException>(() =>
            systemRole.AssignSystemRoleKey(Role.TenantAdminSystemRoleKey, Guid.NewGuid()));
        Assert.Null(systemRole.SystemRoleKey);
    }

    [Fact]
    public void Canonical_ordinary_role_identity_is_independent_from_system_and_protected_classification()
    {
        var projectManager = new Role
        {
            TenantId = TenantA,
            Name = "project.manager",
            Category = RoleCategories.Management,
            IsSystem = false
        };
        projectManager.AssignRoleKey("tenant.project-manager", TenantA);

        Assert.Equal("tenant.project-manager", projectManager.RoleKey);
        Assert.Null(projectManager.SystemRoleKey);
        Assert.False(projectManager.IsSystem);
        Assert.False(ProtectedTenantAdminPolicy.IsProtectedSystemAdminRole(projectManager));
    }

    [Fact]
    public void Only_the_two_approved_administrative_identities_may_be_system_roles()
    {
        var organizationAdmin = new Role
        {
            TenantId = TenantA,
            Name = "organization.admin",
            Category = RoleCategories.Organization,
            IsSystem = true
        };
        organizationAdmin.AssignSystemRoleKey(Role.OrganizationAdminSystemRoleKey, TenantA);

        var projectManager = new Role
        {
            TenantId = TenantA,
            Name = "project.manager",
            Category = RoleCategories.Management,
            IsSystem = true
        };

        Assert.Equal(Role.OrganizationAdminSystemRoleKey, organizationAdmin.SystemRoleKey);
        Assert.True(organizationAdmin.IsSystem);
        Assert.False(ProtectedTenantAdminPolicy.IsProtectedSystemAdminRole(organizationAdmin));
        Assert.Throws<ArgumentException>(() => projectManager.AssignSystemRoleKey("tenant.project-manager", TenantA));
        Assert.Null(projectManager.RoleKey);
        Assert.Null(projectManager.SystemRoleKey);
    }

    [Fact]
    public async Task Sqlite_schema_rejects_a_non_system_role_using_a_reserved_administrative_identity()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using var context = new SqliteTestAppDbContext(options);
        await context.Database.EnsureCreatedAsync();
        using var tenantScope = TrustedTenantExecutionContext.Enter(TenantA);
        context.Tenants.Add(new NEXUS.Modules.Organization.Domain.Tenant
        {
            Id = TenantA,
            Code = "RBAC-ROLE-IDENTITY-TEST",
            DisplayName = "Role identity test",
            CreatedBy = "test"
        });

        var ordinaryRole = new Role
        {
            TenantId = TenantA,
            Name = "Invalid ordinary role",
            Category = RoleCategories.Management,
            IsSystem = false,
            CreatedBy = "test"
        };
        Assert.Throws<ArgumentException>(() => ordinaryRole.AssignRoleKey(Role.OrganizationAdminRoleKey, TenantA));
        // Bypass the domain guard only to exercise the database check against a
        // corrupted/historical row that already contains a reserved RoleKey.
        typeof(Role).GetProperty(nameof(Role.RoleKey))!
            .GetSetMethod(nonPublic: true)!
            .Invoke(ordinaryRole, new object?[] { Role.OrganizationAdminRoleKey });
        context.Roles.Add(ordinaryRole);

        var failure = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        Assert.Contains("CK_Roles_SystemRoleKeyRequiresSystemRole", failure.GetBaseException().Message);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(Role.TenantAdminSystemRoleKey, null)]
    [InlineData("tenant.operator", "tenant.operator")]
    public async Task Sqlite_schema_rejects_system_roles_with_missing_or_unapproved_identity(
        string? systemRoleKey,
        string? roleKey)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using var context = new SqliteTestAppDbContext(options);
        await context.Database.EnsureCreatedAsync();
        using var tenantScope = TrustedTenantExecutionContext.Enter(TenantA);
        context.Tenants.Add(new NEXUS.Modules.Organization.Domain.Tenant
        {
            Id = TenantA,
            Code = "RBAC-SYSTEM-ROLE-CHECK-TEST",
            DisplayName = "System role check test",
            CreatedBy = "test"
        });

        var corruptedSystemRole = new Role
        {
            TenantId = TenantA,
            Name = "Corrupted system role",
            IsSystem = true,
            CreatedBy = "test"
        };
        typeof(Role).GetProperty(nameof(Role.SystemRoleKey))!
            .GetSetMethod(nonPublic: true)!
            .Invoke(corruptedSystemRole, new object?[] { systemRoleKey });
        typeof(Role).GetProperty(nameof(Role.RoleKey))!
            .GetSetMethod(nonPublic: true)!
            .Invoke(corruptedSystemRole, new object?[] { roleKey });
        context.Roles.Add(corruptedSystemRole);

        // SQL CHECK constraints must reject FALSE and UNKNOWN; a NULL identity
        // must not slip through because of three-valued predicate evaluation.
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task Last_admin_safeguard_tracks_active_canonical_role_assignment_without_permission_baseline()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using var context = new SqliteTestAppDbContext(options);
        await context.Database.EnsureCreatedAsync();
        using var tenantScope = TrustedTenantExecutionContext.Enter(TenantA);
        context.Tenants.Add(new NEXUS.Modules.Organization.Domain.Tenant
        {
            Id = TenantA,
            Code = "RBAC-LAST-ADMIN-TEST",
            DisplayName = "Last Admin test",
            CreatedBy = "test"
        });
        var adminUser = new User
        {
            TenantId = TenantA,
            Email = "last-admin@example.test",
            FirstName = "Last",
            LastName = "Admin",
            IsActive = true,
            CreatedBy = "test"
        };
        var adminRole = new Role
        {
            TenantId = TenantA,
            Name = "Tenant Administrator",
            IsSystem = true,
            CreatedBy = "test"
        };
        adminRole.AssignSystemRoleKey(Role.TenantAdminRoleKey, TenantA);
        context.Users.Add(adminUser);
        context.Roles.Add(adminRole);
        context.UserRoles.Add(new UserRole
        {
            TenantId = TenantA,
            User = adminUser,
            Role = adminRole,
            CreatedBy = "test"
        });
        await context.SaveChangesAsync();

        await using (var guard = await TenantAdminMutationGuard.AcquireAsync(context, TenantA))
        {
            Assert.True(await guard.HasActiveTenantAdminAsync(TenantA));
            await guard.CommitAsync();
        }

        adminUser.IsActive = false;
        await context.SaveChangesAsync();
        await using var inactiveGuard = await TenantAdminMutationGuard.AcquireAsync(context, TenantA);
        Assert.False(await inactiveGuard.HasActiveTenantAdminAsync(TenantA));
        await inactiveGuard.CommitAsync();
    }
}
