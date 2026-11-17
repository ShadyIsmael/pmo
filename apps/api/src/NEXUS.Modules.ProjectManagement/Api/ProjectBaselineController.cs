using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.ProjectManagement.Application;
using NEXUS.SharedKernel.Management;

namespace NEXUS.Modules.ProjectManagement.Api;

[ApiController]
[Route("api/projects/{projectId:guid}/baselines")]
[Authorize]
public class ProjectBaselineController : ControllerBase
{
    private readonly IProjectBaselineService _baselineService;
    private readonly IManagementContextAccessor? _managementContext;

    public ProjectBaselineController(IProjectBaselineService baselineService, IManagementContextAccessor? managementContext = null)
    {
        _baselineService = baselineService;
        _managementContext = managementContext;
    }

    [Authorize(Policy = "Project.Edit")]
    [HttpPost("initial")]
    public async Task<IActionResult> CreateInitialBaseline(Guid projectId, CancellationToken cancellationToken)
    {
        var denied = ManagementActorBinding.RequireActor(this, _managementContext, requireWritable: true, out var userId);
        if (denied is not null) return denied;
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
