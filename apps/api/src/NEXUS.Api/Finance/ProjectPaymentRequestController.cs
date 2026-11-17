using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NEXUS.Modules.CostManagement.Application.DTOs;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Management;

namespace NEXUS.Api.Finance;

[ApiController]
[Route("api/projects/{projectId:guid}/payment-requests")]
[Authorize]
public sealed class ProjectPaymentRequestController(
    ProjectPaymentRequestWorkflow workflow,
    IManagementContextAccessor management) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "ProjectPaymentRequest.View")]
    public async Task<IActionResult> GetPage(
        Guid projectId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetScope(projectId, out var tenantId, out var actorId)) return ScopeFailure();
        return ToActionResult(await workflow.GetPageAsync(tenantId, projectId, actorId, page, pageSize, status, cancellationToken));
    }

    [HttpGet("approvers")]
    [Authorize(Policy = "ProjectPaymentRequest.Submit")]
    public async Task<IActionResult> GetEligibleApprovers(Guid projectId, CancellationToken cancellationToken)
    {
        if (!TryGetScope(projectId, out var tenantId, out _)) return ScopeFailure();
        return ToActionResult(await workflow.GetEligibleApproversAsync(tenantId, projectId, cancellationToken));
    }

    [HttpGet("{requestId:guid}/approver-recovery-options/{currentApproverId:guid}")]
    [Authorize(Policy = "ProjectPaymentRequest.Approve")]
    [Authorize(Policy = "Governance.AssignmentRecovery.Manage")]
    public async Task<IActionResult> GetPaymentRequestRecoveryApprovers(
        Guid projectId, Guid requestId, Guid currentApproverId, CancellationToken cancellationToken)
    {
        if (!TryGetScope(projectId, out var tenantId, out var actorId)) return ScopeFailure();
        if (management.Current?.EffectiveReadOnly == true) return Forbid();
        return ToActionResult(await workflow.GetEligibleRecoveryApproversAsync(
            tenantId, projectId, requestId, currentApproverId, actorId, cancellationToken));
    }

    [HttpGet("reversal-approvers")]
    [Authorize(Policy = "ProjectPaymentReversal.Create")]
    public async Task<IActionResult> GetEligibleReversalApprovers(Guid projectId, CancellationToken cancellationToken)
    {
        if (!TryGetScope(projectId, out var tenantId, out var actorId)) return ScopeFailure();
        return ToActionResult(await workflow.GetEligibleReversalApproversAsync(
            tenantId, projectId, actorId, cancellationToken));
    }

    [HttpGet("milestones")]
    [Authorize(Policy = "ProjectPaymentRequest.Create")]
    public async Task<IActionResult> GetMilestones(Guid projectId, CancellationToken cancellationToken)
    {
        if (!TryGetScope(projectId, out var tenantId, out _)) return ScopeFailure();
        return ToActionResult(await workflow.GetActiveMilestonesAsync(tenantId, projectId, cancellationToken));
    }

    [HttpGet("{requestId:guid}")]
    [Authorize(Policy = "ProjectPaymentRequest.ViewOrAssignedReviewer")]
    public async Task<IActionResult> GetById(Guid projectId, Guid requestId, CancellationToken cancellationToken)
    {
        if (!TryGetIdentityScope(out var tenantId, out var actorId)) return ScopeFailure();
        return ToActionResult(await workflow.GetByIdAsync(tenantId, projectId, requestId, actorId, cancellationToken));
    }

    [HttpGet("{requestId:guid}/settlements")]
    [Authorize(Policy = "ProjectPaymentRequest.View")]
    public async Task<IActionResult> GetSettlements(
        Guid projectId, Guid requestId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetScope(projectId, out var tenantId, out var actorId)) return ScopeFailure();
        return ToActionResult(await workflow.GetSettlementsAsync(
            tenantId, projectId, requestId, actorId, page, pageSize, cancellationToken));
    }

    [HttpGet("{requestId:guid}/reversals")]
    [Authorize(Policy = "ProjectPaymentRequest.View")]
    public async Task<IActionResult> GetReversals(Guid projectId, Guid requestId, CancellationToken cancellationToken)
    {
        if (!TryGetScope(projectId, out var tenantId, out var actorId)) return ScopeFailure();
        return ToActionResult(await workflow.GetReversalsAsync(tenantId, projectId, requestId, actorId, cancellationToken));
    }

    [HttpPost("{requestId:guid}/reversals")]
    [Authorize(Policy = "ProjectPaymentReversal.Create")]
    public async Task<IActionResult> CreateReversal(
        Guid projectId, Guid requestId, [FromBody] CreatePaymentReversalDto dto, CancellationToken cancellationToken)
    {
        if (!TryGetScope(projectId, out var tenantId, out var actorId)) return ScopeFailure();
        if (management.Current?.EffectiveReadOnly == true) return Forbid();
        return ToActionResult(await workflow.CreateReversalAsync(
            tenantId, projectId, requestId, actorId, ActorName(), dto, cancellationToken));
    }

    [HttpPut("{requestId:guid}/reversals/{reversalId:guid}")]
    [Authorize(Policy = "ProjectPaymentReversal.Create")]
    public async Task<IActionResult> UpdateReversal(
        Guid projectId, Guid requestId, Guid reversalId, [FromBody] UpdatePaymentReversalDto dto,
        CancellationToken cancellationToken)
    {
        if (!TryGetScope(projectId, out var tenantId, out var actorId)) return ScopeFailure();
        if (management.Current?.EffectiveReadOnly == true) return Forbid();
        return ToActionResult(await workflow.UpdateReversalAsync(
            tenantId, projectId, requestId, reversalId, actorId, ActorName(), dto, cancellationToken));
    }

    [HttpPost("{requestId:guid}/reversals/{reversalId:guid}/submit")]
    [Authorize(Policy = "ProjectPaymentReversal.Create")]
    public async Task<IActionResult> SubmitReversal(
        Guid projectId, Guid requestId, Guid reversalId, [FromBody] SubmitPaymentReversalDto dto,
        CancellationToken cancellationToken)
    {
        if (!TryGetScope(projectId, out var tenantId, out var actorId)) return ScopeFailure();
        if (management.Current?.EffectiveReadOnly == true) return Forbid();
        return ToActionResult(await workflow.SubmitReversalAsync(
            tenantId, projectId, requestId, reversalId, actorId, ActorName(), dto, cancellationToken));
    }

    [HttpGet("{requestId:guid}/reversals/{reversalId:guid}/review")]
    [Authorize(Policy = "ProjectPaymentReversal.Approve")]
    public async Task<IActionResult> GetAssignedReversalReview(
        Guid projectId, Guid requestId, Guid reversalId, CancellationToken cancellationToken)
    {
        if (!TryGetIdentityScope(out var tenantId, out var actorId)) return ScopeFailure();
        return ToActionResult(await workflow.GetAssignedReversalReviewAsync(
            tenantId, projectId, requestId, reversalId, actorId, cancellationToken));
    }

    [HttpGet("{requestId:guid}/reversals/{reversalId:guid}/approver-recovery-options/{currentApproverId:guid}")]
    [Authorize(Policy = "ProjectPaymentReversal.Approve")]
    [Authorize(Policy = "Governance.AssignmentRecovery.Manage")]
    public async Task<IActionResult> GetReversalRecoveryApprovers(
        Guid projectId, Guid requestId, Guid reversalId, Guid currentApproverId,
        CancellationToken cancellationToken)
    {
        if (!TryGetScope(projectId, out var tenantId, out var actorId)) return ScopeFailure();
        if (management.Current?.EffectiveReadOnly == true) return Forbid();
        return ToActionResult(await workflow.GetEligibleReversalRecoveryApproversAsync(
            tenantId, projectId, requestId, reversalId, currentApproverId, actorId, cancellationToken));
    }

    [HttpPost("{requestId:guid}/reversals/{reversalId:guid}/decisions")]
    [Authorize(Policy = "ProjectPaymentReversal.Approve")]
    public async Task<IActionResult> DecideReversal(
        Guid projectId, Guid requestId, Guid reversalId, [FromBody] DecidePaymentRequestApprovalDto dto,
        CancellationToken cancellationToken)
    {
        if (!TryGetIdentityScope(out var tenantId, out var actorId)) return ScopeFailure();
        if (management.Current?.EffectiveReadOnly == true) return Forbid();
        return ToActionResult(await workflow.DecideReversalAsync(
            tenantId, projectId, requestId, reversalId, actorId, ActorName(), dto, cancellationToken));
    }

    [HttpPost("{requestId:guid}/reversals/{reversalId:guid}/approver-reassignments")]
    [Authorize(Policy = "ProjectPaymentReversal.Approve")]
    [Authorize(Policy = "Governance.AssignmentRecovery.Manage")]
    public async Task<IActionResult> ReassignReversalApprover(
        Guid projectId, Guid requestId, Guid reversalId,
        [FromBody] ReassignPaymentRequestApproverDto dto,
        CancellationToken cancellationToken)
    {
        if (!TryGetScope(projectId, out var tenantId, out var actorId)) return ScopeFailure();
        if (management.Current?.EffectiveReadOnly == true) return Forbid();
        return ToActionResult(await workflow.ReassignReversalApproverAsync(
            tenantId, projectId, requestId, reversalId, actorId, ActorName(), dto, cancellationToken));
    }

    [HttpPost("{requestId:guid}/reversals/{reversalId:guid}/cancel")]
    [Authorize(Policy = "ProjectPaymentReversal.Create")]
    public async Task<IActionResult> CancelReversal(
        Guid projectId, Guid requestId, Guid reversalId, [FromBody] CancelPaymentRequestDto dto,
        CancellationToken cancellationToken)
    {
        if (!TryGetScope(projectId, out var tenantId, out var actorId)) return ScopeFailure();
        if (management.Current?.EffectiveReadOnly == true) return Forbid();
        return ToActionResult(await workflow.CancelReversalAsync(
            tenantId, projectId, requestId, reversalId, actorId, ActorName(), dto, cancellationToken));
    }

    [HttpPost("{requestId:guid}/settlements")]
    [Authorize(Policy = "PaymentSettlement.Record")]
    public async Task<IActionResult> RecordSettlement(
        Guid projectId, Guid requestId, [FromBody] RecordProjectPaymentSettlementDto dto,
        CancellationToken cancellationToken)
    {
        if (!TryGetScope(projectId, out var tenantId, out var actorId)) return ScopeFailure();
        if (management.Current?.EffectiveReadOnly == true) return Forbid();
        return ToActionResult(await workflow.RecordSettlementAsync(
            tenantId, projectId, requestId, actorId, ActorName(), dto, cancellationToken));
    }

    [HttpPost]
    [Authorize(Policy = "ProjectPaymentRequest.Create")]
    public async Task<IActionResult> Create(Guid projectId, [FromBody] CreatePaymentRequestDto dto, CancellationToken cancellationToken)
    {
        if (!TryGetScope(projectId, out var tenantId, out var actorId)) return ScopeFailure();
        if (management.Current?.EffectiveReadOnly == true) return Forbid();
        var result = await workflow.CreateAsync(tenantId, projectId, actorId, ActorName(), dto, cancellationToken);
        if (result.IsFailure) return ErrorResult(result.Error);
        return CreatedAtAction(nameof(GetById), new { projectId, requestId = result.Value!.Id }, result.Value);
    }

    [HttpPut("{requestId:guid}")]
    [Authorize(Policy = "ProjectPaymentRequest.EditDraft")]
    public async Task<IActionResult> Update(Guid projectId, Guid requestId, [FromBody] UpdatePaymentRequestDto dto, CancellationToken cancellationToken)
    {
        if (!TryGetScope(projectId, out var tenantId, out var actorId)) return ScopeFailure();
        if (management.Current?.EffectiveReadOnly == true) return Forbid();
        return ToActionResult(await workflow.UpdateAsync(tenantId, projectId, requestId, actorId, ActorName(), dto, cancellationToken));
    }

    [HttpPost("{requestId:guid}/submit")]
    [Authorize(Policy = "ProjectPaymentRequest.Submit")]
    public async Task<IActionResult> Submit(Guid projectId, Guid requestId, [FromBody] SubmitPaymentRequestDto dto, CancellationToken cancellationToken)
    {
        if (!TryGetScope(projectId, out var tenantId, out var actorId)) return ScopeFailure();
        if (management.Current?.EffectiveReadOnly == true) return Forbid();
        return ToActionResult(await workflow.SubmitAsync(tenantId, projectId, requestId, actorId, ActorName(), dto, cancellationToken));
    }

    [HttpPost("{requestId:guid}/decisions")]
    [Authorize(Policy = "ProjectPaymentRequest.Approve")]
    public async Task<IActionResult> Decide(Guid projectId, Guid requestId, [FromBody] DecidePaymentRequestApprovalDto dto, CancellationToken cancellationToken)
    {
        if (!TryGetIdentityScope(out var tenantId, out var actorId)) return ScopeFailure();
        if (management.Current?.EffectiveReadOnly == true) return Forbid();
        return ToActionResult(await workflow.DecideAsync(tenantId, projectId, requestId, actorId, ActorName(), dto, cancellationToken));
    }

    [HttpPost("{requestId:guid}/approver-reassignments")]
    [Authorize(Policy = "ProjectPaymentRequest.Approve")]
    [Authorize(Policy = "Governance.AssignmentRecovery.Manage")]
    public async Task<IActionResult> ReassignApprover(
        Guid projectId,
        Guid requestId,
        [FromBody] ReassignPaymentRequestApproverDto dto,
        CancellationToken cancellationToken)
    {
        if (!TryGetScope(projectId, out var tenantId, out var actorId)) return ScopeFailure();
        if (management.Current?.EffectiveReadOnly == true) return Forbid();
        return ToActionResult(await workflow.ReassignApproverAsync(
            tenantId, projectId, requestId, actorId, ActorName(), dto, cancellationToken));
    }

    [HttpPost("{requestId:guid}/cancel")]
    [Authorize(Policy = "ProjectPaymentRequest.Cancel")]
    public async Task<IActionResult> Cancel(Guid projectId, Guid requestId, [FromBody] CancelPaymentRequestDto dto, CancellationToken cancellationToken)
    {
        if (!TryGetScope(projectId, out var tenantId, out var actorId)) return ScopeFailure();
        if (management.Current?.EffectiveReadOnly == true) return Forbid();
        return ToActionResult(await workflow.CancelAsync(tenantId, projectId, requestId, actorId, ActorName(), dto, cancellationToken));
    }

    private bool TryGetScope(Guid projectId, out Guid tenantId, out Guid actorId)
    {
        tenantId = Guid.Empty;
        actorId = Guid.Empty;
        return Guid.TryParse(User.FindFirst("tenantId")?.Value, out tenantId)
            && tenantId != Guid.Empty
            && Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out actorId)
            && actorId != Guid.Empty
            && management.Current?.TenantId == tenantId
            && management.Current.Scope.ProjectIds.Contains(projectId);
    }

    private bool TryGetIdentityScope(out Guid tenantId, out Guid actorId)
    {
        tenantId = Guid.Empty;
        actorId = Guid.Empty;
        return Guid.TryParse(User.FindFirst("tenantId")?.Value, out tenantId)
            && tenantId != Guid.Empty
            && Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out actorId)
            && actorId != Guid.Empty
            && management.Current?.TenantId == tenantId
            && management.Current.UserId == actorId;
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
