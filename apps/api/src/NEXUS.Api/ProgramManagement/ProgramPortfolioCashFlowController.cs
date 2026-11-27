using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NEXUS.Api.Finance;
using NEXUS.Modules.CostManagement.Application.DTOs;
using NEXUS.Persistence;

namespace NEXUS.Api.ProgramManagement;

[ApiController]
[Authorize]
[Route("api")]
public sealed class ProgramPortfolioCashFlowController(
    AppDbContext db,
    IProgramPortfolioFinancialRollupService financialRollups) : ControllerBase
{
    [Authorize(Policy = "Program.View")]
    [Authorize(Policy = "ProgramFinancialRollup.View")]
    [HttpGet("program-management/programs/{programId:guid}/cash-flow")]
    public async Task<IActionResult> GetProgramCashFlow(
        Guid programId,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] CashFlowGranularity granularity = CashFlowGranularity.Month,
        CancellationToken cancellationToken = default)
    {
        if (!TryValidateRange(from, to, granularity, out var error)) return BadRequest(new { error });

        var scopeResult = await financialRollups.GetProgramCashFlowProjectScopeAsync(programId, cancellationToken);
        if (scopeResult.IsFailure)
        {
            return StatusCode(scopeResult.Error?.StatusCode ?? StatusCodes.Status500InternalServerError,
                new { error = scopeResult.Error?.Message });
        }

        var scope = scopeResult.Value!;
        var currencies = scope.Projects.ToDictionary(project => project.ProjectId, project => project.Currency);
        var projectCashFlows = await ProjectCashFlowReader.ReadManyAsync(
            db, scope.TenantId, currencies, from!.Value, to!.Value, granularity, cancellationToken);
        var response = ProgramPortfolioCashFlowAggregation.Aggregate(
            "Program", programId, from.Value, to.Value, granularity, scope, projectCashFlows);
        return Ok(response);
    }

    [Authorize(Policy = "Portfolio.View")]
    [Authorize(Policy = "PortfolioFinancialRollup.View")]
    [HttpGet("portfolio-management/portfolios/{portfolioId:guid}/cash-flow")]
    public async Task<IActionResult> GetPortfolioCashFlow(
        Guid portfolioId,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] CashFlowGranularity granularity = CashFlowGranularity.Month,
        CancellationToken cancellationToken = default)
    {
        if (!TryValidateRange(from, to, granularity, out var error)) return BadRequest(new { error });

        var scopeResult = await financialRollups.GetPortfolioCashFlowProjectScopeAsync(portfolioId, cancellationToken);
        if (scopeResult.IsFailure)
        {
            return StatusCode(scopeResult.Error?.StatusCode ?? StatusCodes.Status500InternalServerError,
                new { error = scopeResult.Error?.Message });
        }

        var scope = scopeResult.Value!;
        var currencies = scope.Projects.ToDictionary(project => project.ProjectId, project => project.Currency);
        var projectCashFlows = await ProjectCashFlowReader.ReadManyAsync(
            db, scope.TenantId, currencies, from!.Value, to!.Value, granularity, cancellationToken);
        var response = ProgramPortfolioCashFlowAggregation.Aggregate(
            "Portfolio", portfolioId, from.Value, to.Value, granularity, scope, projectCashFlows);
        return Ok(response);
    }

    private static bool TryValidateRange(
        DateOnly? from,
        DateOnly? to,
        CashFlowGranularity granularity,
        out string? error)
    {
        if (!from.HasValue || !to.HasValue)
        {
            error = "Both cash-flow range dates are required.";
            return false;
        }

        if (from.Value > to.Value)
        {
            error = "The cash-flow range must start on or before it ends.";
            return false;
        }

        if (!Enum.IsDefined(granularity))
        {
            error = "The cash-flow granularity is not supported.";
            return false;
        }

        error = null;
        return true;
    }
}
