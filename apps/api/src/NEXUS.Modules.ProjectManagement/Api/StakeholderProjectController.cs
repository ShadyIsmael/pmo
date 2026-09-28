using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.ProjectManagement.Application;

namespace NEXUS.Modules.ProjectManagement.Api;

[ApiController]
[Route("api/stakeholder/projects")]
[Authorize]
public sealed class StakeholderProjectController : ControllerBase
{
    private readonly IStakeholderProjectReadService _service;

    public StakeholderProjectController(IStakeholderProjectReadService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> GetMine(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _service.GetMyProjectsAsync(page, pageSize, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("{projectId:guid}")]
    public async Task<IActionResult> GetMine(Guid projectId, CancellationToken cancellationToken = default)
    {
        var result = await _service.GetMyProjectAsync(projectId, cancellationToken);
        return ToActionResult(result);
    }

    private IActionResult ToActionResult<T>(NEXUS.SharedKernel.Result<T> result)
    {
        if (result.IsSuccess) return Ok(result.Value);
        return result.Error?.StatusCode switch
        {
            401 => Unauthorized(new { error = result.Error.Message }),
            403 => Forbid(),
            404 => NotFound(new { error = result.Error.Message }),
            _ => BadRequest(new { error = result.Error?.Message })
        };
    }
}
