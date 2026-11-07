using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;

namespace NEXUS.Modules.Organization.Api;

public sealed record PlatformTenantProvisioningRequirement : IAuthorizationRequirement;

/// <summary>Tenant provisioning is a platform operation; tenant permission claims are never considered.</summary>
public sealed class PlatformTenantProvisioningAuthorizationHandler
    : AuthorizationHandler<PlatformTenantProvisioningRequirement>
{
    private readonly IConfiguration _configuration;

    public PlatformTenantProvisioningAuthorizationHandler(IConfiguration configuration) => _configuration = configuration;

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PlatformTenantProvisioningRequirement requirement)
    {
        if (context.User.FindAll("platform_role")
                .Any(claim => string.Equals(claim.Value, "PlatformAdministrator", StringComparison.OrdinalIgnoreCase))
            || context.User.FindAll("platform_permission")
                .Any(claim => string.Equals(claim.Value, "tenants.manage", StringComparison.OrdinalIgnoreCase)))
            context.Succeed(requirement);

        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? context.User.FindFirstValue("sub");
        if (Guid.TryParse(userId, out var parsedUserId) && parsedUserId != Guid.Empty)
        {
            var platformAdministrators = _configuration.GetSection("PlatformSettings:AdministratorUserIds")
                .GetChildren()
                .Select(section => section.Value)
                .Where(value => Guid.TryParse(value, out _))
                .Select(value => Guid.Parse(value!));
            if (platformAdministrators.Contains(parsedUserId)) context.Succeed(requirement);
        }
        return Task.CompletedTask;
    }
}
