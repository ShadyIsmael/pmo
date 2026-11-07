using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.Organization.Application;

namespace NEXUS.Modules.Organization.Api;

[ApiController]
[Route("api/reference-data/work-item-statuses")]
[Authorize]
public sealed class WorkItemStatusReferenceController : ControllerBase
{
    private readonly IWorkItemStatusReferenceService _service;

    public WorkItemStatusReferenceController(IWorkItemStatusReferenceService service)
    {
        _service = service;
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
        var result = await _service.CreateAsync(request, ActorId(), cancellationToken);
        return result.IsFailure ? StatusCode(result.Error!.StatusCode, result.Error) : Created(string.Empty, result.Value);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "ReferenceData.Edit")]
    public async Task<IActionResult> Update(Guid id, [FromBody] SaveWorkItemStatusRequest request, CancellationToken cancellationToken)
    {
        var result = await _service.UpdateAsync(id, request, ActorId(), cancellationToken);
        return result.IsFailure ? StatusCode(result.Error!.StatusCode, result.Error) : Ok(result.Value);
    }

    [HttpPut("{id:guid}/deactivate")]
    [Authorize(Policy = "ReferenceData.Delete")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.DeactivateAsync(id, ActorId(), cancellationToken);
        return result.IsFailure ? StatusCode(result.Error!.StatusCode, result.Error) : NoContent();
    }

    private string ActorId() => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "System";
}
