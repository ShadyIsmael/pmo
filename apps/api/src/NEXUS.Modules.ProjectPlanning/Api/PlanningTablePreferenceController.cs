using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.ProjectPlanning.Application;
using NEXUS.Modules.ProjectPlanning.Application.DTOs;

namespace NEXUS.Modules.ProjectPlanning.Api;

[ApiController]
[Route("api/projects/{projectId:guid}/plans/{planId:guid}/table-preferences")]
[Authorize]
public sealed class PlanningTablePreferenceController : ControllerBase
{
    private readonly IPlanningTablePreferenceService _service;
    public PlanningTablePreferenceController(IPlanningTablePreferenceService service) => _service = service;

    [Authorize(Policy = "Planning.View")]
    [HttpGet]
    public async Task<IActionResult> Get(Guid projectId, Guid planId, CancellationToken cancellationToken)
    {
        var result = await _service.GetAsync(projectId, planId, UserId(), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : Error(result.Error);
    }

    [Authorize(Policy = "Planning.Edit")]
    [HttpPut]
    public async Task<IActionResult> Save(Guid projectId, Guid planId, SavePlanningTablePreferenceDto dto, CancellationToken cancellationToken)
    {
        var result = await _service.SaveAsync(projectId, planId, dto, UserId(), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : Error(result.Error);
    }

    [Authorize(Policy = "Planning.Edit")]
    [HttpDelete]
    public async Task<IActionResult> Reset(Guid projectId, Guid planId, CancellationToken cancellationToken)
    {
        var result = await _service.ResetAsync(projectId, planId, UserId(), cancellationToken);
        return result.IsSuccess ? NoContent() : Error(result.Error);
    }

    private string UserId() => User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "System";
    private IActionResult Error(NEXUS.SharedKernel.Error? error) => error?.StatusCode switch { 403 => Forbid(), 404 => NotFound(new { error = error.Message }), 409 => Conflict(new { error = error.Message }), _ => BadRequest(new { error = error?.Message }) };
}
