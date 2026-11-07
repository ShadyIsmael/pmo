using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.CostManagement.Application.DTOs;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Management;

namespace NEXUS.Api.Finance;

[ApiController]
[Route("api/projects/{projectId:guid}/annual-budget-closings")]
[Authorize]
public sealed class ProjectAnnualBudgetClosingController(
    ProjectPaymentRequestWorkflow workflow,
    IManagementContextAccessor management) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "ProjectAnnualBudgetClosing.View")]
    public async Task<IActionResult> GetPage(Guid projectId, CancellationToken cancellationToken)
    {
        if (!TryGetScope(projectId, out var tenantId, out var actorId)) return ScopeFailure();
        return ToActionResult(await workflow.GetClosingsAsync(tenantId, projectId, actorId, cancellationToken));
    }

    [HttpGet("approvers")]
    [Authorize(Policy = "ProjectAnnualBudget.Close")]
    public async Task<IActionResult> GetApprovers(Guid projectId, CancellationToken cancellationToken)
    {
        if (!TryGetScope(projectId, out var tenantId, out var actorId)) return ScopeFailure();
        return ToActionResult(await workflow.GetEligibleClosingApproversAsync(tenantId, projectId, actorId, cancellationToken));
    }

    [HttpPost]
    [Authorize(Policy = "ProjectAnnualBudget.Close")]
    public async Task<IActionResult> Prepare(Guid projectId, [FromBody] PrepareProjectAnnualBudgetClosingDto dto,
        CancellationToken cancellationToken)
    {
        if (!TryGetScope(projectId, out var tenantId, out var actorId)) return ScopeFailure();
        if (management.Current?.EffectiveReadOnly == true) return Forbid();
        var result = await workflow.PrepareClosingAsync(tenantId, projectId, actorId, ActorName(), dto, cancellationToken);
        if (result.IsFailure) return ErrorResult(result.Error);
        return CreatedAtAction(nameof(GetPage), new { projectId }, result.Value);
    }

    [HttpPost("{closingId:guid}/submit")]
    [Authorize(Policy = "ProjectAnnualBudget.Close")]
    public async Task<IActionResult> Submit(Guid projectId, Guid closingId,
        [FromBody] SubmitProjectAnnualBudgetClosingDto dto, CancellationToken cancellationToken)
    {
        if (!TryGetScope(projectId, out var tenantId, out var actorId)) return ScopeFailure();
        if (management.Current?.EffectiveReadOnly == true) return Forbid();
        return ToActionResult(await workflow.SubmitClosingAsync(tenantId, projectId, closingId, actorId, ActorName(), dto, cancellationToken));
    }

    [HttpPost("{closingId:guid}/decisions")]
    [Authorize(Policy = "ProjectAnnualBudget.CloseApprove")]
    public async Task<IActionResult> Decide(Guid projectId, Guid closingId,
        [FromBody] DecideProjectAnnualBudgetClosingDto dto, CancellationToken cancellationToken)
    {
        if (!TryGetScope(projectId, out var tenantId, out var actorId)) return ScopeFailure();
        if (management.Current?.EffectiveReadOnly == true) return Forbid();
        return ToActionResult(await workflow.DecideClosingAsync(tenantId, projectId, closingId, actorId, ActorName(), dto, cancellationToken));
    }

    [HttpPost("{closingId:guid}/cancel")]
    [Authorize(Policy = "ProjectAnnualBudget.Close")]
    public async Task<IActionResult> Cancel(Guid projectId, Guid closingId, [FromBody] CancelProjectAnnualBudgetClosingDto dto,
        CancellationToken cancellationToken)
    {
        if (!TryGetScope(projectId, out var tenantId, out var actorId)) return ScopeFailure();
        if (management.Current?.EffectiveReadOnly == true) return Forbid();
        return ToActionResult(await workflow.CancelClosingAsync(tenantId, projectId, closingId, actorId, ActorName(), dto.RowVersion, cancellationToken));
    }

    private bool TryGetScope(Guid projectId, out Guid tenantId, out Guid actorId)
    {
        tenantId = Guid.Empty;
        actorId = Guid.Empty;
        var current = management.Current;
        return current is not null
            && Guid.TryParse(User.FindFirst("tenantId")?.Value, out tenantId) && tenantId != Guid.Empty
            && Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out actorId) && actorId != Guid.Empty
            && current.TenantId == tenantId && current.UserId == actorId
            && current.Scope.ProjectIds.Contains(projectId);
    }

    private IActionResult ScopeFailure() => Guid.TryParse(User.FindFirst("tenantId")?.Value, out _)
        ? NotFound(new { error = "Project not found." }) : Unauthorized();
    private string? ActorName() => User.FindFirst(ClaimTypes.Name)?.Value ?? User.FindFirst("name")?.Value;
    private IActionResult ToActionResult<T>(Result<T> result) => result.IsFailure ? ErrorResult(result.Error) : Ok(result.Value);
    private IActionResult ErrorResult(Error? error) => error?.StatusCode switch
    {
        401 => Unauthorized(new { error = error.Message }),
        403 => Forbid(),
        404 => NotFound(new { error = error.Message }),
        409 => Conflict(new { error = error.Message }),
        _ => BadRequest(new { error = error?.Message ?? "Request failed" })
    };
}
