using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Approvals.Domain;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.CostManagement.Application;
using NEXUS.Modules.CostManagement.Application.DTOs;
using NEXUS.Modules.CostManagement.Domain;
using NEXUS.Persistence;
using NEXUS.SharedKernel;

namespace NEXUS.Api.Finance;

public sealed partial class ProjectPaymentRequestWorkflow
{
    private const string RefundPermission = "PaymentSettlement.Record";
    private const string SettlementAdjustmentApprovalType = "ProjectSettlementAdjustment";

    public async Task<Result<IReadOnlyList<PaymentRequestEligibleApproverDto>>> GetEligibleSettlementAdjustmentApproversAsync(
        Guid tenantId, Guid projectId, Guid actorId, CancellationToken cancellationToken)
    {
        var access = await ValidateServiceAccessAsync(tenantId, projectId, actorId, requireWrite: true,
            cancellationToken, actionPermission: RefundPermission);
        if (access.Error is not null)
            return Result<IReadOnlyList<PaymentRequestEligibleApproverDto>>.Failure(access.Error);
        var actorError = await ValidateActiveActorAsync(tenantId, actorId, cancellationToken);
        if (actorError is not null)
            return Result<IReadOnlyList<PaymentRequestEligibleApproverDto>>.Failure(actorError);
        var project = await FindProjectQuery(tenantId, projectId).FirstOrDefaultAsync(cancellationToken);
        if (project is null)
            return Result<IReadOnlyList<PaymentRequestEligibleApproverDto>>.Failure(Error.NotFound("Project not found."));
        if (!await IsCurrentProjectManagerAsync(tenantId, project, actorId, cancellationToken))
            return Result<IReadOnlyList<PaymentRequestEligibleApproverDto>>.Failure(Error.Forbidden(
                "Only the current Project Manager may request a settlement adjustment."));
        var eligible = await GetEligibleReversalApproverIdsAsync(tenantId, projectId, cancellationToken);
        var names = await LoadUserNamesAsync(tenantId, eligible, cancellationToken);
        return Result<IReadOnlyList<PaymentRequestEligibleApproverDto>>.Success(eligible
            .Where(id => id != actorId)
            .OrderBy(id => names.GetValueOrDefault(id), StringComparer.OrdinalIgnoreCase)
            .Select(id => new PaymentRequestEligibleApproverDto(id, names.GetValueOrDefault(id) ?? string.Empty))
            .ToArray());
    }

    public async Task<Result<IReadOnlyList<SettlementAdjustmentDto>>> GetSettlementAdjustmentsAsync(
        Guid tenantId, Guid projectId, Guid settlementId, CancellationToken cancellationToken)
    {
        var access = await ValidateServiceAccessAsync(tenantId, projectId,
            management.Current?.UserId ?? Guid.Empty, requireWrite: false, cancellationToken,
            actionPermission: SettlementViewPermission);
        if (access.Error is not null)
            return Result<IReadOnlyList<SettlementAdjustmentDto>>.Failure(access.Error);
        var settlementExists = await db.PaymentSettlements.IgnoreQueryFilters().AsNoTracking()
            .AnyAsync(item => item.TenantId == tenantId && item.ProjectId == projectId
                && item.Id == settlementId && !item.IsDeleted, cancellationToken);
        if (!settlementExists)
            return Result<IReadOnlyList<SettlementAdjustmentDto>>.Failure(Error.NotFound("Settlement not found."));
        var records = await db.PaymentSettlementAdjustments.IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.ProjectId == projectId
                && item.OriginalSettlementId == settlementId && !item.IsDeleted)
            .OrderByDescending(item => item.EffectiveDate)
            .ThenByDescending(item => item.RecordedAt)
            .ToListAsync(cancellationToken);
        var actorId = management.Current?.UserId ?? Guid.Empty;
        var effective = actorId == Guid.Empty
            ? new HashSet<string>(StringComparer.Ordinal)
            : await permissions.GetEffectivePermissionsAsync(actorId, tenantId, cancellationToken);
        IReadOnlyCollection<Guid> eligibleApprovers = effective.Contains("ProjectPaymentReversal.Approve")
            ? await GetEligibleReversalApproverIdsAsync(tenantId, projectId, cancellationToken)
            : Array.Empty<Guid>();
        var approvalIds = records.Where(item => item.Status == PaymentSettlementAdjustment.PendingApproval)
            .Select(item => item.ApprovalRequestId).Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToArray();
        var requesters = approvalIds.Length == 0
            ? new Dictionary<Guid, Guid?>()
            : await db.ApprovalRequests.IgnoreQueryFilters().AsNoTracking()
                .Where(item => item.TenantId == tenantId && approvalIds.Contains(item.Id) && !item.IsDeleted)
                .Select(item => new { item.Id, item.RequestedBy })
                .ToDictionaryAsync(item => item.Id, item => (Guid?)item.RequestedBy, cancellationToken);
        var project = await FindProjectQuery(tenantId, projectId).FirstOrDefaultAsync(cancellationToken);
        var canConfirmAsManager = project is not null
            && management.Current?.EffectiveReadOnly != true
            && effective.Contains(RefundPermission)
            && await IsCurrentProjectManagerAsync(tenantId, project, actorId, cancellationToken);
        var items = records.Select(item => MapAdjustment(item,
            canDecide: item.Status == PaymentSettlementAdjustment.PendingApproval
                && item.AssignedApproverId == actorId && eligibleApprovers.Contains(actorId)
                && item.ApprovalRequestId.HasValue
                && requesters.TryGetValue(item.ApprovalRequestId.Value, out var requesterId)
                && requesterId != actorId,
            canConfirmExecution: item.Status == PaymentSettlementAdjustment.Approved
                && canConfirmAsManager)).ToArray();
        return Result<IReadOnlyList<SettlementAdjustmentDto>>.Success(items);
    }

    public Task<Result<SettlementAdjustmentDto>> RecordRefundAsync(
        Guid tenantId, Guid projectId, Guid actorId, string? actorName,
        RecordSettlementAdjustmentDto dto, CancellationToken cancellationToken) =>
        CreateSettlementAdjustmentRequestAsync(tenantId, projectId, actorId, actorName, dto,
            PaymentSettlementAdjustment.Refund, cancellationToken);

    public Task<Result<SettlementAdjustmentDto>> RecordSettlementCorrectionAsync(
        Guid tenantId, Guid projectId, Guid actorId, string? actorName,
        RecordSettlementAdjustmentDto dto, CancellationToken cancellationToken) =>
        CreateSettlementAdjustmentRequestAsync(tenantId, projectId, actorId, actorName, dto,
            PaymentSettlementAdjustment.Correction, cancellationToken);

    public async Task<Result<SettlementAdjustmentDto>> DecideSettlementAdjustmentAsync(
        Guid tenantId, Guid projectId, Guid settlementId, Guid adjustmentId, Guid actorId,
        string? actorName, DecideSettlementAdjustmentDto dto, CancellationToken cancellationToken)
    {
        var identityError = await ValidateReversalReviewerIdentityAsync(tenantId, actorId, cancellationToken);
        if (identityError is not null)
            return Result<SettlementAdjustmentDto>.Failure(identityError);
        if (dto.Decision is not (PaymentRequestDecisions.Approved or PaymentRequestDecisions.Rejected))
            return Result<SettlementAdjustmentDto>.Failure(Error.Validation("Decision must be Approved or Rejected."));
        if (dto.Decision == PaymentRequestDecisions.Rejected && string.IsNullOrWhiteSpace(dto.Comment))
            return Result<SettlementAdjustmentDto>.Failure(Error.Validation("A rejection comment is required."));

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var project = await FindProjectForUpdateAsync(tenantId, projectId, cancellationToken);
        if (project is null)
            return Result<SettlementAdjustmentDto>.Failure(Error.NotFound("Project not found."));
        var actorError = await ValidateActiveActorAsync(tenantId, actorId, cancellationToken);
        if (actorError is not null)
            return Result<SettlementAdjustmentDto>.Failure(actorError);

        var adjustment = await db.PaymentSettlementAdjustments.IgnoreQueryFilters()
            .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.ProjectId == projectId
                && item.Id == adjustmentId && item.OriginalSettlementId == settlementId && !item.IsDeleted,
                cancellationToken);
        if (adjustment is null || !adjustment.ApprovalRequestId.HasValue)
            return Result<SettlementAdjustmentDto>.Failure(Error.NotFound("Settlement adjustment request not found."));

        var approval = await db.ApprovalRequests.IgnoreQueryFilters()
            .Include(item => item.Actions)
            .FirstOrDefaultAsync(item => item.TenantId == tenantId
                && item.Id == adjustment.ApprovalRequestId.Value && !item.IsDeleted
                && item.Type == SettlementAdjustmentApprovalType, cancellationToken);
        if (approval is null)
            return Result<SettlementAdjustmentDto>.Failure(Error.Conflict("The settlement adjustment approval record is missing."));
        if (adjustment.Status != PaymentSettlementAdjustment.PendingApproval)
        {
            var priorAction = approval.Actions.FirstOrDefault(item => item.ApproverId == actorId);
            var sameDecision = priorAction is not null
                && string.Equals(priorAction.Action,
                    dto.Decision == PaymentRequestDecisions.Approved ? "Approve" : "Reject",
                    StringComparison.OrdinalIgnoreCase)
                && string.Equals(priorAction.Comments?.Trim(), dto.Comment?.Trim(), StringComparison.Ordinal);
            if (sameDecision)
            {
                await transaction.CommitAsync(cancellationToken);
                return Result<SettlementAdjustmentDto>.Success(MapAdjustment(adjustment));
            }
            return Result<SettlementAdjustmentDto>.Failure(Error.Conflict("The settlement adjustment has already been decided."));
        }
        if (!MatchesVersion(adjustment.RowVersion, dto.RowVersion))
            return Result<SettlementAdjustmentDto>.Failure(Error.Conflict("The settlement adjustment changed. Refresh before deciding."));
        if (approval.Status != ApprovalRequest.StatusSubmitted || approval.CurrentApproverId != actorId
            || adjustment.AssignedApproverId != actorId)
            return Result<SettlementAdjustmentDto>.Failure(Error.Forbidden("Only the assigned settlement adjustment approver may decide."));
        if (approval.RequestedBy == actorId)
            return Result<SettlementAdjustmentDto>.Failure(Error.Forbidden("A settlement adjustment requester cannot approve their own request."));
        if (!(await GetEligibleReversalApproverIdsAsync(tenantId, projectId, cancellationToken)).Contains(actorId))
            return Result<SettlementAdjustmentDto>.Failure(Error.Forbidden(
                "The assigned approver must remain an active eligible Project Team member with ProjectPaymentReversal.Approve."));

        var assignment = await db.PaymentRequestApproverAssignments.IgnoreQueryFilters()
            .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.ProjectId == projectId
                && item.ApprovalRequestId == approval.Id && item.ApproverUserId == actorId
                && !item.IsDeleted, cancellationToken);
        if (assignment is null || assignment.State != PaymentRequestAssignmentStates.Pending)
            return Result<SettlementAdjustmentDto>.Failure(Error.Forbidden("No pending approval assignment exists for this actor."));

        var previousStatus = adjustment.Status;
        var decidedAt = DateTime.UtcNow;
        if (dto.Decision == PaymentRequestDecisions.Approved)
        {
            approval.Approve(actorId, dto.Comment?.Trim(), actorId.ToString("D"));
            adjustment.Status = PaymentSettlementAdjustment.Approved;
            adjustment.ApprovedBy = actorId;
            adjustment.ApprovedAtUtc = decidedAt;
            assignment.State = PaymentRequestAssignmentStates.Approved;
        }
        else
        {
            approval.Reject(actorId, dto.Comment!.Trim(), actorId.ToString("D"));
            adjustment.Status = PaymentSettlementAdjustment.Rejected;
            assignment.State = PaymentRequestAssignmentStates.Rejected;
        }
        if (approval.Actions.LastOrDefault() is { } action)
            action.ApproverAssignmentId = assignment.Id;
        adjustment.UpdatedBy = actorId.ToString("D");
        adjustment.UpdatedAt = decidedAt;
        adjustment.RowVersion = Guid.NewGuid().ToByteArray();
        assignment.UpdatedBy = actorId.ToString("D");
        assignment.UpdatedAt = decidedAt;
        AddAdjustmentAudit(tenantId, projectId, adjustment, actorId, actorName,
            dto.Decision == PaymentRequestDecisions.Approved ? "Approved" : "Rejected",
            previousStatus, adjustment.Status,
            new { approvalRequestId = approval.Id, approverId = actorId, comment = dto.Comment?.Trim(), decidedAt });

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<SettlementAdjustmentDto>.Failure(Error.Conflict("The settlement adjustment changed during approval."));
        }
        catch (DbUpdateException)
        {
            return Result<SettlementAdjustmentDto>.Failure(Error.Conflict("The settlement adjustment decision conflicts with another operation."));
        }
        return Result<SettlementAdjustmentDto>.Success(MapAdjustment(adjustment));
    }

    public async Task<Result<SettlementAdjustmentDto>> ConfirmSettlementAdjustmentExecutionAsync(
        Guid tenantId, Guid projectId, Guid settlementId, Guid adjustmentId, Guid actorId,
        string? actorName, ConfirmSettlementAdjustmentExecutionDto dto, CancellationToken cancellationToken)
    {
        var access = await ValidateServiceAccessAsync(tenantId, projectId, actorId, requireWrite: true,
            cancellationToken, actionPermission: RefundPermission);
        if (access.Error is not null)
            return Result<SettlementAdjustmentDto>.Failure(access.Error);
        if (dto.IdempotencyKey == Guid.Empty || dto.ExecutionDate == default)
            return Result<SettlementAdjustmentDto>.Failure(Error.Validation("An execution idempotency key and date are required."));
        if (dto.ExecutionDate > DateOnly.FromDateTime(DateTime.UtcNow))
            return Result<SettlementAdjustmentDto>.Failure(Error.Validation("The execution date cannot be in the future."));

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var project = await FindProjectForUpdateAsync(tenantId, projectId, cancellationToken);
        if (project is null)
            return Result<SettlementAdjustmentDto>.Failure(Error.NotFound("Project not found."));
        var actorError = await ValidateActiveActorAsync(tenantId, actorId, cancellationToken);
        if (actorError is not null)
            return Result<SettlementAdjustmentDto>.Failure(actorError);
        if (!await IsCurrentProjectManagerAsync(tenantId, project, actorId, cancellationToken))
            return Result<SettlementAdjustmentDto>.Failure(Error.Forbidden(
                "Only the current Project Manager may confirm refund or correction execution."));

        var adjustment = await db.PaymentSettlementAdjustments.IgnoreQueryFilters()
            .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.ProjectId == projectId
                && item.Id == adjustmentId && item.OriginalSettlementId == settlementId && !item.IsDeleted,
                cancellationToken);
        if (adjustment is null)
            return Result<SettlementAdjustmentDto>.Failure(Error.NotFound("Settlement adjustment not found."));
        if (adjustment.Status == PaymentSettlementAdjustment.Executed)
        {
            if (adjustment.ExecutionIdempotencyKey == dto.IdempotencyKey)
            {
                await transaction.CommitAsync(cancellationToken);
                return Result<SettlementAdjustmentDto>.Success(MapAdjustment(adjustment));
            }
            return Result<SettlementAdjustmentDto>.Failure(Error.Conflict("The settlement adjustment execution was already confirmed."));
        }
        if (adjustment.Status != PaymentSettlementAdjustment.Approved)
            return Result<SettlementAdjustmentDto>.Failure(Error.Conflict("Only an approved settlement adjustment can be confirmed as executed."));
        if (adjustment.ApprovalRequestId is null)
            return Result<SettlementAdjustmentDto>.Failure(Error.Conflict("The settlement adjustment has no approved authorization record."));

        var duplicateExecutionKey = await db.PaymentSettlementAdjustments.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(item => item.TenantId == tenantId
                && item.ExecutionIdempotencyKey == dto.IdempotencyKey && !item.IsDeleted, cancellationToken);
        if (duplicateExecutionKey is not null)
            return Result<SettlementAdjustmentDto>.Failure(Error.Conflict("This execution idempotency key was already used."));

        var approvalApproved = await db.ApprovalRequests.IgnoreQueryFilters().AsNoTracking()
            .AnyAsync(item => item.TenantId == tenantId && item.Id == adjustment.ApprovalRequestId.Value
                && item.Type == SettlementAdjustmentApprovalType
                && item.Status == ApprovalRequest.StatusApproved && !item.IsDeleted, cancellationToken);
        if (!approvalApproved)
            return Result<SettlementAdjustmentDto>.Failure(Error.Conflict("The approval record is not approved."));

        var settlementExists = await db.PaymentSettlements.IgnoreQueryFilters().AsNoTracking()
            .AnyAsync(item => item.TenantId == tenantId && item.ProjectId == projectId
                && item.Id == settlementId && !item.IsDeleted, cancellationToken);
        if (!settlementExists)
            return Result<SettlementAdjustmentDto>.Failure(Error.NotFound("Original settlement not found."));

        var beforeStatus = adjustment.Status;
        var now = DateTime.UtcNow;
        adjustment.Status = PaymentSettlementAdjustment.Executed;
        adjustment.ExecutionIdempotencyKey = dto.IdempotencyKey;
        adjustment.ExecutionDate = dto.ExecutionDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        adjustment.ExecutionConfirmedBy = actorId;
        adjustment.ExecutionConfirmedAtUtc = now;
        adjustment.UpdatedBy = actorId.ToString("D");
        adjustment.UpdatedAt = now;
        adjustment.RowVersion = Guid.NewGuid().ToByteArray();
        AddAdjustmentAudit(tenantId, projectId, adjustment, actorId, actorName,
            "ExecutionConfirmed", beforeStatus, adjustment.Status,
            new
            {
                approvalRequestId = adjustment.ApprovalRequestId,
                executionIdempotencyKey = dto.IdempotencyKey,
                executionDate = dto.ExecutionDate,
                confirmedBy = actorId,
                confirmedAtUtc = now,
                lifetimeReservationReleased = false
            });

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<SettlementAdjustmentDto>.Failure(Error.Conflict("The settlement adjustment changed during execution confirmation."));
        }
        catch (DbUpdateException)
        {
            return Result<SettlementAdjustmentDto>.Failure(Error.Conflict("The execution idempotency key was used concurrently."));
        }
        return Result<SettlementAdjustmentDto>.Success(MapAdjustment(adjustment));
    }

    private async Task<Result<SettlementAdjustmentDto>> CreateSettlementAdjustmentRequestAsync(
        Guid tenantId, Guid projectId, Guid actorId, string? actorName,
        RecordSettlementAdjustmentDto dto, string adjustmentType, CancellationToken cancellationToken)
    {
        var access = await ValidateServiceAccessAsync(tenantId, projectId, actorId, requireWrite: true,
            cancellationToken, actionPermission: RefundPermission);
        if (access.Error is not null)
            return Result<SettlementAdjustmentDto>.Failure(access.Error);
        if (dto.IdempotencyKey == Guid.Empty)
            return Result<SettlementAdjustmentDto>.Failure(Error.Validation("An idempotency key is required."));
        if (dto.AssignedApproverId == Guid.Empty)
            return Result<SettlementAdjustmentDto>.Failure(Error.Validation("An eligible assigned approver is required."));
        if (dto.Amount <= 0m || decimal.Round(dto.Amount, 4) != dto.Amount)
            return Result<SettlementAdjustmentDto>.Failure(Error.Validation("Adjustment amount must be positive and use no more than four decimal places."));
        if (dto.EffectiveDate == default)
            return Result<SettlementAdjustmentDto>.Failure(Error.Validation("Effective date is required."));
        var reason = dto.Reason?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 2000)
            return Result<SettlementAdjustmentDto>.Failure(Error.Validation("A reason of up to 2000 characters is required."));

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var project = await FindProjectForUpdateAsync(tenantId, projectId, cancellationToken);
        if (project is null)
            return Result<SettlementAdjustmentDto>.Failure(Error.NotFound("Project not found."));
        var actorError = await ValidateActiveActorAsync(tenantId, actorId, cancellationToken);
        if (actorError is not null)
            return Result<SettlementAdjustmentDto>.Failure(actorError);
        if (!await IsCurrentProjectManagerAsync(tenantId, project, actorId, cancellationToken))
            return Result<SettlementAdjustmentDto>.Failure(Error.Forbidden(
                "Only the current Project Manager may request a settlement adjustment."));

        var eligibleApprovers = await GetEligibleReversalApproverIdsAsync(tenantId, projectId, cancellationToken);
        if (dto.AssignedApproverId == actorId)
            return Result<SettlementAdjustmentDto>.Failure(Error.Forbidden("The requester cannot approve their own settlement adjustment."));
        if (!eligibleApprovers.Contains(dto.AssignedApproverId))
            return Result<SettlementAdjustmentDto>.Failure(Error.Forbidden(
                "The selected approver must be an active eligible Project Team member with ProjectPaymentReversal.Approve."));

        var existingKey = await db.PaymentSettlementAdjustments.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(item => item.TenantId == tenantId
                && item.IdempotencyKey == dto.IdempotencyKey, cancellationToken);
        if (existingKey is not null)
        {
            var sameCommand = existingKey.ProjectId == projectId
                && existingKey.OriginalSettlementId == dto.OriginalSettlementId
                && existingKey.Amount == dto.Amount
                && existingKey.AdjustmentType == adjustmentType
                && existingKey.Reason == reason
                && DateOnly.FromDateTime(existingKey.EffectiveDate) == dto.EffectiveDate
                && (existingKey.AssignedApproverId == dto.AssignedApproverId
                    || (existingKey.ApprovalRequestId is null
                        && existingKey.Status == PaymentSettlementAdjustment.Executed));
            if (sameCommand)
            {
                await transaction.CommitAsync(cancellationToken);
                return Result<SettlementAdjustmentDto>.Success(MapAdjustment(existingKey));
            }
            return Result<SettlementAdjustmentDto>.Failure(Error.Conflict(
                "This idempotency key was already used for a different adjustment command."));
        }

        var settlement = await db.PaymentSettlements.IgnoreQueryFilters()
            .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.ProjectId == projectId
                && item.Id == dto.OriginalSettlementId && !item.IsDeleted, cancellationToken);
        if (settlement is null)
            return Result<SettlementAdjustmentDto>.Failure(Error.NotFound("Original settlement not found."));
        var currency = string.IsNullOrWhiteSpace(settlement.Currency)
            ? string.Empty
            : ActualPaidSettlementCalculator.NormalizeCurrency(settlement.Currency);
        if (currency.Length != 3)
            return Result<SettlementAdjustmentDto>.Failure(Error.Conflict("The original settlement has no valid currency."));

        if (adjustmentType == PaymentSettlementAdjustment.Refund)
        {
            var alreadyReservedOrExecuted = await db.PaymentSettlementAdjustments.IgnoreQueryFilters().AsNoTracking()
                .Where(item => item.TenantId == tenantId && item.ProjectId == projectId
                    && item.OriginalSettlementId == settlement.Id
                    && item.AdjustmentType == PaymentSettlementAdjustment.Refund
                    && item.Status != PaymentSettlementAdjustment.Rejected && !item.IsDeleted)
                .SumAsync(item => (decimal?)item.Amount, cancellationToken) ?? 0m;
            if (alreadyReservedOrExecuted + dto.Amount > settlement.Amount)
                return Result<SettlementAdjustmentDto>.Failure(Error.Conflict(
                    "The requested refund exceeds the original settlement after pending, approved, and executed refunds."));
        }

        var now = DateTime.UtcNow;
        var adjustment = new PaymentSettlementAdjustment
        {
            TenantId = tenantId,
            CreatedBy = actorId.ToString("D"),
            ProjectId = projectId,
            OriginalSettlementId = settlement.Id,
            PaymentObligationId = settlement.PaymentObligationId,
            ProjectPaymentRequestId = settlement.ProjectPaymentRequestId,
            PlannedPaymentInstallmentId = settlement.PlannedPaymentInstallmentId,
            AdjustmentType = adjustmentType,
            Amount = dto.Amount,
            Currency = currency,
            Reason = reason,
            EffectiveDate = dto.EffectiveDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            RecordedAt = now,
            IdempotencyKey = dto.IdempotencyKey,
            AssignedApproverId = dto.AssignedApproverId,
            Status = PaymentSettlementAdjustment.PendingApproval,
            RowVersion = Guid.NewGuid().ToByteArray(),
            CreatedAt = now
        };
        var approval = new ApprovalRequest
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CreatedBy = actorId.ToString("D"),
            Title = $"{adjustmentType} request for settlement {settlement.PaymentReference}",
            Description = reason,
            EntityType = "PaymentSettlementAdjustment",
            EntityId = adjustment.Id,
            EntityNumber = adjustment.Id.ToString("N"),
            Type = SettlementAdjustmentApprovalType,
            Status = ApprovalRequest.StatusSubmitted,
            Priority = "Medium",
            Amount = dto.Amount,
            Currency = currency,
            RequestedBy = actorId,
            RequestedAt = now,
            CurrentApproverId = dto.AssignedApproverId,
            ApprovalLevel = 1
        };
        adjustment.ApprovalRequestId = approval.Id;
        var assignment = new PaymentRequestApproverAssignment
        {
            TenantId = tenantId,
            ProjectId = projectId,
            ApprovalRequestId = approval.Id,
            ApproverUserId = dto.AssignedApproverId,
            Position = 1,
            Requirement = PaymentRequestAssignmentRequirements.Mandatory,
            State = PaymentRequestAssignmentStates.Pending,
            CreatedBy = actorId.ToString("D"),
            CreatedAt = now
        };
        db.ApprovalRequests.Add(approval);
        db.PaymentRequestApproverAssignments.Add(assignment);
        db.PaymentSettlementAdjustments.Add(adjustment);
        AddAdjustmentAudit(tenantId, projectId, adjustment, actorId, actorName,
            "Requested", null, adjustment.Status,
            new
            {
                approvalRequestId = approval.Id,
                assignedApproverId = dto.AssignedApproverId,
                settlementId = settlement.Id,
                settlementReference = settlement.PaymentReference,
                adjustmentType,
                amount = adjustment.Amount,
                currency,
                reason,
                effectiveDate = dto.EffectiveDate
            });

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<SettlementAdjustmentDto>.Failure(Error.Conflict("The original settlement changed. Refresh and retry."));
        }
        catch (DbUpdateException)
        {
            return Result<SettlementAdjustmentDto>.Failure(Error.Conflict("The adjustment idempotency key was used by another request."));
        }
        return Result<SettlementAdjustmentDto>.Success(MapAdjustment(adjustment));
    }

    private void AddAdjustmentAudit(
        Guid tenantId, Guid projectId, PaymentSettlementAdjustment adjustment,
        Guid actorId, string? actorName, string action, string? previousStatus,
        string currentStatus, object? details)
    {
        var now = DateTime.UtcNow;
        var previous = new { status = previousStatus };
        var current = new
        {
            status = currentStatus,
            adjustmentId = adjustment.Id,
            approvalRequestId = adjustment.ApprovalRequestId,
            originalSettlementId = adjustment.OriginalSettlementId,
            adjustmentType = adjustment.AdjustmentType,
            amount = adjustment.Amount,
            currency = adjustment.Currency,
            details
        };
        db.AuditEntries.Add(new AuditEntry
        {
            TenantId = tenantId,
            ProjectId = projectId,
            EntityType = "PaymentSettlementAdjustment",
            EntityId = adjustment.Id,
            Action = $"SettlementAdjustment{action}",
            UserId = actorId,
            UserName = string.IsNullOrWhiteSpace(actorName) ? null : actorName.Trim(),
            Timestamp = now,
            Description = $"Settlement {adjustment.AdjustmentType} {action.ToLowerInvariant()}: {adjustment.Amount:0.####} {adjustment.Currency}.",
            Changes = JsonSerializer.Serialize(new { previous, current }),
            OriginalValues = JsonSerializer.Serialize(previous),
            NewValues = JsonSerializer.Serialize(current),
            ChangedFields = "status,approvalRequestId,adjustmentType,amount,currency",
            Source = "User",
            CreatedBy = actorId.ToString("D")
        });
    }

    private static SettlementAdjustmentDto MapAdjustment(PaymentSettlementAdjustment item,
        bool canDecide = false, bool canConfirmExecution = false) =>
        new(item.Id, item.OriginalSettlementId, item.AdjustmentType, item.Amount, item.Currency,
            item.Reason, item.EffectiveDate, item.RecordedAt, item.Status, item.ApprovalRequestId,
            item.AssignedApproverId, item.ApprovedBy, item.ApprovedAtUtc,
            item.ExecutionConfirmedBy, item.ExecutionConfirmedAtUtc, item.ExecutionDate, item.RowVersion,
            canDecide, canConfirmExecution);
}
