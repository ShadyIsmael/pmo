using NEXUS.Modules.Identity.Application;
using NEXUS.Modules.Identity.Domain;
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
        otherSystemRole.AssignSystemRoleKey("tenant.operator", TenantA);

        Assert.True(ProtectedTenantAdminPolicy.IsProtectedSystemAdminRole(protectedAdmin));
        Assert.False(ProtectedTenantAdminPolicy.IsProtectedSystemAdminRole(displayNameOnly));
        Assert.False(ProtectedTenantAdminPolicy.IsProtectedSystemAdminRole(systemRoleWithoutIdentity));
        Assert.False(ProtectedTenantAdminPolicy.IsProtectedSystemAdminRole(otherSystemRole));

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
}
