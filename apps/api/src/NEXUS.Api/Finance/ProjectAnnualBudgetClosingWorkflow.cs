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
using NEXUS.Modules.Identity.Application;
using NEXUS.Modules.Organization.Application.Settings;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.Persistence;
using NEXUS.SharedKernel;

namespace NEXUS.Api.Finance;

/// <summary>Project-scoped fiscal-year closing and commitment funding transfer using the shared approval and audit stores.</summary>
public sealed partial class ProjectPaymentRequestWorkflow
{
    private const string ClosingViewPermission = "ProjectAnnualBudgetClosing.View";
    private const string ClosingPreparePermission = "ProjectAnnualBudget.Close";
    private const string ClosingApprovePermission = "ProjectAnnualBudget.CloseApprove";
    private const string CarryForwardPermission = "ProjectAnnualBudget.CarryForward";
    private const string ClosingApprovalType = "ProjectAnnualBudgetClosing";

    public async Task<Result<ProjectAnnualBudgetClosingPageDto>> GetClosingsAsync(
        Guid tenantId, Guid projectId, Guid actorId, CancellationToken cancellationToken)
    {
        var access = await ValidateServiceAccessAsync(tenantId, projectId, actorId, requireWrite: false, cancellationToken,
            screenViewPermission: ClosingViewPermission);
        if (access.Error is not null) return Result<ProjectAnnualBudgetClosingPageDto>.Failure(access.Error);
        var effective = await permissions.GetEffectivePermissionsAsync(actorId, tenantId, cancellationToken);
        var rows = await db.ProjectAnnualBudgetClosings.IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.ProjectId == projectId && !item.IsDeleted)
            .OrderByDescending(item => item.FiscalYear).ThenByDescending(item => item.CreatedAt)
            .ToListAsync(cancellationToken);
        var approvalIds = rows.Where(item => item.ApprovalRequestId.HasValue)
            .Select(item => item.ApprovalRequestId!.Value).Distinct().ToArray();
        var assignments = approvalIds.Length == 0 ? [] : await db.PaymentRequestApproverAssignments.IgnoreQueryFilters()
            .AsNoTracking().Where(item => item.TenantId == tenantId && !item.IsDeleted && approvalIds.Contains(item.ApprovalRequestId))
            .OrderBy(item => item.Position).ThenBy(item => item.CreatedAt).ToListAsync(cancellationToken);
        var actions = approvalIds.Length == 0 ? [] : await db.ApprovalActions.IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.TenantId == tenantId && !item.IsDeleted && approvalIds.Contains(item.ApprovalRequestId))
            .ToListAsync(cancellationToken);
        var actorIds = assignments.Select(item => item.ApproverUserId).Concat(rows.Select(item => item.PreparedBy ?? Guid.Empty))
            .Concat(rows.Select(item => item.CompletedBy ?? Guid.Empty)).Where(id => id != Guid.Empty).Distinct().ToArray();
        var names = await LoadUserNamesAsync(tenantId, actorIds, cancellationToken);
        var eligible = effective.Contains(ClosingApprovePermission)
            ? await GetEligibleClosingApproverIdsAsync(tenantId, projectId, cancellationToken) : new HashSet<Guid>();
        var canPrepare = effective.Contains(ClosingPreparePermission)
            && management.Current?.EffectiveReadOnly != true;
        var items = rows.Select(row => MapClosing(row,
            row.ApprovalRequestId.HasValue ? assignments.Where(item => item.ApprovalRequestId == row.ApprovalRequestId).ToArray() : [],
            row.ApprovalRequestId.HasValue ? actions.Where(item => item.ApprovalRequestId == row.ApprovalRequestId).ToArray() : [],
            names, actorId, eligible, canPrepare, effective.Contains(CarryForwardPermission))).ToArray();
        var current = await tenantFiscalYear.ResolveForTimestampAsync(tenantId, DateTimeOffset.UtcNow, cancellationToken);
        return Result<ProjectAnnualBudgetClosingPageDto>.Success(new ProjectAnnualBudgetClosingPageDto(current.FiscalYear, items));
    }

    public async Task<Result<IReadOnlyList<PaymentRequestEligibleApproverDto>>> GetEligibleClosingApproversAsync(
        Guid tenantId, Guid projectId, Guid actorId, CancellationToken cancellationToken)
    {
        var access = await ValidateServiceAccessAsync(tenantId, projectId, actorId, requireWrite: true, cancellationToken,
            actionPermission: ClosingPreparePermission);
        if (access.Error is not null) return Result<IReadOnlyList<PaymentRequestEligibleApproverDto>>.Failure(access.Error);
        if (!await HasClosingPreparePermissionAsync(actorId, tenantId, cancellationToken))
            return Result<IReadOnlyList<PaymentRequestEligibleApproverDto>>.Failure(Error.Forbidden("ProjectAnnualBudget.Close is required to select closing approvers."));
        var eligible = await GetEligibleClosingApproverIdsAsync(tenantId, projectId, cancellationToken);
        var names = await LoadUserNamesAsync(tenantId, eligible, cancellationToken);
        return Result<IReadOnlyList<PaymentRequestEligibleApproverDto>>.Success(eligible
            .OrderBy(id => names.GetValueOrDefault(id), StringComparer.OrdinalIgnoreCase)
            .Select(id => new PaymentRequestEligibleApproverDto(id, names.GetValueOrDefault(id) ?? string.Empty)).ToArray());
    }

    public async Task<Result<ProjectAnnualBudgetClosingDto>> PrepareClosingAsync(
        Guid tenantId, Guid projectId, Guid actorId, string? actorName,
        PrepareProjectAnnualBudgetClosingDto dto, CancellationToken cancellationToken)
    {
        var accessError = await ValidateClosingAccessAsync(tenantId, projectId, actorId, ClosingPreparePermission, cancellationToken);
        if (accessError is not null) return Result<ProjectAnnualBudgetClosingDto>.Failure(accessError);
        if (dto.IdempotencyKey == Guid.Empty || dto.FiscalYear is < 1900 or > 2200
            || dto.ProposedFreeCarryForward < 0m || dto.NextFiscalYearNewFunding < 0m
            || !HasMoneyScale(dto.ProposedFreeCarryForward) || !HasMoneyScale(dto.NextFiscalYearNewFunding)
            || !HasMoneyScale(dto.NextFiscalYearAdjustments))
            return Result<ProjectAnnualBudgetClosingDto>.Failure(Error.Validation("Fiscal year, idempotency key, funding values, and carry-forward amounts must be valid."));

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var project = await FindProjectForUpdateAsync(tenantId, projectId, cancellationToken);
        if (project is null) return Result<ProjectAnnualBudgetClosingDto>.Failure(Error.NotFound("Project not found."));
        var existingKey = await db.ProjectAnnualBudgetClosings.IgnoreQueryFilters()
            .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.ProjectId == projectId
                && item.IdempotencyKey == dto.IdempotencyKey, cancellationToken);
        if (existingKey is not null)
        {
            if (existingKey.FiscalYear != dto.FiscalYear || existingKey.ProposedFreeCarryForward != dto.ProposedFreeCarryForward
                || existingKey.CarryOutstandingCommitments != dto.CarryOutstandingCommitments
                || existingKey.NextFiscalYearNewFunding != dto.NextFiscalYearNewFunding
                || existingKey.NextFiscalYearAdjustments != dto.NextFiscalYearAdjustments)
                return Result<ProjectAnnualBudgetClosingDto>.Failure(Error.Conflict("This closing idempotency key was already used for different values."));
            await transaction.CommitAsync(cancellationToken);
            return Result<ProjectAnnualBudgetClosingDto>.Success(await MapClosingAsync(existingKey, actorId, cancellationToken));
        }

        var current = await tenantFiscalYear.ResolveForTimestampAsync(tenantId, DateTimeOffset.UtcNow, cancellationToken);
        if (dto.FiscalYear >= current.FiscalYear)
            return Result<ProjectAnnualBudgetClosingDto>.Failure(Error.Conflict("A fiscal year can only be closed after its Tenant fiscal period has ended."));
        if (await db.ProjectAnnualBudgetClosings.IgnoreQueryFilters().AnyAsync(item => item.TenantId == tenantId
                && item.ProjectId == projectId && item.FiscalYear == dto.FiscalYear
                && item.Status == ProjectAnnualBudgetClosingStatuses.Closed && !item.IsDeleted, cancellationToken))
            return Result<ProjectAnnualBudgetClosingDto>.Failure(Error.Conflict("This Project fiscal year is already closed."));
        if (await db.ProjectAnnualBudgetClosings.IgnoreQueryFilters().AnyAsync(item => item.TenantId == tenantId
                && item.ProjectId == projectId && item.FiscalYear == dto.FiscalYear
                && (item.Status == ProjectAnnualBudgetClosingStatuses.Review
                    || item.Status == ProjectAnnualBudgetClosingStatuses.PendingApproval)
                && !item.IsDeleted, cancellationToken))
            return Result<ProjectAnnualBudgetClosingDto>.Failure(Error.Conflict("A closing attempt for this Project fiscal year is already in progress."));

        var budget = await db.ProjectAnnualBudgets.IgnoreQueryFilters().FirstOrDefaultAsync(item => item.TenantId == tenantId
            && item.ProjectId == projectId && item.FiscalYear == dto.FiscalYear && !item.IsDeleted, cancellationToken);
        if (budget is null || budget.Status != ProjectAnnualBudgetStatuses.Approved)
            return Result<ProjectAnnualBudgetClosingDto>.Failure(Error.Conflict("An approved Project Annual Budget is required before closing this fiscal year."));
        var snapshotResult = await BuildClosingSnapshotAsync(tenantId, project, budget, dto.ProposedFreeCarryForward,
            dto.CarryOutstandingCommitments, dto.NextFiscalYearNewFunding, dto.NextFiscalYearAdjustments, cancellationToken);
        if (snapshotResult.Error is not null) return Result<ProjectAnnualBudgetClosingDto>.Failure(snapshotResult.Error);
        var snapshot = snapshotResult.Snapshot!;
        var closing = new ProjectAnnualBudgetClosing
        {
            TenantId = tenantId,
            ProjectId = projectId,
            SourceAnnualBudgetId = budget.Id,
            FiscalYear = dto.FiscalYear,
            FiscalYearStartDate = snapshot.Period.StartDate,
            FiscalYearEndDate = snapshot.Period.EndDate,
            Currency = snapshot.Currency,
            DestinationFiscalYear = dto.FiscalYear + 1,
            Status = ProjectAnnualBudgetClosingStatuses.Review,
            IdempotencyKey = dto.IdempotencyKey,
            SnapshotFingerprint = snapshot.Fingerprint,
            SnapshotJson = snapshot.Json,
            ApprovedAnnualBudget = budget.TotalAnnualBudget,
            NewFunding = budget.NewApprovedFunding,
            IncomingCarryForward = budget.ApprovedCarryForward,
            ApprovedAdjustments = budget.ApprovedAdjustments,
            ApprovedPaymentRequestAllocations = snapshot.ApprovedPaymentRequestAllocations,
            ApprovedReversalReleases = snapshot.ApprovedReversalReleases,
            EffectiveActiveAllocations = snapshot.EffectiveActiveAllocations,
            ActualSettlementsInFiscalYear = snapshot.ActualSettlementsInFiscalYear,
            OutstandingCommitments = snapshot.OutstandingCommitments,
            FreeAvailableBudget = snapshot.FreeAvailableBudget,
            TotalUnspentBudget = snapshot.TotalUnspentBudget,
            ProposedFreeCarryForward = dto.ProposedFreeCarryForward,
            CarryOutstandingCommitments = dto.CarryOutstandingCommitments,
            ApprovedFreeCarryForward = 0m,
            ApprovedReservedCarryForward = 0m,
            NonCarriedFreeBalance = Math.Max(0m, snapshot.FreeAvailableBudget - dto.ProposedFreeCarryForward),
            NextFiscalYearNewFunding = dto.NextFiscalYearNewFunding,
            NextFiscalYearAdjustments = dto.NextFiscalYearAdjustments,
            PendingPaymentRequestCount = snapshot.PendingPaymentRequestCount,
            PendingReversalRequestCount = snapshot.PendingReversalRequestCount,
            UnresolvedLegacyRequestCount = snapshot.UnresolvedLegacyRequestCount,
            IntegrityIssueCount = snapshot.Issues.Count,
            IntegrityIssuesJson = JsonSerializer.Serialize(snapshot.Issues),
            PreparedBy = actorId,
            PreparedAtUtc = DateTime.UtcNow,
            CreatedBy = actorId.ToString("D")
        };
        db.ProjectAnnualBudgetClosings.Add(closing);
        AddClosingAudit(project, closing, actorId, actorName, "YearEndClosingPrepared",
            "Project fiscal year closing statement prepared", null, SnapshotAudit(closing));
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<ProjectAnnualBudgetClosingDto>.Failure(Error.Conflict("Project financial records changed while preparing the closing. Refresh and retry."));
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            return Result<ProjectAnnualBudgetClosingDto>.Failure(Error.Conflict("A closing attempt or idempotency key already exists for this Project fiscal year."));
        }
        return Result<ProjectAnnualBudgetClosingDto>.Success(await MapClosingAsync(closing, actorId, cancellationToken));
    }

    public async Task<Result<ProjectAnnualBudgetClosingDto>> SubmitClosingAsync(
        Guid tenantId, Guid projectId, Guid closingId, Guid actorId, string? actorName,
        SubmitProjectAnnualBudgetClosingDto dto, CancellationToken cancellationToken)
    {
        var accessError = await ValidateClosingAccessAsync(tenantId, projectId, actorId, ClosingPreparePermission, cancellationToken);
        if (accessError is not null) return Result<ProjectAnnualBudgetClosingDto>.Failure(accessError);
        if (dto.RowVersion is not { Length: > 0 }) return Result<ProjectAnnualBudgetClosingDto>.Failure(Error.Validation("A current row version is required."));
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var project = await FindProjectForUpdateAsync(tenantId, projectId, cancellationToken);
        if (project is null) return Result<ProjectAnnualBudgetClosingDto>.Failure(Error.NotFound("Project not found."));
        var closing = await db.ProjectAnnualBudgetClosings.IgnoreQueryFilters().FirstOrDefaultAsync(item => item.TenantId == tenantId
            && item.ProjectId == projectId && item.Id == closingId && !item.IsDeleted, cancellationToken);
        if (closing is null) return Result<ProjectAnnualBudgetClosingDto>.Failure(Error.NotFound("Closing statement not found."));
        if (closing.PreparedBy != actorId || closing.Status != ProjectAnnualBudgetClosingStatuses.Review)
            return Result<ProjectAnnualBudgetClosingDto>.Failure(Error.Forbidden("Only the preparer may submit a Review closing."));
        if (!MatchesVersion(closing.RowVersion, dto.RowVersion)) return Result<ProjectAnnualBudgetClosingDto>.Failure(Error.Conflict("Closing statement changed. Refresh before submitting."));
        if (closing.IntegrityIssueCount > 0)
            return Result<ProjectAnnualBudgetClosingDto>.Failure(Error.Conflict("Resolve all closing statement integrity issues before submitting."));
        if ((closing.ProposedFreeCarryForward > 0m || closing.OutstandingCommitments > 0m)
            && !await HasPermissionAsync(actorId, tenantId, CarryForwardPermission, cancellationToken))
            return Result<ProjectAnnualBudgetClosingDto>.Failure(Error.Forbidden("ProjectAnnualBudget.CarryForward is required for free or reserved carry forward."));
        var setup = PaymentRequestWorkflowPolicy.ValidateApprovalSetup(project.FinanceApprovalMode, dto.ApprovalMode, dto.Approvers);
        if (setup.Count > 0) return Result<ProjectAnnualBudgetClosingDto>.Failure(Error.Validation(string.Join(",", setup)));
        if (dto.Approvers.Any(item => item.UserId == actorId))
            return Result<ProjectAnnualBudgetClosingDto>.Failure(Error.Validation("The closing preparer cannot approve the same closing."));
        var eligible = await GetEligibleClosingApproverIdsAsync(tenantId, projectId, cancellationToken);
        if (dto.Approvers.Any(item => !eligible.Contains(item.UserId)))
            return Result<ProjectAnnualBudgetClosingDto>.Failure(Error.Validation("Every closing approver must be an active eligible Project Team member with the separate closing approval permission."));

        var now = DateTime.UtcNow;
        var approval = new ApprovalRequest
        {
            TenantId = tenantId,
            Title = $"Close Project {project.Code} — FY {closing.FiscalYear}",
            Description = $"Approve year-end statement and explicit carry forward for {closing.Currency}.",
            EntityType = "Project",
            EntityId = projectId,
            EntityNumber = $"YEC-{project.Code}-{closing.FiscalYear}-{closing.Id:N}".ToUpperInvariant(),
            Type = ClosingApprovalType,
            Status = PaymentRequestStatuses.Pending,
            Priority = "Normal",
            Amount = closing.ProposedFreeCarryForward + closing.OutstandingCommitments,
            Currency = closing.Currency,
            RequestedBy = actorId,
            RequestedAt = now,
            CurrentApproverId = string.Equals(dto.ApprovalMode, PaymentRequestApprovalModes.Sequential, StringComparison.OrdinalIgnoreCase)
                ? dto.Approvers.OrderBy(item => item.Position).First().UserId : null,
            ApprovalLevel = 0,
            CreatedBy = actorId.ToString("D")
        };
        db.ApprovalRequests.Add(approval);
        foreach (var selected in dto.Approvers)
        {
            db.PaymentRequestApproverAssignments.Add(new PaymentRequestApproverAssignment
            {
                TenantId = tenantId,
                ApprovalRequestId = approval.Id,
                ApproverUserId = selected.UserId,
                Position = selected.Position,
                Requirement = selected.Requirement,
                State = PaymentRequestAssignmentStates.Pending,
                CreatedBy = actorId.ToString("D")
            });
        }
        var before = closing.Status;
        closing.Status = ProjectAnnualBudgetClosingStatuses.PendingApproval;
        closing.ApprovalRequestId = approval.Id;
        closing.ApprovalMode = dto.ApprovalMode;
        closing.SubmittedBy = actorId;
        closing.SubmittedAtUtc = now;
        closing.UpdatedBy = actorId.ToString("D");
        AddClosingAudit(project, closing, actorId, actorName, "YearEndClosingSubmitted",
            "Project year-end closing submitted for approval", new { status = before }, new { status = closing.Status, approvalId = approval.Id });
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<ProjectAnnualBudgetClosingDto>.Failure(Error.Conflict("Closing statement changed while being submitted."));
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            return Result<ProjectAnnualBudgetClosingDto>.Failure(Error.Conflict("Another closing approval is already in progress for this fiscal year."));
        }
        return Result<ProjectAnnualBudgetClosingDto>.Success(await MapClosingAsync(closing, actorId, cancellationToken));
    }

    public async Task<Result<ProjectAnnualBudgetClosingDto>> DecideClosingAsync(
        Guid tenantId, Guid projectId, Guid closingId, Guid actorId, string? actorName,
        DecideProjectAnnualBudgetClosingDto dto, CancellationToken cancellationToken)
    {
        var accessError = await ValidateClosingAccessAsync(tenantId, projectId, actorId, ClosingApprovePermission, cancellationToken);
        if (accessError is not null) return Result<ProjectAnnualBudgetClosingDto>.Failure(accessError);
        var decision = string.Equals(dto.Decision, PaymentRequestDecisions.Approved, StringComparison.OrdinalIgnoreCase)
            ? PaymentRequestDecisions.Approved
            : string.Equals(dto.Decision, PaymentRequestDecisions.Rejected, StringComparison.OrdinalIgnoreCase)
                ? PaymentRequestDecisions.Rejected : string.Empty;
        if (decision.Length == 0) return Result<ProjectAnnualBudgetClosingDto>.Failure(Error.Validation("Decision must be Approved or Rejected."));

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var project = await FindProjectForUpdateAsync(tenantId, projectId, cancellationToken);
        if (project is null) return Result<ProjectAnnualBudgetClosingDto>.Failure(Error.NotFound("Project not found."));
        var closing = await db.ProjectAnnualBudgetClosings.IgnoreQueryFilters().FirstOrDefaultAsync(item => item.TenantId == tenantId
            && item.ProjectId == projectId && item.Id == closingId && !item.IsDeleted, cancellationToken);
        if (closing is null || closing.Status != ProjectAnnualBudgetClosingStatuses.PendingApproval || !closing.ApprovalRequestId.HasValue)
            return Result<ProjectAnnualBudgetClosingDto>.Failure(Error.Conflict("Only a pending closing can receive an approval decision."));
        if (closing.SubmittedBy == actorId)
            return Result<ProjectAnnualBudgetClosingDto>.Failure(Error.Forbidden("The closing preparer cannot approve or reject the same closing."));
        var approval = await db.ApprovalRequests.IgnoreQueryFilters().FirstOrDefaultAsync(item => item.TenantId == tenantId
            && item.Id == closing.ApprovalRequestId && !item.IsDeleted && item.Type == ClosingApprovalType, cancellationToken);
        if (approval is null || approval.Status != PaymentRequestStatuses.Pending)
            return Result<ProjectAnnualBudgetClosingDto>.Failure(Error.Conflict("The shared approval record is unavailable or finalized."));
        var assignments = await db.PaymentRequestApproverAssignments.IgnoreQueryFilters()
            .Where(item => item.TenantId == tenantId && item.ApprovalRequestId == approval.Id && !item.IsDeleted)
            .OrderBy(item => item.Position).ToListAsync(cancellationToken);
        var actions = await db.ApprovalActions.IgnoreQueryFilters().Where(item => item.TenantId == tenantId
            && item.ApprovalRequestId == approval.Id && !item.IsDeleted).ToListAsync(cancellationToken);
        var assignment = assignments.FirstOrDefault(item => item.ApproverUserId == actorId);
        if (assignment is null || actions.Any(item => item.ApproverAssignmentId == assignment.Id))
            return Result<ProjectAnnualBudgetClosingDto>.Failure(Error.Forbidden("Only an assigned closing approver without a prior decision may act."));
        var eligibleApprovers = await GetEligibleClosingApproverIdsAsync(tenantId, projectId, cancellationToken);
        if (!eligibleApprovers.Contains(actorId))
            return Result<ProjectAnnualBudgetClosingDto>.Failure(Error.Forbidden(
                "The assigned approver must remain an active eligible Project Team member with closing approval permission."));
        var states = BuildStates(assignments, actions);
        var mayDecide = closing.ApprovalMode == PaymentRequestApprovalModes.Sequential
            ? PaymentRequestWorkflowPolicy.CanDecideSequentially(actorId, approval.Status, states)
            : PaymentRequestWorkflowPolicy.CanDecideInParallel(actorId, approval.Status, states);
        if (!mayDecide) return Result<ProjectAnnualBudgetClosingDto>.Failure(Error.Conflict("This approver is not currently eligible to decide."));
        var now = DateTime.UtcNow;
        assignment.State = decision;
        assignment.UpdatedBy = actorId.ToString("D");
        db.ApprovalActions.Add(new ApprovalAction
        {
            TenantId = tenantId, ApprovalRequestId = approval.Id, ApproverId = actorId,
            ApproverAssignmentId = assignment.Id, Action = decision, Comments = dto.Comment?.Trim(),
            ActionedAt = now, ApprovalLevel = closing.ApprovalMode == PaymentRequestApprovalModes.Sequential ? assignment.Position ?? 0 : 0,
            CreatedBy = actorId.ToString("D")
        });
        var projected = BuildStates(assignments, actions, actorId, decision);
        var final = closing.ApprovalMode == PaymentRequestApprovalModes.Sequential
            ? PaymentRequestWorkflowPolicy.EvaluateSequentialStatus(projected)
            : PaymentRequestWorkflowPolicy.EvaluateParallelStatus(projected);
        if (final is PaymentRequestStatuses.Approved or PaymentRequestStatuses.Rejected)
        {
            approval.Status = final;
            approval.CurrentApproverId = null;
            approval.UpdatedBy = actorId.ToString("D");
            closing.CompletedBy = actorId;
            closing.CompletedAtUtc = now;
            if (final == PaymentRequestStatuses.Rejected)
            {
                closing.Status = ProjectAnnualBudgetClosingStatuses.Rejected;
                AddClosingAudit(project, closing, actorId, actorName, "YearEndClosingRejected",
                    "Project year-end closing was rejected; no year lock or funding transfer was created.",
                    new { status = ProjectAnnualBudgetClosingStatuses.PendingApproval },
                    new { status = closing.Status, comment = dto.Comment?.Trim() });
            }
            else
            {
                if ((closing.ProposedFreeCarryForward > 0m || closing.OutstandingCommitments > 0m)
                    && !await HasPermissionAsync(actorId, tenantId, CarryForwardPermission, cancellationToken))
                    return Result<ProjectAnnualBudgetClosingDto>.Failure(Error.Forbidden("ProjectAnnualBudget.CarryForward is required to approve the selected transfers."));
                var sourceBudget = await FindAnnualBudgetForUpdateAsync(tenantId, projectId, closing.SourceAnnualBudgetId, cancellationToken);
                if (sourceBudget is null || sourceBudget.Status != ProjectAnnualBudgetStatuses.Approved || sourceBudget.ClosedAtUtc.HasValue)
                    return Result<ProjectAnnualBudgetClosingDto>.Failure(Error.Conflict("The source Annual Budget is unavailable, already closed, or no longer approved."));
                var latest = await BuildClosingSnapshotAsync(tenantId, project, sourceBudget, closing.ProposedFreeCarryForward,
                    closing.CarryOutstandingCommitments, closing.NextFiscalYearNewFunding, closing.NextFiscalYearAdjustments, cancellationToken);
                if (latest.Error is not null) return Result<ProjectAnnualBudgetClosingDto>.Failure(latest.Error);
                if (latest.Snapshot!.Issues.Count != 0 || latest.Snapshot.Fingerprint != closing.SnapshotFingerprint)
                    return Result<ProjectAnnualBudgetClosingDto>.Failure(Error.Conflict("Financial records changed after preparation. Cancel this attempt and prepare a fresh closing statement."));
                var destinationResult = await ResolveDestinationBudgetAsync(tenantId, project, closing, actorId, cancellationToken);
                if (destinationResult.Error is not null) return Result<ProjectAnnualBudgetClosingDto>.Failure(destinationResult.Error);
                var destination = destinationResult.Budget!;
                if (destination.Status == ProjectAnnualBudgetStatuses.Approved && destination.ClosedAtUtc.HasValue)
                    return Result<ProjectAnnualBudgetClosingDto>.Failure(Error.Conflict("The destination Annual Budget is already closed."));
                var outstandingRequests = await GetOutstandingRequestsForBudgetAsync(tenantId, projectId, sourceBudget.Id, cancellationToken);
                var reserved = outstandingRequests.Sum(item => item.Balance.RemainingPayable);
                if (reserved != closing.OutstandingCommitments || (reserved > 0m && !closing.CarryOutstandingCommitments))
                    return Result<ProjectAnnualBudgetClosingDto>.Failure(Error.Conflict("Outstanding commitments changed or were not explicitly selected for carry forward."));
                var destinationAllocations = await GetActiveApprovedAllocationAsync(tenantId, projectId, destination, cancellationToken);
                if (destinationAllocations.CurrencyMismatch
                    || destinationAllocations.Amount + reserved > destination.TotalAnnualBudget)
                    return Result<ProjectAnnualBudgetClosingDto>.Failure(Error.Conflict("The destination Annual Budget has insufficient unallocated funding for the approved commitment transfers."));
                if (reserved > 0m && destination.Status != ProjectAnnualBudgetStatuses.Draft
                    && destination.Status != ProjectAnnualBudgetStatuses.Approved)
                    return Result<ProjectAnnualBudgetClosingDto>.Failure(Error.Conflict("The destination Annual Budget is not in a supported state."));

                foreach (var item in outstandingRequests.Where(item => item.Balance.RemainingPayable > 0m))
                {
                    var currentTransfer = await GetCurrentFundingTransferAsync(tenantId, projectId, item.Request.Id, cancellationToken);
                    var duplicate = await db.ProjectPaymentRequestFundingTransfers.IgnoreQueryFilters().AnyAsync(transfer =>
                        transfer.TenantId == tenantId && transfer.ProjectId == projectId
                        && transfer.PaymentRequestId == item.Request.Id && transfer.DestinationFiscalYear == closing.DestinationFiscalYear,
                        cancellationToken);
                    if (duplicate) return Result<ProjectAnnualBudgetClosingDto>.Failure(Error.Conflict("A commitment transfer to this Fiscal Year already exists."));
                    var transfer = new ProjectPaymentRequestFundingTransfer
                    {
                        TenantId = tenantId,
                        ProjectId = projectId,
                        PaymentRequestId = item.Request.Id,
                        SourceAnnualBudgetId = sourceBudget.Id,
                        SourceFiscalYear = closing.FiscalYear,
                        DestinationAnnualBudgetId = destination.Id,
                        DestinationFiscalYear = closing.DestinationFiscalYear!.Value,
                        ClosingId = closing.Id,
                        SupersedesFundingTransferId = currentTransfer?.Id,
                        Amount = item.Balance.RemainingPayable,
                        Currency = NormalizeCurrency(item.Request.Currency),
                        IdempotencyKey = DeterministicGuid(closing.Id, item.Request.Id),
                        ApprovedBy = actorId,
                        ApprovedAtUtc = now,
                        CreatedBy = actorId.ToString("D")
                    };
                    db.ProjectPaymentRequestFundingTransfers.Add(transfer);
                    AddClosingAudit(project, closing, actorId, actorName, "CommitmentFundingTransferred",
                        $"Outstanding Payment Request funding transferred from FY {closing.FiscalYear} to FY {closing.DestinationFiscalYear}.",
                        new { annualBudgetId = sourceBudget.Id, fiscalYear = closing.FiscalYear, amount = 0m },
                        new { annualBudgetId = destination.Id, fiscalYear = closing.DestinationFiscalYear, amount = transfer.Amount,
                            paymentRequestId = item.Request.Id, currency = transfer.Currency });
                }

                destination.ApprovedCarryForward = closing.ProposedFreeCarryForward + reserved;
                destination.ApprovedReservedCarryForward = reserved;
                destination.UpdatedBy = actorId.ToString("D");
                closing.DestinationAnnualBudgetId = destination.Id;
                closing.ApprovedFreeCarryForward = closing.ProposedFreeCarryForward;
                closing.ApprovedReservedCarryForward = reserved;
                closing.NonCarriedFreeBalance = latest.Snapshot.FreeAvailableBudget - closing.ProposedFreeCarryForward;
                closing.Status = ProjectAnnualBudgetClosingStatuses.Closed;
                closing.CompletedBy = actorId;
                closing.CompletedAtUtc = now;
                closing.UpdatedBy = actorId.ToString("D");
                sourceBudget.ClosedByClosingId = closing.Id;
                sourceBudget.ClosedBy = actorId;
                sourceBudget.ClosedAtUtc = now;
                sourceBudget.UpdatedBy = actorId.ToString("D");
                AddClosingAudit(project, closing, actorId, actorName, "YearEndClosingApproved",
                    "Project year-end statement and selected carry forward approved.",
                    new { status = ProjectAnnualBudgetClosingStatuses.PendingApproval },
                    new { status = ProjectAnnualBudgetClosingStatuses.Closed, approvedFreeCarryForward = closing.ApprovedFreeCarryForward,
                        approvedReservedCarryForward = reserved, destinationAnnualBudgetId = destination.Id });
                AddClosingAudit(project, closing, actorId, actorName, "FiscalYearClosed",
                    $"Project FY {closing.FiscalYear} closed; historical closing snapshot is locked.",
                    new { closed = false }, new { closed = true, fiscalYear = closing.FiscalYear, sourceBudgetId = sourceBudget.Id });
                AddClosingAudit(project, closing, actorId, actorName, "FreeCarryForwardApproved",
                    "Approved free balance carry forward recorded.", null,
                    new { amount = closing.ApprovedFreeCarryForward, currency = closing.Currency, destinationFiscalYear = closing.DestinationFiscalYear });
                if (reserved > 0m)
                    AddClosingAudit(project, closing, actorId, actorName, "ReservedCarryForwardApproved",
                        "Approved outstanding commitment funding carry forward recorded.", null,
                        new { amount = reserved, currency = closing.Currency, destinationFiscalYear = closing.DestinationFiscalYear });
                AddClosingAudit(project, closing, actorId, actorName, "AnnualBudgetOpened",
                    $"FY {closing.DestinationFiscalYear} Annual Budget opening recorded as {destination.Status}.", null,
                    new { annualBudgetId = destination.Id, fiscalYear = destination.FiscalYear,
                        newFunding = destination.NewApprovedFunding, freeCarryForward = closing.ProposedFreeCarryForward,
                        reservedCarryForward = reserved, adjustments = destination.ApprovedAdjustments, status = destination.Status });
            }
        }
        else
        {
            approval.CurrentApproverId = closing.ApprovalMode == PaymentRequestApprovalModes.Sequential
                ? PaymentRequestWorkflowPolicy.GetActiveSequentialApprover(projected) : null;
        }
        AddClosingAudit(project, closing, actorId, actorName,
            decision == PaymentRequestDecisions.Approved ? "ClosingApproverApproved" : "ClosingApproverRejected",
            $"Year-end closing decision recorded by {actorName ?? actorId.ToString("D")}.",
            new { approvalStatus = PaymentRequestStatuses.Pending },
            new { decision, comment = dto.Comment?.Trim(), closingStatus = closing.Status });
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<ProjectAnnualBudgetClosingDto>.Failure(Error.Conflict("Closing or budget state changed concurrently. Refresh and reconcile before retrying."));
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            return Result<ProjectAnnualBudgetClosingDto>.Failure(Error.Conflict("A duplicate close or funding transfer was prevented."));
        }
        return Result<ProjectAnnualBudgetClosingDto>.Success(await MapClosingAsync(closing, actorId, cancellationToken));
    }

    public async Task<Result<ProjectAnnualBudgetClosingDto>> CancelClosingAsync(
        Guid tenantId, Guid projectId, Guid closingId, Guid actorId, string? actorName, byte[] rowVersion,
        CancellationToken cancellationToken)
    {
        var accessError = await ValidateClosingAccessAsync(tenantId, projectId, actorId, ClosingPreparePermission, cancellationToken);
        if (accessError is not null) return Result<ProjectAnnualBudgetClosingDto>.Failure(accessError);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var project = await FindProjectForUpdateAsync(tenantId, projectId, cancellationToken);
        if (project is null) return Result<ProjectAnnualBudgetClosingDto>.Failure(Error.NotFound("Project not found."));
        var closing = await db.ProjectAnnualBudgetClosings.IgnoreQueryFilters().FirstOrDefaultAsync(item => item.TenantId == tenantId
            && item.ProjectId == projectId && item.Id == closingId && !item.IsDeleted, cancellationToken);
        if (closing is null) return Result<ProjectAnnualBudgetClosingDto>.Failure(Error.NotFound("Closing statement not found."));
        if (closing.PreparedBy != actorId || closing.Status is not (ProjectAnnualBudgetClosingStatuses.Review or ProjectAnnualBudgetClosingStatuses.PendingApproval))
            return Result<ProjectAnnualBudgetClosingDto>.Failure(Error.Forbidden("Only the preparer may cancel a Review or Pending closing."));
        if (!MatchesVersion(closing.RowVersion, rowVersion)) return Result<ProjectAnnualBudgetClosingDto>.Failure(Error.Conflict("Closing changed. Refresh before cancelling."));
        var previous = closing.Status;
        if (closing.ApprovalRequestId.HasValue)
        {
            var approval = await db.ApprovalRequests.IgnoreQueryFilters().FirstOrDefaultAsync(item => item.TenantId == tenantId
                && item.Id == closing.ApprovalRequestId && !item.IsDeleted, cancellationToken);
            if (approval?.Status == PaymentRequestStatuses.Pending)
            {
                approval.Status = PaymentRequestStatuses.Cancelled;
                approval.CurrentApproverId = null;
                approval.UpdatedBy = actorId.ToString("D");
            }
        }
        closing.Status = ProjectAnnualBudgetClosingStatuses.Cancelled;
        closing.CompletedBy = actorId;
        closing.CompletedAtUtc = DateTime.UtcNow;
        closing.UpdatedBy = actorId.ToString("D");
        AddClosingAudit(project, closing, actorId, actorName, "YearEndClosingCancelled",
            "Closing attempt cancelled before approval; no fiscal lock or transfer was created.",
            new { status = previous }, new { status = closing.Status });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result<ProjectAnnualBudgetClosingDto>.Success(await MapClosingAsync(closing, actorId, cancellationToken));
    }

    private async Task<(ClosingSnapshot? Snapshot, Error? Error)> BuildClosingSnapshotAsync(
        Guid tenantId, Project project, ProjectAnnualBudget budget, decimal proposedFreeCarryForward,
        bool carryOutstandingCommitments, decimal nextNewFunding, decimal nextAdjustments,
        CancellationToken cancellationToken)
    {
        var preview = await tenantFiscalYear.GetPreviewAsync(tenantId, cancellationToken: cancellationToken);
        var period = fiscalYearCalendar.ForFiscalYear(budget.FiscalYear, preview.StartMonth);
        if (budget.FiscalYearStartDate != period.StartDate || budget.FiscalYearEndDate != period.EndDate
            || budget.TenantId != tenantId || budget.ProjectId != project.Id
            || !string.Equals(NormalizeCurrency(budget.Currency), NormalizeCurrency(project.Currency), StringComparison.Ordinal))
            return (null, Error.Conflict("Annual Budget fiscal boundaries, Tenant, Project, or currency do not match the authoritative Tenant calendar."));
        var issues = new List<string>();
        var allocations = await GetActiveApprovedAllocationAsync(tenantId, project.Id, budget, cancellationToken);
        if (allocations.CurrencyMismatch) issues.Add("approved_allocation_currency_or_provenance_mismatch");
        var legacyCount = await CountLegacyApprovedRequestsAsync(tenantId, project.Id, cancellationToken);
        if (legacyCount > 0) issues.Add("unresolved_legacy_approved_payment_requests");
        var pendingRequests = await (from detail in PaymentDetailsQuery(tenantId, project.Id)
            join request in PaymentRequestsQuery(tenantId, project.Id) on detail.ApprovalRequestId equals request.Id
            where detail.RequestKind == ProjectPaymentRequestKinds.PaymentRequest
                && detail.ProjectAnnualBudgetId == budget.Id && request.Status == PaymentRequestStatuses.Pending
            select request.Id).CountAsync(cancellationToken);
        if (pendingRequests > 0) issues.Add("pending_payment_requests_require_explicit_resolution");

        var originalRows = await (from detail in PaymentDetailsQuery(tenantId, project.Id)
            join request in PaymentRequestsQuery(tenantId, project.Id) on detail.ApprovalRequestId equals request.Id
            where detail.RequestKind == ProjectPaymentRequestKinds.PaymentRequest
                && request.Status == PaymentRequestStatuses.Approved
            select new { Detail = detail, Request = request }).ToListAsync(cancellationToken);
        var originAllocations = originalRows.Where(item => item.Detail.ProjectAnnualBudgetId == budget.Id)
            .Sum(item => item.Request.Amount ?? 0m);
        var transferRows = await db.ProjectPaymentRequestFundingTransfers.IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.ProjectId == project.Id && !item.IsDeleted
                && (item.SourceAnnualBudgetId == budget.Id || item.DestinationAnnualBudgetId == budget.Id))
            .ToListAsync(cancellationToken);
        var approvedAllocations = originAllocations
            + transferRows.Where(item => item.DestinationAnnualBudgetId == budget.Id).Sum(item => item.Amount)
            - transferRows.Where(item => item.SourceAnnualBudgetId == budget.Id).Sum(item => item.Amount);
        var currentFunding = new Dictionary<Guid, ProjectPaymentRequestFundingTransfer?>();
        var outstanding = 0m;
        foreach (var item in originalRows)
        {
            var transfer = await GetCurrentFundingTransferAsync(tenantId, project.Id, item.Request.Id, cancellationToken);
            currentFunding[item.Request.Id] = transfer;
            var currentBudgetId = transfer?.DestinationAnnualBudgetId ?? item.Detail.ProjectAnnualBudgetId;
            if (currentBudgetId != budget.Id) continue;
            var balance = await GetReversalBalanceAsync(tenantId, project.Id, item.Request, item.Detail, cancellationToken);
            if (balance.Error is not null || balance.Balance is null)
            {
                issues.Add($"request_balance_integrity:{item.Request.Id:D}");
                continue;
            }
            outstanding += balance.Balance.RemainingPayable;
        }
        var reversalRows = await (from detail in db.ProjectPaymentRequestDetails.IgnoreQueryFilters().AsNoTracking()
            join reversal in db.ApprovalRequests.IgnoreQueryFilters().AsNoTracking() on detail.ApprovalRequestId equals reversal.Id
            where detail.TenantId == tenantId && detail.ProjectId == project.Id
                && detail.RequestKind == ProjectPaymentRequestKinds.ReversalRequest
                && reversal.TenantId == tenantId && !reversal.IsDeleted && reversal.Type == ReversalRequestType
            select new { Detail = detail, Request = reversal }).ToListAsync(cancellationToken);
        var approvedReleases = 0m;
        var pendingReversals = 0;
        foreach (var reversal in reversalRows)
        {
            if (!reversal.Detail.ReversesApprovalRequestId.HasValue) { issues.Add("reversal_missing_original_request"); continue; }
            var originalDetail = originalRows.FirstOrDefault(item => item.Request.Id == reversal.Detail.ReversesApprovalRequestId.Value);
            if (originalDetail is null) continue;
            var transfer = currentFunding.GetValueOrDefault(originalDetail.Request.Id);
            var budgetId = reversal.Detail.FundingTransferId.HasValue
                ? (await db.ProjectPaymentRequestFundingTransfers.IgnoreQueryFilters().AsNoTracking()
                    .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.ProjectId == project.Id
                        && item.Id == reversal.Detail.FundingTransferId.Value, cancellationToken))?.DestinationAnnualBudgetId
                : reversal.Detail.ProjectAnnualBudgetId;
            if (reversal.Request.Status == PaymentRequestStatuses.Pending && (budgetId == budget.Id ||
                (transfer?.DestinationAnnualBudgetId == budget.Id && !reversal.Detail.FundingTransferId.HasValue))) pendingReversals++;
            if (reversal.Request.Status == PaymentRequestStatuses.Approved && budgetId == budget.Id)
            {
                if (reversal.Request.Amount is not > 0m || !string.Equals(NormalizeCurrency(reversal.Request.Currency), NormalizeCurrency(budget.Currency), StringComparison.Ordinal))
                    issues.Add($"reversal_integrity:{reversal.Request.Id:D}");
                else approvedReleases += reversal.Request.Amount.Value;
            }
        }
        if (pendingReversals > 0) issues.Add("pending_reversal_requests_require_explicit_resolution");

        var startMonth = preview.StartMonth;
        var range = fiscalYearCalendar.ToTimestampRange(period, TimeZoneInfo.FindSystemTimeZoneById(preview.TimeZoneId));
        var settlementRows = await db.PaymentSettlements.IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.ProjectId == project.Id && !item.IsDeleted
                && item.SettlementDate >= range.StartInclusive.UtcDateTime && item.SettlementDate < range.EndExclusive.UtcDateTime)
            .Select(item => new { item.Id, item.Amount, item.Currency, item.ProjectPaymentRequestId,
                item.PaymentObligationId, item.FundingTransferId })
            .ToListAsync(cancellationToken);
        var settlementObligationIds = settlementRows.Where(item => item.PaymentObligationId.HasValue)
            .Select(item => item.PaymentObligationId!.Value).Distinct().ToArray();
        var settlementObligationCurrencies = settlementObligationIds.Length == 0
            ? new Dictionary<Guid, string>()
            : await db.Set<PaymentObligation>().IgnoreQueryFilters().AsNoTracking()
                .Where(item => item.TenantId == tenantId && settlementObligationIds.Contains(item.Id))
                .ToDictionaryAsync(item => item.Id, item => item.Currency, cancellationToken);
        var resolvedSettlementCurrencies = settlementRows.ToDictionary(
            item => item.Id,
            item => ResolveSettlementCurrency(item.Currency, item.PaymentObligationId, settlementObligationCurrencies));
        if (settlementRows.Any(item => item.PaymentObligationId.HasValue == item.ProjectPaymentRequestId.HasValue
            || (item.FundingTransferId.HasValue && !item.ProjectPaymentRequestId.HasValue)
            || (item.ProjectPaymentRequestId.HasValue && !originalRows.Any(request => request.Request.Id == item.ProjectPaymentRequestId.Value))
            || (item.FundingTransferId.HasValue && !transferRows.Any(transfer => transfer.Id == item.FundingTransferId.Value
                && transfer.PaymentRequestId == item.ProjectPaymentRequestId))))
            issues.Add("settlement_parent_or_funding_provenance_invalid");
        if (settlementRows.Any(item => item.Amount <= 0m || !HasMoneyScale(item.Amount)
            || string.IsNullOrWhiteSpace(resolvedSettlementCurrencies[item.Id])
            || !string.Equals(NormalizeCurrency(resolvedSettlementCurrencies[item.Id]), NormalizeCurrency(budget.Currency), StringComparison.Ordinal)))
            issues.Add("settlement_amount_or_historical_currency_unresolved");
        var actualPaid = settlementRows.Where(item => item.Amount > 0m && string.Equals(NormalizeCurrency(resolvedSettlementCurrencies[item.Id]), NormalizeCurrency(budget.Currency), StringComparison.Ordinal))
            .Sum(item => item.Amount);
        var active = allocations.Amount;
        var free = budget.TotalAnnualBudget - active;
        var totalUnspent = budget.TotalAnnualBudget - actualPaid;
        if (active < 0m || free < 0m) issues.Add("annual_budget_is_overallocated");
        if (totalUnspent < 0m) issues.Add("actual_cash_exceeds_annual_budget");
        if (approvedAllocations < 0m || approvedReleases < 0m || approvedAllocations - approvedReleases != active)
            issues.Add("approved_allocation_currency_or_provenance_mismatch");
        if (proposedFreeCarryForward > free) issues.Add("proposed_free_carry_forward_exceeds_free_available_balance");
        if (proposedFreeCarryForward > 0m && !await HasPermissionAsync(management.Current?.UserId ?? Guid.Empty, tenantId, CarryForwardPermission, cancellationToken))
            issues.Add("carry_forward_authority_missing");
        if (outstanding > 0m && !carryOutstandingCommitments) issues.Add("outstanding_commitments_not_selected_for_carry_forward");
        if (nextNewFunding < 0m || !HasMoneyScale(nextNewFunding) || !HasMoneyScale(nextAdjustments)
            || nextNewFunding + proposedFreeCarryForward + outstanding + nextAdjustments < 0m)
            issues.Add("destination_annual_budget_values_invalid");
        var snapshotData = new
        {
            projectId = project.Id, sourceBudgetId = budget.Id, fiscalYear = budget.FiscalYear,
            startDate = period.StartDate, endDate = period.EndDate, currency = NormalizeCurrency(budget.Currency),
            annualBudget = budget.TotalAnnualBudget, newFunding = budget.NewApprovedFunding,
            incomingCarryForward = budget.ApprovedCarryForward, adjustments = budget.ApprovedAdjustments,
            approvedPaymentRequestAllocations = approvedAllocations, approvedReversalReleases = approvedReleases,
            effectiveActiveAllocations = active, actualSettlementsInFiscalYear = actualPaid,
            outstandingCommitments = outstanding, freeAvailable = free, totalUnspent,
            proposedFreeCarryForward, carryOutstandingCommitments, pendingRequests, pendingReversals, legacyCount,
            transfers = transferRows.OrderBy(item => item.PaymentRequestId).ThenBy(item => item.DestinationFiscalYear)
                .Select(item => new { item.PaymentRequestId, item.SourceAnnualBudgetId, item.SourceFiscalYear,
                    item.DestinationAnnualBudgetId, item.DestinationFiscalYear, item.SupersedesFundingTransferId, item.Amount }),
            settlementIds = settlementRows.Select(item => item.Id).OrderBy(id => id),
            issues = issues.OrderBy(item => item, StringComparer.Ordinal).ToArray()
        };
        var json = JsonSerializer.Serialize(snapshotData);
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
        return (new ClosingSnapshot(period, NormalizeCurrency(budget.Currency), approvedAllocations, approvedReleases,
            active, actualPaid, outstanding, free, totalUnspent, pendingRequests, pendingReversals, legacyCount,
            issues.Distinct(StringComparer.Ordinal).OrderBy(item => item, StringComparer.Ordinal).ToArray(), fingerprint, json), null);
    }

    private async Task<List<(ApprovalRequest Request, ProjectPaymentRequestDetail Detail, ReversalBalance Balance)>>
        GetOutstandingRequestsForBudgetAsync(Guid tenantId, Guid projectId, Guid budgetId, CancellationToken cancellationToken)
    {
        var rows = await (from detail in PaymentDetailsQuery(tenantId, projectId)
            join request in PaymentRequestsQuery(tenantId, projectId) on detail.ApprovalRequestId equals request.Id
            where detail.RequestKind == ProjectPaymentRequestKinds.PaymentRequest
                && request.Status == PaymentRequestStatuses.Approved
            select new { Detail = detail, Request = request }).ToListAsync(cancellationToken);
        var result = new List<(ApprovalRequest, ProjectPaymentRequestDetail, ReversalBalance)>();
        foreach (var row in rows)
        {
            var transfer = await GetCurrentFundingTransferAsync(tenantId, projectId, row.Request.Id, cancellationToken);
            if ((transfer?.DestinationAnnualBudgetId ?? row.Detail.ProjectAnnualBudgetId) != budgetId) continue;
            var balance = await GetReversalBalanceAsync(tenantId, projectId, row.Request, row.Detail, cancellationToken);
            if (balance.Error is null && balance.Balance is not null)
                result.Add((row.Request, row.Detail, balance.Balance));
        }
        return result;
    }

    private async Task<ProjectPaymentRequestFundingTransfer?> GetCurrentFundingTransferAsync(
        Guid tenantId, Guid projectId, Guid requestId, CancellationToken cancellationToken)
    {
        var history = await db.ProjectPaymentRequestFundingTransfers.IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.ProjectId == projectId && item.PaymentRequestId == requestId && !item.IsDeleted)
            .OrderBy(item => item.DestinationFiscalYear).ToListAsync(cancellationToken);
        if (history.Count == 0) return null;
        var detail = await db.ProjectPaymentRequestDetails.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.ProjectId == projectId
                && item.ApprovalRequestId == requestId && item.RequestKind == ProjectPaymentRequestKinds.PaymentRequest,
                cancellationToken);
        if (detail is null) return null;
        var request = await db.ApprovalRequests.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.Id == requestId && !item.IsDeleted, cancellationToken);
        if (request is null || request.Status != PaymentRequestStatuses.Approved || request.Amount is not > 0m
            || history.Any(item => !IsValidCurrency(item.Currency)
                || !string.Equals(NormalizeCurrency(item.Currency), NormalizeCurrency(request.Currency), StringComparison.Ordinal)
                || item.Amount <= 0m || item.Amount > request.Amount.Value)) return null;
        var closingIds = history.Select(item => item.ClosingId).Distinct().ToArray();
        var closingTransfers = await db.ProjectPaymentRequestFundingTransfers.IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.ProjectId == projectId
                && closingIds.Contains(item.ClosingId) && !item.IsDeleted)
            .GroupBy(item => item.ClosingId)
            .Select(group => new { Id = group.Key, Amount = group.Sum(item => item.Amount) })
            .ToDictionaryAsync(item => item.Id, item => item.Amount, cancellationToken);
        Guid? priorId = null;
        Guid? priorBudgetId = detail.ProjectAnnualBudgetId;
        int? priorYear = detail.AnnualBudgetFiscalYear;
        decimal? priorAmount = null;
        foreach (var transfer in history)
        {
            var closing = await db.ProjectAnnualBudgetClosings.IgnoreQueryFilters().AsNoTracking()
                .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.ProjectId == projectId
                    && item.Id == transfer.ClosingId && item.Status == ProjectAnnualBudgetClosingStatuses.Closed
                    && item.SourceAnnualBudgetId == transfer.SourceAnnualBudgetId
                    && item.DestinationAnnualBudgetId == transfer.DestinationAnnualBudgetId
                    && item.FiscalYear == transfer.SourceFiscalYear
                    && item.DestinationFiscalYear == transfer.DestinationFiscalYear && !item.IsDeleted, cancellationToken);
            if (closing is null || transfer.SupersedesFundingTransferId != priorId
                || transfer.SourceAnnualBudgetId != priorBudgetId || transfer.SourceFiscalYear != priorYear
                || transfer.DestinationFiscalYear != transfer.SourceFiscalYear + 1
                || (priorAmount.HasValue && transfer.Amount > priorAmount.Value)
                || !closingTransfers.TryGetValue(transfer.ClosingId, out var closingAmount)
                || closingAmount != closing.ApprovedReservedCarryForward)
                return null;
            priorId = transfer.Id;
            priorBudgetId = transfer.DestinationAnnualBudgetId;
            priorYear = transfer.DestinationFiscalYear;
            priorAmount = transfer.Amount;
        }
        return history.LastOrDefault();
    }

    private async Task<bool> HasValidFundingTransferChainAsync(
        Guid tenantId, Guid projectId, Guid requestId, CancellationToken cancellationToken)
    {
        var hasTransfers = await db.ProjectPaymentRequestFundingTransfers.IgnoreQueryFilters().AnyAsync(item =>
            item.TenantId == tenantId && item.ProjectId == projectId && item.PaymentRequestId == requestId && !item.IsDeleted,
            cancellationToken);
        return !hasTransfers || await GetCurrentFundingTransferAsync(tenantId, projectId, requestId, cancellationToken) is not null;
    }

    private async Task<(ProjectAnnualBudget? Budget, Error? Error)> ResolveDestinationBudgetAsync(
        Guid tenantId, Project project, ProjectAnnualBudgetClosing closing, Guid actorId, CancellationToken cancellationToken)
    {
        var nextYear = closing.DestinationFiscalYear ?? closing.FiscalYear + 1;
        var preview = await tenantFiscalYear.GetPreviewAsync(tenantId, cancellationToken: cancellationToken);
        var period = fiscalYearCalendar.ForFiscalYear(nextYear, preview.StartMonth);
        var currency = NormalizeCurrency(project.Currency);
        var budget = await db.ProjectAnnualBudgets.IgnoreQueryFilters().FirstOrDefaultAsync(item => item.TenantId == tenantId
            && item.ProjectId == project.Id && item.FiscalYear == nextYear && !item.IsDeleted, cancellationToken);
        if (budget is null)
        {
            budget = new ProjectAnnualBudget
            {
                TenantId = tenantId, ProjectId = project.Id, FiscalYear = nextYear,
                FiscalYearStartDate = period.StartDate, FiscalYearEndDate = period.EndDate,
                Currency = currency, FinancialReference = string.Empty,
                NewApprovedFunding = closing.NextFiscalYearNewFunding,
                ApprovedCarryForward = closing.ProposedFreeCarryForward + closing.OutstandingCommitments,
                ApprovedReservedCarryForward = closing.OutstandingCommitments,
                ApprovedAdjustments = closing.NextFiscalYearAdjustments,
                Status = ProjectAnnualBudgetStatuses.Draft, CreatedBy = actorId.ToString("D")
            };
            budget.FinancialReference = $"{project.Code}-FY{nextYear}-{budget.Id.ToString("N")[..8]}".ToUpperInvariant();
            db.ProjectAnnualBudgets.Add(budget);
            return (budget, null);
        }
        budget = await FindAnnualBudgetForUpdateAsync(tenantId, project.Id, budget.Id, cancellationToken);
        if (budget is null) return (null, Error.Conflict("The destination Annual Budget could not be locked."));
        if (budget.FiscalYearStartDate != period.StartDate || budget.FiscalYearEndDate != period.EndDate
            || !string.Equals(NormalizeCurrency(budget.Currency), currency, StringComparison.Ordinal))
            return (null, Error.Conflict("The destination Annual Budget fiscal period or currency does not match the Tenant and Project."));
        var carry = closing.ProposedFreeCarryForward + closing.OutstandingCommitments;
        if (budget.NewApprovedFunding != closing.NextFiscalYearNewFunding
            || budget.ApprovedAdjustments != closing.NextFiscalYearAdjustments
            || (budget.ApprovedCarryForward != 0m && budget.ApprovedCarryForward != carry)
            || (budget.ApprovedReservedCarryForward != 0m && budget.ApprovedReservedCarryForward != closing.OutstandingCommitments))
            return (null, Error.Conflict("The existing destination Annual Budget conflicts with the approved opening values; reconcile it without overwriting approved funding."));
        if (budget.Status == ProjectAnnualBudgetStatuses.Approved
            && (budget.ApprovedCarryForward != carry || budget.ApprovedReservedCarryForward != closing.OutstandingCommitments))
            return (null, Error.Conflict("The approved destination Annual Budget does not contain the exact carry-forward values."));
        return (budget, null);
    }

    private async Task<HashSet<Guid>> GetEligibleClosingApproverIdsAsync(Guid tenantId, Guid projectId, CancellationToken cancellationToken)
    {
        var candidates = await ProjectTeamApproverEligibility.GetActiveLinkedUserIdsAsync(
            db, tenantId, projectId, cancellationToken);
        var result = new HashSet<Guid>();
        foreach (var id in candidates)
        {
            var effective = await permissions.GetEffectivePermissionsAsync(id, tenantId, cancellationToken);
            if (effective.Contains(ClosingApprovePermission)) result.Add(id);
        }
        return result;
    }

    private async Task<Error?> ValidateClosingAccessAsync(Guid tenantId, Guid projectId, Guid actorId, string specificPermission,
        CancellationToken cancellationToken)
    {
        var access = await ValidateServiceAccessAsync(tenantId, projectId, actorId, requireWrite: true, cancellationToken,
            actionPermission: specificPermission);
        if (access.Error is not null) return access.Error;
        var allowed = specificPermission == ClosingPreparePermission
            ? await HasClosingPreparePermissionAsync(actorId, tenantId, cancellationToken)
            : await HasPermissionAsync(actorId, tenantId, specificPermission, cancellationToken);
        return allowed
            ? null : Error.Forbidden($"{specificPermission} is required for this Project fiscal closing operation.");
    }

    private async Task<bool> HasClosingPreparePermissionAsync(Guid actorId, Guid tenantId, CancellationToken cancellationToken)
    {
        if (actorId == Guid.Empty) return false;
        var effective = await permissions.GetEffectivePermissionsAsync(actorId, tenantId, cancellationToken);
        return effective.Contains(ClosingPreparePermission);
    }

    private async Task<bool> HasPermissionAsync(Guid actorId, Guid tenantId, string permission, CancellationToken cancellationToken)
    {
        if (actorId == Guid.Empty) return false;
        var effective = await permissions.GetEffectivePermissionsAsync(actorId, tenantId, cancellationToken);
        return effective.Contains(permission);
    }

    private async Task<ProjectAnnualBudgetClosingDto> MapClosingAsync(
        ProjectAnnualBudgetClosing closing, Guid actorId, CancellationToken cancellationToken)
    {
        var effective = await permissions.GetEffectivePermissionsAsync(actorId, closing.TenantId, cancellationToken);
        var assignments = closing.ApprovalRequestId.HasValue
            ? await db.PaymentRequestApproverAssignments.IgnoreQueryFilters().AsNoTracking()
                .Where(item => item.TenantId == closing.TenantId && item.ApprovalRequestId == closing.ApprovalRequestId && !item.IsDeleted)
                .OrderBy(item => item.Position).ThenBy(item => item.CreatedAt).ToListAsync(cancellationToken)
            : [];
        var actions = closing.ApprovalRequestId.HasValue
            ? await db.ApprovalActions.IgnoreQueryFilters().AsNoTracking()
                .Where(item => item.TenantId == closing.TenantId && item.ApprovalRequestId == closing.ApprovalRequestId && !item.IsDeleted)
                .ToListAsync(cancellationToken)
            : [];
        var names = await LoadUserNamesAsync(closing.TenantId, assignments.Select(item => item.ApproverUserId), cancellationToken);
        var decisions = assignments.Select(item =>
        {
            var action = actions.FirstOrDefault(candidate => candidate.ApproverAssignmentId == item.Id);
            return new ProjectAnnualBudgetClosingDecisionDto(item.ApproverUserId, names.GetValueOrDefault(item.ApproverUserId),
                item.Position, item.Requirement, item.State, action?.Action, action?.ActionedAt, action?.Comments);
        }).ToArray();
        var issues = DeserializeIssues(closing.IntegrityIssuesJson);
        var canPrepare = effective.Contains(ClosingPreparePermission)
            && management.Current?.EffectiveReadOnly != true && management.Current?.Scope.ProjectIds.Contains(closing.ProjectId) == true;
        var eligible = closing.SubmittedBy != actorId && effective.Contains(ClosingApprovePermission)
            && assignments.Any(item => item.ApproverUserId == actorId && item.State == PaymentRequestAssignmentStates.Pending)
            && closing.Status == ProjectAnnualBudgetClosingStatuses.PendingApproval;
        var canSubmit = canPrepare && closing.Status == ProjectAnnualBudgetClosingStatuses.Review && closing.IntegrityIssueCount == 0
            && ((closing.ProposedFreeCarryForward == 0m && closing.OutstandingCommitments == 0m)
                || effective.Contains(CarryForwardPermission));
        return new ProjectAnnualBudgetClosingDto(closing.Id, closing.ProjectId, closing.SourceAnnualBudgetId,
            closing.FiscalYear, $"FY {closing.FiscalYear}", closing.FiscalYearStartDate, closing.FiscalYearEndDate,
            closing.Currency, closing.Status, closing.ApprovalMode, closing.ApprovalRequestId,
            closing.DestinationAnnualBudgetId, closing.DestinationFiscalYear, closing.ApprovedAnnualBudget,
            closing.NewFunding, closing.IncomingCarryForward, closing.ApprovedAdjustments,
            closing.ApprovedPaymentRequestAllocations, closing.ApprovedReversalReleases,
            closing.EffectiveActiveAllocations, closing.ActualSettlementsInFiscalYear, closing.OutstandingCommitments,
            closing.FreeAvailableBudget, closing.TotalUnspentBudget, closing.ProposedFreeCarryForward,
            closing.CarryOutstandingCommitments, closing.ApprovedFreeCarryForward, closing.ApprovedReservedCarryForward,
            closing.NonCarriedFreeBalance, closing.NextFiscalYearNewFunding, closing.NextFiscalYearAdjustments,
            closing.PendingPaymentRequestCount, closing.PendingReversalRequestCount, closing.UnresolvedLegacyRequestCount,
            closing.IntegrityIssueCount, issues, decisions, closing.PreparedBy, closing.PreparedAtUtc,
            closing.SubmittedBy, closing.SubmittedAtUtc, closing.CompletedBy, closing.CompletedAtUtc,
            closing.RowVersion, canSubmit, eligible, canPrepare && closing.PreparedBy == actorId
                && closing.Status is ProjectAnnualBudgetClosingStatuses.Review or ProjectAnnualBudgetClosingStatuses.PendingApproval);
    }

    private ProjectAnnualBudgetClosingDto MapClosing(ProjectAnnualBudgetClosing closing,
        IReadOnlyCollection<PaymentRequestApproverAssignment> assignments, IReadOnlyCollection<ApprovalAction> actions,
        IReadOnlyDictionary<Guid, string> names, Guid actorId, IReadOnlySet<Guid> eligibleApprovers,
        bool canPrepare, bool canCarry)
    {
        var decisions = assignments.Select(item =>
        {
            var action = actions.FirstOrDefault(candidate => candidate.ApproverAssignmentId == item.Id);
            return new ProjectAnnualBudgetClosingDecisionDto(item.ApproverUserId, names.GetValueOrDefault(item.ApproverUserId),
                item.Position, item.Requirement, item.State, action?.Action, action?.ActionedAt, action?.Comments);
        }).ToArray();
        var issues = DeserializeIssues(closing.IntegrityIssuesJson);
        var canApprove = closing.Status == ProjectAnnualBudgetClosingStatuses.PendingApproval && closing.SubmittedBy != actorId
            && eligibleApprovers.Contains(actorId) && assignments.Any(item => item.ApproverUserId == actorId && item.State == PaymentRequestAssignmentStates.Pending);
        return new ProjectAnnualBudgetClosingDto(closing.Id, closing.ProjectId, closing.SourceAnnualBudgetId,
            closing.FiscalYear, $"FY {closing.FiscalYear}", closing.FiscalYearStartDate, closing.FiscalYearEndDate,
            closing.Currency, closing.Status, closing.ApprovalMode, closing.ApprovalRequestId,
            closing.DestinationAnnualBudgetId, closing.DestinationFiscalYear, closing.ApprovedAnnualBudget,
            closing.NewFunding, closing.IncomingCarryForward, closing.ApprovedAdjustments,
            closing.ApprovedPaymentRequestAllocations, closing.ApprovedReversalReleases,
            closing.EffectiveActiveAllocations, closing.ActualSettlementsInFiscalYear, closing.OutstandingCommitments,
            closing.FreeAvailableBudget, closing.TotalUnspentBudget, closing.ProposedFreeCarryForward,
            closing.CarryOutstandingCommitments, closing.ApprovedFreeCarryForward, closing.ApprovedReservedCarryForward,
            closing.NonCarriedFreeBalance, closing.NextFiscalYearNewFunding, closing.NextFiscalYearAdjustments,
            closing.PendingPaymentRequestCount, closing.PendingReversalRequestCount, closing.UnresolvedLegacyRequestCount,
            closing.IntegrityIssueCount, issues, decisions, closing.PreparedBy, closing.PreparedAtUtc,
            closing.SubmittedBy, closing.SubmittedAtUtc, closing.CompletedBy, closing.CompletedAtUtc,
            closing.RowVersion,
            canPrepare && closing.Status == ProjectAnnualBudgetClosingStatuses.Review && closing.IntegrityIssueCount == 0
                && ((closing.ProposedFreeCarryForward == 0m && closing.OutstandingCommitments == 0m) || canCarry),
            canApprove,
            canPrepare && closing.PreparedBy == actorId && closing.Status is ProjectAnnualBudgetClosingStatuses.Review or ProjectAnnualBudgetClosingStatuses.PendingApproval);
    }

    private void AddClosingAudit(Project project, ProjectAnnualBudgetClosing closing, Guid actorId, string? actorName,
        string action, string description, object? before, object? after)
    {
        var now = DateTime.UtcNow;
        db.AuditEntries.Add(new AuditEntry
        {
            TenantId = project.TenantId, ProjectId = project.Id, EntityType = "ProjectAnnualBudgetClosing",
            EntityId = closing.Id, Action = action, UserId = actorId,
            UserName = string.IsNullOrWhiteSpace(actorName) ? null : actorName.Trim(), Timestamp = now,
            Description = description,
            OriginalValues = before is null ? null : JsonSerializer.Serialize(before),
            NewValues = after is null ? null : JsonSerializer.Serialize(after),
            Changes = JsonSerializer.Serialize(new
            {
                fiscalYear = new { from = (int?)null, to = closing.FiscalYear },
                annualBudgetId = new { from = (Guid?)null, to = closing.SourceAnnualBudgetId },
                destinationAnnualBudgetId = new { from = (Guid?)null, to = closing.DestinationAnnualBudgetId },
                currency = new { from = (string?)null, to = closing.Currency },
                proposedFreeCarryForward = new { from = (decimal?)null, to = closing.ProposedFreeCarryForward },
                outstandingCommitments = new { from = (decimal?)null, to = closing.OutstandingCommitments },
                status = new { from = before is null ? null : closing.Status, to = closing.Status }
            }),
            ChangedFields = "fiscalYear,annualBudgetId,destinationAnnualBudgetId,currency,proposedFreeCarryForward,outstandingCommitments,status",
            Source = "User", CreatedBy = actorId.ToString("D")
        });
    }

    private static object SnapshotAudit(ProjectAnnualBudgetClosing item) => new
    {
        fiscalYear = item.FiscalYear, currency = item.Currency, annualBudget = item.ApprovedAnnualBudget,
        approvedAllocations = item.ApprovedPaymentRequestAllocations, reversalReleases = item.ApprovedReversalReleases,
        effectiveAllocations = item.EffectiveActiveAllocations, actualPaid = item.ActualSettlementsInFiscalYear,
        outstanding = item.OutstandingCommitments, freeAvailable = item.FreeAvailableBudget,
        totalUnspent = item.TotalUnspentBudget, proposedFreeCarryForward = item.ProposedFreeCarryForward,
        carryOutstandingCommitments = item.CarryOutstandingCommitments, issues = DeserializeIssues(item.IntegrityIssuesJson)
    };

    private static IReadOnlyList<string> DeserializeIssues(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try { return JsonSerializer.Deserialize<string[]>(json) ?? []; }
        catch (JsonException) { return ["closing_integrity_issues_unreadable"]; }
    }

    private static bool HasMoneyScale(decimal value) => value < 1_000_000_000_000_000m && decimal.Round(value, 4) == value;

    private static string? ResolveSettlementCurrency(
        string? settlementCurrency, Guid? paymentObligationId, IReadOnlyDictionary<Guid, string> obligationCurrencies)
    {
        if (!string.IsNullOrWhiteSpace(settlementCurrency)) return settlementCurrency;
        return paymentObligationId.HasValue
            && obligationCurrencies.TryGetValue(paymentObligationId.Value, out var ownerCurrency)
                ? ownerCurrency
                : null;
    }

    private static Guid DeterministicGuid(Guid closingId, Guid requestId)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{closingId:D}:{requestId:D}"));
        return new Guid(bytes.AsSpan(0, 16));
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception) =>
        exception.InnerException is Microsoft.Data.SqlClient.SqlException { Number: 2601 or 2627 };

    private sealed record ClosingSnapshot(
        FiscalYearPeriod Period, string Currency, decimal ApprovedPaymentRequestAllocations,
        decimal ApprovedReversalReleases, decimal EffectiveActiveAllocations,
        decimal ActualSettlementsInFiscalYear, decimal OutstandingCommitments,
        decimal FreeAvailableBudget, decimal TotalUnspentBudget, int PendingPaymentRequestCount,
        int PendingReversalRequestCount, int UnresolvedLegacyRequestCount,
        IReadOnlyList<string> Issues, string Fingerprint, string Json);
}
