using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.AIPlanning.Application;

namespace NEXUS.Modules.AIPlanning.Api;

[ApiController]
[Route("api/ai-planning")]
//[Authorize]
public class AIPlanningController : ControllerBase
{
    private readonly IAIPlanningService _aiPlanningService;

    public AIPlanningController(IAIPlanningService aiPlanningService)
    {
        _aiPlanningService = aiPlanningService;
    }

    [HttpPost("generate")]
    public async Task<IActionResult> GeneratePlan([FromBody] PlanGenerationCommand command, CancellationToken cancellationToken)
    {
        var result = await _aiPlanningService.GeneratePlanAsync(command, cancellationToken);
        if (result.IsFailure) return BadRequest(new ProblemDetails { Title = result.Error?.Message, Status = 400 });
        return Ok(result.Value);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _aiPlanningService.GetByIdAsync(id, cancellationToken);
        if (result.IsFailure) return NotFound();
        return Ok(result.Value);
    }

    [HttpGet]
    public async Task<IActionResult> GetPaged([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var result = await _aiPlanningService.GetPagedAsync(page, pageSize, cancellationToken);
        return Ok(result);
    }
}
