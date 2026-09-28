using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Identity.Domain;
using NEXUS.Modules.Portfolio.Domain;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Management;
using Entity = NEXUS.Modules.Portfolio.Domain.Program;

namespace NEXUS.Modules.Portfolio.Application;

public sealed class ProgramService : IProgramService
{
    private static readonly Guid DemoTenant = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly DbContext _db;
    private readonly IHttpContextAccessor _http;
    private readonly IManagementContextAccessor? _managementContext;

    public ProgramService(DbContext db, IHttpContextAccessor http, IManagementContextAccessor? managementContext = null) { _db = db; _http = http; _managementContext = managementContext; }

    public async Task<Result<ProgramDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await InScope(_db.Set<Entity>().AsNoTracking()).FirstOrDefaultAsync(x => x.Id == id, ct);
        return entity is null
            ? Result<ProgramDto>.Failure(Error.NotFound("Program not found"))
            : Result<ProgramDto>.Success(await MapAsync(entity, ct));
    }

    public async Task<PagedResult<ProgramDto>> GetPagedAsync(int page, int pageSize, string? search, string? status, Guid? portfolioId, CancellationToken ct = default)
    {
        page = Math.Max(page, 1); pageSize = Math.Clamp(pageSize, 1, 200);
        var query = InScope(_db.Set<Entity>().AsNoTracking());
        if (!string.IsNullOrWhiteSpace(search)) { var term = search.Trim(); query = query.Where(x => x.Name.Contains(term) || (x.Code != null && x.Code.Contains(term)) || (x.Description != null && x.Description.Contains(term))); }
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);
        if (portfolioId.HasValue) query = query.Where(x => x.PortfolioId == portfolioId.Value);
        var total = await query.CountAsync(ct);
        var entities = await query.OrderByDescending(x => x.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        var items = new List<ProgramDto>();
        foreach (var entity in entities)
        {
            try { items.Add(await MapAsync(entity, ct)); }
            catch { items.Add(new ProgramDto { Id = entity.Id, Name = entity.Name, Code = entity.Code, Status = entity.Status, PortfolioId = entity.PortfolioId }); }
        }
        return new PagedResult<ProgramDto>(items, page, pageSize, total);
    }

    public async Task<Result<ProgramDto>> CreateAsync(SaveProgramDto dto, string userId, CancellationToken ct = default)
    {
        var validation = Validate(dto); if (validation is not null) return Result<ProgramDto>.Failure(validation);
        if (!await IsValidPortfolioAsync(dto.PortfolioId, ct)) return Result<ProgramDto>.Failure(Error.Validation("Invalid Portfolio"));
        if (dto.ProgramManagerId.HasValue && !await IsActiveUserAsync(dto.ProgramManagerId.Value, ct)) return Result<ProgramDto>.Failure(Error.Validation("Program Manager must be an active user"));
        if (await IsDuplicateCodeAsync(dto.Name, null, ct)) return Result<ProgramDto>.Failure(Error.Validation("A program with this name already exists"));

        var code = await NextCodeAsync(ct);

        var portfolio = await _db.Set<Portfolio.Domain.Portfolio>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == dto.PortfolioId, ct);
        var currency = !string.IsNullOrWhiteSpace(dto.Currency)
            ? dto.Currency.Trim().ToUpperInvariant()
            : (portfolio?.Currency ?? "USD");
        if (!await IsValidCurrencyAsync(currency, ct)) return Result<ProgramDto>.Failure(Error.Validation("Currency must be an active configured currency"));

        var entity = new Entity
        {
            TenantId = TenantId(),
            Name = dto.Name.Trim(),
            Code = code,
            Description = dto.Description?.Trim(),
            PortfolioId = dto.PortfolioId,
            ProgramManagerId = dto.ProgramManagerId,
            Status = dto.Status,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            Currency = currency,
            Budget = dto.Budget,
            Health = dto.Health,
            CreatedBy = userId
        };
        _db.Set<Entity>().Add(entity);
        await _db.SaveChangesAsync(ct);

        if (dto.ProgramManagerId.HasValue)
        {
            var assignment = new ProgramManagerAssignment
            {
                TenantId = TenantId(),
                ProgramId = entity.Id,
                ProgramManagerId = dto.ProgramManagerId.Value,
                StartDate = DateTime.UtcNow,
                IsCurrent = true,
                CreatedBy = userId
            };
            _db.Set<ProgramManagerAssignment>().Add(assignment);
            await _db.SaveChangesAsync(ct);
        }

        return Result<ProgramDto>.Success(await MapAsync(entity, ct));
    }

    public async Task<Result<ProgramDto>> UpdateAsync(Guid id, SaveProgramDto dto, string userId, CancellationToken ct = default)
    {
        var validation = Validate(dto); if (validation is not null) return Result<ProgramDto>.Failure(validation);
        var entity = await InScope(_db.Set<Entity>()).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null) return Result<ProgramDto>.Failure(Error.NotFound("Program not found"));
        if (!await IsValidPortfolioAsync(dto.PortfolioId, ct)) return Result<ProgramDto>.Failure(Error.Validation("Invalid Portfolio"));
        if (entity.PortfolioId != dto.PortfolioId && await HasProjectsAsync(id, ct)) return Result<ProgramDto>.Failure(Error.Validation("A Program with Projects cannot be moved to another Portfolio"));
        if (dto.ProgramManagerId.HasValue && !await IsActiveUserAsync(dto.ProgramManagerId.Value, ct)) return Result<ProgramDto>.Failure(Error.Validation("Program Manager must be an active user"));

        if (dto.RowVersion.Length > 0) _db.Entry(entity).Property(x => x.RowVersion).OriginalValue = dto.RowVersion;

        entity.Name = dto.Name.Trim();
        entity.Description = dto.Description?.Trim();
        entity.PortfolioId = dto.PortfolioId;
        entity.Status = dto.Status;
        entity.StartDate = dto.StartDate;
        entity.EndDate = dto.EndDate;
        if (!string.IsNullOrWhiteSpace(dto.Currency))
        {
            var currency = dto.Currency.Trim().ToUpperInvariant();
            if (!await IsValidCurrencyAsync(currency, ct)) return Result<ProgramDto>.Failure(Error.Validation("Currency must be an active configured currency"));
            entity.Currency = currency;
        }
        entity.Budget = dto.Budget;
        entity.Health = dto.Health;
        entity.UpdatedBy = userId;

        if (entity.ProgramManagerId != dto.ProgramManagerId)
        {
            var previousManagerId = entity.ProgramManagerId;
            entity.ProgramManagerId = dto.ProgramManagerId;

            var currentAssignment = await _db.Set<ProgramManagerAssignment>()
                .FirstOrDefaultAsync(a => a.ProgramId == id && a.IsCurrent && !a.IsDeleted, ct);
            if (currentAssignment is not null)
            {
                currentAssignment.IsCurrent = false;
                currentAssignment.EndDate = DateTime.UtcNow;
                currentAssignment.UpdatedBy = userId;
            }

            if (dto.ProgramManagerId.HasValue)
            {
                var newAssignment = new ProgramManagerAssignment
                {
                    TenantId = TenantId(),
                    ProgramId = id,
                    ProgramManagerId = dto.ProgramManagerId.Value,
                    StartDate = DateTime.UtcNow,
                    IsCurrent = true,
                    HandoverFromProgramManagerId = previousManagerId,
                    CreatedBy = userId
                };
                _db.Set<ProgramManagerAssignment>().Add(newAssignment);
            }
        }

        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return Result<ProgramDto>.Failure(Error.Conflict("Program was modified by another user")); }

        return Result<ProgramDto>.Success(await MapAsync(entity, ct));
    }

    public Task<Result> ArchiveAsync(Guid id, string userId, CancellationToken ct = default) => SetStatusAsync(id, "Archived", userId, false, ct);
    public Task<Result> ReactivateAsync(Guid id, string userId, CancellationToken ct = default) => SetStatusAsync(id, "Active", userId, true, ct);

    private async Task<Result> SetStatusAsync(Guid id, string status, string userId, bool requireArchived, CancellationToken ct)
    {
        var entity = await _db.Set<Entity>().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null) return Result.Failure(Error.NotFound("Program not found"));
        if (requireArchived && entity.Status != "Archived") return Result.Failure(Error.Validation("Only archived programs can be reactivated"));
        entity.Status = status; entity.UpdatedBy = userId; await _db.SaveChangesAsync(ct); return Result.Success();
    }

    private async Task<string> NextCodeAsync(CancellationToken ct)
    {
        var codes = await _db.Set<Entity>().Where(x => x.Code != null).Select(x => x.Code!).ToListAsync(ct);
        var next = codes.Select(x => x.StartsWith("PGM-", StringComparison.OrdinalIgnoreCase) && int.TryParse(x[4..], out var n) ? n : 0).DefaultIfEmpty(0).Max() + 1;
        return $"PGM-{next:0000}";
    }

    private async Task<bool> IsValidPortfolioAsync(Guid id, CancellationToken ct) => id != Guid.Empty && await _db.Set<Portfolio.Domain.Portfolio>().AnyAsync(x => x.Id == id && x.TenantId == TenantId() && !x.IsDeleted, ct);
    private async Task<bool> HasProjectsAsync(Guid programId, CancellationToken ct)
    {
        var connection = _db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync(ct);
        using var command = connection.CreateCommand(); command.CommandText = "SELECT COUNT(*) FROM Projects WHERE ProgramId = @pid AND IsDeleted = 0";
        var parameter = command.CreateParameter(); parameter.ParameterName = "@pid"; parameter.Value = programId; command.Parameters.Add(parameter);
        return Convert.ToInt32(await command.ExecuteScalarAsync(ct)) > 0;
    }
    private async Task<bool> IsValidCurrencyAsync(string code, CancellationToken ct)
    {
        var connection = _db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync(ct);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Currencies WHERE TenantId = @tenant AND Code = @code AND IsDeleted = 0";
        var parameter = command.CreateParameter(); parameter.ParameterName = "@code"; parameter.Value = code; command.Parameters.Add(parameter);
        var tenant = command.CreateParameter(); tenant.ParameterName = "@tenant"; tenant.Value = TenantId(); command.Parameters.Add(tenant);
        return Convert.ToInt32(await command.ExecuteScalarAsync(ct)) > 0;
    }
    private async Task<bool> IsActiveUserAsync(Guid id, CancellationToken ct) => id != Guid.Empty && await _db.Set<User>().AnyAsync(x => x.Id == id && x.IsActive, ct);
    private async Task<bool> IsDuplicateCodeAsync(string name, Guid? excludeId, CancellationToken ct)
    {
        var normalizedName = name.Trim().ToLowerInvariant();
        return await _db.Set<Entity>().AnyAsync(x => x.Name.ToLower() == normalizedName && !x.IsDeleted && (!excludeId.HasValue || x.Id != excludeId.Value), ct);
    }

    private async Task<ProgramDto> MapAsync(Entity entity, CancellationToken ct)
    {
        var portfolio = entity.PortfolioId != Guid.Empty ? await _db.Set<Portfolio.Domain.Portfolio>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == entity.PortfolioId, ct) : null;
        var effectiveCurrency = string.IsNullOrWhiteSpace(entity.Currency) ? (portfolio?.Currency ?? "") : entity.Currency;
        User? manager = null;
        if (entity.ProgramManagerId.HasValue) manager = await _db.Set<User>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == entity.ProgramManagerId.Value, ct);
        var projectCount = 0;
        try
        {
            var cmd = _db.Database.GetDbConnection();
            if (cmd.State != System.Data.ConnectionState.Open) await cmd.OpenAsync(ct);
            using var command = cmd.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM Projects WHERE ProgramId = @pid AND IsDeleted = 0";
            var pidParam = command.CreateParameter();
            pidParam.ParameterName = "@pid";
            pidParam.Value = entity.Id;
            command.Parameters.Add(pidParam);
            var result = await command.ExecuteScalarAsync(ct);
            projectCount = Convert.ToInt32(result);
        }
        catch { /* projects table may not have ProgramId column in all environments */ }

        const string projectsSql = "SELECT p.Id, p.Name, p.Code, p.Status, p.Currency, p.Budget, p.ActualCost, COALESCE((SELECT SUM(b.ForecastAmount) FROM Budgets b WHERE b.ProjectId = p.Id AND b.TenantId = {0} AND b.IsDeleted = 0), 0) AS Forecast FROM Projects p WHERE p.TenantId = {0} AND p.ProgramId = {1} AND p.IsDeleted = 0";
        var projects = await _db.Database.SqlQueryRaw<ProgramProjectRow>(projectsSql, entity.TenantId, entity.Id).Select(x => new ProgramProjectDto { Id = x.Id, Name = x.Name, Code = x.Code, Status = x.Status, Currency = x.Currency, Budget = x.Budget, Forecast = x.Forecast, ActualCost = x.ActualCost }).ToListAsync(ct);
        var byCurrency = projects.GroupBy(x => x.Currency, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => new ProgramCurrencyFinancialSummary
            {
                Currency = g.Key,
                IsBaseCurrency = string.Equals(g.Key, effectiveCurrency, StringComparison.OrdinalIgnoreCase),
                ProjectCount = g.Count(),
                Budget = g.Sum(x => x.Budget ?? 0),
                Forecast = g.Sum(x => x.Forecast ?? 0),
                ActualCost = g.Sum(x => x.ActualCost ?? 0)
            }, StringComparer.OrdinalIgnoreCase);
        return new ProgramDto
        {
            Id = entity.Id,
            Name = entity.Name,
            Code = entity.Code,
            Description = entity.Description,
            PortfolioId = entity.PortfolioId,
            PortfolioName = portfolio?.Name,
            Currency = effectiveCurrency,
            Status = entity.Status,
            ProgramManagerId = entity.ProgramManagerId,
            ProgramManagerName = manager != null ? $"{manager.FirstName} {manager.LastName}" : null,
            ProgramManagerEmail = manager?.Email,
            StartDate = entity.StartDate,
            EndDate = entity.EndDate,
            Budget = entity.Budget,
            ActualSpend = entity.ActualSpend,
            Health = entity.Health,
            ProjectCount = projectCount,
            Projects = projects,
            ByCurrency = byCurrency,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedBy = entity.UpdatedBy,
            RowVersion = entity.RowVersion
        };
    }

    private static Error? Validate(SaveProgramDto dto) =>
        string.IsNullOrWhiteSpace(dto.Name) || dto.Name.Trim().Length > 300 ? Error.Validation("Name is required and limited to 300 characters")
        : dto.PortfolioId == Guid.Empty ? Error.Validation("Portfolio is required")
        : dto.StartDate.HasValue && dto.EndDate.HasValue && dto.EndDate.Value < dto.StartDate.Value ? Error.Validation("End Date must not be before Start Date")
        : dto.Status is not ("Active" or "Planning" or "On Hold" or "Completed" or "Archived") ? Error.Validation("Status must be Active, Planning, On Hold, Completed, or Archived")
        : null;

    private Guid TenantId() { var value = _http.HttpContext?.User.FindFirst("tenantId")?.Value; return Guid.TryParse(value, out var id) ? id : DemoTenant; }
    private IQueryable<Entity> InScope(IQueryable<Entity> query)
    {
        var scope = _managementContext?.Current?.Scope;
        return scope is null ? query : query.Where(x => scope.ProgramIds.Contains(x.Id));
    }
    private sealed class ProgramProjectRow { public Guid Id { get; set; } public string Name { get; set; } = ""; public string Code { get; set; } = ""; public string Status { get; set; } = ""; public string Currency { get; set; } = ""; public decimal? Budget { get; set; } public decimal? Forecast { get; set; } public decimal? ActualCost { get; set; } }
}
