using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NEXUS.Api.PortfolioManagement;

[ApiController]
[Route("api/portfolio-management")]
[Authorize]
public sealed class PortfolioManagementController : ControllerBase
{
    private readonly IPortfolioManagementService _service;
    private readonly NEXUS.Api.ProgramManagement.IProgramPortfolioFinancialRollupService _financialRollups;

    public PortfolioManagementController(IPortfolioManagementService service, NEXUS.Api.ProgramManagement.IProgramPortfolioFinancialRollupService financialRollups)
    {
        _service = service;
        _financialRollups = financialRollups;
    }

    [Authorize(Policy = "Portfolio.View")]
    [HttpGet("portfolios")]
    public async Task<IActionResult> GetPortfolios([FromQuery] int page = 1, [FromQuery] int pageSize = 25, [FromQuery] string? search = null, CancellationToken cancellationToken = default)
        => Ok(await _service.GetPortfoliosAsync(page, pageSize, search, cancellationToken));

    [Authorize(Policy = "Portfolio.View")]
    [HttpGet("portfolios/{portfolioId:guid}/overview")]
    public async Task<IActionResult> GetOverview(Guid portfolioId, [FromQuery] int programsPage = 1, [FromQuery] int projectsPage = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var result = await _service.GetOverviewAsync(portfolioId, programsPage, projectsPage, pageSize, cancellationToken);
        return result.IsFailure ? NotFound() : Ok(result.Value);
    }

    [Authorize(Policy = "Portfolio.View")]
    [Authorize(Policy = "PortfolioFinancialRollup.View")]
    [HttpGet("portfolios/{portfolioId:guid}/financial-rollup")]
    public async Task<IActionResult> GetFinancialRollup(Guid portfolioId, [FromQuery] int? fiscalYear = null,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var result = await _financialRollups.GetPortfolioAsync(portfolioId, fiscalYear, page, pageSize, cancellationToken);
        return result.IsFailure
            ? StatusCode(result.Error?.StatusCode ?? StatusCodes.Status500InternalServerError, new { error = result.Error?.Message })
            : Ok(result.Value);
    }
}
