using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using NEXUS.SharedKernel.Management;

namespace NEXUS.Modules.ProjectManagement.Api;

/// <summary>
/// Binds controller-supplied audit actors to the authenticated tenant management context.
/// </summary>
internal static class ManagementActorBinding
{
    public static IActionResult? RequireActor(
        ControllerBase controller,
        IManagementContextAccessor? managementContext,
        bool requireWritable,
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
        {
            return controller.Unauthorized();
        }

        var context = managementContext?.Current;
        if (context is null || context.UserId != userId || context.TenantId != tenantId)
            return controller.Forbid();
        if (requireWritable && context.EffectiveReadOnly)
            return controller.Forbid();

        actorId = userId.ToString("D");
        return null;
    }
}
