using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.CostManagement.Application;

/// <summary>
/// Uses the current request's permission set, which is refreshed from the authoritative
/// tenant grant resolver by UserPermissionClaimsMiddleware on every authenticated request.
/// Application entry points use this guard in addition to endpoint policies.
/// </summary>
internal static class FinancePermissionGuard
{
    public static Error? Require(IHttpContextAccessor accessor, string permission)
    {
        var principal = accessor.HttpContext?.User;
        if (principal?.Identity?.IsAuthenticated != true)
            return Error.Unauthorized("An authenticated user is required.");

        if (!TryGetTenantId(principal, out _))
            return Error.Forbidden("A valid tenant context is required.");

        return principal.HasClaim("permission", permission)
            ? null
            : Error.Forbidden($"The {permission} permission is required.");
    }

    public static bool TryGetTenantId(ClaimsPrincipal? principal, out Guid tenantId)
    {
        tenantId = Guid.Empty;
        return Guid.TryParse(principal?.FindFirst("tenantId")?.Value, out tenantId) && tenantId != Guid.Empty;
    }

    public static bool HasCurrentPermission(IHttpContextAccessor accessor, string permission) =>
        Require(accessor, permission) is null;
}
