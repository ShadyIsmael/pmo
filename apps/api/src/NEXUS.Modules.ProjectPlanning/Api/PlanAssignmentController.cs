using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.ProjectPlanning.Application;
using NEXUS.Modules.ProjectPlanning.Application.DTOs;

namespace NEXUS.Modules.ProjectPlanning.Api;

[ApiController]
[Route("api/projects/{projectId:guid}/plans/{planId:guid}/assignments")]
[Authorize]
public class PlanAssignmentController : ControllerBase
{
    private readonly IPlanAssignmentService _assignmentService;

    public PlanAssignmentController(IPlanAssignmentService assignmentService) => _assignmentService = assignmentService;

    [Authorize(Policy = "Planning.View")]
    [HttpGet]
    public async Task<IActionResult> GetByPlan(Guid projectId, Guid planId, CancellationToken cancellationToken)
    {
        var result = await _assignmentService.GetByPlanAsync(planId, cancellationToken);
        return ToActionResult(result, value => Ok(value));
    }

    [Authorize(Policy = "Planning.View")]
    [HttpGet("capacity")]
    public async Task<IActionResult> GetCapacity(Guid projectId, Guid planId, [FromQuery] Guid resourceId, [FromQuery] DateTime startDate, [FromQuery] DateTime endDate, [FromQuery] Guid? excludeAssignmentId, CancellationToken cancellationToken)
    {
        var result = await _assignmentService.GetCapacityAsync(projectId, planId, resourceId, startDate, endDate, excludeAssignmentId, cancellationToken);
        return ToActionResult(result, value => Ok(value));
    }

    [Authorize(Policy = "Planning.Edit")]
    [HttpPost]
    public async Task<IActionResult> Create(Guid projectId, Guid planId, [FromBody] CreatePlanAssignmentDto dto, CancellationToken cancellationToken)
    {
        var result = await _assignmentService.CreateAsync(planId, dto, UserId(), cancellationToken);
        return ToActionResult(result, value => CreatedAtAction(nameof(GetByPlan), new { projectId, planId }, value));
    }

    [Authorize(Policy = "Planning.Edit")]
    [HttpPost("batch")]
    public async Task<IActionResult> CreateMany(Guid projectId, Guid planId, [FromBody] CreatePlanAssignmentsBatchDto dto, CancellationToken cancellationToken)
    {
        var result = await _assignmentService.CreateManyAsync(planId, dto, UserId(), cancellationToken);
        return ToActionResult(result, value => CreatedAtAction(nameof(GetByPlan), new { projectId, planId }, value));
    }

    [Authorize(Policy = "Planning.Edit")]
    [HttpPut("{assignmentId:guid}")]
    public async Task<IActionResult> Update(Guid projectId, Guid planId, Guid assignmentId, [FromBody] UpdatePlanAssignmentDto dto, CancellationToken cancellationToken)
    {
        var result = await _assignmentService.UpdateAsync(planId, assignmentId, dto, UserId(), cancellationToken);
        return ToActionResult(result, value => Ok(value));
    }

    [Authorize(Policy = "Planning.Edit")]
    [HttpDelete("{assignmentId:guid}")]
    public async Task<IActionResult> Delete(Guid projectId, Guid planId, Guid assignmentId, [FromQuery] byte[] rowVersion, CancellationToken cancellationToken)
    {
        var result = await _assignmentService.DeleteAsync(planId, assignmentId, rowVersion, UserId(), cancellationToken);
        return result.IsSuccess ? NoContent() : ErrorResult(result.Error);
    }

    private string UserId() => User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "System";

    private IActionResult ToActionResult<T>(NEXUS.SharedKernel.Result<T> result, Func<T?, IActionResult> success) =>
        result.IsSuccess ? success(result.Value) : ErrorResult(result.Error);

    private IActionResult ErrorResult(NEXUS.SharedKernel.Error? error) => error?.StatusCode switch
    {
        404 => NotFound(new { error = error.Message }),
        409 => Conflict(new { error = error.Message }),
        _ => BadRequest(new { error = error?.Message })
    };
}
