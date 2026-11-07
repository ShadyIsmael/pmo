using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.ProjectPlanning.Application;
using NEXUS.Modules.ProjectPlanning.Application.DTOs;

namespace NEXUS.Modules.ProjectPlanning.Api;

[ApiController]
[Route("api/projects/{projectId:guid}/plans")]
[Authorize]
public class PlanController : ControllerBase
{
    private readonly IPlanService _planService;

    public PlanController(IPlanService planService)
    {
        _planService = planService;
    }

    [Authorize(Policy = "Planning.View")]
    [HttpGet]
    public async Task<IActionResult> GetByProject(Guid projectId, CancellationToken cancellationToken)
    {
        var result = await _planService.GetByProjectAsync(projectId, cancellationToken);
        if (result.IsFailure) return NotFound(new { error = result.Error?.Message });
        return Ok(result.Value);
    }

    [Authorize(Policy = "Planning.View")]
    [HttpGet("bootstrap")]
    public async Task<IActionResult> GetBootstrap(Guid projectId, CancellationToken cancellationToken)
    {
        var result = await _planService.GetBootstrapAsync(projectId, cancellationToken);
        if (result.IsFailure) return NotFound(new { error = result.Error?.Message });
        return Ok(result.Value);
    }

    [Authorize(Policy = "Planning.Edit")]
    [HttpPost]
    public async Task<IActionResult> Create(Guid projectId, [FromBody] CreatePlanDto dto, CancellationToken cancellationToken)
    {
        dto.ProjectId = projectId;
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "System";
        var result = await _planService.CreateAsync(dto, userId, cancellationToken);
        if (result.IsFailure)
            return result.Error?.StatusCode switch
            {
                409 => Conflict(new { error = result.Error.Message }),
                _ => BadRequest(new { error = result.Error?.Message })
            };
        return CreatedAtAction(nameof(GetById), new { projectId, id = result.Value!.Id }, result.Value);
    }

    [Authorize(Policy = "Planning.View")]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid projectId, Guid id, CancellationToken cancellationToken)
    {
        var result = await _planService.GetByIdAsync(id, cancellationToken);
        if (result.IsFailure) return NotFound(new { error = result.Error?.Message });
        return Ok(result.Value);
    }

    [Authorize(Policy = "Planning.View")]
    [HttpGet("{id:guid}/working-version")]
    public async Task<IActionResult> GetActiveWorkingVersion(Guid projectId, Guid id, CancellationToken cancellationToken)
    {
        var result = await _planService.GetActiveWorkingVersionAsync(id, cancellationToken);
        if (result.IsFailure)
            return result.Error?.StatusCode switch
            {
                404 => NotFound(new { error = result.Error.Message }),
                _ => BadRequest(new { error = result.Error?.Message })
            };
        return Ok(result.Value);
    }
}
