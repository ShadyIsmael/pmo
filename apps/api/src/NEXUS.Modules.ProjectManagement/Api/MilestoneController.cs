using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.ProjectManagement.Application;

namespace NEXUS.Modules.ProjectManagement.Api;

[ApiController]
[Route("api/milestones")]
[Authorize]
public class MilestoneController : ControllerBase
{
    private readonly IMilestoneService _milestoneService;

    public MilestoneController(IMilestoneService milestoneService)
    {
        _milestoneService = milestoneService;
    }

    [Authorize(Policy = "Project.View")]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _milestoneService.GetByIdAsync(id, cancellationToken);
        if (result.IsFailure) return NotFound();
        return Ok(result.Value);
    }

    [Authorize(Policy = "Project.View")]
    [HttpGet]
    public async Task<IActionResult> GetByProject([FromQuery] Guid projectId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var result = await _milestoneService.GetByProjectAsync(projectId, page, pageSize, cancellationToken);
        return Ok(result);
    }
}
