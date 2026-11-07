using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using NEXUS.Modules.Identity.Application;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.Identity.Infrastructure;

public sealed class UserPermissionClaimsMiddleware
{
    private const string PermissionClaimType = "permission";
    private const string SessionClaimType = "sid";
    private readonly RequestDelegate _next;

    public UserPermissionClaimsMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(
        HttpContext context,
        IUserEffectivePermissionService permissions,
        IUserSessionValidator sessions,
        ITenantRegistryStatusValidator tenantRegistry)
    {
        if (context.User.Identity is ClaimsIdentity identity && identity.IsAuthenticated)
        {
            // Strip authorization snapshots before any downstream middleware can
            // observe them, including the invalid-session response path.
            foreach (var claim in context.User.FindAll(PermissionClaimType).Concat(context.User.FindAll(ClaimTypes.Role)).ToArray())
            {
                if (claim.Subject is ClaimsIdentity subject)
                    subject.RemoveClaim(claim);
            }

            if (context.GetEndpoint()?.Metadata.GetMetadata<IAllowAnonymous>() is not null)
            {
                await _next(context);
                return;
            }

            var tenantClaim = context.User.FindFirstValue("tenantId");
            if (HasPlatformAuthority(context.User) && !Guid.TryParse(tenantClaim, out _))
            {
                if (!Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                        ?? context.User.FindFirstValue("sub"), out var platformActorId)
                    || platformActorId == Guid.Empty)
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return;
                }

                foreach (var claim in context.User.FindAll("tenantId").ToArray())
                    if (claim.Subject is ClaimsIdentity subject) subject.RemoveClaim(claim);
                await _next(context);
                return;
            }

            if (!Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
                || !Guid.TryParse(context.User.FindFirstValue("tenantId"), out var tenantId)
                || !Guid.TryParse(context.User.FindFirstValue(SessionClaimType), out var sessionId)
                || !await sessions.IsActiveAsync(sessionId, userId, tenantId, context.RequestAborted))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            if (!await tenantRegistry.IsActiveAsync(tenantId, context.RequestAborted))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }

            // Never trust the permission snapshot embedded in a long-lived JWT. Rebuild
            // it from the tenant-scoped source of truth on every authenticated request,
            // so direct denials and revocations apply without waiting for token expiry.
            var currentPermissions = await permissions.GetEffectivePermissionsAsync(userId, tenantId, context.RequestAborted);
            foreach (var permission in currentPermissions)
                identity.AddClaim(new Claim(PermissionClaimType, permission));

            var currentRoles = await permissions.GetEffectiveRoleNamesAsync(userId, tenantId, context.RequestAborted);
            foreach (var role in currentRoles)
                identity.AddClaim(new Claim(ClaimTypes.Role, role));
        }

        await _next(context);
    }

    private static bool HasPlatformAuthority(ClaimsPrincipal principal) =>
        principal.FindAll("platform_role").Any(claim =>
            string.Equals(claim.Value, "PlatformAdministrator", StringComparison.OrdinalIgnoreCase))
        || principal.FindAll("platform_permission").Any(claim =>
            claim.Value is "tenants.manage");
}
