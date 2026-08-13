using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

    [HttpPost]
    public async Task<IActionResult> Create(Guid projectId, Guid planId, [FromBody] CreatePlanDependencyDto dto, CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "System";
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

    [HttpPut("{dependencyId:guid}")]
    public async Task<IActionResult> Update(Guid projectId, Guid planId, Guid dependencyId, [FromBody] UpdatePlanDependencyDto dto, CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "System";
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

    [HttpDelete("{dependencyId:guid}")]
    public async Task<IActionResult> Delete(Guid projectId, Guid planId, Guid dependencyId, [FromQuery] byte[] rowVersion, CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "System";
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
}