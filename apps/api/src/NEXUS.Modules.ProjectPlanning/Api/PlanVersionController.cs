using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.ProjectPlanning.Application;

namespace NEXUS.Modules.ProjectPlanning.Api;

[ApiController]
[Route("api/projects/{projectId:guid}/plans/{planId:guid}/versions")]
[Authorize]
public class PlanVersionController : ControllerBase
{
    private readonly IPlanVersionService _versionService;
    private readonly IPlanVersionComparisonService _comparisonService;

    public PlanVersionController(IPlanVersionService versionService, IPlanVersionComparisonService comparisonService)
    {
        _versionService = versionService;
        _comparisonService = comparisonService;
    }

    [Authorize(Policy = "Planning.View")]
    [HttpGet("{fromVersionId:guid}/compare/{toVersionId:guid}")]
    public async Task<IActionResult> Compare(
        Guid projectId,
        Guid planId,
        Guid fromVersionId,
        Guid toVersionId,
        CancellationToken cancellationToken)
    {
        var result = await _comparisonService.CompareAsync(planId, fromVersionId, toVersionId, cancellationToken);
        return ToActionResult(result, Ok);
    }

    [Authorize(Policy = "Planning.View")]
    [HttpGet("working")]
    public async Task<IActionResult> GetWorking(Guid projectId, Guid planId, CancellationToken cancellationToken)
    {
        var result = await _versionService.GetActiveWorkingVersionAsync(planId, cancellationToken);
        return ToActionResult(result, Ok);
    }

    [Authorize(Policy = "Planning.Lifecycle")]
    [HttpPost("working/from-current-baseline")]
    public async Task<IActionResult> CreateWorking(Guid projectId, Guid planId, CancellationToken cancellationToken)
    {
        var result = await _versionService.CreateWorkingFromCurrentBaselineAsync(planId, UserId(), cancellationToken);
        return ToActionResult(result, value => CreatedAtAction(nameof(GetWorking), new { projectId, planId }, value));
    }

    [Authorize(Policy = "Planning.Lifecycle")]
    [HttpDelete("working")]
    public async Task<IActionResult> DiscardWorking(Guid projectId, Guid planId, [FromQuery] byte[] rowVersion, CancellationToken cancellationToken)
    {
        var result = await _versionService.DiscardWorkingVersionAsync(planId, rowVersion, UserId(), cancellationToken);
        return result.IsSuccess ? NoContent() : ErrorResult(result.Error);
    }

    [Authorize(Policy = "Planning.Lifecycle")]
    [HttpPost("baseline/initial")]
    public async Task<IActionResult> CreateInitialBaseline(Guid projectId, Guid planId, [FromQuery] byte[] rowVersion, CancellationToken cancellationToken)
    {
        var result = await _versionService.CreateInitialBaselineAsync(planId, rowVersion, UserId(), cancellationToken);
        return ToActionResult(result, value => CreatedAtAction(nameof(GetWorking), new { projectId, planId }, value));
    }

    [Authorize(Policy = "Planning.Lifecycle")]
    [HttpPost("baseline")]
    public async Task<IActionResult> CaptureBaseline(Guid projectId, Guid planId, [FromQuery] byte[] rowVersion, CancellationToken cancellationToken)
    {
        var result = await _versionService.CaptureBaselineAsync(planId, rowVersion, UserId(), cancellationToken);
        return ToActionResult(result, value => CreatedAtAction(nameof(GetWorking), new { projectId, planId }, value));
    }

    [Authorize(Policy = "Planning.Lifecycle")]
    [HttpPost("baseline/promote")]
    public async Task<IActionResult> Promote(Guid projectId, Guid planId, [FromQuery] byte[] rowVersion, CancellationToken cancellationToken)
    {
        var result = await _versionService.PromoteWorkingToBaselineAsync(planId, rowVersion, UserId(), cancellationToken);
        return ToActionResult(result, Ok);
    }

    private string UserId() =>
        User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "System";

    private IActionResult ToActionResult<T>(NEXUS.SharedKernel.Result<T> result, Func<T?, IActionResult> success) =>
        result.IsSuccess ? success(result.Value) : ErrorResult(result.Error);

    private IActionResult ErrorResult(NEXUS.SharedKernel.Error? error) => error?.StatusCode switch
    {
        404 => NotFound(new { error = error.Message }),
        409 => Conflict(new { error = error.Message }),
        _ => BadRequest(new { error = error?.Message })
    };
}
