using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Routing;
using NEXUS.Modules.Identity.Api;
using NEXUS.Modules.Identity.Application;
using NEXUS.Modules.Identity.Domain;
using Xunit;

namespace NEXUS.Api.Tests.Security;

/// <summary>
/// Source-level contracts for the public Identity administration routes. These
/// tests protect endpoint-to-canonical-permission mapping; service tests separately
/// protect payload-level delegation and tenant/resource checks.
/// </summary>
public sealed class IdentityApiAuthorizationScopeTests
{
    [Theory]
    [InlineData(nameof(RolesController.GetAll), "Roles.View")]
    [InlineData(nameof(RolesController.GetManageable), "Roles.View")]
    [InlineData(nameof(RolesController.GetAssignable), "Users.AssignRoles")]
    [InlineData(nameof(RolesController.GetAssignablePermissions), "Roles.ConfigurePermissions")]
    [InlineData(nameof(RolesController.GetById), "Roles.View")]
    [InlineData(nameof(RolesController.Create), "Roles.Create")]
    [InlineData(nameof(RolesController.Update), "Roles.View")]
    [InlineData(nameof(RolesController.Delete), "Roles.Delete")]
    public void Role_administration_routes_keep_their_explicit_tenant_permission(
        string actionName,
        string expectedPermission)
    {
        AssertIdentityActionPolicy(typeof(RolesController), actionName, expectedPermission);
    }

    [Theory]
    [InlineData(nameof(UsersController.GetLookup), "Users.View")]
    [InlineData(nameof(UsersController.GetPaged), "Users.View")]
    [InlineData(nameof(UsersController.Create), "Users.ManageAccounts")]
    [InlineData(nameof(UsersController.ResetPassword), "Users.ResetPassword")]
    [InlineData(nameof(UsersController.Update), "Users.View")]
    [InlineData(nameof(UsersController.GetPermissions), "UserPermissions.Manage")]
    [InlineData(nameof(UsersController.UpdatePermissions), "UserPermissions.Manage")]
    public void User_administration_routes_keep_their_explicit_tenant_permission(
        string actionName,
        string expectedPermission)
    {
        AssertIdentityActionPolicy(typeof(UsersController), actionName, expectedPermission);
    }

    [Theory]
    [InlineData("Roles.Create", "Roles.View")]
    [InlineData("Roles.EditDetails", "Roles.View")]
    [InlineData("Roles.ConfigurePermissions", "Roles.View")]
    [InlineData("Roles.Delete", "Roles.View")]
    [InlineData("Users.ManageAccounts", "Users.View")]
    [InlineData("Users.AssignRoles", "Users.View")]
    [InlineData("Users.SetStatus", "Users.View")]
    [InlineData("Users.ResetPassword", "Users.View")]
    [InlineData("UserPermissions.Manage", "Users.View")]
    public void Identity_action_permissions_remain_tenant_scoped_and_depend_on_the_matching_view(
        string actionPermission,
        string viewPermission)
    {
        Assert.True(PermissionCatalog.ByKey.TryGetValue(actionPermission, out var action));
        Assert.NotNull(action);
        Assert.True(action!.IsActive);
        Assert.Equal(PermissionScope.Tenant, action.Scope);
        Assert.Contains(viewPermission, action.RequiredPermissionKeys);
    }

    private static void AssertIdentityActionPolicy(Type controllerType, string actionName, string expectedPermission)
    {
        Assert.NotEmpty(controllerType.GetCustomAttributes<AuthorizeAttribute>(inherit: true));

        var action = controllerType.GetMethod(actionName);
        Assert.NotNull(action);
        Assert.NotEmpty(action!.GetCustomAttributes<HttpMethodAttribute>(inherit: true));

        var authorization = Assert.Single(action.GetCustomAttributes<AuthorizeAttribute>(inherit: false));
        Assert.Equal(expectedPermission, authorization.Policy);

        Assert.True(PermissionCatalog.ByKey.TryGetValue(expectedPermission, out var definition));
        Assert.NotNull(definition);
        Assert.True(definition!.IsActive);
        Assert.Equal(PermissionScope.Tenant, definition.Scope);
    }
}
