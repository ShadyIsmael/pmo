using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.CostManagement.Application;

namespace NEXUS.Modules.CostManagement.Api;

[ApiController]
[Route("api/costs")]
[Authorize]
public class CostController : ControllerBase
{
    private readonly ICostService _costService;

    public CostController(ICostService costService)
    {
        _costService = costService;
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _costService.GetByIdAsync(id, cancellationToken);
        if (result.IsFailure) return NotFound();
        return Ok(result.Value);
    }

    [HttpGet]
    public async Task<IActionResult> GetPaged([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] Guid? projectId = null, CancellationToken cancellationToken = default)
    {
        var result = await _costService.GetPagedAsync(page, pageSize, projectId, cancellationToken);
        return Ok(result);
    }
}
