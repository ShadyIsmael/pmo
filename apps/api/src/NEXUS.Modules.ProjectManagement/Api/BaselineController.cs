using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.ProjectManagement.Application;

namespace NEXUS.Modules.ProjectManagement.Api;

[ApiController]
[Route("api/baselines")]
[Authorize]
public class BaselineController : ControllerBase
{
    private readonly IBaselineService _baselineService;

    public BaselineController(IBaselineService baselineService)
    {
        _baselineService = baselineService;
    }

    [Authorize(Policy = "Project.View")]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _baselineService.GetByIdAsync(id, cancellationToken);
        if (result.IsFailure) return NotFound();
        return Ok(result.Value);
    }

    [Authorize(Policy = "Project.View")]
    [HttpGet]
    public async Task<IActionResult> GetByProject([FromQuery] Guid projectId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var result = await _baselineService.GetByProjectAsync(projectId, page, pageSize, cancellationToken);
        return Ok(result);
    }
}
