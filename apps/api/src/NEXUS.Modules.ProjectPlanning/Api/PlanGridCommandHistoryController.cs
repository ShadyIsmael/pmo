using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.ProjectPlanning.Application;

namespace NEXUS.Modules.ProjectPlanning.Api;

[ApiController]
[Route("api/projects/{projectId:guid}/plans/{planId:guid}/grid-history")]
[Authorize]
public sealed class PlanGridCommandHistoryController : ControllerBase
{
    private readonly IPlanGridCommandHistoryService _history;

    public PlanGridCommandHistoryController(IPlanGridCommandHistoryService history) => _history = history;

    [Authorize(Policy = "Planning.View")]
    [HttpGet]
    public async Task<IActionResult> GetState(Guid projectId, Guid planId, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var userId)) return Unauthorized();
        var result = await _history.GetStateAsync(projectId, planId, userId, cancellationToken);
        if (result.IsFailure) return result.Error?.StatusCode == 404 ? NotFound(new { error = result.Error.Message }) : BadRequest(new { error = result.Error?.Message });
        return Ok(result.Value);
    }

    [Authorize(Policy = "Planning.Edit")]
    [HttpPost("{commandId:guid}/undo")]
    public Task<IActionResult> Undo(Guid projectId, Guid planId, Guid commandId, CancellationToken cancellationToken) =>
        Apply(projectId, planId, commandId, isRedo: false, cancellationToken);

    [Authorize(Policy = "Planning.Edit")]
    [HttpPost("{commandId:guid}/redo")]
    public Task<IActionResult> Redo(Guid projectId, Guid planId, Guid commandId, CancellationToken cancellationToken) =>
        Apply(projectId, planId, commandId, isRedo: true, cancellationToken);

    private async Task<IActionResult> Apply(Guid projectId, Guid planId, Guid commandId, bool isRedo, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var userId)) return Unauthorized();
        var result = isRedo
            ? await _history.RedoAsync(projectId, planId, commandId, userId, cancellationToken)
            : await _history.UndoAsync(projectId, planId, commandId, userId, cancellationToken);
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
