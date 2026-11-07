using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;

namespace NEXUS.Modules.Organization.Api;

public sealed record PlatformSettingsRequirement(string Action) : IAuthorizationRequirement;

/// <summary>
/// Platform access is granted only by an explicit PlatformAdministrator role or a
/// deployment-managed administrator allowlist. Obsolete lowercase settings aliases
/// and tenant permission claims are deliberately not consulted.
/// </summary>
public sealed class PlatformSettingsAuthorizationHandler : AuthorizationHandler<PlatformSettingsRequirement>
{
    private readonly IConfiguration _configuration;

    public PlatformSettingsAuthorizationHandler(IConfiguration configuration) => _configuration = configuration;

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PlatformSettingsRequirement requirement)
    {
        if (context.User.FindAll("platform_role")
            .Any(claim => string.Equals(claim.Value, "PlatformAdministrator", StringComparison.OrdinalIgnoreCase)))
            context.Succeed(requirement);

        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? context.User.FindFirstValue("sub");
        if (Guid.TryParse(userId, out var parsedUserId) && parsedUserId != Guid.Empty)
        {
            var administrators = _configuration.GetSection("PlatformSettings:AdministratorUserIds")
                .GetChildren()
                .Select(section => section.Value)
                .Where(value => Guid.TryParse(value, out _))
                .Select(value => Guid.Parse(value!));
            if (administrators.Contains(parsedUserId)) context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
