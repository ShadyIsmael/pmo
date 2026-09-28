using System.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Identity.Domain;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Management;
using Entity = NEXUS.Modules.Portfolio.Domain.Portfolio;

namespace NEXUS.Modules.Portfolio.Application;

public sealed class PortfolioService : IPortfolioService
{
    private static readonly Guid DemoTenant = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly DbContext _db;
    private readonly IHttpContextAccessor _http;
    private readonly IManagementContextAccessor? _managementContext;

    public PortfolioService(DbContext db, IHttpContextAccessor http, IManagementContextAccessor? managementContext = null) { _db = db; _http = http; _managementContext = managementContext; }

    public async Task<Result<PortfolioDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await InScope(_db.Set<Entity>().AsNoTracking()).FirstOrDefaultAsync(x => x.Id == id, ct);
        return entity is null
            ? Result<PortfolioDto>.Failure(Error.NotFound("Portfolio not found"))
            : Result<PortfolioDto>.Success(await MapAsync(entity, true, ct));
    }

    public async Task<PagedResult<PortfolioDto>> GetPagedAsync(int page, int pageSize, string? search, string? status, CancellationToken ct = default)
    {
        page = Math.Max(page, 1); pageSize = Math.Clamp(pageSize, 1, 200);
        var query = InScope(_db.Set<Entity>().AsNoTracking());
        if (!string.IsNullOrWhiteSpace(search)) { var term = search.Trim(); query = query.Where(x => x.Name.Contains(term) || (x.Code != null && x.Code.Contains(term)) || (x.Description != null && x.Description.Contains(term))); }
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);
        var total = await query.CountAsync(ct);
        var entities = await query.OrderByDescending(x => x.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        var items = new List<PortfolioDto>(); foreach (var entity in entities) items.Add(await MapAsync(entity, false, ct));
        return new PagedResult<PortfolioDto>(items, page, pageSize, total);
    }

    public async Task<Result<PortfolioDto>> CreateAsync(SavePortfolioDto dto, string userId, CancellationToken ct = default)
    {
        var validation = Validate(dto); if (validation is not null) return Result<PortfolioDto>.Failure(validation);
        if (!await IsActiveUserAsync(dto.OwnerId, ct)) return Result<PortfolioDto>.Failure(Error.Validation("Portfolio Manager must be an active user in this tenant"));
        await using var transaction = _db.Database.BeginTransaction(IsolationLevel.Serializable);
        var next = await NextCodeAsync(ct);
        var entity = new Entity { TenantId = TenantId(), Code = next, Name = dto.Name.Trim(), Description = dto.Description?.Trim(), OwnerId = dto.OwnerId, Status = dto.Status, Currency = string.IsNullOrWhiteSpace(dto.Currency) ? "USD" : dto.Currency.Trim().ToUpperInvariant(), CreatedBy = userId };
        _db.Set<Entity>().Add(entity); await _db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        return Result<PortfolioDto>.Success(await MapAsync(entity, true, ct));
    }

    public async Task<Result<PortfolioDto>> UpdateAsync(Guid id, SavePortfolioDto dto, string userId, CancellationToken ct = default)
    {
        var validation = Validate(dto); if (validation is not null) return Result<PortfolioDto>.Failure(validation);
        if (!await IsActiveUserAsync(dto.OwnerId, ct)) return Result<PortfolioDto>.Failure(Error.Validation("Portfolio Manager must be an active user in this tenant"));
        var entity = await InScope(_db.Set<Entity>()).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null) return Result<PortfolioDto>.Failure(Error.NotFound("Portfolio not found"));
        entity.Name = dto.Name.Trim(); entity.Description = dto.Description?.Trim(); entity.OwnerId = dto.OwnerId; entity.Status = dto.Status; if (!string.IsNullOrWhiteSpace(dto.Currency)) entity.Currency = dto.Currency.Trim().ToUpperInvariant(); entity.UpdatedBy = userId;
        await _db.SaveChangesAsync(ct); return Result<PortfolioDto>.Success(await MapAsync(entity, true, ct));
    }

    public Task<Result> ArchiveAsync(Guid id, string userId, CancellationToken ct = default) => SetStatusAsync(id, "Archived", userId, false, ct);
    public Task<Result> ReactivateAsync(Guid id, string userId, CancellationToken ct = default) => SetStatusAsync(id, "Active", userId, true, ct);

    private async Task<Result> SetStatusAsync(Guid id, string status, string userId, bool requireArchived, CancellationToken ct)
    {
        var entity = await _db.Set<Entity>().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null) return Result.Failure(Error.NotFound("Portfolio not found"));
        if (requireArchived && entity.Status != "Archived") return Result.Failure(Error.Validation("Only archived portfolios can be reactivated"));
        entity.Status = status; entity.UpdatedBy = userId; await _db.SaveChangesAsync(ct); return Result.Success();
    }

    private async Task<string> NextCodeAsync(CancellationToken ct)
    {
        var codes = await _db.Set<Entity>().Where(x => x.Code != null).Select(x => x.Code!).ToListAsync(ct);
        var next = codes.Select(x => x.StartsWith("PF-", StringComparison.OrdinalIgnoreCase) && int.TryParse(x[3..], out var n) ? n : 0).DefaultIfEmpty(0).Max() + 1;
        return $"PF-{next:0000}";
    }

    private async Task<bool> IsActiveUserAsync(Guid id, CancellationToken ct) => id != Guid.Empty && await _db.Set<User>().AnyAsync(x => x.Id == id && x.IsActive, ct);

    private async Task<PortfolioDto> MapAsync(Entity entity, bool includeProjects, CancellationToken ct)
    {
        var owner = await _db.Set<User>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == entity.OwnerId, ct);
        var projects = await LoadProjectsAsync(entity.Id, ct);
        var metrics = BuildMetrics(projects, entity.Currency);
        return new PortfolioDto { Id = entity.Id, Name = entity.Name, Code = entity.Code ?? "", Description = entity.Description, Status = entity.Status ?? "Active", OwnerId = entity.OwnerId ?? Guid.Empty, OwnerName = owner?.FullName, OwnerEmail = owner?.Email, Currency = entity.Currency, Metrics = metrics, Projects = includeProjects ? projects : Array.Empty<PortfolioProjectDto>(), CreatedAt = entity.CreatedAt, UpdatedAt = entity.UpdatedAt, CreatedBy = entity.CreatedBy, UpdatedBy = entity.UpdatedBy };
    }

    private async Task<List<PortfolioProjectDto>> LoadProjectsAsync(Guid portfolioId, CancellationToken ct)
    {
        var tenant = TenantId();
        const string sql = "SELECT p.Id, p.Name, p.Code, p.Status, p.Health, p.RagStatus, p.Progress, p.StartDate, p.EndDate, p.Budget, p.ActualCost, p.Currency, p.ProgramId, COALESCE((SELECT SUM(b.ForecastAmount) FROM Budgets b WHERE b.ProjectId = p.Id AND b.TenantId = {0} AND b.IsDeleted = 0), 0) AS Forecast FROM Projects p LEFT JOIN Programs g ON p.ProgramId = g.Id WHERE p.TenantId = {0} AND p.IsDeleted = 0 AND ((p.ProgramId IS NULL AND p.PortfolioId = {1}) OR (p.ProgramId IS NOT NULL AND g.PortfolioId = {1} AND g.IsDeleted = 0))";
        return await _db.Database.SqlQueryRaw<PortfolioProjectRow>(sql, tenant, portfolioId).Select(x => new PortfolioProjectDto { Id=x.Id, Name=x.Name, Code=x.Code, Status=x.Status, Health=x.Health, RagStatus=x.RagStatus, Progress=x.Progress, StartDate=x.StartDate, EndDate=x.EndDate, Budget=x.Budget, Forecast=x.Forecast, ActualCost=x.ActualCost, Currency=x.Currency, ProgramId=x.ProgramId, IsDirectProject=!x.ProgramId.HasValue }).ToListAsync(ct);
    }

    private static PortfolioMetricsDto BuildMetrics(List<PortfolioProjectDto> projects, string baseCurrency)
    {
        var active = projects.Count(x => !new[] { "Completed", "Cancelled", "Closed" }.Contains(x.Status, StringComparer.OrdinalIgnoreCase));
        var completed = projects.Count(x => string.Equals(x.Status, "Completed", StringComparison.OrdinalIgnoreCase));
        var delayed = projects.Count(x => x.EndDate.HasValue && x.EndDate.Value.Date < DateTime.UtcNow.Date && !new[] { "Completed", "Cancelled", "Closed" }.Contains(x.Status, StringComparer.OrdinalIgnoreCase));
        var byCurrency = projects
            .GroupBy(x => x.Currency)
            .ToDictionary(
                g => g.Key,
                g => new CurrencyFinancialSummary
                {
                    Currency = g.Key,
                    ProjectCount = g.Count(),
                    Budget = g.Sum(x => x.Budget ?? 0),
                    Forecast = g.Sum(x => x.Forecast ?? 0),
                    ActualCost = g.Sum(x => x.ActualCost ?? 0)
                });
        foreach (var summary in byCurrency.Values) summary.IsBaseCurrency = string.Equals(summary.Currency, baseCurrency, StringComparison.OrdinalIgnoreCase);
        var baseSummary = byCurrency.Values.FirstOrDefault(x => x.IsBaseCurrency);
        return new PortfolioMetricsDto { ProjectCount=projects.Count, ActiveProjects=active, CompletedProjects=completed, DelayedProjects=delayed, TotalBudget=baseSummary?.Budget, TotalForecast=baseSummary?.Forecast, TotalActualCost=baseSummary?.ActualCost, AverageProgress=projects.Count == 0 ? 0 : projects.Average(x=>x.Progress), EarliestStartDate=projects.Where(x=>x.StartDate.HasValue).Select(x=>x.StartDate).Min(), LatestEndDate=projects.Where(x=>x.EndDate.HasValue).Select(x=>x.EndDate).Max(), ProjectsByStatus=projects.GroupBy(x=>x.Status).ToDictionary(x=>x.Key,x=>x.Count()), ProjectsByHealth=projects.GroupBy(x=>string.IsNullOrWhiteSpace(x.RagStatus)?x.Health:x.RagStatus).ToDictionary(x=>x.Key,x=>x.Count()), ByCurrency=byCurrency };
    }

    private static Error? Validate(SavePortfolioDto dto) => string.IsNullOrWhiteSpace(dto.Name) || dto.Name.Trim().Length > 300 ? Error.Validation("Name is required and limited to 300 characters") : dto.OwnerId == Guid.Empty ? Error.Validation("Portfolio Manager is required") : dto.Status is not ("Active" or "Archived") ? Error.Validation("Status must be Active or Archived") : !string.IsNullOrWhiteSpace(dto.Currency) && dto.Currency.Trim().Length > 10 ? Error.Validation("Currency code is limited to 10 characters") : null;
    private Guid TenantId() { var value = _http.HttpContext?.User.FindFirst("tenantId")?.Value; return Guid.TryParse(value, out var id) ? id : DemoTenant; }
    private IQueryable<Entity> InScope(IQueryable<Entity> query)
    {
        var scope = _managementContext?.Current?.Scope;
        return scope is null ? query : query.Where(x => scope.PortfolioIds.Contains(x.Id));
    }
    private sealed class PortfolioProjectRow { public Guid Id { get; set; } public string Name { get; set; } = ""; public string Code { get; set; } = ""; public string Status { get; set; } = ""; public string Health { get; set; } = ""; public string RagStatus { get; set; } = ""; public decimal Progress { get; set; } public DateTime? StartDate { get; set; } public DateTime? EndDate { get; set; } public decimal? Budget { get; set; } public decimal? Forecast { get; set; } public decimal? ActualCost { get; set; } public string Currency { get; set; } = "GBP"; public Guid? ProgramId { get; set; } }
}
