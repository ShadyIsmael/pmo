using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.CostManagement.Application;
using NEXUS.Modules.CostManagement.Application.DTOs;
using NEXUS.Modules.CostManagement.Domain;
using NEXUS.Modules.Organization.Domain;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.Persistence;
using NEXUS.SharedKernel.Management;

namespace NEXUS.Api.Finance;

[ApiController]
[Route("api/projects/{projectId:guid}/financial-foundation")]
[Authorize]
public sealed class ProjectFinancialFoundationController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IAuditService _audit;
    private readonly IManagementContextAccessor _management;

    public ProjectFinancialFoundationController(AppDbContext db, IAuditService audit, IManagementContextAccessor management)
    {
        _db = db;
        _audit = audit;
        _management = management;
    }

    [HttpGet]
    [Authorize(Policy = "ProjectFinancialFoundation.View")]
    [Authorize(Policy = "ProjectSettings.View")]
    public async Task<IActionResult> Get(Guid projectId, CancellationToken cancellationToken)
    {
        if (!TryGetTenantId(out var tenantId)) return Unauthorized();
        if (!IsInProjectScope(projectId)) return NotFound();

        var project = await _db.Projects.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == projectId && item.TenantId == tenantId && !item.IsDeleted, cancellationToken);
        if (project is null) return NotFound();

        var obligations = await _db.Set<PaymentObligation>().IgnoreQueryFilters()
            .Where(item => item.ProjectId == project.Id && item.TenantId == tenantId && !item.IsDeleted)
            .Include(item => item.Settlements.Where(settlement => settlement.TenantId == tenantId && !settlement.IsDeleted))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        // Actual Paid also includes confirmed settlements recorded against
        // approved Project Payment Requests, resolved through the shared
        // settlement calculator so both payment rails report once.
        var settlementFacts = await ActualPaidSettlementCalculator.LoadForProjectAsync(
            _db, tenantId, project.Id, ignoreQueryFilters: true, cancellationToken: cancellationToken);
        var requestSettlements = settlementFacts
            .Where(item => item.ProjectPaymentRequestId.HasValue)
            .ToArray();

        var currency = NormalizeCurrency(project.Currency);
        var currencyIsValid = currency.Length == 3
            && currency.All(character => character is >= 'A' and <= 'Z')
            && await _db.Set<Currency>().IgnoreQueryFilters().AnyAsync(item => item.Code == currency && !item.IsDeleted, cancellationToken);
        var obligationsMatchProjectCurrency = obligations.All(item =>
            string.Equals(NormalizeCurrency(item.Currency), currency, StringComparison.Ordinal));
        var requestSettlementsMatchProjectCurrency = requestSettlements.All(item =>
            string.Equals(NormalizeCurrency(item.Currency), currency, StringComparison.Ordinal));
        var adjustmentCurrencies = await _db.PaymentSettlementAdjustments.IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.ProjectId == project.Id && !item.IsDeleted)
            .Select(item => item.Currency)
            .ToListAsync(cancellationToken);
        var adjustmentsMatchProjectCurrency = adjustmentCurrencies.All(item =>
            string.Equals(NormalizeCurrency(item), currency, StringComparison.Ordinal));
        var financialRecordsAreComparable = currencyIsValid && obligationsMatchProjectCurrency
            && requestSettlementsMatchProjectCurrency;
        var grossActualPaid = financialRecordsAreComparable
            ? ProjectFinancialBalanceCalculator.CalculateActualPaid(obligations) + requestSettlements.Sum(item => item.Amount)
            : (decimal?)null;
        var netAdjustment = adjustmentsMatchProjectCurrency && currencyIsValid
            ? await ActualPaidSettlementCalculator.SumNetAdjustmentsAsync(
                _db, tenantId, project.Id, currency, cancellationToken: cancellationToken)
            : (decimal?)null;
        decimal? netActualPaid = grossActualPaid.HasValue && netAdjustment.HasValue
            ? grossActualPaid.Value + netAdjustment.Value
            : null;
        var actorId = _management.Current?.UserId;
        var isCurrentProjectManager = actorId.HasValue
            && (project.ProjectManagerId == actorId.Value
                || await _db.ProjectManagerAssignments.IgnoreQueryFilters().AnyAsync(item =>
                    item.TenantId == tenantId && item.ProjectId == project.Id
                    && item.ProjectManagerId == actorId.Value && item.IsCurrent && !item.IsDeleted,
                    cancellationToken));
        // Project.Budget is the lifetime financial ceiling. It is not spendable
        // authority; annual authorization remains in the linked ProjectAnnualBudget.
        // Preserve legacy response fields for compatibility, but do not compute a
        // second spendable balance from the lifetime ceiling.
        decimal? approvedPaymentRequestAllocations = null;
        decimal? availableBudget = null;

        return Ok(new ProjectFinancialFoundationDto(
            project.Id,
            currencyIsValid ? currency : null,
            project.Budget,
            approvedPaymentRequestAllocations,
            availableBudget,
            grossActualPaid,
            financialRecordsAreComparable ? ProjectFinancialBalanceCalculator.CalculateOutstandingPaymentBalance(obligations) : null,
            project.FinanceApprovalMode,
            Convert.ToBase64String(project.RowVersion),
            grossActualPaid,
            netActualPaid,
            isCurrentProjectManager));
    }

    [HttpPut("approval-configuration")]
    [Authorize(Policy = "ProjectAnnualBudget.ManageApprovalConfiguration")]
    [Authorize(Policy = "ProjectSettings.View")]
    [Authorize(Policy = "ProjectSettings.Edit")]
    [Authorize(Policy = "ProjectFinancialFoundation.View")]
    public async Task<IActionResult> UpdateApprovalConfiguration(
        Guid projectId,
        [FromBody] UpdateProjectFinanceApprovalConfigurationDto request,
        CancellationToken cancellationToken)
    {
        if (!TryGetTenantId(out var tenantId)) return Unauthorized();
        if (!IsInProjectScope(projectId)) return NotFound();
        if (_management.Current?.EffectiveReadOnly == true) return Forbid();
        if (!FinanceApprovalModes.IsValid(request.Mode))
            return BadRequest(new { error = "Mode must be Sequential, Parallel, Both, or null." });
        if (!TryDecodeVersion(request.RowVersion, out var requestedVersion))
            return BadRequest(new { error = "A valid project row version is required." });

        var project = await _db.Projects.IgnoreQueryFilters()
            .FirstOrDefaultAsync(item => item.Id == projectId && item.TenantId == tenantId && !item.IsDeleted, cancellationToken);
        if (project is null) return NotFound();
        if (!project.RowVersion.SequenceEqual(requestedVersion))
            return Conflict(new { error = "Project financial configuration changed. Refresh before saving." });

        var previousMode = project.FinanceApprovalMode;
        if (string.Equals(previousMode, request.Mode, StringComparison.Ordinal))
            return Ok(new { mode = project.FinanceApprovalMode, rowVersion = Convert.ToBase64String(project.RowVersion) });

        project.FinanceApprovalMode = request.Mode;
        var actorId = Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var parsedActor) ? parsedActor : (Guid?)null;
        var actorName = User.FindFirst(ClaimTypes.Name)?.Value ?? User.FindFirst("name")?.Value;
        var before = new { financialReference = project.Code, financeApprovalMode = previousMode };
        var after = new { financialReference = project.Code, financeApprovalMode = request.Mode };
        await _audit.AppendAsync(new AuditEntry
        {
            ProjectId = project.Id,
            TenantId = project.TenantId,
            EntityType = "ProjectFinanceConfiguration",
            EntityId = project.Id,
            Action = "ApprovalModeChanged",
            UserId = actorId,
            UserName = actorName,
            Timestamp = DateTime.UtcNow,
            Description = $"Project financial approval configuration changed for {project.Code}",
            Changes = JsonSerializer.Serialize(new
            {
                financialReference = new { from = project.Code, to = project.Code },
                financeApprovalMode = new { from = previousMode, to = request.Mode }
            }),
            OriginalValues = JsonSerializer.Serialize(before),
            NewValues = JsonSerializer.Serialize(after),
            CreatedBy = actorId?.ToString() ?? "System"
        }, cancellationToken);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new { error = "Project financial configuration changed. Refresh before saving." });
        }

        return Ok(new { mode = project.FinanceApprovalMode, rowVersion = Convert.ToBase64String(project.RowVersion) });
    }

    private bool TryGetTenantId(out Guid tenantId) => Guid.TryParse(User.FindFirst("tenantId")?.Value, out tenantId);

    private bool IsInProjectScope(Guid projectId) => _management.Current?.Scope.ProjectIds.Contains(projectId) == true;

    private static string NormalizeCurrency(string? value) => (value ?? string.Empty).Trim().ToUpperInvariant();

    private static bool TryDecodeVersion(string? value, out byte[] version)
    {
        try
        {
            version = string.IsNullOrWhiteSpace(value) ? [] : Convert.FromBase64String(value);
            return version.Length > 0;
        }
        catch (FormatException)
        {
            version = [];
            return false;
        }
    }
}
