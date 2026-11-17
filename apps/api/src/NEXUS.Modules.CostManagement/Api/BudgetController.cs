using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.CostManagement.Application;
using NEXUS.Modules.CostManagement.Application.DTOs;

namespace NEXUS.Modules.CostManagement.Api;

[ApiController]
[Route("api/budgets")]
[Authorize]
public sealed class BudgetController(IBudgetService budgetService) : ControllerBase
{
    [Authorize(Policy = "Budget.View")]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken) => ToActionResult(await budgetService.GetByIdAsync(id, cancellationToken));

    [Authorize(Policy = "Budget.View")]
    [HttpGet]
    public async Task<IActionResult> GetPaged([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] Guid? projectId = null, [FromQuery] Guid? portfolioId = null, [FromQuery] bool? hasForecast = null, CancellationToken cancellationToken = default) => Ok(await budgetService.GetPagedAsync(page, pageSize, projectId, portfolioId, hasForecast, cancellationToken));

    [Authorize(Policy = "Budget.Create")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateBudgetDto dto, CancellationToken cancellationToken) => ToActionResult(await budgetService.CreateAsync(dto, UserId(), cancellationToken), nameof(GetById));

    [Authorize(Policy = "Budget.Edit")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateBudgetDto dto, CancellationToken cancellationToken) => ToActionResult(await budgetService.UpdateAsync(id, dto, UserId(), cancellationToken));

    [Authorize(Policy = "Budget.Delete")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, [FromBody] DeleteFinancialRecordDto? dto, CancellationToken cancellationToken) => ToActionResult(await budgetService.DeleteAsync(id, dto?.RowVersion ?? Array.Empty<byte>(), UserId(), cancellationToken));

    private string UserId() => User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
    private IActionResult ToActionResult<T>(NEXUS.SharedKernel.Result<T> result, string? createdAction = null) => result.IsFailure ? ErrorResult(result.Error) : createdAction is null ? Ok(result.Value) : CreatedAtAction(createdAction, new { id = result.Value is BudgetDto budget ? budget.Id : Guid.Empty }, result.Value);
    private IActionResult ToActionResult(NEXUS.SharedKernel.Result result) => result.IsFailure ? ErrorResult(result.Error) : NoContent();
    private IActionResult ErrorResult(NEXUS.SharedKernel.Error? error) => error?.StatusCode switch { 401 => Unauthorized(new { error = error.Message }), 403 => Forbid(), 404 => NotFound(new { error = error.Message }), 409 => Conflict(new { error = error.Message }), _ => BadRequest(new { error = error?.Message ?? "Request failed" }) };
}
