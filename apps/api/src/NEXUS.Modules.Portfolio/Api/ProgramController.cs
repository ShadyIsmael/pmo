using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.Portfolio.Application;

namespace NEXUS.Modules.Portfolio.Api;

[ApiController]
[Route("api/programs")]
[Authorize]
public class ProgramController : ControllerBase
{
    private readonly IProgramService _programService;

    public ProgramController(IProgramService programService)
    {
        _programService = programService;
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _programService.GetByIdAsync(id, cancellationToken);
        if (result.IsFailure) return NotFound();
        return Ok(result.Value);
    }

    [HttpGet]
    public async Task<IActionResult> GetByPortfolio([FromQuery] Guid? portfolioId = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 200, CancellationToken cancellationToken = default)
    {
        var result = await _programService.GetByPortfolioAsync(portfolioId, page, pageSize, cancellationToken);
        return Ok(result);
    }
}
