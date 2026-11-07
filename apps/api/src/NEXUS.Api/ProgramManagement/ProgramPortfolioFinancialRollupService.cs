using Microsoft.EntityFrameworkCore;
using NEXUS.Api.Finance;
using NEXUS.Modules.Approvals.Domain;
using NEXUS.Modules.CostManagement.Application.DTOs;
using NEXUS.Modules.CostManagement.Domain;
using NEXUS.Modules.Identity.Application;
using NEXUS.Modules.Organization.Application.Settings;
using NEXUS.Modules.Portfolio.Domain;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.Persistence;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Management;
using ProgramEntity = NEXUS.Modules.Portfolio.Domain.Program;

namespace NEXUS.Api.ProgramManagement;

public static class ProgramPortfolioFinancialRollupAuthorization
{
    public const string ProgramRollupViewPermission = "ProgramFinancialRollup.View";
    public const string PortfolioRollupViewPermission = "PortfolioFinancialRollup.View";

    public static bool HasProgramRollupView(IReadOnlySet<string> effectivePermissions) =>
        effectivePermissions.Contains(ProgramRollupViewPermission);

    public static bool HasPortfolioRollupView(IReadOnlySet<string> effectivePermissions) =>
        effectivePermissions.Contains(PortfolioRollupViewPermission);
}

public interface IProgramPortfolioFinancialRollupService
{
    Task<Result<ProgramPortfolioFinancialRollupDto>> GetProgramAsync(Guid programId, int? fiscalYear, int page, int pageSize, CancellationToken cancellationToken);
    Task<Result<ProgramPortfolioFinancialRollupDto>> GetPortfolioAsync(Guid portfolioId, int? fiscalYear, int page, int pageSize, CancellationToken cancellationToken);
}

/// <summary>
/// Read-only rollups over project financial source records. Parent legacy budget columns are never read.
/// Queries are batched per 800 projects to stay below SQL Server's parameter limit.
/// </summary>
public sealed class ProgramPortfolioFinancialRollupService(
    AppDbContext db,
    IManagementContextAccessor management,
    IUserEffectivePermissionService permissions,
    ITenantFiscalYearService tenantFiscalYear,
    IFiscalYearCalendarService fiscalYearCalendar) : IProgramPortfolioFinancialRollupService
{
    private const string ProgramView = "Program.View";
    private const string PortfolioView = "Portfolio.View";
    private const string PaymentRequestType = "ProjectPaymentRequest";
    private const string ReversalRequestType = "ProjectPaymentRequestReversal";

    public async Task<Result<ProgramPortfolioFinancialRollupDto>> GetProgramAsync(
        Guid programId, int? fiscalYear, int page, int pageSize, CancellationToken cancellationToken)
    {
        var context = management.Current;
        var access = await ValidateReadAsync(context, ProgramView, ProgramPortfolioFinancialRollupAuthorization.ProgramRollupViewPermission, cancellationToken);
        if (access is not null) return Result<ProgramPortfolioFinancialRollupDto>.Failure(access);
        if (context is null || (!context.CanReadAllTenantPortfolioProgramData && !context.Scope.ProgramIds.Contains(programId)))
            return Result<ProgramPortfolioFinancialRollupDto>.Failure(Error.NotFound("Program not found."));

        var program = await db.Set<ProgramEntity>().IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == programId && item.TenantId == context.TenantId && !item.IsDeleted, cancellationToken);
        if (program is null) return Result<ProgramPortfolioFinancialRollupDto>.Failure(Error.NotFound("Program not found."));

        var allProjects = await db.Set<Project>().IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.TenantId == context.TenantId && item.ProgramId == programId && !item.IsDeleted)
            .Select(item => new ProjectRow(item.Id, item.PublicId, item.Name, item.ProgramId, item.PortfolioId, item.Currency, item.Status))
            .ToListAsync(cancellationToken);
        allProjects = allProjects.Select(item => item with { ProgramName = program.Name }).ToList();
        var visibleProjects = context.CanReadAllTenantPortfolioProgramData
            ? allProjects
            : allProjects.Where(item => context.Scope.ProjectIds.Contains(item.Id)).ToList();
        return await BuildAsync("Program", programId, context.TenantId, visibleProjects,
            allProjects.Count != visibleProjects.Count, fiscalYear, page, pageSize, null, cancellationToken);
    }

    public async Task<Result<ProgramPortfolioFinancialRollupDto>> GetPortfolioAsync(
        Guid portfolioId, int? fiscalYear, int page, int pageSize, CancellationToken cancellationToken)
    {
        var context = management.Current;
        var access = await ValidateReadAsync(context, PortfolioView, ProgramPortfolioFinancialRollupAuthorization.PortfolioRollupViewPermission, cancellationToken);
        if (access is not null) return Result<ProgramPortfolioFinancialRollupDto>.Failure(access);
        if (context is null || (!context.CanReadAllTenantPortfolioProgramData && !context.Scope.PortfolioIds.Contains(portfolioId)))
            return Result<ProgramPortfolioFinancialRollupDto>.Failure(Error.NotFound("Portfolio not found."));

        var portfolio = await db.Set<Portfolio>().IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == portfolioId && item.TenantId == context.TenantId && !item.IsDeleted, cancellationToken);
        if (portfolio is null) return Result<ProgramPortfolioFinancialRollupDto>.Failure(Error.NotFound("Portfolio not found."));

        var programs = await db.Set<ProgramEntity>().IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.TenantId == context.TenantId && item.PortfolioId == portfolioId && !item.IsDeleted)
            .OrderBy(item => item.Name).ThenBy(item => item.Id)
            .Select(item => new { item.Id, item.Name })
            .ToListAsync(cancellationToken);
        var programNames = programs.ToDictionary(item => item.Id, item => item.Name);
        var allProjects = await db.Set<Project>().IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.TenantId == context.TenantId && !item.IsDeleted
                && ((item.PortfolioId == portfolioId && item.ProgramId == null)
                    || (item.ProgramId.HasValue && db.Set<ProgramEntity>().IgnoreQueryFilters().Any(program =>
                        program.Id == item.ProgramId.Value && program.TenantId == context.TenantId
                        && program.PortfolioId == portfolioId && !program.IsDeleted))))
            .Select(item => new ProjectRow(item.Id, item.PublicId, item.Name, item.ProgramId, item.PortfolioId, item.Currency, item.Status))
            .ToListAsync(cancellationToken);
        // Current hierarchy is authoritative for this view. Distinct IDs prevent direct/Program links being summed twice.
        allProjects = allProjects.GroupBy(item => item.Id).Select(group => group.First())
            .Select(item => item with { ProgramName = item.ProgramId.HasValue ? programNames.GetValueOrDefault(item.ProgramId.Value) : null }).ToList();
        var visibleProjects = context.CanReadAllTenantPortfolioProgramData
            ? allProjects
            : allProjects.Where(item => context.Scope.ProjectIds.Contains(item.Id)).ToList();
        return await BuildAsync("Portfolio", portfolioId, context.TenantId, visibleProjects,
            allProjects.Count != visibleProjects.Count, fiscalYear, page, pageSize,
            programs.Select(item => new PortfolioProgramRow(item.Id, item.Name)).ToArray(), cancellationToken);
    }

    private async Task<Error?> ValidateReadAsync(
        ManagementContext? context,
        string parentPermission,
        string rollupPermission,
        CancellationToken cancellationToken)
    {
        if (context is null || context.TenantId == Guid.Empty || context.UserId == Guid.Empty) return Error.Forbidden("A valid tenant management context is required.");
        var effective = await permissions.GetEffectivePermissionsAsync(context.UserId, context.TenantId, cancellationToken);
        if (!effective.Contains(parentPermission)) return Error.Forbidden($"{parentPermission} is required.");
        var hasRollupView = rollupPermission == ProgramPortfolioFinancialRollupAuthorization.ProgramRollupViewPermission
            ? ProgramPortfolioFinancialRollupAuthorization.HasProgramRollupView(effective)
            : ProgramPortfolioFinancialRollupAuthorization.HasPortfolioRollupView(effective);
        if (!hasRollupView) return Error.Forbidden($"{rollupPermission} is required.");
        return null;
    }

    private async Task<Result<ProgramPortfolioFinancialRollupDto>> BuildAsync(
        string entityType, Guid entityId, Guid tenantId, IReadOnlyList<ProjectRow> projects,
        bool partialCoverage, int? requestedFiscalYear, int page, int pageSize,
        IReadOnlyList<PortfolioProgramRow>? portfolioPrograms, CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var preview = await tenantFiscalYear.GetPreviewAsync(tenantId, asOf: DateTimeOffset.UtcNow, cancellationToken: cancellationToken);
        var fiscalYear = requestedFiscalYear ?? preview.FiscalYear;
        if (fiscalYear is < 1900 or > 2200)
            return Result<ProgramPortfolioFinancialRollupDto>.Failure(Error.Validation("Fiscal year must be between 1900 and 2200."));

        FiscalYearPeriod period;
        try
        {
            period = fiscalYearCalendar.ForFiscalYear(fiscalYear, preview.StartMonth);
        }
        catch (ArgumentOutOfRangeException)
        {
            return Result<ProgramPortfolioFinancialRollupDto>.Failure(Error.Validation("The requested fiscal year is outside the supported calendar range."));
        }

        var rows = await ReadFinancialRowsAsync(tenantId, projects, fiscalYear, cancellationToken);
        var projectResults = BuildContributions(tenantId, projects, rows, fiscalYear, period, preview.StartMonth);
        var validByCurrency = projectResults.Where(item => item.IsAvailable && item.Currency is not null)
            .GroupBy(item => item.Currency!, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key)
            .Select(group => AggregateCurrency(group.Key, group.ToArray()))
            .ToArray();
        var projectResultsById = projectResults.ToDictionary(item => item.ProjectId);
        var programBreakdowns = entityType == "Portfolio"
            ? (portfolioPrograms ?? Array.Empty<PortfolioProgramRow>()).Select(program =>
            {
                var groupProjects = projects.Where(item => item.ProgramId == program.Id).ToArray();
                return BuildHierarchyGroup(program.Id, program.Name, false, groupProjects, projectResultsById);
            }).Append(BuildHierarchyGroup(null, string.Empty, true,
                projects.Where(item => item.ProgramId is null).ToArray(), projectResultsById)).ToArray()
            : [];
        var closingRows = rows.Closings.Where(item => item.FiscalYear == fiscalYear).GroupBy(item => item.ProjectId).ToDictionary(group => group.Key,
            group => group.OrderByDescending(item => item.Status == ProjectAnnualBudgetClosingStatuses.Closed)
                .ThenByDescending(item => item.CreatedAt).ThenByDescending(item => item.UpdatedAt).First());
        var statuses = projects.Select(project => closingRows.TryGetValue(project.Id, out var closing) ? closing.Status : "Open").ToArray();
        var closedCount = statuses.Count(status => status == ProjectAnnualBudgetClosingStatuses.Closed);
        var reviewCount = statuses.Count(status => status == ProjectAnnualBudgetClosingStatuses.Review);
        var pendingCount = statuses.Count(status => status == ProjectAnnualBudgetClosingStatuses.PendingApproval);
        var openCount = projects.Count - closedCount - reviewCount - pendingCount;
        var gapCount = projectResults.Count(item => item.DataStatus is "FinancialDataRequiresReconciliation" or "DuplicateAnnualBudget" or "InvalidProjectCurrency");
        var missingBudgetCount = projectResults.Count(item => item.DataStatus is "MissingApprovedAnnualBudget" or "AnnualBudgetNotApproved");
        var legacyCount = rows.PaymentDetails.Count(detail => detail.RequestKind == ProjectPaymentRequestKinds.PaymentRequest
            && detail.ProjectAnnualBudgetId is null
            && rows.Approvals.TryGetValue(detail.ApprovalRequestId, out var request)
            && request.Type == PaymentRequestType && request.Status == PaymentRequestStatuses.Approved);
        var allClosed = projects.Count > 0 && closedCount == projects.Count;
        var reconciliationStatus = allClosed && !partialCoverage && gapCount == 0 && missingBudgetCount == 0 && legacyCount == 0 ? "Reconciled" : "Provisional";
        var coverage = projects.Count == 0 ? "NoProjects" : partialCoverage ? "Partial" : "Complete";
        var currencyCount = validByCurrency.Length;
        var closingCoverage = new FinancialRollupClosingCoverageDto(projects.Count, openCount, reviewCount, pendingCount,
            closedCount, gapCount + legacyCount + missingBudgetCount, reconciliationStatus);
        var skipCount = (int)Math.Min((long)(page - 1) * pageSize, int.MaxValue);
        var projectPage = projectResults.OrderBy(item => item.ProjectName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.ProjectId).Skip(skipCount).Take(pageSize).ToArray();

        return Result<ProgramPortfolioFinancialRollupDto>.Success(new ProgramPortfolioFinancialRollupDto(
            entityType, entityId, fiscalYear, period.Label, period.StartDate, period.EndDate, preview.TimeZoneId,
            "CurrentHierarchy", coverage, projects.Count, missingBudgetCount,
            gapCount, legacyCount, currencyCount > 1, reconciliationStatus, closingCoverage, validByCurrency, programBreakdowns, projectPage,
            page, pageSize, projectResults.Count));
    }

    private async Task<FinancialRows> ReadFinancialRowsAsync(
        Guid tenantId, IReadOnlyList<ProjectRow> projects, int fiscalYear, CancellationToken cancellationToken)
    {
        var ids = projects.Select(item => item.Id).Distinct().ToArray();
        var budgets = new List<ProjectAnnualBudget>();
        var paymentDetails = new List<ProjectPaymentRequestDetail>();
        var transfers = new List<ProjectPaymentRequestFundingTransfer>();
        var settlements = new List<PaymentSettlement>();
        var closings = new List<ProjectAnnualBudgetClosing>();
        foreach (var batch in ids.Chunk(800))
        {
            budgets.AddRange(await db.Set<ProjectAnnualBudget>().IgnoreQueryFilters().AsNoTracking()
                .Where(item => item.TenantId == tenantId && batch.Contains(item.ProjectId) && !item.IsDeleted)
                .ToListAsync(cancellationToken));
            paymentDetails.AddRange(await db.Set<ProjectPaymentRequestDetail>().IgnoreQueryFilters().AsNoTracking()
                .Where(item => item.TenantId == tenantId && batch.Contains(item.ProjectId))
                .ToListAsync(cancellationToken));
            transfers.AddRange(await db.Set<ProjectPaymentRequestFundingTransfer>().IgnoreQueryFilters().AsNoTracking()
                .Where(item => item.TenantId == tenantId && batch.Contains(item.ProjectId) && !item.IsDeleted)
                .ToListAsync(cancellationToken));
            settlements.AddRange(await db.Set<PaymentSettlement>().IgnoreQueryFilters().AsNoTracking()
                .Where(item => item.TenantId == tenantId && batch.Contains(item.ProjectId)
                    && item.ProjectPaymentRequestId.HasValue && !item.IsDeleted)
                .ToListAsync(cancellationToken));
            closings.AddRange(await db.Set<ProjectAnnualBudgetClosing>().IgnoreQueryFilters().AsNoTracking()
                .Where(item => item.TenantId == tenantId && batch.Contains(item.ProjectId) && !item.IsDeleted)
                .ToListAsync(cancellationToken));
        }
        var detailIds = paymentDetails.Select(item => item.ApprovalRequestId).Distinct().ToArray();
        var approvals = new List<ApprovalRequest>();
        foreach (var batch in detailIds.Chunk(800))
            approvals.AddRange(await db.Set<ApprovalRequest>().IgnoreQueryFilters().AsNoTracking()
                .Where(item => item.TenantId == tenantId && batch.Contains(item.Id) && !item.IsDeleted)
                .ToListAsync(cancellationToken));
        return new FinancialRows(budgets, paymentDetails, transfers, settlements, closings,
            approvals.ToDictionary(item => item.Id));
    }

    private List<FinancialRollupProjectDto> BuildContributions(
        Guid tenantId, IReadOnlyList<ProjectRow> projects, FinancialRows rows, int fiscalYear, FiscalYearPeriod period, int fiscalYearStartMonth)
    {
        var result = new List<FinancialRollupProjectDto>(projects.Count);
        var budgetsByProject = rows.Budgets.GroupBy(item => item.ProjectId).ToDictionary(group => group.Key, group => group.ToArray());
        var detailsByProject = rows.PaymentDetails.GroupBy(item => item.ProjectId).ToDictionary(group => group.Key, group => group.ToArray());
        var transfersByProject = rows.Transfers.GroupBy(item => item.ProjectId).ToDictionary(group => group.Key, group => group.ToArray());
        var settlementsByProject = rows.Settlements.GroupBy(item => item.ProjectId).ToDictionary(group => group.Key, group => group.ToArray());
        var closingsByProject = rows.Closings.GroupBy(item => item.ProjectId).ToDictionary(group => group.Key, group => group.ToArray());
        foreach (var project in projects)
        {
            var currency = NormalizeCurrency(project.Currency);
            var projectBudgets = budgetsByProject.GetValueOrDefault(project.Id, Array.Empty<ProjectAnnualBudget>());
            var yearBudgets = projectBudgets.Where(item => item.FiscalYear == fiscalYear).ToArray();
            var budget = yearBudgets.Length == 1 ? yearBudgets[0] : null;
            var projectDetails = detailsByProject.GetValueOrDefault(project.Id, Array.Empty<ProjectPaymentRequestDetail>());
            var details = projectDetails.Where(item => item.RequestKind == ProjectPaymentRequestKinds.PaymentRequest
                && rows.Approvals.TryGetValue(item.ApprovalRequestId, out var request)
                && request.Type == PaymentRequestType && request.EntityType == "Project" && request.EntityId == project.Id).ToArray();
            var originDetails = details.Where(item => item.AnnualBudgetFiscalYear == fiscalYear
                || (budget is not null && item.ProjectAnnualBudgetId == budget.Id)).ToArray();
            var approvedDetails = details.Where(item => rows.Approvals[item.ApprovalRequestId].Status == PaymentRequestStatuses.Approved).ToArray();
            var projectTransfers = transfersByProject.GetValueOrDefault(project.Id, Array.Empty<ProjectPaymentRequestFundingTransfer>());
            var projectSettlements = settlementsByProject.GetValueOrDefault(project.Id, Array.Empty<PaymentSettlement>())
                .Where(item => item.ProjectPaymentRequestId.HasValue).ToArray();
            var projectClosings = closingsByProject.GetValueOrDefault(project.Id, Array.Empty<ProjectAnnualBudgetClosing>())
                .Where(item => item.FiscalYear == fiscalYear)
                .OrderByDescending(item => item.CreatedAt).ThenByDescending(item => item.UpdatedAt).ToArray();
            var closing = projectClosings.FirstOrDefault(item => item.Status == ProjectAnnualBudgetClosingStatuses.Closed)
                ?? projectClosings.FirstOrDefault();

            var gap = false;
            var financialDetails = projectDetails.Where(item => item.RequestKind == ProjectPaymentRequestKinds.PaymentRequest).ToArray();
            if (financialDetails.Any(item => !rows.Approvals.TryGetValue(item.ApprovalRequestId, out var approval)
                || approval.Type != PaymentRequestType || approval.EntityType != "Project" || approval.EntityId != project.Id)) gap = true;
            if (!IsValidCurrency(currency) || string.IsNullOrWhiteSpace(currency) || yearBudgets.Length > 1) gap = true;
            if (budget is not null && (string.IsNullOrWhiteSpace(budget.Currency)
                || !IsValidCurrency(NormalizeCurrency(budget.Currency))
                || !string.Equals(NormalizeCurrency(budget.Currency), currency, StringComparison.Ordinal)
                || budget.FiscalYearStartDate != period.StartDate || budget.FiscalYearEndDate != period.EndDate)) gap = true;

            foreach (var requestDetail in approvedDetails)
            {
                var request = rows.Approvals[requestDetail.ApprovalRequestId];
                if (request.Amount is not > 0m || string.IsNullOrWhiteSpace(request.Currency)
                    || !IsValidCurrency(NormalizeCurrency(request.Currency))
                    || !string.Equals(NormalizeCurrency(request.Currency), currency, StringComparison.Ordinal)) gap = true;
                if (requestDetail.ProjectAnnualBudgetId is null)
                {
                    gap = true;
                    continue;
                }
                var originalBudget = projectBudgets.FirstOrDefault(item => item.Id == requestDetail.ProjectAnnualBudgetId);
                if (originalBudget is null || (requestDetail.AnnualBudgetFiscalYear.HasValue
                    && originalBudget.FiscalYear != requestDetail.AnnualBudgetFiscalYear.Value)) gap = true;
            }

            var requests = details.Select(item => rows.Approvals[item.ApprovalRequestId]).ToDictionary(item => item.Id);
            var detailsByRequestId = details.ToDictionary(item => item.ApprovalRequestId);
            var requestIds = requests.Keys.ToHashSet();
            var applicableTransfers = projectTransfers.Where(item => requestIds.Contains(item.PaymentRequestId)).ToArray();
            var budgetById = projectBudgets.ToDictionary(item => item.Id);
            foreach (var transferGroup in applicableTransfers.GroupBy(item => item.PaymentRequestId))
            {
                var root = details.FirstOrDefault(item => item.ApprovalRequestId == transferGroup.Key);
                var chain = transferGroup.OrderBy(item => item.SourceFiscalYear).ThenBy(item => item.DestinationFiscalYear).ToArray();
                Guid? priorTransferId = null;
                Guid? priorBudgetId = root?.ProjectAnnualBudgetId;
                int? priorYear = root?.AnnualBudgetFiscalYear;
                decimal? priorAmount = null;
                foreach (var transfer in chain)
                {
                    var sourceValid = budgetById.TryGetValue(transfer.SourceAnnualBudgetId, out var source);
                    var destinationValid = budgetById.TryGetValue(transfer.DestinationAnnualBudgetId, out var destination);
                    var closeValid = rows.Closings.Any(item => item.Id == transfer.ClosingId && item.TenantId == tenantId
                        && item.ProjectId == project.Id && item.Status == ProjectAnnualBudgetClosingStatuses.Closed
                        && item.SourceAnnualBudgetId == transfer.SourceAnnualBudgetId
                        && item.DestinationAnnualBudgetId == transfer.DestinationAnnualBudgetId);
                    if (!sourceValid || !destinationValid || !closeValid || transfer.Amount <= 0m
                        || string.IsNullOrWhiteSpace(transfer.Currency)
                        || !IsValidCurrency(NormalizeCurrency(transfer.Currency)) || !string.Equals(NormalizeCurrency(transfer.Currency), currency, StringComparison.Ordinal)
                        || source!.FiscalYear != transfer.SourceFiscalYear || destination!.FiscalYear != transfer.DestinationFiscalYear
                        || transfer.DestinationFiscalYear != transfer.SourceFiscalYear + 1
                        || transfer.SupersedesFundingTransferId != priorTransferId
                        || transfer.SourceAnnualBudgetId != priorBudgetId || transfer.SourceFiscalYear != priorYear
                        || (priorAmount.HasValue && transfer.Amount > priorAmount.Value)) gap = true;
                    priorTransferId = transfer.Id;
                    priorBudgetId = transfer.DestinationAnnualBudgetId;
                    priorYear = transfer.DestinationFiscalYear;
                    priorAmount = transfer.Amount;
                }
            }

            var projectReversalDetails = projectDetails.Where(item => item.RequestKind == ProjectPaymentRequestKinds.ReversalRequest).ToArray();
            foreach (var reversalDetail in projectReversalDetails)
            {
                if (!rows.Approvals.TryGetValue(reversalDetail.ApprovalRequestId, out var reversalRequest)
                    || reversalRequest.Type != ReversalRequestType || reversalRequest.EntityType != "Project"
                    || reversalRequest.EntityId != project.Id
                    || (reversalRequest.Status == PaymentRequestStatuses.Approved
                        && (!reversalDetail.ReversesApprovalRequestId.HasValue || !requestIds.Contains(reversalDetail.ReversesApprovalRequestId.Value))))
                    gap = true;
            }
            var releases = projectReversalDetails.Where(item => item.ReversesApprovalRequestId.HasValue
                    && requestIds.Contains(item.ReversesApprovalRequestId.Value)
                    && rows.Approvals.TryGetValue(item.ApprovalRequestId, out var reversal)
                    && reversal.Type == ReversalRequestType && reversal.EntityType == "Project" && reversal.EntityId == project.Id
                    && reversal.Status == PaymentRequestStatuses.Approved)
                .Select(item => new ReversalRow(item, rows.Approvals[item.ApprovalRequestId])).ToArray();
            foreach (var reversal in releases)
            {
                var parent = details.First(item => item.ApprovalRequestId == reversal.Detail.ReversesApprovalRequestId);
                var request = rows.Approvals[parent.ApprovalRequestId];
                if (reversal.Approval.Amount is not > 0m || !string.Equals(NormalizeCurrency(reversal.Approval.Currency), NormalizeCurrency(request.Currency), StringComparison.Ordinal)) gap = true;
                if (!reversal.Detail.FundingTransferId.HasValue && (reversal.Detail.ProjectAnnualBudgetId != parent.ProjectAnnualBudgetId
                    || reversal.Detail.AnnualBudgetFiscalYear != parent.AnnualBudgetFiscalYear)) gap = true;
                if (reversal.Detail.FundingTransferId.HasValue && !applicableTransfers.Any(item => item.Id == reversal.Detail.FundingTransferId
                    && item.PaymentRequestId == parent.ApprovalRequestId)) gap = true;
            }

            if (projectSettlements.Any(item => !requests.TryGetValue(item.ProjectPaymentRequestId!.Value, out var parent)
                || parent.Status != PaymentRequestStatuses.Approved)) gap = true;
            var validSettlements = projectSettlements.Where(item => requests.TryGetValue(item.ProjectPaymentRequestId!.Value, out var parent)
                && parent.Status == PaymentRequestStatuses.Approved).ToArray();
            foreach (var settlement in validSettlements)
            {
                var request = requests[settlement.ProjectPaymentRequestId!.Value];
                if (settlement.Amount <= 0m || string.IsNullOrWhiteSpace(settlement.Currency)
                    || !IsValidCurrency(NormalizeCurrency(settlement.Currency))
                    || !string.Equals(NormalizeCurrency(settlement.Currency), NormalizeCurrency(request.Currency), StringComparison.Ordinal)) gap = true;
                if (settlement.FundingTransferId.HasValue && !applicableTransfers.Any(item =>
                    item.Id == settlement.FundingTransferId && item.PaymentRequestId == request.Id)) gap = true;
                var settlementFiscalYear = fiscalYearCalendar.Resolve(DateOnly.FromDateTime(settlement.SettlementDate), fiscalYearStartMonth).FiscalYear;
                var settlementYearTransfers = applicableTransfers.Where(item => item.PaymentRequestId == request.Id
                    && item.DestinationFiscalYear == settlementFiscalYear).ToArray();
                var originDetail = detailsByRequestId[request.Id];
                int? originalFundingFiscalYear = originDetail.AnnualBudgetFiscalYear;
                if (!originalFundingFiscalYear.HasValue && originDetail.ProjectAnnualBudgetId.HasValue
                    && budgetById.TryGetValue(originDetail.ProjectAnnualBudgetId.Value, out var originBudget))
                    originalFundingFiscalYear = originBudget.FiscalYear;
                if (settlementYearTransfers.Length > 1
                    || settlement.FundingTransferId != settlementYearTransfers.FirstOrDefault()?.Id
                    || (settlementYearTransfers.Length == 0 && originalFundingFiscalYear != settlementFiscalYear)) gap = true;
            }
            foreach (var request in approvedDetails.Select(item => rows.Approvals[item.ApprovalRequestId]))
            {
                var paid = validSettlements.Where(item => item.ProjectPaymentRequestId == request.Id && item.Amount > 0m
                    && string.Equals(NormalizeCurrency(item.Currency), NormalizeCurrency(request.Currency), StringComparison.Ordinal)).Sum(item => item.Amount);
                var released = releases.Where(item => item.Detail.ReversesApprovalRequestId == request.Id).Sum(item => item.Approval.Amount ?? 0m);
                if ((request.Amount ?? 0m) < paid + released) gap = true;
            }

            var draft = originDetails.Count(item => rows.Approvals[item.ApprovalRequestId].Status == PaymentRequestStatuses.Draft);
            var pending = originDetails.Count(item => rows.Approvals[item.ApprovalRequestId].Status == PaymentRequestStatuses.Pending);
            var approvedOrigin = originDetails.Where(item => rows.Approvals[item.ApprovalRequestId].Status == PaymentRequestStatuses.Approved).ToArray();
            var rejected = originDetails.Count(item => rows.Approvals[item.ApprovalRequestId].Status == PaymentRequestStatuses.Rejected);
            var requestApprovedAmount = approvedOrigin.Where(item => budget is not null && item.ProjectAnnualBudgetId == budget.Id)
                .Sum(item => rows.Approvals[item.ApprovalRequestId].Amount ?? 0m);
            var releasedAmount = releases.Where(item => FundingBudgetId(item, applicableTransfers) == budget?.Id)
                .Sum(item => item.Approval.Amount ?? 0m);
            var sourceAmount = approvedDetails.Where(item => item.ProjectAnnualBudgetId == budget?.Id)
                .Sum(item => rows.Approvals[item.ApprovalRequestId].Amount ?? 0m);
            var transferIn = applicableTransfers.Where(item => item.DestinationAnnualBudgetId == budget?.Id).Sum(item => item.Amount);
            var transferOut = applicableTransfers.Where(item => item.SourceAnnualBudgetId == budget?.Id).Sum(item => item.Amount);
            var rawEffectiveAllocation = sourceAmount + transferIn - transferOut - releasedAmount;
            if (rawEffectiveAllocation < 0m || (budget?.Status == ProjectAnnualBudgetStatuses.Approved
                && rawEffectiveAllocation > budget.TotalAnnualBudget)) gap = true;
            var effectiveAllocation = Math.Max(0m, rawEffectiveAllocation);
            var remainingPayable = 0m;
            foreach (var requestDetail in approvedDetails)
            {
                var request = rows.Approvals[requestDetail.ApprovalRequestId];
                var chain = applicableTransfers.Where(item => item.PaymentRequestId == request.Id)
                    .OrderBy(item => item.SourceFiscalYear).ThenBy(item => item.DestinationFiscalYear).ToArray();
                var currentTransfer = chain.LastOrDefault();
                var currentBudgetId = currentTransfer?.DestinationAnnualBudgetId ?? requestDetail.ProjectAnnualBudgetId;
                if (currentBudgetId != budget?.Id) continue;
                var currentAllocation = currentTransfer?.Amount ?? request.Amount ?? 0m;
                var currentRelease = releases.Where(item => item.Detail.ReversesApprovalRequestId == request.Id
                    && FundingBudgetId(item, applicableTransfers) == currentBudgetId).Sum(item => item.Approval.Amount ?? 0m);
                var paid = validSettlements.Where(item => item.ProjectPaymentRequestId == request.Id
                    && item.FundingTransferId == currentTransfer?.Id
                    && item.Amount > 0m
                    && string.Equals(NormalizeCurrency(item.Currency), NormalizeCurrency(request.Currency), StringComparison.Ordinal))
                    .Sum(item => item.Amount);
                remainingPayable += Math.Max(0m, currentAllocation - currentRelease - paid);
            }

            var notSettled = approvedOrigin.Count(detail =>
            {
                var request = rows.Approvals[detail.ApprovalRequestId];
                var released = releases.Where(item => item.Detail.ReversesApprovalRequestId == detail.ApprovalRequestId).Sum(item => item.Approval.Amount ?? 0m);
                return (request.Amount ?? 0m) > released
                    && validSettlements.All(item => item.ProjectPaymentRequestId != detail.ApprovalRequestId);
            });
            var partialSettled = approvedOrigin.Count(detail =>
            {
                var paid = validSettlements.Where(item => item.ProjectPaymentRequestId == detail.ApprovalRequestId).Sum(item => item.Amount);
                var released = releases.Where(item => item.Detail.ReversesApprovalRequestId == detail.ApprovalRequestId).Sum(item => item.Approval.Amount ?? 0m);
                var effective = Math.Max(0m, (rows.Approvals[detail.ApprovalRequestId].Amount ?? 0m) - released);
                return paid > 0m && paid < effective;
            });
            var fullSettled = approvedOrigin.Count(detail =>
            {
                var paid = validSettlements.Where(item => item.ProjectPaymentRequestId == detail.ApprovalRequestId).Sum(item => item.Amount);
                var released = releases.Where(item => item.Detail.ReversesApprovalRequestId == detail.ApprovalRequestId).Sum(item => item.Approval.Amount ?? 0m);
                var effective = Math.Max(0m, (rows.Approvals[detail.ApprovalRequestId].Amount ?? 0m) - released);
                return effective > 0m && paid >= effective;
            });

            var actualSettlements = validSettlements.Where(item => item.SettlementDate.Date >= period.StartDate.ToDateTime(TimeOnly.MinValue)
                && item.SettlementDate.Date < period.EndDateExclusive.ToDateTime(TimeOnly.MinValue)
                && item.Amount > 0m && string.Equals(NormalizeCurrency(item.Currency), currency, StringComparison.Ordinal))
                .Sum(item => item.Amount);
            decimal? approvedBudget = budget?.Status == ProjectAnnualBudgetStatuses.Approved ? budget.TotalAnnualBudget : null;
            decimal? newFunding = budget?.Status == ProjectAnnualBudgetStatuses.Approved ? budget.NewApprovedFunding : null;
            decimal? freeCarry = budget?.Status == ProjectAnnualBudgetStatuses.Approved ? budget.ApprovedFreeCarryForward : null;
            decimal? reservedCarry = budget?.Status == ProjectAnnualBudgetStatuses.Approved ? budget.ApprovedReservedCarryForward : null;
            decimal? adjustments = budget?.Status == ProjectAnnualBudgetStatuses.Approved ? budget.ApprovedAdjustments : null;
            decimal? active = budget?.Status == ProjectAnnualBudgetStatuses.Approved ? effectiveAllocation : null;
            decimal? available = approvedBudget.HasValue ? approvedBudget.Value - effectiveAllocation : null;
            decimal? releasedOut = budget?.Status == ProjectAnnualBudgetStatuses.Approved ? releasedAmount : null;
            decimal? remaining = budget?.Status == ProjectAnnualBudgetStatuses.Approved ? remainingPayable : null;
            decimal? paidThisYear = actualSettlements;
            decimal? totalUnspent = approvedBudget.HasValue ? approvedBudget.Value - actualSettlements : null;
            decimal? freeCarryOut = closing?.Status == ProjectAnnualBudgetClosingStatuses.Closed ? closing.ApprovedFreeCarryForward : 0m;
            decimal? reservedCarryOut = closing?.Status == ProjectAnnualBudgetClosingStatuses.Closed ? closing.ApprovedReservedCarryForward : 0m;
            decimal? nonCarried = closing?.Status == ProjectAnnualBudgetClosingStatuses.Closed ? closing.NonCarriedFreeBalance : 0m;
            if (closing?.Status == ProjectAnnualBudgetClosingStatuses.Closed)
            {
                approvedBudget = closing.ApprovedAnnualBudget;
                newFunding = closing.NewFunding;
                freeCarry = closing.IncomingCarryForward - closing.ApprovedReservedCarryForward;
                reservedCarry = closing.ApprovedReservedCarryForward;
                adjustments = closing.ApprovedAdjustments;
                active = closing.EffectiveActiveAllocations;
                available = closing.FreeAvailableBudget;
                releasedOut = closing.ApprovedReversalReleases;
                remaining = closing.OutstandingCommitments;
                paidThisYear = closing.ActualSettlementsInFiscalYear;
                totalUnspent = closing.TotalUnspentBudget;
            }

            var status = string.IsNullOrWhiteSpace(currency) || !IsValidCurrency(NormalizeCurrency(currency)) ? "InvalidProjectCurrency"
                : yearBudgets.Length > 1 ? "DuplicateAnnualBudget"
                : budget is null ? "MissingApprovedAnnualBudget"
                : budget.Status != ProjectAnnualBudgetStatuses.Approved ? "AnnualBudgetNotApproved"
                : gap ? "FinancialDataRequiresReconciliation" : "Available";
            if (gap) status = "FinancialDataRequiresReconciliation";
            result.Add(new FinancialRollupProjectDto(project.Id, project.PublicId, project.Name, project.ProgramId, project.ProgramName, !project.ProgramId.HasValue,
                IsValidCurrency(NormalizeCurrency(currency)) && !string.IsNullOrWhiteSpace(currency) ? currency : null, !gap && budget?.Status == ProjectAnnualBudgetStatuses.Approved,
                status, gap ? null : approvedBudget, gap ? null : newFunding, gap ? null : freeCarry, gap ? null : reservedCarry,
                gap ? null : adjustments, gap ? null : active, gap ? null : available, gap ? null : totalUnspent, gap ? null : releasedOut,
                gap ? null : requestApprovedAmount, gap ? null : remaining, gap ? null : paidThisYear,
                originDetails.Length, draft, pending, approvedOrigin.Length, rejected, notSettled, partialSettled, fullSettled,
                closing?.Status is ProjectAnnualBudgetClosingStatuses.Review or ProjectAnnualBudgetClosingStatuses.PendingApproval or ProjectAnnualBudgetClosingStatuses.Closed
                    ? closing.Status : "Open", gap ? null : freeCarryOut, gap ? null : reservedCarryOut, gap ? null : nonCarried));
        }
        return result;
    }

    private static FinancialRollupCurrencyGroupDto AggregateCurrency(string currency, FinancialRollupProjectDto[] projects) => new(
        currency, projects.Length, projects.Sum(item => item.ApprovedAnnualBudget ?? 0m), projects.Sum(item => item.NewApprovedFunding ?? 0m),
        projects.Sum(item => item.ApprovedFreeCarryForward ?? 0m), projects.Sum(item => item.ApprovedReservedCarryForward ?? 0m),
        projects.Sum(item => item.ApprovedAdjustments ?? 0m), projects.Sum(item => item.EffectiveActiveAllocations ?? 0m),
        projects.Sum(item => item.AvailableAnnualBudget ?? 0m), projects.Sum(item => item.TotalUnspentBudget ?? 0m),
        projects.Sum(item => item.ApprovedReversalReleases ?? 0m),
        projects.Sum(item => item.TotalPaymentRequests), projects.Sum(item => item.DraftRequests), projects.Sum(item => item.PendingApprovalRequests),
        projects.Sum(item => item.ApprovedRequests), projects.Sum(item => item.RejectedRequests), projects.Sum(item => item.ApprovedRequestAmount ?? 0m),
        projects.Sum(item => item.RemainingPayableCommitments ?? 0m), projects.Sum(item => item.ActualSettlementsInFiscalYear ?? 0m),
        projects.Sum(item => item.NotSettledRequests), projects.Sum(item => item.PartiallySettledRequests), projects.Sum(item => item.FullySettledRequests),
        projects.Sum(item => item.FreeCarryForward ?? 0m), projects.Sum(item => item.ReservedCarryForward ?? 0m),
        projects.Sum(item => item.NonCarriedBalance ?? 0m));

    private static FinancialRollupHierarchyGroupDto BuildHierarchyGroup(
        Guid? programId, string name, bool isDirectProjectGroup, IReadOnlyList<ProjectRow> projects,
        IReadOnlyDictionary<Guid, FinancialRollupProjectDto> projectResultsById)
    {
        var currencyGroups = projects.Select(project => projectResultsById[project.Id])
            .Where(project => project.IsAvailable && project.Currency is not null)
            .GroupBy(project => project.Currency!, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key)
            .Select(group => AggregateCurrency(group.Key, group.ToArray()))
            .ToArray();
        return new FinancialRollupHierarchyGroupDto(programId, name, isDirectProjectGroup, projects.Count, currencyGroups);
    }

    private static Guid? FundingBudgetId(ReversalRow reversal, IReadOnlyList<ProjectPaymentRequestFundingTransfer> transfers)
    {
        if (!reversal.Detail.FundingTransferId.HasValue) return reversal.Detail.ProjectAnnualBudgetId;
        return transfers.FirstOrDefault(item => item.Id == reversal.Detail.FundingTransferId.Value
            && item.PaymentRequestId == reversal.Detail.ReversesApprovalRequestId)?.DestinationAnnualBudgetId;
    }

    private static string NormalizeCurrency(string? currency) => currency?.Trim().ToUpperInvariant() ?? string.Empty;
    private static bool IsValidCurrency(string? currency) => !string.IsNullOrWhiteSpace(currency) && currency.Length == 3 && currency.All(character => character is >= 'A' and <= 'Z');

    private sealed record ProjectRow(Guid Id, Guid PublicId, string Name, Guid? ProgramId, Guid? PortfolioId, string Currency, string Status)
    {
        public string? ProgramName { get; init; }
    }
    private sealed record PortfolioProgramRow(Guid Id, string Name);
    private sealed record ReversalRow(ProjectPaymentRequestDetail Detail, ApprovalRequest Approval);
    private sealed record FinancialRows(
        IReadOnlyList<ProjectAnnualBudget> Budgets,
        IReadOnlyList<ProjectPaymentRequestDetail> PaymentDetails,
        IReadOnlyList<ProjectPaymentRequestFundingTransfer> Transfers,
        IReadOnlyList<PaymentSettlement> Settlements,
        IReadOnlyList<ProjectAnnualBudgetClosing> Closings,
        IReadOnlyDictionary<Guid, ApprovalRequest> Approvals);
}
