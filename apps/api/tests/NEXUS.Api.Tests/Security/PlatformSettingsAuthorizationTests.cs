using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using NEXUS.Modules.Organization.Api;
using Xunit;

namespace NEXUS.Api.Tests.Security;

public sealed class PlatformSettingsAuthorizationTests
{
    [Theory]
    [InlineData("settings.read")]
    [InlineData("settings.write")]
    public async Task Legacy_platform_permission_values_do_not_authorize_settings(string legacyPermission)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [new Claim("platform_permission", legacyPermission)],
        authenticationType: "test"));
        var requirement = new PlatformSettingsRequirement("read");
        var context = new AuthorizationHandlerContext([requirement], principal, resource: null);
        var handler = new PlatformSettingsAuthorizationHandler(new ConfigurationBuilder().Build());

        await handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task Explicit_platform_administrator_role_can_access_platform_settings()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [new Claim("platform_role", "PlatformAdministrator")],
        authenticationType: "test"));
        var requirement = new PlatformSettingsRequirement("write");
        var context = new AuthorizationHandlerContext([requirement], principal, resource: null);
        var handler = new PlatformSettingsAuthorizationHandler(new ConfigurationBuilder().Build());

        await handler.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }
}
