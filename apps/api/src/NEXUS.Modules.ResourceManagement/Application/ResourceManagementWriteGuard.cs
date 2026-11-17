using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Management;

namespace NEXUS.Modules.ResourceManagement.Application;

/// <summary>
/// Keeps application-layer writes aligned with the trusted request actor when a
/// management context is present. Context-free service initiated work remains valid.
/// </summary>
internal static class ResourceManagementWriteGuard
{
    public static Error? RequireActorContext(
        IHttpContextAccessor? httpContextAccessor,
        IManagementContextAccessor? managementContextAccessor,
        string actorId)
    {
        var management = managementContextAccessor?.Current;
        var principal = httpContextAccessor?.HttpContext?.User;
        if (management?.EffectiveReadOnly == true
            || string.Equals(principal?.FindFirst("readOnly")?.Value, "true", StringComparison.OrdinalIgnoreCase))
            return Error.Forbidden("The current management context is read-only.");

        // Context-free background/service work remains allowed. If a trusted
        // management context exists, the supplied actor must match it even when this
        // service was invoked without an HTTP context.
        if (management is null)
            return null;

        if (!Guid.TryParse(actorId, out var suppliedActorId)
            || suppliedActorId == Guid.Empty
            || suppliedActorId != management.UserId
            || management.TenantId == Guid.Empty)
            return Error.Forbidden("The mutation actor must match the current management context.");

        if (httpContextAccessor?.HttpContext is null)
            return null;

        var actorClaim = principal?.FindFirst(ClaimTypes.NameIdentifier);
        var actorValue = actorClaim?.Value ?? (actorClaim is null ? principal?.FindFirst("sub")?.Value : null);
        if (principal?.Identity?.IsAuthenticated != true
            || !Guid.TryParse(actorValue, out var userId)
            || userId == Guid.Empty)
            return Error.Unauthorized("A valid authenticated user identity is required.");
        if (!Guid.TryParse(principal?.FindFirst("tenantId")?.Value, out var tenantId)
            || tenantId == Guid.Empty
            || suppliedActorId != userId
            || management.UserId != userId
            || management.TenantId != tenantId)
            return Error.Forbidden("The authenticated user and Tenant must match the current management context.");

        return null;
    }
}
