using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.ProjectManagement.Application;
using NEXUS.Modules.ProjectManagement.Application.DTOs;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.ProjectManagement.Api;

[ApiController]
[Authorize]
[Route("api/projects/{projectId:guid}/phase-gate")]
public sealed class ProjectPhaseGateController : ControllerBase
{
    private readonly IProjectPhaseGateReadinessService _phaseGate;

    public ProjectPhaseGateController(IProjectPhaseGateReadinessService phaseGate) => _phaseGate = phaseGate;

    [HttpGet("readiness")]
    [Authorize(Policy = "Project.View")]
    [Authorize(Policy = "Document.View")]
    public async Task<IActionResult> GetReadiness(
        Guid projectId,
        [FromQuery] Guid? targetPhaseId,
        CancellationToken cancellationToken)
    {
        var result = await _phaseGate.GetAsync(projectId, targetPhaseId, cancellationToken);
        return result.IsFailure ? ToActionResult(result.Error!) : Ok(result.Value);
    }

    [HttpPost("requirements/{requirementSnapshotId:guid}/document")]
    [Authorize(Policy = "Project.View")]
    [Authorize(Policy = "Document.View")]
    [Authorize(Policy = "Document.Edit")]
    public async Task<IActionResult> LinkDocument(
        Guid projectId,
        Guid requirementSnapshotId,
        [FromBody] LinkPhaseGateDocumentRequestDto request,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId)) return Unauthorized(Payload(Error.Unauthorized()));
        var result = await _phaseGate.LinkDocumentAsync(projectId, requirementSnapshotId,
            request.DocumentId, request.DocumentVersionId, actorId, cancellationToken);
        return result.IsFailure ? ToActionResult(result.Error!) : Ok(result.Value);
    }

    [HttpDelete("requirements/{requirementSnapshotId:guid}/document")]
    [Authorize(Policy = "Project.View")]
    [Authorize(Policy = "Document.View")]
    [Authorize(Policy = "Document.Edit")]
    public async Task<IActionResult> UnlinkDocument(
        Guid projectId,
        Guid requirementSnapshotId,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId)) return Unauthorized(Payload(Error.Unauthorized()));
        var result = await _phaseGate.UnlinkDocumentAsync(projectId, requirementSnapshotId, actorId, cancellationToken);
        return result.IsFailure ? ToActionResult(result.Error!) : Ok(result.Value);
    }

    [HttpPost("change-phase")]
    [Authorize(Policy = "Project.View")]
    [Authorize(Policy = "ProjectPhase.Change")]
    public async Task<IActionResult> ChangePhase(
        Guid projectId,
        [FromBody] ChangeProjectPhaseRequestDto request,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId)) return Unauthorized(Payload(Error.Unauthorized()));
        var result = await _phaseGate.ChangePhaseAsync(projectId, request.TargetPhaseId,
            request.ExpectedProjectVersionToken, actorId, cancellationToken);
        return result.IsFailure ? ToActionResult(result.Error!) : Ok(result.Value);
    }

    [HttpGet("history")]
    [Authorize(Policy = "Project.View")]
    public async Task<IActionResult> GetHistory(Guid projectId, CancellationToken cancellationToken)
    {
        // Phase-change history is immutable project data. It deliberately does not require
        // Document.View because it returns the recorded decision, not document contents.
        var result = await _phaseGate.GetPhaseHistoryAsync(projectId, cancellationToken);
        return result.IsFailure ? ToActionResult(result.Error!) : Ok(result.Value);
    }

    private bool TryGetActorId(out Guid actorId) =>
        Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value,
            out actorId) && actorId != Guid.Empty;

    private IActionResult ToActionResult(Error error)
    {
        var payload = Payload(error);
        return error.StatusCode switch
        {
            401 => Unauthorized(payload),
            403 => StatusCode(StatusCodes.Status403Forbidden, payload),
            404 => NotFound(payload),
            409 => Conflict(payload),
            _ => BadRequest(payload)
        };
    }

    private static object Payload(Error error) => new
    {
        code = error.Code,
        message = error.Message,
        details = error.Details
    };
}
