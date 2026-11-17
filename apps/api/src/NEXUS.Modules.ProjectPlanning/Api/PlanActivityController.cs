using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
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

    [Authorize(Policy = "Planning.View")]
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

    [Authorize(Policy = "Planning.Edit")]
    [HttpPost]
    public async Task<IActionResult> Create(Guid projectId, Guid planId, [FromBody] CreatePlanActivityDto dto, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var userId)) return Unauthorized();
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

    [Authorize(Policy = "Planning.Edit")]
    [HttpPut("{activityId:guid}")]
    public async Task<IActionResult> Update(Guid projectId, Guid planId, Guid activityId, [FromBody] UpdatePlanActivityDto dto, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var userId)) return Unauthorized();
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

    [Authorize(Policy = "Planning.Edit")]
    [HttpPost("bulk-update")]
    public async Task<IActionResult> BulkUpdate(Guid projectId, Guid planId, [FromBody] BulkUpdatePlanActivitiesDto dto, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var userId)) return Unauthorized();
        var result = await _activityService.BulkUpdateAsync(planId, dto, userId, cancellationToken);
        if (result.IsFailure)
            return result.Error?.StatusCode switch
            {
                404 => NotFound(new { error = result.Error.Message }),
                409 => Conflict(new { error = result.Error.Message }),
                _ => BadRequest(new { error = result.Error?.Message })
            };
        return Ok(result.Value);
    }

    [Authorize(Policy = "Planning.Edit")]
    [HttpPost("{activityId:guid}/move")]
    public async Task<IActionResult> Move(Guid projectId, Guid planId, Guid activityId, [FromBody] MovePlanActivityDto dto, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var userId)) return Unauthorized();
        var result = await _activityService.MoveAsync(planId, activityId, dto, userId, cancellationToken);
        if (result.IsFailure)
            return result.Error?.StatusCode switch
            {
                404 => NotFound(new { error = result.Error.Message }),
                409 => Conflict(new { error = result.Error.Message }),
                _ => BadRequest(new { error = result.Error?.Message })
            };
        return Ok(result.Value);
    }

    [Authorize(Policy = "Planning.Edit")]
    [HttpDelete("{activityId:guid}")]
    public async Task<IActionResult> Delete(Guid projectId, Guid planId, Guid activityId, [FromQuery] byte[] rowVersion, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var userId)) return Unauthorized();
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

    private bool TryGetActorId(out string actorId)
    {
        actorId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        return User.Identity?.IsAuthenticated == true && Guid.TryParse(actorId, out var parsed) && parsed != Guid.Empty;
    }
}
