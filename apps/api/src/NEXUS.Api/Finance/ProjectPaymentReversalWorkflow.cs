using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Approvals.Domain;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.CostManagement.Application;
using NEXUS.Modules.CostManagement.Application.DTOs;
using NEXUS.Modules.CostManagement.Domain;
using NEXUS.Modules.Identity.Infrastructure;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.Persistence;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Management;

namespace NEXUS.Api.Finance;

/// <summary>
/// Approved reversals release only unpaid Payment Request commitment. This uses
/// the same ApprovalRequest, ApprovalAction, assignment, project lock and audit
/// stores as the Payment Request workflow.
/// </summary>
public sealed partial class ProjectPaymentRequestWorkflow
{
    private const string ReversalRequestType = "ProjectPaymentRequestReversal";
    private const string ReversalCreatePermission = "ProjectPaymentReversal.Create";
    private const string ReversalApprovePermission = "ProjectPaymentReversal.Approve";
    private const string ReversalEntityType = "ProjectPaymentReversal";

    public async Task<Result<ProjectPaymentReversalPageDto>> GetReversalsAsync(
        Guid tenantId, Guid projectId, Guid requestId, Guid actorId, CancellationToken cancellationToken)
    {
        var access = await ValidateServiceAccessAsync(tenantId, projectId, actorId, requireWrite: false, cancellationToken);
        if (access.Error is not null) return Result<ProjectPaymentReversalPageDto>.Failure(access.Error);
        var original = await FindRequestAsync(tenantId, projectId, requestId, cancellationToken);
        if (original is null || original.Value.Detail.RequestKind != ProjectPaymentRequestKinds.PaymentRequest)
            return Result<ProjectPaymentReversalPageDto>.Failure(Error.NotFound("Payment Request not found."));
        if (original.Value.Request.Status != PaymentRequestStatuses.Approved || original.Value.Request.Amount is not > 0m)
            return Result<ProjectPaymentReversalPageDto>.Failure(Error.Conflict("Only an Approved Payment Request can have reversal history."));

        var balanceResult = await GetReversalBalanceAsync(tenantId, projectId, original.Value.Request, original.Value.Detail, cancellationToken);
        if (balanceResult.Error is not null || balanceResult.Balance is null)
            return Result<ProjectPaymentReversalPageDto>.Failure(balanceResult.Error
                ?? Error.Conflict("The Payment Request reversal balance could not be calculated."));
        var balance = balanceResult.Balance;
        var reversalRows = await FindReversalsAsync(tenantId, projectId, requestId, tracked: false, cancellationToken);
        var hasPending = reversalRows.Any(item => item.Request.Status == PaymentRequestStatuses.Pending);
        var effective = await permissions.GetEffectivePermissionsAsync(actorId, tenantId, cancellationToken);
        var canCreatePermission = effective.Contains(ReversalCreatePermission)
            && management.Current?.EffectiveReadOnly != true;
        var project = await FindProjectQuery(tenantId, projectId).AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        var budgetReady = await IsReversalBudgetCurrentAndApprovedAsync(
            tenantId, projectId, project, original.Value.Detail, cancellationToken);
        var canCreate = project is not null && canCreatePermission && budgetReady
            && balance.MaximumReversibleAmount > 0m && !hasPending;
        var assignmentIds = reversalRows.Select(item => item.Request.Id).ToArray();
        List<ApprovalAction> actions = assignmentIds.Length == 0
            ? new List<ApprovalAction>()
            : await db.ApprovalActions.IgnoreQueryFilters().AsNoTracking()
                .Where(item => item.TenantId == tenantId && !item.IsDeleted && assignmentIds.Contains(item.ApprovalRequestId))
                .ToListAsync(cancellationToken);
        var userIds = reversalRows.Select(item => item.Request.RequestedBy ?? Guid.Empty)
            .Concat(await db.PaymentRequestApproverAssignments.IgnoreQueryFilters().AsNoTracking()
                .Where(item => item.TenantId == tenantId && !item.IsDeleted && assignmentIds.Contains(item.ApprovalRequestId))
                .Select(item => item.ApproverUserId).ToListAsync(cancellationToken))
            .Where(id => id != Guid.Empty).Distinct().ToArray();
        var names = await LoadUserNamesAsync(tenantId, userIds, cancellationToken);
        var canRecoverAssignments = effective.Contains(ReversalApprovePermission)
            && effective.Contains(AssignmentRecoveryPermission)
            && management.Current?.EffectiveReadOnly != true;
        var eligible = effective.Contains(ReversalApprovePermission)
            ? await GetEligibleReversalApproverIdsAsync(tenantId, projectId, cancellationToken)
            : new HashSet<Guid>();
        var canApprove = effective.Contains(ReversalApprovePermission)
            && management.Current?.EffectiveReadOnly != true && eligible.Contains(actorId);
        var currentYearReady = budgetReady && !hasPending;
        var items = new List<ProjectPaymentReversalDto>(reversalRows.Count);
        foreach (var row in reversalRows.OrderByDescending(item => item.Request.RequestedAt))
        {
            var assignments = await GetAssignmentsAsync(tenantId, row.Request.Id, tracked: false, cancellationToken);
            var approvers = MapApprovers(row.Request, row.Detail, assignments,
                actions.Where(item => item.ApprovalRequestId == row.Request.Id).ToArray(), names, actorId,
                canRecoverAssignments ? eligible : null, canRecoverAssignments)
                .Select(item => item with
                {
                    CanDecide = canApprove && row.Request.RequestedBy != actorId && item.CanDecide
                })
                .ToArray();
            var isRequester = row.Request.RequestedBy == actorId;
            items.Add(new ProjectPaymentReversalDto(
                row.Request.Id,
                ReversalReference(row.Request),
                requestId,
                Reference(original.Value.Request),
                row.Detail.ProjectAnnualBudgetId,
                row.Detail.AnnualBudgetFiscalYear,
                row.Detail.AnnualBudgetReference,
                original.Value.Request.Amount.Value,
                balance.TotalSettled,
                balance.TotalReleased,
                balance.MaximumReversibleAmount,
                row.Request.Amount ?? 0m,
                row.Request.Currency ?? string.Empty,
                row.Request.Description ?? string.Empty,
                row.Request.Status,
                row.Request.RequestedBy ?? Guid.Empty,
                names.GetValueOrDefault(row.Request.RequestedBy ?? Guid.Empty) ?? string.Empty,
                row.Request.RequestedAt,
                row.Detail.SubmittedAt,
                row.Detail.CompletedAt,
                row.Detail.ApprovalMode,
                approvers,
                row.Request.RowVersion,
                isRequester && row.Request.Status == PaymentRequestStatuses.Draft && canCreatePermission,
                isRequester && row.Request.Status == PaymentRequestStatuses.Draft && canCreatePermission
                    && currentYearReady && row.Request.Amount is > 0m && row.Request.Amount <= balance.MaximumReversibleAmount,
                canApprove && approvers.Any(item => item.CanDecide),
                isRequester && row.Request.Status == PaymentRequestStatuses.Draft && canCreatePermission));
        }

        return Result<ProjectPaymentReversalPageDto>.Success(new ProjectPaymentReversalPageDto(
            requestId,
            Reference(original.Value.Request),
            balance.OriginalAmount,
            balance.TotalSettled,
            balance.TotalReleased,
            balance.EffectiveActiveAllocation,
            balance.RemainingPayable,
            balance.MaximumReversibleAmount,
            original.Value.Request.Currency ?? string.Empty,
            original.Value.Detail.ProjectAnnualBudgetId,
            original.Value.Detail.AnnualBudgetFiscalYear,
            original.Value.Detail.AnnualBudgetReference,
            hasPending,
            canCreate,
            items));
    }

    public async Task<Result<IReadOnlyList<PaymentRequestEligibleApproverDto>>> GetEligibleReversalApproversAsync(
        Guid tenantId, Guid projectId, Guid actorId, CancellationToken cancellationToken)
    {
        var access = await ValidateServiceAccessAsync(tenantId, projectId, actorId, requireWrite: true, cancellationToken,
            actionPermission: ReversalCreatePermission);
        if (access.Error is not null)
            return Result<IReadOnlyList<PaymentRequestEligibleApproverDto>>.Failure(access.Error);
        var effective = await permissions.GetEffectivePermissionsAsync(actorId, tenantId, cancellationToken);
        if (!effective.Contains(ReversalCreatePermission))
            return Result<IReadOnlyList<PaymentRequestEligibleApproverDto>>.Failure(
                Error.Forbidden("ProjectPaymentReversal.Create is required to select reversal approvers."));
        var eligible = await GetEligibleReversalApproverIdsAsync(tenantId, projectId, cancellationToken);
        // No saved Reversal-specific self-approval policy exists, so the requester
        // must not be offered as an approver even when they are on the Project Team.
        eligible.Remove(actorId);
        var names = await LoadUserNamesAsync(tenantId, eligible, cancellationToken);
        return Result<IReadOnlyList<PaymentRequestEligibleApproverDto>>.Success(eligible
            .OrderBy(id => names.GetValueOrDefault(id), StringComparer.OrdinalIgnoreCase)
            .Select(id => new PaymentRequestEligibleApproverDto(id, names.GetValueOrDefault(id) ?? string.Empty))
            .ToArray());
    }

    public async Task<Result<IReadOnlyList<PaymentRequestEligibleApproverDto>>> GetEligibleReversalRecoveryApproversAsync(
        Guid tenantId, Guid projectId, Guid requestId, Guid reversalId, Guid currentApproverId, Guid actorId,
        CancellationToken cancellationToken)
    {
        var access = await ValidateServiceAccessAsync(tenantId, projectId, actorId, requireWrite: true,
            cancellationToken, actionPermission: ReversalApprovePermission);
        if (access.Error is not null)
            return Result<IReadOnlyList<PaymentRequestEligibleApproverDto>>.Failure(access.Error);
        var actorPermissions = await permissions.GetEffectivePermissionsAsync(actorId, tenantId, cancellationToken);
        if (!actorPermissions.Contains("Governance.AssignmentRecovery.Manage"))
            return Result<IReadOnlyList<PaymentRequestEligibleApproverDto>>.Failure(Error.Forbidden(
                "Governance.AssignmentRecovery.Manage is required to view reversal recovery options."));
        var actorError = await ValidateActiveActorAsync(tenantId, actorId, cancellationToken);
        if (actorError is not null)
            return Result<IReadOnlyList<PaymentRequestEligibleApproverDto>>.Failure(actorError);

        var original = await FindRequestAsync(tenantId, projectId, requestId, cancellationToken);
        if (original is null || original.Value.Detail.RequestKind != ProjectPaymentRequestKinds.PaymentRequest)
            return Result<IReadOnlyList<PaymentRequestEligibleApproverDto>>.Failure(Error.NotFound("Payment Request not found."));
        var reversal = await FindReversalByIdAsync(tenantId, projectId, requestId, reversalId,
            tracked: false, cancellationToken);
        if (reversal is null)
            return Result<IReadOnlyList<PaymentRequestEligibleApproverDto>>.Failure(Error.NotFound("Reversal Request not found."));
        if (reversal.Value.Request.Status != PaymentRequestStatuses.Pending || reversal.Value.Detail.ApprovalMode is null)
            return Result<IReadOnlyList<PaymentRequestEligibleApproverDto>>.Failure(Error.Conflict(
                "Approver recovery options are available only for a pending Reversal Request."));

        var assignments = await GetAssignmentsAsync(tenantId, reversalId, tracked: false, cancellationToken);
        var pending = assignments.Where(item => item.ApproverUserId == currentApproverId
            && item.State == PaymentRequestAssignmentStates.Pending).Take(2).ToArray();
        if (pending.Length != 1)
            return Result<IReadOnlyList<PaymentRequestEligibleApproverDto>>.Failure(Error.Conflict(
                "The selected approver must have exactly one pending assignment on this reversal."));
        if (await db.ApprovalActions.IgnoreQueryFilters().AnyAsync(item => item.TenantId == tenantId
                && !item.IsDeleted && item.ApprovalRequestId == reversalId
                && item.ApproverAssignmentId == pending[0].Id, cancellationToken))
            return Result<IReadOnlyList<PaymentRequestEligibleApproverDto>>.Failure(Error.Conflict(
                "An assignment with a recorded decision cannot be recovered."));

        var eligible = await GetEligibleReversalApproverIdsAsync(tenantId, projectId, cancellationToken);
        var requesterId = reversal.Value.Request.RequestedBy ?? Guid.Empty;
        eligible.Remove(requesterId);
        if (eligible.Contains(currentApproverId))
            return Result<IReadOnlyList<PaymentRequestEligibleApproverDto>>.Failure(Error.Conflict(
                "The assigned approver remains eligible; reassignment is not available."));
        eligible.ExceptWith(assignments.Select(item => item.ApproverUserId));
        var names = await LoadUserNamesAsync(tenantId, eligible, cancellationToken);
        return Result<IReadOnlyList<PaymentRequestEligibleApproverDto>>.Success(eligible
            .OrderBy(id => names.GetValueOrDefault(id), StringComparer.OrdinalIgnoreCase)
            .Select(id => new PaymentRequestEligibleApproverDto(id, names.GetValueOrDefault(id) ?? string.Empty))
            .ToArray());
    }

    public async Task<Result<ProjectPaymentReversalDto>> ReassignReversalApproverAsync(
        Guid tenantId, Guid projectId, Guid requestId, Guid reversalId, Guid actorId, string? actorName,
        ReassignPaymentRequestApproverDto dto, CancellationToken cancellationToken)
    {
        var access = await ValidateServiceAccessAsync(tenantId, projectId, actorId, requireWrite: true,
            cancellationToken, actionPermission: ReversalApprovePermission);
        if (access.Error is not null)
            return Result<ProjectPaymentReversalDto>.Failure(access.Error);

        var actorPermissions = await permissions.GetEffectivePermissionsAsync(actorId, tenantId, cancellationToken);
        if (!actorPermissions.Contains("Governance.AssignmentRecovery.Manage"))
            return Result<ProjectPaymentReversalDto>.Failure(Error.Forbidden(
                "Governance.AssignmentRecovery.Manage is required to reassign a reversal approver."));
        var reason = dto.Reason?.Trim();
        if (dto.CurrentApproverId == Guid.Empty || dto.ReplacementApproverId == Guid.Empty
            || dto.CurrentApproverId == dto.ReplacementApproverId)
            return Result<ProjectPaymentReversalDto>.Failure(Error.Validation(
                "Choose distinct current and replacement approvers."));
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 1000)
            return Result<ProjectPaymentReversalDto>.Failure(Error.Validation(
                "A reassignment reason of 1 to 1000 characters is required."));

        await using var mutation = await TenantAdminMutationGuard.AcquireAsync(db, tenantId, cancellationToken);
        actorPermissions = await permissions.GetEffectivePermissionsAsync(actorId, tenantId, cancellationToken);
        if (!actorPermissions.Contains(ReversalApprovePermission)
            || !actorPermissions.Contains("Governance.AssignmentRecovery.Manage"))
            return Result<ProjectPaymentReversalDto>.Failure(Error.Forbidden(
                "Reversal reassignment permissions must remain effective when the operation starts."));
        var activeActorError = await ValidateActiveActorAsync(tenantId, actorId, cancellationToken);
        if (activeActorError is not null)
            return Result<ProjectPaymentReversalDto>.Failure(activeActorError);

        var project = await FindProjectForUpdateAsync(tenantId, projectId, cancellationToken);
        if (project is null)
            return Result<ProjectPaymentReversalDto>.Failure(Error.NotFound("Project not found."));
        var original = await FindRequestTrackedAsync(tenantId, projectId, requestId, cancellationToken);
        if (original is null || original.Value.Detail.RequestKind != ProjectPaymentRequestKinds.PaymentRequest)
            return Result<ProjectPaymentReversalDto>.Failure(Error.NotFound("Payment Request not found."));
        var reversal = await FindReversalByIdAsync(tenantId, projectId, requestId, reversalId,
            tracked: true, cancellationToken);
        if (reversal is null)
            return Result<ProjectPaymentReversalDto>.Failure(Error.NotFound("Reversal Request not found."));
        if (!MatchesVersion(reversal.Value.Request.RowVersion, dto.RowVersion))
            return Result<ProjectPaymentReversalDto>.Failure(Error.Conflict(
                "Reversal Request changed. Refresh before reassigning an approver."));
        if (reversal.Value.Request.Status != PaymentRequestStatuses.Pending || reversal.Value.Detail.ApprovalMode is null)
            return Result<ProjectPaymentReversalDto>.Failure(Error.Conflict(
                "Only a pending Reversal Request can have an approver reassigned."));
        if (reversal.Value.Request.RequestedBy == dto.ReplacementApproverId)
            return Result<ProjectPaymentReversalDto>.Failure(Error.Validation(
                "The requester cannot be assigned to approve their own Reversal Request."));

        var assignments = await GetAssignmentsAsync(tenantId, reversalId, tracked: true, cancellationToken);
        var pendingAssignments = assignments.Where(item => item.ApproverUserId == dto.CurrentApproverId
            && item.State == PaymentRequestAssignmentStates.Pending).Take(2).ToArray();
        if (pendingAssignments.Length != 1)
            return Result<ProjectPaymentReversalDto>.Failure(Error.Conflict(
                "The selected approver must have exactly one pending assignment on this reversal."));
        var assignment = pendingAssignments[0];
        var actions = await db.ApprovalActions.IgnoreQueryFilters()
            .Where(item => item.TenantId == tenantId && !item.IsDeleted && item.ApprovalRequestId == reversalId)
            .ToListAsync(cancellationToken);
        if (actions.Any(item => item.ApproverAssignmentId == assignment.Id))
            return Result<ProjectPaymentReversalDto>.Failure(Error.Conflict(
                "An approver assignment with a recorded decision cannot be reassigned."));
        if (assignments.Any(item => item.Id != assignment.Id && item.ApproverUserId == dto.ReplacementApproverId))
            return Result<ProjectPaymentReversalDto>.Failure(Error.Validation(
                "The replacement approver is already assigned to this approval plan."));

        var eligibleApprovers = await GetEligibleReversalApproverIdsAsync(tenantId, projectId, cancellationToken);
        eligibleApprovers.Remove(reversal.Value.Request.RequestedBy ?? Guid.Empty);
        if (eligibleApprovers.Contains(dto.CurrentApproverId))
            return Result<ProjectPaymentReversalDto>.Failure(Error.Conflict(
                "The assigned approver remains eligible; reassignment is not available."));
        if (!eligibleApprovers.Contains(dto.ReplacementApproverId))
            return Result<ProjectPaymentReversalDto>.Failure(Error.Validation(
                "The replacement must be an active Project Team member with ProjectPaymentReversal.Approve permission."));

        var balanceResult = await GetReversalBalanceAsync(
            tenantId, projectId, original.Value.Request, original.Value.Detail, cancellationToken);
        if (balanceResult.Error is not null || balanceResult.Balance is null)
            return Result<ProjectPaymentReversalDto>.Failure(balanceResult.Error
                ?? Error.Conflict("The Payment Request reversal balance could not be calculated."));

        var names = await LoadUserNamesAsync(tenantId,
            [dto.CurrentApproverId, dto.ReplacementApproverId], cancellationToken);
        string ApproverSnapshot() => JsonSerializer.Serialize(assignments
            .OrderBy(item => item.Position)
            .Select(item => new { item.ApproverUserId, item.Position, item.State }));
        var beforeApprovers = ApproverSnapshot();
        var beforeState = new
        {
            status = reversal.Value.Request.Status,
            amount = reversal.Value.Request.Amount,
            reason = reversal.Value.Request.Description,
            approvalMode = reversal.Value.Detail.ApprovalMode,
            approvers = beforeApprovers
        };
        assignment.ApproverUserId = dto.ReplacementApproverId;
        assignment.UpdatedBy = actorId.ToString("D");
        reversal.Value.Request.UpdatedBy = actorId.ToString("D");
        if (reversal.Value.Detail.ApprovalMode == PaymentRequestApprovalModes.Sequential
            && reversal.Value.Request.CurrentApproverId == dto.CurrentApproverId)
            reversal.Value.Request.CurrentApproverId = dto.ReplacementApproverId;
        var afterState = new
        {
            status = reversal.Value.Request.Status,
            amount = reversal.Value.Request.Amount,
            reason = reversal.Value.Request.Description,
            approvalMode = reversal.Value.Detail.ApprovalMode,
            approvers = ApproverSnapshot()
        };
        AddReversalAudit(project, reversal.Value.Request, original.Value.Request, reversal.Value.Detail,
            actorId, actorName, "ApproverReassigned",
            $"Reversal {ReversalReference(reversal.Value.Request)} approver reassigned from "
                + $"{names.GetValueOrDefault(dto.CurrentApproverId) ?? dto.CurrentApproverId.ToString("D")} to "
                + $"{names.GetValueOrDefault(dto.ReplacementApproverId) ?? dto.ReplacementApproverId.ToString("D")}: {reason}",
            balanceResult.Balance, balanceResult.Balance, beforeState, afterState,
            balanceResult.Balance, reversal.Value.Request.Amount ?? 0m);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await mutation.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<ProjectPaymentReversalDto>.Failure(Error.Conflict(
                "Reversal Request changed during approver reassignment. Refresh and try again."));
        }
        catch (DbUpdateException)
        {
            return Result<ProjectPaymentReversalDto>.Failure(Error.Conflict(
                "Reversal approver reassignment conflicted with another update."));
        }

        var finalBalance = await GetReversalBalanceAsync(
            tenantId, projectId, original.Value.Request, original.Value.Detail, cancellationToken);
        if (finalBalance.Error is not null || finalBalance.Balance is null)
            return Result<ProjectPaymentReversalDto>.Failure(finalBalance.Error
                ?? Error.Conflict("The Payment Request reversal balance could not be calculated."));
        return Result<ProjectPaymentReversalDto>.Success(await MapReversalAsync(
            tenantId, actorId, original.Value.Request, original.Value.Detail,
            reversal.Value.Request, reversal.Value.Detail, finalBalance.Balance, cancellationToken));
    }

    public async Task<Result<ProjectPaymentReversalDto>> CreateReversalAsync(
        Guid tenantId, Guid projectId, Guid requestId, Guid actorId, string? actorName,
        CreatePaymentReversalDto dto, CancellationToken cancellationToken)
    {
        var access = await ValidateReversalCreateAccessAsync(tenantId, projectId, actorId, cancellationToken);
        if (access is not null) return Result<ProjectPaymentReversalDto>.Failure(access);
        var reason = dto.Reason?.Trim() ?? string.Empty;
        if (dto.IdempotencyKey == Guid.Empty)
            return Result<ProjectPaymentReversalDto>.Failure(Error.Validation("A reversal idempotency key is required."));
        if (dto.Amount <= 0m || decimal.Round(dto.Amount, 4) != dto.Amount)
            return Result<ProjectPaymentReversalDto>.Failure(Error.Validation("Reversal amount must be positive and use no more than four decimal places."));
        if (reason.Length is 0 or > 2000)
            return Result<ProjectPaymentReversalDto>.Failure(Error.Validation("A reversal reason of up to 2000 characters is required."));

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var project = await FindProjectForUpdateAsync(tenantId, projectId, cancellationToken);
        if (project is null) return Result<ProjectPaymentReversalDto>.Failure(Error.NotFound("Project not found."));
        var actorError = await ValidateActiveActorAsync(tenantId, actorId, cancellationToken);
        if (actorError is not null) return Result<ProjectPaymentReversalDto>.Failure(actorError);
        var original = await FindRequestTrackedAsync(tenantId, projectId, requestId, cancellationToken);
        if (original is null || original.Value.Detail.RequestKind != ProjectPaymentRequestKinds.PaymentRequest)
            return Result<ProjectPaymentReversalDto>.Failure(Error.NotFound("Payment Request not found."));
        var originalRequest = original.Value.Request;
        var originalDetail = original.Value.Detail;
        var originalError = await ValidateReversibleOriginalAsync(tenantId, projectId, project, originalRequest,
            originalDetail, cancellationToken);
        if (originalError is not null) return Result<ProjectPaymentReversalDto>.Failure(originalError);
        var balanceResult = await GetReversalBalanceAsync(tenantId, projectId, originalRequest, originalDetail, cancellationToken);
        if (balanceResult.Error is not null || balanceResult.Balance is null)
            return Result<ProjectPaymentReversalDto>.Failure(balanceResult.Error
                ?? Error.Conflict("The Payment Request reversal balance could not be calculated."));
        var fingerprint = ReversalFingerprint(requestId, dto.Amount, reason);
        var priorDetail = await AllProjectPaymentDetailsQuery(tenantId, projectId)
            .FirstOrDefaultAsync(item => item.RequestKind == ProjectPaymentRequestKinds.ReversalRequest
                && item.ReversesApprovalRequestId == requestId && item.IdempotencyKey == dto.IdempotencyKey, cancellationToken);
        if (priorDetail is not null)
        {
            if (!string.Equals(priorDetail.RequestFingerprint, fingerprint, StringComparison.Ordinal))
                return Result<ProjectPaymentReversalDto>.Failure(Error.Conflict("This idempotency key was already used for different reversal details."));
            var prior = await FindReversalByIdAsync(tenantId, projectId, requestId, priorDetail.ApprovalRequestId, tracked: false, cancellationToken);
            if (prior is null) return Result<ProjectPaymentReversalDto>.Failure(Error.NotFound("Reversal Request not found."));
            if (prior.Value.Request.RequestedBy != actorId)
                return Result<ProjectPaymentReversalDto>.Failure(Error.Forbidden("Only the original requester may retry this idempotent create command."));
            await transaction.CommitAsync(cancellationToken);
            return Result<ProjectPaymentReversalDto>.Success(await MapReversalAsync(
                tenantId, actorId, originalRequest, originalDetail, prior.Value.Request, prior.Value.Detail,
                balanceResult.Balance, cancellationToken));
        }
        if (dto.Amount > balanceResult.Balance.MaximumReversibleAmount)
            return Result<ProjectPaymentReversalDto>.Failure(Error.Conflict("The requested release exceeds the current unpaid commitment."));

        var reversal = new ApprovalRequest
        {
            TenantId = tenantId,
            CreatedBy = actorId.ToString("D"),
            Title = $"Reversal Request for {Reference(originalRequest)}",
            Description = reason,
            EntityType = "Project",
            EntityId = projectId,
            Type = ReversalRequestType,
            Status = PaymentRequestStatuses.Draft,
            Priority = "Medium",
            Amount = dto.Amount,
            Currency = NormalizeCurrency(originalRequest.Currency),
            RequestedBy = actorId,
            RequestedAt = DateTime.UtcNow
        };
        reversal.EntityNumber = MakeReversalReference(reversal.Id);
        var reversalDetail = new ProjectPaymentRequestDetail
        {
            ApprovalRequestId = reversal.Id,
            TenantId = tenantId,
            ProjectId = projectId,
            ProjectAnnualBudgetId = originalDetail.ProjectAnnualBudgetId,
            AnnualBudgetFiscalYear = originalDetail.AnnualBudgetFiscalYear,
            AnnualBudgetReference = originalDetail.AnnualBudgetReference,
            RequestKind = ProjectPaymentRequestKinds.ReversalRequest,
            ReversesApprovalRequestId = requestId,
            FundingTransferId = (await GetCurrentFundingTransferAsync(tenantId, projectId, requestId, cancellationToken))?.Id,
            IdempotencyKey = dto.IdempotencyKey,
            RequestFingerprint = fingerprint
        };
        db.ApprovalRequests.Add(reversal);
        db.ProjectPaymentRequestDetails.Add(reversalDetail);
        AddReversalAudit(project, reversal, originalRequest, reversalDetail, actorId, actorName,
            "ReversalRequested", "Unpaid commitment reversal requested", null, balanceResult.Balance,
            null, new { reason }, balanceResult.Balance, dto.Amount);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<ProjectPaymentReversalDto>.Failure(Error.Conflict("The Payment Request changed while creating this reversal. Refresh and retry."));
        }
        catch (DbUpdateException)
        {
            return Result<ProjectPaymentReversalDto>.Failure(Error.Conflict("This reversal idempotency key was used by another command."));
        }
        return Result<ProjectPaymentReversalDto>.Success(await MapReversalAsync(
            tenantId, actorId, originalRequest, originalDetail, reversal, reversalDetail,
            balanceResult.Balance, cancellationToken));
    }

    public async Task<Result<ProjectPaymentReversalDto>> UpdateReversalAsync(
        Guid tenantId, Guid projectId, Guid requestId, Guid reversalId, Guid actorId, string? actorName,
        UpdatePaymentReversalDto dto, CancellationToken cancellationToken)
    {
        var access = await ValidateReversalCreateAccessAsync(tenantId, projectId, actorId, cancellationToken);
        if (access is not null) return Result<ProjectPaymentReversalDto>.Failure(access);
        if (dto.Amount <= 0m || decimal.Round(dto.Amount, 4) != dto.Amount)
            return Result<ProjectPaymentReversalDto>.Failure(Error.Validation("Reversal amount must be positive and use no more than four decimal places."));
        var reason = dto.Reason?.Trim() ?? string.Empty;
        if (reason.Length is 0 or > 2000)
            return Result<ProjectPaymentReversalDto>.Failure(Error.Validation("A reversal reason of up to 2000 characters is required."));
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var project = await FindProjectForUpdateAsync(tenantId, projectId, cancellationToken);
        if (project is null) return Result<ProjectPaymentReversalDto>.Failure(Error.NotFound("Project not found."));
        var original = await FindRequestTrackedAsync(tenantId, projectId, requestId, cancellationToken);
        var reversal = await FindReversalByIdAsync(tenantId, projectId, requestId, reversalId, tracked: true, cancellationToken);
        if (original is null || original.Value.Detail.RequestKind != ProjectPaymentRequestKinds.PaymentRequest || reversal is null)
            return Result<ProjectPaymentReversalDto>.Failure(Error.NotFound("Reversal Request not found."));
        if (reversal.Value.Request.RequestedBy != actorId)
            return Result<ProjectPaymentReversalDto>.Failure(Error.Forbidden("Only the reversal requester may edit this draft."));
        if (reversal.Value.Request.Status != PaymentRequestStatuses.Draft)
            return Result<ProjectPaymentReversalDto>.Failure(Error.Conflict("Only a Draft Reversal Request can be edited."));
        if (!MatchesVersion(reversal.Value.Request.RowVersion, dto.RowVersion))
            return Result<ProjectPaymentReversalDto>.Failure(Error.Conflict("Reversal Request changed. Refresh before editing."));
        var originalError = await ValidateReversibleOriginalAsync(tenantId, projectId, project,
            original.Value.Request, original.Value.Detail, cancellationToken);
        if (originalError is not null) return Result<ProjectPaymentReversalDto>.Failure(originalError);
        var balance = await GetReversalBalanceAsync(tenantId, projectId, original.Value.Request, original.Value.Detail, cancellationToken);
        if (balance.Error is not null || balance.Balance is null)
            return Result<ProjectPaymentReversalDto>.Failure(balance.Error
                ?? Error.Conflict("The Payment Request reversal balance could not be calculated."));
        if (dto.Amount > balance.Balance.MaximumReversibleAmount)
            return Result<ProjectPaymentReversalDto>.Failure(Error.Conflict("The requested release exceeds the current unpaid commitment."));
        var beforeStatus = reversal.Value.Request.Status;
        var beforeAmount = reversal.Value.Request.Amount;
        var beforeReason = reversal.Value.Request.Description;
        reversal.Value.Request.Amount = dto.Amount;
        reversal.Value.Request.Description = reason;
        reversal.Value.Request.UpdatedBy = actorId.ToString("D");
        AddReversalAudit(project, reversal.Value.Request, original.Value.Request, reversal.Value.Detail,
            actorId, actorName, "ReversalEdited", "Reversal draft updated", balance.Balance, balance.Balance,
            new { status = beforeStatus, amount = beforeAmount, reason = beforeReason },
            new { status = reversal.Value.Request.Status, amount = dto.Amount, reason }, balance.Balance, dto.Amount);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<ProjectPaymentReversalDto>.Failure(Error.Conflict("Reversal Request changed. Refresh before editing."));
        }
        return Result<ProjectPaymentReversalDto>.Success(await MapReversalAsync(
            tenantId, actorId, original.Value.Request, original.Value.Detail, reversal.Value.Request,
            reversal.Value.Detail, balance.Balance, cancellationToken));
    }

    public async Task<Result<ProjectPaymentReversalDto>> SubmitReversalAsync(
        Guid tenantId, Guid projectId, Guid requestId, Guid reversalId, Guid actorId, string? actorName,
        SubmitPaymentReversalDto dto, CancellationToken cancellationToken)
    {
        var access = await ValidateReversalCreateAccessAsync(tenantId, projectId, actorId, cancellationToken);
        if (access is not null) return Result<ProjectPaymentReversalDto>.Failure(access);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var project = await FindProjectForUpdateAsync(tenantId, projectId, cancellationToken);
        if (project is null) return Result<ProjectPaymentReversalDto>.Failure(Error.NotFound("Project not found."));
        var actorError = await ValidateActiveActorAsync(tenantId, actorId, cancellationToken);
        if (actorError is not null) return Result<ProjectPaymentReversalDto>.Failure(actorError);
        var original = await FindRequestTrackedAsync(tenantId, projectId, requestId, cancellationToken);
        var reversal = await FindReversalByIdAsync(tenantId, projectId, requestId, reversalId, tracked: true, cancellationToken);
        if (original is null || reversal is null || original.Value.Detail.RequestKind != ProjectPaymentRequestKinds.PaymentRequest)
            return Result<ProjectPaymentReversalDto>.Failure(Error.NotFound("Reversal Request not found."));
        if (reversal.Value.Request.RequestedBy != actorId)
            return Result<ProjectPaymentReversalDto>.Failure(Error.Forbidden("Only the reversal requester may submit this draft."));
        var mode = NormalizeMode(dto.ApprovalMode);
        var selections = NormalizeSelections(dto.Approvers, mode);
        if (reversal.Value.Request.Status != PaymentRequestStatuses.Draft)
        {
            var priorAssignments = await GetAssignmentsAsync(tenantId, reversalId, tracked: false, cancellationToken);
            if (reversal.Value.Request.Status == PaymentRequestStatuses.Pending
                && string.Equals(reversal.Value.Detail.ApprovalMode, mode, StringComparison.OrdinalIgnoreCase)
                && SameSelections(priorAssignments, selections))
            {
                await transaction.CommitAsync(cancellationToken);
                var currentBalance = await GetReversalBalanceAsync(tenantId, projectId, original.Value.Request, original.Value.Detail, cancellationToken);
                if (currentBalance.Error is not null || currentBalance.Balance is null)
                    return Result<ProjectPaymentReversalDto>.Failure(currentBalance.Error
                        ?? Error.Conflict("The Payment Request reversal balance could not be calculated."));
                return Result<ProjectPaymentReversalDto>.Success(await MapReversalAsync(
                    tenantId, actorId, original.Value.Request, original.Value.Detail, reversal.Value.Request,
                    reversal.Value.Detail, currentBalance.Balance, cancellationToken));
            }
            return Result<ProjectPaymentReversalDto>.Failure(Error.Conflict("Only Draft Reversal Requests can be submitted."));
        }
        if (!MatchesVersion(reversal.Value.Request.RowVersion, dto.RowVersion))
            return Result<ProjectPaymentReversalDto>.Failure(Error.Conflict("Reversal Request changed. Refresh before submitting."));
        var originalError = await ValidateReversibleOriginalAsync(tenantId, projectId, project,
            original.Value.Request, original.Value.Detail, cancellationToken);
        if (originalError is not null) return Result<ProjectPaymentReversalDto>.Failure(originalError);
        if (await HasOtherPendingReversalAsync(tenantId, projectId, requestId, reversalId, cancellationToken))
            return Result<ProjectPaymentReversalDto>.Failure(Error.Conflict("Another Reversal Request for this Payment Request is already pending approval."));
        var setupErrors = PaymentRequestWorkflowPolicy.ValidateApprovalSetup(project.FinanceApprovalMode, mode, selections);
        if (setupErrors.Count > 0)
            return Result<ProjectPaymentReversalDto>.Failure(Error.Validation(string.Join(",", setupErrors)));
        var eligible = await GetEligibleReversalApproverIdsAsync(tenantId, projectId, cancellationToken);
        eligible.Remove(actorId);
        if (selections.Any(item => !eligible.Contains(item.UserId)))
            return Result<ProjectPaymentReversalDto>.Failure(Error.Validation(
                "Every reversal approver must be an active eligible Project Team member with ProjectPaymentReversal.Approve permission, and the requester cannot approve their own request."));
        var balanceResult = await GetReversalBalanceAsync(tenantId, projectId, original.Value.Request, original.Value.Detail, cancellationToken);
        if (balanceResult.Error is not null || balanceResult.Balance is null)
            return Result<ProjectPaymentReversalDto>.Failure(balanceResult.Error
                ?? Error.Conflict("The Payment Request reversal balance could not be calculated."));
        if (reversal.Value.Request.Amount is not > 0m
            || reversal.Value.Request.Amount > balanceResult.Balance.MaximumReversibleAmount)
            return Result<ProjectPaymentReversalDto>.Failure(Error.Conflict("The requested release exceeds the current unpaid commitment."));

        var now = DateTime.UtcNow;
        reversal.Value.Detail.ApprovalMode = mode;
        reversal.Value.Detail.SubmittedAt = now;
        reversal.Value.Request.Status = PaymentRequestStatuses.Pending;
        reversal.Value.Request.CurrentApproverId = mode == PaymentRequestApprovalModes.Sequential
            ? selections.OrderBy(item => item.Position).First().UserId : null;
        reversal.Value.Request.ApprovalLevel = mode == PaymentRequestApprovalModes.Sequential ? 1 : 0;
        reversal.Value.Request.UpdatedBy = actorId.ToString("D");
        var assignments = selections.Select(item => new PaymentRequestApproverAssignment
        {
            TenantId = tenantId,
            ApprovalRequestId = reversalId,
            ApproverUserId = item.UserId,
            Position = item.Position,
            Requirement = item.Requirement,
            State = PaymentRequestAssignmentStates.Pending,
            CreatedBy = actorId.ToString("D")
        }).ToArray();
        db.PaymentRequestApproverAssignments.AddRange(assignments);
        var names = await LoadUserNamesAsync(tenantId, selections.Select(item => item.UserId), cancellationToken);
        var approverSummary = string.Join(", ", selections.OrderBy(item => item.Position ?? int.MaxValue)
            .Select(item => $"{names.GetValueOrDefault(item.UserId) ?? item.UserId.ToString("D")} [{item.UserId:D}] ({item.Requirement})"));
        AddReversalAudit(project, reversal.Value.Request, original.Value.Request, reversal.Value.Detail,
            actorId, actorName, "ReversalSubmitted", "Reversal submitted for financial approval", balanceResult.Balance,
            balanceResult.Balance, new { status = PaymentRequestStatuses.Draft },
            new { status = PaymentRequestStatuses.Pending, approvalMode = mode, approvers = approverSummary },
            balanceResult.Balance, reversal.Value.Request.Amount ?? 0m);
        AddReversalAudit(project, reversal.Value.Request, original.Value.Request, reversal.Value.Detail,
            actorId, actorName, "ReversalApproverAssigned", $"Reversal approvers assigned: {approverSummary}",
            balanceResult.Balance, balanceResult.Balance, null, new { approvalMode = mode, approvers = approverSummary },
            balanceResult.Balance, reversal.Value.Request.Amount ?? 0m);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<ProjectPaymentReversalDto>.Failure(Error.Conflict("Reversal Request changed while submitting. Refresh and try again."));
        }
        return Result<ProjectPaymentReversalDto>.Success(await MapReversalAsync(
            tenantId, actorId, original.Value.Request, original.Value.Detail, reversal.Value.Request,
            reversal.Value.Detail, balanceResult.Balance, cancellationToken));
    }

    public async Task<Result<ProjectPaymentReversalDto>> GetAssignedReversalReviewAsync(
        Guid tenantId, Guid projectId, Guid requestId, Guid reversalId, Guid actorId,
        CancellationToken cancellationToken)
    {
        var identityError = await ValidateReversalReviewerIdentityAsync(tenantId, actorId, cancellationToken);
        if (identityError is not null)
            return Result<ProjectPaymentReversalDto>.Failure(identityError);

        var original = await FindRequestAsync(tenantId, projectId, requestId, cancellationToken);
        var reversal = await FindReversalByIdAsync(tenantId, projectId, requestId, reversalId,
            tracked: false, cancellationToken);
        if (original is null || reversal is null
            || original.Value.Detail.RequestKind != ProjectPaymentRequestKinds.PaymentRequest
            || reversal.Value.Request.Status != PaymentRequestStatuses.Pending
            || reversal.Value.Request.RequestedBy == actorId
            || reversal.Value.Detail.ApprovalMode is null
            || !(await GetEligibleReversalApproverIdsAsync(tenantId, projectId, cancellationToken)).Contains(actorId))
            return Result<ProjectPaymentReversalDto>.Failure(Error.NotFound("Assigned Reversal Request not found."));

        var assignments = await GetAssignmentsAsync(tenantId, reversalId, tracked: false, cancellationToken);
        var actions = await db.ApprovalActions.IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.TenantId == tenantId && !item.IsDeleted && item.ApprovalRequestId == reversalId)
            .ToListAsync(cancellationToken);
        var assignment = assignments.FirstOrDefault(item => item.ApproverUserId == actorId
            && item.State == PaymentRequestAssignmentStates.Pending);
        if (assignment is null || actions.Any(item => item.ApproverAssignmentId == assignment.Id))
            return Result<ProjectPaymentReversalDto>.Failure(Error.NotFound("Assigned Reversal Request not found."));
        var states = BuildStates(assignments, actions);
        var canDecide = reversal.Value.Detail.ApprovalMode == PaymentRequestApprovalModes.Sequential
            ? PaymentRequestWorkflowPolicy.CanDecideSequentially(actorId, reversal.Value.Request.Status, states)
            : PaymentRequestWorkflowPolicy.CanDecideInParallel(actorId, reversal.Value.Request.Status, states);
        if (!canDecide)
            return Result<ProjectPaymentReversalDto>.Failure(Error.NotFound("Assigned Reversal Request not found."));

        var balance = await GetReversalBalanceAsync(tenantId, projectId, original.Value.Request,
            original.Value.Detail, cancellationToken);
        if (balance.Error is not null || balance.Balance is null)
            return Result<ProjectPaymentReversalDto>.Failure(balance.Error
                ?? Error.Conflict("The Payment Request reversal balance could not be calculated."));
        return Result<ProjectPaymentReversalDto>.Success(await MapReversalAsync(
            tenantId, actorId, original.Value.Request, original.Value.Detail,
            reversal.Value.Request, reversal.Value.Detail, balance.Balance, cancellationToken));
    }

    public async Task<Result<ProjectPaymentReversalDto>> DecideReversalAsync(
        Guid tenantId, Guid projectId, Guid requestId, Guid reversalId, Guid actorId, string? actorName,
        DecidePaymentRequestApprovalDto dto, CancellationToken cancellationToken)
    {
        var identityError = await ValidateReversalReviewerIdentityAsync(tenantId, actorId, cancellationToken);
        if (identityError is not null)
            return Result<ProjectPaymentReversalDto>.Failure(identityError);
        if (!PaymentRequestDecisions.IsValid(dto.Decision))
            return Result<ProjectPaymentReversalDto>.Failure(Error.Validation("Decision must be Approved or Rejected."));

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var project = await FindProjectForUpdateAsync(tenantId, projectId, cancellationToken);
        if (project is null) return Result<ProjectPaymentReversalDto>.Failure(Error.NotFound("Project not found."));
        var actorError = await ValidateActiveActorAsync(tenantId, actorId, cancellationToken);
        if (actorError is not null) return Result<ProjectPaymentReversalDto>.Failure(actorError);
        var original = await FindRequestTrackedAsync(tenantId, projectId, requestId, cancellationToken);
        var reversal = await FindReversalByIdAsync(tenantId, projectId, requestId, reversalId, tracked: true, cancellationToken);
        if (original is null || reversal is null || original.Value.Detail.RequestKind != ProjectPaymentRequestKinds.PaymentRequest)
            return Result<ProjectPaymentReversalDto>.Failure(Error.NotFound("Reversal Request not found."));
        if (reversal.Value.Request.RequestedBy == actorId)
            return Result<ProjectPaymentReversalDto>.Failure(Error.Forbidden("A Reversal Request requester cannot approve their own request."));
        var eligible = await GetEligibleReversalApproverIdsAsync(tenantId, projectId, cancellationToken);
        if (!eligible.Contains(actorId))
            return Result<ProjectPaymentReversalDto>.Failure(Error.Forbidden("An assigned approver must remain an active eligible Project Team member."));
        var assignments = await GetAssignmentsAsync(tenantId, reversalId, tracked: true, cancellationToken);
        var actions = await db.ApprovalActions.IgnoreQueryFilters()
            .Where(item => item.TenantId == tenantId && !item.IsDeleted && item.ApprovalRequestId == reversalId)
            .ToListAsync(cancellationToken);
        var assignment = assignments.FirstOrDefault(item => item.ApproverUserId == actorId);
        if (assignment is null)
            return Result<ProjectPaymentReversalDto>.Failure(Error.Forbidden("Only an explicitly assigned reversal approver may decide."));
        var normalizedDecision = string.Equals(dto.Decision, PaymentRequestDecisions.Approved, StringComparison.OrdinalIgnoreCase)
            ? "Approve" : "Reject";
        var existingAction = actions.FirstOrDefault(item => item.ApproverAssignmentId == assignment.Id);
        if (existingAction is not null)
        {
            if (string.Equals(existingAction.Action, normalizedDecision, StringComparison.OrdinalIgnoreCase)
                && string.Equals(existingAction.Comments?.Trim(), dto.Comment?.Trim(), StringComparison.Ordinal))
            {
                var currentBalance = await GetReversalBalanceAsync(tenantId, projectId, original.Value.Request, original.Value.Detail, cancellationToken);
                if (currentBalance.Error is not null || currentBalance.Balance is null)
                    return Result<ProjectPaymentReversalDto>.Failure(currentBalance.Error
                        ?? Error.Conflict("The Payment Request reversal balance could not be calculated."));
                await transaction.CommitAsync(cancellationToken);
                return Result<ProjectPaymentReversalDto>.Success(await MapReversalAsync(
                    tenantId, actorId, original.Value.Request, original.Value.Detail, reversal.Value.Request,
                    reversal.Value.Detail, currentBalance.Balance, cancellationToken));
            }
            return Result<ProjectPaymentReversalDto>.Failure(Error.Conflict("This approver has already made a different decision."));
        }
        if (!MatchesVersion(reversal.Value.Request.RowVersion, dto.RowVersion))
            return Result<ProjectPaymentReversalDto>.Failure(Error.Conflict("Reversal Request changed. Refresh before deciding."));
        if (reversal.Value.Request.Status != PaymentRequestStatuses.Pending || reversal.Value.Detail.ApprovalMode is null)
            return Result<ProjectPaymentReversalDto>.Failure(Error.Conflict("This Reversal Request is no longer awaiting approval."));
        var states = BuildStates(assignments, actions);
        var canDecide = reversal.Value.Detail.ApprovalMode == PaymentRequestApprovalModes.Sequential
            ? PaymentRequestWorkflowPolicy.CanDecideSequentially(actorId, reversal.Value.Request.Status, states)
            : PaymentRequestWorkflowPolicy.CanDecideInParallel(actorId, reversal.Value.Request.Status, states);
        if (!canDecide || assignment.State != PaymentRequestAssignmentStates.Pending)
            return Result<ProjectPaymentReversalDto>.Failure(Error.Forbidden("Only the active assigned approver may decide this reversal."));

        var originalError = await ValidateReversibleOriginalAsync(tenantId, projectId, project,
            original.Value.Request, original.Value.Detail, cancellationToken);
        if (originalError is not null && string.Equals(dto.Decision, PaymentRequestDecisions.Approved, StringComparison.OrdinalIgnoreCase))
            return Result<ProjectPaymentReversalDto>.Failure(originalError);
        var balanceResult = await GetReversalBalanceAsync(tenantId, projectId, original.Value.Request, original.Value.Detail, cancellationToken);
        if (balanceResult.Error is not null || balanceResult.Balance is null)
            return Result<ProjectPaymentReversalDto>.Failure(balanceResult.Error
                ?? Error.Conflict("The Payment Request reversal balance could not be calculated."));
        var approved = string.Equals(dto.Decision, PaymentRequestDecisions.Approved, StringComparison.OrdinalIgnoreCase);
        if (approved && reversal.Value.Request.Amount is not > 0m)
            return Result<ProjectPaymentReversalDto>.Failure(Error.Conflict("The requested release amount is invalid."));

        var now = DateTime.UtcNow;
        assignment.State = approved ? PaymentRequestAssignmentStates.Approved : PaymentRequestAssignmentStates.Rejected;
        assignment.UpdatedBy = actorId.ToString("D");
        reversal.Value.Request.UpdatedBy = actorId.ToString("D");
        db.ApprovalActions.Add(new ApprovalAction
        {
            TenantId = tenantId,
            ApprovalRequestId = reversalId,
            ApproverId = actorId,
            ApproverAssignmentId = assignment.Id,
            Action = normalizedDecision,
            Comments = dto.Comment?.Trim(),
            ActionedAt = now,
            ApprovalLevel = reversal.Value.Detail.ApprovalMode == PaymentRequestApprovalModes.Sequential ? assignment.Position ?? 0 : 0,
            CreatedBy = actorId.ToString("D")
        });
        var pendingBefore = reversal.Value.Request.Status;
        AddReversalAudit(project, reversal.Value.Request, original.Value.Request, reversal.Value.Detail,
            actorId, actorName, approved ? "ReversalApproverApproved" : "ReversalApproverRejected",
            $"Reversal {normalizedDecision.ToLowerInvariant()} recorded by {actorName ?? actorId.ToString("D")}",
            balanceResult.Balance, balanceResult.Balance, new { status = pendingBefore },
            new { decision = approved ? PaymentRequestDecisions.Approved : PaymentRequestDecisions.Rejected, comment = dto.Comment?.Trim() },
            balanceResult.Balance, reversal.Value.Request.Amount ?? 0m);
        var projected = BuildStates(assignments, actions, actorId, normalizedDecision);
        var finalStatus = reversal.Value.Detail.ApprovalMode == PaymentRequestApprovalModes.Sequential
            ? PaymentRequestWorkflowPolicy.EvaluateSequentialStatus(projected)
            : PaymentRequestWorkflowPolicy.EvaluateParallelStatus(projected);
        if (finalStatus is PaymentRequestStatuses.Approved or PaymentRequestStatuses.Rejected)
        {
            reversal.Value.Request.Status = finalStatus;
            reversal.Value.Request.CurrentApproverId = null;
            reversal.Value.Detail.CompletedAt = now;
            if (finalStatus == PaymentRequestStatuses.Approved)
            {
                if (await HasOtherPendingReversalAsync(tenantId, projectId, requestId, reversalId, cancellationToken))
                    return Result<ProjectPaymentReversalDto>.Failure(Error.Conflict("Another Reversal Request is pending for this Payment Request."));
                var fundingTransfer = reversal.Value.Detail.FundingTransferId.HasValue
                    ? await db.ProjectPaymentRequestFundingTransfers.IgnoreQueryFilters().AsNoTracking()
                        .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.ProjectId == projectId
                            && item.Id == reversal.Value.Detail.FundingTransferId.Value
                            && item.PaymentRequestId == requestId && !item.IsDeleted, cancellationToken)
                    : null;
                if (reversal.Value.Detail.FundingTransferId.HasValue && fundingTransfer is null)
                    return Result<ProjectPaymentReversalDto>.Failure(Error.Conflict("The reversal funding transfer is unavailable."));
                var activeBudgetId = fundingTransfer?.DestinationAnnualBudgetId ?? original.Value.Detail.ProjectAnnualBudgetId;
                if (!activeBudgetId.HasValue)
                    return Result<ProjectPaymentReversalDto>.Failure(Error.Conflict("The active funding Annual Budget is unavailable."));
                var budget = await FindAnnualBudgetForUpdateAsync(tenantId, projectId, activeBudgetId.Value, cancellationToken);
                if (budget is null)
                    return Result<ProjectPaymentReversalDto>.Failure(Error.Conflict("The active funding Annual Budget is unavailable."));
                var budgetError = ValidateAnnualBudgetAssociation(project, budget, budget.FiscalYear, requireApproved: true);
                if (budgetError is not null) return Result<ProjectPaymentReversalDto>.Failure(budgetError);
                if (budget.ClosedAtUtc.HasValue)
                    return Result<ProjectPaymentReversalDto>.Failure(Error.Conflict("A reversal cannot release funds from a closed fiscal period."));
                if (fundingTransfer is not null && !string.Equals(NormalizeCurrency(fundingTransfer.Currency), NormalizeCurrency(budget.Currency), StringComparison.Ordinal))
                    return Result<ProjectPaymentReversalDto>.Failure(Error.Conflict("The active funding transfer currency is inconsistent."));
                var latest = await GetReversalBalanceAsync(tenantId, projectId, original.Value.Request, original.Value.Detail, cancellationToken);
                if (latest.Error is not null || latest.Balance is null)
                    return Result<ProjectPaymentReversalDto>.Failure(latest.Error
                        ?? Error.Conflict("The Payment Request reversal balance could not be calculated."));
                if (reversal.Value.Request.Amount is null || reversal.Value.Request.Amount > latest.Balance.MaximumReversibleAmount)
                    return Result<ProjectPaymentReversalDto>.Failure(Error.Conflict(
                        "The unpaid commitment changed. This reversal now exceeds the remaining reversible amount."));
                reversal.Value.Request.Status = PaymentRequestStatuses.Approved;
                AddReversalAudit(project, reversal.Value.Request, original.Value.Request, reversal.Value.Detail,
                    actorId, actorName, "ReversalApproved", "Unpaid commitment reversal approved", balanceResult.Balance,
                    latest.Balance, new { status = PaymentRequestStatuses.Pending },
                    new { status = PaymentRequestStatuses.Approved, approvedReleasedAmount = reversal.Value.Request.Amount },
                    latest.Balance, reversal.Value.Request.Amount.Value);
                AddReversalAudit(project, reversal.Value.Request, original.Value.Request, reversal.Value.Detail,
                    actorId, actorName, "BudgetAllocationReleased", "Approved unpaid commitment released from the original Annual Budget",
                    latest.Balance, latest.Balance with
                    {
                        TotalReleased = latest.Balance.TotalReleased + reversal.Value.Request.Amount.Value,
                        EffectiveActiveAllocation = latest.Balance.EffectiveActiveAllocation - reversal.Value.Request.Amount.Value,
                        RemainingPayable = latest.Balance.RemainingPayable - reversal.Value.Request.Amount.Value,
                        MaximumReversibleAmount = latest.Balance.MaximumReversibleAmount - reversal.Value.Request.Amount.Value
                    }, null,
                    new { approvedReleasedAmount = reversal.Value.Request.Amount, annualBudgetId = budget.Id,
                        annualBudgetReference = budget.FinancialReference, fiscalYear = budget.FiscalYear },
                    latest.Balance, reversal.Value.Request.Amount.Value);
            }
            else
            {
                AddReversalAudit(project, reversal.Value.Request, original.Value.Request, reversal.Value.Detail,
                    actorId, actorName, "ReversalRejected", "Unpaid commitment reversal rejected", balanceResult.Balance,
                    balanceResult.Balance, new { status = PaymentRequestStatuses.Pending },
                    new { status = PaymentRequestStatuses.Rejected, comment = dto.Comment?.Trim() },
                    balanceResult.Balance, reversal.Value.Request.Amount ?? 0m);
            }
        }
        else if (reversal.Value.Detail.ApprovalMode == PaymentRequestApprovalModes.Sequential)
        {
            reversal.Value.Request.CurrentApproverId = PaymentRequestWorkflowPolicy.GetActiveSequentialApprover(projected);
            reversal.Value.Request.ApprovalLevel = assignments.FirstOrDefault(item => item.ApproverUserId == reversal.Value.Request.CurrentApproverId)?.Position
                ?? reversal.Value.Request.ApprovalLevel;
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<ProjectPaymentReversalDto>.Failure(Error.Conflict("Reversal Request changed during approval. Refresh before deciding."));
        }
        catch (DbUpdateException)
        {
            return Result<ProjectPaymentReversalDto>.Failure(Error.Conflict("This reversal decision conflicts with another decision. Refresh the history."));
        }
        var finalBalance = await GetReversalBalanceAsync(tenantId, projectId, original.Value.Request, original.Value.Detail, cancellationToken);
        if (finalBalance.Error is not null || finalBalance.Balance is null)
            return Result<ProjectPaymentReversalDto>.Failure(finalBalance.Error
                ?? Error.Conflict("The Payment Request reversal balance could not be calculated."));
        return Result<ProjectPaymentReversalDto>.Success(await MapReversalAsync(
            tenantId, actorId, original.Value.Request, original.Value.Detail, reversal.Value.Request,
            reversal.Value.Detail, finalBalance.Balance, cancellationToken));
    }

    public async Task<Result<ProjectPaymentReversalDto>> CancelReversalAsync(
        Guid tenantId, Guid projectId, Guid requestId, Guid reversalId, Guid actorId, string? actorName,
        CancelPaymentRequestDto dto, CancellationToken cancellationToken)
    {
        var access = await ValidateReversalCreateAccessAsync(tenantId, projectId, actorId, cancellationToken);
        if (access is not null) return Result<ProjectPaymentReversalDto>.Failure(access);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var project = await FindProjectForUpdateAsync(tenantId, projectId, cancellationToken);
        if (project is null) return Result<ProjectPaymentReversalDto>.Failure(Error.NotFound("Project not found."));
        var original = await FindRequestTrackedAsync(tenantId, projectId, requestId, cancellationToken);
        var reversal = await FindReversalByIdAsync(tenantId, projectId, requestId, reversalId, tracked: true, cancellationToken);
        if (original is null || reversal is null || reversal.Value.Request.RequestedBy != actorId)
            return Result<ProjectPaymentReversalDto>.Failure(Error.NotFound("Reversal Request not found."));
        if (reversal.Value.Request.Status != PaymentRequestStatuses.Draft)
            return Result<ProjectPaymentReversalDto>.Failure(Error.Conflict("Only a Draft Reversal Request can be cancelled."));
        if (!MatchesVersion(reversal.Value.Request.RowVersion, dto.RowVersion))
            return Result<ProjectPaymentReversalDto>.Failure(Error.Conflict("Reversal Request changed. Refresh before cancelling."));
        var balance = await GetReversalBalanceAsync(tenantId, projectId, original.Value.Request, original.Value.Detail, cancellationToken);
        if (balance.Error is not null || balance.Balance is null)
            return Result<ProjectPaymentReversalDto>.Failure(balance.Error
                ?? Error.Conflict("The Payment Request reversal balance could not be calculated."));
        var before = reversal.Value.Request.Status;
        reversal.Value.Request.Status = PaymentRequestStatuses.Cancelled;
        reversal.Value.Request.UpdatedBy = actorId.ToString("D");
        reversal.Value.Detail.CompletedAt = DateTime.UtcNow;
        AddReversalAudit(project, reversal.Value.Request, original.Value.Request, reversal.Value.Detail,
            actorId, actorName, "ReversalCancelled", "Draft Reversal Request cancelled", balance.Balance,
            balance.Balance, new { status = before }, new { status = PaymentRequestStatuses.Cancelled },
            balance.Balance, reversal.Value.Request.Amount ?? 0m);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<ProjectPaymentReversalDto>.Failure(Error.Conflict("Reversal Request changed. Refresh before cancelling."));
        }
        return Result<ProjectPaymentReversalDto>.Success(await MapReversalAsync(
            tenantId, actorId, original.Value.Request, original.Value.Detail, reversal.Value.Request,
            reversal.Value.Detail, balance.Balance, cancellationToken));
    }

    private async Task<Error?> ValidateReversalCreateAccessAsync(
        Guid tenantId, Guid projectId, Guid actorId, CancellationToken cancellationToken)
    {
        var access = await ValidateServiceAccessAsync(tenantId, projectId, actorId, requireWrite: true, cancellationToken,
            actionPermission: ReversalCreatePermission);
        if (access.Error is not null) return access.Error;
        var effective = await permissions.GetEffectivePermissionsAsync(actorId, tenantId, cancellationToken);
        return effective.Contains(ReversalCreatePermission)
            ? null
            : Error.Forbidden("ProjectPaymentReversal.Create is required to create or manage a reversal draft.");
    }

    private async Task<Error?> ValidateReversibleOriginalAsync(
        Guid tenantId, Guid projectId, Project project, ApprovalRequest original,
        ProjectPaymentRequestDetail detail, CancellationToken cancellationToken)
    {
        if (detail.RequestKind != ProjectPaymentRequestKinds.PaymentRequest
            || original.Status != PaymentRequestStatuses.Approved || original.Amount is not > 0m)
            return Error.Conflict("Only an Approved Payment Request with a positive original amount can be reversed.");
        if (detail.ProjectAnnualBudgetId is null || detail.AnnualBudgetFiscalYear is null
            || string.IsNullOrWhiteSpace(detail.AnnualBudgetReference))
            return Error.Conflict("This historical Payment Request has no verified Annual Budget association. Reconcile it before reversal.");
        if (!string.Equals(NormalizeCurrency(original.Currency), NormalizeCurrency(project.Currency), StringComparison.Ordinal))
            return Error.Conflict("The approved Payment Request currency no longer matches the Project currency.");
        if (!await HasValidFundingTransferChainAsync(tenantId, projectId, original.Id, cancellationToken))
            return Error.Conflict("The Payment Request funding-transfer history is inconsistent. Reconcile it before reversal.");
        var fundingTransfer = await GetCurrentFundingTransferAsync(tenantId, projectId, original.Id, cancellationToken);
        var activeBudgetId = fundingTransfer?.DestinationAnnualBudgetId ?? detail.ProjectAnnualBudgetId.Value;
        var budget = await FindAnnualBudgetForUpdateAsync(tenantId, projectId, activeBudgetId, cancellationToken);
        if (budget is null)
            return Error.Conflict("The current funding Annual Budget is unavailable.");
        var budgetError = ValidateAnnualBudgetAssociation(project, budget, budget.FiscalYear, requireApproved: true);
        if (budgetError is not null) return budgetError;
        if (budget.ClosedAtUtc.HasValue)
            return Error.Conflict("Reversal release for a closed Fiscal Year is blocked. Carry the commitment through an approved closing first.");
        var currentPeriod = await tenantFiscalYear.ResolveForTimestampAsync(tenantId, DateTimeOffset.UtcNow, cancellationToken);
        if (currentPeriod.FiscalYear != budget.FiscalYear)
            return Error.Conflict("Reversal release is allowed only against the current open Annual Budget funding this commitment.");
        if (fundingTransfer is null && !string.Equals(detail.AnnualBudgetReference, budget.FinancialReference, StringComparison.Ordinal))
            return Error.Conflict("The original Annual Budget reference changed. Reconcile the financial history before reversal.");
        return null;
    }

    private async Task<bool> IsReversalBudgetCurrentAndApprovedAsync(
        Guid tenantId, Guid projectId, Project? project, ProjectPaymentRequestDetail detail,
        CancellationToken cancellationToken)
    {
        if (project is null || detail.ProjectAnnualBudgetId is null || detail.AnnualBudgetFiscalYear is null)
            return false;
        if (!await HasValidFundingTransferChainAsync(tenantId, projectId, detail.ApprovalRequestId, cancellationToken))
            return false;
        var transfer = await GetCurrentFundingTransferAsync(tenantId, projectId, detail.ApprovalRequestId, cancellationToken);
        var activeBudgetId = transfer?.DestinationAnnualBudgetId ?? detail.ProjectAnnualBudgetId.Value;
        var budget = await db.ProjectAnnualBudgets.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == activeBudgetId
                && item.TenantId == tenantId && item.ProjectId == projectId && !item.IsDeleted, cancellationToken);
        var current = await tenantFiscalYear.ResolveForTimestampAsync(tenantId, DateTimeOffset.UtcNow, cancellationToken);
        return budget is not null && budget.Status == ProjectAnnualBudgetStatuses.Approved
            && !budget.ClosedAtUtc.HasValue && current.FiscalYear == budget.FiscalYear
            && ValidateAnnualBudgetAssociation(project, budget, budget.FiscalYear, requireApproved: true) is null
            && (transfer is not null || string.Equals(detail.AnnualBudgetReference, budget.FinancialReference, StringComparison.Ordinal));
    }

    private async Task<(ReversalBalance? Balance, Error? Error)> GetReversalBalanceAsync(
        Guid tenantId, Guid projectId, ApprovalRequest original, ProjectPaymentRequestDetail originalDetail,
        CancellationToken cancellationToken)
    {
        if (original.Amount is not > 0m || decimal.Round(original.Amount.Value, 4) != original.Amount.Value)
            return (null, Error.Conflict("The original approved Payment Request amount is invalid."));
        var currency = NormalizeCurrency(original.Currency);
        if (string.IsNullOrWhiteSpace(currency))
            return (null, Error.Conflict("The original Payment Request has no valid currency."));
        var settlements = await db.PaymentSettlements.IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.ProjectId == projectId
                && item.ProjectPaymentRequestId == original.Id && !item.IsDeleted)
            .Select(item => new { item.Amount, item.Currency, item.FundingTransferId })
            .ToListAsync(cancellationToken);
        if (settlements.Any(item => item.Amount <= 0m || decimal.Round(item.Amount, 4) != item.Amount
            || !string.Equals(NormalizeCurrency(item.Currency), currency, StringComparison.Ordinal)))
            return (null, Error.Conflict("The Payment Request has an invalid or currency-mismatched settlement. Reconcile the financial history."));
        var settlementTransferIds = settlements.Where(item => item.FundingTransferId.HasValue)
            .Select(item => item.FundingTransferId!.Value).Distinct().ToArray();
        var settlementTransfers = settlementTransferIds.Length == 0 ? [] : await db.ProjectPaymentRequestFundingTransfers.IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.ProjectId == projectId
                && item.PaymentRequestId == original.Id && settlementTransferIds.Contains(item.Id) && !item.IsDeleted)
            .Select(item => item.Id).ToListAsync(cancellationToken);
        if (settlementTransferIds.Any(id => !settlementTransfers.Contains(id)))
            return (null, Error.Conflict("A settlement references a funding transfer belonging to another Payment Request. Reconcile the financial history."));
        var settled = settlements.Sum(item => item.Amount);

        var approvedReversals = await (from detail in AllProjectPaymentDetailsQuery(tenantId, projectId).AsNoTracking()
            join reversal in db.ApprovalRequests.IgnoreQueryFilters().AsNoTracking()
                on detail.ApprovalRequestId equals reversal.Id
            where detail.RequestKind == ProjectPaymentRequestKinds.ReversalRequest
                && detail.ReversesApprovalRequestId == original.Id
                && reversal.TenantId == tenantId && !reversal.IsDeleted
                && reversal.Type == ReversalRequestType && reversal.Status == PaymentRequestStatuses.Approved
            select new
            {
                reversal.Amount,
                reversal.Currency,
                reversal.EntityType,
                reversal.EntityId,
                detail.ProjectAnnualBudgetId,
                detail.AnnualBudgetFiscalYear,
                detail.AnnualBudgetReference,
                detail.FundingTransferId
            }).ToListAsync(cancellationToken);
        var approvedReversalTransfers = approvedReversals.Where(item => item.FundingTransferId.HasValue)
            .Select(item => item.FundingTransferId!.Value).Distinct().ToArray();
        var transfers = approvedReversalTransfers.Length == 0 ? [] : await db.ProjectPaymentRequestFundingTransfers.IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.ProjectId == projectId && approvedReversalTransfers.Contains(item.Id) && !item.IsDeleted)
            .ToDictionaryAsync(item => item.Id, cancellationToken);
        if (approvedReversals.Any(item => item.Amount is not > 0m
            || decimal.Round(item.Amount.Value, 4) != item.Amount.Value
            || !string.Equals(NormalizeCurrency(item.Currency), currency, StringComparison.Ordinal)
            || item.EntityType != "Project" || item.EntityId != projectId
            || item.ProjectAnnualBudgetId != originalDetail.ProjectAnnualBudgetId
            || item.AnnualBudgetFiscalYear != originalDetail.AnnualBudgetFiscalYear
            || !string.Equals(item.AnnualBudgetReference, originalDetail.AnnualBudgetReference, StringComparison.Ordinal)
            || (item.FundingTransferId.HasValue && (!transfers.TryGetValue(item.FundingTransferId.Value, out var transfer)
                || transfer.PaymentRequestId != original.Id || !string.Equals(NormalizeCurrency(transfer.Currency), currency, StringComparison.Ordinal)))))
            return (null, Error.Conflict("An approved reversal has invalid amount, currency, or Annual Budget provenance. Reconcile the financial history."));
        var released = approvedReversals.Sum(item => item.Amount!.Value);
        if (settled < 0m || released < 0m || settled + released > original.Amount.Value)
            return (null, Error.Conflict("Paid settlements and approved reversals exceed the original approved amount. Reconcile the financial history."));
        var active = original.Amount.Value - released;
        var payable = active - settled;
        return (new ReversalBalance(original.Amount.Value, settled, released, active, payable, payable), null);
    }

    private async Task<bool> HasOtherPendingReversalAsync(
        Guid tenantId, Guid projectId, Guid originalRequestId, Guid excludedReversalId, CancellationToken cancellationToken) =>
        await (from detail in AllProjectPaymentDetailsQuery(tenantId, projectId).AsNoTracking()
               join reversal in db.ApprovalRequests.IgnoreQueryFilters().AsNoTracking()
                   on detail.ApprovalRequestId equals reversal.Id
               where detail.RequestKind == ProjectPaymentRequestKinds.ReversalRequest
                   && detail.ReversesApprovalRequestId == originalRequestId
                   && detail.ApprovalRequestId != excludedReversalId
                   && reversal.TenantId == tenantId && !reversal.IsDeleted
                   && reversal.Type == ReversalRequestType && reversal.Status == PaymentRequestStatuses.Pending
               select reversal.Id).AnyAsync(cancellationToken);

    private async Task<List<(ApprovalRequest Request, ProjectPaymentRequestDetail Detail)>> FindReversalsAsync(
        Guid tenantId, Guid projectId, Guid originalRequestId, bool tracked, CancellationToken cancellationToken)
    {
        var detailsQuery = AllProjectPaymentDetailsQuery(tenantId, projectId)
            .Where(item => item.RequestKind == ProjectPaymentRequestKinds.ReversalRequest
                && item.ReversesApprovalRequestId == originalRequestId);
        if (!tracked) detailsQuery = detailsQuery.AsNoTracking();
        var details = await detailsQuery.ToListAsync(cancellationToken);
        if (details.Count == 0) return [];
        var ids = details.Select(item => item.ApprovalRequestId).ToArray();
        var requestsQuery = db.ApprovalRequests.IgnoreQueryFilters()
            .Where(item => item.TenantId == tenantId && !item.IsDeleted
                && item.Type == ReversalRequestType && item.EntityType == "Project" && item.EntityId == projectId
                && ids.Contains(item.Id));
        if (!tracked) requestsQuery = requestsQuery.AsNoTracking();
        var requests = await requestsQuery.ToDictionaryAsync(item => item.Id, cancellationToken);
        return details.Where(detail => requests.ContainsKey(detail.ApprovalRequestId))
            .Select(detail => (requests[detail.ApprovalRequestId], detail)).ToList();
    }

    private async Task<(ApprovalRequest Request, ProjectPaymentRequestDetail Detail)?> FindReversalByIdAsync(
        Guid tenantId, Guid projectId, Guid originalRequestId, Guid reversalId, bool tracked, CancellationToken cancellationToken)
    {
        var details = AllProjectPaymentDetailsQuery(tenantId, projectId)
            .Where(item => item.ApprovalRequestId == reversalId
                && item.RequestKind == ProjectPaymentRequestKinds.ReversalRequest
                && item.ReversesApprovalRequestId == originalRequestId);
        if (!tracked) details = details.AsNoTracking();
        var detail = await details.FirstOrDefaultAsync(cancellationToken);
        if (detail is null) return null;
        var requests = db.ApprovalRequests.IgnoreQueryFilters()
            .Where(item => item.Id == reversalId && item.TenantId == tenantId && !item.IsDeleted
                && item.Type == ReversalRequestType && item.EntityType == "Project" && item.EntityId == projectId);
        if (!tracked) requests = requests.AsNoTracking();
        var request = await requests.FirstOrDefaultAsync(cancellationToken);
        return request is null ? null : (request, detail);
    }

    private async Task<ProjectPaymentReversalDto> MapReversalAsync(
        Guid tenantId, Guid actorId, ApprovalRequest original, ProjectPaymentRequestDetail originalDetail,
        ApprovalRequest reversal, ProjectPaymentRequestDetail reversalDetail, ReversalBalance balance,
        CancellationToken cancellationToken)
    {
        var assignments = await GetAssignmentsAsync(tenantId, reversal.Id, tracked: false, cancellationToken);
        var actions = await db.ApprovalActions.IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.TenantId == tenantId && !item.IsDeleted && item.ApprovalRequestId == reversal.Id)
            .ToListAsync(cancellationToken);
        var names = await LoadUserNamesAsync(tenantId,
            assignments.Select(item => item.ApproverUserId).Append(reversal.RequestedBy ?? Guid.Empty), cancellationToken);
        var effective = await permissions.GetEffectivePermissionsAsync(actorId, tenantId, cancellationToken);
        var canRecoverAssignments = effective.Contains(ReversalApprovePermission)
            && effective.Contains(AssignmentRecoveryPermission)
            && management.Current?.EffectiveReadOnly != true;
        var eligible = effective.Contains(ReversalApprovePermission)
            ? await GetEligibleReversalApproverIdsAsync(tenantId, reversalDetail.ProjectId, cancellationToken)
            : new HashSet<Guid>();
        var requester = reversal.RequestedBy == actorId;
        var canApprove = !requester && effective.Contains(ReversalApprovePermission)
            && management.Current?.EffectiveReadOnly != true && eligible.Contains(actorId);
        var approvers = MapApprovers(reversal, reversalDetail, assignments, actions, names, actorId,
                canRecoverAssignments ? eligible : null, canRecoverAssignments)
            .Select(item => item with { CanDecide = canApprove && item.CanDecide }).ToArray();
        var project = await FindProjectQuery(reversalDetail.TenantId, reversalDetail.ProjectId)
            .AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        var hasPending = await HasOtherPendingReversalAsync(tenantId, reversalDetail.ProjectId,
            original.Id, reversal.Id, cancellationToken)
            || (reversal.Status == PaymentRequestStatuses.Pending);
        var budgetReady = await IsReversalBudgetCurrentAndApprovedAsync(
            tenantId, reversalDetail.ProjectId, project, originalDetail, cancellationToken);
        var canCreate = effective.Contains(ReversalCreatePermission)
            && management.Current?.EffectiveReadOnly != true;
        return new ProjectPaymentReversalDto(
            reversal.Id, ReversalReference(reversal), original.Id, Reference(original),
            reversalDetail.ProjectAnnualBudgetId, reversalDetail.AnnualBudgetFiscalYear, reversalDetail.AnnualBudgetReference,
            balance.OriginalAmount, balance.TotalSettled, balance.TotalReleased, balance.MaximumReversibleAmount,
            reversal.Amount ?? 0m, reversal.Currency ?? string.Empty, reversal.Description ?? string.Empty,
            reversal.Status, reversal.RequestedBy ?? Guid.Empty, names.GetValueOrDefault(reversal.RequestedBy ?? Guid.Empty) ?? string.Empty,
            reversal.RequestedAt, reversalDetail.SubmittedAt, reversalDetail.CompletedAt, reversalDetail.ApprovalMode,
            approvers, reversal.RowVersion,
            requester && reversal.Status == PaymentRequestStatuses.Draft && canCreate,
            requester && reversal.Status == PaymentRequestStatuses.Draft && canCreate
                && budgetReady && !hasPending && reversal.Amount is > 0m
                && reversal.Amount <= balance.MaximumReversibleAmount,
            canApprove && approvers.Any(item => item.CanDecide),
            requester && reversal.Status == PaymentRequestStatuses.Draft && canCreate);
    }

    private void AddReversalAudit(
        Project project, ApprovalRequest reversal, ApprovalRequest original, ProjectPaymentRequestDetail detail,
        Guid actorId, string? actorName, string action, string description, ReversalBalance? beforeBalance,
        ReversalBalance? afterBalance, object? beforeState, object? afterState, ReversalBalance balance,
        decimal amount)
    {
        var before = ToReversalAuditState(beforeState);
        var after = ToReversalAuditState(afterState);
        object BalanceValues(ReversalBalance? value) => value is null ? new { } : new
        {
            originalApprovedAmount = value.OriginalAmount,
            totalSettled = value.TotalSettled,
            previouslyReleasedAmount = value.TotalReleased,
            effectiveActiveAllocation = value.EffectiveActiveAllocation,
            remainingPayable = value.RemainingPayable
        };
        var oldValues = new
        {
            reversalReference = ReversalReference(reversal),
            originalPaymentRequestReference = Reference(original),
            annualBudgetId = detail.ProjectAnnualBudgetId,
            annualBudgetReference = detail.AnnualBudgetReference,
            fiscalYear = detail.AnnualBudgetFiscalYear,
            amount = before?.Amount ?? reversal.Amount,
            currency = reversal.Currency,
            status = before?.Status ?? reversal.Status,
            reason = before?.Reason ?? reversal.Description,
            approvalMode = before?.ApprovalMode,
            approvers = before?.Approvers,
            balance = BalanceValues(beforeBalance)
        };
        var newValues = new
        {
            reversalReference = ReversalReference(reversal),
            originalPaymentRequestReference = Reference(original),
            annualBudgetId = detail.ProjectAnnualBudgetId,
            annualBudgetReference = detail.AnnualBudgetReference,
            fiscalYear = detail.AnnualBudgetFiscalYear,
            amount,
            currency = reversal.Currency,
            status = after?.Status ?? reversal.Status,
            reason = after?.Reason ?? reversal.Description,
            approvalMode = after?.ApprovalMode ?? detail.ApprovalMode,
            approvers = after?.Approvers,
            decision = after?.Decision,
            comment = after?.Comment,
            approvedReleasedAmount = action == "BudgetAllocationReleased" || action == "ReversalApproved" ? amount : 0m,
            balance = BalanceValues(afterBalance ?? balance)
        };
        db.AuditEntries.Add(new AuditEntry
        {
            TenantId = project.TenantId,
            ProjectId = project.Id,
            EntityType = ReversalEntityType,
            EntityId = reversal.Id,
            Action = action,
            UserId = actorId,
            UserName = string.IsNullOrWhiteSpace(actorName) ? null : actorName.Trim(),
            Timestamp = DateTime.UtcNow,
            Description = description,
            Changes = JsonSerializer.Serialize(new
            {
                financialReference = new { from = ReversalReference(reversal), to = ReversalReference(reversal) },
                originalPaymentRequestReference = new { from = Reference(original), to = Reference(original) },
                amount = new { from = before?.Amount, to = reversal.Amount },
                currency = new { from = reversal.Currency, to = reversal.Currency },
                status = new { from = before?.Status, to = after?.Status ?? reversal.Status },
                approvedReleasedAmount = new { from = beforeBalance?.TotalReleased ?? 0m, to = afterBalance?.TotalReleased ?? balance.TotalReleased },
                totalSettled = new { from = beforeBalance?.TotalSettled ?? balance.TotalSettled, to = afterBalance?.TotalSettled ?? balance.TotalSettled },
                effectiveActiveAllocation = new { from = beforeBalance?.EffectiveActiveAllocation ?? balance.EffectiveActiveAllocation, to = afterBalance?.EffectiveActiveAllocation ?? balance.EffectiveActiveAllocation },
                remainingPayable = new { from = beforeBalance?.RemainingPayable ?? balance.RemainingPayable, to = afterBalance?.RemainingPayable ?? balance.RemainingPayable },
                annualBudgetReference = new { from = detail.AnnualBudgetReference, to = detail.AnnualBudgetReference },
                fiscalYear = new { from = detail.AnnualBudgetFiscalYear, to = detail.AnnualBudgetFiscalYear },
                decision = new { from = before?.Decision, to = after?.Decision },
                comment = new { from = before?.Comment, to = after?.Comment },
                approvalMode = new { from = before?.ApprovalMode, to = after?.ApprovalMode ?? detail.ApprovalMode },
                approvers = new { from = before?.Approvers, to = after?.Approvers },
                reason = new { from = before?.Reason, to = after?.Reason ?? reversal.Description }
            }),
            OriginalValues = JsonSerializer.Serialize(oldValues),
            NewValues = JsonSerializer.Serialize(newValues),
            ChangedFields = "reversalReference,originalPaymentRequestReference,annualBudgetReference,fiscalYear,amount,currency,status,reason,approvalMode,approvers,approvedReleasedAmount,totalSettled,effectiveActiveAllocation,remainingPayable,decision,comment",
            Source = "User",
            CreatedBy = actorId.ToString("D")
        });
    }

    private static string ReversalReference(ApprovalRequest request) =>
        string.IsNullOrWhiteSpace(request.EntityNumber) ? MakeReversalReference(request.Id) : request.EntityNumber;

    private static string MakeReversalReference(Guid id) => $"PPR-REV-{id:N}".ToUpperInvariant();

    private static string ReversalFingerprint(Guid requestId, decimal amount, string reason) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{requestId:D}|{amount:0.####}|{reason.Trim()}")));

    private IQueryable<ProjectPaymentRequestDetail> AllProjectPaymentDetailsQuery(Guid tenantId, Guid projectId) =>
        db.ProjectPaymentRequestDetails.IgnoreQueryFilters()
            .Where(item => item.TenantId == tenantId && item.ProjectId == projectId);

    private async Task<Error?> ValidateReversalReviewerIdentityAsync(
        Guid tenantId, Guid actorId, CancellationToken cancellationToken)
    {
        var current = management.Current;
        if (tenantId == Guid.Empty || actorId == Guid.Empty || current is null
            || current.TenantId != tenantId || current.UserId != actorId)
            return Error.NotFound("Reversal Request not found.");
        if (current.EffectiveReadOnly)
            return Error.Forbidden("An approval decision is unavailable in a read-only management context.");
        var effective = await permissions.GetEffectivePermissionsAsync(actorId, tenantId, cancellationToken);
        return effective.Contains(ReversalApprovePermission)
            ? null
            : Error.Forbidden("ProjectPaymentReversal.Approve is required to review a reversal.");
    }

    private async Task<HashSet<Guid>> GetEligibleReversalApproverIdsAsync(
        Guid tenantId, Guid projectId, CancellationToken cancellationToken)
    {
        var teamFinanceUsers = await ProjectTeamApproverEligibility.GetActiveLinkedUserIdsAsync(
            db, tenantId, projectId, cancellationToken);
        var eligible = new HashSet<Guid>();
        foreach (var userId in teamFinanceUsers)
        {
            var effective = await permissions.GetEffectivePermissionsAsync(userId, tenantId, cancellationToken);
            if (effective.Contains(ReversalApprovePermission)) eligible.Add(userId);
        }
        return eligible;
    }

    private static ReversalAuditState? ToReversalAuditState(object? state)
    {
        if (state is null) return null;
        var json = JsonSerializer.SerializeToElement(state);
        return new ReversalAuditState(
            ReadString(json, "status"), ReadDecimal(json, "amount"), ReadString(json, "reason"),
            ReadString(json, "decision"), ReadString(json, "comment"), ReadString(json, "approvalMode"),
            ReadString(json, "approvers"));
    }

    private sealed record ReversalAuditState(
        string? Status, decimal? Amount, string? Reason, string? Decision, string? Comment,
        string? ApprovalMode, string? Approvers);

    private sealed record ReversalBalance(
        decimal OriginalAmount,
        decimal TotalSettled,
        decimal TotalReleased,
        decimal EffectiveActiveAllocation,
        decimal RemainingPayable,
        decimal MaximumReversibleAmount);
}
