using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using NEXUS.Modules.ProjectPlanning.Application;
using NEXUS.Modules.ProjectPlanning.Application.DTOs;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.ProjectPlanning.Api;

[ApiController]
[Route("api/projects/{projectId:guid}/plans/{planId:guid}/calendars")]
[Authorize]
public class PlanCalendarController : ControllerBase
{
    private readonly IPlanningCalendarService _service;

    public PlanCalendarController(IPlanningCalendarService service) => _service = service;

    [Authorize(Policy = "Planning.View")]
    [HttpGet]
    public async Task<IActionResult> GetByPlan(Guid projectId, Guid planId, CancellationToken cancellationToken)
    {
        var result = await _service.GetByPlanAsync(projectId, planId, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ErrorResult(result.Error);
    }

    [Authorize(Policy = "Planning.View")]
    [HttpGet("versions/{planVersionId:guid}")]
    public async Task<IActionResult> GetByVersion(Guid projectId, Guid planId, Guid planVersionId, CancellationToken cancellationToken)
    {
        var result = await _service.GetByVersionAsync(projectId, planId, planVersionId, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ErrorResult(result.Error);
    }

    [Authorize(Policy = "Planning.Edit")]
    [HttpPost]
    public async Task<IActionResult> Create(Guid projectId, Guid planId, [FromBody] CreatePlanCalendarDto dto, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId)) return Unauthorized();
        var result = await _service.CreatePlanCalendarAsync(projectId, planId, dto, actorId, cancellationToken);
        return result.IsSuccess ? CreatedAtAction(nameof(GetByPlan), new { projectId, planId }, result.Value) : ErrorResult(result.Error);
    }

    [Authorize(Policy = "Planning.Edit")]
    [HttpPut("{calendarId:guid}")]
    public async Task<IActionResult> Update(Guid projectId, Guid planId, Guid calendarId, [FromBody] UpdatePlanCalendarDto dto, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId)) return Unauthorized();
        var result = await _service.UpdatePlanCalendarAsync(projectId, planId, calendarId, dto, actorId, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : ErrorResult(result.Error);
    }

    [Authorize(Policy = "Planning.Edit")]
    [HttpDelete("{calendarId:guid}")]
    public async Task<IActionResult> Delete(Guid projectId, Guid planId, Guid calendarId, [FromQuery] byte[] rowVersion, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId)) return Unauthorized();
        var result = await _service.DeletePlanCalendarAsync(projectId, planId, calendarId, rowVersion, actorId, cancellationToken);
        return result.IsSuccess ? NoContent() : ErrorResult(result.Error);
    }

    private bool TryGetActorId(out string actorId)
    {
        actorId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        return User.Identity?.IsAuthenticated == true && Guid.TryParse(actorId, out var parsed) && parsed != Guid.Empty;
    }

    private IActionResult ErrorResult(Error? error) => error?.StatusCode switch
    {
        404 => NotFound(new { error = error.Message }),
        409 => Conflict(new { error = error.Message }),
        _ => BadRequest(new { error = error?.Message })
    };
}
