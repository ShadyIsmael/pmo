using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.CostManagement.Application.DTOs;
using NEXUS.Modules.CostManagement.Domain;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Helpers;
using NEXUS.SharedKernel.Management;

namespace NEXUS.Modules.CostManagement.Application;

public sealed class BudgetService : IBudgetService
{
    private readonly DbContext _context;
    private readonly IAuditService _auditService;
    private readonly IProjectReferenceLookup _projectLookup;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IManagementContextAccessor? _managementContext;

    public BudgetService(DbContext context, IAuditService auditService, IProjectReferenceLookup projectLookup, IHttpContextAccessor httpContextAccessor, IManagementContextAccessor? managementContext = null)
    {
        _context = context; _auditService = auditService; _projectLookup = projectLookup; _httpContextAccessor = httpContextAccessor; _managementContext = managementContext;
    }

    public async Task<Result<BudgetDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var accessError = FinancePermissionGuard.Require(_httpContextAccessor, "Budget.View");
        if (accessError is not null) return Result<BudgetDto>.Failure(accessError);
        var budget = await ApplyManagementScope(_context.Set<Budget>().Where(item => item.TenantId == CurrentTenant()))
            .FirstOrDefaultAsync(item => item.Id == id && !item.IsDeleted, cancellationToken);
        return budget is null ? Result<BudgetDto>.Failure(Error.NotFound()) : Result<BudgetDto>.Success(await ToDtoAsync(budget, cancellationToken));
    }

    public async Task<PagedResult<BudgetDto>> GetPagedAsync(int page, int pageSize, Guid? projectId = null, Guid? portfolioId = null, bool? hasForecast = null, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 200);
        if (FinancePermissionGuard.Require(_httpContextAccessor, "Budget.View") is not null)
            return new PagedResult<BudgetDto>(Array.Empty<BudgetDto>(), page, pageSize, 0);
        var query = ApplyManagementScope(_context.Set<Budget>().AsNoTracking().Where(item => item.TenantId == CurrentTenant() && !item.IsDeleted));
        if (projectId.HasValue) query = query.Where(item => item.ProjectId == projectId);
        if (portfolioId.HasValue) query = query.Where(item => item.PortfolioId == portfolioId);
        if (hasForecast == true) query = query.Where(item => item.ForecastAmount > 0);
        else if (hasForecast == false) query = query.Where(item => item.ForecastAmount <= 0);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(item => item.FiscalYear).ThenBy(item => item.FiscalQuarter).ThenBy(item => item.Name)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        var projects = (await _projectLookup.GetFinancialReferencesAsync(cancellationToken))
            .ToDictionary(project => project.Id, project => project.PublicId);
        var dtos = items.Select(item => item.ToDto(
            item.ProjectId.HasValue && projects.TryGetValue(item.ProjectId.Value, out var publicId) ? publicId : null)).ToList();
        return new PagedResult<BudgetDto>(dtos, page, pageSize, total);
    }

    public async Task<Result<BudgetDto>> CreateAsync(CreateBudgetDto dto, string userId, CancellationToken cancellationToken = default)
    {
        var accessError = FinancePermissionGuard.Require(_httpContextAccessor, "Budget.Create");
        if (accessError is not null) return Result<BudgetDto>.Failure(accessError);
        if (!CanAccessProjectOrPortfolio(dto.ProjectId, dto.PortfolioId))
            return Result<BudgetDto>.Failure(Error.Forbidden("The budget is outside the actor's current project or portfolio scope."));
        await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
        var validation = await ValidateAsync(dto.ProjectId, dto.Name, dto.Category, dto.PlannedAmount, dto.ApprovedAmount, dto.ForecastAmount, dto.Currency, dto.FiscalYear, dto.FiscalQuarter, dto.StartDate, dto.EndDate, cancellationToken);
        if (validation is not null) return Result<BudgetDto>.Failure(validation);
        var allocationError = await BudgetAuthority.ValidateAllocationAsync(_context, _projectLookup, dto.ProjectId, null, dto.ApprovedAmount, cancellationToken);
        if (allocationError is not null) return Result<BudgetDto>.Failure(allocationError);
        var budget = new Budget
        {
            TenantId = CurrentTenant(), CreatedBy = userId, ProjectId = dto.ProjectId, PortfolioId = dto.PortfolioId,
            Name = dto.Name.Trim(), Description = dto.Description?.Trim(), Category = dto.Category.Trim(),
            PlannedAmount = dto.PlannedAmount, ApprovedAmount = dto.ApprovedAmount, ForecastAmount = dto.ForecastAmount,
            Currency = NormalizeCurrency(dto.Currency), FiscalYear = dto.FiscalYear, FiscalQuarter = dto.FiscalQuarter,
            Status = dto.Status.Trim(), StartDate = dto.StartDate, EndDate = dto.EndDate, RowVersion = NewVersion()
        };
        _context.Set<Budget>().Add(budget);
        await AuditAsync("Budget", budget.Id, "Create", userId, null, dto, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result<BudgetDto>.Success(await ToDtoAsync(budget, cancellationToken));
    }

    public async Task<Result<BudgetDto>> UpdateAsync(Guid id, UpdateBudgetDto dto, string userId, CancellationToken cancellationToken = default)
    {
        var accessError = FinancePermissionGuard.Require(_httpContextAccessor, "Budget.Edit");
        if (accessError is not null) return Result<BudgetDto>.Failure(accessError);
        await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
        var budget = await ApplyManagementScope(_context.Set<Budget>().Where(item => item.TenantId == CurrentTenant()))
            .FirstOrDefaultAsync(item => item.Id == id && !item.IsDeleted, cancellationToken);
        if (budget is null) return Result<BudgetDto>.Failure(Error.NotFound("Budget not found"));
        if (!MatchesVersion(budget.RowVersion, dto.RowVersion)) return Result<BudgetDto>.Failure(Error.Conflict("Budget was changed by another user"));
        var validation = await ValidateAsync(budget.ProjectId, dto.Name, dto.Category, dto.PlannedAmount, dto.ApprovedAmount, dto.ForecastAmount, dto.Currency, dto.FiscalYear, dto.FiscalQuarter, dto.StartDate, dto.EndDate, cancellationToken);
        if (validation is not null) return Result<BudgetDto>.Failure(validation);
        if (dto.ApprovedAmount != budget.ApprovedAmount)
        {
            var allocationError = await BudgetAuthority.ValidateAllocationAsync(_context, _projectLookup, budget.ProjectId, id, dto.ApprovedAmount, cancellationToken);
            if (allocationError is not null) return Result<BudgetDto>.Failure(allocationError);
        }
        var original = JsonSerializer.Serialize(budget.ToDto());
        budget.Name = dto.Name.Trim(); budget.Description = dto.Description?.Trim(); budget.Category = dto.Category.Trim();
        budget.PlannedAmount = dto.PlannedAmount; budget.ApprovedAmount = dto.ApprovedAmount; budget.ForecastAmount = dto.ForecastAmount;
        budget.Currency = NormalizeCurrency(dto.Currency); budget.FiscalYear = dto.FiscalYear; budget.FiscalQuarter = dto.FiscalQuarter;
        budget.Status = dto.Status.Trim(); budget.StartDate = dto.StartDate; budget.EndDate = dto.EndDate;
        budget.UpdatedBy = userId; budget.UpdatedAt = DateTime.UtcNow; budget.RowVersion = NewVersion();
        await AuditAsync("Budget", budget.Id, "Update", userId, original, dto, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken);
        return Result<BudgetDto>.Success(await ToDtoAsync(budget, cancellationToken));
    }

    public async Task<Result> DeleteAsync(Guid id, byte[] rowVersion, string userId, CancellationToken cancellationToken = default)
    {
        var accessError = FinancePermissionGuard.Require(_httpContextAccessor, "Budget.Delete");
        if (accessError is not null) return Result.Failure(accessError);
        var budget = await ApplyManagementScope(_context.Set<Budget>().Where(item => item.TenantId == CurrentTenant()))
            .FirstOrDefaultAsync(item => item.Id == id && !item.IsDeleted, cancellationToken);
        if (budget is null) return Result.Failure(Error.NotFound("Budget not found"));
        if (!MatchesVersion(budget.RowVersion, rowVersion)) return Result.Failure(Error.Conflict("Budget was changed by another user"));
        var original = JsonSerializer.Serialize(budget.ToDto());
        budget.IsDeleted = true; budget.UpdatedBy = userId; budget.UpdatedAt = DateTime.UtcNow; budget.RowVersion = NewVersion();
        await AuditAsync("Budget", budget.Id, "Delete", userId, original, null, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken); return Result.Success();
    }

    private async Task<Error?> ValidateAsync(Guid? projectId, string? name, string? category, decimal planned, decimal approved, decimal forecast, string? currency, int fiscalYear, int? quarter, DateTime? start, DateTime? end, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name)) return Error.Validation("Budget name is required");
        if (string.IsNullOrWhiteSpace(category)) return Error.Validation("Budget category is required");
        if (planned < 0 || approved < 0 || forecast < 0) return Error.Validation("Budget amounts cannot be negative");
        if (string.IsNullOrWhiteSpace(currency) || currency.Trim().Length != 3) return Error.Validation("Currency must be a 3-letter code");
        if (fiscalYear is < 1900 or > 2200) return Error.Validation("Fiscal year is invalid");
        if (quarter is < 1 or > 4) return Error.Validation("Fiscal quarter is invalid");
        if (start.HasValue && end.HasValue && end < start) return Error.Validation("Budget end date cannot be before start date");
        if (projectId.HasValue)
        {
            var project = await _projectLookup.FindAsync(projectId.Value, cancellationToken);
            if (project is null) return Error.Validation("Project not found");
            if (!string.Equals(NormalizeCurrency(currency), NormalizeCurrency(project.Currency), StringComparison.OrdinalIgnoreCase)) return Error.Validation("Budget currency must match the project currency");
        }
        return null;
    }

    private async Task<BudgetDto> ToDtoAsync(Budget budget, CancellationToken cancellationToken)
    {
        var project = budget.ProjectId.HasValue
            ? await _projectLookup.FindAsync(budget.ProjectId.Value, cancellationToken)
            : null;
        return budget.ToDto(project?.PublicId);
    }

    private IQueryable<Budget> ApplyManagementScope(IQueryable<Budget> query)
    {
        var management = _managementContext?.Current;
        if (management is null || management.TenantId != CurrentTenant() || management.Scope is null)
            return query.Where(_ => false);
        var scope = management.Scope;
        var projectIds = scope.ProjectIds.ToArray();
        var portfolioIds = scope.PortfolioIds.ToArray();
        return query.Where(item =>
            item.ProjectId.HasValue
                ? projectIds.Contains(item.ProjectId.Value)
                : item.PortfolioId.HasValue && portfolioIds.Contains(item.PortfolioId.Value));
    }

    private bool CanAccessProjectOrPortfolio(Guid? projectId, Guid? portfolioId)
    {
        var management = _managementContext?.Current;
        if (management is null || management.TenantId != CurrentTenant() || management.Scope is null) return false;
        if (projectId.HasValue) return management.Scope.ProjectIds.Contains(projectId.Value);
        if (portfolioId.HasValue) return management.Scope.PortfolioIds.Contains(portfolioId.Value);
        return false;
    }

    private Guid CurrentTenant() => FinancePermissionGuard.TryGetTenantId(_httpContextAccessor.HttpContext?.User, out var tenant) ? tenant : Guid.Empty;
    private static byte[] NewVersion() => Guid.NewGuid().ToByteArray();
    private static bool MatchesVersion(byte[] current, byte[] requested) => requested.Length == 0 || current.Length == 0 || current.SequenceEqual(requested);
    private static string NormalizeCurrency(string? value) => (value ?? string.Empty).Trim().ToUpperInvariant();
    private string? CurrentUserName => _httpContextAccessor.HttpContext?.User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value
        ?? _httpContextAccessor.HttpContext?.User.FindFirst("name")?.Value;
    private async Task AuditAsync(string type, Guid id, string action, string userId, string? original, object? values, CancellationToken cancellationToken)
    {
        var result = await _auditService.AppendAsync(FinancialAuditEntryFactory.ForCurrentRecord(
            _context, type, id, action, userId, CurrentUserName, original, values), cancellationToken);
        if (result.IsFailure) throw new InvalidOperationException(result.Error?.Message ?? "Financial audit append failed.");
    }
}
