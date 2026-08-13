using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.ProjectPlanning.Application;
using NEXUS.Modules.ProjectPlanning.Application.DTOs;

namespace NEXUS.Modules.ProjectPlanning.Api;

[ApiController]
[Route("api/projects/{projectId:guid}/plans/{planId:guid}/wbs")]
[Authorize]
public class PlanWbsController : ControllerBase
{
    private readonly IPlanWbsService _wbsService;

    public PlanWbsController(IPlanWbsService wbsService)
    {
        _wbsService = wbsService;
    }

    [HttpGet]
    public async Task<IActionResult> GetTree(Guid projectId, Guid planId, CancellationToken cancellationToken)
    {
        var result = await _wbsService.GetTreeAsync(planId, cancellationToken);
        if (result.IsFailure)
            return result.Error?.StatusCode switch
            {
                404 => NotFound(new { error = result.Error.Message }),
                _ => BadRequest(new { error = result.Error?.Message })
            };
        return Ok(result.Value);
    }

    [HttpPost]
    public async Task<IActionResult> Create(Guid projectId, Guid planId, [FromBody] CreatePlanWbsNodeDto dto, CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "System";
        var result = await _wbsService.CreateAsync(planId, dto, userId, cancellationToken);
        if (result.IsFailure)
            return result.Error?.StatusCode switch
            {
                404 => NotFound(new { error = result.Error.Message }),
                409 => Conflict(new { error = result.Error.Message }),
                _ => BadRequest(new { error = result.Error?.Message })
            };
        return CreatedAtAction(nameof(GetTree), new { projectId, planId }, result.Value);
    }

    [HttpPut("{nodeId:guid}")]
    public async Task<IActionResult> Update(Guid projectId, Guid planId, Guid nodeId, [FromBody] UpdatePlanWbsNodeDto dto, CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "System";
        var result = await _wbsService.UpdateAsync(planId, nodeId, dto, userId, cancellationToken);
        if (result.IsFailure)
            return result.Error?.StatusCode switch
            {
                404 => NotFound(new { error = result.Error.Message }),
                409 => Conflict(new { error = result.Error.Message }),
                _ => BadRequest(new { error = result.Error?.Message })
            };
        return Ok(result.Value);
    }

    [HttpDelete("{nodeId:guid}")]
    public async Task<IActionResult> Delete(Guid projectId, Guid planId, Guid nodeId, [FromQuery] byte[] rowVersion, CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "System";
        var result = await _wbsService.DeleteAsync(planId, nodeId, rowVersion, userId, cancellationToken);
        if (result.IsFailure)
            return result.Error?.StatusCode switch
            {
                404 => NotFound(new { error = result.Error.Message }),
                409 => Conflict(new { error = result.Error.Message }),
                _ => BadRequest(new { error = result.Error?.Message })
            };
        return NoContent();
    }
}