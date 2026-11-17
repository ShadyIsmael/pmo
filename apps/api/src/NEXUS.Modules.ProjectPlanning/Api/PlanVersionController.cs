using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NEXUS.Modules.ProjectPlanning.Application;

namespace NEXUS.Modules.ProjectPlanning.Api;

[ApiController]
[Route("api/projects/{projectId:guid}/plans/{planId:guid}/versions")]
[Authorize]
public class PlanVersionController : ControllerBase
{
    private readonly IPlanVersionService _versionService;
    private readonly IPlanVersionComparisonService _comparisonService;
    private readonly ILogger<PlanVersionController> _logger;

    public PlanVersionController(
        IPlanVersionService versionService,
        IPlanVersionComparisonService comparisonService,
        ILogger<PlanVersionController> logger)
    {
        _versionService = versionService;
        _comparisonService = comparisonService;
        _logger = logger;
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
        if (!TryGetActorId(out var actorId)) return Unauthorized();
        var result = await _versionService.CreateWorkingFromCurrentBaselineAsync(planId, actorId, cancellationToken);
        return ToActionResult(result, value => CreatedAtAction(nameof(GetWorking), new { projectId, planId }, value));
    }

    [Authorize(Policy = "Planning.Lifecycle")]
    [HttpDelete("working")]
    public async Task<IActionResult> DiscardWorking(Guid projectId, Guid planId, [FromQuery] byte[] rowVersion, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId)) return Unauthorized();
        var result = await _versionService.DiscardWorkingVersionAsync(planId, rowVersion, actorId, cancellationToken);
        return result.IsSuccess ? NoContent() : ErrorResult(result.Error);
    }

    [Authorize(Policy = "Planning.Lifecycle")]
    [HttpPost("baseline/initial")]
    public async Task<IActionResult> CreateInitialBaseline(Guid projectId, Guid planId, [FromQuery] byte[] rowVersion, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId)) return Unauthorized();
        var result = await _versionService.CreateInitialBaselineAsync(planId, rowVersion, actorId, cancellationToken);
        return ToActionResult(result, value => CreatedAtAction(nameof(GetWorking), new { projectId, planId }, value));
    }

    [Authorize(Policy = "Planning.Lifecycle")]
    [HttpPost("baseline")]
    public async Task<IActionResult> CaptureBaseline(Guid projectId, Guid planId, [FromQuery] byte[] rowVersion, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId)) return Unauthorized();
        try
        {
            var result = await _versionService.CaptureBaselineAsync(planId, rowVersion, actorId, cancellationToken);
            return ToActionResult(result, value => CreatedAtAction(nameof(GetWorking), new { projectId, planId }, value));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unhandled error while capturing a plan baseline for project {ProjectId}, plan {PlanId}.", projectId, planId);
            throw;
        }
    }

    [Authorize(Policy = "Planning.Lifecycle")]
    [HttpPost("baseline/promote")]
    public async Task<IActionResult> Promote(Guid projectId, Guid planId, [FromQuery] byte[] rowVersion, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out var actorId)) return Unauthorized();
        var result = await _versionService.PromoteWorkingToBaselineAsync(planId, rowVersion, actorId, cancellationToken);
        return ToActionResult(result, Ok);
    }

    private bool TryGetActorId(out string actorId)
    {
        actorId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        return User.Identity?.IsAuthenticated == true && Guid.TryParse(actorId, out var parsed) && parsed != Guid.Empty;
    }

    private IActionResult ToActionResult<T>(NEXUS.SharedKernel.Result<T> result, Func<T?, IActionResult> success) =>
        result.IsSuccess ? success(result.Value) : ErrorResult(result.Error);

    private IActionResult ErrorResult(NEXUS.SharedKernel.Error? error) => error?.StatusCode switch
    {
        404 => NotFound(new { error = error.Message }),
        409 => Conflict(new { error = error.Message }),
        _ => BadRequest(new { error = error?.Message })
    };
}
