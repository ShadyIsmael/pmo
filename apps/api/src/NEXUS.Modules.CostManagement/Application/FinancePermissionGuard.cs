using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Management;

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

    public static Error? RequireWritableManagementContext(ManagementContext? management, Guid tenantId)
    {
        if (tenantId == Guid.Empty || management is null || management.TenantId != tenantId || management.Scope is null)
            return Error.Forbidden("A writable management context for the current Tenant is required.");
        return management.EffectiveReadOnly
            ? Error.Forbidden("The current management context is read-only.")
            : null;
    }

    public static Error? RequireWritableActorContext(
        IHttpContextAccessor accessor,
        ManagementContext? management,
        string actorUserId,
        Guid tenantId)
    {
        var principal = accessor.HttpContext?.User;
        if (principal?.Identity?.IsAuthenticated != true)
            return Error.Unauthorized("An authenticated user is required.");
        if (tenantId == Guid.Empty || !TryGetTenantId(principal, out var claimTenantId))
            return Error.Forbidden("A valid tenant context is required.");
        var actorClaim = principal.FindFirst(ClaimTypes.NameIdentifier);
        var actorValue = actorClaim?.Value ?? (actorClaim is null ? principal.FindFirst("sub")?.Value : null);
        if (!Guid.TryParse(actorValue, out var claimActorId)
            || claimActorId == Guid.Empty)
            return Error.Unauthorized("A valid user identity is required.");
        if (!Guid.TryParse(actorUserId, out var suppliedActorId) || suppliedActorId != claimActorId)
            return Error.Forbidden("The financial mutation actor must match the authenticated user.");
        if (claimTenantId != tenantId || management is null || management.TenantId != claimTenantId
            || management.UserId != claimActorId || management.Scope is null)
            return Error.Forbidden("The authenticated user and Tenant must match the current management context.");
        return management.EffectiveReadOnly
            ? Error.Forbidden("The current management context is read-only.")
            : null;
    }
}
