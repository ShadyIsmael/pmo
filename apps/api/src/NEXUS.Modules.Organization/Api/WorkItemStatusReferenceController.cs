using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.Organization.Application;
using NEXUS.SharedKernel.Management;

namespace NEXUS.Modules.Organization.Api;

[ApiController]
[Route("api/reference-data/work-item-statuses")]
[Authorize]
public sealed class WorkItemStatusReferenceController : ControllerBase
{
    private readonly IWorkItemStatusReferenceService _service;
    private readonly IManagementContextAccessor _managementContext;

    public WorkItemStatusReferenceController(IWorkItemStatusReferenceService service, IManagementContextAccessor managementContext)
    {
        _service = service;
        _managementContext = managementContext;
    }

    [HttpGet]
    [Authorize(Policy = "ReferenceData.View")]
    public async Task<IActionResult> Get([FromQuery] bool includeInactive = true, CancellationToken cancellationToken = default)
    {
        return Ok(await _service.GetAsync(includeInactive, cancellationToken));
    }

    [HttpPost]
    [Authorize(Policy = "ReferenceData.Create")]
    public async Task<IActionResult> Create([FromBody] SaveWorkItemStatusRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId)) return Forbid();
        var result = await _service.CreateAsync(request, actorId, cancellationToken);
        return result.IsFailure ? StatusCode(result.Error!.StatusCode, result.Error) : Created(string.Empty, result.Value);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "ReferenceData.Edit")]
    public async Task<IActionResult> Update(Guid id, [FromBody] SaveWorkItemStatusRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId)) return Forbid();
        var result = await _service.UpdateAsync(id, request, actorId, cancellationToken);
        return result.IsFailure ? StatusCode(result.Error!.StatusCode, result.Error) : Ok(result.Value);
    }

    [HttpPut("{id:guid}/deactivate")]
    [Authorize(Policy = "ReferenceData.Delete")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId)) return Forbid();
        var result = await _service.DeactivateAsync(id, actorId, cancellationToken);
        return result.IsFailure ? StatusCode(result.Error!.StatusCode, result.Error) : NoContent();
    }

    private bool TryGetActorId(out string actorId)
    {
        actorId = string.Empty;
        var tenantClaim = User.FindFirstValue("tenantId");
        var context = _managementContext.Current;
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) || userId == Guid.Empty
            || !Guid.TryParse(tenantClaim, out var tenantId) || tenantId == Guid.Empty
            || context is null || context.EffectiveReadOnly || context.UserId != userId || context.TenantId != tenantId)
        {
            return false;
        }

        actorId = userId.ToString("D");
        return true;
    }
}
