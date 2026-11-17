using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using NEXUS.Modules.CostManagement.Application;
using NEXUS.Modules.CostManagement.Application.DTOs;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Management;

namespace NEXUS.Modules.CostManagement.Api;

[ApiController]
[Route("api/commitments")]
[Authorize]
public sealed class CommitmentController(
    ICommitmentService commitmentService,
    IManagementContextAccessor? managementContext = null) : ControllerBase
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
    public async Task<IActionResult> Create([FromBody] CreateCommitmentDto dto, CancellationToken cancellationToken)
    {
        var actorError = RequireActor(out var userId);
        if (actorError is not null) return actorError;
        return ToActionResult(await commitmentService.CreateAsync(dto, userId, cancellationToken), nameof(GetById));
    }

    [Authorize(Policy = "Commitment.Edit")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCommitmentDto dto, CancellationToken cancellationToken)
    {
        var actorError = RequireActor(out var userId);
        if (actorError is not null) return actorError;
        return ToActionResult(await commitmentService.UpdateAsync(id, dto, userId, cancellationToken));
    }

    [Authorize(Policy = "Commitment.Close")]
    [HttpPost("{id:guid}/close")]
    public async Task<IActionResult> Close(Guid id, [FromQuery] byte[]? rowVersion, CancellationToken cancellationToken)
    {
        var actorError = RequireActor(out var userId);
        if (actorError is not null) return actorError;
        return ToActionResult(await commitmentService.CloseAsync(id, rowVersion ?? [], userId, cancellationToken));
    }

    [Authorize(Policy = "Commitment.Cancel")]
    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, [FromQuery] byte[]? rowVersion, CancellationToken cancellationToken)
    {
        var actorError = RequireActor(out var userId);
        if (actorError is not null) return actorError;
        return ToActionResult(await commitmentService.CancelAsync(id, rowVersion ?? [], userId, cancellationToken));
    }

    [Authorize(Policy = "Commitment.Delete")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, [FromQuery] byte[]? rowVersion, CancellationToken cancellationToken)
    {
        var actorError = RequireActor(out var userId);
        if (actorError is not null) return actorError;
        return ToActionResult(await commitmentService.DeleteAsync(id, rowVersion ?? [], userId, cancellationToken));
    }

    private IActionResult? RequireActor(out string actorId)
    {
        actorId = string.Empty;
        var actorClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        var actorValue = actorClaim?.Value ?? (actorClaim is null ? User.FindFirst("sub")?.Value : null);
        if (User.Identity?.IsAuthenticated != true
            || !Guid.TryParse(actorValue, out var userId)
            || userId == Guid.Empty
            || !Guid.TryParse(User.FindFirst("tenantId")?.Value, out var tenantId)
            || tenantId == Guid.Empty)
            return Unauthorized();

        var context = managementContext?.Current;
        if (context is null || context.UserId != userId || context.TenantId != tenantId)
            return Forbid();
        if (context.EffectiveReadOnly || string.Equals(User.FindFirst("readOnly")?.Value, "true", StringComparison.OrdinalIgnoreCase))
            return Forbid();

        actorId = userId.ToString("D");
        return null;
    }

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
