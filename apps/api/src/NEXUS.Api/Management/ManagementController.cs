using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NEXUS.SharedKernel.Management;

namespace NEXUS.Api.Management;

[ApiController]
[Authorize]
[Route("api/me/perspectives")]
public sealed class ManagementController : ControllerBase
{
    private readonly IManagementScopeResolver _resolver;
    private readonly IManagementContextAccessor _accessor;

    public ManagementController(IManagementScopeResolver resolver, IManagementContextAccessor accessor)
    {
        _resolver = resolver;
        _accessor = accessor;
    }

    [HttpGet]
    public async Task<ActionResult<ManagementPerspectiveSnapshot>> Get(CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) || !Guid.TryParse(User.FindFirstValue("tenantId"), out var tenantId)) return Unauthorized();
        return Ok(await _resolver.GetPerspectiveSnapshotAsync(userId, tenantId, _accessor.Current, cancellationToken));
    }
}
