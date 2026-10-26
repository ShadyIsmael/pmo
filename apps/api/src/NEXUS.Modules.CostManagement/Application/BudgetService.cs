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
        var budget = await _context.Set<Budget>().FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        return budget is null ? Result<BudgetDto>.Failure(Error.NotFound()) : Result<BudgetDto>.Success(await ToDtoAsync(budget, cancellationToken));
    }

    public async Task<PagedResult<BudgetDto>> GetPagedAsync(int page, int pageSize, Guid? projectId = null, Guid? portfolioId = null, bool? hasForecast = null, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 200);
        var query = ApplyManagementScope(_context.Set<Budget>().AsNoTracking());
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
        _context.Set<Budget>().Add(budget); await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await AuditAsync("Budget", budget.Id, "Create", userId, null, dto, cancellationToken);
        return Result<BudgetDto>.Success(await ToDtoAsync(budget, cancellationToken));
    }

    public async Task<Result<BudgetDto>> UpdateAsync(Guid id, UpdateBudgetDto dto, string userId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
        var budget = await _context.Set<Budget>().FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
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
        await _context.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); await AuditAsync("Budget", budget.Id, "Update", userId, original, dto, cancellationToken);
        return Result<BudgetDto>.Success(await ToDtoAsync(budget, cancellationToken));
    }

    public async Task<Result> DeleteAsync(Guid id, byte[] rowVersion, string userId, CancellationToken cancellationToken = default)
    {
        var budget = await _context.Set<Budget>().FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (budget is null) return Result.Failure(Error.NotFound("Budget not found"));
        if (!MatchesVersion(budget.RowVersion, rowVersion)) return Result.Failure(Error.Conflict("Budget was changed by another user"));
        budget.IsDeleted = true; budget.UpdatedBy = userId; budget.UpdatedAt = DateTime.UtcNow; budget.RowVersion = NewVersion();
        await _context.SaveChangesAsync(cancellationToken); await AuditAsync("Budget", budget.Id, "Delete", userId, null, null, cancellationToken); return Result.Success();
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
        if (_managementContext is null) return query;
        var scope = _managementContext.Current?.Scope;
        if (scope is null) return query.Where(_ => false);
        var projectIds = scope.ProjectIds.ToArray();
        var portfolioIds = scope.PortfolioIds.ToArray();
        return query.Where(item =>
            item.ProjectId.HasValue
                ? projectIds.Contains(item.ProjectId.Value)
                : item.PortfolioId.HasValue && portfolioIds.Contains(item.PortfolioId.Value));
    }

    private Guid CurrentTenant() => Guid.TryParse(_httpContextAccessor.HttpContext?.User.FindFirst("tenantId")?.Value, out var tenant) ? tenant : TenantConstants.DemoTenantId;
    private static byte[] NewVersion() => Guid.NewGuid().ToByteArray();
    private static bool MatchesVersion(byte[] current, byte[] requested) => requested.Length == 0 || current.Length == 0 || current.SequenceEqual(requested);
    private static string NormalizeCurrency(string? value) => (value ?? "GBP").Trim().ToUpperInvariant();
    private async Task AuditAsync(string type, Guid id, string action, string userId, string? original, object? values, CancellationToken cancellationToken) => await _auditService.LogAsync(new AuditEntry { EntityType = type, EntityId = id, Action = action, UserId = Guid.TryParse(userId, out var parsed) ? parsed : null, OriginalValues = original, NewValues = values is null ? null : JsonSerializer.Serialize(values), Timestamp = DateTime.UtcNow, CreatedBy = userId }, cancellationToken);
}
