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
        if (!TryGetIdentityScope(projectId, out var tenantId, out _)) return ScopeFailure();

        var project = await _db.Projects.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == projectId && item.TenantId == tenantId && !item.IsDeleted, cancellationToken);
        if (project is null) return NotFound();

        var obligations = await _db.Set<PaymentObligation>().IgnoreQueryFilters()
            .Where(item => item.ProjectId == project.Id && item.TenantId == tenantId && !item.IsDeleted)
            .Include(item => item.Settlements.Where(settlement => settlement.TenantId == tenantId && !settlement.IsDeleted))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var currency = NormalizeCurrency(project.Currency);
        var currencyIsValid = currency.Length == 3
            && currency.All(character => character is >= 'A' and <= 'Z')
            && await _db.Set<Currency>().IgnoreQueryFilters().AnyAsync(item => item.Code == currency && !item.IsDeleted, cancellationToken);
        var obligationsMatchProjectCurrency = obligations.All(item =>
            string.Equals(NormalizeCurrency(item.Currency), currency, StringComparison.Ordinal));
        var financialRecordsAreComparable = currencyIsValid && obligationsMatchProjectCurrency;
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
            financialRecordsAreComparable ? ProjectFinancialBalanceCalculator.CalculateActualPaid(obligations) : null,
            financialRecordsAreComparable ? ProjectFinancialBalanceCalculator.CalculateOutstandingPaymentBalance(obligations) : null,
            project.FinanceApprovalMode,
            Convert.ToBase64String(project.RowVersion)));
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
        if (!TryGetIdentityScope(projectId, out var tenantId, out var actorId)) return ScopeFailure();
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
            CreatedBy = actorId.ToString()
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

    private bool TryGetIdentityScope(Guid projectId, out Guid tenantId, out Guid actorId)
    {
        tenantId = Guid.Empty;
        actorId = Guid.Empty;
        var current = _management.Current;
        return current is not null
            && Guid.TryParse(User.FindFirst("tenantId")?.Value, out tenantId)
            && tenantId != Guid.Empty
            && Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out actorId)
            && actorId != Guid.Empty
            && current.TenantId == tenantId
            && current.UserId == actorId
            && current.Scope.ProjectIds.Contains(projectId);
    }

    private IActionResult ScopeFailure() => Guid.TryParse(User.FindFirst("tenantId")?.Value, out var tenantId)
        && tenantId != Guid.Empty
        ? NotFound()
        : Unauthorized();

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
