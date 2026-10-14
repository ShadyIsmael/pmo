using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using NEXUS.Modules.Identity.Application;

namespace NEXUS.Modules.Identity.Infrastructure;

public sealed class UserPermissionClaimsMiddleware
{
    private const string PermissionClaimType = "permission";
    private readonly RequestDelegate _next;

    public UserPermissionClaimsMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, IUserEffectivePermissionService permissions)
    {
        if (context.User.Identity is ClaimsIdentity identity && identity.IsAuthenticated)
        {
            // Never trust the permission snapshot embedded in a long-lived JWT. Rebuild
            // it from the tenant-scoped source of truth on every authenticated request,
            // so direct denials and revocations apply without waiting for token expiry.
            foreach (var claim in context.User.FindAll(PermissionClaimType).ToArray())
            {
                if (claim.Subject is ClaimsIdentity subject)
                    subject.RemoveClaim(claim);
            }

            if (Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
                && Guid.TryParse(context.User.FindFirstValue("tenantId"), out var tenantId))
            {
                var currentPermissions = await permissions.GetEffectivePermissionsAsync(userId, tenantId, context.RequestAborted);
                foreach (var permission in currentPermissions)
                    identity.AddClaim(new Claim(PermissionClaimType, permission));
            }
        }

        await _next(context);
    }
}
