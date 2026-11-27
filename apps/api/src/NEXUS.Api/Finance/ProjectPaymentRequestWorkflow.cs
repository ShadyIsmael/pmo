using System.Data;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using NEXUS.Modules.Approvals.Domain;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.CostManagement.Application;
using NEXUS.Modules.CostManagement.Application.DTOs;
using NEXUS.Modules.CostManagement.Domain;
using NEXUS.Modules.Identity.Application;
using NEXUS.Modules.Identity.Domain;
using NEXUS.Modules.Organization.Application.Settings;
using NEXUS.Modules.Organization.Domain;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.Modules.ProjectPlanning.Domain;
using NEXUS.Modules.ResourceManagement.Domain;
using NEXUS.Persistence;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Management;

namespace NEXUS.Api.Finance;

/// <summary>Project-only command/query implementation over the shared approval and audit stores.</summary>
public sealed partial class ProjectPaymentRequestWorkflow(
    AppDbContext db,
    IProjectMilestoneReferenceLookup milestoneLookup,
    ITenantFiscalYearService tenantFiscalYear,
    IFiscalYearCalendarService fiscalYearCalendar,
    IUserEffectivePermissionService permissions,
    IManagementContextAccessor management,
    IPlannedPaymentService plannedPayments,
    IProjectPaymentRequestApprovalNotificationPublisher? approvalNotifications = null)
{
    private const string EntityType = "ProjectPaymentRequest";
    private const string RequestType = "ProjectPaymentRequest";
    private const string ViewPermission = "ProjectPaymentRequest.View";
    private const string SettlementViewPermission = "ProjectSettlement.View";
    private const string CreatePermission = "ProjectPaymentRequest.Create";
    private const string EditDraftPermission = "ProjectPaymentRequest.EditDraft";
    private const string SubmitPermission = "ProjectPaymentRequest.Submit";
    private const string CancelPermission = "ProjectPaymentRequest.Cancel";
    private const string ApprovePermission = "ProjectPaymentRequest.Approve";
    private const string SettlementRecordPermission = "PaymentSettlement.Record";

    public async Task<Result<IReadOnlyList<PaymentRequestEligibleApproverDto>>> GetEligibleApproversAsync(
        Guid tenantId, Guid projectId, CancellationToken cancellationToken)
    {
        var access = await ValidateServiceAccessAsync(tenantId, projectId, management.Current?.UserId ?? Guid.Empty,
            requireWrite: true, cancellationToken, actionPermission: SubmitPermission);
        if (access.Error is not null)
            return Result<IReadOnlyList<PaymentRequestEligibleApproverDto>>.Failure(access.Error);
        var projectExists = await FindProjectQuery(tenantId, projectId).AnyAsync(cancellationToken);
        if (!projectExists) return Result<IReadOnlyList<PaymentRequestEligibleApproverDto>>.Failure(Error.NotFound("Project not found"));

        var candidateIds = await ProjectTeamApproverEligibility.GetActiveLinkedUserIdsAsync(
            db, tenantId, projectId, cancellationToken);
        var items = await db.Users.IgnoreQueryFilters().AsNoTracking()
            .Where(user => candidateIds.Contains(user.Id) && user.TenantId == tenantId
                && user.IsActive && !user.IsDeleted)
            .OrderBy(user => user.FirstName)
            .ThenBy(user => user.LastName)
            .Select(user => new { user.Id, user.FirstName, user.LastName, user.Email })
            .ToListAsync(cancellationToken);

        var eligible = new List<PaymentRequestEligibleApproverDto>();
        foreach (var item in items.GroupBy(candidate => candidate.Id).Select(group => group.First()))
        {
            var effective = await permissions.GetEffectivePermissionsAsync(item.Id, tenantId, cancellationToken);
            if (!effective.Contains(ApprovePermission)) continue;
            eligible.Add(new PaymentRequestEligibleApproverDto(
                item.Id,
                string.IsNullOrWhiteSpace($"{item.FirstName} {item.LastName}".Trim()) ? item.Email : $"{item.FirstName} {item.LastName}".Trim()));
        }

        return Result<IReadOnlyList<PaymentRequestEligibleApproverDto>>.Success(eligible);
    }

    public async Task<Result<IReadOnlyList<PaymentRequestMilestoneOptionDto>>> GetActiveMilestonesAsync(
        Guid tenantId, Guid projectId, CancellationToken cancellationToken)
    {
        var access = await ValidateServiceAccessAsync(tenantId, projectId, management.Current?.UserId ?? Guid.Empty,
            requireWrite: true, cancellationToken, actionPermission: CreatePermission);
        if (access.Error is not null)
            return Result<IReadOnlyList<PaymentRequestMilestoneOptionDto>>.Failure(access.Error);
        if (!await FindProjectQuery(tenantId, projectId).AnyAsync(cancellationToken))
            return Result<IReadOnlyList<PaymentRequestMilestoneOptionDto>>.Failure(Error.NotFound("Project not found"));

        var items = await db.PlanActivities.IgnoreQueryFilters().AsNoTracking()
            .Where(activity => activity.TenantId == tenantId
                && !activity.IsDeleted
                && activity.Type == PlanActivityType.Milestone
                && activity.PlanVersion != null
                && !activity.PlanVersion.IsDeleted
                && activity.PlanVersion.Type == PlanVersionType.Working
                && activity.PlanVersion.Status == PlanVersionStatus.Active
                && activity.PlanVersion.Plan != null
                && !activity.PlanVersion.Plan.IsDeleted
                && activity.PlanVersion.Plan.TenantId == tenantId
                && activity.PlanVersion.Plan.ProjectId == projectId)
            .OrderBy(activity => activity.EndDate)
            .ThenBy(activity => activity.Name)
            .Select(activity => new PaymentRequestMilestoneOptionDto(activity.Id, activity.Name, activity.EndDate))
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<PaymentRequestMilestoneOptionDto>>.Success(items);
    }

    public async Task<Result<ProjectPaymentRequestPageDto>> GetPageAsync(
        Guid tenantId, Guid projectId, Guid actorId, int page, int pageSize, string? status, CancellationToken cancellationToken)
    {
        var access = await ValidateServiceAccessAsync(tenantId, projectId, actorId, requireWrite: false, cancellationToken);
        if (access.Error is not null)
            return Result<ProjectPaymentRequestPageDto>.Failure(access.Error);
        var project = await FindProjectQuery(tenantId, projectId).FirstOrDefaultAsync(cancellationToken);
        if (project is null)
            return Result<ProjectPaymentRequestPageDto>.Failure(Error.NotFound("Project not found"));

        var effective = await permissions.GetEffectivePermissionsAsync(actorId, tenantId, cancellationToken);
        var writableContext = management.Current?.EffectiveReadOnly != true;
        var currentProjectManager = await IsCurrentProjectManagerAsync(tenantId, project, actorId, cancellationToken);
        var canCreate = writableContext && effective.Contains(CreatePermission) && currentProjectManager;
        var canEdit = writableContext && effective.Contains(EditDraftPermission);
        var canSubmit = writableContext && effective.Contains(SubmitPermission) && currentProjectManager;
        var canApprove = writableContext && effective.Contains(ApprovePermission);
        var canCancel = writableContext && effective.Contains(CancelPermission);

        var now = DateTimeOffset.UtcNow;
        var period = await tenantFiscalYear.ResolveForTimestampAsync(tenantId, now, cancellationToken);
        var budgetAvailability = await GetBudgetAvailabilityAsync(tenantId, project, period, actorId,
            canCreate, cancellationToken);

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = from detail in PaymentDetailsQuery(tenantId, projectId)
                    join request in PaymentRequestsQuery(tenantId, projectId) on detail.ApprovalRequestId equals request.Id
                    select new { detail, request };
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(row => row.request.Status == status.Trim());

        var total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderByDescending(row => row.request.RequestedAt)
            .ThenByDescending(row => row.request.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        var requestIds = rows.Select(row => row.request.Id).ToArray();
        var assignments = requestIds.Length == 0
            ? []
            : await db.PaymentRequestApproverAssignments.IgnoreQueryFilters().AsNoTracking()
                .Where(item => item.TenantId == tenantId && !item.IsDeleted && requestIds.Contains(item.ApprovalRequestId))
                .OrderBy(item => item.Position)
                .ThenBy(item => item.CreatedAt)
                .ToListAsync(cancellationToken);
        var actions = requestIds.Length == 0
            ? []
            : await db.ApprovalActions.IgnoreQueryFilters().AsNoTracking()
                .Where(item => item.TenantId == tenantId && !item.IsDeleted && requestIds.Contains(item.ApprovalRequestId))
                .ToListAsync(cancellationToken);
        var userIds = rows.Select(row => row.request.RequestedBy ?? Guid.Empty)
            .Concat(assignments.Select(item => item.ApproverUserId))
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToArray();
        var names = await LoadUserNamesAsync(tenantId, userIds, cancellationToken);
        var budgetIds = rows.Select(row => row.detail.ProjectAnnualBudgetId).Where(id => id.HasValue)
            .Select(id => id!.Value).Distinct().ToArray();
        var budgets = budgetIds.Length == 0
            ? new Dictionary<Guid, ProjectAnnualBudget>()
            : await db.ProjectAnnualBudgets.IgnoreQueryFilters().AsNoTracking()
                .Where(item => item.TenantId == tenantId && item.ProjectId == projectId && budgetIds.Contains(item.Id))
                .ToDictionaryAsync(item => item.Id, cancellationToken);
        var lineIds = rows.Select(row => row.detail.ProjectAnnualBudgetLineId).Where(id => id.HasValue)
            .Select(id => id!.Value).Distinct().ToArray();
        var budgetLines = lineIds.Length == 0
            ? new Dictionary<Guid, ProjectAnnualBudgetLine>()
            : await db.ProjectAnnualBudgetLines.IgnoreQueryFilters().AsNoTracking()
                .Where(item => item.TenantId == tenantId && item.ProjectId == projectId && lineIds.Contains(item.Id))
                .ToDictionaryAsync(item => item.Id, cancellationToken);
        var list = rows.Select(row => ToListItem(row.request, row.detail,
            assignments.Where(item => item.ApprovalRequestId == row.request.Id
                && item.ApprovalCycleId == row.detail.CurrentApprovalCycleId).ToArray(),
            actions.Where(item => item.ApprovalRequestId == row.request.Id).ToArray(),
            names, actorId, canEdit, canSubmit, canApprove, canCancel,
            row.detail.ProjectAnnualBudgetId.HasValue && budgets.TryGetValue(row.detail.ProjectAnnualBudgetId.Value, out var budget)
                ? budget
                : null,
            row.detail.ProjectAnnualBudgetLineId.HasValue && budgetLines.TryGetValue(row.detail.ProjectAnnualBudgetLineId.Value, out var line)
                ? line
                : null)).ToArray();

        return Result<ProjectPaymentRequestPageDto>.Success(new ProjectPaymentRequestPageDto(list, page, pageSize, total, budgetAvailability));
    }

    public async Task<Result<ProjectPaymentRequestDetailDto>> GetByIdAsync(
        Guid tenantId, Guid projectId, Guid requestId, Guid actorId, CancellationToken cancellationToken)
    {
        if (!await HasProjectReadOrReviewerScopeAsync(tenantId, projectId, requestId, actorId, cancellationToken))
            return Result<ProjectPaymentRequestDetailDto>.Failure(Error.NotFound("Payment Request not found"));
        var row = await FindRequestAsync(tenantId, projectId, requestId, cancellationToken);
        if (row is null) return Result<ProjectPaymentRequestDetailDto>.Failure(Error.NotFound("Payment Request not found"));
        return Result<ProjectPaymentRequestDetailDto>.Success(await ToDetailAsync(row.Value.Request, row.Value.Detail, actorId, cancellationToken));
    }

    public async Task<Result<ProjectPaymentRequestSettlementPageDto>> GetSettlementsAsync(
        Guid tenantId, Guid projectId, Guid requestId, Guid actorId, int page, int pageSize, CancellationToken cancellationToken)
    {
        var access = await ValidateServiceAccessAsync(tenantId, projectId, actorId, requireWrite: false, cancellationToken,
            additionalViewPermissions: [SettlementViewPermission]);
        if (access.Error is not null)
            return Result<ProjectPaymentRequestSettlementPageDto>.Failure(access.Error);
        var row = await FindRequestAsync(tenantId, projectId, requestId, cancellationToken);
        if (row is null)
            return Result<ProjectPaymentRequestSettlementPageDto>.Failure(Error.NotFound("Payment Request not found."));
        if (row.Value.Request.Amount is not > 0m)
            return Result<ProjectPaymentRequestSettlementPageDto>.Failure(Error.Conflict("The Payment Request does not have a valid approved amount."));

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = db.PaymentSettlements.IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.TenantId == tenantId
                && item.ProjectId == projectId
                && item.ProjectPaymentRequestId == requestId
                && !item.IsDeleted);
        var total = await query.CountAsync(cancellationToken);
        var balanceResult = await GetReversalBalanceAsync(
            tenantId, projectId, row.Value.Request, row.Value.Detail, cancellationToken);
        if (balanceResult.Error is not null)
            return Result<ProjectPaymentRequestSettlementPageDto>.Failure(balanceResult.Error);
        if (balanceResult.Balance is null)
            return Result<ProjectPaymentRequestSettlementPageDto>.Failure(Error.Conflict("The Payment Request settlement balance could not be verified."));
        var items = await query.OrderByDescending(item => item.SettlementDate)
            .ThenByDescending(item => item.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        var fiscalMonth = await tenantFiscalYear.GetStartMonthAsync(tenantId, cancellationToken);
        var mapped = items.Select(item => MapPaymentRequestSettlement(
            item, row.Value.Request, fiscalYearCalendar.Resolve(DateOnly.FromDateTime(item.SettlementDate), fiscalMonth).FiscalYear)).ToArray();
        var balance = balanceResult.Balance;
        var summary = new ProjectPaymentRequestSettlementSummaryDto(
            row.Value.Request.Id,
            Reference(row.Value.Request),
            row.Value.Request.Status,
            balance.OriginalAmount,
            balance.TotalSettled,
            balance.TotalReleased,
            balance.EffectiveActiveAllocation,
            balance.RemainingPayable,
            SettlementStatus(balance.TotalSettled, balance.RemainingPayable),
            string.IsNullOrWhiteSpace(row.Value.Request.Currency) ? string.Empty : NormalizeCurrency(row.Value.Request.Currency),
            row.Value.Detail.ProjectAnnualBudgetId,
            row.Value.Detail.AnnualBudgetFiscalYear,
            row.Value.Detail.AnnualBudgetReference);
        return Result<ProjectPaymentRequestSettlementPageDto>.Success(
            new ProjectPaymentRequestSettlementPageDto(summary, mapped, page, pageSize, total));
    }

    public async Task<Result<ProjectPaymentRequestSettlementDto>> RecordSettlementAsync(
        Guid tenantId, Guid projectId, Guid requestId, Guid actorId, string? actorName,
        RecordProjectPaymentSettlementDto dto, CancellationToken cancellationToken)
    {
        var access = await ValidateServiceAccessAsync(tenantId, projectId, actorId, requireWrite: true, cancellationToken,
            actionPermission: SettlementRecordPermission);
        if (access.Error is not null)
            return Result<ProjectPaymentRequestSettlementDto>.Failure(access.Error);
        if (dto.IdempotencyKey == Guid.Empty)
            return Result<ProjectPaymentRequestSettlementDto>.Failure(Error.Validation("A settlement idempotency key is required."));
        if (dto.Amount <= 0m || decimal.Round(dto.Amount, 4) != dto.Amount)
            return Result<ProjectPaymentRequestSettlementDto>.Failure(Error.Validation(
                "Settlement amount must be positive and use no more than four decimal places."));
        if (dto.PaymentDate == default)
            return Result<ProjectPaymentRequestSettlementDto>.Failure(Error.Validation("Payment date is required."));
        var settlementReference = dto.PaymentReference?.Trim() ?? string.Empty;
        var description = dto.Description?.Trim();
        if (settlementReference.Length is 0 or > 100)
            return Result<ProjectPaymentRequestSettlementDto>.Failure(Error.Validation("A settlement reference of up to 100 characters is required."));
        if (string.IsNullOrWhiteSpace(description) || description.Length > 2000)
            return Result<ProjectPaymentRequestSettlementDto>.Failure(Error.Validation("A settlement description of up to 2000 characters is required."));

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var project = await FindProjectForUpdateAsync(tenantId, projectId, cancellationToken);
        if (project is null)
            return Result<ProjectPaymentRequestSettlementDto>.Failure(Error.NotFound("Project not found."));
        var accessError = await ValidateActiveActorAsync(tenantId, actorId, cancellationToken);
        if (accessError is not null)
            return Result<ProjectPaymentRequestSettlementDto>.Failure(accessError);
        if (!await IsCurrentProjectManagerAsync(tenantId, project, actorId, cancellationToken))
            return Result<ProjectPaymentRequestSettlementDto>.Failure(Error.Forbidden("Only the current Project Manager may record a Payment Settlement."));
        var row = await FindRequestTrackedAsync(tenantId, projectId, requestId, cancellationToken);
        if (row is null)
            return Result<ProjectPaymentRequestSettlementDto>.Failure(Error.NotFound("Payment Request not found."));
        var (request, detail) = row.Value;
        if (request.Status != PaymentRequestStatuses.Approved)
            return Result<ProjectPaymentRequestSettlementDto>.Failure(Error.Conflict("Only an Approved Payment Request can receive an actual settlement."));
        if (request.Amount is not > 0m || decimal.Round(request.Amount.Value, 4) != request.Amount.Value)
            return Result<ProjectPaymentRequestSettlementDto>.Failure(Error.Conflict("The approved Payment Request amount is invalid."));
        var currency = string.IsNullOrWhiteSpace(request.Currency) ? string.Empty : NormalizeCurrency(request.Currency);
        if (currency.Length != 3 || !currency.All(character => character is >= 'A' and <= 'Z'))
            return Result<ProjectPaymentRequestSettlementDto>.Failure(Error.Conflict("The approved Payment Request has no valid currency."));

        var existingKey = await db.PaymentSettlements.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.IdempotencyKey == dto.IdempotencyKey, cancellationToken);
        if (existingKey is not null)
        {
            var sameCommand = existingKey.ProjectPaymentRequestId == requestId
                && existingKey.ProjectId == projectId
                && existingKey.PaymentObligationId is null
                && existingKey.Amount == dto.Amount
                && DateOnly.FromDateTime(existingKey.SettlementDate) == dto.PaymentDate
                && existingKey.PaymentReference == settlementReference
                && existingKey.Currency == currency
                && existingKey.Description == description;
            if (sameCommand)
            {
                await transaction.CommitAsync(cancellationToken);
                var existingPeriod = await tenantFiscalYear.ResolveForDateAsync(tenantId, dto.PaymentDate, cancellationToken);
                return Result<ProjectPaymentRequestSettlementDto>.Success(
                    MapPaymentRequestSettlement(existingKey, request, existingPeriod.FiscalYear));
            }
            return Result<ProjectPaymentRequestSettlementDto>.Failure(Error.Conflict(
                "This settlement idempotency key was already used for a different financial command."));
        }

        if (!await HasValidFundingTransferChainAsync(tenantId, projectId, requestId, cancellationToken))
            return Result<ProjectPaymentRequestSettlementDto>.Failure(Error.Conflict("The Payment Request funding-transfer history is inconsistent. Reconcile it before recording another settlement."));
        var currentTransfer = await GetCurrentFundingTransferAsync(tenantId, projectId, requestId, cancellationToken);
        if (currentTransfer is not null && currentTransfer.PaymentRequestId != requestId)
            return Result<ProjectPaymentRequestSettlementDto>.Failure(Error.Conflict("The active funding transfer does not belong to this Payment Request."));
        var fundingBudgetId = currentTransfer?.DestinationAnnualBudgetId ?? detail.ProjectAnnualBudgetId;
        if (!fundingBudgetId.HasValue)
            return Result<ProjectPaymentRequestSettlementDto>.Failure(Error.Conflict("This historical Payment Request has no verified current funding assignment."));
        var fundingBudget = await FindAnnualBudgetForUpdateAsync(tenantId, projectId, fundingBudgetId.Value, cancellationToken);
        if (fundingBudget is null || fundingBudget.Status != ProjectAnnualBudgetStatuses.Approved
            || fundingBudget.ClosedAtUtc.HasValue
            || !string.Equals(NormalizeCurrency(fundingBudget.Currency), currency, StringComparison.Ordinal))
            return Result<ProjectPaymentRequestSettlementDto>.Failure(Error.Conflict("The active funding Annual Budget must be approved, open, and use the Payment Request currency before settlement."));
        var period = await tenantFiscalYear.ResolveForDateAsync(tenantId, dto.PaymentDate, cancellationToken);
        if (period.FiscalYear != fundingBudget.FiscalYear)
            return Result<ProjectPaymentRequestSettlementDto>.Failure(Error.Conflict("The actual payment date must fall within the Fiscal Year currently funding this commitment."));

        var duplicateReference = await db.PaymentSettlements.IgnoreQueryFilters().AnyAsync(item =>
            item.TenantId == tenantId && item.ProjectId == projectId && item.ProjectPaymentRequestId == requestId
            && item.PaymentReference == settlementReference && !item.IsDeleted, cancellationToken);
        if (duplicateReference)
            return Result<ProjectPaymentRequestSettlementDto>.Failure(Error.Conflict(
                "This settlement reference is already used for the Payment Request."));

        var balanceResult = await GetReversalBalanceAsync(tenantId, projectId, request, detail, cancellationToken);
        if (balanceResult.Error is not null)
            return Result<ProjectPaymentRequestSettlementDto>.Failure(balanceResult.Error);
        if (balanceResult.Balance is null)
            return Result<ProjectPaymentRequestSettlementDto>.Failure(Error.Conflict("The Payment Request settlement balance could not be verified."));
        var settledBefore = balanceResult.Balance.TotalSettled;
        var effectiveAllocationBefore = balanceResult.Balance.EffectiveActiveAllocation;
        var outstandingBefore = balanceResult.Balance.RemainingPayable;
        if (outstandingBefore <= 0m || dto.Amount > outstandingBefore)
            return Result<ProjectPaymentRequestSettlementDto>.Failure(Error.Conflict(
                "Settlement amount exceeds the remaining approved Payment Request amount."));

        var beforeStatus = SettlementStatus(settledBefore, outstandingBefore);
        var settledAfter = settledBefore + dto.Amount;
        var outstandingAfter = Math.Max(0m, effectiveAllocationBefore - settledAfter);
        var afterStatus = SettlementStatus(settledAfter, outstandingAfter);
        var now = DateTime.UtcNow;
        var settlement = new PaymentSettlement
        {
            TenantId = tenantId,
            CreatedBy = actorId.ToString("D"),
            ProjectPaymentRequestId = requestId,
            PlannedPaymentInstallmentId = detail.PlannedPaymentInstallmentId,
            FundingTransferId = currentTransfer?.Id,
            ProjectId = projectId,
            IdempotencyKey = dto.IdempotencyKey,
            Amount = dto.Amount,
            Currency = currency,
            SettlementDate = dto.PaymentDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            PaymentReference = settlementReference,
            Description = description,
            RowVersion = Guid.NewGuid().ToByteArray(),
            CreatedAt = now
        };
        db.PaymentSettlements.Add(settlement);
        AddSettlementAudit(settlement, request, detail, actorId, actorName, now,
            settledBefore, settledAfter, outstandingBefore, outstandingAfter, beforeStatus, afterStatus, period.FiscalYear);

        if (detail.PlannedPaymentInstallmentId.HasValue)
        {
            var installment = await db.PlannedPaymentInstallments.IgnoreQueryFilters()
                .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.ProjectId == projectId
                    && item.Id == detail.PlannedPaymentInstallmentId.Value && !item.IsDeleted, cancellationToken);
            if (installment is null)
                return Result<ProjectPaymentRequestSettlementDto>.Failure(Error.Conflict(
                    "The Payment Request references a Planned Payment installment that no longer exists."));
            var settledByInstallment = await ActualPaidSettlementCalculator.LoadForInstallmentsAsync(
                db, tenantId, projectId, [installment.Id], ignoreQueryFilters: true, cancellationToken);
            installment.ActualPaid = settledByInstallment.GetValueOrDefault(installment.Id) + dto.Amount;
            installment.UpdatedBy = actorId.ToString("D");
            installment.UpdatedAt = now;
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<ProjectPaymentRequestSettlementDto>.Failure(Error.Conflict(
                "The Payment Request balance changed while recording this settlement. Refresh and try again."));
        }
        catch (DbUpdateException)
        {
            return Result<ProjectPaymentRequestSettlementDto>.Failure(Error.Conflict(
                "The settlement reference or idempotency key was used by another request. Refresh the settlement history."));
        }

        return Result<ProjectPaymentRequestSettlementDto>.Success(
            MapPaymentRequestSettlement(settlement, request, period.FiscalYear));
    }

    public async Task<Result<QuickOneOffPaymentRequestResultDto>> CreateQuickOneOffAsync(
        Guid tenantId,
        Guid projectId,
        Guid actorId,
        string? actorName,
        CreateQuickOneOffPaymentRequestDto dto,
        CancellationToken cancellationToken)
    {
        var serviceAccess = await ValidateServiceAccessAsync(tenantId, projectId, actorId, requireWrite: true,
            cancellationToken, actionPermission: CreatePermission);
        if (serviceAccess.Error is not null)
            return Result<QuickOneOffPaymentRequestResultDto>.Failure(serviceAccess.Error);
        if (dto.IdempotencyKey == Guid.Empty)
            return Result<QuickOneOffPaymentRequestResultDto>.Failure(Error.Validation(
                "A Quick One-off idempotency key is required."));
        if (!IsValidVersionlessAmount(dto.Amount))
            return Result<QuickOneOffPaymentRequestResultDto>.Failure(Error.Validation(
                "Amount must be positive and use no more than four decimal places."));
        if (dto.DueDateUtc.Kind != DateTimeKind.Utc)
            return Result<QuickOneOffPaymentRequestResultDto>.Failure(Error.Validation(
                "The installment due date must be UTC.", nameof(dto.DueDateUtc)));

        // This is the sole transaction owner for the composite operation. The
        // existing registration, draft creation, and submission services join it.
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var plannedResult = await plannedPayments.RegisterAsync(
                tenantId,
                projectId,
                actorId,
                actorName,
                new CreatePlannedPaymentDto(dto.Description, new[]
                {
                    new CreatePlannedPaymentInstallmentDto(
                        dto.Amount, dto.DueDateUtc, dto.ProjectPlanMilestoneId, dto.Purpose)
                }, dto.IdempotencyKey),
                cancellationToken);
            if (plannedResult.IsFailure)
                return Result<QuickOneOffPaymentRequestResultDto>.Failure(plannedResult.Error!);
            var plannedPayment = plannedResult.Value!;
            if (plannedPayment.Installments.Count != 1)
                return Result<QuickOneOffPaymentRequestResultDto>.Failure(Error.Conflict(
                    "A Quick One-off Planned Payment must contain exactly one installment."));
            var installment = plannedPayment.Installments[0];

            var requestResult = await CreateAsync(tenantId, projectId, actorId, actorName,
                new CreatePaymentRequestDto(
                    dto.Description,
                    dto.Amount,
                    dto.Purpose,
                    dto.ProjectPlanMilestoneId,
                    dto.IdempotencyKey,
                    installment.Id,
                    dto.ProjectAnnualBudgetId,
                    dto.ProjectAnnualBudgetLineId),
                cancellationToken);
            if (requestResult.IsFailure)
                return Result<QuickOneOffPaymentRequestResultDto>.Failure(requestResult.Error!);
            var paymentRequest = requestResult.Value!;

            var oneOffDetail = await db.ProjectPaymentRequestDetails.IgnoreQueryFilters()
                .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.ProjectId == projectId
                    && item.ApprovalRequestId == paymentRequest.Id, cancellationToken);
            if (oneOffDetail is null)
                return Result<QuickOneOffPaymentRequestResultDto>.Failure(Error.Conflict(
                    "The Quick One-off Payment Request detail was not persisted."));
            oneOffDetail.CreatedFromQuickOneOff = true;
            await db.SaveChangesAsync(cancellationToken);

            if (dto.SubmitForApproval)
            {
                var submissionResult = await SubmitAsync(tenantId, projectId, paymentRequest.Id, actorId, actorName,
                    new SubmitPaymentRequestDto(
                        dto.ApprovalMode ?? string.Empty,
                        dto.Approvers ?? Array.Empty<PaymentRequestApproverSelectionDto>(),
                        paymentRequest.RowVersion,
                        dto.IdempotencyKey),
                    cancellationToken);
                if (submissionResult.IsFailure)
                    return Result<QuickOneOffPaymentRequestResultDto>.Failure(submissionResult.Error!);
                paymentRequest = submissionResult.Value!;
            }

            await transaction.CommitAsync(cancellationToken);
            return Result<QuickOneOffPaymentRequestResultDto>.Success(
                new QuickOneOffPaymentRequestResultDto(plannedPayment, installment, paymentRequest));
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<QuickOneOffPaymentRequestResultDto>.Failure(Error.Conflict(
                "The project funding or payment request changed while creating this Quick One-off. Refresh and retry."));
        }
        catch (DbUpdateException)
        {
            return Result<QuickOneOffPaymentRequestResultDto>.Failure(Error.Conflict(
                "A Quick One-off with this idempotency key was created concurrently. Retry with the same key to retrieve it."));
        }
    }

    public async Task<Result<ProjectPaymentRequestDetailDto>> CreateAsync(
        Guid tenantId, Guid projectId, Guid actorId, string? actorName, CreatePaymentRequestDto dto, CancellationToken cancellationToken)
    {
        var serviceAccess = await ValidateServiceAccessAsync(tenantId, projectId, actorId, requireWrite: true, cancellationToken,
            actionPermission: CreatePermission);
        if (serviceAccess.Error is not null)
            return Result<ProjectPaymentRequestDetailDto>.Failure(serviceAccess.Error);
        if (!IsValidVersionlessAmount(dto.Amount))
            return Result<ProjectPaymentRequestDetailDto>.Failure(Error.Validation("Amount must be positive and use no more than four decimal places."));
        if (dto.IdempotencyKey is null || dto.IdempotencyKey == Guid.Empty)
            return Result<ProjectPaymentRequestDetailDto>.Failure(Error.Validation("A request idempotency key is required."));

        // Join a coordinator-owned transaction for Quick One-off create; standalone
        // Payment Request creation remains a serializable transaction of its own.
        await using var transaction = db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            : null;
        var project = await FindProjectForUpdateAsync(tenantId, projectId, cancellationToken);
        if (project is null) return Result<ProjectPaymentRequestDetailDto>.Failure(Error.NotFound("Project not found"));
        var accessError = await ValidateActiveActorAsync(tenantId, actorId, cancellationToken);
        if (accessError is not null) return Result<ProjectPaymentRequestDetailDto>.Failure(accessError);
        if (!await IsCurrentProjectManagerAsync(tenantId, project, actorId, cancellationToken))
            return Result<ProjectPaymentRequestDetailDto>.Failure(Error.Forbidden("Only the current Project Manager may create a Payment Request."));

        var title = dto.Title?.Trim() ?? string.Empty;
        var reason = dto.Reason?.Trim() ?? string.Empty;
        var validation = PaymentRequestWorkflowPolicy.ValidateDraft(dto with { Title = title, Reason = reason }, project.Currency);
        if (validation.Count > 0) return Result<ProjectPaymentRequestDetailDto>.Failure(Error.Validation(string.Join(",", validation)));
        var currency = NormalizeCurrency(project.Currency);
        if (!await IsConfiguredCurrencyAsync(currency, cancellationToken))
            return Result<ProjectPaymentRequestDetailDto>.Failure(Error.Conflict("The project currency is not configured in the currency catalog."));
        if (!dto.PlannedPaymentInstallmentId.HasValue || dto.PlannedPaymentInstallmentId == Guid.Empty
            || !dto.ProjectAnnualBudgetId.HasValue || dto.ProjectAnnualBudgetId == Guid.Empty
            || !dto.ProjectAnnualBudgetLineId.HasValue || dto.ProjectAnnualBudgetLineId == Guid.Empty)
            return Result<ProjectPaymentRequestDetailDto>.Failure(Error.Validation(
                "A Planned Payment installment, approved Annual Budget, and exactly one Budget Line are required."));
        if (dto.ProjectPlanMilestoneId.HasValue
            && !await milestoneLookup.IsActiveMilestoneForProjectAsync(projectId, dto.ProjectPlanMilestoneId.Value, cancellationToken))
            return Result<ProjectPaymentRequestDetailDto>.Failure(Error.Validation("The selected milestone is not active in a working plan for this project."));

        var fingerprint = Fingerprint(title, dto.Amount, reason, dto.ProjectPlanMilestoneId,
            dto.PlannedPaymentInstallmentId, dto.ProjectAnnualBudgetId, dto.ProjectAnnualBudgetLineId);
        var priorDetail = await db.ProjectPaymentRequestDetails.IgnoreQueryFilters()
            .FirstOrDefaultAsync(item => item.TenantId == tenantId
                && item.ProjectId == projectId
                && item.IdempotencyKey == dto.IdempotencyKey,
                cancellationToken);
        if (priorDetail is not null)
        {
            if (!string.Equals(priorDetail.RequestFingerprint, fingerprint, StringComparison.Ordinal))
                return Result<ProjectPaymentRequestDetailDto>.Failure(Error.Conflict("The idempotency key was already used for different request content."));
            var priorRequest = await PaymentRequestsQuery(tenantId, projectId)
                .FirstOrDefaultAsync(item => item.Id == priorDetail.ApprovalRequestId, cancellationToken);
            if (priorRequest is null) return Result<ProjectPaymentRequestDetailDto>.Failure(Error.NotFound("Payment Request not found"));
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
            return Result<ProjectPaymentRequestDetailDto>.Success(await ToDetailAsync(priorRequest, priorDetail, actorId, cancellationToken));
        }

        var requestedAt = DateTimeOffset.UtcNow;
        var annualBudget = await FindAnnualBudgetForUpdateAsync(
            tenantId, projectId, dto.ProjectAnnualBudgetId!.Value, cancellationToken);
        if (annualBudget is null)
            return Result<ProjectPaymentRequestDetailDto>.Failure(Error.Validation("The selected Annual Budget is not available for this project."));
        var budgetError = ValidateSelectedAnnualBudgetAssociation(project, annualBudget);
        if (budgetError is not null)
            return Result<ProjectPaymentRequestDetailDto>.Failure(budgetError);
        var budgetLine = await db.ProjectAnnualBudgetLines.IgnoreQueryFilters()
            .FirstOrDefaultAsync(item => item.Id == dto.ProjectAnnualBudgetLineId!.Value
                && item.TenantId == tenantId && item.ProjectId == projectId
                && item.ProjectAnnualBudgetId == annualBudget.Id && !item.IsDeleted, cancellationToken);
        if (budgetLine is null)
            return Result<ProjectPaymentRequestDetailDto>.Failure(Error.Validation(
                "The selected Budget Line does not belong to the selected Annual Budget."));

        var request = new ApprovalRequest
        {
            TenantId = tenantId,
            CreatedBy = actorId.ToString("D"),
            Title = title,
            Description = reason,
            EntityType = "Project",
            EntityId = projectId,
            Type = RequestType,
            Status = PaymentRequestStatuses.Draft,
            Priority = "Medium",
            Amount = dto.Amount,
            Currency = currency,
            RequestedBy = actorId,
            RequestedAt = requestedAt.UtcDateTime,
            EntityNumber = string.Empty
        };
        request.EntityNumber = MakeReference(request.Id);
        var detail = new ProjectPaymentRequestDetail
        {
            ApprovalRequestId = request.Id,
            TenantId = tenantId,
            ProjectId = projectId,
            ProjectAnnualBudgetId = annualBudget.Id,
            ProjectAnnualBudgetLineId = budgetLine.Id,
            AnnualBudgetFiscalYear = annualBudget.FiscalYear,
            AnnualBudgetReference = annualBudget.FinancialReference,
            PlanActivityId = dto.ProjectPlanMilestoneId,
            PlannedPaymentInstallmentId = dto.PlannedPaymentInstallmentId,
            RequestKind = ProjectPaymentRequestKinds.PaymentRequest,
            IdempotencyKey = dto.IdempotencyKey,
            RequestFingerprint = fingerprint
        };

        db.ApprovalRequests.Add(request);
        db.ProjectPaymentRequestDetails.Add(detail);
        AddAudit(project, request, actorId, actorName, "Created", "Payment Request created", null,
            AuditValues(request, detail, new
            {
                annualBudgetId = annualBudget.Id, fiscalYear = annualBudget.FiscalYear,
                budgetLineId = budgetLine.Id, budgetLineReference = budgetLine.Reference,
                plannedPaymentInstallmentId = dto.PlannedPaymentInstallmentId
            }),
            "financialReference,amount,currency,status,plannedPaymentInstallmentId,annualBudgetId,fiscalYear,annualBudgetLineId");

        if (dto.PlannedPaymentInstallmentId.HasValue)
        {
            var lockResult = await plannedPayments.ApplyPaymentRequestAsync(
                tenantId, projectId, dto.PlannedPaymentInstallmentId.Value, request.Id,
                dto.Amount, actorId, actorName, cancellationToken);
            if (lockResult.IsFailure) return Result<ProjectPaymentRequestDetailDto>.Failure(lockResult.Error!);
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<ProjectPaymentRequestDetailDto>.Failure(Error.Conflict("The project or request changed. Refresh and retry."));
        }

        return Result<ProjectPaymentRequestDetailDto>.Success(await ToDetailAsync(request, detail, actorId, cancellationToken));
    }

    public async Task<Result<ProjectPaymentRequestDetailDto>> UpdateAsync(
        Guid tenantId, Guid projectId, Guid requestId, Guid actorId, string? actorName, UpdatePaymentRequestDto dto, CancellationToken cancellationToken)
    {
        var serviceAccess = await ValidateServiceAccessAsync(tenantId, projectId, actorId, requireWrite: true, cancellationToken,
            actionPermission: EditDraftPermission);
        if (serviceAccess.Error is not null)
            return Result<ProjectPaymentRequestDetailDto>.Failure(serviceAccess.Error);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var project = await FindProjectForUpdateAsync(tenantId, projectId, cancellationToken);
        if (project is null) return Result<ProjectPaymentRequestDetailDto>.Failure(Error.NotFound("Project not found"));
        var accessError = await ValidateActiveActorAsync(tenantId, actorId, cancellationToken);
        if (accessError is not null) return Result<ProjectPaymentRequestDetailDto>.Failure(accessError);
        var row = await FindRequestTrackedAsync(tenantId, projectId, requestId, cancellationToken);
        if (row is null) return Result<ProjectPaymentRequestDetailDto>.Failure(Error.NotFound("Payment Request not found"));
        var (request, detail) = row.Value;
        if (!MatchesVersion(request.RowVersion, dto.RowVersion))
            return Result<ProjectPaymentRequestDetailDto>.Failure(Error.Conflict("Payment Request changed. Refresh before editing."));
        if ((request.Status != PaymentRequestStatuses.Draft
                && request.Status != PaymentRequestStatuses.ReturnedForRevision)
            || request.RequestedBy != actorId)
            return Result<ProjectPaymentRequestDetailDto>.Failure(Error.Forbidden(
                "Only the requester may edit a draft or returned Payment Request."));
        if (!IsValidVersionlessAmount(dto.Amount))
            return Result<ProjectPaymentRequestDetailDto>.Failure(Error.Validation("Amount must be positive and use no more than four decimal places."));

        var title = dto.Title?.Trim() ?? string.Empty;
        var reason = dto.Reason?.Trim() ?? string.Empty;
        var createShape = new CreatePaymentRequestDto(title, dto.Amount, reason, dto.ProjectPlanMilestoneId,
            detail.IdempotencyKey, detail.PlannedPaymentInstallmentId, dto.ProjectAnnualBudgetId ?? detail.ProjectAnnualBudgetId,
            dto.ProjectAnnualBudgetLineId ?? detail.ProjectAnnualBudgetLineId);
        var validation = PaymentRequestWorkflowPolicy.ValidateDraft(createShape, project.Currency);
        if (validation.Count > 0) return Result<ProjectPaymentRequestDetailDto>.Failure(Error.Validation(string.Join(",", validation)));
        if (!await IsConfiguredCurrencyAsync(NormalizeCurrency(project.Currency), cancellationToken))
            return Result<ProjectPaymentRequestDetailDto>.Failure(Error.Conflict("The project currency is not configured in the currency catalog."));
        if (dto.ProjectPlanMilestoneId.HasValue
            && !await milestoneLookup.IsActiveMilestoneForProjectAsync(projectId, dto.ProjectPlanMilestoneId.Value, cancellationToken))
            return Result<ProjectPaymentRequestDetailDto>.Failure(Error.Validation("The selected milestone is not active in a working plan for this project."));

        var annualBudgetId = dto.ProjectAnnualBudgetId ?? detail.ProjectAnnualBudgetId;
        var lineId = dto.ProjectAnnualBudgetLineId ?? detail.ProjectAnnualBudgetLineId;
        if (!annualBudgetId.HasValue || !lineId.HasValue || !detail.PlannedPaymentInstallmentId.HasValue)
            return Result<ProjectPaymentRequestDetailDto>.Failure(Error.Conflict(
                "The Payment Request must retain its Planned Payment installment, Annual Budget, and Budget Line."));
        var annualBudget = await FindAnnualBudgetForUpdateAsync(tenantId, projectId, annualBudgetId.Value, cancellationToken);
        if (annualBudget is null || ValidateSelectedAnnualBudgetAssociation(project, annualBudget) is not null)
            return Result<ProjectPaymentRequestDetailDto>.Failure(Error.Conflict(
                "The selected Annual Budget is unavailable, closed, or incompatible with the project currency."));
        var budgetLine = await db.ProjectAnnualBudgetLines.IgnoreQueryFilters()
            .FirstOrDefaultAsync(item => item.Id == lineId.Value && item.TenantId == tenantId
                && item.ProjectId == projectId && item.ProjectAnnualBudgetId == annualBudget.Id && !item.IsDeleted,
                cancellationToken);
        if (budgetLine is null)
            return Result<ProjectPaymentRequestDetailDto>.Failure(Error.Validation(
                "The selected Budget Line does not belong to the selected Annual Budget."));
        var installmentCheck = await plannedPayments.ApplyPaymentRequestAsync(
            tenantId, projectId, detail.PlannedPaymentInstallmentId.Value, requestId,
            dto.Amount, actorId, actorName, cancellationToken);
        if (installmentCheck.IsFailure)
            return Result<ProjectPaymentRequestDetailDto>.Failure(installmentCheck.Error!);

        var before = AuditValues(request, detail, null);
        request.Title = title;
        request.Description = reason;
        request.Amount = dto.Amount;
        request.Currency = NormalizeCurrency(project.Currency);
        request.UpdatedBy = actorId.ToString("D");
        detail.PlanActivityId = dto.ProjectPlanMilestoneId;
        detail.ProjectAnnualBudgetId = annualBudget.Id;
        detail.ProjectAnnualBudgetLineId = budgetLine.Id;
        detail.AnnualBudgetFiscalYear = annualBudget.FiscalYear;
        detail.AnnualBudgetReference = annualBudget.FinancialReference;
        AddAudit(project, request, actorId, actorName, "Edited", "Payment Request edited", before,
            AuditValues(request, detail, new { budgetLineId = budgetLine.Id, budgetLineReference = budgetLine.Reference }),
            "financialReference,amount,currency,milestone,annualBudgetId,fiscalYear,annualBudgetLineId");

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<ProjectPaymentRequestDetailDto>.Failure(Error.Conflict("Payment Request changed. Refresh before editing."));
        }
        return Result<ProjectPaymentRequestDetailDto>.Success(await ToDetailAsync(request, detail, actorId, cancellationToken));
    }

    public async Task<Result<ProjectPaymentRequestDetailDto>> SubmitAsync(
        Guid tenantId, Guid projectId, Guid requestId, Guid actorId, string? actorName, SubmitPaymentRequestDto dto, CancellationToken cancellationToken)
    {
        var serviceAccess = await ValidateServiceAccessAsync(tenantId, projectId, actorId, requireWrite: true, cancellationToken,
            actionPermission: SubmitPermission);
        if (serviceAccess.Error is not null)
            return Result<ProjectPaymentRequestDetailDto>.Failure(serviceAccess.Error);
        if (!dto.IdempotencyKey.HasValue || dto.IdempotencyKey == Guid.Empty)
            return Result<ProjectPaymentRequestDetailDto>.Failure(Error.Validation("A submission idempotency key is required."));
        // Quick One-off Submit uses this existing approval workflow inside the
        // coordinator's transaction; direct submissions retain their own transaction.
        await using var transaction = db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            : null;
        var project = await FindProjectForUpdateAsync(tenantId, projectId, cancellationToken);
        if (project is null) return Result<ProjectPaymentRequestDetailDto>.Failure(Error.NotFound("Project not found"));
        var accessError = await ValidateActiveActorAsync(tenantId, actorId, cancellationToken);
        if (accessError is not null) return Result<ProjectPaymentRequestDetailDto>.Failure(accessError);
        var row = await FindRequestTrackedAsync(tenantId, projectId, requestId, cancellationToken);
        if (row is null) return Result<ProjectPaymentRequestDetailDto>.Failure(Error.NotFound("Payment Request not found"));
        var (request, detail) = row.Value;

        var approvalMode = NormalizeMode(dto.ApprovalMode);
        if (approvalMode is not (PaymentRequestApprovalModes.Sequential or PaymentRequestApprovalModes.Parallel))
            return Result<ProjectPaymentRequestDetailDto>.Failure(Error.Validation(
                "Approval mode must be Sequential or Parallel.", nameof(dto.ApprovalMode)));
        var selected = NormalizeSelections(dto.Approvers, approvalMode);
        if (PaymentRequestWorkflowPolicy.ContainsRequester(request.RequestedBy, selected))
            return Result<ProjectPaymentRequestDetailDto>.Failure(Error.Validation(
                $"{PaymentRequestWorkflowPolicy.RequesterCannotBeApprover}: The requester cannot be assigned as an approver on their own Payment Request.",
                nameof(dto.Approvers)));
        var submissionFingerprint = SubmissionFingerprint(request, detail, approvalMode, selected);
        var priorCycle = await db.ProjectPaymentRequestApprovalCycles.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.ProjectId == projectId
                && item.ApprovalRequestId == requestId && item.SubmitIdempotencyKey == dto.IdempotencyKey.Value
                && !item.IsDeleted, cancellationToken);
        if (priorCycle is not null)
        {
            var replayFingerprint = SubmissionFingerprint(priorCycle, approvalMode, selected);
            if (!string.Equals(priorCycle.SubmissionFingerprint, replayFingerprint, StringComparison.Ordinal))
                return Result<ProjectPaymentRequestDetailDto>.Failure(Error.Conflict(
                    "The submission idempotency key was already used for different approval or funding content."));
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
            return Result<ProjectPaymentRequestDetailDto>.Success(await ToDetailAsync(request, detail, actorId, cancellationToken));
        }
        if (request.Status != PaymentRequestStatuses.Draft
            && request.Status != PaymentRequestStatuses.ReturnedForRevision)
            return Result<ProjectPaymentRequestDetailDto>.Failure(Error.Conflict(
                "Only Draft or Returned for Revision Payment Requests can be submitted."));
        if (!MatchesVersion(request.RowVersion, dto.RowVersion))
            return Result<ProjectPaymentRequestDetailDto>.Failure(Error.Conflict("Payment Request changed. Refresh before submitting."));
        if (request.RequestedBy != actorId)
            return Result<ProjectPaymentRequestDetailDto>.Failure(Error.Forbidden("Only the requester may submit this Payment Request."));
        if (!await IsCurrentProjectManagerAsync(tenantId, project, actorId, cancellationToken))
            return Result<ProjectPaymentRequestDetailDto>.Failure(Error.Forbidden(
                "Only the current Project Manager may submit or resubmit a Payment Request."));

        if (!await IsConfiguredCurrencyAsync(NormalizeCurrency(project.Currency), cancellationToken))
            return Result<ProjectPaymentRequestDetailDto>.Failure(Error.Conflict("The project currency is not configured in the currency catalog."));

        var validation = PaymentRequestWorkflowPolicy.ValidateApprovalSetup(
            project.FinanceApprovalMode, approvalMode, selected, request.RequestedBy);
        if (validation.Count > 0) return Result<ProjectPaymentRequestDetailDto>.Failure(Error.Validation(string.Join(",", validation)));
        var eligibility = await GetEligibleApproverIdsAsync(tenantId, projectId, cancellationToken);
        if (selected.Any(item => !eligibility.Contains(item.UserId)))
            return Result<ProjectPaymentRequestDetailDto>.Failure(Error.Validation(
                "Every approver must be an active Project Team member linked to an active user with ProjectPaymentRequest.Approve permission."));
        if (request.Amount is null or <= 0 || !string.Equals(request.Currency, NormalizeCurrency(project.Currency), StringComparison.Ordinal))
            return Result<ProjectPaymentRequestDetailDto>.Failure(Error.Conflict("Payment Request amount or currency no longer matches the project."));

        if (detail.ProjectAnnualBudgetId is null || detail.AnnualBudgetFiscalYear is null
            || detail.ProjectAnnualBudgetLineId is null || detail.PlannedPaymentInstallmentId is null)
            return Result<ProjectPaymentRequestDetailDto>.Failure(Error.Conflict(
                "The Payment Request must reference one Planned Payment installment, Annual Budget, and Budget Line."));
        var annualBudget = await FindAnnualBudgetForUpdateAsync(tenantId, projectId, detail.ProjectAnnualBudgetId.Value, cancellationToken);
        if (annualBudget is null)
            return Result<ProjectPaymentRequestDetailDto>.Failure(Error.Conflict("The associated Annual Budget is unavailable."));
        var budgetError = ValidateSelectedAnnualBudgetAssociation(project, annualBudget);
        if (budgetError is not null)
            return Result<ProjectPaymentRequestDetailDto>.Failure(budgetError);
        var budgetLine = await db.ProjectAnnualBudgetLines.IgnoreQueryFilters()
            .FirstOrDefaultAsync(item => item.Id == detail.ProjectAnnualBudgetLineId.Value
                && item.TenantId == tenantId && item.ProjectId == projectId
                && item.ProjectAnnualBudgetId == annualBudget.Id && !item.IsDeleted, cancellationToken);
        if (budgetLine is null)
            return Result<ProjectPaymentRequestDetailDto>.Failure(Error.Conflict(
                "The associated Budget Line is unavailable or belongs to another Annual Budget."));
        var legacyApprovedCount = await CountLegacyApprovedRequestsAsync(tenantId, projectId, cancellationToken);
        if (legacyApprovedCount > 0)
            return Result<ProjectPaymentRequestDetailDto>.Failure(Error.Conflict(
                "Approved historical Payment Requests have no Fiscal Year association. Reconcile the project history before submitting new requests."));
        var currentAllocations = await GetActiveApprovedAllocationAsync(tenantId, projectId, annualBudget, cancellationToken);
        if (currentAllocations.CurrencyMismatch)
            return Result<ProjectPaymentRequestDetailDto>.Failure(Error.Conflict(
                "Approved requests contain an incompatible currency for this Annual Budget. Resolve the project financial data before submission."));
        if (request.Amount.Value > annualBudget.TotalAnnualBudget - currentAllocations.Amount)
            return Result<ProjectPaymentRequestDetailDto>.Failure(Error.Conflict(
                "The requested amount exceeds the available amount in the associated Annual Budget."));
        var lineAllocations = await GetActiveBudgetLineAllocationAsync(
            tenantId, projectId, annualBudget.Id, budgetLine.Id, excludeRequestId: null, cancellationToken);
        if (lineAllocations.CurrencyMismatch || request.Amount.Value > budgetLine.AuthorizedLimit - lineAllocations.Amount)
            return Result<ProjectPaymentRequestDetailDto>.Failure(Error.Conflict(
                "The requested amount exceeds the available amount in the selected Budget Line."));

        var installmentCheck = await plannedPayments.ApplyPaymentRequestAsync(
            tenantId, projectId, detail.PlannedPaymentInstallmentId.Value, requestId,
            request.Amount.Value, actorId, actorName, cancellationToken);
        if (installmentCheck.IsFailure)
            return Result<ProjectPaymentRequestDetailDto>.Failure(installmentCheck.Error!);

        var cycleNumber = checked(detail.CurrentApprovalCycleNumber + 1);
        var cycleId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var priorStatus = request.Status;
        var cycle = new ProjectPaymentRequestApprovalCycle
        {
            Id = cycleId,
            TenantId = tenantId,
            ProjectId = projectId,
            ApprovalRequestId = requestId,
            CycleNumber = cycleNumber,
            ApprovalMode = approvalMode,
            Amount = request.Amount.Value,
            Currency = NormalizeCurrency(request.Currency),
            PlannedPaymentInstallmentId = detail.PlannedPaymentInstallmentId.Value,
            ProjectAnnualBudgetId = annualBudget.Id,
            FiscalYear = annualBudget.FiscalYear,
            AnnualBudgetReference = annualBudget.FinancialReference,
            ProjectAnnualBudgetLineId = budgetLine.Id,
            AnnualBudgetLineReference = budgetLine.Reference,
            AnnualBudgetLineDescription = budgetLine.Description,
            RequesterId = actorId,
            SubmittedAtUtc = now,
            SubmitIdempotencyKey = dto.IdempotencyKey.Value,
            SubmissionFingerprint = submissionFingerprint,
            ApproversSnapshotJson = JsonSerializer.Serialize(selected.OrderBy(item => item.Position ?? int.MaxValue)
                .Select(item => new { item.UserId, item.Position, item.Requirement })),
            CreatedBy = actorId.ToString("D")
        };

        detail.ApprovalMode = approvalMode;
        detail.CurrentApprovalCycleId = cycleId;
        detail.CurrentApprovalCycleNumber = cycleNumber;
        detail.SubmittedAt = now;
        detail.CompletedAt = null;
        request.Status = PaymentRequestStatuses.Pending;
        request.CurrentApproverId = approvalMode == PaymentRequestApprovalModes.Sequential ? selected.OrderBy(item => item.Position).First().UserId : null;
        request.ApprovalLevel = approvalMode == PaymentRequestApprovalModes.Sequential ? 1 : 0;
        request.UpdatedBy = actorId.ToString("D");

        var assignments = selected.Select(selection => new PaymentRequestApproverAssignment
        {
            TenantId = tenantId,
            ProjectId = projectId,
            ApprovalRequestId = requestId,
            ApprovalCycleId = cycleId,
            ApproverUserId = selection.UserId,
            Position = selection.Position,
            Requirement = selection.Requirement,
            State = PaymentRequestAssignmentStates.Pending,
            CreatedBy = actorId.ToString("D")
        }).ToArray();
        db.PaymentRequestApproverAssignments.AddRange(assignments);
        var names = await LoadUserNamesAsync(tenantId, selected.Select(item => item.UserId), cancellationToken);
        var approverSummary = string.Join(", ", selected.OrderBy(item => item.Position ?? int.MaxValue)
            .Select(item => $"{names.GetValueOrDefault(item.UserId) ?? item.UserId.ToString("D")} ({item.Requirement})"));
        var before = AuditValues(request, detail, null, statusOverride: priorStatus);
        db.ProjectPaymentRequestApprovalCycles.Add(cycle);
        AddAudit(project, request, actorId, actorName, "Submitted", "Payment Request submitted for approval", before,
            AuditValues(request, detail, new { approvalMode, approvers = approverSummary, cycleId, cycleNumber }),
            "status,approvalMode,approvers,amount,currency,annualBudgetId,fiscalYear,annualBudgetLineId,approvalCycleId");
        AddAudit(project, request, actorId, actorName, "ApproversAssigned", $"Approvers assigned to {Reference(request)}: {approverSummary}",
            null, AuditValues(request, detail, new { approvalMode, approvers = approverSummary, cycleId, cycleNumber }),
            "approvalMode,approvers,approvalCycleId");
        AddAudit(project, request, actorId, actorName, "FundingReserved",
            $"{request.Amount:0.####} {request.Currency} reserved for approval cycle {cycleNumber} against Annual Budget {annualBudget.FinancialReference}, Budget Line {budgetLine.Reference}, and installment {detail.PlannedPaymentInstallmentId}.",
            null, AuditValues(request, detail, new
            {
                cycleId, cycleNumber, allocation = request.Amount, currency = request.Currency,
                annualBudgetId = annualBudget.Id, fiscalYear = annualBudget.FiscalYear,
                budgetLineId = budgetLine.Id, installmentId = detail.PlannedPaymentInstallmentId
            }), "amount,currency,annualBudgetId,fiscalYear,annualBudgetLineId,installmentId,approvalCycleId");
        if (cycleNumber > 1)
            AddAudit(project, request, actorId, actorName, "Resubmitted",
                $"Payment Request {Reference(request)} resubmitted as approval cycle {cycleNumber}.", before,
                AuditValues(request, detail, new { cycleId, cycleNumber, approvalMode, approvers = approverSummary }),
                "status,approvalCycleId,approvalCycleNumber,approvalMode,approvers");

        if (approvalNotifications is not null)
        {
            var notificationResult = await approvalNotifications.QueueActionableStepsAsync(
                project, request, cycleId, approvalMode, assignments, eligibility, cancellationToken);
            if (notificationResult.IsFailure)
                return Result<ProjectPaymentRequestDetailDto>.Failure(notificationResult.Error
                    ?? Error.Validation("Payment Request approval notifications could not be queued."));
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<ProjectPaymentRequestDetailDto>.Failure(Error.Conflict("Payment Request changed. Refresh before submitting."));
        }
        return Result<ProjectPaymentRequestDetailDto>.Success(await ToDetailAsync(request, detail, actorId, cancellationToken));
    }

    public async Task<Result<ProjectPaymentRequestDetailDto>> DecideAsync(
        Guid tenantId, Guid projectId, Guid requestId, Guid actorId, string? actorName, DecidePaymentRequestApprovalDto dto, CancellationToken cancellationToken)
    {
        var serviceAccess = await ValidateServiceAccessAsync(tenantId, projectId, actorId, requireWrite: true, cancellationToken,
            actionPermission: ApprovePermission, assignedReviewRequestId: requestId, allowAssignedReviewer: true);
        if (serviceAccess.Error is not null)
            return Result<ProjectPaymentRequestDetailDto>.Failure(serviceAccess.Error);
        if (!PaymentRequestDecisions.IsValid(dto.Decision))
            return Result<ProjectPaymentRequestDetailDto>.Failure(Error.Validation(
                "Decision must be Approved, Rejected, or Returned for Revision."));
        var isApprovedDecision = string.Equals(dto.Decision, PaymentRequestDecisions.Approved, StringComparison.OrdinalIgnoreCase);
        var isRejectedDecision = string.Equals(dto.Decision, PaymentRequestDecisions.Rejected, StringComparison.OrdinalIgnoreCase);
        var isReturnDecision = string.Equals(dto.Decision, PaymentRequestDecisions.ReturnedForRevision, StringComparison.OrdinalIgnoreCase);
        if (dto.ApprovedAmount.HasValue && (!isApprovedDecision || dto.ApprovedAmount.Value <= 0m))
            return Result<ProjectPaymentRequestDetailDto>.Failure(Error.Validation(
                "Partial approval is not supported; approval must cover the full Payment Request amount."));
        if (isReturnDecision && string.IsNullOrWhiteSpace(dto.Comment))
            return Result<ProjectPaymentRequestDetailDto>.Failure(Error.Validation(
                "A reason is required when returning a Payment Request for revision.", nameof(dto.Comment)));
        if (dto.Comment?.Trim().Length > 2000)
            return Result<ProjectPaymentRequestDetailDto>.Failure(Error.Validation("Decision comments cannot exceed 2000 characters."));

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var project = await FindProjectForUpdateAsync(tenantId, projectId, cancellationToken);
        if (project is null) return Result<ProjectPaymentRequestDetailDto>.Failure(Error.NotFound("Project not found"));
        var accessError = await ValidateActiveActorAsync(tenantId, actorId, cancellationToken);
        if (accessError is not null) return Result<ProjectPaymentRequestDetailDto>.Failure(accessError);
        var row = await FindRequestTrackedAsync(tenantId, projectId, requestId, cancellationToken);
        if (row is null) return Result<ProjectPaymentRequestDetailDto>.Failure(Error.NotFound("Payment Request not found"));
        var (request, detail) = row.Value;
        if (!MatchesVersion(request.RowVersion, dto.RowVersion))
            return Result<ProjectPaymentRequestDetailDto>.Failure(Error.Conflict("Payment Request changed. Refresh before deciding."));
        if (dto.ApprovedAmount.HasValue && request.Amount != dto.ApprovedAmount.Value)
            return Result<ProjectPaymentRequestDetailDto>.Failure(Error.Validation(
                "Partial approval is not supported; the approved amount must equal the full Payment Request amount.",
                nameof(dto.ApprovedAmount)));
        if (request.Status != PaymentRequestStatuses.Pending || detail.ApprovalMode is null)
            return Result<ProjectPaymentRequestDetailDto>.Failure(Error.Conflict("This Payment Request is no longer awaiting approval."));

        if (request.RequestedBy == actorId)
            return Result<ProjectPaymentRequestDetailDto>.Failure(Error.Forbidden("The requester cannot approve their own Payment Request."));

        var eligibleApprovers = await GetEligibleApproverIdsAsync(tenantId, projectId, cancellationToken);
        if (!eligibleApprovers.Contains(actorId))
            return Result<ProjectPaymentRequestDetailDto>.Failure(Error.Forbidden(
                "An approver must remain an active eligible Project Team member with ProjectPaymentRequest.Approve permission to decide."));

        var assignments = await GetAssignmentsAsync(tenantId, requestId, tracked: true, cancellationToken,
            detail.CurrentApprovalCycleId);
        var actions = await db.ApprovalActions.IgnoreQueryFilters()
            .Where(item => item.TenantId == tenantId && !item.IsDeleted && item.ApprovalRequestId == requestId)
            .ToListAsync(cancellationToken);
        var states = BuildStates(assignments, actions);
        var canDecide = detail.ApprovalMode == PaymentRequestApprovalModes.Sequential
            ? PaymentRequestWorkflowPolicy.CanDecideSequentially(actorId, request.Status, states)
            : PaymentRequestWorkflowPolicy.CanDecideInParallel(actorId, request.Status, states);
        if (!canDecide)
            return Result<ProjectPaymentRequestDetailDto>.Failure(Error.Forbidden("Only an active, assigned approver may decide this request."));
        var assignment = assignments.Single(item => item.ApproverUserId == actorId && item.State == PaymentRequestAssignmentStates.Pending);
        if (actions.Any(item => item.ApproverAssignmentId == assignment.Id))
            return Result<ProjectPaymentRequestDetailDto>.Failure(Error.Conflict("This approver has already decided."));

        var now = DateTime.UtcNow;
        var approved = isApprovedDecision;
        var rejected = isRejectedDecision;
        var decisionAction = approved ? "Approve" : rejected ? "Reject" : "ReturnForRevision";
        assignment.State = approved ? PaymentRequestAssignmentStates.Approved
            : rejected ? PaymentRequestAssignmentStates.Rejected : PaymentRequestAssignmentStates.ReturnedForRevision;
        assignment.UpdatedBy = actorId.ToString("D");
        request.UpdatedBy = actorId.ToString("D");
        db.ApprovalActions.Add(new ApprovalAction
        {
            TenantId = tenantId,
            ApprovalRequestId = requestId,
            ApproverId = actorId,
            ApproverAssignmentId = assignment.Id,
            Action = decisionAction,
            Comments = dto.Comment?.Trim(),
            ActionedAt = now,
            ApprovalLevel = detail.ApprovalMode == PaymentRequestApprovalModes.Sequential ? assignment.Position ?? 0 : 0,
            CreatedBy = actorId.ToString("D")
        });

        var prior = AuditValues(request, detail, null, statusOverride: PaymentRequestStatuses.Pending);
        var individualAction = approved ? "ApproverApproved" : rejected ? "ApproverRejected" : "ApproverReturnedForRevision";
        AddAudit(project, request, actorId, actorName, individualAction,
            $"{(approved ? "Approval" : rejected ? "Rejection" : "Return for Revision")} recorded by {actorName ?? actorId.ToString("D")} for {Reference(request)}",
            prior, AuditValues(request, detail, new { decision = dto.Decision, comment = dto.Comment?.Trim(), cycleId = detail.CurrentApprovalCycleId }),
            "decision,approver,comment,amount,currency,approvalCycleId");

        var projectedStates = BuildStates(assignments, actions, actorId, decisionAction);
        var status = detail.ApprovalMode == PaymentRequestApprovalModes.Sequential
            ? PaymentRequestWorkflowPolicy.EvaluateSequentialStatus(projectedStates)
            : PaymentRequestWorkflowPolicy.EvaluateParallelStatus(projectedStates);
        if (status is PaymentRequestStatuses.Approved or PaymentRequestStatuses.Rejected or PaymentRequestStatuses.ReturnedForRevision)
        {
            var cycle = detail.CurrentApprovalCycleId.HasValue
                ? await db.ProjectPaymentRequestApprovalCycles.IgnoreQueryFilters()
                    .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.ProjectId == projectId
                        && item.ApprovalRequestId == requestId && item.Id == detail.CurrentApprovalCycleId.Value
                        && !item.IsDeleted, cancellationToken)
                : null;
            if (status == PaymentRequestStatuses.Approved)
            {
                if (!await IsConfiguredCurrencyAsync(NormalizeCurrency(project.Currency), cancellationToken))
                    return Result<ProjectPaymentRequestDetailDto>.Failure(Error.Conflict("The project currency is not configured in the currency catalog."));
                if (detail.ProjectAnnualBudgetId is null || detail.AnnualBudgetFiscalYear is null
                    || detail.ProjectAnnualBudgetLineId is null || request.Amount is null)
                    return Result<ProjectPaymentRequestDetailDto>.Failure(Error.Conflict(
                        "This Payment Request has no complete Annual Budget and Budget Line association."));
                var annualBudget = await FindAnnualBudgetForUpdateAsync(
                    tenantId, projectId, detail.ProjectAnnualBudgetId.Value, cancellationToken);
                if (annualBudget is null)
                    return Result<ProjectPaymentRequestDetailDto>.Failure(Error.Conflict("The associated Annual Budget is unavailable."));
                var budgetError = ValidateSelectedAnnualBudgetAssociation(project, annualBudget);
                if (budgetError is not null || annualBudget.FiscalYear != detail.AnnualBudgetFiscalYear.Value)
                    return Result<ProjectPaymentRequestDetailDto>.Failure(budgetError ?? Error.Conflict(
                        "The selected Annual Budget fiscal year changed after submission."));
                var budgetLine = await db.ProjectAnnualBudgetLines.IgnoreQueryFilters()
                    .FirstOrDefaultAsync(item => item.Id == detail.ProjectAnnualBudgetLineId.Value
                        && item.TenantId == tenantId && item.ProjectId == projectId
                        && item.ProjectAnnualBudgetId == annualBudget.Id && !item.IsDeleted, cancellationToken);
                if (budgetLine is null)
                    return Result<ProjectPaymentRequestDetailDto>.Failure(Error.Conflict(
                        "The selected Budget Line is unavailable at final approval."));
                var legacyApprovedCount = await CountLegacyApprovedRequestsAsync(tenantId, projectId, cancellationToken);
                if (legacyApprovedCount > 0)
                    return Result<ProjectPaymentRequestDetailDto>.Failure(Error.Conflict(
                        "Approved historical Payment Requests have no Fiscal Year association. Reconcile the project history before approving new requests."));
                var allocated = await GetActiveApprovedAllocationAsync(
                    tenantId, projectId, annualBudget, cancellationToken, excludeRequestId: requestId);
                var currency = NormalizeCurrency(project.Currency);
                if (allocated.CurrencyMismatch
                    || !string.Equals(NormalizeCurrency(request.Currency), currency, StringComparison.Ordinal)
                    || !string.Equals(NormalizeCurrency(annualBudget.Currency), currency, StringComparison.Ordinal))
                    return Result<ProjectPaymentRequestDetailDto>.Failure(Error.Conflict(
                        "Active requests or the Annual Budget contain an incompatible currency. Resolve the project financial data before approval."));
                var available = annualBudget.TotalAnnualBudget - allocated.Amount;
                if (request.Amount.Value > available)
                    return Result<ProjectPaymentRequestDetailDto>.Failure(Error.Conflict(
                        "The approved amount exceeds the remaining Annual Budget after excluding its existing reservation."));
                var lineAllocation = await GetActiveBudgetLineAllocationAsync(
                    tenantId, projectId, annualBudget.Id, budgetLine.Id, excludeRequestId: requestId, cancellationToken);
                if (lineAllocation.CurrencyMismatch || request.Amount.Value > budgetLine.AuthorizedLimit - lineAllocation.Amount)
                    return Result<ProjectPaymentRequestDetailDto>.Failure(Error.Conflict(
                        "The approved amount exceeds the remaining Budget Line allocation after excluding its existing reservation."));

                request.Status = PaymentRequestStatuses.Approved;
                detail.CompletedAt = now;
                AddAudit(project, request, actorId, actorName, "Approved",
                    $"Payment Request {Reference(request)} approved in full against FY {annualBudget.FiscalYear} Annual Budget {annualBudget.FinancialReference} and Budget Line {budgetLine.Reference}.", prior,
                    AuditValues(request, detail, new { status = PaymentRequestStatuses.Approved, cycleId = detail.CurrentApprovalCycleId }),
                    "status,amount,currency,annualBudgetId,fiscalYear,annualBudgetLineId,approvalCycleId");
            }
            else
            {
                request.Status = status;
                detail.CompletedAt = now;
                var action = status == PaymentRequestStatuses.Rejected ? "Rejected" : "ReturnedForRevision";
                var description = status == PaymentRequestStatuses.Rejected
                    ? $"Payment Request {Reference(request)} rejected."
                    : $"Payment Request {Reference(request)} returned for revision; Annual Budget and Budget Line reservations released, while the Planned Payment lifetime reservation remains intact.";
                AddAudit(project, request, actorId, actorName, action, description, prior,
                    AuditValues(request, detail, new { status, comment = dto.Comment?.Trim(), cycleId = detail.CurrentApprovalCycleId }),
                    "status,amount,currency,annualBudgetId,annualBudgetLineId,approvalCycleId,comment");
                AddAudit(project, request, actorId, actorName, "FundingReleased",
                    $"The {request.Amount:0.####} {request.Currency} Annual Budget and Budget Line reservation for approval cycle {detail.CurrentApprovalCycleNumber} of {Reference(request)} was released.",
                    null, AuditValues(request, detail, new
                    {
                        cycleId = detail.CurrentApprovalCycleId, cycleNumber = detail.CurrentApprovalCycleNumber,
                        allocation = request.Amount, currency = request.Currency,
                        annualBudgetId = detail.ProjectAnnualBudgetId, fiscalYear = detail.AnnualBudgetFiscalYear,
                        budgetLineId = detail.ProjectAnnualBudgetLineId,
                        lifetimeReservationPreserved = true
                    }), "amount,currency,annualBudgetId,fiscalYear,annualBudgetLineId,approvalCycleId");
            }
            if (cycle is not null)
            {
                cycle.Status = status;
                cycle.CompletedAtUtc = now;
                cycle.DecisionComment = dto.Comment?.Trim();
                cycle.UpdatedAt = now;
                cycle.UpdatedBy = actorId.ToString("D");
            }
            foreach (var other in assignments.Where(item => item.State == PaymentRequestAssignmentStates.Pending))
            {
                other.State = PaymentRequestAssignmentStates.Closed;
                other.UpdatedAt = now;
                other.UpdatedBy = actorId.ToString("D");
            }
            request.CurrentApproverId = null;
        }
        else if (detail.ApprovalMode == PaymentRequestApprovalModes.Sequential)
        {
            request.CurrentApproverId = PaymentRequestWorkflowPolicy.GetActiveSequentialApprover(projectedStates);
            request.ApprovalLevel = assignments.FirstOrDefault(item => item.ApproverUserId == request.CurrentApproverId)?.Position ?? request.ApprovalLevel;
        }

        if (status == PaymentRequestStatuses.Pending && detail.CurrentApprovalCycleId.HasValue && approvalNotifications is not null)
        {
            var notificationResult = await approvalNotifications.QueueActionableStepsAsync(
                project, request, detail.CurrentApprovalCycleId.Value, detail.ApprovalMode, assignments,
                eligibleApprovers, cancellationToken);
            if (notificationResult.IsFailure)
                return Result<ProjectPaymentRequestDetailDto>.Failure(notificationResult.Error
                    ?? Error.Validation("Payment Request approval notifications could not be queued."));
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<ProjectPaymentRequestDetailDto>.Failure(Error.Conflict("Payment Request changed during approval. Refresh before deciding."));
        }
        return Result<ProjectPaymentRequestDetailDto>.Success(await ToDetailAsync(request, detail, actorId, cancellationToken));
    }

    public async Task<Result<ProjectPaymentRequestDetailDto>> CancelAsync(
        Guid tenantId, Guid projectId, Guid requestId, Guid actorId, string? actorName, CancelPaymentRequestDto dto, CancellationToken cancellationToken)
    {
        var serviceAccess = await ValidateServiceAccessAsync(tenantId, projectId, actorId, requireWrite: true, cancellationToken,
            actionPermission: CancelPermission);
        if (serviceAccess.Error is not null)
            return Result<ProjectPaymentRequestDetailDto>.Failure(serviceAccess.Error);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var project = await FindProjectForUpdateAsync(tenantId, projectId, cancellationToken);
        if (project is null) return Result<ProjectPaymentRequestDetailDto>.Failure(Error.NotFound("Project not found"));
        var accessError = await ValidateActiveActorAsync(tenantId, actorId, cancellationToken);
        if (accessError is not null) return Result<ProjectPaymentRequestDetailDto>.Failure(accessError);
        var row = await FindRequestTrackedAsync(tenantId, projectId, requestId, cancellationToken);
        if (row is null) return Result<ProjectPaymentRequestDetailDto>.Failure(Error.NotFound("Payment Request not found"));
        var (request, detail) = row.Value;
        if (!MatchesVersion(request.RowVersion, dto.RowVersion))
            return Result<ProjectPaymentRequestDetailDto>.Failure(Error.Conflict("Payment Request changed. Refresh before cancelling."));
        if (!PaymentRequestWorkflowPolicy.CanCancelBeforeApproval(request.Status))
            return Result<ProjectPaymentRequestDetailDto>.Failure(Error.Conflict("An approved Payment Request cannot be cancelled. Submit a separate approved Reversal Request."));
        if (!await IsCurrentProjectManagerAsync(tenantId, project, actorId, cancellationToken))
            return Result<ProjectPaymentRequestDetailDto>.Failure(Error.Forbidden(
                "Only the current Project Manager may cancel an unapproved Payment Request."));

        var hasSettlement = await db.PaymentSettlements.IgnoreQueryFilters().AsNoTracking()
            .AnyAsync(item => item.TenantId == tenantId && item.ProjectId == projectId
                && item.ProjectPaymentRequestId == requestId && !item.IsDeleted, cancellationToken);
        if (hasSettlement)
            return Result<ProjectPaymentRequestDetailDto>.Failure(Error.Conflict(
                "A Payment Request with a recorded settlement cannot be cancelled."));

        var cancelledAt = DateTime.UtcNow;
        if (detail.CreatedFromQuickOneOff)
        {
            var oneOffError = await CancelQuickOneOffPlanWhenUnsharedAsync(
                tenantId, projectId, actorId, actorName, project, request, detail, cancelledAt, cancellationToken);
            if (oneOffError is not null)
                return Result<ProjectPaymentRequestDetailDto>.Failure(oneOffError);
        }

        var prior = AuditValues(request, detail, null);
        var wasReserved = request.Status == PaymentRequestStatuses.Pending;
        request.Status = PaymentRequestStatuses.Cancelled;
        request.CurrentApproverId = null;
        request.UpdatedBy = actorId.ToString("D");
        detail.CompletedAt = cancelledAt;
        var assignments = await GetAssignmentsAsync(tenantId, requestId, tracked: true, cancellationToken,
            detail.CurrentApprovalCycleId);
        foreach (var assignment in assignments.Where(item => item.State == PaymentRequestAssignmentStates.Pending))
        {
            assignment.State = PaymentRequestAssignmentStates.Closed;
            assignment.UpdatedAt = cancelledAt;
            assignment.UpdatedBy = actorId.ToString("D");
        }
        if (detail.CurrentApprovalCycleId.HasValue)
        {
            var cycle = await db.ProjectPaymentRequestApprovalCycles.IgnoreQueryFilters()
                .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.ProjectId == projectId
                    && item.ApprovalRequestId == requestId && item.Id == detail.CurrentApprovalCycleId.Value
                    && !item.IsDeleted, cancellationToken);
            if (cycle is not null)
            {
                cycle.Status = ProjectPaymentRequestApprovalCycleStatuses.Cancelled;
                cycle.CompletedAtUtc = cancelledAt;
                cycle.UpdatedAt = cancelledAt;
                cycle.UpdatedBy = actorId.ToString("D");
            }
        }
        AddAudit(project, request, actorId, actorName, "Cancelled", $"Payment Request {Reference(request)} cancelled before approval", prior,
            AuditValues(request, detail, new { status = PaymentRequestStatuses.Cancelled }), "status,financialReference,amount,currency");
        if (wasReserved)
            AddAudit(project, request, actorId, actorName, "FundingReleased",
                $"Pending Annual Budget and Budget Line reservations for approval cycle {detail.CurrentApprovalCycleNumber} of {Reference(request)} were released; the Planned Payment lifetime reservation remains intact.",
                null, AuditValues(request, detail, new
                {
                    cycleId = detail.CurrentApprovalCycleId, cycleNumber = detail.CurrentApprovalCycleNumber,
                    allocation = request.Amount, currency = request.Currency,
                    annualBudgetId = detail.ProjectAnnualBudgetId, fiscalYear = detail.AnnualBudgetFiscalYear,
                    budgetLineId = detail.ProjectAnnualBudgetLineId, lifetimeReservationPreserved = true
                }), "amount,currency,annualBudgetId,fiscalYear,annualBudgetLineId,approvalCycleId");

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<ProjectPaymentRequestDetailDto>.Failure(Error.Conflict("Payment Request changed. Refresh before cancelling."));
        }
        return Result<ProjectPaymentRequestDetailDto>.Success(await ToDetailAsync(request, detail, actorId, cancellationToken));
    }

    private async Task<Error?> CancelQuickOneOffPlanWhenUnsharedAsync(
        Guid tenantId, Guid projectId, Guid actorId, string? actorName, Project project,
        ApprovalRequest request, ProjectPaymentRequestDetail detail, DateTime cancelledAt,
        CancellationToken cancellationToken)
    {
        if (!detail.PlannedPaymentInstallmentId.HasValue)
            return Error.Conflict("The Quick One-off request has no linked installment; its lifetime reservation cannot be safely resolved.");

        var installment = await db.PlannedPaymentInstallments.IgnoreQueryFilters()
            .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.ProjectId == projectId
                && item.Id == detail.PlannedPaymentInstallmentId.Value && !item.IsDeleted, cancellationToken);
        if (installment is null)
            return Error.Conflict("The Quick One-off installment is missing; cancellation cannot safely resolve its Plan.");

        var plan = await db.PlannedPayments.IgnoreQueryFilters()
            .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.ProjectId == projectId
                && item.Id == installment.PlannedPaymentId && !item.IsDeleted, cancellationToken);
        if (plan is null)
            return Error.Conflict("The Quick One-off Planned Payment is missing; cancellation cannot safely resolve its lifetime reservation.");

        var installmentIds = await db.PlannedPaymentInstallments.IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.ProjectId == projectId
                && item.PlannedPaymentId == plan.Id && !item.IsDeleted)
            .Select(item => item.Id)
            .ToArrayAsync(cancellationToken);
        if (installmentIds.Length != 1 || installmentIds[0] != installment.Id)
            return Error.Conflict("The Quick One-off Plan no longer has exactly one installment; it cannot be cancelled as an orphan.");

        var siblingRequestIds = await db.ProjectPaymentRequestDetails.IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.ProjectId == projectId
                && item.RequestKind == ProjectPaymentRequestKinds.PaymentRequest
                && item.PlannedPaymentInstallmentId == installment.Id
                && item.ApprovalRequestId != request.Id)
            .Select(item => item.ApprovalRequestId)
            .ToArrayAsync(cancellationToken);
        var hasActiveSibling = siblingRequestIds.Length > 0 && await db.ApprovalRequests.IgnoreQueryFilters().AsNoTracking()
            .AnyAsync(item => item.TenantId == tenantId && siblingRequestIds.Contains(item.Id)
                && !item.IsDeleted
                && item.Status != PaymentRequestStatuses.Cancelled
                && item.Status != PaymentRequestStatuses.Rejected
                && item.Status != PaymentRequestStatuses.Reversed, cancellationToken);
        var hasPlanSettlement = await db.PaymentSettlements.IgnoreQueryFilters().AsNoTracking()
            .AnyAsync(item => item.TenantId == tenantId && item.ProjectId == projectId && !item.IsDeleted
                && ((item.PlannedPaymentInstallmentId.HasValue
                        && installmentIds.Contains(item.PlannedPaymentInstallmentId.Value))
                    || (item.ProjectPaymentRequestId.HasValue
                        && siblingRequestIds.Contains(item.ProjectPaymentRequestId.Value))), cancellationToken);
        if (hasActiveSibling || hasPlanSettlement)
            return null; // The Plan remains owned by another request or an immutable settlement.

        if (plan.State is PlannedPaymentStates.Cancelled or PlannedPaymentStates.Replaced)
            return null;
        if (plan.State != PlannedPaymentStates.Locked)
            return Error.Conflict("The Quick One-off Plan is not in a cancellable locked state.");

        var reservation = await db.PlannedPaymentLifetimeReservations.IgnoreQueryFilters()
            .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.ProjectId == projectId
                && item.PlannedPaymentId == plan.Id && !item.IsDeleted, cancellationToken);
        if (reservation is null || reservation.ReleasedAmount < 0m
            || reservation.ReleasedAmount > reservation.ReservedAmount)
            return Error.Conflict("The Quick One-off lifetime reservation is missing or inconsistent.");

        var before = new
        {
            state = plan.State,
            reservedAmount = reservation.ReservedAmount,
            releasedAmount = reservation.ReleasedAmount
        };
        plan.State = PlannedPaymentStates.Cancelled;
        plan.UpdatedBy = actorId.ToString("D");
        plan.UpdatedAt = cancelledAt;
        reservation.ReleasedAmount = reservation.ReservedAmount;
        reservation.UpdatedBy = actorId.ToString("D");
        reservation.UpdatedAt = cancelledAt;

        var after = new
        {
            state = plan.State,
            reservedAmount = reservation.ReservedAmount,
            releasedAmount = reservation.ReleasedAmount,
            cancelledPaymentRequestId = request.Id
        };
        db.AuditEntries.Add(new AuditEntry
        {
            TenantId = tenantId,
            ProjectId = projectId,
            EntityType = "PlannedPayment",
            EntityId = plan.Id,
            Action = "CancelledWithQuickOneOffRequest",
            UserId = actorId,
            UserName = string.IsNullOrWhiteSpace(actorName) ? null : actorName.Trim(),
            Timestamp = cancelledAt,
            Description = $"Quick One-off Planned Payment {plan.Reference} was cancelled with its unapproved Payment Request.",
            OriginalValues = JsonSerializer.Serialize(before),
            NewValues = JsonSerializer.Serialize(after),
            Changes = JsonSerializer.Serialize(new { before, after }),
            ChangedFields = "state,reservationReleasedAmount",
            Source = "User",
            CreatedBy = actorId.ToString("D")
        });
        db.AuditEntries.Add(new AuditEntry
        {
            TenantId = tenantId,
            ProjectId = projectId,
            EntityType = "PlannedPaymentLifetimeReservation",
            EntityId = reservation.Id,
            Action = "ReleasedWithQuickOneOffCancellation",
            UserId = actorId,
            UserName = string.IsNullOrWhiteSpace(actorName) ? null : actorName.Trim(),
            Timestamp = cancelledAt,
            Description = $"The unused lifetime reservation for Quick One-off Planned Payment {plan.Reference} was released.",
            OriginalValues = JsonSerializer.Serialize(new { releasedAmount = before.releasedAmount }),
            NewValues = JsonSerializer.Serialize(new { releasedAmount = reservation.ReleasedAmount }),
            Changes = JsonSerializer.Serialize(new { releasedAmount = new { from = before.releasedAmount, to = reservation.ReleasedAmount } }),
            ChangedFields = "releasedAmount",
            Source = "User",
            CreatedBy = actorId.ToString("D")
        });
        return null;
    }

    private IQueryable<Project> FindProjectQuery(Guid tenantId, Guid projectId) => db.Projects.IgnoreQueryFilters()
        .Where(item => item.Id == projectId && item.TenantId == tenantId && !item.IsDeleted);

    private IQueryable<ProjectPaymentRequestDetail> PaymentDetailsQuery(Guid tenantId, Guid projectId) =>
        db.ProjectPaymentRequestDetails.IgnoreQueryFilters().Where(item => item.TenantId == tenantId
            && item.ProjectId == projectId && item.RequestKind == ProjectPaymentRequestKinds.PaymentRequest);

    private IQueryable<ApprovalRequest> PaymentRequestsQuery(Guid tenantId, Guid projectId) => db.ApprovalRequests.IgnoreQueryFilters()
        .Where(item => item.TenantId == tenantId
            && !item.IsDeleted
            && item.Type == RequestType
            && item.EntityType == "Project"
            && item.EntityId == projectId);

    private async Task<Project?> FindProjectForUpdateAsync(Guid tenantId, Guid projectId, CancellationToken cancellationToken)
    {
        // All financial commands take the same SQL Server project-row lock inside
        // the transaction. A final approval therefore observes prior reservations.
        if (string.Equals(db.Database.ProviderName, "Microsoft.EntityFrameworkCore.SqlServer", StringComparison.Ordinal))
        {
            var connection = db.Database.GetDbConnection();
            await using DbCommand command = connection.CreateCommand();
            command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = "SELECT [Id] FROM [Projects] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = @projectId AND [TenantId] = @tenantId AND [IsDeleted] = 0";
            AddParameter(command, "@projectId", projectId);
            AddParameter(command, "@tenantId", tenantId);
            var result = await command.ExecuteScalarAsync(cancellationToken);
            if (result is null or DBNull) return null;
        }
        return await FindProjectQuery(tenantId, projectId).FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<ProjectAnnualBudget?> FindAnnualBudgetForUpdateAsync(
        Guid tenantId, Guid projectId, Guid annualBudgetId, CancellationToken cancellationToken)
    {
        // Keep the project lock as the common serialization point, then lock the exact
        // annual-budget row so final approval re-reads the current budget and allocations.
        if (string.Equals(db.Database.ProviderName, "Microsoft.EntityFrameworkCore.SqlServer", StringComparison.Ordinal))
        {
            var connection = db.Database.GetDbConnection();
            await using DbCommand command = connection.CreateCommand();
            command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = "SELECT [Id] FROM [ProjectAnnualBudgets] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = @budgetId AND [TenantId] = @tenantId AND [ProjectId] = @projectId AND [IsDeleted] = 0";
            AddParameter(command, "@budgetId", annualBudgetId);
            AddParameter(command, "@tenantId", tenantId);
            AddParameter(command, "@projectId", projectId);
            var result = await command.ExecuteScalarAsync(cancellationToken);
            if (result is null or DBNull) return null;
        }

        return await db.ProjectAnnualBudgets.IgnoreQueryFilters()
            .FirstOrDefaultAsync(item => item.Id == annualBudgetId && item.TenantId == tenantId
                && item.ProjectId == projectId && !item.IsDeleted, cancellationToken);
    }

    private static void AddParameter(DbCommand command, string name, Guid value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = DbType.Guid;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private async Task<(ApprovalRequest Request, ProjectPaymentRequestDetail Detail)?> FindRequestAsync(
        Guid tenantId, Guid projectId, Guid requestId, CancellationToken cancellationToken)
    {
        var detail = await PaymentDetailsQuery(tenantId, projectId)
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.ApprovalRequestId == requestId, cancellationToken);
        if (detail is null) return null;
        var request = await PaymentRequestsQuery(tenantId, projectId).AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == requestId, cancellationToken);
        return request is null ? null : (request, detail);
    }

    private async Task<(ApprovalRequest Request, ProjectPaymentRequestDetail Detail)?> FindRequestTrackedAsync(
        Guid tenantId, Guid projectId, Guid requestId, CancellationToken cancellationToken)
    {
        var detail = await PaymentDetailsQuery(tenantId, projectId)
            .FirstOrDefaultAsync(item => item.ApprovalRequestId == requestId, cancellationToken);
        if (detail is null) return null;
        var request = await PaymentRequestsQuery(tenantId, projectId)
            .FirstOrDefaultAsync(item => item.Id == requestId, cancellationToken);
        return request is null ? null : (request, detail);
    }

    private async Task<ProjectPaymentRequestDetailDto> ToDetailAsync(
        ApprovalRequest request, ProjectPaymentRequestDetail detail, Guid actorId, CancellationToken cancellationToken)
    {
        var allAssignments = await db.PaymentRequestApproverAssignments.IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.TenantId == detail.TenantId && !item.IsDeleted && item.ApprovalRequestId == request.Id)
            .OrderBy(item => item.Position).ThenBy(item => item.CreatedAt).ToListAsync(cancellationToken);
        var assignments = allAssignments.Where(item => item.ApprovalCycleId == detail.CurrentApprovalCycleId).ToArray();
        var actions = await db.ApprovalActions.IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.TenantId == detail.TenantId && !item.IsDeleted && item.ApprovalRequestId == request.Id)
            .ToListAsync(cancellationToken);
        var cycles = await db.ProjectPaymentRequestApprovalCycles.IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.TenantId == detail.TenantId && item.ProjectId == detail.ProjectId
                && item.ApprovalRequestId == request.Id && !item.IsDeleted)
            .OrderBy(item => item.CycleNumber).ToListAsync(cancellationToken);
        var ids = allAssignments.Select(item => item.ApproverUserId)
            .Append(request.RequestedBy ?? Guid.Empty).Where(id => id != Guid.Empty);
        var names = await LoadUserNamesAsync(detail.TenantId, ids, cancellationToken);
        var requesterName = names.GetValueOrDefault(request.RequestedBy ?? Guid.Empty) ?? string.Empty;
        var approvers = MapApprovers(request, detail, assignments, actions, names, actorId);
        var approvalCycles = cycles.Select(cycle => new ProjectPaymentRequestApprovalCycleDto(
            cycle.Id, cycle.CycleNumber, cycle.SubmittedAtUtc, cycle.CompletedAtUtc,
            cycle.ApprovalMode, cycle.Status, cycle.Amount, cycle.Currency,
            cycle.ProjectAnnualBudgetId, cycle.AnnualBudgetReference, cycle.FiscalYear,
            cycle.ProjectAnnualBudgetLineId, cycle.AnnualBudgetLineDescription, requesterName,
            cycle.DecisionComment,
            MapApprovers(request, new ProjectPaymentRequestDetail { ApprovalMode = cycle.ApprovalMode },
                allAssignments.Where(item => item.ApprovalCycleId == cycle.Id).ToArray(), actions, names, actorId)))
            .ToArray();
        var effective = await permissions.GetEffectivePermissionsAsync(actorId, detail.TenantId, cancellationToken);
        var writableContext = management.Current?.EffectiveReadOnly != true;
        var currentProject = await FindProjectQuery(detail.TenantId, detail.ProjectId).FirstOrDefaultAsync(cancellationToken);
        var isCurrentProjectManager = currentProject is not null
            && await IsCurrentProjectManagerAsync(detail.TenantId, currentProject, actorId, cancellationToken);
        var editableStatus = request.Status == PaymentRequestStatuses.Draft
            || request.Status == PaymentRequestStatuses.ReturnedForRevision;
        var canEdit = writableContext && effective.Contains(EditDraftPermission)
            && editableStatus && request.RequestedBy == actorId;
        var canSubmit = writableContext && effective.Contains(SubmitPermission)
            && editableStatus && request.RequestedBy == actorId && isCurrentProjectManager;
        var canDecide = writableContext && effective.Contains(ApprovePermission) && approvers.Any(item => item.CanDecide);
        var canCancel = writableContext && effective.Contains(CancelPermission)
            && PaymentRequestWorkflowPolicy.CanCancelBeforeApproval(request.Status) && isCurrentProjectManager;
        var milestoneName = detail.PlanActivityId.HasValue
            ? await milestoneLookup.FindActiveMilestoneNameForProjectAsync(detail.ProjectId, detail.PlanActivityId.Value, cancellationToken)
            : null;
        var annualBudget = detail.ProjectAnnualBudgetId.HasValue
            ? await db.ProjectAnnualBudgets.IgnoreQueryFilters().AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == detail.ProjectAnnualBudgetId.Value
                    && item.TenantId == detail.TenantId && item.ProjectId == detail.ProjectId, cancellationToken)
            : null;
        var budgetLine = detail.ProjectAnnualBudgetLineId.HasValue
            ? await db.ProjectAnnualBudgetLines.IgnoreQueryFilters().AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == detail.ProjectAnnualBudgetLineId.Value
                    && item.TenantId == detail.TenantId && item.ProjectId == detail.ProjectId, cancellationToken)
            : null;
        return new ProjectPaymentRequestDetailDto(
            request.Id,
            detail.ProjectId,
            Reference(request),
            request.Title,
            request.Amount ?? 0m,
            request.Currency ?? string.Empty,
            request.Description ?? string.Empty,
            detail.PlanActivityId,
            milestoneName,
            request.Status,
            request.RequestedBy ?? Guid.Empty,
            requesterName,
            request.RequestedAt,
            detail.SubmittedAt,
            detail.CompletedAt,
            detail.ApprovalMode,
            approvers,
            request.RowVersion,
            canEdit,
            canSubmit,
            canDecide,
            canCancel,
            detail.ProjectAnnualBudgetId,
            detail.AnnualBudgetFiscalYear,
            detail.AnnualBudgetReference ?? annualBudget?.FinancialReference,
            detail.PlannedPaymentInstallmentId,
            detail.ProjectAnnualBudgetLineId,
            budgetLine?.Description,
            approvalCycles);
    }

    private ProjectPaymentRequestListItemDto ToListItem(
        ApprovalRequest request,
        ProjectPaymentRequestDetail detail,
        IReadOnlyCollection<PaymentRequestApproverAssignment> assignments,
        IReadOnlyCollection<ApprovalAction> actions,
        IReadOnlyDictionary<Guid, string> names,
        Guid actorId,
        bool canEditPermission,
        bool canSubmitPermission,
        bool canApprovePermission,
        bool canCancelPermission,
        ProjectAnnualBudget? annualBudget,
        ProjectAnnualBudgetLine? budgetLine)
    {
        var approvers = MapApprovers(request, detail, assignments, actions, names, actorId);
        var editableStatus = request.Status == PaymentRequestStatuses.Draft
            || request.Status == PaymentRequestStatuses.ReturnedForRevision;
        var canEdit = canEditPermission && editableStatus && request.RequestedBy == actorId;
        var canSubmit = canSubmitPermission && editableStatus && request.RequestedBy == actorId;
        return new ProjectPaymentRequestListItemDto(
            request.Id,
            Reference(request),
            request.Title,
            request.Amount ?? 0m,
            request.Currency ?? string.Empty,
            request.Status,
            request.RequestedBy ?? Guid.Empty,
            names.GetValueOrDefault(request.RequestedBy ?? Guid.Empty) ?? string.Empty,
            request.RequestedAt,
            detail.SubmittedAt,
            detail.CompletedAt,
            detail.ApprovalMode,
            detail.RequestKind == ProjectPaymentRequestKinds.PaymentRequest && PaymentRequestWorkflowPolicy.ReservesBudget(request.Status),
            request.RowVersion,
            canEdit,
            canSubmit,
            canApprovePermission && approvers.Any(item => item.IsActive && item.UserId == actorId),
            canCancelPermission && PaymentRequestWorkflowPolicy.CanCancelBeforeApproval(request.Status) && request.RequestedBy == actorId,
            detail.ProjectAnnualBudgetId,
            detail.AnnualBudgetFiscalYear,
            detail.AnnualBudgetReference ?? annualBudget?.FinancialReference,
            detail.PlannedPaymentInstallmentId,
            detail.ProjectAnnualBudgetLineId,
            budgetLine?.Description);
    }

    private IReadOnlyList<PaymentRequestApproverDto> MapApprovers(
        ApprovalRequest request,
        ProjectPaymentRequestDetail detail,
        IReadOnlyCollection<PaymentRequestApproverAssignment> assignments,
        IReadOnlyCollection<ApprovalAction> actions,
        IReadOnlyDictionary<Guid, string> names,
        Guid actorId)
    {
        var actionByAssignment = actions.Where(item => item.ApproverAssignmentId.HasValue)
            .GroupBy(item => item.ApproverAssignmentId!.Value)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(item => item.ActionedAt).First());
        var states = BuildStates(assignments, actions);
        var activeSequential = detail.ApprovalMode == PaymentRequestApprovalModes.Sequential
            ? PaymentRequestWorkflowPolicy.GetActiveSequentialApprover(states)
            : null;
        return assignments.OrderBy(item => item.Position ?? int.MaxValue).ThenBy(item => names.GetValueOrDefault(item.ApproverUserId))
            .Select(item =>
            {
                actionByAssignment.TryGetValue(item.Id, out var action);
                var isActive = request.Status == PaymentRequestStatuses.Pending
                    && item.State == PaymentRequestAssignmentStates.Pending
                    && (detail.ApprovalMode == PaymentRequestApprovalModes.Sequential
                        ? activeSequential == item.ApproverUserId
                        : detail.ApprovalMode == PaymentRequestApprovalModes.Parallel);
                return new PaymentRequestApproverDto(
                    item.ApproverUserId,
                    names.GetValueOrDefault(item.ApproverUserId) ?? string.Empty,
                    item.Position,
                    item.Requirement,
                    action is null ? item.State : NormalizeDecision(action.Action),
                    action?.ActionedAt,
                    action?.Comments,
                    isActive,
                    isActive && item.ApproverUserId == actorId);
            })
            .ToArray();
    }

    private async Task<List<PaymentRequestApproverAssignment>> GetAssignmentsAsync(
        Guid tenantId, Guid requestId, bool tracked, CancellationToken cancellationToken, Guid? cycleId = null)
    {
        var query = db.PaymentRequestApproverAssignments.IgnoreQueryFilters()
            .Where(item => item.TenantId == tenantId && !item.IsDeleted && item.ApprovalRequestId == requestId
                && item.ApprovalCycleId == cycleId);
        if (!tracked) query = query.AsNoTracking();
        return await query.OrderBy(item => item.Position).ThenBy(item => item.CreatedAt).ToListAsync(cancellationToken);
    }

    private async Task<HashSet<Guid>> GetEligibleApproverIdsAsync(Guid tenantId, Guid projectId, CancellationToken cancellationToken)
    {
        var eligible = await ProjectTeamApproverEligibility.GetActiveLinkedUserIdsAsync(
            db, tenantId, projectId, cancellationToken);
        var eligibleApprovers = new HashSet<Guid>();
        foreach (var userId in eligible)
        {
            var effective = await permissions.GetEffectivePermissionsAsync(userId, tenantId, cancellationToken);
            if (effective.Contains(ApprovePermission)) eligibleApprovers.Add(userId);
        }
        return eligibleApprovers;
    }

    private async Task<Dictionary<Guid, string>> LoadUserNamesAsync(Guid tenantId, IEnumerable<Guid> userIds, CancellationToken cancellationToken)
    {
        var ids = userIds.Where(id => id != Guid.Empty).Distinct().ToArray();
        if (ids.Length == 0) return [];
        var users = await db.Users.IgnoreQueryFilters().AsNoTracking()
            .Where(user => user.TenantId == tenantId && !user.IsDeleted && ids.Contains(user.Id))
            .Select(user => new { user.Id, user.FirstName, user.LastName })
            .ToListAsync(cancellationToken);
        return users.ToDictionary(user => user.Id, user => $"{user.FirstName} {user.LastName}".Trim());
    }

    private static IReadOnlyList<PaymentRequestApproverState> BuildStates(
        IReadOnlyCollection<PaymentRequestApproverAssignment> assignments,
        IReadOnlyCollection<ApprovalAction> actions,
        Guid? projectedActorId = null,
        string? projectedAction = null) => assignments.Select(item =>
    {
        var action = actions.FirstOrDefault(candidate => candidate.ApproverAssignmentId == item.Id)?.Action;
        if (item.ApproverUserId == projectedActorId && projectedAction is not null) action = projectedAction;
        return new PaymentRequestApproverState(item.ApproverUserId, item.Position, item.Requirement,
            action is null ? (item.State == PaymentRequestAssignmentStates.Pending ? null : item.State) : NormalizeDecision(action));
    }).ToArray();

    private async Task<(decimal Amount, bool CurrencyMismatch)> GetActiveApprovedAllocationAsync(
        Guid tenantId, Guid projectId, ProjectAnnualBudget budget, CancellationToken cancellationToken,
        Guid? excludeRequestId = null)
    {
        // Count Approved allocations and Pending (submitted) reservations so concurrent requests
        // cannot collectively exceed the annual budget/lifetime ceiling. Draft/terminal requests do
        // not reserve. Each request is included once by its current status.
        var rows = await (
            from detail in PaymentDetailsQuery(tenantId, projectId)
            join request in PaymentRequestsQuery(tenantId, projectId) on detail.ApprovalRequestId equals request.Id
            where detail.RequestKind == ProjectPaymentRequestKinds.PaymentRequest
                && (excludeRequestId == null || detail.ApprovalRequestId != excludeRequestId.Value)
                && (request.Status == PaymentRequestStatuses.Approved || request.Status == PaymentRequestStatuses.Pending)
            select new
            {
                detail.ProjectId,
                detail.TenantId,
                detail.ApprovalRequestId,
                detail.ProjectAnnualBudgetId,
                detail.AnnualBudgetFiscalYear,
                detail.AnnualBudgetReference,
                request.Amount,
                request.Currency,
                request.Status
            })
            .ToListAsync(cancellationToken);
        var currency = string.IsNullOrWhiteSpace(budget.Currency) ? string.Empty : NormalizeCurrency(budget.Currency);
        var mismatch = false;
        var originalAmount = 0m;
        foreach (var row in rows)
        {
            if (row.ProjectId != projectId || row.TenantId != tenantId || row.Amount is not > 0m
                || !IsValidCurrency(row.Currency)) mismatch = true;
            if (row.ProjectAnnualBudgetId == budget.Id)
            {
                if (row.AnnualBudgetFiscalYear != budget.FiscalYear || row.Amount is not > 0m
                    || !string.Equals(NormalizeCurrency(row.Currency), currency, StringComparison.Ordinal)) mismatch = true;
                else originalAmount += row.Amount.Value;
            }
        }
        var approvedRequestIds = rows.Where(item => item.Status == PaymentRequestStatuses.Approved).Select(item => item.ApprovalRequestId).ToArray();
        var requestIds = approvedRequestIds;
        var transfers = requestIds.Length == 0 ? [] : await db.ProjectPaymentRequestFundingTransfers.IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.ProjectId == projectId && requestIds.Contains(item.PaymentRequestId) && !item.IsDeleted)
            .ToListAsync(cancellationToken);
        var closingIds = transfers.Select(item => item.ClosingId).Distinct().ToArray();
        var closings = closingIds.Length == 0 ? new Dictionary<Guid, ProjectAnnualBudgetClosing>()
            : await db.ProjectAnnualBudgetClosings.IgnoreQueryFilters().AsNoTracking()
                .Where(item => item.TenantId == tenantId && item.ProjectId == projectId && closingIds.Contains(item.Id) && !item.IsDeleted)
                .ToDictionaryAsync(item => item.Id, cancellationToken);
        var budgetIds = transfers.Select(item => item.SourceAnnualBudgetId).Concat(transfers.Select(item => item.DestinationAnnualBudgetId))
            .Append(budget.Id).Distinct().ToArray();
        var projectBudgets = await db.ProjectAnnualBudgets.IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.ProjectId == projectId && budgetIds.Contains(item.Id) && !item.IsDeleted)
            .ToDictionaryAsync(item => item.Id, cancellationToken);
        foreach (var transfer in transfers)
        {
            var request = rows.FirstOrDefault(item => item.ApprovalRequestId == transfer.PaymentRequestId);
            var sourceExists = projectBudgets.TryGetValue(transfer.SourceAnnualBudgetId, out var source);
            var destinationExists = projectBudgets.TryGetValue(transfer.DestinationAnnualBudgetId, out var destination);
            if (request is null || !sourceExists || !destinationExists || transfer.Amount <= 0m
                || !IsValidCurrency(transfer.Currency) || !string.Equals(NormalizeCurrency(transfer.Currency), NormalizeCurrency(request.Currency), StringComparison.Ordinal)
                || source!.FiscalYear != transfer.SourceFiscalYear || destination!.FiscalYear != transfer.DestinationFiscalYear
                || destination.FiscalYear != source.FiscalYear + 1
                || source.ProjectId != projectId || destination.ProjectId != projectId
                || !closings.TryGetValue(transfer.ClosingId, out var closing)
                || closing.Status != ProjectAnnualBudgetClosingStatuses.Closed
                || closing.SourceAnnualBudgetId != transfer.SourceAnnualBudgetId
                || closing.DestinationAnnualBudgetId != transfer.DestinationAnnualBudgetId
                || closing.FiscalYear != transfer.SourceFiscalYear
                || closing.DestinationFiscalYear != transfer.DestinationFiscalYear)
                mismatch = true;
        }
        foreach (var group in transfers.GroupBy(item => item.PaymentRequestId))
        {
            var request = rows.FirstOrDefault(item => item.ApprovalRequestId == group.Key);
            var ordered = group.OrderBy(item => item.SourceFiscalYear).ThenBy(item => item.DestinationFiscalYear).ToArray();
            Guid? priorId = null;
            Guid? priorBudgetId = request?.ProjectAnnualBudgetId;
            int? priorYear = request?.AnnualBudgetFiscalYear;
            decimal? priorAmount = null;
            foreach (var transfer in ordered)
            {
                if (request is null || transfer.SupersedesFundingTransferId != priorId
                    || transfer.SourceAnnualBudgetId != priorBudgetId || transfer.SourceFiscalYear != priorYear
                    || (priorAmount.HasValue && transfer.Amount > priorAmount.Value))
                    mismatch = true;
                priorId = transfer.Id;
                priorBudgetId = transfer.DestinationAnnualBudgetId;
                priorYear = transfer.DestinationFiscalYear;
                priorAmount = transfer.Amount;
            }
        }
        var transferIn = transfers.Where(item => item.DestinationAnnualBudgetId == budget.Id).Sum(item => item.Amount);
        var transferOut = transfers.Where(item => item.SourceAnnualBudgetId == budget.Id).Sum(item => item.Amount);
        var reversals = await (
            from detail in db.ProjectPaymentRequestDetails.IgnoreQueryFilters().AsNoTracking()
            join reversal in db.ApprovalRequests.IgnoreQueryFilters().AsNoTracking()
                on detail.ApprovalRequestId equals reversal.Id
            where detail.TenantId == tenantId && detail.ProjectId == projectId
                && detail.RequestKind == ProjectPaymentRequestKinds.ReversalRequest
                && detail.ReversesApprovalRequestId.HasValue
                && requestIds.Contains(detail.ReversesApprovalRequestId.Value)
                && reversal.TenantId == tenantId && !reversal.IsDeleted
                && reversal.Type == ReversalRequestType
                && reversal.Status == PaymentRequestStatuses.Approved
            select new
            {
                OriginalId = detail.ReversesApprovalRequestId!.Value,
                detail.ProjectAnnualBudgetId,
                detail.AnnualBudgetFiscalYear,
                detail.AnnualBudgetReference,
                detail.FundingTransferId,
                reversal.Amount,
                reversal.Currency,
                reversal.EntityType,
                reversal.EntityId
            })
            .ToListAsync(cancellationToken);
        var releaseForBudget = 0m;
        foreach (var reversal in reversals)
        {
            var original = rows.FirstOrDefault(item => item.ApprovalRequestId == reversal.OriginalId);
            var assignedBudgetId = reversal.ProjectAnnualBudgetId;
            if (reversal.FundingTransferId.HasValue)
            {
                var transfer = transfers.FirstOrDefault(item => item.Id == reversal.FundingTransferId.Value
                    && item.PaymentRequestId == reversal.OriginalId);
                if (transfer is null) mismatch = true;
                else assignedBudgetId = transfer.DestinationAnnualBudgetId;
            }
            if (reversal.EntityType != "Project"
                || reversal.EntityId != projectId
                || reversal.Amount is not > 0m
                || decimal.Round(reversal.Amount.Value, 4) != reversal.Amount.Value
                || original is null
                || !string.Equals(NormalizeCurrency(reversal.Currency), NormalizeCurrency(original.Currency), StringComparison.Ordinal)
                || (!reversal.FundingTransferId.HasValue && (reversal.AnnualBudgetFiscalYear != original.AnnualBudgetFiscalYear
                    || !string.Equals(original.AnnualBudgetReference, reversal.AnnualBudgetReference, StringComparison.Ordinal))))
                mismatch = true;
            if (assignedBudgetId == budget.Id) releaseForBudget += reversal.Amount ?? 0m;
        }
        foreach (var row in rows)
        {
            if (row.Status != PaymentRequestStatuses.Approved) continue;
            var released = reversals.Where(reversal => reversal.OriginalId == row.ApprovalRequestId).Sum(reversal => reversal.Amount ?? 0m);
            var settled = await db.PaymentSettlements.IgnoreQueryFilters().AsNoTracking()
                .Where(item => item.TenantId == tenantId && item.ProjectId == projectId && item.ProjectPaymentRequestId == row.ApprovalRequestId && !item.IsDeleted)
                .SumAsync(item => (decimal?)item.Amount, cancellationToken) ?? 0m;
            if ((row.Amount ?? 0m) < released + settled) mismatch = true;
        }
        var activeAmount = originalAmount + transferIn - transferOut - releaseForBudget;
        if (activeAmount < 0m)
            mismatch = true;
        return (Math.Max(0m, activeAmount), mismatch);
    }

    private static bool IsValidCurrency(string? currency)
    {
        var normalized = NormalizeCurrency(currency);
        return normalized.Length == 3 && normalized.All(character => character is >= 'A' and <= 'Z');
    }

    private async Task<int> CountLegacyApprovedRequestsAsync(Guid tenantId, Guid projectId, CancellationToken cancellationToken) =>
        await (from detail in PaymentDetailsQuery(tenantId, projectId)
               join request in PaymentRequestsQuery(tenantId, projectId) on detail.ApprovalRequestId equals request.Id
               where detail.RequestKind == ProjectPaymentRequestKinds.PaymentRequest
                   && detail.ProjectAnnualBudgetId == null && request.Status == PaymentRequestStatuses.Approved
               select request.Id).CountAsync(cancellationToken);

    private async Task<ProjectPaymentRequestBudgetAvailabilityDto> GetBudgetAvailabilityAsync(
        Guid tenantId, Project project, NEXUS.Modules.Organization.Application.Settings.FiscalYearPeriod period,
        Guid actorId, bool canWrite, CancellationToken cancellationToken)
    {
        var budget = await db.ProjectAnnualBudgets.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.ProjectId == project.Id
                && item.FiscalYear == period.FiscalYear && !item.IsDeleted, cancellationToken);
        var legacyCount = await CountLegacyApprovedRequestsAsync(tenantId, project.Id, cancellationToken);
        var canCreateAsProjectManager = canWrite
            && await IsCurrentProjectManagerAsync(tenantId, project, actorId, cancellationToken);
        if (budget is null)
        {
            return new ProjectPaymentRequestBudgetAvailabilityDto(
                null, period.FiscalYear, period.Label, period.StartDate, period.EndDate, null,
                NormalizeCurrency(project.Currency), null, null, 0m, 0m, legacyCount,
                "AnnualBudgetMissing", false, false);
        }

        var budgetError = ValidateAnnualBudgetAssociation(project, budget, period, requireApproved: false);
        var allocations = await GetActiveApprovedAllocationAsync(tenantId, project.Id, budget, cancellationToken);
        string availabilityStatus;
        decimal? approvedAmount = null;
        decimal? availableAmount = null;
        if (budgetError is not null || allocations.CurrencyMismatch)
        {
            availabilityStatus = "CurrencyOrPeriodMismatch";
        }
        else if (budget.Status != ProjectAnnualBudgetStatuses.Approved)
        {
            availabilityStatus = "AnnualBudgetDraft";
            availableAmount = 0m;
        }
        else if (budget.ClosedAtUtc.HasValue)
        {
            availabilityStatus = "FiscalYearClosed";
        }
        else if (legacyCount > 0)
        {
            availabilityStatus = "LegacyReconciliationRequired";
            approvedAmount = budget.TotalAnnualBudget;
        }
        else
        {
            approvedAmount = budget.TotalAnnualBudget;
            availableAmount = Math.Max(0m, approvedAmount.Value - allocations.Amount);
            availabilityStatus = availableAmount <= 0m ? "NoAvailableBalance" : "Ready";
        }

        return new ProjectPaymentRequestBudgetAvailabilityDto(
            budget.Id,
            period.FiscalYear,
            period.Label,
            period.StartDate,
            period.EndDate,
            budget.FinancialReference,
            NormalizeCurrency(project.Currency),
            budget.Status,
            approvedAmount,
            allocations.Amount,
            availableAmount,
            legacyCount,
            availabilityStatus,
            canCreateAsProjectManager && budgetError is null && legacyCount == 0,
            availabilityStatus == "Ready");
    }

    private static Error? ValidateAnnualBudgetAssociation(
        Project project, ProjectAnnualBudget budget,
        NEXUS.Modules.Organization.Application.Settings.FiscalYearPeriod period, bool requireApproved)
    {
        if (budget.TenantId != project.TenantId || budget.ProjectId != project.Id
            || budget.FiscalYear != period.FiscalYear
            || budget.FiscalYearStartDate != period.StartDate || budget.FiscalYearEndDate != period.EndDate
            || !string.Equals(NormalizeCurrency(budget.Currency), NormalizeCurrency(project.Currency), StringComparison.Ordinal))
            return Error.Conflict("The Annual Budget does not match this Tenant, Project, Fiscal Year, or Project currency.");
        if (requireApproved && budget.Status != ProjectAnnualBudgetStatuses.Approved)
            return Error.Conflict($"The Annual Budget for FY {period.FiscalYear} must be approved before the Payment Request can proceed.");
        if (requireApproved && budget.ClosedAtUtc.HasValue)
            return Error.Conflict($"The Annual Budget for FY {period.FiscalYear} is closed.");
        return null;
    }

    private static Error? ValidateAnnualBudgetAssociation(
        Project project, ProjectAnnualBudget budget, int expectedFiscalYear, bool requireApproved)
    {
        if (budget.TenantId != project.TenantId || budget.ProjectId != project.Id
            || budget.FiscalYear != expectedFiscalYear
            || !string.Equals(NormalizeCurrency(budget.Currency), NormalizeCurrency(project.Currency), StringComparison.Ordinal))
            return Error.Conflict("The Annual Budget does not match this Tenant, Project, Fiscal Year, or Project currency.");
        if (requireApproved && budget.Status != ProjectAnnualBudgetStatuses.Approved)
            return Error.Conflict($"The associated FY {expectedFiscalYear} Annual Budget must remain approved before final approval.");
        if (requireApproved && budget.ClosedAtUtc.HasValue)
            return Error.Conflict($"The associated FY {expectedFiscalYear} Annual Budget is closed.");
        return null;
    }

    private async Task<(Error? Error, bool CanWrite)> ValidateServiceAccessAsync(
        Guid tenantId, Guid projectId, Guid actorId, bool requireWrite, CancellationToken cancellationToken,
        string? actionPermission = null, string? screenViewPermission = null,
        IReadOnlyCollection<string>? additionalViewPermissions = null,
        Guid? assignedReviewRequestId = null, bool allowAssignedReviewer = false)
    {
        var current = management.Current;
        if (tenantId == Guid.Empty || projectId == Guid.Empty || actorId == Guid.Empty || current is null
            || current.TenantId != tenantId || current.UserId != actorId)
            return (Error.NotFound("Project not found."), false);

        var effective = await permissions.GetEffectivePermissionsAsync(actorId, tenantId, cancellationToken);
        var inProjectScope = current.Scope.ProjectIds.Contains(projectId);
        var assignedReviewerInProject = false;
        if (!inProjectScope && allowAssignedReviewer && actionPermission == ApprovePermission
            && assignedReviewRequestId.HasValue && effective.Contains(ApprovePermission))
        {
            var assignedRow = await FindRequestAsync(tenantId, projectId, assignedReviewRequestId.Value, cancellationToken);
            assignedReviewerInProject = assignedRow.HasValue
                && await IsAssignedPendingApproverAsync(tenantId, projectId, assignedRow.Value.Request,
                    assignedRow.Value.Detail, actorId, cancellationToken);
        }
        if (!inProjectScope && !assignedReviewerInProject)
            return (Error.NotFound("Project not found."), false);

        var canWrite = !current.EffectiveReadOnly && (effective.Contains(CreatePermission)
            || effective.Contains(EditDraftPermission) || effective.Contains(SubmitPermission)
            || effective.Contains(CancelPermission) || effective.Contains(ApprovePermission)
            || effective.Contains(SettlementRecordPermission) || effective.Contains(ClosingPreparePermission));
        var canRead = effective.Contains(screenViewPermission ?? ViewPermission)
            && (additionalViewPermissions is null || additionalViewPermissions.All(effective.Contains));
        if (requireWrite)
        {
            var hasAction = actionPermission is not null && effective.Contains(actionPermission);
            if (!hasAction)
                return (Error.Forbidden($"The {actionPermission ?? "specific Payment Request action"} permission is required."), false);
            if (current.EffectiveReadOnly)
                return (Error.Forbidden("This operation is unavailable in a read-only management context."), false);
        }
        if (!requireWrite && !canRead)
            return (Error.Forbidden("The requested canonical financial View permission is required."), false);
        return (null, canWrite);
    }

    private async Task<bool> HasProjectReadOrReviewerScopeAsync(
        Guid tenantId, Guid projectId, Guid requestId, Guid actorId, CancellationToken cancellationToken)
    {
        var current = management.Current;
        if (tenantId == Guid.Empty || projectId == Guid.Empty || requestId == Guid.Empty || actorId == Guid.Empty
            || current is null || current.TenantId != tenantId || current.UserId != actorId)
            return false;

        var row = await FindRequestAsync(tenantId, projectId, requestId, cancellationToken);
        if (row is null) return false;
        var effective = await permissions.GetEffectivePermissionsAsync(actorId, tenantId, cancellationToken);
        var scopedView = current.Scope.ProjectIds.Contains(projectId)
            && effective.Contains(ViewPermission);
        return scopedView || (effective.Contains(ApprovePermission)
            && await IsAssignedPendingApproverAsync(tenantId, projectId, row.Value.Request, row.Value.Detail,
                actorId, cancellationToken));
    }

    private async Task<bool> IsAssignedPendingApproverAsync(
        Guid tenantId,
        Guid projectId,
        ApprovalRequest request,
        ProjectPaymentRequestDetail detail,
        Guid actorId,
        CancellationToken cancellationToken)
    {
        if (request.RequestedBy == actorId
            || request.Status != PaymentRequestStatuses.Pending || detail.ApprovalMode is null
            || management.Current?.EffectiveReadOnly == true
            || !(await GetEligibleApproverIdsAsync(tenantId, projectId, cancellationToken)).Contains(actorId))
            return false;

        var assignments = await GetAssignmentsAsync(tenantId, request.Id, tracked: false, cancellationToken,
            detail.CurrentApprovalCycleId);
        var actions = await db.ApprovalActions.IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.TenantId == tenantId && !item.IsDeleted && item.ApprovalRequestId == request.Id)
            .ToListAsync(cancellationToken);
        var assigned = assignments.FirstOrDefault(item => item.ApproverUserId == actorId
            && item.State == PaymentRequestAssignmentStates.Pending);
        if (assigned is null || actions.Any(item => item.ApproverAssignmentId == assigned.Id))
            return false;

        return detail.ApprovalMode != PaymentRequestApprovalModes.Sequential
            || PaymentRequestWorkflowPolicy.GetActiveSequentialApprover(BuildStates(assignments, actions)) == actorId;
    }

    private async Task<bool> IsConfiguredCurrencyAsync(string currency, CancellationToken cancellationToken) =>
        currency.Length == 3 && currency.All(character => character is >= 'A' and <= 'Z')
        && await db.Currencies.IgnoreQueryFilters().AnyAsync(item => item.Code == currency && !item.IsDeleted, cancellationToken);

    private async Task<Error?> ValidateActiveActorAsync(Guid tenantId, Guid actorId, CancellationToken cancellationToken) =>
        actorId == Guid.Empty || !await db.Users.IgnoreQueryFilters().AnyAsync(item => item.Id == actorId
            && item.TenantId == tenantId && item.IsActive && !item.IsDeleted, cancellationToken)
            ? Error.Forbidden("An active tenant user is required.")
            : null;

    private async Task<bool> IsCurrentProjectManagerAsync(Guid tenantId, Project project, Guid actorId, CancellationToken cancellationToken) =>
        project.ProjectManagerId == actorId
        || await db.ProjectManagerAssignments.IgnoreQueryFilters().AnyAsync(item => item.TenantId == tenantId
            && item.ProjectId == project.Id && item.ProjectManagerId == actorId && item.IsCurrent && !item.IsDeleted, cancellationToken);

    private void AddAudit(Project project, ApprovalRequest request, Guid actorId, string? actorName, string action,
        string description, object? before, object? after, string changedFields)
    {
        var audit = new AuditEntry
        {
            TenantId = project.TenantId,
            ProjectId = project.Id,
            EntityType = EntityType,
            EntityId = request.Id,
            Action = action,
            UserId = actorId,
            UserName = string.IsNullOrWhiteSpace(actorName) ? null : actorName.Trim(),
            Timestamp = DateTime.UtcNow,
            Description = description,
            Changes = JsonSerializer.Serialize(BuildAuditChanges(request, before, after)),
            OriginalValues = before is null ? null : JsonSerializer.Serialize(before),
            NewValues = after is null ? null : JsonSerializer.Serialize(after),
            ChangedFields = changedFields,
            Source = "User",
            CreatedBy = actorId.ToString("D")
        };
        db.AuditEntries.Add(audit);
    }

    private void AddSettlementAudit(
        PaymentSettlement settlement,
        ApprovalRequest request,
        ProjectPaymentRequestDetail detail,
        Guid actorId,
        string? actorName,
        DateTime timestampUtc,
        decimal settledBefore,
        decimal settledAfter,
        decimal outstandingBefore,
        decimal outstandingAfter,
        string statusBefore,
        string statusAfter,
        int reportingFiscalYear)
    {
        var requestReference = Reference(request);
        var settlementCurrency = settlement.Currency ?? throw new InvalidOperationException(
            "A Payment Request settlement must persist the approved request currency.");
        var annualBudget = new
        {
            id = detail.ProjectAnnualBudgetId,
            reference = detail.AnnualBudgetReference,
            fiscalYear = detail.AnnualBudgetFiscalYear
        };
        var common = new
        {
            paymentRequestId = request.Id,
            paymentRequestReference = requestReference,
            settlementId = settlement.Id,
            settlementReference = settlement.PaymentReference,
            approvedAmount = request.Amount,
            settlementAmount = settlement.Amount,
            currency = settlementCurrency,
            paymentDate = DateOnly.FromDateTime(settlement.SettlementDate),
            reportingFiscalYear,
            annualBudget
        };
        var before = new
        {
            paymentRequestReference = requestReference,
            approvedAmount = request.Amount,
            totalSettled = settledBefore,
            outstandingAmount = outstandingBefore,
            settlementStatus = statusBefore,
            annualBudget
        };
        var after = new
        {
            paymentRequestReference = requestReference,
            approvedAmount = request.Amount,
            settlementAmount = settlement.Amount,
            settlementReference = settlement.PaymentReference,
            currency = settlementCurrency,
            paymentDate = DateOnly.FromDateTime(settlement.SettlementDate),
            totalSettled = settledAfter,
            outstandingAmount = outstandingAfter,
            settlementStatus = statusAfter,
            reportingFiscalYear,
            annualBudget
        };

        db.AuditEntries.Add(new AuditEntry
        {
            TenantId = settlement.TenantId,
            ProjectId = detail.ProjectId,
            EntityType = "PaymentSettlement",
            EntityId = settlement.Id,
            Action = "ActualSettlementRecorded",
            UserId = actorId,
            UserName = string.IsNullOrWhiteSpace(actorName) ? null : actorName.Trim(),
            Timestamp = timestampUtc,
            Description = $"Actual settlement {settlement.PaymentReference} recorded for Payment Request {requestReference}: {settlement.Amount:0.####} {settlementCurrency}; outstanding {outstandingAfter:0.####} {settlementCurrency}.",
            Changes = JsonSerializer.Serialize(new
            {
                paymentRequestReference = new { from = (string?)null, to = requestReference },
                settlementReference = new { from = (string?)null, to = settlement.PaymentReference },
                approvedAmount = new { from = (decimal?)null, to = request.Amount },
                settlementAmount = new { from = (decimal?)null, to = settlement.Amount },
                currency = new { from = (string?)null, to = settlementCurrency },
                paymentDate = new { from = (DateOnly?)null, to = DateOnly.FromDateTime(settlement.SettlementDate) },
                totalSettled = new { from = settledBefore, to = settledAfter },
                outstandingAmount = new { from = outstandingBefore, to = outstandingAfter },
                settlementStatus = new { from = statusBefore, to = statusAfter },
                reportingFiscalYear = new { from = (int?)null, to = reportingFiscalYear },
                annualBudgetId = new { from = (Guid?)null, to = detail.ProjectAnnualBudgetId },
                annualBudgetReference = new { from = (string?)null, to = detail.AnnualBudgetReference },
                annualBudgetFiscalYear = new { from = (int?)null, to = detail.AnnualBudgetFiscalYear }
            }),
            OriginalValues = JsonSerializer.Serialize(before),
            NewValues = JsonSerializer.Serialize(after),
            ChangedFields = "paymentRequestReference,settlementReference,approvedAmount,settlementAmount,currency,paymentDate,totalSettled,outstandingAmount,settlementStatus,reportingFiscalYear,annualBudgetId,annualBudgetReference,annualBudgetFiscalYear",
            Source = "User",
            CreatedBy = actorId.ToString("D")
        });

        if (!string.Equals(statusBefore, statusAfter, StringComparison.Ordinal))
        {
            db.AuditEntries.Add(new AuditEntry
            {
                TenantId = settlement.TenantId,
                ProjectId = detail.ProjectId,
                EntityType = "PaymentSettlement",
                EntityId = settlement.Id,
                Action = "SettlementStatusChanged",
                UserId = actorId,
                UserName = string.IsNullOrWhiteSpace(actorName) ? null : actorName.Trim(),
                Timestamp = timestampUtc,
                Description = $"Payment Request {requestReference} settlement status changed from {statusBefore} to {statusAfter}.",
                Changes = JsonSerializer.Serialize(new
                {
                    settlementStatus = new { from = statusBefore, to = statusAfter },
                    approvedAmount = new { from = request.Amount, to = request.Amount },
                    totalSettled = new { from = settledBefore, to = settledAfter },
                    outstandingAmount = new { from = outstandingBefore, to = outstandingAfter },
                    currency = new { from = settlementCurrency, to = settlementCurrency },
                    paymentRequestReference = new { from = requestReference, to = requestReference },
                    settlementReference = new { from = settlement.PaymentReference, to = settlement.PaymentReference },
                    annualBudgetId = new { from = detail.ProjectAnnualBudgetId, to = detail.ProjectAnnualBudgetId },
                    annualBudgetReference = new { from = detail.AnnualBudgetReference, to = detail.AnnualBudgetReference },
                    annualBudgetFiscalYear = new { from = detail.AnnualBudgetFiscalYear, to = detail.AnnualBudgetFiscalYear },
                    annualBudgetLineId = new { from = detail.ProjectAnnualBudgetLineId, to = detail.ProjectAnnualBudgetLineId },
                    plannedPaymentInstallmentId = new { from = detail.PlannedPaymentInstallmentId, to = detail.PlannedPaymentInstallmentId },
                    approvalCycleId = new { from = detail.CurrentApprovalCycleId, to = detail.CurrentApprovalCycleId },
                    approvalCycleNumber = new { from = detail.CurrentApprovalCycleNumber, to = detail.CurrentApprovalCycleNumber }
                }),
                OriginalValues = JsonSerializer.Serialize(new { status = statusBefore, settledAmount = settledBefore, outstandingAmount = outstandingBefore }),
                NewValues = JsonSerializer.Serialize(new { status = statusAfter, settledAmount = settledAfter, outstandingAmount = outstandingAfter, common }),
                ChangedFields = "settlementStatus,approvedAmount,totalSettled,outstandingAmount,currency,paymentRequestReference,settlementReference,annualBudgetId,annualBudgetReference,annualBudgetFiscalYear,annualBudgetLineId,plannedPaymentInstallmentId,approvalCycleId,approvalCycleNumber",
                Source = "User",
                CreatedBy = actorId.ToString("D")
            });
        }
    }

    private static ProjectPaymentRequestSettlementDto MapPaymentRequestSettlement(
        PaymentSettlement settlement, ApprovalRequest request, int reportingFiscalYear) => new(
        settlement.Id,
        request.Id,
        settlement.ProjectId,
        Reference(request),
        settlement.PaymentReference,
        settlement.Amount,
        settlement.Currency ?? throw new InvalidOperationException(
            "A Payment Request settlement must persist the approved request currency."),
        DateOnly.FromDateTime(settlement.SettlementDate),
        reportingFiscalYear,
        settlement.Description,
        DateTime.SpecifyKind(settlement.CreatedAt, DateTimeKind.Utc),
        Guid.TryParse(settlement.CreatedBy, out var actorId) ? actorId : null);

    private static string SettlementStatus(decimal settledAmount, decimal outstandingAmount) =>
        settledAmount <= 0m ? "Not Settled" : outstandingAmount <= 0m ? "Fully Settled" : "Partially Settled";

    private static object BuildAuditChanges(ApprovalRequest request, object? before, object? after)
    {
        JsonElement? beforeJson = before is null ? null : JsonSerializer.SerializeToElement(before);
        JsonElement? afterJson = after is null ? null : JsonSerializer.SerializeToElement(after);
        return new
        {
            financialReference = Change(ReadString(beforeJson, "financialReference"), ReadString(afterJson, "financialReference") ?? Reference(request)),
            amount = Change(ReadDecimal(beforeJson, "amount"), ReadDecimal(afterJson, "amount") ?? request.Amount),
            currency = Change(ReadString(beforeJson, "currency"), ReadString(afterJson, "currency") ?? request.Currency),
            status = Change(ReadString(beforeJson, "status"), ReadString(afterJson, "status") ?? request.Status),
            approvalMode = Change(ReadString(beforeJson, "approvalMode"), ReadString(afterJson, "approvalMode")),
            annualBudgetId = Change(ReadString(beforeJson, "annualBudgetId"), ReadString(afterJson, "annualBudgetId")),
            annualBudgetReference = Change(ReadString(beforeJson, "annualBudgetReference"), ReadString(afterJson, "annualBudgetReference")),
            fiscalYear = Change(ReadDecimal(beforeJson, "fiscalYear"), ReadDecimal(afterJson, "fiscalYear")),
            approvers = Change(ReadString(beforeJson, "approvers"), ReadString(afterJson, "approvers")),
            decision = Change(ReadString(beforeJson, "decision"), ReadString(afterJson, "decision")),
            comment = Change(ReadString(beforeJson, "comment"), ReadString(afterJson, "comment")),
            milestone = Change(ReadString(beforeJson, "milestone"), ReadString(afterJson, "milestone")),
            annualBudgetLineId = Change(ReadString(beforeJson, "annualBudgetLineId"), ReadString(afterJson, "annualBudgetLineId")),
            plannedPaymentInstallmentId = Change(ReadString(beforeJson, "plannedPaymentInstallmentId"), ReadString(afterJson, "plannedPaymentInstallmentId")),
            approvalCycleId = Change(ReadString(beforeJson, "approvalCycleId"), ReadString(afterJson, "approvalCycleId")),
            approvalCycleNumber = Change(ReadDecimal(beforeJson, "approvalCycleNumber"), ReadDecimal(afterJson, "approvalCycleNumber"))
        };
    }

    private static Dictionary<string, object?> AuditValues(ApprovalRequest request, ProjectPaymentRequestDetail detail, object? extra,
        string? statusOverride = null)
    {
        var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["financialReference"] = Reference(request),
            ["amount"] = request.Amount,
            ["currency"] = request.Currency,
            ["status"] = statusOverride ?? request.Status,
            ["approvalMode"] = detail.ApprovalMode,
            ["milestone"] = detail.PlanActivityId?.ToString("D"),
            ["annualBudgetId"] = detail.ProjectAnnualBudgetId?.ToString("D"),
            ["annualBudgetReference"] = detail.AnnualBudgetReference,
            ["fiscalYear"] = detail.AnnualBudgetFiscalYear,
            ["annualBudgetLineId"] = detail.ProjectAnnualBudgetLineId?.ToString("D"),
            ["plannedPaymentInstallmentId"] = detail.PlannedPaymentInstallmentId?.ToString("D"),
            ["approvalCycleId"] = detail.CurrentApprovalCycleId?.ToString("D"),
            ["approvalCycleNumber"] = detail.CurrentApprovalCycleNumber
        };
        if (extra is not null)
        {
            var extraElement = JsonSerializer.SerializeToElement(extra);
            if (extraElement.ValueKind == JsonValueKind.Object)
                foreach (var property in extraElement.EnumerateObject()) values[property.Name] = property.Value.Clone();
        }
        return values;
    }

    private static object Change<T>(T? from, T? to) => new { from, to };

    private static string? ReadString(JsonElement? value, string name) =>
        value is { ValueKind: JsonValueKind.Object } element
        && element.TryGetProperty(name, out var property)
        ? property.ValueKind == JsonValueKind.String ? property.GetString() : property.ToString()
        : null;

    private static decimal? ReadDecimal(JsonElement? value, string name) =>
        value is { ValueKind: JsonValueKind.Object } element
        && element.TryGetProperty(name, out var property)
        && property.ValueKind == JsonValueKind.Number
        && property.TryGetDecimal(out var number) ? number : null;

    private static PaymentRequestApproverSelectionDto[] NormalizeSelections(
        IReadOnlyList<PaymentRequestApproverSelectionDto>? items, string? mode) => (items ?? [])
        .Select(item => new PaymentRequestApproverSelectionDto(
            item.UserId,
            string.Equals(mode, PaymentRequestApprovalModes.Sequential, StringComparison.OrdinalIgnoreCase) ? item.Position : null,
            string.Equals(item.Requirement, PaymentRequestApproverRequirements.Mandatory, StringComparison.OrdinalIgnoreCase)
                ? PaymentRequestApproverRequirements.Mandatory
                : string.Equals(item.Requirement, PaymentRequestApproverRequirements.Optional, StringComparison.OrdinalIgnoreCase)
                    ? PaymentRequestApproverRequirements.Optional
                    : item.Requirement?.Trim() ?? string.Empty))
        .ToArray();

    private static string? NormalizeMode(string? mode) =>
        string.Equals(mode, PaymentRequestApprovalModes.Sequential, StringComparison.OrdinalIgnoreCase)
            ? PaymentRequestApprovalModes.Sequential
            : string.Equals(mode, PaymentRequestApprovalModes.Parallel, StringComparison.OrdinalIgnoreCase)
                ? PaymentRequestApprovalModes.Parallel
                : mode?.Trim();

    private static bool SameSelections(
        IReadOnlyCollection<PaymentRequestApproverAssignment> existing,
        IReadOnlyCollection<PaymentRequestApproverSelectionDto> requested)
    {
        if (existing.Count != requested.Count || requested.Select(item => item.UserId).Distinct().Count() != requested.Count) return false;
        var expected = requested.ToDictionary(item => item.UserId);
        return existing.All(item => expected.TryGetValue(item.ApproverUserId, out var selected)
            && item.Position == selected.Position
            && string.Equals(item.Requirement, selected.Requirement, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeDecision(string action) => action switch
    {
        "Approve" or "Approved" => PaymentRequestDecisions.Approved,
        "Reject" or "Rejected" => PaymentRequestDecisions.Rejected,
        "ReturnForRevision" or "ReturnedForRevision" => PaymentRequestDecisions.ReturnedForRevision,
        _ => action
    };

    private static bool IsValidVersionlessAmount(decimal amount) =>
        amount > 0m && amount < 1_000_000_000_000_000m && decimal.Round(amount, 4) == amount;

    private static bool MatchesVersion(byte[]? current, byte[]? expected) =>
        current is { Length: > 0 } && expected is { Length: > 0 } && current.SequenceEqual(expected);

    private static string NormalizeCurrency(string? currency) => (currency ?? string.Empty).Trim().ToUpperInvariant();

    private static string Fingerprint(
        string title, decimal amount, string reason, Guid? milestoneId,
        Guid? plannedPaymentInstallmentId = null, Guid? annualBudgetId = null, Guid? annualBudgetLineId = null)
    {
        // Preserve the historical payload for existing records while binding new
        // idempotent creates to their complete installment and funding selection.
        var canonical = plannedPaymentInstallmentId.HasValue
            ? JsonSerializer.Serialize(new
            {
                title, amount, reason, milestoneId, plannedPaymentInstallmentId,
                annualBudgetId, annualBudgetLineId
            })
            : JsonSerializer.Serialize(new { title, amount, reason, milestoneId });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private static string SubmissionFingerprint(
        ApprovalRequest request,
        ProjectPaymentRequestDetail detail,
        string approvalMode,
        IReadOnlyCollection<PaymentRequestApproverSelectionDto> approvers) =>
        SubmissionFingerprint(request.Id, request.Amount, request.Currency,
            detail.PlannedPaymentInstallmentId, detail.ProjectAnnualBudgetId,
            detail.ProjectAnnualBudgetLineId, approvalMode, approvers);

    private static string SubmissionFingerprint(
        ProjectPaymentRequestApprovalCycle cycle,
        string approvalMode,
        IReadOnlyCollection<PaymentRequestApproverSelectionDto> approvers) =>
        SubmissionFingerprint(cycle.ApprovalRequestId, cycle.Amount, cycle.Currency,
            cycle.PlannedPaymentInstallmentId, cycle.ProjectAnnualBudgetId,
            cycle.ProjectAnnualBudgetLineId, approvalMode, approvers);

    private static string SubmissionFingerprint(
        Guid requestId, decimal? amount, string? currency, Guid? installmentId,
        Guid? annualBudgetId, Guid? annualBudgetLineId, string approvalMode,
        IReadOnlyCollection<PaymentRequestApproverSelectionDto> approvers)
    {
        var canonical = JsonSerializer.Serialize(new
        {
            requestId,
            amount,
            currency = NormalizeCurrency(currency),
            installmentId,
            annualBudgetId,
            annualBudgetLineId,
            approvalMode,
            approvers = approvers.OrderBy(item => item.Position ?? int.MaxValue)
                .ThenBy(item => item.UserId)
                .Select(item => new { item.UserId, item.Position, item.Requirement })
        });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private static string MakeReference(Guid id) => $"PPR-{id:N}".ToUpperInvariant();

    private static string Reference(ApprovalRequest request) => string.IsNullOrWhiteSpace(request.EntityNumber)
        ? MakeReference(request.Id)
        : request.EntityNumber;
}
