using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.CostManagement.Application;
using NEXUS.Modules.CostManagement.Application.DTOs;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.CostManagement.Api;

[ApiController]
[Route("api/commitments")]
[Authorize]
public sealed class CommitmentController(ICommitmentService commitmentService) : ControllerBase
{
    [Authorize(Policy = "Commitment.View")]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await commitmentService.GetByIdAsync(id, cancellationToken);
        return result.IsFailure ? NotFound(new { error = result.Error?.Message }) : Ok(result.Value);
    }

    [Authorize(Policy = "Commitment.View")]
    [HttpGet]
    public async Task<IActionResult> GetPaged(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] Guid? projectId = null, CancellationToken cancellationToken = default) =>
        Ok(await commitmentService.GetPagedAsync(page, pageSize, projectId, cancellationToken));

    [Authorize(Policy = "Commitment.Create")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCommitmentDto dto, CancellationToken cancellationToken) =>
        ToActionResult(await commitmentService.CreateAsync(dto, UserId(), cancellationToken), nameof(GetById));

    [Authorize(Policy = "Commitment.Edit")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCommitmentDto dto, CancellationToken cancellationToken) =>
        ToActionResult(await commitmentService.UpdateAsync(id, dto, UserId(), cancellationToken));

    [Authorize(Policy = "Commitment.Close")]
    [HttpPost("{id:guid}/close")]
    public async Task<IActionResult> Close(Guid id, [FromQuery] byte[]? rowVersion, CancellationToken cancellationToken) =>
        ToActionResult(await commitmentService.CloseAsync(id, rowVersion ?? [], UserId(), cancellationToken));

    [Authorize(Policy = "Commitment.Cancel")]
    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, [FromQuery] byte[]? rowVersion, CancellationToken cancellationToken) =>
        ToActionResult(await commitmentService.CancelAsync(id, rowVersion ?? [], UserId(), cancellationToken));

    [Authorize(Policy = "Commitment.Delete")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, [FromQuery] byte[]? rowVersion, CancellationToken cancellationToken) =>
        ToActionResult(await commitmentService.DeleteAsync(id, rowVersion ?? [], UserId(), cancellationToken));

    private string UserId() =>
        User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "System";

    private IActionResult ToActionResult<T>(Result<T> result, string? createdAction = null) =>
        result.IsFailure ? ErrorResult(result.Error) :
        createdAction is null ? Ok(result.Value) :
        CreatedAtAction(createdAction, new { id = result.Value is PurchaseCommitmentDto c ? c.Id : Guid.Empty }, result.Value);

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
