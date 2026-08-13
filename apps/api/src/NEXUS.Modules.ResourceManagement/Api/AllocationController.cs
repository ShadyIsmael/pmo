using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.ResourceManagement.Application;
using NEXUS.Modules.ResourceManagement.Application.DTOs;

namespace NEXUS.Modules.ResourceManagement.Api;

// TODO: Restore [Authorize] policies when Authentication is implemented
[ApiController]
[Route("api/allocations")]
public class AllocationController : ControllerBase
{
    private readonly IAllocationService _allocationService;

    public AllocationController(IAllocationService allocationService)
    {
        _allocationService = allocationService;
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _allocationService.GetByIdAsync(id, cancellationToken);
        if (result.IsFailure) return NotFound(new { error = result.Error?.Message });
        return Ok(result.Value);
    }

    [HttpGet]
    public async Task<IActionResult> GetPaged(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] Guid? resourceId = null,
        [FromQuery] Guid? projectId = null,
        [FromQuery] string? status = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _allocationService.GetPagedAsync(page, pageSize, resourceId, projectId, status, cancellationToken);
        return Ok(result);
    }

    [HttpGet("by-resource/{resourceId:guid}")]
    public async Task<IActionResult> GetByResource(Guid resourceId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var result = await _allocationService.GetByResourceAsync(resourceId, page, pageSize, cancellationToken);
        return Ok(result);
    }

    [HttpGet("by-project/{projectId:guid}")]
    public async Task<IActionResult> GetByProject(Guid projectId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var result = await _allocationService.GetByProjectAsync(projectId, page, pageSize, cancellationToken);
        return Ok(result);
    }

    [HttpGet("conflicts")]
    public async Task<IActionResult> GetConflicts(
        [FromQuery] Guid resourceId,
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate,
        [FromQuery] Guid? excludeAllocationId = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _allocationService.GetConflictsAsync(resourceId, startDate, endDate, excludeAllocationId is not null ? new decimal(excludeAllocationId.GetHashCode()) : null, cancellationToken);
        if (result.IsFailure) return BadRequest(new { error = result.Error?.Message });
        return Ok(result.Value);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateAllocationDto dto, CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "System";
        var result = await _allocationService.CreateAsync(dto, userId, cancellationToken);
        if (result.IsFailure) return BadRequest(new { error = result.Error?.Message });
        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateAllocationDto dto, CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "System";
        var result = await _allocationService.UpdateAsync(id, dto, userId, cancellationToken);
        if (result.IsFailure)
            return result.Error?.StatusCode switch
            {
                404 => NotFound(new { error = result.Error.Message }),
                _ => BadRequest(new { error = result.Error?.Message })
            };
        return Ok(result.Value);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "System";
        var result = await _allocationService.DeleteAsync(id, userId, cancellationToken);
        if (result.IsFailure) return NotFound(new { error = result.Error?.Message });
        return NoContent();
    }
}
