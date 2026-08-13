using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.RiskManagement.Application;

namespace NEXUS.Modules.RiskManagement.Api;

[ApiController]
[Route("api/risks")]
[Authorize]
public class RiskController : ControllerBase
{
    private readonly IRiskService _riskService;

    public RiskController(IRiskService riskService)
    {
        _riskService = riskService;
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _riskService.GetByIdAsync(id, cancellationToken);
        if (result.IsFailure) return NotFound();
        return Ok(result.Value);
    }

    [HttpGet]
    public async Task<IActionResult> GetPaged([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] Guid? projectId = null, [FromQuery] string? status = null, CancellationToken cancellationToken = default)
    {
        var result = await _riskService.GetPagedAsync(page, pageSize, projectId, status, cancellationToken);
        return Ok(result);
    }
}
