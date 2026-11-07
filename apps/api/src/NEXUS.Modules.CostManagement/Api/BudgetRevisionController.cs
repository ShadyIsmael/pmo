using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.CostManagement.Application;
using NEXUS.Modules.CostManagement.Application.DTOs;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.CostManagement.Api;

[ApiController]
[Route("api/budget-revisions")]
[Authorize]
public sealed class BudgetRevisionController(IBudgetRevisionService revisionService) : ControllerBase
{
    [Authorize(Policy = "BudgetRevision.View")]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken) =>
        ToActionResult(await revisionService.GetByIdAsync(id, cancellationToken));

    [Authorize(Policy = "BudgetRevision.View")]
    [HttpGet("by-budget/{budgetId:guid}")]
    public async Task<IActionResult> GetByBudgetId(Guid budgetId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default) =>
        Ok(await revisionService.GetByBudgetIdAsync(budgetId, page, pageSize, cancellationToken));

    [Authorize(Policy = "BudgetRevision.View")]
    [HttpGet("by-project/{projectId:guid}")]
    public async Task<IActionResult> GetByProjectId(Guid projectId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? status = null, CancellationToken cancellationToken = default) =>
        Ok(await revisionService.GetByProjectIdAsync(projectId, page, pageSize, status, cancellationToken));

    [Authorize(Policy = "BudgetRevision.Create")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateBudgetRevisionDto dto, CancellationToken cancellationToken) =>
        ToActionResult(await revisionService.CreateAsync(dto, UserId(), cancellationToken), nameof(GetById));

    [Authorize(Policy = "BudgetRevision.EditDraft")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateBudgetRevisionDto dto, CancellationToken cancellationToken) =>
        ToActionResult(await revisionService.UpdateAsync(id, dto, UserId(), cancellationToken));

    [Authorize(Policy = "BudgetRevision.SubmitForReview")]
    [HttpPost("{id:guid}/submit")]
    public async Task<IActionResult> Submit(Guid id, [FromBody] SubmitBudgetRevisionDto dto, CancellationToken cancellationToken) =>
        ToActionResult(await revisionService.SubmitAsync(id, dto, UserId(), cancellationToken));

    [Authorize(Policy = "BudgetRevision.Approve")]
    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, [FromBody] ReviewBudgetRevisionDto dto, CancellationToken cancellationToken) =>
        ToActionResult(await revisionService.ApproveAsync(id, dto, UserId(), cancellationToken));

    [Authorize(Policy = "BudgetRevision.Approve")]
    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, [FromBody] ReviewBudgetRevisionDto dto, CancellationToken cancellationToken) =>
        ToActionResult(await revisionService.RejectAsync(id, dto, UserId(), cancellationToken));

    [Authorize(Policy = "BudgetRevision.Cancel")]
    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken) =>
        ToActionResult(await revisionService.CancelAsync(id, UserId(), cancellationToken));

    [Authorize(Policy = "BudgetRevision.Delete")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, [FromQuery] byte[]? rowVersion, CancellationToken cancellationToken) =>
        ToActionResult(await revisionService.DeleteAsync(id, rowVersion ?? [], UserId(), cancellationToken));

    private string UserId() => User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "System";

    private IActionResult ToActionResult<T>(Result<T> result, string? createdAction = null) =>
        result.IsFailure ? ErrorResult(result.Error) :
        createdAction is null ? Ok(result.Value) :
        CreatedAtAction(createdAction, new { id = result.Value is BudgetRevisionDto rev ? rev.Id : Guid.Empty }, result.Value);

    private IActionResult ToActionResult(Result result) =>
        result.IsFailure ? ErrorResult(result.Error) : NoContent();

    private IActionResult ErrorResult(Error? error) => error?.StatusCode switch
    {
        401 => Unauthorized(new { error = error.Message }),
        403 => Forbid(),
        404 => NotFound(new { error = error.Message }),
        409 => Conflict(new { error = error.Message }),
        _ => BadRequest(new { error = error?.Message ?? "Request failed" })
    };
}
