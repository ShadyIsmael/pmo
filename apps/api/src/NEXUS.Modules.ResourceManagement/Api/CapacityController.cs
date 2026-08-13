using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.ResourceManagement.Application;
using NEXUS.Modules.ResourceManagement.Application.DTOs;

namespace NEXUS.Modules.ResourceManagement.Api;

// TODO: Restore [Authorize] policies when Authentication is implemented
[ApiController]
[Route("api/capacity")]
public class CapacityController : ControllerBase
{
    private readonly ICapacityService _capacityService;

    public CapacityController(ICapacityService capacityService)
    {
        _capacityService = capacityService;
    }

    [HttpGet("matrix")]
    public async Task<IActionResult> GetMatrix([FromQuery] CapacityFilterDto filter, CancellationToken cancellationToken)
    {
        var result = await _capacityService.GetMatrixAsync(filter, cancellationToken);
        if (result.IsFailure) return BadRequest(new { error = result.Error?.Message });
        return Ok(result.Value);
    }

    [HttpGet("availability")]
    public async Task<IActionResult> GetAvailability([FromQuery] CapacityFilterDto filter, CancellationToken cancellationToken)
    {
        var result = await _capacityService.GetAvailabilityAsync(filter, cancellationToken);
        return Ok(result);
    }

    [HttpGet("demand-vs-capacity")]
    public async Task<IActionResult> GetDemandVsCapacity([FromQuery] CapacityFilterDto filter, CancellationToken cancellationToken)
    {
        var result = await _capacityService.GetDemandVsCapacityAsync(filter, cancellationToken);
        if (result.IsFailure) return BadRequest(new { error = result.Error?.Message });
        return Ok(result.Value);
    }

    [HttpGet("gantt")]
    public async Task<IActionResult> GetGanttData([FromQuery] CapacityFilterDto filter, CancellationToken cancellationToken)
    {
        var result = await _capacityService.GetGanttDataAsync(filter, cancellationToken);
        if (result.IsFailure) return BadRequest(new { error = result.Error?.Message });
        return Ok(result.Value);
    }

    [HttpGet("resource/{resourceId:guid}/availability")]
    public async Task<IActionResult> GetResourceAvailability(Guid resourceId, [FromQuery] DateTime startDate, [FromQuery] DateTime endDate, CancellationToken cancellationToken)
    {
        var result = await _capacityService.GetResourceAvailabilityAsync(resourceId, startDate, endDate, cancellationToken);
        if (result.IsFailure) return NotFound(new { error = result.Error?.Message });
        return Ok(result.Value);
    }
}
