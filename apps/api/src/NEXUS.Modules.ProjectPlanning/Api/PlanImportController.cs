using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.ProjectPlanning.Application;
using NEXUS.Modules.ProjectPlanning.Application.DTOs;
using NEXUS.Modules.ProjectPlanning.Domain;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.ProjectPlanning.Api;

[ApiController]
[Authorize]
[Route("api/projects/{projectId:guid}/plan-import")]
public sealed class PlanImportController : ControllerBase
{
    private const long MaximumFileSize = 128L * 1024L * 1024L;
    private readonly IPlanningImportService _importService;

    public PlanImportController(IPlanningImportService importService)
    {
        _importService = importService;
    }

    [Authorize(Policy = "Planning.View")]
    [HttpPost("preview")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaximumFileSize)]
    public async Task<IActionResult> Preview(
        Guid projectId,
        IFormFile file,
        [FromForm] Guid? targetPlanId,
        CancellationToken cancellationToken)
    {
        var validation = ValidateFile(file);
        if (validation is not null) return BadRequest(new { error = validation.Message });

        await using var stream = file.OpenReadStream();
        var result = await _importService.PreviewAsync(stream, file.FileName, projectId, targetPlanId, cancellationToken);
        return ToActionResult(result);
    }

    [Authorize(Policy = "Planning.Manage")]
    [HttpPost("commit")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaximumFileSize)]
    public async Task<IActionResult> Commit(
        Guid projectId,
        IFormFile file,
        [FromForm] Guid? targetPlanId,
        [FromForm] string? newPlanName,
        [FromForm] PlanType? newPlanType,
        CancellationToken cancellationToken)
    {
        var validation = ValidateFile(file);
        if (validation is not null) return BadRequest(new { error = validation.Message });

        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "System";
        await using var stream = file.OpenReadStream();
        var result = await _importService.ImportAsync(stream, file.FileName, new CommitPlanImportDto
        {
            ProjectId = projectId,
            TargetPlanId = targetPlanId,
            NewPlanName = newPlanName,
            NewPlanType = newPlanType
        }, userId, cancellationToken);
        return ToActionResult(result);
    }

    private static Error? ValidateFile(IFormFile? file)
    {
        if (file is null || file.Length == 0) return Error.Validation("Choose a non-empty Microsoft Project .mpp file");
        if (file.Length > MaximumFileSize) return Error.Validation("The MPP file exceeds the 128 MB import limit");
        if (!string.Equals(Path.GetExtension(file.FileName), ".mpp", StringComparison.OrdinalIgnoreCase))
            return Error.Validation("Only Microsoft Project .mpp files are supported");
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
}
