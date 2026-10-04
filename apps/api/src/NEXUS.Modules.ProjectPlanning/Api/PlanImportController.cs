using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.ProjectPlanning.Application;
using NEXUS.Modules.ProjectPlanning.Application.DTOs;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.ProjectPlanning.Api;

[ApiController]
[Authorize]
[Route("api/projects/{projectId:guid}/plan-imports")]
public sealed class PlanImportController : ControllerBase
{
    private const long MaximumFileSize = 128L * 1024L * 1024L;
    private readonly IPlanningImportService _importService;

    public PlanImportController(IPlanningImportService importService) => _importService = importService;

    [Authorize(Policy = "Planning.View")]
    [HttpPost("sessions")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaximumFileSize)]
    public async Task<IActionResult> CreateSession(Guid projectId, IFormFile file, [FromForm] Guid? targetPlanId, CancellationToken cancellationToken)
    {
        var validation = ValidateFile(file);
        if (validation is not null) return BadRequest(new { error = validation.Message });
        await using var stream = file.OpenReadStream();
        var result = await _importService.CreateSessionAsync(stream, file.FileName, projectId, targetPlanId, CurrentUserId(), cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Policy = "Planning.View")]
    [HttpGet("sessions/{sessionId:guid}")]
    public async Task<IActionResult> GetSession(Guid projectId, Guid sessionId, CancellationToken cancellationToken) =>
        ToActionResult(await _importService.GetSessionAsync(projectId, sessionId, cancellationToken));

    [Authorize(Policy = "Planning.Manage")]
    [HttpPatch("sessions/{sessionId:guid}/resources/{sourceResourceId}")]
    public async Task<IActionResult> ResolveResource(Guid projectId, Guid sessionId, string sourceResourceId, [FromBody] PlanImportResourceResolutionDto resolution, CancellationToken cancellationToken) =>
        ToActionResult(await _importService.ResolveResourceAsync(projectId, sessionId, sourceResourceId, resolution, CurrentUserId(), cancellationToken));

    [Authorize(Policy = "Planning.Manage")]
    [HttpPatch("sessions/{sessionId:guid}/assignments/{sourceAssignmentId}")]
    public async Task<IActionResult> ResolveAssignment(Guid projectId, Guid sessionId, string sourceAssignmentId, [FromBody] PlanImportAssignmentResolutionDto resolution, CancellationToken cancellationToken) =>
        ToActionResult(await _importService.ResolveAssignmentAsync(projectId, sessionId, sourceAssignmentId, resolution, CurrentUserId(), cancellationToken));

    [Authorize(Policy = "Planning.Manage")]
    [HttpPatch("sessions/{sessionId:guid}/calendars/{sourceCalendarId}")]
    public async Task<IActionResult> ResolveCalendar(Guid projectId, Guid sessionId, string sourceCalendarId, [FromBody] PlanImportCalendarResolutionDto resolution, CancellationToken cancellationToken) =>
        ToActionResult(await _importService.ResolveCalendarAsync(projectId, sessionId, sourceCalendarId, resolution, CurrentUserId(), cancellationToken));

    [Authorize(Policy = "Planning.Manage")]
    [HttpPost("sessions/{sessionId:guid}/commit")]
    public async Task<IActionResult> Commit(Guid projectId, Guid sessionId, [FromBody] CommitPlanImportDto request, CancellationToken cancellationToken)
    {
        request.ProjectId = projectId;
        return ToActionResult(await _importService.CommitSessionAsync(projectId, sessionId, request, CurrentUserId(), cancellationToken));
    }

    [Authorize(Policy = "Planning.Manage")]
    [HttpPost("sessions/{sessionId:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid projectId, Guid sessionId, CancellationToken cancellationToken) =>
        ToActionResult(await _importService.CancelSessionAsync(projectId, sessionId, CurrentUserId(), cancellationToken));

    private string CurrentUserId() => User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "System";

    private static Error? ValidateFile(IFormFile? file)
    {
        if (file is null || file.Length == 0) return Error.Validation("Choose a non-empty Microsoft Project .mpp file");
        if (file.Length > MaximumFileSize) return Error.Validation("The MPP file exceeds the 128 MB import limit");
        if (!string.Equals(Path.GetExtension(file.FileName), ".mpp", StringComparison.OrdinalIgnoreCase)) return Error.Validation("Only Microsoft Project .mpp files are supported");
        return null;
    }

    private IActionResult ToActionResult<T>(Result<T> result)
    {
        if (result.IsSuccess) return Ok(result.Value);
        return result.Error?.StatusCode switch
        {
            404 => NotFound(new { error = result.Error.Message }),
            409 => Conflict(new { error = result.Error.Message }),
            _ => BadRequest(new { error = result.Error?.Message ?? "The plan import failed" })
        };
    }

    private IActionResult ToActionResult(Result result)
    {
        if (result.IsSuccess) return NoContent();
        return result.Error?.StatusCode switch
        {
            404 => NotFound(new { error = result.Error.Message }),
            409 => Conflict(new { error = result.Error.Message }),
            _ => BadRequest(new { error = result.Error?.Message ?? "The plan import failed" })
        };
    }
}
