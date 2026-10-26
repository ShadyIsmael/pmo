using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using NEXUS.Modules.Identity.Application;

namespace NEXUS.Modules.Identity.Infrastructure;

public sealed class UserPermissionClaimsMiddleware
{
    private const string PermissionClaimType = "permission";
    private const string SessionClaimType = "sid";
    private readonly RequestDelegate _next;

    public UserPermissionClaimsMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, IUserEffectivePermissionService permissions, IUserSessionValidator sessions)
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

            if (!Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
                || !Guid.TryParse(context.User.FindFirstValue("tenantId"), out var tenantId)
                || !Guid.TryParse(context.User.FindFirstValue(SessionClaimType), out var sessionId)
                || !await sessions.IsActiveAsync(sessionId, userId, tenantId, context.RequestAborted))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
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
}
