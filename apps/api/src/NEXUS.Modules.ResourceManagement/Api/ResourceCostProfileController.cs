using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.ResourceManagement.Application;
using NEXUS.Modules.ResourceManagement.Application.DTOs;
using NEXUS.SharedKernel.Management;

namespace NEXUS.Modules.ResourceManagement.Api;

[Authorize]
[ApiController]
[Route("api/resources/{resourceId:guid}/cost-profiles")]
public class ResourceCostProfileController : ControllerBase
{
    private readonly IResourceCostProfileService _service;
    private readonly IManagementContextAccessor? _managementContext;

    public ResourceCostProfileController(IResourceCostProfileService service, IManagementContextAccessor? managementContext = null)
    {
        _service = service;
        _managementContext = managementContext;
    }

    [HttpGet]
    [Authorize(Policy = "Resource.ViewCostRate")]
    public async Task<IActionResult> GetByResource(Guid resourceId, CancellationToken cancellationToken)
    {
        var result = await _service.GetByResourceAsync(resourceId, cancellationToken);
        if (result.IsFailure) return NotFound(new { error = result.Error?.Message });
        return Ok(result.Value);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "Resource.ViewCostRate")]
    public async Task<IActionResult> GetById(Guid resourceId, Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(resourceId, id, cancellationToken);
        if (result.IsFailure) return NotFound(new { error = result.Error?.Message });
        return Ok(result.Value);
    }

    [HttpPost]
    [Authorize(Policy = "Resource.EditCostRate")]
    public async Task<IActionResult> Create(Guid resourceId, [FromBody] CreateResourceCostProfileDto dto, CancellationToken cancellationToken)
    {
        var actorError = ResourceManagementActorBinding.RequireActor(this, _managementContext, requireManagementContext: true, out var userId);
        if (actorError is not null) return actorError;
        dto.ResourceId = resourceId;
        var result = await _service.CreateAsync(dto, userId, cancellationToken);
        if (result.IsFailure)
            return result.Error?.StatusCode switch
            {
                409 => Conflict(new { error = result.Error.Message }),
                _ => BadRequest(new { error = result.Error?.Message })
            };
        return CreatedAtAction(nameof(GetById), new { resourceId, id = result.Value!.Id }, result.Value);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "Resource.EditCostRate")]
    public async Task<IActionResult> Update(Guid resourceId, Guid id, [FromBody] UpdateResourceCostProfileDto dto, CancellationToken cancellationToken)
    {
        var actorError = ResourceManagementActorBinding.RequireActor(this, _managementContext, requireManagementContext: true, out var userId);
        if (actorError is not null) return actorError;
        var result = await _service.UpdateAsync(resourceId, id, dto, userId, cancellationToken);
        if (result.IsFailure)
            return result.Error?.StatusCode switch
            {
                404 => NotFound(new { error = result.Error.Message }),
                409 => Conflict(new { error = result.Error.Message }),
                _ => BadRequest(new { error = result.Error?.Message })
            };
        return Ok(result.Value);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "Resource.EditCostRate")]
    public async Task<IActionResult> Delete(Guid resourceId, Guid id, CancellationToken cancellationToken)
    {
        var actorError = ResourceManagementActorBinding.RequireActor(this, _managementContext, requireManagementContext: true, out var userId);
        if (actorError is not null) return actorError;
        var result = await _service.DeleteAsync(resourceId, id, userId, cancellationToken);
        if (result.IsFailure) return NotFound(new { error = result.Error?.Message });
        return NoContent();
    }
}
