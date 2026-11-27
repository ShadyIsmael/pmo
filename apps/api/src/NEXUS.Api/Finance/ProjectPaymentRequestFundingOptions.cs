using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.CostManagement.Application.DTOs;
using NEXUS.Modules.CostManagement.Domain;
using NEXUS.SharedKernel;

namespace NEXUS.Api.Finance;

public sealed partial class ProjectPaymentRequestWorkflow
{
    public async Task<Result<ProjectPaymentRequestFundingOptionsDto>> GetFundingOptionsAsync(
        Guid tenantId, Guid projectId, Guid actorId, CancellationToken cancellationToken)
    {
        var access = await ValidateServiceAccessAsync(tenantId, projectId, actorId, requireWrite: false,
            cancellationToken, screenViewPermission: CreatePermission);
        if (access.Error is not null)
            return Result<ProjectPaymentRequestFundingOptionsDto>.Failure(access.Error);
        var project = await FindProjectQuery(tenantId, projectId).FirstOrDefaultAsync(cancellationToken);
        if (project is null)
            return Result<ProjectPaymentRequestFundingOptionsDto>.Failure(Error.NotFound("Project not found."));
        var actorError = await ValidateActiveActorAsync(tenantId, actorId, cancellationToken);
        if (actorError is not null)
            return Result<ProjectPaymentRequestFundingOptionsDto>.Failure(actorError);
        if (!await IsCurrentProjectManagerAsync(tenantId, project, actorId, cancellationToken))
            return Result<ProjectPaymentRequestFundingOptionsDto>.Failure(Error.Forbidden(
                "Only the current Project Manager may prepare a Payment Request."));

        var currency = NormalizeCurrency(project.Currency);
        if (!await IsConfiguredCurrencyAsync(currency, cancellationToken))
            return Result<ProjectPaymentRequestFundingOptionsDto>.Failure(Error.Conflict(
                "The project currency is not configured in the currency catalog."));

        var budgets = await db.ProjectAnnualBudgets.IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.ProjectId == projectId && !item.IsDeleted
                && item.Status == ProjectAnnualBudgetStatuses.Approved && item.ClosedAtUtc == null
                && item.Currency == currency)
            .OrderBy(item => item.FiscalYear)
            .ToListAsync(cancellationToken);
        var budgetIds = budgets.Select(item => item.Id).ToArray();
        var lines = budgetIds.Length == 0
            ? []
            : await db.ProjectAnnualBudgetLines.IgnoreQueryFilters().AsNoTracking()
                .Where(item => item.TenantId == tenantId && item.ProjectId == projectId
                    && budgetIds.Contains(item.ProjectAnnualBudgetId) && !item.IsDeleted)
                .OrderBy(item => item.Reference)
                .ToListAsync(cancellationToken);
        var activeRequests = budgetIds.Length == 0
            ? []
            : await (from detail in db.ProjectPaymentRequestDetails.IgnoreQueryFilters().AsNoTracking()
                     join request in db.ApprovalRequests.IgnoreQueryFilters().AsNoTracking()
                         on detail.ApprovalRequestId equals request.Id
                     where detail.TenantId == tenantId && detail.ProjectId == projectId
                         && detail.RequestKind == ProjectPaymentRequestKinds.PaymentRequest
                         && detail.ProjectAnnualBudgetId.HasValue && budgetIds.Contains(detail.ProjectAnnualBudgetId.Value)
                         && (request.Status == PaymentRequestStatuses.Pending || request.Status == PaymentRequestStatuses.Approved)
                         && !request.IsDeleted
                     select new { RequestId = request.Id, BudgetId = detail.ProjectAnnualBudgetId!.Value,
                         LineId = detail.ProjectAnnualBudgetLineId, request.Amount, request.Currency })
                .ToListAsync(cancellationToken);
        var activeRequestIds = activeRequests.Select(item => item.RequestId).Distinct().ToArray();
        var releasedByRequest = activeRequestIds.Length == 0
            ? new Dictionary<Guid, decimal>()
            : await (from detail in db.ProjectPaymentRequestDetails.IgnoreQueryFilters().AsNoTracking()
                     join request in db.ApprovalRequests.IgnoreQueryFilters().AsNoTracking()
                         on detail.ApprovalRequestId equals request.Id
                     where detail.TenantId == tenantId && detail.ProjectId == projectId
                         && detail.RequestKind == ProjectPaymentRequestKinds.ReversalRequest
                         && detail.ReversesApprovalRequestId.HasValue
                         && activeRequestIds.Contains(detail.ReversesApprovalRequestId.Value)
                         && request.Status == PaymentRequestStatuses.Approved && !request.IsDeleted
                     group request by detail.ReversesApprovalRequestId!.Value into grouped
                     select new { RequestId = grouped.Key, Amount = grouped.Sum(item => item.Amount ?? 0m) })
                .ToDictionaryAsync(item => item.RequestId, item => item.Amount, cancellationToken);

        var lineTotals = new Dictionary<Guid, decimal>();
        var budgetTotals = new Dictionary<Guid, decimal>();
        foreach (var row in activeRequests)
        {
            var amount = row.Amount ?? 0m;
            var released = releasedByRequest.GetValueOrDefault(row.RequestId);
            if (amount <= 0m || released > amount || !string.Equals(NormalizeCurrency(row.Currency), currency, StringComparison.Ordinal))
                return Result<ProjectPaymentRequestFundingOptionsDto>.Failure(Error.Conflict(
                    "Active Payment Request funding contains an invalid amount or currency. Reconcile it before creating another request."));
            var allocation = Math.Max(0m, amount - released);
            budgetTotals[row.BudgetId] = budgetTotals.GetValueOrDefault(row.BudgetId) + allocation;
            if (row.LineId.HasValue)
                lineTotals[row.LineId.Value] = lineTotals.GetValueOrDefault(row.LineId.Value) + allocation;
        }

        var options = budgets.Select(budget =>
        {
            var budgetLines = lines.Where(line => line.ProjectAnnualBudgetId == budget.Id).Select(line =>
            {
                var reserved = lineTotals.GetValueOrDefault(line.Id);
                return new ProjectPaymentRequestBudgetLineOptionDto(line.Id, line.Reference, line.Description,
                    line.AuthorizedLimit, reserved, Math.Max(0m, line.AuthorizedLimit - reserved));
            }).ToArray();
            var reserved = budgetTotals.GetValueOrDefault(budget.Id);
            return new ProjectPaymentRequestAnnualBudgetOptionDto(budget.Id, budget.FinancialReference,
                budget.FiscalYear, currency, budget.Status, budget.TotalAnnualBudget, reserved,
                Math.Max(0m, budget.TotalAnnualBudget - reserved), budgetLines);
        }).ToArray();
        return Result<ProjectPaymentRequestFundingOptionsDto>.Success(
            new ProjectPaymentRequestFundingOptionsDto(currency, options));
    }
}
