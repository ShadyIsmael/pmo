using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.ProjectPlanning.Application;
using NEXUS.Modules.ProjectPlanning.Application.DTOs;

namespace NEXUS.Modules.ProjectPlanning.Api;

[ApiController]
[Route("api/projects/{projectId:guid}/plans/{planId:guid}/activities")]
[Authorize]
public class PlanActivityController : ControllerBase
{
    private readonly IPlanActivityService _activityService;

    public PlanActivityController(IPlanActivityService activityService)
    {
        _activityService = activityService;
    }

    [HttpGet]
    public async Task<IActionResult> GetByPlan(Guid projectId, Guid planId, CancellationToken cancellationToken)
    {
        var result = await _activityService.GetByPlanAsync(planId, cancellationToken);
        if (result.IsFailure)
            return result.Error?.StatusCode switch
            {
                404 => NotFound(new { error = result.Error.Message }),
                _ => BadRequest(new { error = result.Error?.Message })
            };
        return Ok(result.Value);
    }

    [HttpPost]
    public async Task<IActionResult> Create(Guid projectId, Guid planId, [FromBody] CreatePlanActivityDto dto, CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "System";
        var result = await _activityService.CreateAsync(planId, dto, userId, cancellationToken);
        if (result.IsFailure)
            return result.Error?.StatusCode switch
            {
                404 => NotFound(new { error = result.Error.Message }),
                409 => Conflict(new { error = result.Error.Message }),
                _ => BadRequest(new { error = result.Error?.Message })
            };
        return CreatedAtAction(nameof(GetByPlan), new { projectId, planId }, result.Value);
    }

    [HttpPut("{activityId:guid}")]
    public async Task<IActionResult> Update(Guid projectId, Guid planId, Guid activityId, [FromBody] UpdatePlanActivityDto dto, CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "System";
        var result = await _activityService.UpdateAsync(planId, activityId, dto, userId, cancellationToken);
        if (result.IsFailure)
            return result.Error?.StatusCode switch
            {
                404 => NotFound(new { error = result.Error.Message }),
                409 => Conflict(new { error = result.Error.Message }),
                _ => BadRequest(new { error = result.Error?.Message })
            };
        return Ok(result.Value);
    }

    [HttpDelete("{activityId:guid}")]
    public async Task<IActionResult> Delete(Guid projectId, Guid planId, Guid activityId, [FromQuery] byte[] rowVersion, CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "System";
        var result = await _activityService.DeleteAsync(planId, activityId, rowVersion, userId, cancellationToken);
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