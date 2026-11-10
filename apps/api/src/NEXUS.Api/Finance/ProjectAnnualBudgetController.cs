using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.CostManagement.Application.DTOs;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Management;

namespace NEXUS.Api.Finance;

[ApiController]
[Route("api/projects/{projectId:guid}/annual-budgets")]
[Authorize]
public sealed class ProjectAnnualBudgetController(
    ProjectAnnualBudgetWorkflow workflow,
    IManagementContextAccessor management) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "ProjectAnnualBudget.View")]
    public async Task<IActionResult> GetPage(Guid projectId, CancellationToken cancellationToken)
    {
        if (!TryGetScope(projectId, out var tenantId, out var actorId)) return ScopeFailure();
        return ToActionResult(await workflow.GetPageAsync(tenantId, projectId, actorId, cancellationToken));
    }

    [HttpGet("{itemId:guid}/review/{requestId:guid}")]
    [Authorize(Policy = "ProjectAnnualBudget.Approve")]
    public async Task<IActionResult> GetAssignedReview(
        Guid projectId, Guid itemId, Guid requestId, CancellationToken cancellationToken)
    {
        if (!TryGetIdentityScope(out var tenantId, out var actorId)) return ScopeFailure();
        return ToActionResult(await workflow.GetAssignedReviewAsync(
            tenantId, projectId, itemId, requestId, actorId, cancellationToken));
    }

    [HttpGet("approval-configuration")]
    [Authorize(Policy = "ProjectAnnualBudget.View")]
    [Authorize(Policy = "ProjectSettings.View")]
    public async Task<IActionResult> GetApprovalConfiguration(Guid projectId, CancellationToken cancellationToken)
    {
        if (!TryGetScope(projectId, out var tenantId, out var actorId)) return ScopeFailure();
        return ToActionResult(await workflow.GetApprovalConfigurationAsync(tenantId, projectId, actorId, cancellationToken));
    }

    [HttpPut("approval-configuration")]
    [Authorize(Policy = "ProjectAnnualBudget.ManageApprovalConfiguration")]
    [Authorize(Policy = "ProjectSettings.View")]
    [Authorize(Policy = "ProjectSettings.Edit")]
    public async Task<IActionResult> SaveApprovalConfiguration(
        Guid projectId,
        [FromBody] SaveProjectAnnualBudgetApprovalConfigurationDto dto,
        CancellationToken cancellationToken)
    {
        if (!TryGetScope(projectId, out var tenantId, out var actorId)) return ScopeFailure();
        if (management.Current?.EffectiveReadOnly == true) return Forbid();
        return ToActionResult(await workflow.SaveApprovalConfigurationAsync(
            tenantId, projectId, actorId, ActorName(), dto, cancellationToken));
    }

    [HttpPost]
    [Authorize(Policy = "ProjectAnnualBudget.Create")]
    public async Task<IActionResult> Create(
        Guid projectId,
        [FromBody] CreateProjectAnnualBudgetDto dto,
        CancellationToken cancellationToken)
    {
        if (!TryGetScope(projectId, out var tenantId, out var actorId)) return ScopeFailure();
        if (management.Current?.EffectiveReadOnly == true) return Forbid();
        var result = await workflow.CreateAsync(tenantId, projectId, actorId, ActorName(), dto, cancellationToken);
        if (result.IsFailure) return ErrorResult(result.Error);
        return CreatedAtAction(nameof(GetPage), new { projectId }, result.Value);
    }

    [HttpPut("{itemId:guid}")]
    [Authorize(Policy = "ProjectAnnualBudget.EditDraft")]
    public async Task<IActionResult> UpdateDraft(
        Guid projectId,
        Guid itemId,
        [FromBody] UpdateProjectAnnualBudgetDto dto,
        CancellationToken cancellationToken)
    {
        if (!TryGetScope(projectId, out var tenantId, out var actorId)) return ScopeFailure();
        if (management.Current?.EffectiveReadOnly == true) return Forbid();
        return ToActionResult(await workflow.UpdateDraftAsync(
            tenantId, projectId, itemId, actorId, ActorName(), dto, cancellationToken));
    }

    [HttpPost("{itemId:guid}/submit")]
    [Authorize(Policy = "ProjectAnnualBudget.SubmitForReview")]
    public async Task<IActionResult> SubmitForReview(
        Guid projectId,
        Guid itemId,
        [FromBody] SubmitProjectAnnualBudgetReviewDto dto,
        CancellationToken cancellationToken)
    {
        if (!TryGetScope(projectId, out var tenantId, out var actorId)) return ScopeFailure();
        if (management.Current?.EffectiveReadOnly == true) return Forbid();
        return ToActionResult(await workflow.SubmitForReviewAsync(
            tenantId, projectId, itemId, actorId, ActorName(), dto, cancellationToken));
    }

    [HttpPost("{itemId:guid}/revisions")]
    [Authorize(Policy = "ProjectAnnualBudget.ReviseApproved")]
    public async Task<IActionResult> ReviseApproved(
        Guid projectId,
        Guid itemId,
        [FromBody] ReviseProjectAnnualBudgetDto dto,
        CancellationToken cancellationToken)
    {
        if (!TryGetScope(projectId, out var tenantId, out var actorId)) return ScopeFailure();
        if (management.Current?.EffectiveReadOnly == true) return Forbid();
        return ToActionResult(await workflow.ReviseApprovedAsync(
            tenantId, projectId, itemId, actorId, ActorName(), dto, cancellationToken));
    }

    [HttpPost("{itemId:guid}/review/{requestId:guid}")]
    [Authorize(Policy = "ProjectAnnualBudget.Approve")]
    public async Task<IActionResult> DecideReview(
        Guid projectId,
        Guid itemId,
        Guid requestId,
        [FromBody] DecideProjectAnnualBudgetReviewDto dto,
        CancellationToken cancellationToken)
    {
        if (!TryGetIdentityScope(out var tenantId, out var actorId)) return ScopeFailure();
        return ToActionResult(await workflow.DecideReviewAsync(
            tenantId, projectId, itemId, requestId, actorId, ActorName(), dto, cancellationToken));
    }

    [HttpPost("{itemId:guid}/approve")]
    [Authorize(Policy = "ProjectAnnualBudget.Approve")]
    public async Task<IActionResult> Approve(
        Guid projectId,
        Guid itemId,
        [FromBody] ApproveProjectAnnualBudgetDto dto,
        CancellationToken cancellationToken)
    {
        if (!TryGetIdentityScope(out var tenantId, out var actorId)) return ScopeFailure();
        return ToActionResult(await workflow.ApproveAsync(
            tenantId, projectId, itemId, actorId, ActorName(), dto, cancellationToken));
    }

    [HttpDelete("{itemId:guid}")]
    [Authorize(Policy = "ProjectAnnualBudget.Delete")]
    public async Task<IActionResult> DeleteDraft(
        Guid projectId,
        Guid itemId,
        [FromBody] DeleteProjectAnnualBudgetDto dto,
        CancellationToken cancellationToken)
    {
        if (!TryGetScope(projectId, out var tenantId, out var actorId)) return ScopeFailure();
        if (management.Current?.EffectiveReadOnly == true) return Forbid();
        return ToActionResult(await workflow.DeleteDraftAsync(
            tenantId, projectId, itemId, actorId, ActorName(), dto, cancellationToken));
    }

    [HttpPost("{itemId:guid}/withdraw")]
    [Authorize(Policy = "ProjectAnnualBudget.Withdraw")]
    public async Task<IActionResult> WithdrawSubmission(
        Guid projectId,
        Guid itemId,
        [FromBody] WithdrawProjectAnnualBudgetSubmissionDto dto,
        CancellationToken cancellationToken)
    {
        if (!TryGetScope(projectId, out var tenantId, out var actorId)) return ScopeFailure();
        if (management.Current?.EffectiveReadOnly == true) return Forbid();
        return ToActionResult(await workflow.WithdrawSubmissionAsync(
            tenantId, projectId, itemId, actorId, ActorName(), dto, cancellationToken));
    }

    private bool TryGetScope(Guid projectId, out Guid tenantId, out Guid actorId)
    {
        return TryGetIdentityScope(out tenantId, out actorId)
            && management.Current!.Scope.ProjectIds.Contains(projectId);
    }

    private bool TryGetIdentityScope(out Guid tenantId, out Guid actorId)
    {
        tenantId = Guid.Empty;
        actorId = Guid.Empty;
        var current = management.Current;
        return current is not null
            && Guid.TryParse(User.FindFirst("tenantId")?.Value, out tenantId)
            && tenantId != Guid.Empty
            && Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out actorId)
            && actorId != Guid.Empty
            && current.TenantId == tenantId
            && current.UserId == actorId;
    }

    private IActionResult ScopeFailure() => Guid.TryParse(User.FindFirst("tenantId")?.Value, out _)
        ? NotFound(new { error = "Project not found." })
        : Unauthorized();

    private string? ActorName() => User.FindFirst(ClaimTypes.Name)?.Value ?? User.FindFirst("name")?.Value;

    private IActionResult ToActionResult<T>(Result<T> result) => result.IsFailure
        ? ErrorResult(result.Error)
        : Ok(result.Value);

    private IActionResult ErrorResult(Error? error) => error?.StatusCode switch
    {
        401 => Unauthorized(new { error = error.Message }),
        403 => Forbid(),
        404 => NotFound(new { error = error.Message }),
        409 => Conflict(new { error = error.Message }),
        _ => BadRequest(new { error = error?.Message ?? "Request failed" })
    };
}
