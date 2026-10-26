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

public sealed class CostService : ICostService
{
    private readonly DbContext _context;
    private readonly IAuditService _auditService;
    private readonly IProjectReferenceLookup _projectLookup;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IManagementContextAccessor? _managementContext;

    public CostService(DbContext context, IAuditService auditService, IProjectReferenceLookup projectLookup, IHttpContextAccessor httpContextAccessor, IManagementContextAccessor? managementContext = null)
    {
        _context = context; _auditService = auditService; _projectLookup = projectLookup; _httpContextAccessor = httpContextAccessor; _managementContext = managementContext;
    }

    public async Task<Result<CostEntryDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entry = await _context.Set<CostEntry>().FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        return entry is null ? Result<CostEntryDto>.Failure(Error.NotFound()) : Result<CostEntryDto>.Success(entry.ToDto());
    }

    public async Task<PagedResult<CostEntryDto>> GetPagedAsync(int page, int pageSize, Guid? projectId = null, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 200);
        var query = ApplyManagementScope(_context.Set<CostEntry>().AsNoTracking());
        if (projectId.HasValue) query = query.Where(item => item.ProjectId == projectId);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(item => item.IncurredDate).ThenBy(item => item.Description)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new PagedResult<CostEntryDto>(items.Select(item => item.ToDto()).ToList(), page, pageSize, total);
    }

    public async Task<Result<CostEntryDto>> CreateAsync(CreateCostEntryDto dto, string userId, CancellationToken cancellationToken = default)
    {
        var validation = await ValidateAsync(dto.ProjectId, dto.BudgetId, dto.Category, dto.Amount, dto.TaxAmount, dto.Currency, dto.IncurredDate, cancellationToken);
        if (validation is not null) return Result<CostEntryDto>.Failure(validation);
        var entry = new CostEntry
        {
            TenantId = CurrentTenant(), CreatedBy = userId, ProjectId = dto.ProjectId, BudgetId = dto.BudgetId, ResourceId = dto.ResourceId,
            Category = dto.Category.Trim(), SubCategory = dto.SubCategory?.Trim(), Description = dto.Description?.Trim(), Amount = dto.Amount,
            Currency = NormalizeCurrency(dto.Currency), TaxAmount = dto.TaxAmount, IncurredDate = dto.IncurredDate, Type = dto.Type.Trim(),
            Reference = dto.Reference?.Trim(), VendorName = dto.VendorName?.Trim(), Status = dto.Status.Trim(), IncurredBy = dto.IncurredBy, RowVersion = NewVersion()
        };
        _context.Set<CostEntry>().Add(entry); await _context.SaveChangesAsync(cancellationToken);
        await AuditAsync("CostEntry", entry.Id, "Create", userId, null, dto, cancellationToken);
        return Result<CostEntryDto>.Success(entry.ToDto());
    }

    public async Task<Result<CostEntryDto>> UpdateAsync(Guid id, UpdateCostEntryDto dto, string userId, CancellationToken cancellationToken = default)
    {
        var entry = await _context.Set<CostEntry>().FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entry is null) return Result<CostEntryDto>.Failure(Error.NotFound("Cost entry not found"));
        if (!MatchesVersion(entry.RowVersion, dto.RowVersion)) return Result<CostEntryDto>.Failure(Error.Conflict("Cost entry was changed by another user"));
        var validation = await ValidateAsync(entry.ProjectId, dto.BudgetId, dto.Category, dto.Amount, dto.TaxAmount, dto.Currency, dto.IncurredDate, cancellationToken);
        if (validation is not null) return Result<CostEntryDto>.Failure(validation);
        var original = JsonSerializer.Serialize(entry.ToDto());
        entry.BudgetId = dto.BudgetId; entry.ResourceId = dto.ResourceId; entry.Category = dto.Category.Trim(); entry.SubCategory = dto.SubCategory?.Trim();
        entry.Description = dto.Description?.Trim(); entry.Amount = dto.Amount; entry.Currency = NormalizeCurrency(dto.Currency); entry.TaxAmount = dto.TaxAmount;
        entry.IncurredDate = dto.IncurredDate; entry.Type = dto.Type.Trim(); entry.Reference = dto.Reference?.Trim(); entry.VendorName = dto.VendorName?.Trim();
        entry.Status = dto.Status.Trim(); entry.IncurredBy = dto.IncurredBy; entry.UpdatedBy = userId; entry.UpdatedAt = DateTime.UtcNow; entry.RowVersion = NewVersion();
        await _context.SaveChangesAsync(cancellationToken); await AuditAsync("CostEntry", entry.Id, "Update", userId, original, dto, cancellationToken);
        return Result<CostEntryDto>.Success(entry.ToDto());
    }

    public async Task<Result> DeleteAsync(Guid id, byte[] rowVersion, string userId, CancellationToken cancellationToken = default)
    {
        var entry = await _context.Set<CostEntry>().FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entry is null) return Result.Failure(Error.NotFound("Cost entry not found"));
        if (!MatchesVersion(entry.RowVersion, rowVersion)) return Result.Failure(Error.Conflict("Cost entry was changed by another user"));
        entry.IsDeleted = true; entry.UpdatedBy = userId; entry.UpdatedAt = DateTime.UtcNow; entry.RowVersion = NewVersion();
        await _context.SaveChangesAsync(cancellationToken); await AuditAsync("CostEntry", entry.Id, "Delete", userId, null, null, cancellationToken); return Result.Success();
    }

    private async Task<Error?> ValidateAsync(Guid? projectId, Guid? budgetId, string? category, decimal amount, decimal? tax, string? currency, DateTime incurredDate, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(category)) return Error.Validation("Cost category is required");
        if (amount < 0 || tax < 0) return Error.Validation("Cost amounts cannot be negative");
        if (string.IsNullOrWhiteSpace(currency) || currency.Trim().Length != 3) return Error.Validation("Currency must be a 3-letter code");
        if (incurredDate == default) return Error.Validation("Incurred date is required");
        if (projectId.HasValue)
        {
            var project = await _projectLookup.FindAsync(projectId.Value, cancellationToken);
            if (project is null) return Error.Validation("Project not found");
            if (!string.Equals(NormalizeCurrency(currency), NormalizeCurrency(project.Currency), StringComparison.OrdinalIgnoreCase)) return Error.Validation("Cost currency must match the project currency");
        }
        if (budgetId.HasValue)
        {
            var budget = await _context.Set<Budget>().FirstOrDefaultAsync(item => item.Id == budgetId.Value, cancellationToken);
            if (budget is null) return Error.Validation("Budget not found");
            if (projectId.HasValue && budget.ProjectId != projectId) return Error.Validation("Budget does not belong to the project");
        }
        return null;
    }

    private Guid CurrentTenant() => Guid.TryParse(_httpContextAccessor.HttpContext?.User.FindFirst("tenantId")?.Value, out var tenant) ? tenant : TenantConstants.DemoTenantId;
    private IQueryable<CostEntry> ApplyManagementScope(IQueryable<CostEntry> query)
    {
        if (_managementContext is null) return query;
        var projectIds = _managementContext.Current?.Scope.ProjectIds.ToArray();
        if (projectIds is null) return query.Where(_ => false);
        return query.Where(item => item.ProjectId.HasValue
            ? projectIds.Contains(item.ProjectId.Value)
            : !item.BudgetId.HasValue);
    }
    private static byte[] NewVersion() => Guid.NewGuid().ToByteArray();
    private static bool MatchesVersion(byte[] current, byte[] requested) => requested.Length == 0 || current.Length == 0 || current.SequenceEqual(requested);
    private static string NormalizeCurrency(string? value) => (value ?? "GBP").Trim().ToUpperInvariant();
    private async Task AuditAsync(string type, Guid id, string action, string userId, string? original, object? values, CancellationToken cancellationToken) => await _auditService.LogAsync(new AuditEntry { EntityType = type, EntityId = id, Action = action, UserId = Guid.TryParse(userId, out var parsed) ? parsed : null, OriginalValues = original, NewValues = values is null ? null : JsonSerializer.Serialize(values), Timestamp = DateTime.UtcNow, CreatedBy = userId }, cancellationToken);
}
