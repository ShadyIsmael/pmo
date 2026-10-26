using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.ProjectPlanning.Application;

namespace NEXUS.Modules.ProjectPlanning.Api;

[ApiController]
[Route("api/projects/{projectId:guid}/plans/{planId:guid}/scheduling")]
[Authorize]
public sealed class PlanSchedulingCommandController : ControllerBase
{
    private readonly IPlanActivityService _activityService;
    private readonly IPlanSchedulingAnalysisService _analysisService;

    public PlanSchedulingCommandController(
        IPlanActivityService activityService,
        IPlanSchedulingAnalysisService analysisService)
    {
        _activityService = activityService;
        _analysisService = analysisService;
    }

    [Authorize(Policy = "Planning.Manage")]
    [HttpPost("recalculate")]
    public async Task<IActionResult> Recalculate(Guid projectId, Guid planId, CancellationToken cancellationToken)
    {
        // Resolve through the project-scoped query before delegating so a plan
        // ID cannot be recalculated through an unrelated project route.
        var version = await _analysisService.GetActiveWorkingVersionIdAsync(projectId, planId, cancellationToken);
        if (version.IsFailure)
            return version.Error?.StatusCode == 404
                ? NotFound(new { error = version.Error.Message })
                : BadRequest(new { error = version.Error?.Message });

        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "System";
        var result = await _activityService.RecalculateScheduleAsync(planId, userId, cancellationToken);
        if (result.IsFailure)
            return result.Error?.StatusCode switch
            {
                404 => NotFound(new { error = result.Error.Message }),
                409 => Conflict(new { error = result.Error.Message }),
                _ => BadRequest(new { error = result.Error?.Message })
            };

        return Ok(result.Value);
    }
}
