using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using NEXUS.SharedKernel.Management;

namespace NEXUS.Modules.ResourceManagement.Api;

/// <summary>Binds HTTP mutations to the authenticated user and trusted tenant context.</summary>
internal static class ResourceManagementActorBinding
{
    public static IActionResult? RequireActor(
        ControllerBase controller,
        IManagementContextAccessor? managementContext,
        bool requireManagementContext,
        out string actorId)
    {
        actorId = string.Empty;
        var principal = controller.User;
        var actorClaim = principal.FindFirst(ClaimTypes.NameIdentifier);
        var actorValue = actorClaim?.Value ?? (actorClaim is null ? principal.FindFirst("sub")?.Value : null);
        if (principal.Identity?.IsAuthenticated != true
            || !Guid.TryParse(actorValue, out var userId)
            || userId == Guid.Empty
            || !Guid.TryParse(principal.FindFirst("tenantId")?.Value, out var tenantId)
            || tenantId == Guid.Empty)
            return controller.Unauthorized();

        var context = managementContext?.Current;
        if ((requireManagementContext && context is null)
            || (context is not null && (context.UserId != userId || context.TenantId != tenantId)))
            return controller.Forbid();
        if (context?.EffectiveReadOnly == true
            || string.Equals(principal.FindFirst("readOnly")?.Value, "true", StringComparison.OrdinalIgnoreCase))
            return controller.Forbid();

        actorId = userId.ToString("D");
        return null;
    }
}
