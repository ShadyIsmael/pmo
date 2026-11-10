using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Approvals.Domain;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.CostManagement.Application;
using NEXUS.Modules.CostManagement.Application.DTOs;
using NEXUS.Modules.CostManagement.Domain;
using NEXUS.Modules.Identity.Application;
using NEXUS.Modules.Identity.Domain;
using NEXUS.Modules.Organization.Application.Settings;
using NEXUS.Modules.Organization.Domain;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.Persistence;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Management;

namespace NEXUS.Api.Finance;

/// <summary>
/// Project-only annual Budget lifecycle. Project.Budget is the lifetime ceiling;
/// only an approved annual allocation authorizes Project Payment Requests.
/// </summary>
public sealed class ProjectAnnualBudgetWorkflow(
    AppDbContext db,
    ITenantFiscalYearService tenantFiscalYear,
    IFiscalYearCalendarService fiscalCalendar,
    IUserEffectivePermissionService permissions,
    IManagementContextAccessor management,
    IAuditService audit,
    IProjectAnnualBudgetApprovalNotificationPublisher approvalNotifications,
    IManagementScopeResolver scopeResolver)
{
    private const string ViewPermission = "ProjectAnnualBudget.View";
    private const string CreatePermission = "ProjectAnnualBudget.Create";
    private const string EditDraftPermission = "ProjectAnnualBudget.EditDraft";
    private const string SubmitPermission = "ProjectAnnualBudget.SubmitForReview";
    private const string ApprovePermission = "ProjectAnnualBudget.Approve";
    private const string DeletePermission = "ProjectAnnualBudget.Delete";
    private const string WithdrawPermission = "ProjectAnnualBudget.Withdraw";
    private const string ReviseApprovedPermission = "ProjectAnnualBudget.ReviseApproved";
    private const string ApprovalConfigurationPermission = "ProjectAnnualBudget.ManageApprovalConfiguration";
    private const string ProjectSettingsViewPermission = "ProjectSettings.View";
    private const string ProjectSettingsEditPermission = "ProjectSettings.Edit";
    private const string OverrideApprovalPermission = "ProjectApproval.Override";
    private const string EntityType = "ProjectAnnualBudget";
    private const string ReviewApprovalType = "ProjectAnnualBudgetReview";
    private const string RevisionApprovalType = "ProjectAnnualBudgetRevision";

    public async Task<Result<ProjectAnnualBudgetPageDto>> GetPageAsync(
        Guid tenantId, Guid projectId, Guid actorId, CancellationToken cancellationToken)
    {
        var access = await ValidateAccessAsync(tenantId, projectId, actorId, AccessKind.Read, cancellationToken);
        if (access.Error is not null)
            return Result<ProjectAnnualBudgetPageDto>.Failure(access.Error);

        var project = await FindProjectQuery(tenantId, projectId)
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);
        if (project is null)
            return Result<ProjectAnnualBudgetPageDto>.Failure(Error.NotFound("Project not found."));

        var currentPeriod = await tenantFiscalYear.ResolveForTimestampAsync(
            tenantId, DateTimeOffset.UtcNow, cancellationToken);
        var items = await db.ProjectAnnualBudgets.IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.ProjectId == projectId && !item.IsDeleted)
            .OrderByDescending(item => item.FiscalYear)
            .ToListAsync(cancellationToken);
        var hasAnyAnnualBudgetRecord = await db.ProjectAnnualBudgets.IgnoreQueryFilters().AsNoTracking()
            .AnyAsync(item => item.TenantId == tenantId && item.ProjectId == projectId, cancellationToken);

        var itemIds = items.Select(item => item.Id).ToArray();
        var approvals = itemIds.Length == 0 ? new List<ApprovalRequest>() : await db.ApprovalRequests.IgnoreQueryFilters().AsNoTracking()
            .Where(request => request.TenantId == tenantId && !request.IsDeleted
                && request.EntityType == EntityType && itemIds.Contains(request.EntityId)
                && (request.Type == ReviewApprovalType || request.Type == RevisionApprovalType))
            .OrderBy(request => request.RequestedAt)
            .ToListAsync(cancellationToken);
        var projectCurrency = NormalizeCurrency(project.Currency);
        if (projectCurrency.Length != 3 || projectCurrency.Any(character => character is < 'A' or > 'Z'))
            return Result<ProjectAnnualBudgetPageDto>.Failure(Error.Conflict("The Project currency is not configured as a valid three-letter code."));
        if (items.Any(item => !string.Equals(NormalizeCurrency(item.Currency), projectCurrency, StringComparison.Ordinal)))
            return Result<ProjectAnnualBudgetPageDto>.Failure(Error.Conflict("Existing Annual Budgets use a different currency from the Project. Reconcile those records before reviewing financial totals."));
        if (approvals.Any(request => request.Status == PaymentRequestStatuses.Pending
            && (!request.Amount.HasValue || request.Amount.Value < 0m
                || !string.Equals(NormalizeCurrency(request.Currency), projectCurrency, StringComparison.Ordinal))))
            return Result<ProjectAnnualBudgetPageDto>.Failure(Error.Conflict("A submitted Annual Budget review has an invalid amount or currency. Resolve the review before reviewing financial totals."));
        var approvalIds = approvals.Select(request => request.Id).ToArray();
        var assignments = approvalIds.Length == 0 ? new List<PaymentRequestApproverAssignment>() : await db.PaymentRequestApproverAssignments.IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.TenantId == tenantId && !item.IsDeleted && approvalIds.Contains(item.ApprovalRequestId))
            .ToListAsync(cancellationToken);
        var actions = approvalIds.Length == 0 ? new List<ApprovalAction>() : await db.ApprovalActions.IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.TenantId == tenantId && !item.IsDeleted && approvalIds.Contains(item.ApprovalRequestId))
            .ToListAsync(cancellationToken);
        var protectedDependencyIds = new HashSet<Guid>();
        var closingReferenceIds = new HashSet<Guid>();
        var paymentRequestReferenceIds = new HashSet<Guid>();
        var fundingTransferReferenceIds = new HashSet<Guid>();
        if (itemIds.Length > 0)
        {
            var closingReferences = await db.ProjectAnnualBudgetClosings.IgnoreQueryFilters().AsNoTracking()
                .Where(closing => closing.TenantId == tenantId && (itemIds.Contains(closing.SourceAnnualBudgetId)
                    || (closing.DestinationAnnualBudgetId.HasValue && itemIds.Contains(closing.DestinationAnnualBudgetId.Value))))
                .Select(closing => new { closing.SourceAnnualBudgetId, closing.DestinationAnnualBudgetId })
                .ToListAsync(cancellationToken);
            foreach (var reference in closingReferences)
            {
                if (itemIds.Contains(reference.SourceAnnualBudgetId))
                {
                    protectedDependencyIds.Add(reference.SourceAnnualBudgetId);
                    closingReferenceIds.Add(reference.SourceAnnualBudgetId);
                }
                if (reference.DestinationAnnualBudgetId.HasValue && itemIds.Contains(reference.DestinationAnnualBudgetId.Value))
                {
                    protectedDependencyIds.Add(reference.DestinationAnnualBudgetId.Value);
                    closingReferenceIds.Add(reference.DestinationAnnualBudgetId.Value);
                }
            }

            var paymentRequestBudgetIds = await db.ProjectPaymentRequestDetails.IgnoreQueryFilters().AsNoTracking()
                .Where(detail => detail.TenantId == tenantId && detail.ProjectId == projectId
                    && detail.ProjectAnnualBudgetId.HasValue && itemIds.Contains(detail.ProjectAnnualBudgetId.Value))
                .Select(detail => detail.ProjectAnnualBudgetId!.Value)
                .Distinct()
                .ToListAsync(cancellationToken);
            protectedDependencyIds.UnionWith(paymentRequestBudgetIds);
            paymentRequestReferenceIds.UnionWith(paymentRequestBudgetIds);

            var transferReferences = await db.ProjectPaymentRequestFundingTransfers.IgnoreQueryFilters().AsNoTracking()
                .Where(transfer => transfer.TenantId == tenantId && (itemIds.Contains(transfer.SourceAnnualBudgetId)
                    || itemIds.Contains(transfer.DestinationAnnualBudgetId)))
                .Select(transfer => new { transfer.SourceAnnualBudgetId, transfer.DestinationAnnualBudgetId })
                .ToListAsync(cancellationToken);
            foreach (var reference in transferReferences)
            {
                if (itemIds.Contains(reference.SourceAnnualBudgetId))
                {
                    protectedDependencyIds.Add(reference.SourceAnnualBudgetId);
                    fundingTransferReferenceIds.Add(reference.SourceAnnualBudgetId);
                }
                if (itemIds.Contains(reference.DestinationAnnualBudgetId))
                {
                    protectedDependencyIds.Add(reference.DestinationAnnualBudgetId);
                    fundingTransferReferenceIds.Add(reference.DestinationAnnualBudgetId);
                }
            }
        }
        var requestEntityIds = approvals.ToDictionary(request => request.Id, request => request.EntityId);
        var activePendingApprovalIds = approvals
            .Where(request => request.Status == PaymentRequestStatuses.Pending)
            .Select(request => request.Id)
            .ToHashSet();
        var pendingAssignmentBudgetIds = assignments
            .Where(assignment => assignment.State == PaymentRequestAssignmentStates.Pending
                && activePendingApprovalIds.Contains(assignment.ApprovalRequestId)
                && requestEntityIds.ContainsKey(assignment.ApprovalRequestId))
            .Select(assignment => requestEntityIds[assignment.ApprovalRequestId])
            .ToHashSet();
        var actors = approvals.Where(request => request.RequestedBy.HasValue).Select(request => request.RequestedBy!.Value)
            .Concat(assignments.Select(item => item.ApproverUserId)).Concat(actions.Select(item => item.ApproverId)).Distinct().ToArray();
        var actorRows = actors.Length == 0 ? new List<User>() : await db.Users.IgnoreQueryFilters().AsNoTracking()
            .Where(user => user.TenantId == tenantId && actors.Contains(user.Id))
            .ToListAsync(cancellationToken);
        var names = actorRows.ToDictionary(user => user.Id, user => user.FullName);
        var eligibleApprovers = access.CanApprove
            ? (await GetEligibleApproverRoleCategoriesAsync(tenantId, projectId, cancellationToken)).Keys.ToHashSet()
            : new HashSet<Guid>();
        var approvedAllocations = items.Where(item => item.Status == ProjectAnnualBudgetStatuses.Approved)
            .Sum(item => item.NewApprovedFunding + item.ApprovedAdjustments);
        var pendingSubmittedAllocations = approvals.Where(request => request.Status == PaymentRequestStatuses.Pending)
            .Sum(request =>
            {
                var budget = items.FirstOrDefault(item => item.Id == request.EntityId);
                if (budget is null || !request.Amount.HasValue) return 0m;
                if (request.Type == RevisionApprovalType && budget.Status == ProjectAnnualBudgetStatuses.Approved)
                    return Math.Max(0m, request.Amount.Value - (budget.NewApprovedFunding + budget.ApprovedAdjustments));
                return request.Type == ReviewApprovalType && budget.Status == ProjectAnnualBudgetStatuses.Draft
                    ? request.Amount.Value : 0m;
            });
        var remainingCeiling = project.Budget.HasValue
            ? project.Budget.Value - approvedAllocations - pendingSubmittedAllocations
            : (decimal?)null;
        var latestBudget = items.FirstOrDefault();
        int? missingClosingFiscalYear = latestBudget is not null && latestBudget.FiscalYear < 2200
            ? latestBudget.FiscalYear
            : null;
        if (latestBudget is not null && latestBudget.FiscalYear < 2200)
        {
            var nextFiscalYear = latestBudget.FiscalYear + 1;
            var closedDestinationIds = await db.ProjectAnnualBudgetClosings.IgnoreQueryFilters().AsNoTracking()
                .Where(closing => closing.TenantId == tenantId && closing.ProjectId == projectId
                    && closing.SourceAnnualBudgetId == latestBudget.Id && closing.FiscalYear == latestBudget.FiscalYear
                    && closing.DestinationFiscalYear == nextFiscalYear
                    && closing.Status == ProjectAnnualBudgetClosingStatuses.Closed && !closing.IsDeleted
                    && closing.DestinationAnnualBudgetId.HasValue)
                .Select(closing => closing.DestinationAnnualBudgetId)
                .ToListAsync(cancellationToken);
            var hasClosingCreatedOpening = closedDestinationIds.Any(destinationId =>
                destinationId.HasValue && items.Any(item => item.Id == destinationId.Value
                    && item.FiscalYear == nextFiscalYear));
            if (hasClosingCreatedOpening)
                missingClosingFiscalYear = null;
        }
        var initialBudgetFiscalYear = !hasAnyAnnualBudgetRecord ? currentPeriod.FiscalYear : (int?)null;
        var initialBudgetSuggestion = !hasAnyAnnualBudgetRecord ? project.Budget : null;

        return Result<ProjectAnnualBudgetPageDto>.Success(new ProjectAnnualBudgetPageDto(
            currentPeriod.FiscalYear,
            await tenantFiscalYear.GetStartMonthAsync(tenantId, cancellationToken),
            items.Select(item => Map(item, access.CanEditDraft, access.CanSubmit, access.CanRevise, access.CanApprove, actorId,
                approvals, assignments, actions, eligibleApprovers, names,
                access.CanDeleteDraft, access.CanWithdrawSubmission,
                protectedDependencyIds.Contains(item.Id) || item.ClosedByClosingId.HasValue || item.ClosedAtUtc.HasValue
                    || item.ClosedBy.HasValue || item.ApprovedCarryForward != 0m || item.ApprovedReservedCarryForward != 0m,
                pendingAssignmentBudgetIds.Contains(item.Id),
                access.DeleteDraftAuthorizationBlockReason,
                closingReferenceIds.Contains(item.Id)
                    ? "closing-reference"
                    : paymentRequestReferenceIds.Contains(item.Id)
                        ? "payment-request-reference"
                        : fundingTransferReferenceIds.Contains(item.Id) ? "funding-transfer-reference" : null)).ToArray(),
            project.Budget,
            NormalizeCurrency(project.Currency),
            approvedAllocations,
            pendingSubmittedAllocations,
            remainingCeiling,
            initialBudgetFiscalYear,
            initialBudgetSuggestion,
            initialBudgetSuggestion.HasValue ? "ProjectLifetimeCeiling" : null,
            missingClosingFiscalYear));
    }

    public async Task<Result<ProjectAnnualBudgetItemDto>> GetAssignedReviewAsync(
        Guid tenantId, Guid projectId, Guid itemId, Guid requestId, Guid actorId, CancellationToken cancellationToken)
    {
        var access = await ValidateAccessAsync(tenantId, projectId, actorId, AccessKind.Approve, cancellationToken);
        if (access.Error is not null)
            return Result<ProjectAnnualBudgetItemDto>.Failure(access.Error);

        var budget = await FindBudgetQuery(tenantId, projectId).AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == itemId, cancellationToken);
        if (budget is null)
            return Result<ProjectAnnualBudgetItemDto>.Failure(Error.NotFound("Annual budget review not found."));

        var request = await db.ApprovalRequests.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == requestId && item.TenantId == tenantId && !item.IsDeleted
                && item.EntityType == EntityType && item.EntityId == itemId
                && (item.Type == ReviewApprovalType || item.Type == RevisionApprovalType)
                && item.Status == PaymentRequestStatuses.Pending, cancellationToken);
        if (request is null || request.RequestedBy == actorId)
            return Result<ProjectAnnualBudgetItemDto>.Failure(Error.NotFound("Annual budget review not found."));

        var assignments = await db.PaymentRequestApproverAssignments.IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.TenantId == tenantId && !item.IsDeleted && item.ApprovalRequestId == requestId)
            .ToListAsync(cancellationToken);
        var actions = await db.ApprovalActions.IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.TenantId == tenantId && !item.IsDeleted && item.ApprovalRequestId == requestId)
            .ToListAsync(cancellationToken);
        var eligibleApprovers = (await GetEligibleApproverRoleCategoriesAsync(tenantId, projectId, cancellationToken)).Keys.ToHashSet();
        var assigned = assignments.Any(item => item.ApproverUserId == actorId
            && item.State == PaymentRequestAssignmentStates.Pending
            && actions.All(action => action.ApproverAssignmentId != item.Id));
        if (!assigned || !eligibleApprovers.Contains(actorId)
            || (assignments.Any(item => item.Position.HasValue)
                && !PaymentRequestWorkflowPolicy.CanDecideSequentially(actorId, request.Status,
                    BuildApproverStates(assignments, actions))))
            return Result<ProjectAnnualBudgetItemDto>.Failure(Error.NotFound("Annual budget review not found."));

        var actorIds = assignments.Select(item => item.ApproverUserId).Append(request.RequestedBy ?? Guid.Empty)
            .Concat(actions.Select(item => item.ApproverId)).Where(id => id != Guid.Empty).Distinct().ToArray();
        var actorRows = actorIds.Length == 0 ? new List<User>() : await db.Users.IgnoreQueryFilters().AsNoTracking()
            .Where(user => user.TenantId == tenantId && actorIds.Contains(user.Id) && !user.IsDeleted)
            .ToListAsync(cancellationToken);
        var names = actorRows.ToDictionary(user => user.Id, user => user.FullName);
        return Result<ProjectAnnualBudgetItemDto>.Success(Map(
            budget, false, false, false, true, actorId, new[] { request }, assignments, actions,
            new HashSet<Guid> { actorId }, names));
    }

    public async Task<Result<IReadOnlyList<ProjectAnnualBudgetPendingApprovalDto>>> GetMyActionableApprovalsAsync(
        Guid tenantId, Guid actorId, CancellationToken cancellationToken)
    {
        var current = management.Current;
        if (tenantId == Guid.Empty || actorId == Guid.Empty
            || current is not null && (current.TenantId != tenantId || current.UserId != actorId))
            return Result<IReadOnlyList<ProjectAnnualBudgetPendingApprovalDto>>.Failure(
                Error.NotFound("Approval assignments not found."));

        var effective = await permissions.GetEffectivePermissionsAsync(actorId, tenantId, cancellationToken);
        if (!effective.Contains(ViewPermission))
            return Result<IReadOnlyList<ProjectAnnualBudgetPendingApprovalDto>>.Failure(
                Error.Forbidden("ProjectAnnualBudget.View is required to view assigned budget approvals."));
        if (!effective.Contains(ApprovePermission))
            return Result<IReadOnlyList<ProjectAnnualBudgetPendingApprovalDto>>.Failure(
                Error.Forbidden("ProjectAnnualBudget.Approve is required to view assigned budget approvals."));

        // A read-only perspective may inspect project data, but it cannot present
        // approval actions as currently actionable.
        if (current?.EffectiveReadOnly == true)
            return Result<IReadOnlyList<ProjectAnnualBudgetPendingApprovalDto>>.Success([]);

        // My Work is cross-project. Rebuild the authenticated user's native
        // assignment scope without any selected project header, then verify each
        // candidate project through the canonical resolver before returning it.
        var perspective = await scopeResolver.GetPerspectiveSnapshotAsync(
            actorId, tenantId, cancellationToken: cancellationToken);
        var projectScope = perspective.Scope.ProjectIds;
        if (projectScope.Count == 0)
            return Result<IReadOnlyList<ProjectAnnualBudgetPendingApprovalDto>>.Success([]);

        var candidates = await (
            from assignment in db.PaymentRequestApproverAssignments.IgnoreQueryFilters().AsNoTracking()
            join request in db.ApprovalRequests.IgnoreQueryFilters().AsNoTracking()
                on assignment.ApprovalRequestId equals request.Id
            join budget in db.ProjectAnnualBudgets.IgnoreQueryFilters().AsNoTracking()
                on request.EntityId equals budget.Id
            join project in db.Projects.IgnoreQueryFilters().AsNoTracking()
                on budget.ProjectId equals project.Id
            where assignment.TenantId == tenantId && !assignment.IsDeleted
                && assignment.ApproverUserId == actorId && assignment.State == PaymentRequestAssignmentStates.Pending
                && request.TenantId == tenantId && !request.IsDeleted
                && request.EntityType == EntityType
                && (request.Type == ReviewApprovalType || request.Type == RevisionApprovalType)
                && request.Status == PaymentRequestStatuses.Pending && request.RequestedBy != actorId
                && budget.TenantId == tenantId && !budget.IsDeleted
                && project.TenantId == tenantId && !project.IsDeleted
                && projectScope.Contains(project.Id)
            select new
            {
                AssignmentId = assignment.Id,
                assignment.ApprovalRequestId,
                assignment.ApproverUserId,
                assignment.Position,
                request.RequestedBy,
                request.RequestedAt,
                request.Amount,
                request.Description,
                Currency = request.Currency ?? budget.Currency,
                request.Type,
                BudgetId = budget.Id,
                budget.FiscalYear,
                budget.FinancialReference,
                budget.NewApprovedFunding,
                budget.ApprovedCarryForward,
                budget.ApprovedAdjustments,
                budget.TotalAnnualBudget,
                ProjectId = project.Id,
                project.PublicId,
                project.Name,
                project.Code
            }).ToListAsync(cancellationToken);

        if (candidates.Count == 0)
            return Result<IReadOnlyList<ProjectAnnualBudgetPendingApprovalDto>>.Success([]);

        var requestIds = candidates.Select(item => item.ApprovalRequestId).Distinct().ToArray();
        var assignments = await db.PaymentRequestApproverAssignments.IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.TenantId == tenantId && !item.IsDeleted && requestIds.Contains(item.ApprovalRequestId))
            .ToListAsync(cancellationToken);
        var actions = await db.ApprovalActions.IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.TenantId == tenantId && !item.IsDeleted && requestIds.Contains(item.ApprovalRequestId))
            .ToListAsync(cancellationToken);
        var requesterIds = candidates.Where(item => item.RequestedBy.HasValue)
            .Select(item => item.RequestedBy!.Value).Distinct().ToArray();
        var requesterNames = requesterIds.Length == 0 ? new Dictionary<Guid, string>()
            : await db.Users.IgnoreQueryFilters().AsNoTracking()
                .Where(user => user.TenantId == tenantId && requesterIds.Contains(user.Id) && !user.IsDeleted)
                .ToDictionaryAsync(user => user.Id, user => user.FullName, cancellationToken);
        var eligibleByProject = new Dictionary<Guid, HashSet<Guid>>();
        var accessibleProjectIds = new HashSet<Guid>();
        foreach (var candidateProjectId in candidates.Select(item => item.ProjectId).Distinct())
        {
            if (!await scopeResolver.CanAccessProjectAsync(actorId, tenantId, candidateProjectId, cancellationToken))
                continue;
            accessibleProjectIds.Add(candidateProjectId);
            var eligible = await GetEligibleApproverRoleCategoriesAsync(tenantId, candidateProjectId, cancellationToken);
            eligibleByProject[candidateProjectId] = eligible.Keys.ToHashSet();
        }

        var actionable = new List<ProjectAnnualBudgetPendingApprovalDto>();
        foreach (var candidate in candidates)
        {
            if (!accessibleProjectIds.Contains(candidate.ProjectId))
                continue;
            if (!eligibleByProject.GetValueOrDefault(candidate.ProjectId, []).Contains(actorId))
                continue;
            var requestAssignments = assignments.Where(item => item.ApprovalRequestId == candidate.ApprovalRequestId).ToArray();
            var requestActions = actions.Where(item => item.ApprovalRequestId == candidate.ApprovalRequestId).ToArray();
            var ownAssignment = requestAssignments.FirstOrDefault(item => item.Id == candidate.AssignmentId);
            if (ownAssignment is null || requestActions.Any(action => action.ApproverAssignmentId == ownAssignment.Id))
                continue;

            var states = BuildApproverStates(requestAssignments, requestActions);
            var sequential = requestAssignments.Any(item => item.Position.HasValue);
            var canActNow = sequential
                ? PaymentRequestWorkflowPolicy.CanDecideSequentially(actorId, PaymentRequestStatuses.Pending, states)
                : PaymentRequestWorkflowPolicy.CanDecideInParallel(actorId, PaymentRequestStatuses.Pending, states);
            if (!canActNow) continue;

            if (!TryResolveAnnualBudgetReviewValues(candidate.Type, candidate.Description,
                    candidate.NewApprovedFunding, candidate.ApprovedCarryForward, candidate.ApprovedAdjustments,
                    candidate.Amount, out _, out _, out _, out _, out var requestedAnnualTotal))
                return Result<IReadOnlyList<ProjectAnnualBudgetPendingApprovalDto>>.Failure(
                    Error.Conflict("A persisted Annual Budget approval proposal is unreadable."));

            var priorApprovalAction = sequential && ownAssignment.Position.HasValue
                ? requestActions.Where(action => requestAssignments.Any(item => item.Id == action.ApproverAssignmentId
                        && item.Position.HasValue && item.Position.Value < ownAssignment.Position.Value)
                    && (action.Action == "Approved" || action.Action == "Approve"))
                    .OrderByDescending(action => action.ActionedAt).FirstOrDefault()
                : null;
            var actionableAt = priorApprovalAction?.ActionedAt ?? candidate.RequestedAt;
            actionable.Add(new ProjectAnnualBudgetPendingApprovalDto(
                candidate.ApprovalRequestId,
                candidate.BudgetId,
                candidate.PublicId,
                candidate.Name,
                candidate.Code,
                candidate.FiscalYear,
                candidate.FinancialReference,
                requestedAnnualTotal ?? candidate.TotalAnnualBudget,
                candidate.Currency,
                sequential ? PaymentRequestApprovalModes.Sequential : PaymentRequestApprovalModes.Parallel,
                candidate.RequestedAt,
                actionableAt,
                candidate.RequestedBy.HasValue ? requesterNames.GetValueOrDefault(candidate.RequestedBy.Value) : null));
        }

        return Result<IReadOnlyList<ProjectAnnualBudgetPendingApprovalDto>>.Success(
            actionable.OrderByDescending(item => item.ActionableAtUtc).ThenBy(item => item.ProjectName, StringComparer.OrdinalIgnoreCase).ToArray());
    }

    public async Task<Result<ProjectAnnualBudgetAssignedApprovalDetailDto>> GetMyAssignedApprovalDetailAsync(
        Guid tenantId, Guid actorId, Guid requestId, CancellationToken cancellationToken)
    {
        var current = management.Current;
        if (tenantId == Guid.Empty || actorId == Guid.Empty || requestId == Guid.Empty
            || current is not null && (current.TenantId != tenantId || current.UserId != actorId))
            return Result<ProjectAnnualBudgetAssignedApprovalDetailDto>.Failure(
                Error.NotFound("Assigned Annual Budget review not found."));

        var effective = await permissions.GetEffectivePermissionsAsync(actorId, tenantId, cancellationToken);
        if (!effective.Contains(ViewPermission))
            return Result<ProjectAnnualBudgetAssignedApprovalDetailDto>.Failure(
                Error.Forbidden("ProjectAnnualBudget.View is required to view an assigned Annual Budget review."));

        var approval = await db.ApprovalRequests.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(request => request.Id == requestId && request.TenantId == tenantId
                && !request.IsDeleted && request.EntityType == EntityType
                && (request.Type == ReviewApprovalType || request.Type == RevisionApprovalType), cancellationToken);
        if (approval is null || approval.RequestedBy == actorId)
            return Result<ProjectAnnualBudgetAssignedApprovalDetailDto>.Failure(
                Error.NotFound("Assigned Annual Budget review not found."));

        // Cancellation retires pending assignments using soft-delete. Keep that
        // immutable assignment snapshot available to its reviewers for history.
        var assignments = await db.PaymentRequestApproverAssignments.IgnoreQueryFilters().AsNoTracking()
            .Where(assignment => assignment.TenantId == tenantId && assignment.ApprovalRequestId == requestId
                && (!assignment.IsDeleted || approval.Status == PaymentRequestStatuses.Cancelled))
            .OrderBy(assignment => assignment.Position).ThenBy(assignment => assignment.Id)
            .ToListAsync(cancellationToken);
        var ownAssignment = assignments.FirstOrDefault(assignment => assignment.ApproverUserId == actorId);
        if (ownAssignment is null)
            return Result<ProjectAnnualBudgetAssignedApprovalDetailDto>.Failure(
                Error.NotFound("Assigned Annual Budget review not found."));

        var budget = await db.ProjectAnnualBudgets.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == approval.EntityId && item.TenantId == tenantId && !item.IsDeleted,
                cancellationToken);
        if (budget is null)
            return Result<ProjectAnnualBudgetAssignedApprovalDetailDto>.Failure(
                Error.NotFound("Assigned Annual Budget review not found."));
        var project = await FindProjectQuery(tenantId, budget.ProjectId).AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);
        if (project is null || !await scopeResolver.CanAccessProjectAsync(
                actorId, tenantId, project.Id, cancellationToken))
            return Result<ProjectAnnualBudgetAssignedApprovalDetailDto>.Failure(
                Error.NotFound("Assigned Annual Budget review not found."));

        var actions = await db.ApprovalActions.IgnoreQueryFilters().AsNoTracking()
            .Where(action => action.TenantId == tenantId && action.ApprovalRequestId == requestId && !action.IsDeleted)
            .OrderBy(action => action.ActionedAt)
            .ToListAsync(cancellationToken);
        var requester = approval.RequestedBy.HasValue
            ? await db.Users.IgnoreQueryFilters().AsNoTracking()
                .Where(user => user.TenantId == tenantId && user.Id == approval.RequestedBy.Value && !user.IsDeleted)
                .Select(user => user.FullName)
                .FirstOrDefaultAsync(cancellationToken)
            : null;
        var reviewerIds = assignments.Select(assignment => assignment.ApproverUserId).Distinct().ToArray();
        var reviewerNames = reviewerIds.Length == 0 ? new Dictionary<Guid, string>()
            : await db.Users.IgnoreQueryFilters().AsNoTracking()
                .Where(user => user.TenantId == tenantId && reviewerIds.Contains(user.Id) && !user.IsDeleted)
                .ToDictionaryAsync(user => user.Id, user => user.FullName, cancellationToken);

        if (!TryResolveAnnualBudgetReviewValues(approval, budget, out var newFunding,
                out var carryForward, out var adjustments, out var reason))
            return Result<ProjectAnnualBudgetAssignedApprovalDetailDto>.Failure(
                Error.Conflict("The persisted Annual Budget review proposal is unreadable. No decision can be made."));

        var sequential = assignments.Any(assignment => assignment.Position.HasValue);
        var approvalMode = sequential ? PaymentRequestApprovalModes.Sequential : PaymentRequestApprovalModes.Parallel;
        var approverStates = BuildApproverStates(assignments, actions);
        bool IsWorkflowActive(PaymentRequestApproverAssignment assignment)
        {
            if (approval.Status != PaymentRequestStatuses.Pending
                || assignment.State != PaymentRequestAssignmentStates.Pending
                || actions.Any(action => action.ApproverAssignmentId == assignment.Id))
                return false;
            return sequential
                ? PaymentRequestWorkflowPolicy.CanDecideSequentially(assignment.ApproverUserId, approval.Status, approverStates)
                : PaymentRequestWorkflowPolicy.CanDecideInParallel(assignment.ApproverUserId, approval.Status, approverStates);
        }

        var eligible = await GetEligibleApproverRoleCategoriesAsync(tenantId, project.Id, cancellationToken);
        var ownAction = actions.FirstOrDefault(action => action.ApproverAssignmentId == ownAssignment.Id);
        var ownStepIsActive = IsWorkflowActive(ownAssignment);
        var canDecide = approval.Status == PaymentRequestStatuses.Pending
            && ownAssignment.State == PaymentRequestAssignmentStates.Pending
            && ownAction is null
            && approval.RequestedBy != actorId
            && effective.Contains(ApprovePermission)
            && current?.EffectiveReadOnly != true
            && eligible.ContainsKey(actorId)
            && ownStepIsActive;
        var disabledReason = canDecide ? null
            : approval.Status != PaymentRequestStatuses.Pending ? "request_completed"
            : ownAssignment.State != PaymentRequestAssignmentStates.Pending || ownAction is not null ? "already_decided"
            : !effective.Contains(ApprovePermission) ? "approval_permission_required"
            : current?.EffectiveReadOnly == true ? "scope_read_only"
            : !eligible.ContainsKey(actorId) ? "reviewer_not_currently_eligible"
            : "waiting_for_previous_approvers";

        var steps = assignments.Select(assignment =>
        {
            var action = actions.LastOrDefault(candidate => candidate.ApproverAssignmentId == assignment.Id);
            var decision = NormalizeAnnualBudgetDecision(action?.Action)
                ?? (assignment.State == PaymentRequestAssignmentStates.Approved ? "Approved"
                    : assignment.State == PaymentRequestAssignmentStates.Rejected ? "Rejected" : null);
            var isActionable = IsWorkflowActive(assignment);
            var workflowState = decision
                ?? (approval.Status == PaymentRequestStatuses.Cancelled ? "Cancelled"
                    : approval.Status is PaymentRequestStatuses.Approved or PaymentRequestStatuses.Rejected or "Returned"
                        ? "NotRequired"
                        : isActionable ? "Active" : "Waiting");
            return new ProjectAnnualBudgetAssignedApprovalStepDto(
                assignment.Id,
                assignment.ApproverUserId,
                reviewerNames.GetValueOrDefault(assignment.ApproverUserId),
                assignment.Position,
                assignment.Requirement,
                assignment.State,
                decision,
                action?.ActionedAt,
                action?.Comments,
                assignment.ApproverUserId == actorId,
                isActionable,
                workflowState);
        }).ToArray();

        return Result<ProjectAnnualBudgetAssignedApprovalDetailDto>.Success(new ProjectAnnualBudgetAssignedApprovalDetailDto(
            approval.Id,
            budget.Id,
            project.PublicId,
            project.Name,
            project.Code,
            budget.FiscalYear,
            budget.FiscalYearStartDate,
            budget.FiscalYearEndDate,
            approval.Type,
            approval.Title,
            approval.Type == RevisionApprovalType ? null : approval.Description,
            approval.Status,
            budget.Status,
            requester,
            approval.RequestedAt,
            null,
            reason,
            newFunding,
            carryForward,
            adjustments,
            newFunding + carryForward + adjustments,
            budget.Currency,
            project.Budget,
            project.Currency,
            approvalMode,
            steps,
            budget.RowVersion,
            approval.RowVersion,
            canDecide,
            disabledReason));
    }

    public async Task<Result<IReadOnlyList<ProjectAnnualBudgetAssignedApprovalHistoryDto>>> GetMyAssignedApprovalHistoryAsync(
        Guid tenantId, Guid actorId, CancellationToken cancellationToken)
    {
        var current = management.Current;
        if (tenantId == Guid.Empty || actorId == Guid.Empty
            || current is not null && (current.TenantId != tenantId || current.UserId != actorId))
            return Result<IReadOnlyList<ProjectAnnualBudgetAssignedApprovalHistoryDto>>.Failure(
                Error.NotFound("Assigned Annual Budget review history not found."));

        var effective = await permissions.GetEffectivePermissionsAsync(actorId, tenantId, cancellationToken);
        if (!effective.Contains(ViewPermission))
            return Result<IReadOnlyList<ProjectAnnualBudgetAssignedApprovalHistoryDto>>.Failure(
                Error.Forbidden("ProjectAnnualBudget.View is required to view assigned Annual Budget history."));

        var candidates = await (
            from assignment in db.PaymentRequestApproverAssignments.IgnoreQueryFilters().AsNoTracking()
            join request in db.ApprovalRequests.IgnoreQueryFilters().AsNoTracking()
                on assignment.ApprovalRequestId equals request.Id
            join budget in db.ProjectAnnualBudgets.IgnoreQueryFilters().AsNoTracking()
                on request.EntityId equals budget.Id
            join project in db.Projects.IgnoreQueryFilters().AsNoTracking()
                on budget.ProjectId equals project.Id
            where assignment.TenantId == tenantId && assignment.ApproverUserId == actorId
                && (!assignment.IsDeleted || request.Status == PaymentRequestStatuses.Cancelled)
                && request.TenantId == tenantId && !request.IsDeleted && request.EntityType == EntityType
                && (request.Type == ReviewApprovalType || request.Type == RevisionApprovalType)
                && request.RequestedBy != actorId
                && budget.TenantId == tenantId && !budget.IsDeleted
                && project.TenantId == tenantId && !project.IsDeleted
            select new
            {
                AssignmentId = assignment.Id,
                assignment.ApproverUserId,
                assignment.State,
                assignment.Position,
                assignment.Requirement,
                RequestId = request.Id,
                request.Type,
                request.Status,
                request.RequestedBy,
                request.RequestedAt,
                request.Amount,
                RequestCurrency = request.Currency,
                request.Description,
                BudgetId = budget.Id,
                budget.FiscalYear,
                budget.FinancialReference,
                BudgetCurrency = budget.Currency,
                budget.NewApprovedFunding,
                budget.ApprovedCarryForward,
                budget.ApprovedAdjustments,
                budget.TotalAnnualBudget,
                ProjectId = project.Id,
                project.PublicId,
                project.Name,
                project.Code
            }).ToListAsync(cancellationToken);

        if (candidates.Count == 0)
            return Result<IReadOnlyList<ProjectAnnualBudgetAssignedApprovalHistoryDto>>.Success([]);

        var accessibleProjectIds = new HashSet<Guid>();
        foreach (var projectId in candidates.Select(candidate => candidate.ProjectId).Distinct())
            if (await scopeResolver.CanAccessProjectAsync(actorId, tenantId, projectId, cancellationToken))
                accessibleProjectIds.Add(projectId);

        if (accessibleProjectIds.Count == 0)
            return Result<IReadOnlyList<ProjectAnnualBudgetAssignedApprovalHistoryDto>>.Success([]);

        var requestIds = candidates.Where(candidate => accessibleProjectIds.Contains(candidate.ProjectId))
            .Select(candidate => candidate.RequestId).Distinct().ToArray();
        var assignments = await db.PaymentRequestApproverAssignments.IgnoreQueryFilters().AsNoTracking()
            .Where(assignment => assignment.TenantId == tenantId && requestIds.Contains(assignment.ApprovalRequestId))
            .ToListAsync(cancellationToken);
        var actions = await db.ApprovalActions.IgnoreQueryFilters().AsNoTracking()
            .Where(action => action.TenantId == tenantId && requestIds.Contains(action.ApprovalRequestId) && !action.IsDeleted)
            .ToListAsync(cancellationToken);
        var currentActionableIds = new HashSet<Guid>();
        if (effective.Contains(ApprovePermission) && current?.EffectiveReadOnly != true)
        {
            var actionable = await GetMyActionableApprovalsAsync(tenantId, actorId, cancellationToken);
            if (actionable.IsFailure)
                return Result<IReadOnlyList<ProjectAnnualBudgetAssignedApprovalHistoryDto>>.Failure(actionable.Error!);
            currentActionableIds.UnionWith(actionable.Value!.Select(item => item.RequestId));
        }

        var requesterIds = candidates.Where(candidate => candidate.RequestedBy.HasValue)
            .Select(candidate => candidate.RequestedBy!.Value).Distinct().ToArray();
        var requesterNames = requesterIds.Length == 0 ? new Dictionary<Guid, string>()
            : await db.Users.IgnoreQueryFilters().AsNoTracking()
                .Where(user => user.TenantId == tenantId && requesterIds.Contains(user.Id) && !user.IsDeleted)
                .ToDictionaryAsync(user => user.Id, user => user.FullName, cancellationToken);

        var history = new List<ProjectAnnualBudgetAssignedApprovalHistoryDto>();
        foreach (var candidate in candidates.Where(candidate => accessibleProjectIds.Contains(candidate.ProjectId))
                     .GroupBy(candidate => candidate.RequestId).Select(group => group.First()))
        {
            if (currentActionableIds.Contains(candidate.RequestId))
                continue;
            var requestAssignments = assignments.Where(item => item.ApprovalRequestId == candidate.RequestId
                && (!item.IsDeleted || candidate.Status == PaymentRequestStatuses.Cancelled)).ToArray();
            var requestActions = actions.Where(item => item.ApprovalRequestId == candidate.RequestId).ToArray();
            if (!TryResolveAnnualBudgetReviewValues(candidate.Type, candidate.Description,
                    candidate.NewApprovedFunding, candidate.ApprovedCarryForward, candidate.ApprovedAdjustments,
                    candidate.Amount, out _, out _, out _, out var proposalReason, out var proposedTotal))
                return Result<IReadOnlyList<ProjectAnnualBudgetAssignedApprovalHistoryDto>>.Failure(
                    Error.Conflict("A persisted Annual Budget history proposal is unreadable."));

            var ownAssignment = requestAssignments.FirstOrDefault(item => item.Id == candidate.AssignmentId);
            if (ownAssignment is null)
                continue;
            var action = requestActions.LastOrDefault(item => item.ApproverAssignmentId == ownAssignment.Id);
            var assignedStepState = NormalizeAnnualBudgetDecision(action?.Action)
                ?? (candidate.Status == PaymentRequestStatuses.Cancelled ? "Cancelled"
                    : ownAssignment.State == PaymentRequestAssignmentStates.Pending && candidate.Status == PaymentRequestStatuses.Pending
                        ? "Waiting"
                        : ownAssignment.State);
            var approvalMode = requestAssignments.Any(item => item.Position.HasValue)
                ? PaymentRequestApprovalModes.Sequential : PaymentRequestApprovalModes.Parallel;
            var totalRequested = proposedTotal ?? candidate.TotalAnnualBudget;
            history.Add(new ProjectAnnualBudgetAssignedApprovalHistoryDto(
                candidate.RequestId,
                candidate.BudgetId,
                candidate.PublicId,
                candidate.Name,
                candidate.Code,
                candidate.FiscalYear,
                candidate.FinancialReference,
                totalRequested,
                candidate.RequestCurrency ?? candidate.BudgetCurrency,
                approvalMode,
                candidate.RequestedAt,
                candidate.RequestedBy.HasValue ? requesterNames.GetValueOrDefault(candidate.RequestedBy.Value) : null,
                candidate.Status,
                candidate.Type,
                false,
                assignedStepState));
        }

        return Result<IReadOnlyList<ProjectAnnualBudgetAssignedApprovalHistoryDto>>.Success(
            history.OrderByDescending(item => item.RequestedAtUtc)
                .ThenBy(item => item.ProjectName, StringComparer.OrdinalIgnoreCase).ToArray());
    }

    public async Task<Result<ProjectAnnualBudgetItemDto>> CreateAsync(
        Guid tenantId,
        Guid projectId,
        Guid actorId,
        string? actorName,
        CreateProjectAnnualBudgetDto dto,
        CancellationToken cancellationToken)
    {
        var access = await ValidateAccessAsync(tenantId, projectId, actorId, AccessKind.Create, cancellationToken);
        if (access.Error is not null)
            return Result<ProjectAnnualBudgetItemDto>.Failure(access.Error);
        if (!IsValidAmounts(dto.NewApprovedFunding, 0m, dto.ApprovedAdjustments))
            return Result<ProjectAnnualBudgetItemDto>.Failure(Error.Validation("Funding and adjustments must use at most four decimal places, and the total must not be negative."));
        if (dto.FiscalYear is < 1900 or > 2200)
            return Result<ProjectAnnualBudgetItemDto>.Failure(Error.Validation("Fiscal year must be between 1900 and 2200."));

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var project = await FindProjectQuery(tenantId, projectId)
            .FirstOrDefaultAsync(cancellationToken);
        if (project is null)
            return Result<ProjectAnnualBudgetItemDto>.Failure(Error.NotFound("Project not found."));

        var currency = NormalizeCurrency(project.Currency);
        if (!await IsConfiguredCurrencyAsync(currency, cancellationToken))
            return Result<ProjectAnnualBudgetItemDto>.Failure(Error.Conflict("The project's currency is missing or is not configured."));

        var duplicate = await db.ProjectAnnualBudgets.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.ProjectId == projectId
                && item.FiscalYear == dto.FiscalYear, cancellationToken);
        if (duplicate is not null)
        {
            var isClosingCreatedOpening = !duplicate.IsDeleted && await db.ProjectAnnualBudgetClosings.IgnoreQueryFilters().AsNoTracking()
                .AnyAsync(closing => closing.TenantId == tenantId && closing.ProjectId == projectId
                    && closing.DestinationFiscalYear == dto.FiscalYear
                    && closing.DestinationAnnualBudgetId == duplicate.Id
                    && closing.Status == ProjectAnnualBudgetClosingStatuses.Closed && !closing.IsDeleted,
                    cancellationToken);
            if (isClosingCreatedOpening)
                return Result<ProjectAnnualBudgetItemDto>.Failure(Error.Conflict($"The FY {dto.FiscalYear} opening Draft already exists from its approved prior-year closing. Refresh Annual Budgets and use that record; do not create another or recalculate carry-forward."));
            return Result<ProjectAnnualBudgetItemDto>.Failure(Error.Conflict("An annual budget already exists for this project and fiscal year."));
        }

        FiscalYearPeriod period;
        FiscalYearPeriod currentPeriod;
        int startMonth;
        try
        {
            startMonth = await tenantFiscalYear.GetStartMonthAsync(tenantId, cancellationToken);
            currentPeriod = await tenantFiscalYear.ResolveForTimestampAsync(
                tenantId, DateTimeOffset.UtcNow, cancellationToken);
            period = fiscalCalendar.ForFiscalYear(dto.FiscalYear, startMonth);
        }
        catch (ArgumentOutOfRangeException)
        {
            return Result<ProjectAnnualBudgetItemDto>.Failure(Error.Validation("The requested fiscal year is outside the supported date range."));
        }

        var existingBudgets = await db.ProjectAnnualBudgets.IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.ProjectId == projectId)
            .OrderByDescending(item => item.FiscalYear)
            .ToListAsync(cancellationToken);
        var openingError = await ValidateFiscalYearOpeningAsync(
            tenantId, project, dto.FiscalYear, startMonth, currentPeriod, existingBudgets, cancellationToken);
        if (openingError is not null)
            return Result<ProjectAnnualBudgetItemDto>.Failure(openingError);

        var ceilingError = await ValidateCeilingCapacityAsync(
            tenantId, project, dto.NewApprovedFunding + dto.ApprovedAdjustments, null, null, cancellationToken);
        if (ceilingError is not null)
            return Result<ProjectAnnualBudgetItemDto>.Failure(ceilingError);

        var item = new ProjectAnnualBudget
        {
            TenantId = tenantId,
            ProjectId = projectId,
            FiscalYear = period.FiscalYear,
            FiscalYearStartDate = period.StartDate,
            FiscalYearEndDate = period.EndDate,
            FinancialReference = string.Empty,
            Currency = currency,
            NewApprovedFunding = dto.NewApprovedFunding,
            ApprovedCarryForward = 0m,
            ApprovedAdjustments = dto.ApprovedAdjustments,
            Status = ProjectAnnualBudgetStatuses.Draft,
            CreatedBy = actorId.ToString("D")
        };
        item.FinancialReference = MakeReference(project.Code, period.FiscalYear, item.Id);

        db.ProjectAnnualBudgets.Add(item);
        await AppendAuditAsync(item, project, actorId, actorName, "Created", null, item, cancellationToken);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<ProjectAnnualBudgetItemDto>.Failure(Error.Conflict("The project changed while the annual budget was being created. Refresh and retry."));
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            return Result<ProjectAnnualBudgetItemDto>.Failure(Error.Conflict("An annual budget already exists for this project and fiscal year."));
        }

        return Result<ProjectAnnualBudgetItemDto>.Success(Map(
            item, access.CanEditDraft, access.CanSubmit, access.CanRevise));
    }

    public async Task<Result<ProjectAnnualBudgetItemDto>> UpdateDraftAsync(
        Guid tenantId,
        Guid projectId,
        Guid itemId,
        Guid actorId,
        string? actorName,
        UpdateProjectAnnualBudgetDto dto,
        CancellationToken cancellationToken)
    {
        var access = await ValidateAccessAsync(tenantId, projectId, actorId, AccessKind.EditDraft, cancellationToken);
        if (access.Error is not null)
            return Result<ProjectAnnualBudgetItemDto>.Failure(access.Error);
        if (!IsValidAmounts(dto.NewApprovedFunding, 0m, dto.ApprovedAdjustments))
            return Result<ProjectAnnualBudgetItemDto>.Failure(Error.Validation("Funding and adjustments must use at most four decimal places, and the total must not be negative."));
        if (dto.RowVersion is not { Length: > 0 })
            return Result<ProjectAnnualBudgetItemDto>.Failure(Error.Validation("A current row version is required."));

        var project = await FindProjectQuery(tenantId, projectId).FirstOrDefaultAsync(cancellationToken);
        if (project is null)
            return Result<ProjectAnnualBudgetItemDto>.Failure(Error.NotFound("Project not found."));
        var item = await FindBudgetQuery(tenantId, projectId)
            .FirstOrDefaultAsync(row => row.Id == itemId, cancellationToken);
        if (item is null)
            return Result<ProjectAnnualBudgetItemDto>.Failure(Error.NotFound("Annual budget not found."));
        if (!item.RowVersion.SequenceEqual(dto.RowVersion))
            return Result<ProjectAnnualBudgetItemDto>.Failure(Error.Conflict("The annual budget changed. Refresh before editing."));
        if (item.Status != ProjectAnnualBudgetStatuses.Draft)
            return Result<ProjectAnnualBudgetItemDto>.Failure(Error.Conflict("Approved annual budgets are immutable."));
        if (await db.ApprovalRequests.IgnoreQueryFilters().AnyAsync(request => request.TenantId == tenantId
                && !request.IsDeleted && request.EntityType == EntityType && request.EntityId == item.Id
                && request.Type == ReviewApprovalType && request.Status == PaymentRequestStatuses.Pending, cancellationToken))
            return Result<ProjectAnnualBudgetItemDto>.Failure(Error.Conflict("A submitted Annual Budget cannot be edited while review is in progress."));
        var openedByCompletedClosing = await db.ProjectAnnualBudgetClosings.IgnoreQueryFilters().AnyAsync(closing =>
            closing.TenantId == tenantId && closing.ProjectId == projectId && closing.DestinationAnnualBudgetId == item.Id
            && closing.Status == ProjectAnnualBudgetClosingStatuses.Closed && !closing.IsDeleted, cancellationToken);
        if (openedByCompletedClosing)
            return Result<ProjectAnnualBudgetItemDto>.Failure(Error.Conflict("This draft is the destination opening of a completed closing and cannot be edited outside its approval history."));
        if (!string.Equals(item.Currency, NormalizeCurrency(project.Currency), StringComparison.Ordinal))
            return Result<ProjectAnnualBudgetItemDto>.Failure(Error.Conflict("The project currency has changed since this draft was created. The annual budget cannot be edited across currencies."));

        var ceilingError = await ValidateCeilingCapacityAsync(
            tenantId, project, dto.NewApprovedFunding + dto.ApprovedAdjustments, item.Id, null, cancellationToken);
        if (ceilingError is not null)
            return Result<ProjectAnnualBudgetItemDto>.Failure(ceilingError);

        var before = Snapshot(item);
        if (item.NewApprovedFunding == dto.NewApprovedFunding
            && item.ApprovedAdjustments == dto.ApprovedAdjustments)
            return Result<ProjectAnnualBudgetItemDto>.Success(Map(
                item, access.CanEditDraft, access.CanSubmit, access.CanRevise));

        item.NewApprovedFunding = dto.NewApprovedFunding;
        item.ApprovedAdjustments = dto.ApprovedAdjustments;
        item.UpdatedAt = DateTime.UtcNow;
        item.UpdatedBy = actorId.ToString("D");
        await AppendAuditAsync(item, project, actorId, actorName, "Updated", before, item, cancellationToken);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<ProjectAnnualBudgetItemDto>.Failure(Error.Conflict("The annual budget changed. Refresh before editing."));
        }

        return Result<ProjectAnnualBudgetItemDto>.Success(Map(
            item, access.CanEditDraft, access.CanSubmit, access.CanRevise));
    }

    public async Task<Result<ProjectAnnualBudgetItemDto>> DeleteDraftAsync(
        Guid tenantId, Guid projectId, Guid itemId, Guid actorId, string? actorName,
        DeleteProjectAnnualBudgetDto dto, CancellationToken cancellationToken)
    {
        var access = await ValidateAccessAsync(tenantId, projectId, actorId, AccessKind.DeleteDraft, cancellationToken);
        if (access.Error is not null)
            return Result<ProjectAnnualBudgetItemDto>.Failure(access.Error);
        if (dto.RowVersion is not { Length: > 0 })
            return Result<ProjectAnnualBudgetItemDto>.Failure(Error.Validation("A current row version is required."));

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var project = await FindProjectQuery(tenantId, projectId).FirstOrDefaultAsync(cancellationToken);
        if (project is null)
            return Result<ProjectAnnualBudgetItemDto>.Failure(Error.NotFound("Project not found."));
        var item = await FindBudgetQuery(tenantId, projectId)
            .FirstOrDefaultAsync(row => row.Id == itemId, cancellationToken);
        if (item is null)
            return Result<ProjectAnnualBudgetItemDto>.Failure(Error.NotFound("Annual budget not found."));
        if (!item.RowVersion.SequenceEqual(dto.RowVersion))
            return Result<ProjectAnnualBudgetItemDto>.Failure(Error.Conflict("The Annual Budget changed. Refresh before deleting it."));
        if (item.Status != ProjectAnnualBudgetStatuses.Draft)
            return Result<ProjectAnnualBudgetItemDto>.Failure(Error.Conflict("Only an eligible Draft Annual Budget can be deleted. Approved budgets cannot be deleted."));
        if (item.ClosedByClosingId.HasValue || item.ClosedAtUtc.HasValue || item.ClosedBy.HasValue
            || item.ApprovedCarryForward != 0m || item.ApprovedReservedCarryForward != 0m)
            return Result<ProjectAnnualBudgetItemDto>.Failure(Error.Conflict("This Draft carries closing or carry-forward metadata and cannot be deleted without reconciling its financial provenance."));

        var relatedApprovals = await db.ApprovalRequests.IgnoreQueryFilters().AsNoTracking()
            .Where(request => request.TenantId == tenantId
                && request.EntityType == EntityType && request.EntityId == item.Id
                && (request.Type == ReviewApprovalType || request.Type == RevisionApprovalType))
            .ToListAsync(cancellationToken);
        if (relatedApprovals.Any(request => !request.IsDeleted && request.Status == PaymentRequestStatuses.Pending))
            return Result<ProjectAnnualBudgetItemDto>.Failure(Error.Conflict("This Annual Budget has a submission awaiting approval. Withdraw the submission before deleting its Draft."));

        var activePendingApprovalIds = relatedApprovals
            .Where(request => !request.IsDeleted && request.Status == PaymentRequestStatuses.Pending)
            .Select(request => request.Id)
            .ToArray();
        var hasPendingAssignment = activePendingApprovalIds.Length > 0 && await db.PaymentRequestApproverAssignments.IgnoreQueryFilters()
            .AnyAsync(assignment => assignment.TenantId == tenantId && !assignment.IsDeleted
                && activePendingApprovalIds.Contains(assignment.ApprovalRequestId)
                && assignment.State == PaymentRequestAssignmentStates.Pending, cancellationToken);
        if (hasPendingAssignment)
            return Result<ProjectAnnualBudgetItemDto>.Failure(Error.Conflict("An approval step is still active for this Annual Budget. Resolve or withdraw that review before deleting the Draft."));

        var hasClosingReference = await db.ProjectAnnualBudgetClosings.IgnoreQueryFilters().AnyAsync(closing =>
            closing.TenantId == tenantId && closing.ProjectId == projectId
            && (closing.SourceAnnualBudgetId == item.Id || closing.DestinationAnnualBudgetId == item.Id), cancellationToken);
        if (hasClosingReference)
            return Result<ProjectAnnualBudgetItemDto>.Failure(Error.Conflict("This Draft is referenced by a fiscal-year closing. It cannot be deleted without preserving the closing and carry-forward provenance."));

        var hasPaymentRequestReference = await db.ProjectPaymentRequestDetails.IgnoreQueryFilters().AnyAsync(detail =>
            detail.TenantId == tenantId && detail.ProjectId == projectId && detail.ProjectAnnualBudgetId == item.Id,
            cancellationToken);
        if (hasPaymentRequestReference)
            return Result<ProjectAnnualBudgetItemDto>.Failure(Error.Conflict("This Annual Budget is referenced by a Payment Request and cannot be deleted."));

        var hasFundingTransferReference = await db.ProjectPaymentRequestFundingTransfers.IgnoreQueryFilters().AnyAsync(transfer =>
            transfer.TenantId == tenantId && transfer.ProjectId == projectId
            && (transfer.SourceAnnualBudgetId == item.Id || transfer.DestinationAnnualBudgetId == item.Id), cancellationToken);
        if (hasFundingTransferReference)
            return Result<ProjectAnnualBudgetItemDto>.Failure(Error.Conflict("This Annual Budget is referenced by an approved funding transfer and cannot be deleted."));

        var now = DateTime.UtcNow;
        var before = Snapshot(item);
        item.IsDeleted = true;
        item.UpdatedAt = now;
        item.UpdatedBy = actorId.ToString("D");
        await AppendAuditAsync(item, project, actorId, actorName, "DeletedDraft", before,
            new
            {
                budgetId = item.Id,
                fiscalYear = item.FiscalYear,
                financialReference = item.FinancialReference,
                action = "DeletedDraft",
                stateBefore = ProjectAnnualBudgetStatuses.Draft,
                stateAfter = "Deleted",
                deletedAtUtc = now,
                isDeleted = true
            }, cancellationToken, now);
        db.ProjectAnnualBudgets.Remove(item);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<ProjectAnnualBudgetItemDto>.Failure(Error.Conflict("The Annual Budget changed while it was being deleted. Refresh and retry."));
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 547 })
        {
            return Result<ProjectAnnualBudgetItemDto>.Failure(Error.Conflict("A financial record now references this Annual Budget. Refresh the Finance page before trying again."));
        }

        return Result<ProjectAnnualBudgetItemDto>.Success(Map(item, false, false, false));
    }

    public async Task<Result<ProjectAnnualBudgetItemDto>> WithdrawSubmissionAsync(
        Guid tenantId, Guid projectId, Guid itemId, Guid actorId, string? actorName,
        WithdrawProjectAnnualBudgetSubmissionDto dto, CancellationToken cancellationToken)
    {
        var access = await ValidateAccessAsync(tenantId, projectId, actorId, AccessKind.WithdrawSubmission, cancellationToken);
        if (access.Error is not null)
            return Result<ProjectAnnualBudgetItemDto>.Failure(access.Error);
        if (dto.RowVersion is not { Length: > 0 } || dto.ApprovalRowVersion is not { Length: > 0 })
            return Result<ProjectAnnualBudgetItemDto>.Failure(Error.Validation("Current Annual Budget and approval row versions are required."));

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var project = await FindProjectQuery(tenantId, projectId).FirstOrDefaultAsync(cancellationToken);
        if (project is null)
            return Result<ProjectAnnualBudgetItemDto>.Failure(Error.NotFound("Project not found."));
        var item = await FindBudgetQuery(tenantId, projectId)
            .FirstOrDefaultAsync(row => row.Id == itemId, cancellationToken);
        if (item is null)
            return Result<ProjectAnnualBudgetItemDto>.Failure(Error.NotFound("Annual budget not found."));
        if (!item.RowVersion.SequenceEqual(dto.RowVersion))
            return Result<ProjectAnnualBudgetItemDto>.Failure(Error.Conflict("The Annual Budget changed. Refresh before withdrawing the submission."));

        var pendingApprovals = await db.ApprovalRequests.IgnoreQueryFilters()
            .Where(request => request.TenantId == tenantId && !request.IsDeleted
                && request.EntityType == EntityType && request.EntityId == item.Id
                && (request.Type == ReviewApprovalType || request.Type == RevisionApprovalType)
                && request.Status == PaymentRequestStatuses.Pending)
            .OrderByDescending(request => request.RequestedAt)
            .ToListAsync(cancellationToken);
        if (pendingApprovals.Count == 0)
            return Result<ProjectAnnualBudgetItemDto>.Failure(Error.Conflict("This Annual Budget has no pending submission to withdraw."));
        if (pendingApprovals.Count > 1)
            return Result<ProjectAnnualBudgetItemDto>.Failure(Error.Conflict("More than one Annual Budget submission is pending. Reconcile the approval state before withdrawing."));
        var approval = pendingApprovals[0];
        if (!approval.RowVersion.SequenceEqual(dto.ApprovalRowVersion))
            return Result<ProjectAnnualBudgetItemDto>.Failure(Error.Conflict("The approval changed. Refresh before withdrawing the submission."));
        var isInitialReview = approval.Type == ReviewApprovalType && item.Status == ProjectAnnualBudgetStatuses.Draft;
        var isApprovedRevision = approval.Type == RevisionApprovalType && item.Status == ProjectAnnualBudgetStatuses.Approved;
        if (!isInitialReview && !isApprovedRevision)
            return Result<ProjectAnnualBudgetItemDto>.Failure(Error.Conflict("The Annual Budget state no longer matches this pending approval. Refresh and reconcile the workflow."));

        var assignments = await db.PaymentRequestApproverAssignments.IgnoreQueryFilters()
            .Where(assignment => assignment.TenantId == tenantId && !assignment.IsDeleted
                && assignment.ApprovalRequestId == approval.Id)
            .ToListAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var previousBudgetStatus = item.Status;
        foreach (var assignment in assignments.Where(assignment => assignment.State == PaymentRequestAssignmentStates.Pending))
        {
            assignment.IsDeleted = true;
            assignment.UpdatedAt = now;
            assignment.UpdatedBy = actorId.ToString("D");
        }

        approval.Status = PaymentRequestStatuses.Cancelled;
        approval.CurrentApproverId = null;
        approval.UpdatedAt = now;
        approval.UpdatedBy = actorId.ToString("D");
        var withdrawalAction = new ApprovalAction
        {
            TenantId = tenantId,
            ApprovalRequestId = approval.Id,
            ApproverId = actorId,
            Action = "Withdrawn",
            Comments = "Submission withdrawn by an authorized user.",
            ActionedAt = now,
            ApprovalLevel = approval.ApprovalLevel,
            CreatedBy = actorId.ToString("D")
        };
        db.ApprovalActions.Add(withdrawalAction);

        item.UpdatedAt = now;
        item.UpdatedBy = actorId.ToString("D");
        await AppendAuditAsync(item, project, actorId, actorName, "SubmissionWithdrawn", Snapshot(item),
            new
            {
                budgetId = item.Id,
                fiscalYear = item.FiscalYear,
                approvalRequestId = approval.Id,
                approvalType = approval.Type,
                action = "SubmissionWithdrawn",
                approvalStateBefore = PaymentRequestStatuses.Pending,
                approvalStateAfter = PaymentRequestStatuses.Cancelled,
                budgetStateBefore = previousBudgetStatus,
                budgetStateAfter = item.Status,
                timestampUtc = now
            }, cancellationToken, now);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<ProjectAnnualBudgetItemDto>.Failure(Error.Conflict("The Annual Budget or its approval changed while the submission was being withdrawn. Refresh and retry."));
        }

        return await GetMappedItemAsync(tenantId, projectId, itemId, actorId, cancellationToken);
    }

    public async Task<Result<ProjectAnnualBudgetApprovalConfigurationDto>> GetApprovalConfigurationAsync(
        Guid tenantId, Guid projectId, Guid actorId, CancellationToken cancellationToken)
    {
        var access = await ValidateAccessAsync(tenantId, projectId, actorId, AccessKind.ViewApprovalConfiguration, cancellationToken);
        if (access.Error is not null)
            return Result<ProjectAnnualBudgetApprovalConfigurationDto>.Failure(access.Error);

        var project = await FindProjectQuery(tenantId, projectId).AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);
        if (project is null)
            return Result<ProjectAnnualBudgetApprovalConfigurationDto>.Failure(Error.NotFound("Project not found."));

        var configuration = await db.Set<ProjectAnnualBudgetApprovalConfiguration>().IgnoreQueryFilters()
            .AsNoTracking()
            .Include(item => item.Approvers.Where(approver => approver.TenantId == tenantId
                && approver.ProjectId == projectId && !approver.IsDeleted))
            .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.ProjectId == projectId && !item.IsDeleted,
                cancellationToken);
        var eligibleRoleCategories = await GetEligibleApproverRoleCategoriesAsync(tenantId, projectId, cancellationToken);
        var eligibleIds = eligibleRoleCategories.Keys.ToHashSet();
        var eligibleUsers = await db.Users.IgnoreQueryFilters().AsNoTracking()
            .Where(user => user.TenantId == tenantId && eligibleIds.Contains(user.Id)
                && user.IsActive && !user.IsDeleted)
            .Select(user => new { user.Id, user.FullName })
            .ToListAsync(cancellationToken);
        var configuredIds = configuration?.Approvers.Select(item => item.ApproverUserId).Distinct().ToArray() ?? [];
        var configuredUsers = await db.Users.IgnoreQueryFilters().AsNoTracking()
            .Where(user => user.TenantId == tenantId && configuredIds.Contains(user.Id))
            .Select(user => new { user.Id, user.FullName })
            .ToListAsync(cancellationToken);
        var configuredNames = configuredUsers.ToDictionary(user => user.Id, user => user.FullName);
        var configuredApprovers = (configuration?.Approvers ?? [])
            .OrderBy(item => item.Position ?? int.MaxValue)
            .ThenBy(item => item.ApproverUserId)
            .Select(item => new ProjectAnnualBudgetConfiguredApproverDto(
                item.ApproverUserId,
                configuredNames.GetValueOrDefault(item.ApproverUserId),
                item.Position,
                eligibleIds.Contains(item.ApproverUserId),
                eligibleIds.Contains(item.ApproverUserId) ? null : "configured_approver_unavailable"))
            .ToArray();
        var defaultSelections = configuration is null
            ? Array.Empty<PaymentRequestApproverSelectionDto>()
            : configuration.Approvers.OrderBy(item => item.Position ?? int.MaxValue)
                .ThenBy(item => item.ApproverUserId)
                .Select(item => new PaymentRequestApproverSelectionDto(
                    item.ApproverUserId, item.Position, PaymentRequestApproverRequirements.Mandatory))
                .ToArray();
        var allowedModes = GetAllowedAnnualBudgetApprovalModes(project.FinanceApprovalMode);
        var issues = ValidateDefaultApprovalConfiguration(
            project.FinanceApprovalMode, configuration?.ApprovalMode, defaultSelections, eligibleIds,
            configuration is not null).ToList();
        if (defaultSelections.Any(item => item.UserId == actorId))
            issues.Add("annual_budget_submitter_is_configured_approver");
        var effective = await permissions.GetEffectivePermissionsAsync(actorId, tenantId, cancellationToken);
        var canOverride = access.CanWrite && effective.Contains(OverrideApprovalPermission);
        var isValid = issues.Count == 0;

        return Result<ProjectAnnualBudgetApprovalConfigurationDto>.Success(new ProjectAnnualBudgetApprovalConfigurationDto(
            project.Id,
            project.Name,
            "ProjectSettings",
            project.FinanceApprovalMode,
            configuration?.ApprovalMode,
            allowedModes,
            configuredApprovers,
            eligibleUsers.OrderBy(user => user.FullName, StringComparer.OrdinalIgnoreCase)
            .Select(user => new ProjectAnnualBudgetEligibleApproverDto(user.Id, user.FullName)
            {
                EligibleRoleCategories = eligibleRoleCategories[user.Id]
            })
                .ToArray(),
            configuration?.RowVersion,
            project.RowVersion,
            configuration is not null,
            isValid,
            isValid && !defaultSelections.Any(item => item.UserId == actorId),
            issues,
            canOverride));
    }

    public async Task<Result<ProjectAnnualBudgetApprovalConfigurationDto>> SaveApprovalConfigurationAsync(
        Guid tenantId, Guid projectId, Guid actorId, string? actorName,
        SaveProjectAnnualBudgetApprovalConfigurationDto dto, CancellationToken cancellationToken)
    {
        var access = await ValidateAccessAsync(tenantId, projectId, actorId, AccessKind.ManageApprovalConfiguration, cancellationToken);
        if (access.Error is not null)
            return Result<ProjectAnnualBudgetApprovalConfigurationDto>.Failure(access.Error);
        if (dto.ProjectSettingsRowVersion is not { Length: > 0 })
            return Result<ProjectAnnualBudgetApprovalConfigurationDto>.Failure(Error.Validation("Refresh the Project approval settings before saving."));

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var project = await FindProjectQuery(tenantId, projectId).FirstOrDefaultAsync(cancellationToken);
        if (project is null)
            return Result<ProjectAnnualBudgetApprovalConfigurationDto>.Failure(Error.NotFound("Project not found."));
        if (!project.RowVersion.SequenceEqual(dto.ProjectSettingsRowVersion))
            return Result<ProjectAnnualBudgetApprovalConfigurationDto>.Failure(Error.Conflict("Project approval settings changed. Refresh before saving."));

        var approverIds = dto.ApproverUserIds?.ToArray() ?? [];
        if (approverIds.Length == 0 || approverIds.Any(id => id == Guid.Empty)
            || approverIds.Distinct().Count() != approverIds.Length)
            return Result<ProjectAnnualBudgetApprovalConfigurationDto>.Failure(Error.Validation("Select distinct eligible approvers for the Project defaults."));
        var selections = approverIds.Select((id, index) => new PaymentRequestApproverSelectionDto(
            id,
            string.Equals(dto.ApprovalMode, PaymentRequestApprovalModes.Sequential, StringComparison.OrdinalIgnoreCase)
                ? index + 1 : null,
            PaymentRequestApproverRequirements.Mandatory)).ToArray();
        var eligibleIds = (await GetEligibleApproverRoleCategoriesAsync(tenantId, projectId, cancellationToken)).Keys.ToHashSet();
        var issues = ValidateDefaultApprovalConfiguration(project.FinanceApprovalMode, dto.ApprovalMode,
            selections, eligibleIds, hasConfiguration: true);
        if (issues.Count > 0)
            return Result<ProjectAnnualBudgetApprovalConfigurationDto>.Failure(Error.Validation(string.Join(",", issues)));

        var configuration = await db.Set<ProjectAnnualBudgetApprovalConfiguration>().IgnoreQueryFilters()
            .Include(item => item.Approvers.Where(approver => approver.TenantId == tenantId
                && approver.ProjectId == projectId && !approver.IsDeleted))
            .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.ProjectId == projectId && !item.IsDeleted,
                cancellationToken);
        if (configuration is null && dto.RowVersion is { Length: > 0 })
            return Result<ProjectAnnualBudgetApprovalConfigurationDto>.Failure(Error.Conflict("Project approval defaults changed. Refresh before saving."));
        if (configuration is not null
            && (dto.RowVersion is not { Length: > 0 } || !configuration.RowVersion.SequenceEqual(dto.RowVersion)))
            return Result<ProjectAnnualBudgetApprovalConfigurationDto>.Failure(Error.Conflict("Project approval defaults changed. Refresh before saving."));

        var before = configuration is null ? null : SnapshotApprovalConfiguration(configuration,
            projectApprovalMode: project.FinanceApprovalMode);
        if (configuration is null)
        {
            configuration = new ProjectAnnualBudgetApprovalConfiguration
            {
                TenantId = tenantId,
                ProjectId = projectId,
                CreatedBy = actorId.ToString("D")
            };
            db.Set<ProjectAnnualBudgetApprovalConfiguration>().Add(configuration);
        }
        configuration.ApprovalMode = dto.ApprovalMode;
        configuration.UpdatedAt = DateTime.UtcNow;
        configuration.UpdatedBy = actorId.ToString("D");

        var selectedById = selections.ToDictionary(item => item.UserId);
        var existingById = configuration.Approvers.ToDictionary(item => item.ApproverUserId);
        foreach (var current in configuration.Approvers.ToArray())
        {
            if (!selectedById.TryGetValue(current.ApproverUserId, out var selected))
            {
                current.IsDeleted = true;
                current.UpdatedAt = DateTime.UtcNow;
                current.UpdatedBy = actorId.ToString("D");
                continue;
            }
            current.Position = selected.Position;
            current.UpdatedAt = DateTime.UtcNow;
            current.UpdatedBy = actorId.ToString("D");
        }
        foreach (var selected in selections.Where(item => !existingById.ContainsKey(item.UserId)))
        {
            configuration.Approvers.Add(new ProjectAnnualBudgetApprovalApprover
            {
                TenantId = tenantId,
                ProjectId = projectId,
                ConfigurationId = configuration.Id,
                ApproverUserId = selected.UserId,
                Position = selected.Position,
                CreatedBy = actorId.ToString("D")
            });
        }

        var after = SnapshotApprovalConfiguration(configuration, selections, project.FinanceApprovalMode);
        await audit.AppendAsync(new AuditEntry
        {
            TenantId = tenantId,
            ProjectId = projectId,
            EntityType = "ProjectAnnualBudgetApprovalConfiguration",
            EntityId = configuration.Id,
            Action = before is null ? "Configured" : "Updated",
            UserId = actorId,
            UserName = actorName,
            Timestamp = DateTime.UtcNow,
            Description = $"Annual Budget approval defaults changed for {project.Name}.",
            Changes = JsonSerializer.Serialize(new { from = before, to = after }),
            OriginalValues = before is null ? null : JsonSerializer.Serialize(before),
            NewValues = JsonSerializer.Serialize(after),
            CreatedBy = actorId.ToString("D")
        }, cancellationToken);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<ProjectAnnualBudgetApprovalConfigurationDto>.Failure(Error.Conflict("Project approval defaults changed. Refresh before saving."));
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            return Result<ProjectAnnualBudgetApprovalConfigurationDto>.Failure(Error.Conflict("Project approval defaults changed. Refresh before saving."));
        }

        return await GetApprovalConfigurationAsync(tenantId, projectId, actorId, cancellationToken);
    }

    private async Task<(Error? Error, EffectiveAnnualBudgetApprovalPlan? Plan)> ResolveEffectiveApprovalPlanAsync(
        Guid tenantId,
        Guid projectId,
        Project project,
        Guid actorId,
        string? requestedMode,
        IReadOnlyList<PaymentRequestApproverSelectionDto>? requestedApprovers,
        bool customizeForThisSubmission,
        string? overrideReason,
        byte[]? configurationRowVersion,
        byte[]? projectSettingsRowVersion,
        CancellationToken cancellationToken)
    {
        if (projectSettingsRowVersion is not { Length: > 0 }
            || !project.RowVersion.SequenceEqual(projectSettingsRowVersion))
            return (Error.Conflict("Project approval settings changed. Refresh before submitting."), null);

        var configuration = await db.Set<ProjectAnnualBudgetApprovalConfiguration>().IgnoreQueryFilters()
            .AsNoTracking()
            .Include(item => item.Approvers.Where(approver => approver.TenantId == tenantId
                && approver.ProjectId == projectId && !approver.IsDeleted))
            .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.ProjectId == projectId && !item.IsDeleted,
                cancellationToken);
        if (configuration is null && configurationRowVersion is { Length: > 0 })
            return (Error.Conflict("Project Annual Budget approval defaults changed. Refresh before submitting."), null);
        if (configuration is not null
            && (configurationRowVersion is not { Length: > 0 }
                || !configuration.RowVersion.SequenceEqual(configurationRowVersion)))
            return (Error.Conflict("Project Annual Budget approval defaults changed. Refresh before submitting."), null);

        var eligibleIds = (await GetEligibleApproverRoleCategoriesAsync(tenantId, projectId, cancellationToken)).Keys.ToHashSet();
        var defaultSelections = configuration is null
            ? Array.Empty<PaymentRequestApproverSelectionDto>()
            : configuration.Approvers.OrderBy(item => item.Position ?? int.MaxValue)
                .ThenBy(item => item.ApproverUserId)
                .Select(item => new PaymentRequestApproverSelectionDto(
                    item.ApproverUserId, item.Position, PaymentRequestApproverRequirements.Mandatory))
                .ToArray();
        var defaultErrors = ValidateDefaultApprovalConfiguration(project.FinanceApprovalMode,
            configuration?.ApprovalMode, defaultSelections, eligibleIds, configuration is not null).ToList();
        if (defaultSelections.Any(item => item.UserId == actorId))
            defaultErrors.Add("annual_budget_submitter_is_configured_approver");
        var defaultPlan = new AnnualBudgetApprovalPlanAuditSnapshot(
            "ProjectSettings",
            project.FinanceApprovalMode,
            configuration?.ApprovalMode,
            configuration?.RowVersion is { Length: > 0 } ? Convert.ToBase64String(configuration.RowVersion) : null,
            defaultSelections);

        var selections = requestedApprovers?.ToArray() ?? [];
        if (!customizeForThisSubmission)
        {
            if (configuration is null || defaultErrors.Count > 0)
                return (Error.Conflict("The Project Settings Annual Budget approval defaults are missing or invalid. Configure Project Settings or use an authorized submission override."), null);
            if ((requestedMode is not null || requestedApprovers is not null)
                && !ApprovalPlansEqual(requestedMode, selections, configuration.ApprovalMode, defaultSelections))
                return (Error.Forbidden("Changing the Project Settings approval plan requires an explicit authorized override for this submission."), null);
            if (defaultSelections.Any(item => item.UserId == actorId))
                return (Error.Validation("The submitter cannot review the same Annual Budget. Choose a different authorized submission plan."), null);

            return (null, new EffectiveAnnualBudgetApprovalPlan(
                configuration.ApprovalMode,
                defaultSelections,
                defaultPlan,
                new AnnualBudgetApprovalPlanAuditSnapshot(
                    "ProjectSettings", project.FinanceApprovalMode, configuration.ApprovalMode,
                    configuration.RowVersion is { Length: > 0 } ? Convert.ToBase64String(configuration.RowVersion) : null,
                    defaultSelections),
                false,
                null));
        }

        var effectivePermissions = await permissions.GetEffectivePermissionsAsync(actorId, tenantId, cancellationToken);
        if (!effectivePermissions.Contains(OverrideApprovalPermission))
            return (Error.Forbidden("ProjectApproval.Override is required to customize the approval plan for this submission."), null);
        if (string.IsNullOrWhiteSpace(overrideReason))
            return (Error.Validation("A reason is required when customizing the approval plan for this submission."), null);
        if (overrideReason.Trim().Length > 1000)
            return (Error.Validation("The submission override reason cannot exceed 1000 characters."), null);

        var overrideErrors = ValidateDefaultApprovalConfiguration(project.FinanceApprovalMode,
            requestedMode, selections, eligibleIds, hasConfiguration: true);
        if (overrideErrors.Count > 0)
            return (Error.Validation("The customized approval plan is invalid: " + string.Join(",", overrideErrors)), null);
        if (selections.Any(item => item.UserId == actorId))
            return (Error.Validation("The submitter cannot review the same Annual Budget."), null);

        var effectivePlan = new AnnualBudgetApprovalPlanAuditSnapshot(
            "SubmissionOverride",
            project.FinanceApprovalMode,
            requestedMode,
            configuration?.RowVersion is { Length: > 0 } ? Convert.ToBase64String(configuration.RowVersion) : null,
            selections.OrderBy(item => item.Position ?? int.MaxValue).ThenBy(item => item.UserId).ToArray());
        return (null, new EffectiveAnnualBudgetApprovalPlan(
            requestedMode!,
            effectivePlan.Approvers.ToArray(),
            defaultPlan,
            effectivePlan,
            true,
            overrideReason.Trim()));
    }

    private static IReadOnlyList<string> ValidateDefaultApprovalConfiguration(
        string? projectApprovalMode,
        string? approvalMode,
        IReadOnlyCollection<PaymentRequestApproverSelectionDto> approvers,
        IReadOnlySet<Guid> eligibleApprovers,
        bool hasConfiguration)
    {
        var issues = new List<string>();
        if (!hasConfiguration)
            issues.Add("annual_budget_approval_configuration_missing");

        issues.AddRange(PaymentRequestWorkflowPolicy.ValidateApprovalSetup(projectApprovalMode, approvalMode, approvers));
        if (approvers.Any(item => !string.Equals(item.Requirement,
                PaymentRequestApproverRequirements.Mandatory, StringComparison.OrdinalIgnoreCase)))
            issues.Add("annual_budget_approvers_must_be_mandatory");
        if (approvers.Any(item => !eligibleApprovers.Contains(item.UserId)))
            issues.Add("configured_approver_unavailable");

        if (string.Equals(approvalMode, PaymentRequestApprovalModes.Sequential, StringComparison.OrdinalIgnoreCase))
        {
            var positions = approvers.Select(item => item.Position).OrderBy(position => position).ToArray();
            if (positions.Any(position => !position.HasValue)
                || !positions.Select(position => position!.Value)
                    .SequenceEqual(Enumerable.Range(1, approvers.Count)))
                issues.Add("sequential_approver_positions_invalid");
        }
        else if (string.Equals(approvalMode, PaymentRequestApprovalModes.Parallel, StringComparison.OrdinalIgnoreCase)
            && approvers.Any(item => item.Position.HasValue))
        {
            issues.Add("parallel_approver_positions_invalid");
        }

        return issues.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static bool ApprovalPlansEqual(
        string? requestedMode,
        IReadOnlyCollection<PaymentRequestApproverSelectionDto> requestedApprovers,
        string defaultMode,
        IReadOnlyCollection<PaymentRequestApproverSelectionDto> defaultApprovers)
    {
        if (!string.Equals(requestedMode, defaultMode, StringComparison.OrdinalIgnoreCase))
            return false;
        static string Requirement(string value) => value.Trim().ToUpperInvariant();
        var requested = requestedApprovers.OrderBy(item => item.Position ?? int.MaxValue).ThenBy(item => item.UserId)
            .Select(item => (item.UserId, item.Position, Requirement(item.Requirement))).ToArray();
        var defaults = defaultApprovers.OrderBy(item => item.Position ?? int.MaxValue).ThenBy(item => item.UserId)
            .Select(item => (item.UserId, item.Position, Requirement(item.Requirement))).ToArray();
        return requested.SequenceEqual(defaults);
    }

    private static IReadOnlyList<string> GetAllowedAnnualBudgetApprovalModes(string? projectApprovalMode) =>
        projectApprovalMode switch
        {
            FinanceApprovalModes.Sequential => [PaymentRequestApprovalModes.Sequential],
            FinanceApprovalModes.Parallel => [PaymentRequestApprovalModes.Parallel],
            FinanceApprovalModes.Both => [PaymentRequestApprovalModes.Sequential, PaymentRequestApprovalModes.Parallel],
            _ => []
        };

    private static object SnapshotApprovalConfiguration(
        ProjectAnnualBudgetApprovalConfiguration configuration,
        IReadOnlyCollection<PaymentRequestApproverSelectionDto>? selections = null,
        string? projectApprovalMode = null) => new
        {
            configurationSource = "ProjectSettings",
            projectApprovalMode,
            approvalMode = configuration.ApprovalMode,
            approvers = (selections ?? configuration.Approvers
                    .Where(item => !item.IsDeleted)
                    .Select(item => new PaymentRequestApproverSelectionDto(
                        item.ApproverUserId, item.Position, PaymentRequestApproverRequirements.Mandatory))
                    .ToArray())
                .OrderBy(item => item.Position ?? int.MaxValue).ThenBy(item => item.UserId)
                .Select(item => new { userId = item.UserId, item.Position, requirement = PaymentRequestApproverRequirements.Mandatory })
                .ToArray()
        };

    public async Task<Result<ProjectAnnualBudgetItemDto>> SubmitForReviewAsync(
        Guid tenantId, Guid projectId, Guid itemId, Guid actorId, string? actorName,
        SubmitProjectAnnualBudgetReviewDto dto, CancellationToken cancellationToken)
    {
        var access = await ValidateAccessAsync(tenantId, projectId, actorId, AccessKind.SubmitForReview, cancellationToken);
        if (access.Error is not null)
            return Result<ProjectAnnualBudgetItemDto>.Failure(access.Error);
        if (dto.RowVersion is not { Length: > 0 })
            return Result<ProjectAnnualBudgetItemDto>.Failure(Error.Validation("A current row version is required."));

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var project = await FindProjectQuery(tenantId, projectId).FirstOrDefaultAsync(cancellationToken);
        if (project is null) return Result<ProjectAnnualBudgetItemDto>.Failure(Error.NotFound("Project not found."));
        var item = await FindBudgetQuery(tenantId, projectId).FirstOrDefaultAsync(row => row.Id == itemId, cancellationToken);
        if (item is null) return Result<ProjectAnnualBudgetItemDto>.Failure(Error.NotFound("Annual budget not found."));
        if (item.Status != ProjectAnnualBudgetStatuses.Draft)
            return Result<ProjectAnnualBudgetItemDto>.Failure(Error.Conflict("Only a Draft annual budget can be submitted for review."));
        if (!item.RowVersion.SequenceEqual(dto.RowVersion))
            return Result<ProjectAnnualBudgetItemDto>.Failure(Error.Conflict("The annual budget changed. Refresh before submitting."));
        if (!string.Equals(item.Currency, NormalizeCurrency(project.Currency), StringComparison.Ordinal))
            return Result<ProjectAnnualBudgetItemDto>.Failure(Error.Conflict("The project currency has changed since this draft was created. Submission is blocked."));
        if (item.ApprovedReservedCarryForward < 0m || item.ApprovedReservedCarryForward > item.ApprovedCarryForward)
            return Result<ProjectAnnualBudgetItemDto>.Failure(Error.Conflict("Reserved carry forward must be a non-negative subset of approved carry forward."));
        if (item.ApprovedCarryForward > 0m && !await HasClosedCarryForwardSourceAsync(tenantId, projectId, item.Id, cancellationToken))
            return Result<ProjectAnnualBudgetItemDto>.Failure(Error.Conflict("Carry forward cannot be submitted without the corresponding completed Project year-end closing."));
        var existingReview = await db.ApprovalRequests.IgnoreQueryFilters().AnyAsync(request => request.TenantId == tenantId
            && !request.IsDeleted && request.EntityType == EntityType && request.EntityId == item.Id
            && request.Type == ReviewApprovalType && request.Status == PaymentRequestStatuses.Pending, cancellationToken);
        if (existingReview)
            return Result<ProjectAnnualBudgetItemDto>.Failure(Error.Conflict("This Annual Budget already has a review in progress."));
        EffectiveAnnualBudgetApprovalPlan? plan;
        if (item.DraftApprovalConfigurationJson is not null)
        {
            if (!ProjectAnnualBudgetDraftApprovalConfigurationSnapshot.TryRead(
                    item.DraftApprovalConfigurationJson, out var draftConfiguration) || draftConfiguration is null)
                return Result<ProjectAnnualBudgetItemDto>.Failure(
                    Error.Conflict("The Draft approval configuration is invalid. Reopen Configure Approval and save it again."));

            if (draftConfiguration.ProjectSettingsRowVersion is not { Length: > 0 }
                || !project.RowVersion.SequenceEqual(draftConfiguration.ProjectSettingsRowVersion))
                return Result<ProjectAnnualBudgetItemDto>.Failure(
                    Error.Conflict("Project approval settings changed since this Draft plan was saved. Reopen Configure Approval and save the plan again."));

            var projectApprovalConfiguration = await db.Set<ProjectAnnualBudgetApprovalConfiguration>().IgnoreQueryFilters()
                .AsNoTracking()
                .Include(configuration => configuration.Approvers.Where(approver => approver.TenantId == tenantId
                    && approver.ProjectId == projectId && !approver.IsDeleted))
                .FirstOrDefaultAsync(configuration => configuration.TenantId == tenantId
                    && configuration.ProjectId == projectId && !configuration.IsDeleted, cancellationToken);
            if (projectApprovalConfiguration is null
                    ? draftConfiguration.ProjectSettingsConfigurationRowVersion is { Length: > 0 }
                    : draftConfiguration.ProjectSettingsConfigurationRowVersion is not { Length: > 0 }
                        || !projectApprovalConfiguration.RowVersion.SequenceEqual(draftConfiguration.ProjectSettingsConfigurationRowVersion))
                return Result<ProjectAnnualBudgetItemDto>.Failure(
                    Error.Conflict("Project approval defaults changed since this Draft plan was saved. Reopen Configure Approval and save the plan again."));

            var eligibleApproverIds = (await GetEligibleApproverRoleCategoriesAsync(tenantId, projectId, cancellationToken))
                .Keys.ToHashSet();
            var planIssues = ValidateDefaultApprovalConfiguration(project.FinanceApprovalMode,
                draftConfiguration.ApprovalMode, draftConfiguration.Approvers, eligibleApproverIds, hasConfiguration: true);
            if (planIssues.Count > 0)
                return Result<ProjectAnnualBudgetItemDto>.Failure(
                    Error.Validation("The saved Draft approval configuration is no longer valid: " + string.Join(",", planIssues)));
            if (draftConfiguration.Approvers.Any(approver => approver.UserId == actorId))
                return Result<ProjectAnnualBudgetItemDto>.Failure(
                    Error.Validation("The submitter cannot review the same Annual Budget."));

            var defaultSelections = projectApprovalConfiguration is null
                ? Array.Empty<PaymentRequestApproverSelectionDto>()
                : projectApprovalConfiguration.Approvers.OrderBy(approver => approver.Position ?? int.MaxValue)
                    .ThenBy(approver => approver.ApproverUserId)
                    .Select(approver => new PaymentRequestApproverSelectionDto(
                        approver.ApproverUserId, approver.Position, PaymentRequestApproverRequirements.Mandatory))
                    .ToArray();
            var defaultPlan = new AnnualBudgetApprovalPlanAuditSnapshot(
                "ProjectSettings",
                project.FinanceApprovalMode,
                projectApprovalConfiguration?.ApprovalMode,
                projectApprovalConfiguration?.RowVersion is { Length: > 0 }
                    ? Convert.ToBase64String(projectApprovalConfiguration.RowVersion) : null,
                defaultSelections);
            var effectivePlan = new AnnualBudgetApprovalPlanAuditSnapshot(
                "DraftOverride",
                project.FinanceApprovalMode,
                draftConfiguration.ApprovalMode,
                projectApprovalConfiguration?.RowVersion is { Length: > 0 }
                    ? Convert.ToBase64String(projectApprovalConfiguration.RowVersion) : null,
                draftConfiguration.Approvers.OrderBy(approver => approver.Position ?? int.MaxValue)
                    .ThenBy(approver => approver.UserId).ToArray());
            plan = new EffectiveAnnualBudgetApprovalPlan(
                draftConfiguration.ApprovalMode,
                effectivePlan.Approvers.ToArray(),
                defaultPlan,
                effectivePlan,
                true,
                draftConfiguration.OverrideReason.Trim());
        }
        else
        {
            var currentSettings = await db.Set<ProjectAnnualBudgetApprovalConfiguration>().IgnoreQueryFilters()
                .AsNoTracking()
                .FirstOrDefaultAsync(configuration => configuration.TenantId == tenantId
                    && configuration.ProjectId == projectId && !configuration.IsDeleted, cancellationToken);
            var hasExplicitTransientOverride = dto.CustomizeForThisSubmission;
            var (planError, resolvedPlan) = await ResolveEffectiveApprovalPlanAsync(
                tenantId, projectId, project, actorId,
                hasExplicitTransientOverride ? dto.ApprovalMode : null,
                hasExplicitTransientOverride ? dto.Approvers : null,
                hasExplicitTransientOverride,
                hasExplicitTransientOverride ? dto.OverrideReason : null,
                hasExplicitTransientOverride ? dto.ApprovalConfigurationRowVersion : currentSettings?.RowVersion,
                hasExplicitTransientOverride ? dto.ProjectSettingsRowVersion : project.RowVersion,
                cancellationToken);
            if (planError is not null)
                return Result<ProjectAnnualBudgetItemDto>.Failure(planError);
            plan = resolvedPlan;
        }
        if (plan is null)
            return Result<ProjectAnnualBudgetItemDto>.Failure(Error.Conflict("The approval plan could not be resolved."));
        var ceilingError = await ValidateCeilingCapacityAsync(
            tenantId, project, item.NewApprovedFunding + item.ApprovedAdjustments, item.Id, null, cancellationToken);
        if (ceilingError is not null)
            return Result<ProjectAnnualBudgetItemDto>.Failure(ceilingError);

        var now = DateTime.UtcNow;
        var ordered = plan.Approvers;
        var approval = new ApprovalRequest
        {
            TenantId = tenantId,
            Title = $"Review Annual Budget {item.FinancialReference}",
            Description = $"Review the proposed annual allocation for FY {item.FiscalYear}.",
            EntityType = EntityType,
            EntityId = item.Id,
            EntityNumber = item.FinancialReference,
            Type = ReviewApprovalType,
            Status = PaymentRequestStatuses.Pending,
            Priority = "Normal",
            Amount = item.NewApprovedFunding + item.ApprovedAdjustments,
            Currency = item.Currency,
            RequestedBy = actorId,
            RequestedAt = now,
            CurrentApproverId = string.Equals(plan.ApprovalMode, PaymentRequestApprovalModes.Sequential, StringComparison.OrdinalIgnoreCase)
                ? ordered[0].UserId : null,
            ApprovalLevel = 0,
            CreatedBy = actorId.ToString("D")
        };
        db.ApprovalRequests.Add(approval);
        var approverAssignments = ordered.Select(selection => new PaymentRequestApproverAssignment
        {
            TenantId = tenantId,
            ApprovalRequestId = approval.Id,
            ApproverUserId = selection.UserId,
            Position = selection.Position,
            Requirement = selection.Requirement,
            State = PaymentRequestAssignmentStates.Pending,
            CreatedBy = actorId.ToString("D")
        }).ToArray();
        db.PaymentRequestApproverAssignments.AddRange(approverAssignments);
        item.UpdatedAt = now;
        item.UpdatedBy = actorId.ToString("D");
        await AppendAuditAsync(item, project, actorId, actorName, "SubmittedForReview", null,
            new
            {
                approvalRequestId = approval.Id,
                item.FiscalYear,
                item.Currency,
                amount = approval.Amount,
                approvalMode = plan.ApprovalMode,
                approvalConfigurationSource = plan.EffectivePlan.ConfigurationSource,
                defaultApprovalPlan = plan.DefaultPlan,
                effectiveApprovalPlan = plan.EffectivePlan,
                @override = plan.WasCustomized ? new
                {
                    userId = actorId,
                    userName = actorName,
                    timestampUtc = now,
                    reason = plan.OverrideReason
                } : null
            }, cancellationToken);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            var eligibleReviewerUserIds = (await GetEligibleApproverRoleCategoriesAsync(
                tenantId, projectId, cancellationToken)).Keys.ToHashSet();
            var notificationResult = await approvalNotifications.QueueActionableStepsAsync(
                project, item, approval, approverAssignments, Array.Empty<ApprovalAction>(),
                eligibleReviewerUserIds, cancellationToken);
            if (notificationResult.IsFailure)
                return Result<ProjectAnnualBudgetItemDto>.Failure(notificationResult.Error
                    ?? Error.Validation("Annual Budget approval notification could not be queued."));
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<ProjectAnnualBudgetItemDto>.Failure(Error.Conflict("The annual budget changed while being submitted."));
        }
        return await GetMappedItemAsync(tenantId, projectId, itemId, actorId, cancellationToken);
    }

    public async Task<Result<ProjectAnnualBudgetItemDto>> ReviseApprovedAsync(
        Guid tenantId, Guid projectId, Guid itemId, Guid actorId, string? actorName,
        ReviseProjectAnnualBudgetDto dto, CancellationToken cancellationToken)
    {
        var access = await ValidateAccessAsync(tenantId, projectId, actorId, AccessKind.ReviseApproved, cancellationToken);
        if (access.Error is not null)
            return Result<ProjectAnnualBudgetItemDto>.Failure(access.Error);
        if (dto.RowVersion is not { Length: > 0 } || !IsValidAmounts(dto.NewApprovedFunding, 0m, dto.ApprovedAdjustments))
            return Result<ProjectAnnualBudgetItemDto>.Failure(Error.Validation("Provide a current row version and valid funding and adjustment amounts."));
        if (string.IsNullOrWhiteSpace(dto.Reason))
            return Result<ProjectAnnualBudgetItemDto>.Failure(Error.Validation("A reason is required for an approved Annual Budget revision."));

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var project = await FindProjectQuery(tenantId, projectId).FirstOrDefaultAsync(cancellationToken);
        if (project is null) return Result<ProjectAnnualBudgetItemDto>.Failure(Error.NotFound("Project not found."));
        var item = await FindBudgetQuery(tenantId, projectId).FirstOrDefaultAsync(row => row.Id == itemId, cancellationToken);
        if (item is null) return Result<ProjectAnnualBudgetItemDto>.Failure(Error.NotFound("Annual budget not found."));
        if (item.Status != ProjectAnnualBudgetStatuses.Approved)
            return Result<ProjectAnnualBudgetItemDto>.Failure(Error.Conflict("Only an approved Annual Budget can be revised."));
        if (!item.RowVersion.SequenceEqual(dto.RowVersion))
            return Result<ProjectAnnualBudgetItemDto>.Failure(Error.Conflict("The Annual Budget changed. Refresh before preparing a revision."));
        if (item.ClosedAtUtc.HasValue || await HasClosedCarryForwardSourceAsync(tenantId, projectId, item.Id, cancellationToken))
            return Result<ProjectAnnualBudgetItemDto>.Failure(Error.Conflict("A closed Fiscal Year cannot be revised outside its governed correction process."));
        if (!string.Equals(item.Currency, NormalizeCurrency(project.Currency), StringComparison.Ordinal))
            return Result<ProjectAnnualBudgetItemDto>.Failure(Error.Conflict("The project currency has changed. Resolve the currency discrepancy before revising this Annual Budget."));
        if (await db.ApprovalRequests.IgnoreQueryFilters().AnyAsync(request => request.TenantId == tenantId && !request.IsDeleted
                && request.EntityType == EntityType && request.EntityId == item.Id && request.Type == RevisionApprovalType
                && request.Status == PaymentRequestStatuses.Pending, cancellationToken))
            return Result<ProjectAnnualBudgetItemDto>.Failure(Error.Conflict("An Annual Budget revision is already awaiting review."));
        var (planError, plan) = await ResolveEffectiveApprovalPlanAsync(
            tenantId, projectId, project, actorId, dto.ApprovalMode, dto.Approvers,
            dto.CustomizeForThisSubmission, dto.OverrideReason, dto.ApprovalConfigurationRowVersion,
            dto.ProjectSettingsRowVersion, cancellationToken);
        if (planError is not null)
            return Result<ProjectAnnualBudgetItemDto>.Failure(planError);
        if (plan is null)
            return Result<ProjectAnnualBudgetItemDto>.Failure(Error.Conflict("The approval plan could not be resolved."));
        var proposedAllocation = dto.NewApprovedFunding + dto.ApprovedAdjustments;
        var ceilingError = await ValidateCeilingCapacityAsync(tenantId, project, proposedAllocation, item.Id, null, cancellationToken);
        if (ceilingError is not null)
            return Result<ProjectAnnualBudgetItemDto>.Failure(ceilingError);

        var ordered = plan.Approvers;
        var proposal = new AnnualBudgetRevisionProposal(dto.NewApprovedFunding, dto.ApprovedAdjustments, dto.Reason.Trim());
        var now = DateTime.UtcNow;
        var approval = new ApprovalRequest
        {
            TenantId = tenantId,
            Title = $"Revise Annual Budget {item.FinancialReference}",
            Description = JsonSerializer.Serialize(proposal),
            EntityType = EntityType,
            EntityId = item.Id,
            EntityNumber = item.FinancialReference,
            Type = RevisionApprovalType,
            Status = PaymentRequestStatuses.Pending,
            Priority = "Normal",
            Amount = proposedAllocation,
            Currency = item.Currency,
            RequestedBy = actorId,
            RequestedAt = now,
            CurrentApproverId = string.Equals(plan.ApprovalMode, PaymentRequestApprovalModes.Sequential, StringComparison.OrdinalIgnoreCase)
                ? ordered[0].UserId : null,
            ApprovalLevel = 0,
            CreatedBy = actorId.ToString("D")
        };
        db.ApprovalRequests.Add(approval);
        var approverAssignments = ordered.Select(selection => new PaymentRequestApproverAssignment
        {
            TenantId = tenantId,
            ApprovalRequestId = approval.Id,
            ApproverUserId = selection.UserId,
            Position = selection.Position,
            Requirement = selection.Requirement,
            State = PaymentRequestAssignmentStates.Pending,
            CreatedBy = actorId.ToString("D")
        }).ToArray();
        db.PaymentRequestApproverAssignments.AddRange(approverAssignments);
        await AppendAuditAsync(item, project, actorId, actorName, "RevisionSubmitted", Snapshot(item),
            new
            {
                approvalRequestId = approval.Id,
                proposal,
                approvalMode = plan.ApprovalMode,
                approvalConfigurationSource = "ProjectSettings",
                defaultApprovalPlan = plan.DefaultPlan,
                effectiveApprovalPlan = plan.EffectivePlan,
                @override = plan.WasCustomized ? new
                {
                    userId = actorId,
                    userName = actorName,
                    timestampUtc = now,
                    reason = plan.OverrideReason
                } : null
            }, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        var eligibleReviewerUserIds = (await GetEligibleApproverRoleCategoriesAsync(
            tenantId, projectId, cancellationToken)).Keys.ToHashSet();
        var notificationResult = await approvalNotifications.QueueActionableStepsAsync(
            project, item, approval, approverAssignments, Array.Empty<ApprovalAction>(),
            eligibleReviewerUserIds, cancellationToken);
        if (notificationResult.IsFailure)
            return Result<ProjectAnnualBudgetItemDto>.Failure(notificationResult.Error
                ?? Error.Validation("Annual Budget revision notification could not be queued."));
        await transaction.CommitAsync(cancellationToken);
        return await GetMappedItemAsync(tenantId, projectId, itemId, actorId, cancellationToken);
    }

    public async Task<Result<ProjectAnnualBudgetItemDto>> DecideReviewAsync(
        Guid tenantId, Guid projectId, Guid itemId, Guid requestId, Guid actorId, string? actorName,
        DecideProjectAnnualBudgetReviewDto dto, CancellationToken cancellationToken)
    {
        var access = await ValidateAccessAsync(tenantId, projectId, actorId, AccessKind.Approve, cancellationToken);
        if (access.Error is not null)
            return Result<ProjectAnnualBudgetItemDto>.Failure(access.Error);
        var decision = dto.Decision is "Approved" or "Rejected" or "Returned" ? dto.Decision : string.Empty;
        if (decision.Length == 0)
            return Result<ProjectAnnualBudgetItemDto>.Failure(Error.Validation("Decision must be Approved, Rejected, or Returned."));
        if ((decision is "Rejected" or "Returned") && string.IsNullOrWhiteSpace(dto.Reason))
            return Result<ProjectAnnualBudgetItemDto>.Failure(Error.Validation(
                decision == "Rejected"
                    ? "A reason is required when rejecting an Annual Budget review."
                    : "A reason is required when returning an Annual Budget for changes."));

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var project = await FindProjectQuery(tenantId, projectId).FirstOrDefaultAsync(cancellationToken);
        if (project is null) return Result<ProjectAnnualBudgetItemDto>.Failure(Error.NotFound("Project not found."));
        var item = await FindBudgetQuery(tenantId, projectId).FirstOrDefaultAsync(row => row.Id == itemId, cancellationToken);
        if (item is null) return Result<ProjectAnnualBudgetItemDto>.Failure(Error.NotFound("Annual budget not found."));
        if (dto.BudgetRowVersion is not { Length: > 0 } || !item.RowVersion.SequenceEqual(dto.BudgetRowVersion))
            return Result<ProjectAnnualBudgetItemDto>.Failure(Error.Conflict("The Annual Budget changed. Refresh before deciding."));
        var approval = await db.ApprovalRequests.IgnoreQueryFilters().FirstOrDefaultAsync(request => request.TenantId == tenantId
            && !request.IsDeleted && request.Id == requestId && request.EntityType == EntityType && request.EntityId == itemId
            && (request.Type == ReviewApprovalType || request.Type == RevisionApprovalType), cancellationToken);
        if (approval is null || approval.Status != PaymentRequestStatuses.Pending)
            return Result<ProjectAnnualBudgetItemDto>.Failure(Error.Conflict("This Annual Budget is no longer awaiting review."));
        if (dto.ApprovalRowVersion is not { Length: > 0 } || !approval.RowVersion.SequenceEqual(dto.ApprovalRowVersion))
            return Result<ProjectAnnualBudgetItemDto>.Failure(Error.Conflict("The review changed. Refresh before deciding."));
        if (approval.RequestedBy == actorId)
            return Result<ProjectAnnualBudgetItemDto>.Failure(Error.Forbidden("The submitter cannot review the same Annual Budget."));

        var assignments = await db.PaymentRequestApproverAssignments.IgnoreQueryFilters()
            .Where(row => row.TenantId == tenantId && row.ApprovalRequestId == requestId && !row.IsDeleted)
            .OrderBy(row => row.Position).ToListAsync(cancellationToken);
        var actions = await db.ApprovalActions.IgnoreQueryFilters()
            .Where(row => row.TenantId == tenantId && row.ApprovalRequestId == requestId && !row.IsDeleted)
            .ToListAsync(cancellationToken);
        var assignment = assignments.FirstOrDefault(row => row.ApproverUserId == actorId && row.State == PaymentRequestAssignmentStates.Pending);
        if (assignment is null || actions.Any(action => action.ApproverAssignmentId == assignment.Id))
            return Result<ProjectAnnualBudgetItemDto>.Failure(Error.Forbidden("Only an assigned reviewer without a prior decision may act."));
        var eligible = (await GetEligibleApproverRoleCategoriesAsync(tenantId, projectId, cancellationToken)).Keys.ToHashSet();
        if (!eligible.Contains(actorId))
            return Result<ProjectAnnualBudgetItemDto>.Failure(Error.Forbidden(
                "The assigned reviewer must remain an active eligible Project Team member, Project Sponsor, or Project Manager with ProjectAnnualBudget.View and ProjectAnnualBudget.Approve."));
        var states = BuildApproverStates(assignments, actions);
        var sequential = assignments.Any(row => row.Position.HasValue);
        var mayDecide = sequential
            ? PaymentRequestWorkflowPolicy.CanDecideSequentially(actorId, approval.Status, states)
            : PaymentRequestWorkflowPolicy.CanDecideInParallel(actorId, approval.Status, states);
        if (!mayDecide)
            return Result<ProjectAnnualBudgetItemDto>.Failure(Error.Conflict("This reviewer is not currently eligible to decide."));

        var now = DateTime.UtcNow;
        assignment.State = decision == "Approved" ? PaymentRequestAssignmentStates.Approved : PaymentRequestAssignmentStates.Rejected;
        assignment.UpdatedBy = actorId.ToString("D");
        var decisionAction = new ApprovalAction
        {
            TenantId = tenantId,
            ApprovalRequestId = approval.Id,
            ApproverId = actorId,
            ApproverAssignmentId = assignment.Id,
            Action = decision,
            Comments = dto.Reason?.Trim(),
            ActionedAt = now,
            ApprovalLevel = sequential ? assignment.Position ?? 0 : 0,
            CreatedBy = actorId.ToString("D")
        };
        db.ApprovalActions.Add(decisionAction);
        actions.Add(decisionAction);

        if (decision == "Returned")
        {
            approval.Status = "Returned";
            approval.CurrentApproverId = null;
            approval.UpdatedBy = actorId.ToString("D");
            item.UpdatedAt = now;
            item.UpdatedBy = actorId.ToString("D");
            await AppendAuditAsync(item, project, actorId, actorName, "ReturnedForChanges", null,
                new { approvalRequestId = approval.Id, reason = dto.Reason!.Trim() }, cancellationToken);
        }
        else if (decision == "Rejected")
        {
            approval.Status = PaymentRequestStatuses.Rejected;
            approval.CurrentApproverId = null;
            approval.UpdatedBy = actorId.ToString("D");
            item.UpdatedAt = now;
            item.UpdatedBy = actorId.ToString("D");
            await AppendAuditAsync(item, project, actorId, actorName, "Rejected", null,
                new { approvalRequestId = approval.Id, reason = dto.Reason!.Trim() }, cancellationToken);
        }
        else
        {
            var projected = BuildApproverStates(assignments, actions, actorId, "Approved");
            var final = sequential
                ? PaymentRequestWorkflowPolicy.EvaluateSequentialStatus(projected)
                : PaymentRequestWorkflowPolicy.EvaluateParallelStatus(projected);
            approval.UpdatedBy = actorId.ToString("D");
            if (final == PaymentRequestStatuses.Approved)
            {
                if (approval.Type == RevisionApprovalType)
                {
                    AnnualBudgetRevisionProposal? proposal;
                    try { proposal = JsonSerializer.Deserialize<AnnualBudgetRevisionProposal>(approval.Description ?? string.Empty); }
                    catch (JsonException) { proposal = null; }
                    if (proposal is null)
                        return Result<ProjectAnnualBudgetItemDto>.Failure(Error.Conflict("The approved revision proposal is unreadable. No financial values were changed."));
                    var ceilingError = await ValidateCeilingCapacityAsync(tenantId, project,
                        proposal.NewApprovedFunding + proposal.ApprovedAdjustments, item.Id, approval.Id, cancellationToken);
                    if (ceilingError is not null) return Result<ProjectAnnualBudgetItemDto>.Failure(ceilingError);
                    var before = Snapshot(item);
                    item.NewApprovedFunding = proposal.NewApprovedFunding;
                    item.ApprovedAdjustments = proposal.ApprovedAdjustments;
                    item.ApprovedBy = actorId;
                    item.ApprovedAt = now;
                    item.UpdatedAt = now;
                    item.UpdatedBy = actorId.ToString("D");
                    await AppendAuditAsync(item, project, actorId, actorName, "Revised", before,
                        new { newApprovedFunding = item.NewApprovedFunding, approvedAdjustments = item.ApprovedAdjustments, reason = proposal.Reason }, cancellationToken);
                }
                else
                {
                    if (item.Status != ProjectAnnualBudgetStatuses.Draft)
                        return Result<ProjectAnnualBudgetItemDto>.Failure(Error.Conflict("Only a Draft annual budget can enter Approved status."));
                    var ceilingError = await ValidateCeilingCapacityAsync(tenantId, project,
                        item.NewApprovedFunding + item.ApprovedAdjustments, item.Id, approval.Id, cancellationToken);
                    if (ceilingError is not null) return Result<ProjectAnnualBudgetItemDto>.Failure(ceilingError);
                    var before = Snapshot(item);
                    item.Status = ProjectAnnualBudgetStatuses.Approved;
                    item.ApprovedBy = actorId;
                    item.ApprovedAt = now;
                    item.UpdatedAt = now;
                    item.UpdatedBy = actorId.ToString("D");
                    await AppendAuditAsync(item, project, actorId, actorName, "Approved", before, item, cancellationToken);
                }
                approval.Status = PaymentRequestStatuses.Approved;
                approval.CurrentApproverId = null;
            }
            else
            {
                approval.CurrentApproverId = sequential
                    ? PaymentRequestWorkflowPolicy.GetActiveSequentialApprover(projected)
                    : null;
            }
        }
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            var eligibleReviewerUserIds = (await GetEligibleApproverRoleCategoriesAsync(
                tenantId, projectId, cancellationToken)).Keys.ToHashSet();
            var notificationResult = await approvalNotifications.QueueActionableStepsAsync(
                project, item, approval, assignments, actions, eligibleReviewerUserIds, cancellationToken);
            if (notificationResult.IsFailure)
                return Result<ProjectAnnualBudgetItemDto>.Failure(notificationResult.Error
                    ?? Error.Validation("Annual Budget approval notification could not be queued."));
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<ProjectAnnualBudgetItemDto>.Failure(Error.Conflict("The Annual Budget review changed. Refresh before deciding."));
        }
        var historyActorIds = assignments.Select(row => row.ApproverUserId)
            .Append(approval.RequestedBy ?? Guid.Empty)
            .Concat(actions.Select(row => row.ApproverId))
            .Where(id => id != Guid.Empty).Distinct().ToArray();
        var historyUsers = historyActorIds.Length == 0 ? new List<User>()
            : await db.Users.IgnoreQueryFilters().AsNoTracking()
                .Where(user => user.TenantId == tenantId && historyActorIds.Contains(user.Id) && !user.IsDeleted)
                .ToListAsync(cancellationToken);
        var historyNames = historyUsers.ToDictionary(user => user.Id, user => user.FullName);
        return Result<ProjectAnnualBudgetItemDto>.Success(Map(
            item, false, false, false, true, actorId, new[] { approval }, assignments, actions, eligible, historyNames));
    }

    public async Task<Result<ProjectAnnualBudgetItemDto>> ApproveAsync(
        Guid tenantId,
        Guid projectId,
        Guid itemId,
        Guid actorId,
        string? actorName,
        ApproveProjectAnnualBudgetDto dto,
        CancellationToken cancellationToken)
    {
        if (dto.RowVersion is not { Length: > 0 })
            return Result<ProjectAnnualBudgetItemDto>.Failure(Error.Validation("A current row version is required."));
        var request = await db.ApprovalRequests.IgnoreQueryFilters().AsNoTracking()
            .Where(row => row.TenantId == tenantId && !row.IsDeleted && row.EntityType == EntityType
                && row.EntityId == itemId && (row.Type == ReviewApprovalType || row.Type == RevisionApprovalType)
                && row.Status == PaymentRequestStatuses.Pending)
            .OrderByDescending(row => row.RequestedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (request is null)
            return Result<ProjectAnnualBudgetItemDto>.Failure(Error.Conflict("An Annual Budget must be submitted and assigned for review before it can be approved."));
        return await DecideReviewAsync(tenantId, projectId, itemId, request.Id, actorId, actorName,
            new DecideProjectAnnualBudgetReviewDto("Approved", null, dto.RowVersion, request.RowVersion), cancellationToken);
    }

    private async Task<Result<ProjectAnnualBudgetItemDto>> GetMappedItemAsync(
        Guid tenantId, Guid projectId, Guid itemId, Guid actorId, CancellationToken cancellationToken)
    {
        var page = await GetPageAsync(tenantId, projectId, actorId, cancellationToken);
        if (page.IsFailure) return Result<ProjectAnnualBudgetItemDto>.Failure(page.Error!);
        var item = page.Value!.Items.FirstOrDefault(row => row.Id == itemId);
        return item is null
            ? Result<ProjectAnnualBudgetItemDto>.Failure(Error.NotFound("Annual budget not found."))
            : Result<ProjectAnnualBudgetItemDto>.Success(item);
    }

    private async Task<Error?> ValidateCeilingCapacityAsync(
        Guid tenantId, Project project, decimal proposedAllocation, Guid? excludedBudgetId,
        Guid? excludedApprovalId, CancellationToken cancellationToken)
    {
        if (!project.Budget.HasValue || project.Budget.Value < 0m)
            return Error.Conflict("Set an approved Project lifetime financial ceiling before preparing an Annual Budget allocation.");
        if (proposedAllocation < 0m)
            return Error.Validation("An Annual Budget allocation cannot be negative.");

        var budgets = await FindBudgetQuery(tenantId, project.Id).AsNoTracking().ToListAsync(cancellationToken);
        var projectCurrency = NormalizeCurrency(project.Currency);
        if (budgets.Any(item => !string.Equals(NormalizeCurrency(item.Currency), projectCurrency, StringComparison.Ordinal)))
            return Error.Conflict("Existing Annual Budgets use a different currency from the Project lifetime ceiling. Reconcile those records before changing allocations.");
        var approved = budgets.Where(item => item.Id != excludedBudgetId && item.Status == ProjectAnnualBudgetStatuses.Approved)
            .Sum(item => item.NewApprovedFunding + item.ApprovedAdjustments);
        var budgetById = budgets.ToDictionary(item => item.Id);
        var pending = await db.ApprovalRequests.IgnoreQueryFilters().AsNoTracking()
            .Where(request => request.TenantId == tenantId && !request.IsDeleted
                && request.Status == PaymentRequestStatuses.Pending
                && request.Id != excludedApprovalId && request.EntityType == EntityType
                && (request.Type == ReviewApprovalType || request.Type == RevisionApprovalType))
            .ToListAsync(cancellationToken);
        if (pending.Any(request => budgetById.ContainsKey(request.EntityId)
            && (!request.Amount.HasValue || request.Amount.Value < 0m
                || !string.Equals(NormalizeCurrency(request.Currency), projectCurrency, StringComparison.Ordinal))))
            return Error.Conflict("A pending Annual Budget review has an invalid amount or currency. Resolve the review before changing allocations.");
        var pendingReservation = pending.Sum(request =>
        {
            if (!budgetById.TryGetValue(request.EntityId, out var budget) || !request.Amount.HasValue)
                return 0m;
            if (request.Type == RevisionApprovalType && budget.Status == ProjectAnnualBudgetStatuses.Approved)
                return Math.Max(0m, request.Amount.Value - budget.NewApprovedFunding - budget.ApprovedAdjustments);
            return request.Type == ReviewApprovalType && budget.Status == ProjectAnnualBudgetStatuses.Draft
                ? request.Amount.Value : 0m;
        });
        if (approved + pendingReservation + proposedAllocation > project.Budget.Value)
            return Error.Conflict("The approved and submitted Annual Budget allocations would exceed the Project lifetime financial ceiling. Reduce this allocation or submit an approved ceiling revision first.");
        return null;
    }

    private async Task<Error?> ValidateFiscalYearOpeningAsync(
        Guid tenantId,
        Project project,
        int fiscalYear,
        int fiscalYearStartMonth,
        FiscalYearPeriod currentPeriod,
        IReadOnlyList<ProjectAnnualBudget> existingBudgets,
        CancellationToken cancellationToken)
    {
        if (existingBudgets.Count == 0)
        {
            return fiscalYear == currentPeriod.FiscalYear
                ? null
                : Error.Conflict($"The first Annual Budget must use the Tenant's current fiscal year (FY {currentPeriod.FiscalYear}). Refresh Finance and use the configured Tenant fiscal calendar.");
        }

        var previous = existingBudgets[0];
        if (fiscalYear <= previous.FiscalYear)
            return Error.Conflict($"An Annual Budget already exists through FY {previous.FiscalYear}. Earlier fiscal years cannot be added after later records; review the existing year history.");

        if (previous.FiscalYear >= 2200 || fiscalYear != previous.FiscalYear + 1)
            return Error.Conflict($"Annual Budgets must be opened in fiscal-year order. Complete the FY {previous.FiscalYear} closing before opening FY {previous.FiscalYear + 1}.");

        if (previous.Status != ProjectAnnualBudgetStatuses.Approved || !previous.ClosedAtUtc.HasValue)
            return Error.Conflict($"Approve and close FY {previous.FiscalYear} before opening FY {fiscalYear}. The approved closing creates the next-year Draft and supplies its carry-forward amount.");

        FiscalYearPeriod expectedPreviousPeriod;
        try
        {
            expectedPreviousPeriod = fiscalCalendar.ForFiscalYear(previous.FiscalYear, fiscalYearStartMonth);
        }
        catch (ArgumentOutOfRangeException)
        {
            return Error.Conflict("The prior Annual Budget fiscal-year boundaries cannot be validated against the current Tenant fiscal calendar. Reconcile the period before opening another year.");
        }

        if (previous.FiscalYearStartDate != expectedPreviousPeriod.StartDate
            || previous.FiscalYearEndDate != expectedPreviousPeriod.EndDate)
            return Error.Conflict($"The FY {previous.FiscalYear} Annual Budget dates do not match the Tenant fiscal calendar. Reconcile the period before opening FY {fiscalYear}.");

        var closing = await db.ProjectAnnualBudgetClosings.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.ProjectId == project.Id
                && item.SourceAnnualBudgetId == previous.Id && item.FiscalYear == previous.FiscalYear
                && item.DestinationFiscalYear == fiscalYear
                && item.Status == ProjectAnnualBudgetClosingStatuses.Closed && !item.IsDeleted,
                cancellationToken);
        if (closing is null)
            return Error.Conflict($"FY {previous.FiscalYear} has no approved closing. Complete and approve its Project Year-End Closing; the workflow will create the FY {fiscalYear} opening Draft with only the approved carry-forward amount.");

        if (!closing.DestinationAnnualBudgetId.HasValue
            || !string.Equals(NormalizeCurrency(closing.Currency), NormalizeCurrency(project.Currency), StringComparison.Ordinal)
            || closing.FiscalYearStartDate != expectedPreviousPeriod.StartDate
            || closing.FiscalYearEndDate != expectedPreviousPeriod.EndDate)
            return Error.Conflict($"The approved FY {previous.FiscalYear} closing has an invalid destination or period. Reconcile its opening record before creating an Annual Budget.");

        var destination = await db.ProjectAnnualBudgets.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.ProjectId == project.Id
                && item.Id == closing.DestinationAnnualBudgetId.Value, cancellationToken);
        if (destination is null)
            return Error.Conflict($"The approved FY {previous.FiscalYear} closing references a missing FY {fiscalYear} opening Draft. Reconcile the closing record; no carry-forward value was generated.");

        FiscalYearPeriod expectedDestinationPeriod;
        try
        {
            expectedDestinationPeriod = fiscalCalendar.ForFiscalYear(fiscalYear, fiscalYearStartMonth);
        }
        catch (ArgumentOutOfRangeException)
        {
            return Error.Conflict("The approved closing destination period cannot be validated against the Tenant fiscal calendar. Reconcile the opening record before proceeding.");
        }

        var approvedCarryForward = closing.ApprovedFreeCarryForward + closing.ApprovedReservedCarryForward;
        if (destination.FiscalYear != fiscalYear
            || destination.FiscalYearStartDate != expectedDestinationPeriod.StartDate
            || destination.FiscalYearEndDate != expectedDestinationPeriod.EndDate
            || !string.Equals(NormalizeCurrency(destination.Currency), NormalizeCurrency(closing.Currency), StringComparison.Ordinal)
            || destination.ApprovedCarryForward != approvedCarryForward
            || destination.ApprovedReservedCarryForward != closing.ApprovedReservedCarryForward
            || destination.NewApprovedFunding != closing.NextFiscalYearNewFunding
            || destination.ApprovedAdjustments != closing.NextFiscalYearAdjustments)
            return Error.Conflict($"The approved FY {previous.FiscalYear} closing destination does not match its approved carry-forward, New Funding, adjustments, or Tenant fiscal period. Reconcile the opening record before proceeding.");

        return Error.Conflict($"FY {fiscalYear} is governed by the approved FY {previous.FiscalYear} closing. Refresh Annual Budgets and use the closing-created Draft so approved carry-forward, New Funding, and adjustments remain linked to that closing.");
    }

    private Task<bool> HasClosedCarryForwardSourceAsync(
        Guid tenantId, Guid projectId, Guid annualBudgetId, CancellationToken cancellationToken) =>
        db.ProjectAnnualBudgetClosings.IgnoreQueryFilters().AnyAsync(closing =>
            closing.TenantId == tenantId && closing.ProjectId == projectId
            && closing.DestinationAnnualBudgetId == annualBudgetId
            && closing.Status == ProjectAnnualBudgetClosingStatuses.Closed && !closing.IsDeleted,
            cancellationToken);

    private async Task<Dictionary<Guid, IReadOnlyList<string>>> GetEligibleApproverRoleCategoriesAsync(
        Guid tenantId, Guid projectId, CancellationToken cancellationToken)
    {
        var participants = await ProjectTeamApproverEligibility.GetActiveLinkedUserIdsAsync(
            db, tenantId, projectId, cancellationToken);
        var projectAssignments = await FindProjectQuery(tenantId, projectId).AsNoTracking()
            .Select(project => new { project.ProjectManagerId, project.SponsorId })
            .FirstOrDefaultAsync(cancellationToken);
        if (projectAssignments is not null)
        {
            if (projectAssignments.ProjectManagerId.HasValue) participants.Add(projectAssignments.ProjectManagerId.Value);
            if (projectAssignments.SponsorId.HasValue) participants.Add(projectAssignments.SponsorId.Value);
        }

        if (participants.Count == 0) return [];

        var roleAssignments = await db.UserRoles.IgnoreQueryFilters().AsNoTracking()
            .Where(assignment => assignment.TenantId == tenantId && !assignment.IsDeleted
                && participants.Contains(assignment.UserId)
                && assignment.User.TenantId == tenantId && assignment.User.IsActive && !assignment.User.IsDeleted
                && assignment.Role.TenantId == tenantId && !assignment.Role.IsDeleted)
            .Select(assignment => new { assignment.UserId, RoleCategory = assignment.Role.Category })
            .ToListAsync(cancellationToken);
        var qualifyingCategories = roleAssignments
            .GroupBy(assignment => assignment.UserId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<string>)group
                .Select(assignment => assignment.RoleCategory)
                .Where(category => !string.IsNullOrWhiteSpace(category))
                .Select(category => category!)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(category => category, StringComparer.Ordinal)
                .ToArray());

        var result = new Dictionary<Guid, IReadOnlyList<string>>();
        foreach (var (userId, categories) in qualifyingCategories)
        {
            var effective = await permissions.GetEffectivePermissionsAsync(userId, tenantId, cancellationToken);
            if (effective.Contains(ViewPermission) && effective.Contains(ApprovePermission))
                result.Add(userId, categories);
        }
        return result;
    }

    private static IReadOnlyList<PaymentRequestApproverState> BuildApproverStates(
        IReadOnlyCollection<PaymentRequestApproverAssignment> assignments,
        IReadOnlyCollection<ApprovalAction> actions,
        Guid? projectedActor = null,
        string? projectedDecision = null) => assignments.Select(assignment =>
        {
            var action = actions.FirstOrDefault(candidate => candidate.ApproverAssignmentId == assignment.Id);
            var decision = action?.Action switch
            {
                "Approve" or "Approved" => PaymentRequestDecisions.Approved,
                "Reject" or "Rejected" or "Return" or "Returned" => PaymentRequestDecisions.Rejected,
                _ => assignment.State == PaymentRequestAssignmentStates.Approved
                    ? PaymentRequestDecisions.Approved
                    : assignment.State == PaymentRequestAssignmentStates.Rejected ? PaymentRequestDecisions.Rejected : null
            };
            if (projectedActor == assignment.ApproverUserId) decision = projectedDecision;
            return new PaymentRequestApproverState(assignment.ApproverUserId, assignment.Position, assignment.Requirement, decision);
        }).ToArray();

    private static bool TryResolveAnnualBudgetReviewValues(
        ApprovalRequest approval,
        ProjectAnnualBudget budget,
        out decimal newFunding,
        out decimal carryForward,
        out decimal adjustments,
        out string? reason) => TryResolveAnnualBudgetReviewValues(
            approval.Type, approval.Description, budget.NewApprovedFunding, budget.ApprovedCarryForward,
            budget.ApprovedAdjustments, approval.Amount, out newFunding, out carryForward,
            out adjustments, out reason, out _);

    private static bool TryResolveAnnualBudgetReviewValues(
        string requestType,
        string? description,
        decimal currentNewFunding,
        decimal approvedCarryForward,
        decimal currentAdjustments,
        decimal? persistedRequestAmount,
        out decimal newFunding,
        out decimal carryForward,
        out decimal adjustments,
        out string? reason,
        out decimal? totalRequested)
    {
        carryForward = approvedCarryForward;
        reason = null;
        totalRequested = null;
        if (requestType == RevisionApprovalType)
        {
            AnnualBudgetRevisionProposal? proposal;
            try { proposal = JsonSerializer.Deserialize<AnnualBudgetRevisionProposal>(description ?? string.Empty); }
            catch (JsonException) { proposal = null; }
            if (proposal is null)
            {
                newFunding = 0m;
                adjustments = 0m;
                return false;
            }
            newFunding = proposal.NewApprovedFunding;
            adjustments = proposal.ApprovedAdjustments;
            reason = proposal.Reason;
            totalRequested = newFunding + carryForward + adjustments;
            return true;
        }

        if (requestType != ReviewApprovalType)
        {
            newFunding = 0m;
            adjustments = 0m;
            return false;
        }

        newFunding = currentNewFunding;
        adjustments = currentAdjustments;
        totalRequested = newFunding + carryForward + adjustments;
        // The persisted approval amount is only a reservation value and may
        // exclude carry-forward. Annual Budget display totals come from its
        // independent fiscal-year row, never Project.Budget.
        _ = persistedRequestAmount;
        return true;
    }

    private static string? NormalizeAnnualBudgetDecision(string? decision) => decision switch
    {
        "Approve" or "Approved" => "Approved",
        "Reject" or "Rejected" => "Rejected",
        "Return" or "Returned" => "Returned",
        _ => null
    };

    private static ProjectAnnualBudgetItemDto Map(
        ProjectAnnualBudget item, bool canEditDraft, bool canSubmit, bool canRevise, bool canApprove, Guid actorId,
        IReadOnlyCollection<ApprovalRequest> approvals,
        IReadOnlyCollection<PaymentRequestApproverAssignment> assignments,
        IReadOnlyCollection<ApprovalAction> actions,
        IReadOnlySet<Guid> eligibleApprovers,
        IReadOnlyDictionary<Guid, string> names,
        bool canDeleteDraft = false,
        bool canWithdrawSubmission = false,
        bool hasProtectedFinancialDependency = false,
        bool hasPendingAssignmentStep = false,
        string? authorizationBlockReason = null,
        string? financialDependencyReason = null)
    {
        var itemApprovals = approvals.Where(request => request.EntityId == item.Id)
            .OrderBy(request => request.RequestedAt).ToArray();
        var latestInitial = itemApprovals.LastOrDefault(request => request.Type == ReviewApprovalType);
        var latestRevision = itemApprovals.LastOrDefault(request => request.Type == RevisionApprovalType);
        var latest = item.Status == ProjectAnnualBudgetStatuses.Approved && latestRevision is not null
            ? latestRevision : latestInitial ?? latestRevision;
        var latestAssignments = latest is null ? Array.Empty<PaymentRequestApproverAssignment>() : assignments.Where(row => row.ApprovalRequestId == latest.Id).ToArray();
        var latestActions = latest is null ? Array.Empty<ApprovalAction>() : actions.Where(row => row.ApprovalRequestId == latest.Id).ToArray();
        var isPending = latest?.Status == PaymentRequestStatuses.Pending;
        var pendingApprovals = itemApprovals.Where(request => request.Status == PaymentRequestStatuses.Pending).ToArray();
        var canReview = isPending && canApprove && latest!.RequestedBy != actorId && eligibleApprovers.Contains(actorId)
            && latestAssignments.Any(row => row.ApproverUserId == actorId && row.State == PaymentRequestAssignmentStates.Pending
                && latestActions.All(action => action.ApproverAssignmentId != row.Id));
        if (canReview && latestAssignments.Any(row => row.Position.HasValue))
            canReview = PaymentRequestWorkflowPolicy.CanDecideSequentially(actorId, latest!.Status,
                BuildApproverStates(latestAssignments, latestActions));
        var reviewStatus = item.Status == ProjectAnnualBudgetStatuses.Approved
            ? latestRevision?.Status switch
            {
                PaymentRequestStatuses.Pending => "RevisionPending",
                "Returned" => "RevisionReturned",
                PaymentRequestStatuses.Rejected => "RevisionRejected",
                _ => "Approved"
            }
            : latestInitial?.Status == PaymentRequestStatuses.Pending ? "Pending"
                : latestInitial?.Status == "Returned" ? "Returned"
                : latestInitial?.Status == "Rejected" ? "Rejected" : "Draft";
        var history = itemApprovals.SelectMany(request =>
        {
            var submitted = new ProjectAnnualBudgetReviewEventDto(
                request.Type == RevisionApprovalType ? "RevisionSubmitted" : "Submitted",
                request.RequestedBy.HasValue ? names.GetValueOrDefault(request.RequestedBy.Value) : null,
                request.RequestedAt, null, null);
            var decisions = actions.Where(action => action.ApprovalRequestId == request.Id)
                .OrderBy(action => action.ActionedAt)
                .Select(action => new ProjectAnnualBudgetReviewEventDto(
                    action.Action,
                    names.GetValueOrDefault(action.ApproverId),
                    action.ActionedAt,
                    action.Comments,
                    action.ApprovalLevel));
            return new[] { submitted }.Concat(decisions);
        }).OrderByDescending(item => item.TimestampUtc).ToArray();
        var canEdit = canEditDraft && item.Status == ProjectAnnualBudgetStatuses.Draft && !isPending;
        var canSubmitForReview = canSubmit && item.Status == ProjectAnnualBudgetStatuses.Draft && !isPending;
        var canSubmitRevision = canRevise && item.Status == ProjectAnnualBudgetStatuses.Approved && !item.ClosedAtUtc.HasValue
            && !itemApprovals.Any(request => request.Type == RevisionApprovalType && request.Status == PaymentRequestStatuses.Pending);
        var approvalMode = latestAssignments.Any(row => row.Position.HasValue)
            ? PaymentRequestApprovalModes.Sequential
            : latestAssignments.Length > 0 ? PaymentRequestApprovalModes.Parallel : null;
        var canDelete = canDeleteDraft && item.Status == ProjectAnnualBudgetStatuses.Draft
            && pendingApprovals.Length == 0 && !hasPendingAssignmentStep && !hasProtectedFinancialDependency;
        var hasClosingMetadata = item.ClosedByClosingId.HasValue || item.ClosedAtUtc.HasValue || item.ClosedBy.HasValue
            || item.ApprovedCarryForward != 0m || item.ApprovedReservedCarryForward != 0m;
        var deleteDraftBlockReason = authorizationBlockReason
            ?? (item.Status != ProjectAnnualBudgetStatuses.Draft ? "not-draft"
            : pendingApprovals.Length > 0 ? "pending-review"
            : hasPendingAssignmentStep ? "approval-step-active"
            : hasClosingMetadata ? "closing-metadata"
            : financialDependencyReason ?? (hasProtectedFinancialDependency ? "financial-dependency" : null));
        var canWithdraw = canWithdrawSubmission && pendingApprovals.Length == 1
            && ((pendingApprovals[0].Type == ReviewApprovalType && item.Status == ProjectAnnualBudgetStatuses.Draft)
                || (pendingApprovals[0].Type == RevisionApprovalType && item.Status == ProjectAnnualBudgetStatuses.Approved));
        return new ProjectAnnualBudgetItemDto(
            item.Id, item.ProjectId, item.FiscalYear, $"FY {item.FiscalYear}", item.FiscalYearStartDate,
            item.FiscalYearEndDate, item.FinancialReference, item.Currency, item.NewApprovedFunding,
            item.ApprovedCarryForward, item.ApprovedReservedCarryForward, item.ApprovedFreeCarryForward,
            item.ApprovedAdjustments, item.TotalAnnualBudget, item.Status, item.ApprovedBy, item.ApprovedAt,
            item.CreatedAt, item.UpdatedAt, item.RowVersion, item.ClosedByClosingId, item.ClosedAtUtc,
            item.ClosedBy, item.ClosedAtUtc.HasValue, canEdit, canReview, reviewStatus, approvalMode,
            latest?.Id, latest?.RowVersion, latest?.RequestedBy, latest?.RequestedAt, canSubmitForReview, canReview, canSubmitRevision, history)
        {
            CanDeleteDraft = canDelete,
            CanWithdrawSubmission = canWithdraw,
            DeleteDraftBlockReason = deleteDraftBlockReason
        };
    }

    private async Task<BudgetAccess> ValidateAccessAsync(
        Guid tenantId, Guid projectId, Guid actorId, AccessKind kind, CancellationToken cancellationToken)
    {
        var current = management.Current;
        var scopeAllowsProject = current?.Scope.ProjectIds.Contains(projectId) == true;
        if (tenantId == Guid.Empty || actorId == Guid.Empty || current is null
            || current.TenantId != tenantId || current.UserId != actorId
            || !scopeAllowsProject)
            return BudgetAccess.Denied(Error.NotFound("Project not found."));

        var effective = await permissions.GetEffectivePermissionsAsync(actorId, tenantId, cancellationToken);
        var canWrite = !current.EffectiveReadOnly;
        var canCreate = canWrite && effective.Contains(CreatePermission);
        var canEditDraft = canWrite && effective.Contains(EditDraftPermission);
        var canSubmit = canWrite && effective.Contains(SubmitPermission);
        var canRevise = canWrite && effective.Contains(ReviseApprovedPermission);
        var canRead = effective.Contains(ViewPermission);
        var hasDeletePermission = effective.Contains(DeletePermission);
        var canDeleteDraft = canWrite && canRead && hasDeletePermission;
        var deleteDraftAuthorizationBlockReason = !canRead ? "view-required"
            : !hasDeletePermission ? "permission-required"
            : !canWrite ? "scope-read-only" : null;
        var canWithdrawSubmission = canWrite && effective.Contains(WithdrawPermission);
        var canViewProjectSettings = effective.Contains(ProjectSettingsViewPermission);
        var canEditProjectSettings = canWrite && canViewProjectSettings && effective.Contains(ProjectSettingsEditPermission);
        var canManageApprovalConfiguration = canEditProjectSettings && effective.Contains(ApprovalConfigurationPermission);
        var canApprove = canWrite && effective.Contains(ApprovePermission);

        var error = kind switch
        {
            AccessKind.Read when !canRead => Error.Forbidden("ProjectAnnualBudget.View is required."),
            AccessKind.Create when !canCreate => Error.Forbidden("ProjectAnnualBudget.Create is required."),
            AccessKind.EditDraft when !canEditDraft => Error.Forbidden("ProjectAnnualBudget.EditDraft is required."),
            AccessKind.SubmitForReview when !canSubmit => Error.Forbidden("ProjectAnnualBudget.SubmitForReview is required."),
            AccessKind.ReviseApproved when !canRevise => Error.Forbidden("ProjectAnnualBudget.ReviseApproved is required."),
            AccessKind.DeleteDraft when !canRead => Error.Forbidden("ProjectAnnualBudget.View is required to delete an Annual Budget Draft."),
            AccessKind.DeleteDraft when !canWrite => Error.Forbidden("A writable Project Management scope is required to delete an Annual Budget Draft."),
            AccessKind.DeleteDraft when !hasDeletePermission => Error.Forbidden("ProjectAnnualBudget.Delete is required."),
            AccessKind.WithdrawSubmission when !canWithdrawSubmission => Error.Forbidden("ProjectAnnualBudget.Withdraw is required."),
            AccessKind.ViewApprovalConfiguration when !canRead || !canViewProjectSettings => Error.Forbidden("ProjectAnnualBudget.View and ProjectSettings.View are required to view approval configuration."),
            AccessKind.ManageApprovalConfiguration when !canManageApprovalConfiguration => Error.Forbidden("ProjectSettings.View, ProjectSettings.Edit, and ProjectAnnualBudget.ManageApprovalConfiguration are required."),
            AccessKind.Approve when !canRead => Error.Forbidden("ProjectAnnualBudget.View is required to review an assigned Annual Budget."),
            AccessKind.Approve when !canApprove => Error.Forbidden("ProjectAnnualBudget.Approve is required."),
            _ => null
        };
        return new BudgetAccess(error, canCreate || canEditDraft || canSubmit || canRevise || canManageApprovalConfiguration,
            canApprove, canCreate, canEditDraft, canSubmit, canRevise, canManageApprovalConfiguration,
            canDeleteDraft, canWithdrawSubmission, deleteDraftAuthorizationBlockReason);
    }

    private IQueryable<Project> FindProjectQuery(Guid tenantId, Guid projectId) => db.Projects.IgnoreQueryFilters()
        .Where(project => project.TenantId == tenantId && project.Id == projectId && !project.IsDeleted);

    private IQueryable<ProjectAnnualBudget> FindBudgetQuery(Guid tenantId, Guid projectId) => db.ProjectAnnualBudgets.IgnoreQueryFilters()
        .Where(item => item.TenantId == tenantId && item.ProjectId == projectId && !item.IsDeleted);

    private async Task<bool> IsConfiguredCurrencyAsync(string currency, CancellationToken cancellationToken) =>
        currency.Length == 3
        && currency.All(character => character is >= 'A' and <= 'Z')
        && await db.Set<Currency>().IgnoreQueryFilters().AnyAsync(
            item => item.Code == currency && !item.IsDeleted,
            cancellationToken);

    private async Task AppendAuditAsync(
        ProjectAnnualBudget item,
        Project project,
        Guid actorId,
        string? actorName,
        string action,
        object? before,
        object after,
        CancellationToken cancellationToken,
        DateTime? timestampUtc = null)
    {
        var beforeValues = before is null ? null : JsonSerializer.Serialize(before);
        var afterValues = JsonSerializer.Serialize(after);
        await audit.AppendAsync(new AuditEntry
        {
            TenantId = item.TenantId,
            ProjectId = item.ProjectId,
            EntityType = EntityType,
            EntityId = item.Id,
            Action = action,
            UserId = actorId,
            UserName = actorName,
            Timestamp = timestampUtc ?? DateTime.UtcNow,
            Description = action switch
            {
                "Created" => $"Annual budget draft {item.FinancialReference} created for {project.Name}.",
                "Updated" => $"Annual budget draft {item.FinancialReference} updated for {project.Name}.",
                "SubmittedForReview" => $"Annual budget {item.FinancialReference} submitted for review for {project.Name}.",
                "RevisionSubmitted" => $"Annual budget revision submitted for {item.FinancialReference} for {project.Name}.",
                "ReturnedForChanges" => $"Annual budget {item.FinancialReference} returned for changes for {project.Name}.",
                "Rejected" => $"Annual budget {item.FinancialReference} rejected for {project.Name}.",
                "Revised" => $"Approved annual budget {item.FinancialReference} revised for {project.Name}.",
                "DeletedDraft" => $"Annual budget draft {item.FinancialReference} for FY {item.FiscalYear} deleted by {actorName ?? actorId.ToString("D")}.",
                "SubmissionWithdrawn" => $"Annual budget submission {item.FinancialReference} for FY {item.FiscalYear} withdrawn by {actorName ?? actorId.ToString("D")}.",
                _ => $"Annual budget {item.FinancialReference} approved for {project.Name}."
            },
            Changes = JsonSerializer.Serialize(BuildChanges(before as BudgetSnapshot, item)),
            OriginalValues = beforeValues,
            NewValues = afterValues,
            CreatedBy = actorId.ToString("D")
        }, cancellationToken);
    }

    private static object BuildChanges(BudgetSnapshot? before, ProjectAnnualBudget after)
    {
        var current = Snapshot(after);
        return new
        {
            financialReference = Change(before?.FinancialReference, current.FinancialReference),
            fiscalYear = Change(before?.FiscalYear, current.FiscalYear),
            currency = Change(before?.Currency, current.Currency),
            newFunding = Change(before?.NewApprovedFunding, current.NewApprovedFunding),
            approvedCarryForward = Change(before?.ApprovedCarryForward, current.ApprovedCarryForward),
            approvedReservedCarryForward = Change(before?.ApprovedReservedCarryForward, current.ApprovedReservedCarryForward),
            approvedAdjustments = Change(before?.ApprovedAdjustments, current.ApprovedAdjustments),
            totalAnnualBudget = Change(before?.TotalAnnualBudget, current.TotalAnnualBudget),
            amount = Change(before?.TotalAnnualBudget, current.TotalAnnualBudget),
            status = Change(before?.Status, current.Status),
            approvedBy = Change(before?.ApprovedBy, current.ApprovedBy),
            approvedAtUtc = Change(before?.ApprovedAt, current.ApprovedAt),
            draftApprovalConfiguration = Change(before?.DraftApprovalConfigurationJson, current.DraftApprovalConfigurationJson),
            isDeleted = Change(before?.IsDeleted, current.IsDeleted)
        };
    }

    private static BudgetSnapshot Snapshot(ProjectAnnualBudget item) => new(
        item.FinancialReference,
        item.FiscalYear,
        item.Currency,
        item.NewApprovedFunding,
        item.ApprovedCarryForward,
        item.ApprovedReservedCarryForward,
        item.ApprovedFreeCarryForward,
        item.ApprovedAdjustments,
        item.TotalAnnualBudget,
        item.Status,
        item.ApprovedBy,
        item.ApprovedAt,
        item.DraftApprovalConfigurationJson,
        item.IsDeleted);

    private static object Change<T>(T? from, T? to) => new { from, to };

    private static ProjectAnnualBudgetItemDto Map(
        ProjectAnnualBudget item, bool canEditDraft, bool canSubmit, bool canRevise) => new(
        item.Id,
        item.ProjectId,
        item.FiscalYear,
        $"FY {item.FiscalYear}",
        item.FiscalYearStartDate,
        item.FiscalYearEndDate,
        item.FinancialReference,
        item.Currency,
        item.NewApprovedFunding,
        item.ApprovedCarryForward,
        item.ApprovedReservedCarryForward,
        item.ApprovedFreeCarryForward,
        item.ApprovedAdjustments,
        item.TotalAnnualBudget,
        item.Status,
        item.ApprovedBy,
        item.ApprovedAt,
        item.CreatedAt,
        item.UpdatedAt,
        item.RowVersion,
        item.ClosedByClosingId,
        item.ClosedAtUtc,
        item.ClosedBy,
        item.ClosedAtUtc.HasValue,
        canEditDraft && item.Status == ProjectAnnualBudgetStatuses.Draft,
        false,
        item.Status == ProjectAnnualBudgetStatuses.Approved ? "Approved" : "Draft",
        null,
        null,
        null,
        item.ApprovedBy,
        item.ApprovedAt,
        canSubmit && item.Status == ProjectAnnualBudgetStatuses.Draft,
        false,
        canRevise && item.Status == ProjectAnnualBudgetStatuses.Approved && !item.ClosedAtUtc.HasValue,
        []);

    private static bool IsValidAmounts(decimal funding, decimal carryForward, decimal adjustments)
    {
        const decimal maximum = 999999999999999.9999m;
        return funding >= 0m && carryForward >= 0m
            && funding <= maximum && carryForward <= maximum
            && adjustments >= -maximum && adjustments <= maximum
            && funding + carryForward + adjustments is >= 0m and <= maximum
            && decimal.Round(funding, 4) == funding
            && decimal.Round(carryForward, 4) == carryForward
            && decimal.Round(adjustments, 4) == adjustments;
    }

    private static string NormalizeCurrency(string? value) => (value ?? string.Empty).Trim().ToUpperInvariant();

    private sealed record AnnualBudgetRevisionProposal(
        decimal NewApprovedFunding,
        decimal ApprovedAdjustments,
        string Reason);

    private sealed record EffectiveAnnualBudgetApprovalPlan(
        string ApprovalMode,
        PaymentRequestApproverSelectionDto[] Approvers,
        AnnualBudgetApprovalPlanAuditSnapshot DefaultPlan,
        AnnualBudgetApprovalPlanAuditSnapshot EffectivePlan,
        bool WasCustomized,
        string? OverrideReason);

    private sealed record AnnualBudgetApprovalPlanAuditSnapshot(
        string ConfigurationSource,
        string? ProjectApprovalMode,
        string? ApprovalMode,
        string? ConfigurationRowVersion,
        IReadOnlyList<PaymentRequestApproverSelectionDto> Approvers);

    private static string MakeReference(string? projectCode, int fiscalYear, Guid id)
    {
        var code = string.IsNullOrWhiteSpace(projectCode) ? "PROJECT" : projectCode.Trim();
        var suffix = id.ToString("N");
        var availableCodeLength = Math.Max(1, 99 - suffix.Length - fiscalYear.ToString().Length - 10);
        if (code.Length > availableCodeLength) code = code[..availableCodeLength];
        return $"{code}-FY{fiscalYear}-AB-{suffix}";
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: 2601 or 2627 };

    private enum AccessKind { Read, ViewApprovalConfiguration, Create, EditDraft, SubmitForReview, ReviseApproved, DeleteDraft, WithdrawSubmission, ManageApprovalConfiguration, Approve }
    private sealed record BudgetAccess(
        Error? Error,
        bool CanWrite,
        bool CanApprove,
        bool CanCreate,
        bool CanEditDraft,
        bool CanSubmit,
        bool CanRevise,
        bool CanManageApprovalConfiguration,
        bool CanDeleteDraft,
        bool CanWithdrawSubmission,
        string? DeleteDraftAuthorizationBlockReason)
    {
        public static BudgetAccess Denied(Error error) => new(error, false, false, false, false, false, false, false, false, false, "permission-required");
    }

    private sealed record BudgetSnapshot(
        string FinancialReference,
        int FiscalYear,
        string Currency,
        decimal NewApprovedFunding,
        decimal ApprovedCarryForward,
        decimal ApprovedReservedCarryForward,
        decimal ApprovedFreeCarryForward,
        decimal ApprovedAdjustments,
        decimal TotalAnnualBudget,
        string Status,
        Guid? ApprovedBy,
        DateTime? ApprovedAt,
        string? DraftApprovalConfigurationJson,
        bool IsDeleted);
}
