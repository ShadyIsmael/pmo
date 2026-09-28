using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.ResourceManagement.Application;
using NEXUS.Modules.ResourceManagement.Application.DTOs;

namespace NEXUS.Modules.ResourceManagement.Api;

[Authorize]
[ApiController]
[Route("api/resources/{resourceId:guid}/cost-profiles")]
public class ResourceCostProfileController : ControllerBase
{
    private readonly IResourceCostProfileService _service;

    public ResourceCostProfileController(IResourceCostProfileService service)
    {
        _service = service;
    }

    [HttpGet]
    [Authorize(Policy = "Resource.View")]
    public async Task<IActionResult> GetByResource(Guid resourceId, CancellationToken cancellationToken)
    {
        var result = await _service.GetByResourceAsync(resourceId, cancellationToken);
        if (result.IsFailure) return NotFound(new { error = result.Error?.Message });
        return Ok(result.Value);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "Resource.View")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(id, cancellationToken);
        if (result.IsFailure) return NotFound(new { error = result.Error?.Message });
        return Ok(result.Value);
    }

    [HttpPost]
    [Authorize(Policy = "Resource.Manage")]
    public async Task<IActionResult> Create(Guid resourceId, [FromBody] CreateResourceCostProfileDto dto, CancellationToken cancellationToken)
    {
        dto.ResourceId = resourceId;
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "System";
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
    [Authorize(Policy = "Resource.Manage")]
    public async Task<IActionResult> Update(Guid resourceId, Guid id, [FromBody] UpdateResourceCostProfileDto dto, CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "System";
        var result = await _service.UpdateAsync(id, dto, userId, cancellationToken);
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
    [Authorize(Policy = "Resource.Manage")]
    public async Task<IActionResult> Delete(Guid resourceId, Guid id, CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "System";
        var result = await _service.DeleteAsync(id, userId, cancellationToken);
        if (result.IsFailure) return NotFound(new { error = result.Error?.Message });
        return NoContent();
    }
}
