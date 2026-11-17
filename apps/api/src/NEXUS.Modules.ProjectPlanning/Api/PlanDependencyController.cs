using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using NEXUS.Modules.ProjectPlanning.Application;
using NEXUS.Modules.ProjectPlanning.Application.DTOs;

namespace NEXUS.Modules.ProjectPlanning.Api;

[ApiController]
[Route("api/projects/{projectId:guid}/plans/{planId:guid}/dependencies")]
[Authorize]
public class PlanDependencyController : ControllerBase
{
    private readonly IPlanDependencyService _dependencyService;

    public PlanDependencyController(IPlanDependencyService dependencyService)
    {
        _dependencyService = dependencyService;
    }

    [Authorize(Policy = "Planning.View")]
    [HttpGet]
    public async Task<IActionResult> GetByPlan(Guid projectId, Guid planId, CancellationToken cancellationToken)
    {
        var result = await _dependencyService.GetByPlanAsync(planId, cancellationToken);
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
    public async Task<IActionResult> Create(Guid projectId, Guid planId, [FromBody] CreatePlanDependencyDto dto, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var userId)) return Unauthorized();
        var result = await _dependencyService.CreateAsync(planId, dto, userId, cancellationToken);
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
    [HttpPut("{dependencyId:guid}")]
    public async Task<IActionResult> Update(Guid projectId, Guid planId, Guid dependencyId, [FromBody] UpdatePlanDependencyDto dto, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var userId)) return Unauthorized();
        var result = await _dependencyService.UpdateAsync(planId, dependencyId, dto, userId, cancellationToken);
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
    [HttpDelete("{dependencyId:guid}")]
    public async Task<IActionResult> Delete(Guid projectId, Guid planId, Guid dependencyId, [FromQuery] byte[] rowVersion, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var userId)) return Unauthorized();
        var result = await _dependencyService.DeleteAsync(planId, dependencyId, rowVersion, userId, cancellationToken);
        if (result.IsFailure)
            return result.Error?.StatusCode switch
            {
                404 => NotFound(new { error = result.Error.Message }),
                409 => Conflict(new { error = result.Error.Message }),
                _ => BadRequest(new { error = result.Error?.Message })
            };
        return NoContent();
    }

    [Authorize(Policy = "Planning.Edit")]
    [HttpPut("predecessors")]
    public async Task<IActionResult> ReplacePredecessors(Guid projectId, Guid planId, [FromBody] ReplacePlanPredecessorsDto dto, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var userId)) return Unauthorized();
        var result = await _dependencyService.ReplacePredecessorsAsync(planId, dto, userId, cancellationToken);
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
    [HttpPut("bulk-predecessors")]
    public async Task<IActionResult> ReplacePredecessorsBulk(Guid projectId, Guid planId, [FromBody] BulkReplacePlanPredecessorsDto dto, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var userId)) return Unauthorized();
        var result = await _dependencyService.ReplacePredecessorsBulkAsync(planId, dto, userId, cancellationToken);
        if (result.IsFailure)
            return result.Error?.StatusCode switch
            {
                404 => NotFound(new { error = result.Error.Message }),
                409 => Conflict(new { error = result.Error.Message }),
                _ => BadRequest(new { error = result.Error?.Message })
            };
        return Ok(result.Value);
    }

    private bool TryGetActorId(out string actorId)
    {
        actorId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        return User.Identity?.IsAuthenticated == true && Guid.TryParse(actorId, out var parsed) && parsed != Guid.Empty;
    }
}
