using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.CostManagement.Application;

namespace NEXUS.Modules.CostManagement.Api;

[ApiController]
[Route("api/budgets")]
[Authorize]
public class BudgetController : ControllerBase
{
    private readonly IBudgetService _budgetService;

    public BudgetController(IBudgetService budgetService)
    {
        _budgetService = budgetService;
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _budgetService.GetByIdAsync(id, cancellationToken);
        if (result.IsFailure) return NotFound();
        return Ok(result.Value);
    }

    [HttpGet]
    public async Task<IActionResult> GetPaged([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] Guid? projectId = null, [FromQuery] Guid? portfolioId = null, CancellationToken cancellationToken = default)
    {
        var result = await _budgetService.GetPagedAsync(page, pageSize, projectId, portfolioId, cancellationToken);
        return Ok(result);
    }
}
