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
    IManagementContextAccessor management)
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
        var canCreate = writableContext && effective.Contains(CreatePermission);
        var canEdit = writableContext && effective.Contains(EditDraftPermission);
        var canSubmit = writableContext && effective.Contains(SubmitPermission);
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
        var list = rows.Select(row => ToListItem(row.request, row.detail,
            assignments.Where(item => item.ApprovalRequestId == row.request.Id).ToArray(),
            actions.Where(item => item.ApprovalRequestId == row.request.Id).ToArray(),
            names, actorId, canEdit, canSubmit, canApprove, canCancel,
            row.detail.ProjectAnnualBudgetId.HasValue && budgets.TryGetValue(row.detail.ProjectAnnualBudgetId.Value, out var budget)
                ? budget
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

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
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
        if (dto.ProjectPlanMilestoneId.HasValue
            && !await milestoneLookup.IsActiveMilestoneForProjectAsync(projectId, dto.ProjectPlanMilestoneId.Value, cancellationToken))
            return Result<ProjectPaymentRequestDetailDto>.Failure(Error.Validation("The selected milestone is not active in a working plan for this project."));

        var fingerprint = Fingerprint(title, dto.Amount, reason, dto.ProjectPlanMilestoneId);
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
            await transaction.CommitAsync(cancellationToken);
            return Result<ProjectPaymentRequestDetailDto>.Success(await ToDetailAsync(priorRequest, priorDetail, actorId, cancellationToken));
        }

        // The API derives the effective financial date from server UTC and the Tenant's
        // configured time zone. Clients cannot select or override a Fiscal Year.
        var requestedAt = DateTimeOffset.UtcNow;
        var period = await tenantFiscalYear.ResolveForTimestampAsync(tenantId, requestedAt, cancellationToken);
        var annualBudget = await db.ProjectAnnualBudgets.IgnoreQueryFilters()
            .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.ProjectId == projectId
                && item.FiscalYear == period.FiscalYear && !item.IsDeleted, cancellationToken);
        if (annualBudget is null)
            return Result<ProjectPaymentRequestDetailDto>.Failure(Error.Conflict(
                $"Create an annual budget for FY {period.FiscalYear} before creating a Payment Request."));
        var budgetError = ValidateAnnualBudgetAssociation(project, annualBudget, period, requireApproved: false);
        if (budgetError is not null)
            return Result<ProjectPaymentRequestDetailDto>.Failure(budgetError);

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
            AnnualBudgetFiscalYear = annualBudget.FiscalYear,
            AnnualBudgetReference = annualBudget.FinancialReference,
            PlanActivityId = dto.ProjectPlanMilestoneId,
            RequestKind = ProjectPaymentRequestKinds.PaymentRequest,
            IdempotencyKey = dto.IdempotencyKey,
            RequestFingerprint = fingerprint
        };

        db.ApprovalRequests.Add(request);
        db.ProjectPaymentRequestDetails.Add(detail);
        AddAudit(project, request, actorId, actorName, "Created", "Payment Request created", null,
            AuditValues(request, detail, new { annualBudgetId = annualBudget.Id, fiscalYear = annualBudget.FiscalYear }),
            "financialReference,amount,currency,status,annualBudgetId,fiscalYear");

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
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
        if (request.Status != PaymentRequestStatuses.Draft || request.RequestedBy != actorId)
            return Result<ProjectPaymentRequestDetailDto>.Failure(Error.Forbidden("Only the requester may edit a draft Payment Request."));
        if (!IsValidVersionlessAmount(dto.Amount))
            return Result<ProjectPaymentRequestDetailDto>.Failure(Error.Validation("Amount must be positive and use no more than four decimal places."));

        var title = dto.Title?.Trim() ?? string.Empty;
        var reason = dto.Reason?.Trim() ?? string.Empty;
        var createShape = new CreatePaymentRequestDto(title, dto.Amount, reason, dto.ProjectPlanMilestoneId, detail.IdempotencyKey);
        var validation = PaymentRequestWorkflowPolicy.ValidateDraft(createShape, project.Currency);
        if (validation.Count > 0) return Result<ProjectPaymentRequestDetailDto>.Failure(Error.Validation(string.Join(",", validation)));
        if (!await IsConfiguredCurrencyAsync(NormalizeCurrency(project.Currency), cancellationToken))
            return Result<ProjectPaymentRequestDetailDto>.Failure(Error.Conflict("The project currency is not configured in the currency catalog."));
        if (dto.ProjectPlanMilestoneId.HasValue
            && !await milestoneLookup.IsActiveMilestoneForProjectAsync(projectId, dto.ProjectPlanMilestoneId.Value, cancellationToken))
            return Result<ProjectPaymentRequestDetailDto>.Failure(Error.Validation("The selected milestone is not active in a working plan for this project."));

        var before = AuditValues(request, detail, null);
        request.Title = title;
        request.Description = reason;
        request.Amount = dto.Amount;
        request.Currency = NormalizeCurrency(project.Currency);
        request.UpdatedBy = actorId.ToString("D");
        detail.PlanActivityId = dto.ProjectPlanMilestoneId;
        detail.RequestFingerprint = Fingerprint(title, dto.Amount, reason, dto.ProjectPlanMilestoneId);
        AddAudit(project, request, actorId, actorName, "Edited", "Payment Request edited", before,
            AuditValues(request, detail, null), "financialReference,amount,currency,milestone");

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
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var project = await FindProjectForUpdateAsync(tenantId, projectId, cancellationToken);
        if (project is null) return Result<ProjectPaymentRequestDetailDto>.Failure(Error.NotFound("Project not found"));
        var accessError = await ValidateActiveActorAsync(tenantId, actorId, cancellationToken);
        if (accessError is not null) return Result<ProjectPaymentRequestDetailDto>.Failure(accessError);
        var row = await FindRequestTrackedAsync(tenantId, projectId, requestId, cancellationToken);
        if (row is null) return Result<ProjectPaymentRequestDetailDto>.Failure(Error.NotFound("Payment Request not found"));
        var (request, detail) = row.Value;

        var approvalMode = NormalizeMode(dto.ApprovalMode);
        var selected = NormalizeSelections(dto.Approvers, approvalMode);
        if (request.Status != PaymentRequestStatuses.Draft)
        {
            var existingAssignments = await GetAssignmentsAsync(tenantId, requestId, tracked: false, cancellationToken);
            if (request.RequestedBy == actorId
                && request.Status == PaymentRequestStatuses.Pending
                && string.Equals(detail.ApprovalMode, approvalMode, StringComparison.OrdinalIgnoreCase)
                && SameSelections(existingAssignments, selected))
            {
                await transaction.CommitAsync(cancellationToken);
                return Result<ProjectPaymentRequestDetailDto>.Success(await ToDetailAsync(request, detail, actorId, cancellationToken));
            }
            return Result<ProjectPaymentRequestDetailDto>.Failure(Error.Conflict("Only draft Payment Requests can be submitted."));
        }
        if (!MatchesVersion(request.RowVersion, dto.RowVersion))
            return Result<ProjectPaymentRequestDetailDto>.Failure(Error.Conflict("Payment Request changed. Refresh before submitting."));
        if (request.RequestedBy != actorId)
            return Result<ProjectPaymentRequestDetailDto>.Failure(Error.Forbidden("Only the requester may submit this Payment Request."));

        if (!await IsConfiguredCurrencyAsync(NormalizeCurrency(project.Currency), cancellationToken))
            return Result<ProjectPaymentRequestDetailDto>.Failure(Error.Conflict("The project currency is not configured in the currency catalog."));

        var validation = PaymentRequestWorkflowPolicy.ValidateApprovalSetup(project.FinanceApprovalMode, approvalMode, selected);
        if (validation.Count > 0) return Result<ProjectPaymentRequestDetailDto>.Failure(Error.Validation(string.Join(",", validation)));
        var eligibility = await GetEligibleApproverIdsAsync(tenantId, projectId, cancellationToken);
        if (selected.Any(item => !eligibility.Contains(item.UserId)))
            return Result<ProjectPaymentRequestDetailDto>.Failure(Error.Validation(
                "Every approver must be an active Project Team member linked to an active user with ProjectPaymentRequest.Approve permission."));
        if (request.Amount is null or <= 0 || !string.Equals(request.Currency, NormalizeCurrency(project.Currency), StringComparison.Ordinal))
            return Result<ProjectPaymentRequestDetailDto>.Failure(Error.Conflict("Payment Request amount or currency no longer matches the project."));

        if (detail.ProjectAnnualBudgetId is null || detail.AnnualBudgetFiscalYear is null)
            return Result<ProjectPaymentRequestDetailDto>.Failure(Error.Conflict(
                "This historical Payment Request has no Fiscal Year budget association. Reconcile it before submitting."));
        var currentPeriod = await tenantFiscalYear.ResolveForTimestampAsync(tenantId, DateTimeOffset.UtcNow, cancellationToken);
        if (currentPeriod.FiscalYear != detail.AnnualBudgetFiscalYear.Value)
            return Result<ProjectPaymentRequestDetailDto>.Failure(Error.Conflict(
                "A draft Payment Request can only be submitted in its associated current Fiscal Year. Create a new request for the current year."));
        var annualBudget = await FindAnnualBudgetForUpdateAsync(tenantId, projectId, detail.ProjectAnnualBudgetId.Value, cancellationToken);
        if (annualBudget is null)
            return Result<ProjectPaymentRequestDetailDto>.Failure(Error.Conflict("The associated Annual Budget is unavailable."));
        var budgetError = ValidateAnnualBudgetAssociation(project, annualBudget, currentPeriod, requireApproved: true);
        if (budgetError is not null)
            return Result<ProjectPaymentRequestDetailDto>.Failure(budgetError);
        if (annualBudget.ClosedAtUtc.HasValue)
            return Result<ProjectPaymentRequestDetailDto>.Failure(Error.Conflict("The associated Fiscal Year is closed. A new request cannot be submitted against a closed Annual Budget."));
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

        detail.ApprovalMode = approvalMode;
        detail.SubmittedAt = DateTime.UtcNow;
        request.Status = PaymentRequestStatuses.Pending;
        request.CurrentApproverId = approvalMode == PaymentRequestApprovalModes.Sequential ? selected.OrderBy(item => item.Position).First().UserId : null;
        request.ApprovalLevel = approvalMode == PaymentRequestApprovalModes.Sequential ? 1 : 0;
        request.UpdatedBy = actorId.ToString("D");

        var assignments = selected.Select(selection => new PaymentRequestApproverAssignment
        {
            TenantId = tenantId,
            ApprovalRequestId = requestId,
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
        var before = AuditValues(request, detail, null, statusOverride: PaymentRequestStatuses.Draft);
        AddAudit(project, request, actorId, actorName, "Submitted", "Payment Request submitted for approval", before,
            AuditValues(request, detail, new { approvalMode, approvers = approverSummary }),
            "status,approvalMode,approvers,amount,currency,annualBudgetId,fiscalYear");
        AddAudit(project, request, actorId, actorName, "ApproversAssigned", $"Approvers assigned to {Reference(request)}: {approverSummary}",
            null, AuditValues(request, detail, new { approvalMode, approvers = approverSummary }),
            "approvalMode,approvers");

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
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
            return Result<ProjectPaymentRequestDetailDto>.Failure(Error.Validation("Decision must be Approved or Rejected."));

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
        if (request.Status != PaymentRequestStatuses.Pending || detail.ApprovalMode is null)
            return Result<ProjectPaymentRequestDetailDto>.Failure(Error.Conflict("This Payment Request is no longer awaiting approval."));

        var eligibleApprovers = await GetEligibleApproverIdsAsync(tenantId, projectId, cancellationToken);
        if (!eligibleApprovers.Contains(actorId))
            return Result<ProjectPaymentRequestDetailDto>.Failure(Error.Forbidden(
                "An approver must remain an active eligible Project Team member with ProjectPaymentRequest.Approve permission to decide."));

        var assignments = await GetAssignmentsAsync(tenantId, requestId, tracked: true, cancellationToken);
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
        var approved = string.Equals(dto.Decision, PaymentRequestDecisions.Approved, StringComparison.OrdinalIgnoreCase);
        assignment.State = approved ? PaymentRequestAssignmentStates.Approved : PaymentRequestAssignmentStates.Rejected;
        assignment.UpdatedBy = actorId.ToString("D");
        request.UpdatedBy = actorId.ToString("D");
        db.ApprovalActions.Add(new ApprovalAction
        {
            TenantId = tenantId,
            ApprovalRequestId = requestId,
            ApproverId = actorId,
            ApproverAssignmentId = assignment.Id,
            Action = approved ? "Approve" : "Reject",
            Comments = dto.Comment?.Trim(),
            ActionedAt = now,
            ApprovalLevel = detail.ApprovalMode == PaymentRequestApprovalModes.Sequential ? assignment.Position ?? 0 : 0,
            CreatedBy = actorId.ToString("D")
        });

        var prior = AuditValues(request, detail, null, statusOverride: PaymentRequestStatuses.Pending);
        var individualAction = approved ? "ApproverApproved" : "ApproverRejected";
        AddAudit(project, request, actorId, actorName, individualAction,
            $"{(approved ? "Approval" : "Rejection")} recorded by {actorName ?? actorId.ToString("D")} for {Reference(request)}",
            prior, AuditValues(request, detail, new { decision = approved ? PaymentRequestDecisions.Approved : PaymentRequestDecisions.Rejected, comment = dto.Comment?.Trim() }),
            "decision,approver,comment,amount,currency");

        var projectedStates = BuildStates(assignments, actions, actorId, approved ? "Approve" : "Reject");
        var status = detail.ApprovalMode == PaymentRequestApprovalModes.Sequential
            ? PaymentRequestWorkflowPolicy.EvaluateSequentialStatus(projectedStates)
            : PaymentRequestWorkflowPolicy.EvaluateParallelStatus(projectedStates);
        if (status is PaymentRequestStatuses.Approved or PaymentRequestStatuses.Rejected)
        {
            if (status == PaymentRequestStatuses.Approved)
            {
                if (!await IsConfiguredCurrencyAsync(NormalizeCurrency(project.Currency), cancellationToken))
                    return Result<ProjectPaymentRequestDetailDto>.Failure(Error.Conflict("The project currency is not configured in the currency catalog."));
                if (detail.ProjectAnnualBudgetId is null || detail.AnnualBudgetFiscalYear is null)
                    return Result<ProjectPaymentRequestDetailDto>.Failure(Error.Conflict(
                        "This historical Payment Request has no Fiscal Year budget association. Reconcile it before approval."));
                var annualBudget = await FindAnnualBudgetForUpdateAsync(
                    tenantId, projectId, detail.ProjectAnnualBudgetId.Value, cancellationToken);
                if (annualBudget is null)
                    return Result<ProjectPaymentRequestDetailDto>.Failure(Error.Conflict("The associated Annual Budget is unavailable."));
                var budgetError = ValidateAnnualBudgetAssociation(project, annualBudget,
                    detail.AnnualBudgetFiscalYear.Value, requireApproved: true);
                if (budgetError is not null)
                    return Result<ProjectPaymentRequestDetailDto>.Failure(budgetError);
                if (annualBudget.ClosedAtUtc.HasValue)
                    return Result<ProjectPaymentRequestDetailDto>.Failure(Error.Conflict("A Payment Request cannot be finally approved against a closed Fiscal Year."));
                var legacyApprovedCount = await CountLegacyApprovedRequestsAsync(tenantId, projectId, cancellationToken);
                if (legacyApprovedCount > 0)
                    return Result<ProjectPaymentRequestDetailDto>.Failure(Error.Conflict(
                        "Approved historical Payment Requests have no Fiscal Year association. Reconcile the project history before approving new requests."));
                var allocated = await GetActiveApprovedAllocationAsync(tenantId, projectId, annualBudget, cancellationToken);
        var currency = string.IsNullOrWhiteSpace(project.Currency) ? string.Empty : NormalizeCurrency(project.Currency);
                if (allocated.CurrencyMismatch || request.Amount is null
                    || !string.Equals(NormalizeCurrency(request.Currency), currency, StringComparison.Ordinal)
                    || !string.Equals(NormalizeCurrency(annualBudget.Currency), currency, StringComparison.Ordinal))
                    return Result<ProjectPaymentRequestDetailDto>.Failure(Error.Conflict(
                        "Approved requests or the Annual Budget contain an incompatible currency. Resolve the project financial data before approval."));
                var available = annualBudget.TotalAnnualBudget - allocated.Amount;
                if (request.Amount.Value > available)
                    return Result<ProjectPaymentRequestDetailDto>.Failure(Error.Conflict(
                        "The approved amount exceeds the available amount in the associated Annual Budget."));
                request.Status = PaymentRequestStatuses.Approved;
                detail.CompletedAt = now;
                AddAudit(project, request, actorId, actorName, "Approved",
                    $"Payment Request {Reference(request)} approved against FY {annualBudget.FiscalYear} Annual Budget {annualBudget.FinancialReference}", prior,
                    AuditValues(request, detail, new { status = PaymentRequestStatuses.Approved }),
                    "status,amount,currency,annualBudgetId,fiscalYear");
                AddAudit(project, request, actorId, actorName, "BudgetReserved",
                    $"{request.Amount:0.####} {currency} reserved against FY {annualBudget.FiscalYear} Annual Budget {annualBudget.FinancialReference} for {Reference(request)}",
                    null, AuditValues(request, detail, new
                    {
                        allocation = request.Amount,
                        status = PaymentRequestStatuses.Approved,
                        annualBudgetId = annualBudget.Id,
                        annualBudgetReference = annualBudget.FinancialReference,
                        fiscalYear = annualBudget.FiscalYear,
                        availableAfter = available - request.Amount.Value
                    }), "financialReference,amount,currency,annualBudgetId,fiscalYear");
            }
            else
            {
                request.Status = PaymentRequestStatuses.Rejected;
                detail.CompletedAt = now;
                AddAudit(project, request, actorId, actorName, "Rejected", $"Payment Request {Reference(request)} rejected", prior,
                    AuditValues(request, detail, new { status = PaymentRequestStatuses.Rejected }), "status,amount,currency");
            }
            request.CurrentApproverId = null;
        }
        else if (detail.ApprovalMode == PaymentRequestApprovalModes.Sequential)
        {
            request.CurrentApproverId = PaymentRequestWorkflowPolicy.GetActiveSequentialApprover(projectedStates);
            request.ApprovalLevel = assignments.FirstOrDefault(item => item.ApproverUserId == request.CurrentApproverId)?.Position ?? request.ApprovalLevel;
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
        if (request.RequestedBy != actorId)
            return Result<ProjectPaymentRequestDetailDto>.Failure(Error.Forbidden("Only the requester may cancel this Payment Request."));

        var prior = AuditValues(request, detail, null);
        request.Status = PaymentRequestStatuses.Cancelled;
        request.CurrentApproverId = null;
        request.UpdatedBy = actorId.ToString("D");
        detail.CompletedAt = DateTime.UtcNow;
        AddAudit(project, request, actorId, actorName, "Cancelled", $"Payment Request {Reference(request)} cancelled before approval", prior,
            AuditValues(request, detail, new { status = PaymentRequestStatuses.Cancelled }), "status,financialReference,amount,currency");

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
        var assignments = await GetAssignmentsAsync(detail.TenantId, request.Id, tracked: false, cancellationToken);
        var actions = await db.ApprovalActions.IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.TenantId == detail.TenantId && !item.IsDeleted && item.ApprovalRequestId == request.Id)
            .ToListAsync(cancellationToken);
        var ids = assignments.Select(item => item.ApproverUserId).Append(request.RequestedBy ?? Guid.Empty).Where(id => id != Guid.Empty);
        var names = await LoadUserNamesAsync(detail.TenantId, ids, cancellationToken);
        var approvers = MapApprovers(request, detail, assignments, actions, names, actorId);
        var requesterName = names.GetValueOrDefault(request.RequestedBy ?? Guid.Empty) ?? string.Empty;
        var effective = await permissions.GetEffectivePermissionsAsync(actorId, detail.TenantId, cancellationToken);
        var writableContext = management.Current?.EffectiveReadOnly != true;
        var canEdit = writableContext && effective.Contains(EditDraftPermission)
            && request.Status == PaymentRequestStatuses.Draft && request.RequestedBy == actorId;
        var canSubmit = writableContext && effective.Contains(SubmitPermission)
            && request.Status == PaymentRequestStatuses.Draft && request.RequestedBy == actorId;
        var canDecide = writableContext && effective.Contains(ApprovePermission) && approvers.Any(item => item.CanDecide);
        var canCancel = writableContext && effective.Contains(CancelPermission)
            && PaymentRequestWorkflowPolicy.CanCancelBeforeApproval(request.Status) && request.RequestedBy == actorId;
        var milestoneName = detail.PlanActivityId.HasValue
            ? await milestoneLookup.FindActiveMilestoneNameForProjectAsync(detail.ProjectId, detail.PlanActivityId.Value, cancellationToken)
            : null;
        var annualBudget = detail.ProjectAnnualBudgetId.HasValue
            ? await db.ProjectAnnualBudgets.IgnoreQueryFilters().AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == detail.ProjectAnnualBudgetId.Value
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
            detail.AnnualBudgetReference ?? annualBudget?.FinancialReference);
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
        ProjectAnnualBudget? annualBudget)
    {
        var approvers = MapApprovers(request, detail, assignments, actions, names, actorId);
        var canEdit = canEditPermission && request.Status == PaymentRequestStatuses.Draft && request.RequestedBy == actorId;
        var canSubmit = canSubmitPermission && request.Status == PaymentRequestStatuses.Draft && request.RequestedBy == actorId;
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
            detail.AnnualBudgetReference ?? annualBudget?.FinancialReference);
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
        Guid tenantId, Guid requestId, bool tracked, CancellationToken cancellationToken)
    {
        var query = db.PaymentRequestApproverAssignments.IgnoreQueryFilters()
            .Where(item => item.TenantId == tenantId && !item.IsDeleted && item.ApprovalRequestId == requestId);
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
        Guid tenantId, Guid projectId, ProjectAnnualBudget budget, CancellationToken cancellationToken)
    {
        // Count Approved allocations and Pending (submitted) reservations so concurrent requests
        // cannot collectively exceed the annual budget/lifetime ceiling. Draft/terminal requests do
        // not reserve. Each request is included once by its current status.
        var rows = await (
            from detail in PaymentDetailsQuery(tenantId, projectId)
            join request in PaymentRequestsQuery(tenantId, projectId) on detail.ApprovalRequestId equals request.Id
            where detail.RequestKind == ProjectPaymentRequestKinds.PaymentRequest
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
        if (request.Status != PaymentRequestStatuses.Pending || detail.ApprovalMode is null
            || management.Current?.EffectiveReadOnly == true
            || !(await GetEligibleApproverIdsAsync(tenantId, projectId, cancellationToken)).Contains(actorId))
            return false;

        var assignments = await GetAssignmentsAsync(tenantId, request.Id, tracked: false, cancellationToken);
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
                    annualBudgetFiscalYear = new { from = detail.AnnualBudgetFiscalYear, to = detail.AnnualBudgetFiscalYear }
                }),
                OriginalValues = JsonSerializer.Serialize(new { status = statusBefore, settledAmount = settledBefore, outstandingAmount = outstandingBefore }),
                NewValues = JsonSerializer.Serialize(new { status = statusAfter, settledAmount = settledAfter, outstandingAmount = outstandingAfter, common }),
                ChangedFields = "settlementStatus,approvedAmount,totalSettled,outstandingAmount,currency,paymentRequestReference,settlementReference,annualBudgetId,annualBudgetReference,annualBudgetFiscalYear",
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
            milestone = Change(ReadString(beforeJson, "milestone"), ReadString(afterJson, "milestone"))
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
            ["fiscalYear"] = detail.AnnualBudgetFiscalYear
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
        _ => action
    };

    private static bool IsValidVersionlessAmount(decimal amount) =>
        amount > 0m && amount < 1_000_000_000_000_000m && decimal.Round(amount, 4) == amount;

    private static bool MatchesVersion(byte[]? current, byte[]? expected) =>
        current is { Length: > 0 } && expected is { Length: > 0 } && current.SequenceEqual(expected);

    private static string NormalizeCurrency(string? currency) => (currency ?? string.Empty).Trim().ToUpperInvariant();

    private static string Fingerprint(string title, decimal amount, string reason, Guid? milestoneId)
    {
        var canonical = JsonSerializer.Serialize(new { title, amount, reason, milestoneId });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private static string MakeReference(Guid id) => $"PPR-{id:N}".ToUpperInvariant();

    private static string Reference(ApprovalRequest request) => string.IsNullOrWhiteSpace(request.EntityNumber)
        ? MakeReference(request.Id)
        : request.EntityNumber;
}
