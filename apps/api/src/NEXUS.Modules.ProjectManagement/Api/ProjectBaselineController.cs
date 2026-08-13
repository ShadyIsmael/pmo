using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.ProjectManagement.Application;

namespace NEXUS.Modules.ProjectManagement.Api;

[ApiController]
[Route("api/projects/{projectId:guid}/baselines")]
[Authorize]
public class ProjectBaselineController : ControllerBase
{
    private readonly IProjectBaselineService _baselineService;

    public ProjectBaselineController(IProjectBaselineService baselineService)
    {
        _baselineService = baselineService;
    }

    [HttpPost("initial")]
    public async Task<IActionResult> CreateInitialBaseline(Guid projectId, CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "System";
        var result = await _baselineService.CreateInitialBaselineAsync(projectId, userId, cancellationToken);
        if (result.IsFailure)
        {
            return result.Error?.StatusCode switch
            {
                404 => NotFound(new { error = result.Error.Message }),
                409 => Conflict(new { error = result.Error.Message }),
                401 => Unauthorized(new { error = result.Error.Message }),
                _ => BadRequest(new { error = result.Error?.Message })
            };
        }
        return Ok(result.Value);
    }
}