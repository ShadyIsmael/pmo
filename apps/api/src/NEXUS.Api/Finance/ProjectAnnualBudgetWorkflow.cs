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
    IAuditService audit)
{
    private const string ViewPermission = "ProjectAnnualBudget.View";
    private const string CreatePermission = "ProjectAnnualBudget.Create";
    private const string EditDraftPermission = "ProjectAnnualBudget.EditDraft";
    private const string SubmitPermission = "ProjectAnnualBudget.SubmitForReview";
    private const string ApprovePermission = "ProjectAnnualBudget.Approve";
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
        var actors = approvals.Where(request => request.RequestedBy.HasValue).Select(request => request.RequestedBy!.Value)
            .Concat(assignments.Select(item => item.ApproverUserId)).Concat(actions.Select(item => item.ApproverId)).Distinct().ToArray();
        var actorRows = actors.Length == 0 ? new List<User>() : await db.Users.IgnoreQueryFilters().AsNoTracking()
            .Where(user => user.TenantId == tenantId && actors.Contains(user.Id))
            .ToListAsync(cancellationToken);
        var names = actorRows.ToDictionary(user => user.Id, user => user.FullName);
        var eligibleApprovers = access.CanApprove
            ? await GetEligibleApproverIdsAsync(tenantId, projectId, cancellationToken)
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

        return Result<ProjectAnnualBudgetPageDto>.Success(new ProjectAnnualBudgetPageDto(
            currentPeriod.FiscalYear,
            await tenantFiscalYear.GetStartMonthAsync(tenantId, cancellationToken),
            items.Select(item => Map(item, access.CanEditDraft, access.CanSubmit, access.CanRevise, access.CanApprove, actorId, approvals, assignments, actions, eligibleApprovers, names)).ToArray(),
            project.Budget,
            NormalizeCurrency(project.Currency),
            approvedAllocations,
            pendingSubmittedAllocations,
            remainingCeiling));
    }

    public async Task<Result<ProjectAnnualBudgetItemDto>> GetAssignedReviewAsync(
        Guid tenantId, Guid projectId, Guid itemId, Guid requestId, Guid actorId, CancellationToken cancellationToken)
    {
        var access = await ValidateAccessAsync(tenantId, projectId, actorId, AccessKind.Approve, cancellationToken,
            itemId, requestId, allowAssignedReviewer: true);
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
        var eligibleApprovers = await GetEligibleApproverIdsAsync(tenantId, projectId, cancellationToken);
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
        var ceilingError = await ValidateCeilingCapacityAsync(
            tenantId, project, dto.NewApprovedFunding + dto.ApprovedAdjustments, null, null, cancellationToken);
        if (ceilingError is not null)
            return Result<ProjectAnnualBudgetItemDto>.Failure(ceilingError);

        var duplicate = await db.ProjectAnnualBudgets.IgnoreQueryFilters()
            .AnyAsync(item => item.TenantId == tenantId && item.ProjectId == projectId
                && item.FiscalYear == dto.FiscalYear, cancellationToken);
        if (duplicate)
            return Result<ProjectAnnualBudgetItemDto>.Failure(Error.Conflict("An annual budget already exists for this project and fiscal year."));

        FiscalYearPeriod period;
        try
        {
            var startMonth = await tenantFiscalYear.GetStartMonthAsync(tenantId, cancellationToken);
            period = fiscalCalendar.ForFiscalYear(dto.FiscalYear, startMonth);
        }
        catch (ArgumentOutOfRangeException)
        {
            return Result<ProjectAnnualBudgetItemDto>.Failure(Error.Validation("The requested fiscal year is outside the supported date range."));
        }

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

        return Result<ProjectAnnualBudgetItemDto>.Success(Map(item, access.CanWrite, access.CanApprove));
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
            return Result<ProjectAnnualBudgetItemDto>.Success(Map(item, access.CanWrite, access.CanApprove));

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

        return Result<ProjectAnnualBudgetItemDto>.Success(Map(item, access.CanWrite, access.CanApprove));
    }

    public async Task<Result<IReadOnlyList<ProjectAnnualBudgetEligibleApproverDto>>> GetEligibleApproversAsync(
        Guid tenantId, Guid projectId, Guid actorId, CancellationToken cancellationToken)
    {
        var access = await ValidateAccessAsync(tenantId, projectId, actorId, AccessKind.SubmitForReview, cancellationToken);
        if (access.Error is not null)
            return Result<IReadOnlyList<ProjectAnnualBudgetEligibleApproverDto>>.Failure(access.Error);
        var eligible = await GetEligibleApproverIdsAsync(tenantId, projectId, cancellationToken);
        var users = eligible.Count == 0 ? new List<User>() : await db.Users.IgnoreQueryFilters().AsNoTracking()
            .Where(user => user.TenantId == tenantId && eligible.Contains(user.Id) && !user.IsDeleted && user.IsActive)
            .ToListAsync(cancellationToken);
        var names = users.ToDictionary(user => user.Id, user => user.FullName);
        return Result<IReadOnlyList<ProjectAnnualBudgetEligibleApproverDto>>.Success(eligible
            .OrderBy(id => names.GetValueOrDefault(id), StringComparer.OrdinalIgnoreCase)
            .Select(id => new ProjectAnnualBudgetEligibleApproverDto(id, names.GetValueOrDefault(id) ?? id.ToString("D")))
            .ToArray());
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
        var eligibleIds = await GetEligibleApproverIdsAsync(tenantId, projectId, cancellationToken);
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
                .Select(user => new ProjectAnnualBudgetEligibleApproverDto(user.Id, user.FullName))
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
        var eligibleIds = await GetEligibleApproverIdsAsync(tenantId, projectId, cancellationToken);
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

        var eligibleIds = await GetEligibleApproverIdsAsync(tenantId, projectId, cancellationToken);
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
        var (planError, plan) = await ResolveEffectiveApprovalPlanAsync(
            tenantId, projectId, project, actorId, dto.ApprovalMode, dto.Approvers,
            dto.CustomizeForThisSubmission, dto.OverrideReason, dto.ApprovalConfigurationRowVersion,
            dto.ProjectSettingsRowVersion, cancellationToken);
        if (planError is not null)
            return Result<ProjectAnnualBudgetItemDto>.Failure(planError);
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
        foreach (var selection in ordered)
        {
            db.PaymentRequestApproverAssignments.Add(new PaymentRequestApproverAssignment
            {
                TenantId = tenantId,
                ApprovalRequestId = approval.Id,
                ApproverUserId = selection.UserId,
                Position = selection.Position,
                Requirement = selection.Requirement,
                State = PaymentRequestAssignmentStates.Pending,
                CreatedBy = actorId.ToString("D")
            });
        }
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
        try
        {
            await db.SaveChangesAsync(cancellationToken);
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
        foreach (var selection in ordered)
        {
            db.PaymentRequestApproverAssignments.Add(new PaymentRequestApproverAssignment
            {
                TenantId = tenantId,
                ApprovalRequestId = approval.Id,
                ApproverUserId = selection.UserId,
                Position = selection.Position,
                Requirement = selection.Requirement,
                State = PaymentRequestAssignmentStates.Pending,
                CreatedBy = actorId.ToString("D")
            });
        }
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
        await transaction.CommitAsync(cancellationToken);
        return await GetMappedItemAsync(tenantId, projectId, itemId, actorId, cancellationToken);
    }

    public async Task<Result<ProjectAnnualBudgetItemDto>> DecideReviewAsync(
        Guid tenantId, Guid projectId, Guid itemId, Guid requestId, Guid actorId, string? actorName,
        DecideProjectAnnualBudgetReviewDto dto, CancellationToken cancellationToken)
    {
        var access = await ValidateAccessAsync(tenantId, projectId, actorId, AccessKind.Approve, cancellationToken,
            itemId, requestId, allowAssignedReviewer: true);
        if (access.Error is not null)
            return Result<ProjectAnnualBudgetItemDto>.Failure(access.Error);
        var decision = dto.Decision is "Approved" or "Returned" ? dto.Decision : string.Empty;
        if (decision.Length == 0)
            return Result<ProjectAnnualBudgetItemDto>.Failure(Error.Validation("Decision must be Approved or Returned."));
        if (decision == "Returned" && string.IsNullOrWhiteSpace(dto.Reason))
            return Result<ProjectAnnualBudgetItemDto>.Failure(Error.Validation("A reason is required when returning an Annual Budget for changes."));

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
        var eligible = await GetEligibleApproverIdsAsync(tenantId, projectId, cancellationToken);
        if (!eligible.Contains(actorId))
            return Result<ProjectAnnualBudgetItemDto>.Failure(Error.Forbidden("The assigned reviewer must remain an active eligible Project Team member with the required finance permissions."));
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

    private Task<bool> HasClosedCarryForwardSourceAsync(
        Guid tenantId, Guid projectId, Guid annualBudgetId, CancellationToken cancellationToken) =>
        db.ProjectAnnualBudgetClosings.IgnoreQueryFilters().AnyAsync(closing =>
            closing.TenantId == tenantId && closing.ProjectId == projectId
            && closing.DestinationAnnualBudgetId == annualBudgetId
            && closing.Status == ProjectAnnualBudgetClosingStatuses.Closed && !closing.IsDeleted,
            cancellationToken);

    private async Task<HashSet<Guid>> GetEligibleApproverIdsAsync(
        Guid tenantId, Guid projectId, CancellationToken cancellationToken)
    {
        var candidates = await ProjectTeamApproverEligibility.GetActiveLinkedUserIdsAsync(db, tenantId, projectId, cancellationToken);
        var result = new HashSet<Guid>();
        foreach (var id in candidates)
        {
            var effective = await permissions.GetEffectivePermissionsAsync(id, tenantId, cancellationToken);
            if (effective.Contains(ApprovePermission)) result.Add(id);
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

    private static ProjectAnnualBudgetItemDto Map(
        ProjectAnnualBudget item, bool canEditDraft, bool canSubmit, bool canRevise, bool canApprove, Guid actorId,
        IReadOnlyCollection<ApprovalRequest> approvals,
        IReadOnlyCollection<PaymentRequestApproverAssignment> assignments,
        IReadOnlyCollection<ApprovalAction> actions,
        IReadOnlySet<Guid> eligibleApprovers,
        IReadOnlyDictionary<Guid, string> names)
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
        var canReview = isPending && canApprove && latest!.RequestedBy != actorId && eligibleApprovers.Contains(actorId)
            && latestAssignments.Any(row => row.ApproverUserId == actorId && row.State == PaymentRequestAssignmentStates.Pending
                && latestActions.All(action => action.ApproverAssignmentId != row.Id));
        if (canReview && latestAssignments.Any(row => row.Position.HasValue))
            canReview = PaymentRequestWorkflowPolicy.CanDecideSequentially(actorId, latest!.Status,
                BuildApproverStates(latestAssignments, latestActions));
        var reviewStatus = item.Status == ProjectAnnualBudgetStatuses.Approved
            ? latestRevision?.Status == PaymentRequestStatuses.Pending ? "RevisionPending"
                : latestRevision?.Status == "Returned" ? "RevisionReturned" : "Approved"
            : latestInitial?.Status == PaymentRequestStatuses.Pending ? "Pending"
                : latestInitial?.Status == "Returned" ? "Returned" : "Draft";
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
        return new ProjectAnnualBudgetItemDto(
            item.Id, item.ProjectId, item.FiscalYear, $"FY {item.FiscalYear}", item.FiscalYearStartDate,
            item.FiscalYearEndDate, item.FinancialReference, item.Currency, item.NewApprovedFunding,
            item.ApprovedCarryForward, item.ApprovedReservedCarryForward, item.ApprovedFreeCarryForward,
            item.ApprovedAdjustments, item.TotalAnnualBudget, item.Status, item.ApprovedBy, item.ApprovedAt,
            item.CreatedAt, item.UpdatedAt, item.RowVersion, item.ClosedByClosingId, item.ClosedAtUtc,
            item.ClosedBy, item.ClosedAtUtc.HasValue, canEdit, canReview, reviewStatus, approvalMode,
            latest?.Id, latest?.RowVersion, latest?.RequestedBy, latest?.RequestedAt, canSubmitForReview, canReview, canSubmitRevision, history);
    }

    private async Task<BudgetAccess> ValidateAccessAsync(
        Guid tenantId, Guid projectId, Guid actorId, AccessKind kind, CancellationToken cancellationToken,
        Guid? reviewItemId = null, Guid? reviewRequestId = null, bool allowAssignedReviewer = false)
    {
        var current = management.Current;
        var scopeAllowsProject = current?.Scope.ProjectIds.Contains(projectId) == true;
        var assignedReviewerAllowsProject = allowAssignedReviewer && reviewItemId.HasValue && reviewRequestId.HasValue
            && current is not null && current.TenantId == tenantId && current.UserId == actorId
            && await IsAssignedReviewerAsync(tenantId, projectId, reviewItemId.Value, reviewRequestId.Value, actorId, cancellationToken);
        if (tenantId == Guid.Empty || actorId == Guid.Empty || current is null
            || current.TenantId != tenantId || current.UserId != actorId
            || (!scopeAllowsProject && !assignedReviewerAllowsProject))
            return BudgetAccess.Denied(Error.NotFound("Project not found."));

        var effective = await permissions.GetEffectivePermissionsAsync(actorId, tenantId, cancellationToken);
        var canWrite = !current.EffectiveReadOnly || assignedReviewerAllowsProject;
        var canCreate = canWrite && effective.Contains(CreatePermission);
        var canEditDraft = canWrite && effective.Contains(EditDraftPermission);
        var canSubmit = canWrite && effective.Contains(SubmitPermission);
        var canRevise = canWrite && effective.Contains(ReviseApprovedPermission);
        var canViewProjectSettings = effective.Contains(ProjectSettingsViewPermission);
        var canEditProjectSettings = canWrite && canViewProjectSettings && effective.Contains(ProjectSettingsEditPermission);
        var canManageApprovalConfiguration = canEditProjectSettings && effective.Contains(ApprovalConfigurationPermission);
        var canApprove = canWrite && effective.Contains(ApprovePermission);
        var canRead = effective.Contains(ViewPermission);

        var error = kind switch
        {
            AccessKind.Read when !canRead => Error.Forbidden("ProjectAnnualBudget.View is required."),
            AccessKind.Create when !canCreate => Error.Forbidden("ProjectAnnualBudget.Create is required."),
            AccessKind.EditDraft when !canEditDraft => Error.Forbidden("ProjectAnnualBudget.EditDraft is required."),
            AccessKind.SubmitForReview when !canSubmit => Error.Forbidden("ProjectAnnualBudget.SubmitForReview is required."),
            AccessKind.ReviseApproved when !canRevise => Error.Forbidden("ProjectAnnualBudget.ReviseApproved is required."),
            AccessKind.ViewApprovalConfiguration when !canRead || !canViewProjectSettings => Error.Forbidden("ProjectAnnualBudget.View and ProjectSettings.View are required to view approval configuration."),
            AccessKind.ManageApprovalConfiguration when !canManageApprovalConfiguration => Error.Forbidden("ProjectSettings.View, ProjectSettings.Edit, and ProjectAnnualBudget.ManageApprovalConfiguration are required."),
            AccessKind.Approve when !canApprove => Error.Forbidden("ProjectAnnualBudget.Approve is required."),
            _ => null
        };
        return new BudgetAccess(error, canCreate || canEditDraft || canSubmit || canRevise || canManageApprovalConfiguration,
            canApprove, canCreate, canEditDraft, canSubmit, canRevise, canManageApprovalConfiguration);
    }

    private async Task<bool> IsAssignedReviewerAsync(
        Guid tenantId, Guid projectId, Guid itemId, Guid requestId, Guid actorId, CancellationToken cancellationToken)
    {
        var projectExists = await FindProjectQuery(tenantId, projectId).AnyAsync(cancellationToken);
        if (!projectExists) return false;
        var request = await db.ApprovalRequests.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == requestId && item.TenantId == tenantId && !item.IsDeleted
                && item.EntityType == EntityType && item.EntityId == itemId
                && (item.Type == ReviewApprovalType || item.Type == RevisionApprovalType)
                && item.Status == PaymentRequestStatuses.Pending && item.RequestedBy != actorId, cancellationToken);
        if (request is null) return false;
        var assignments = await db.PaymentRequestApproverAssignments.IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.TenantId == tenantId && !item.IsDeleted && item.ApprovalRequestId == requestId)
            .OrderBy(item => item.Position).ToListAsync(cancellationToken);
        var assigned = assignments.FirstOrDefault(item => item.ApproverUserId == actorId
            && item.State == PaymentRequestAssignmentStates.Pending);
        if (assigned is null) return false;
        var actions = await db.ApprovalActions.IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.TenantId == tenantId && !item.IsDeleted && item.ApprovalRequestId == requestId)
            .ToListAsync(cancellationToken);
        if (actions.Any(action => action.ApproverAssignmentId == assigned.Id)) return false;
        var eligible = await GetEligibleApproverIdsAsync(tenantId, projectId, cancellationToken);
        if (!eligible.Contains(actorId)) return false;
        return !assignments.Any(item => item.Position.HasValue)
            || PaymentRequestWorkflowPolicy.CanDecideSequentially(actorId, request.Status,
                BuildApproverStates(assignments, actions));
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
        CancellationToken cancellationToken)
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
            Timestamp = DateTime.UtcNow,
            Description = action switch
            {
                "Created" => $"Annual budget draft {item.FinancialReference} created for {project.Name}.",
                "Updated" => $"Annual budget draft {item.FinancialReference} updated for {project.Name}.",
                "SubmittedForReview" => $"Annual budget {item.FinancialReference} submitted for review for {project.Name}.",
                "RevisionSubmitted" => $"Annual budget revision submitted for {item.FinancialReference} for {project.Name}.",
                "ReturnedForChanges" => $"Annual budget {item.FinancialReference} returned for changes for {project.Name}.",
                "Revised" => $"Approved annual budget {item.FinancialReference} revised for {project.Name}.",
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
            approvedAtUtc = Change(before?.ApprovedAt, current.ApprovedAt)
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
        item.ApprovedAt);

    private static object Change<T>(T? from, T? to) => new { from, to };

    private static ProjectAnnualBudgetItemDto Map(ProjectAnnualBudget item, bool canWrite, bool canApprove) => new(
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
        canWrite && item.Status == ProjectAnnualBudgetStatuses.Draft,
        false,
        item.Status == ProjectAnnualBudgetStatuses.Approved ? "Approved" : "Draft",
        null,
        null,
        null,
        item.ApprovedBy,
        item.ApprovedAt,
        canWrite && item.Status == ProjectAnnualBudgetStatuses.Draft,
        false,
        canWrite && item.Status == ProjectAnnualBudgetStatuses.Approved && !item.ClosedAtUtc.HasValue,
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

    private enum AccessKind { Read, ViewApprovalConfiguration, Create, EditDraft, SubmitForReview, ReviseApproved, ManageApprovalConfiguration, Approve }
    private sealed record BudgetAccess(
        Error? Error,
        bool CanWrite,
        bool CanApprove,
        bool CanCreate,
        bool CanEditDraft,
        bool CanSubmit,
        bool CanRevise,
        bool CanManageApprovalConfiguration)
    {
        public static BudgetAccess Denied(Error error) => new(error, false, false, false, false, false, false, false);
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
        DateTime? ApprovedAt);
}
