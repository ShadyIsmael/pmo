using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.CostManagement.Application;
using NEXUS.Modules.CostManagement.Application.DTOs;

namespace NEXUS.Modules.CostManagement.Api;

[ApiController]
[Route("api/costs")]
[Authorize]
public sealed class CostController(ICostService costService) : ControllerBase
{
    [Authorize(Policy = "Cost.View")]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken) => ToActionResult(await costService.GetByIdAsync(id, cancellationToken));

    [Authorize(Policy = "Cost.View")]
    [HttpGet]
    public async Task<IActionResult> GetPaged([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] Guid? projectId = null, CancellationToken cancellationToken = default) => Ok(await costService.GetPagedAsync(page, pageSize, projectId, cancellationToken));

    [Authorize(Policy = "Cost.Create")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCostEntryDto dto, CancellationToken cancellationToken) => ToActionResult(await costService.CreateAsync(dto, UserId(), cancellationToken), nameof(GetById));

    [Authorize(Policy = "Cost.Edit")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCostEntryDto dto, CancellationToken cancellationToken) => ToActionResult(await costService.UpdateAsync(id, dto, UserId(), cancellationToken));

    [Authorize(Policy = "Cost.Delete")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, [FromBody] DeleteFinancialRecordDto? dto, CancellationToken cancellationToken) => ToActionResult(await costService.DeleteAsync(id, dto?.RowVersion ?? Array.Empty<byte>(), UserId(), cancellationToken));

    private string UserId() => User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
    private IActionResult ToActionResult<T>(NEXUS.SharedKernel.Result<T> result, string? createdAction = null) => result.IsFailure ? ErrorResult(result.Error) : createdAction is null ? Ok(result.Value) : CreatedAtAction(createdAction, new { id = result.Value is CostEntryDto entry ? entry.Id : Guid.Empty }, result.Value);
    private IActionResult ToActionResult(NEXUS.SharedKernel.Result result) => result.IsFailure ? ErrorResult(result.Error) : NoContent();
    private IActionResult ErrorResult(NEXUS.SharedKernel.Error? error) => error?.StatusCode switch { 401 => Unauthorized(new { error = error.Message }), 403 => Forbid(), 404 => NotFound(new { error = error.Message }), 409 => Conflict(new { error = error.Message }), _ => BadRequest(new { error = error?.Message ?? "Request failed" }) };
}
