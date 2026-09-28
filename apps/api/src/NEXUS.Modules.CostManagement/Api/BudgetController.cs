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
    [Authorize(Policy = "Finances.Read")]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken) => ToActionResult(await budgetService.GetByIdAsync(id, cancellationToken));

    [Authorize(Policy = "Finances.Read")]
    [HttpGet]
    public async Task<IActionResult> GetPaged([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] Guid? projectId = null, [FromQuery] Guid? portfolioId = null, [FromQuery] bool? hasForecast = null, CancellationToken cancellationToken = default) => Ok(await budgetService.GetPagedAsync(page, pageSize, projectId, portfolioId, hasForecast, cancellationToken));

    [Authorize(Policy = "Finances.Write")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateBudgetDto dto, CancellationToken cancellationToken) => ToActionResult(await budgetService.CreateAsync(dto, UserId(), cancellationToken), nameof(GetById));

    [Authorize(Policy = "Finances.Write")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateBudgetDto dto, CancellationToken cancellationToken) => ToActionResult(await budgetService.UpdateAsync(id, dto, UserId(), cancellationToken));

    [Authorize(Policy = "Finances.Write")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, [FromBody] DeleteFinancialRecordDto? dto, CancellationToken cancellationToken) => ToActionResult(await budgetService.DeleteAsync(id, dto?.RowVersion ?? Array.Empty<byte>(), UserId(), cancellationToken));

    private string UserId() => User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "System";
    private IActionResult ToActionResult<T>(NEXUS.SharedKernel.Result<T> result, string? createdAction = null) => result.IsFailure ? ErrorResult(result.Error) : createdAction is null ? Ok(result.Value) : CreatedAtAction(createdAction, new { id = result.Value is BudgetDto budget ? budget.Id : Guid.Empty }, result.Value);
    private IActionResult ToActionResult(NEXUS.SharedKernel.Result result) => result.IsFailure ? ErrorResult(result.Error) : NoContent();
    private IActionResult ErrorResult(NEXUS.SharedKernel.Error? error) => error?.StatusCode switch { 404 => NotFound(new { error = error.Message }), 409 => Conflict(new { error = error.Message }), _ => BadRequest(new { error = error?.Message ?? "Request failed" }) };
}
