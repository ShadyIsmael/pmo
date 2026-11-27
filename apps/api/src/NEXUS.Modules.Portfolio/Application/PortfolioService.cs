using System.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Identity.Application;
using NEXUS.Modules.Identity.Domain;
using NEXUS.Modules.Organization.Domain;
using NEXUS.Modules.Portfolio.Domain;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Management;
using Entity = NEXUS.Modules.Portfolio.Domain.Portfolio;
using OrganizationEntity = NEXUS.Modules.Organization.Domain.Organization;

namespace NEXUS.Modules.Portfolio.Application;

public sealed class PortfolioService : IPortfolioService
{
    private readonly DbContext _db;
    private readonly IHttpContextAccessor _http;
    private readonly IManagementContextAccessor? _managementContext;
    private readonly IUserEffectivePermissionService? _effectivePermissions;
    private readonly IUserOrganizationScopeLookup? _organizationScopeLookup;

    public PortfolioService(
        DbContext db,
        IHttpContextAccessor http,
        IManagementContextAccessor? managementContext = null,
        IUserEffectivePermissionService? effectivePermissions = null,
        IUserOrganizationScopeLookup? organizationScopeLookup = null)
    {
        _db = db;
        _http = http;
        _managementContext = managementContext;
        _effectivePermissions = effectivePermissions;
        _organizationScopeLookup = organizationScopeLookup;
    }

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

    public async Task<PagedResult<PortfolioLookupDto>> GetProjectLookupAsync(int page, int pageSize, string? search, string? status, CancellationToken ct = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var tenantId = TenantId();
        var context = _managementContext?.Current;
        if (context is not null && context.TenantId != tenantId)
            return new PagedResult<PortfolioLookupDto>(Array.Empty<PortfolioLookupDto>(), page, pageSize, 0);

        var managementContext = _managementContext?.Current;
        IQueryable<Entity> query;
        if (_http.HttpContext?.User.HasClaim("permission", "Project.Create") == true)
        {
            if (managementContext is null || managementContext.TenantId != tenantId)
                return new PagedResult<PortfolioLookupDto>(Array.Empty<PortfolioLookupDto>(), page, pageSize, 0);

            var organizationIds = await GetAuthorizedProjectOrganizationsAsync(managementContext, ct);
            if (organizationIds.Count == 0)
                return new PagedResult<PortfolioLookupDto>(Array.Empty<PortfolioLookupDto>(), page, pageSize, 0);

            query = _db.Set<Entity>().AsNoTracking()
                .Where(item => item.TenantId == tenantId
                    && !item.IsDeleted
                    && item.OrganizationId.HasValue
                    && organizationIds.Contains(item.OrganizationId.Value));
        }
        else
        {
            query = InScope(_db.Set<Entity>().AsNoTracking().Where(item => item.TenantId == tenantId));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(item => item.Name.Contains(term) || (item.Code != null && item.Code.Contains(term)));
        }
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(item => item.Status == status);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(item => item.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new PortfolioLookupDto
            {
                Id = item.Id,
                Name = item.Name,
                Currency = item.Currency
            })
            .ToListAsync(ct);
        return new PagedResult<PortfolioLookupDto>(items, page, pageSize, total);
    }

    private async Task<HashSet<Guid>> GetAuthorizedProjectOrganizationsAsync(ManagementContext context, CancellationToken ct)
    {
        var candidateOrganizationIds = context.Scope.OrganizationIds.ToHashSet();
        if (_organizationScopeLookup is not null)
        {
            var roleOrganizationIds = await _organizationScopeLookup.GetOrganizationIdsAsync(context.UserId, context.TenantId, ct);
            candidateOrganizationIds.UnionWith(roleOrganizationIds);
        }

        if (candidateOrganizationIds.Count == 0) return [];

        return await _db.Set<OrganizationEntity>().AsNoTracking()
            .Where(organization => organization.TenantId == context.TenantId
                && !organization.IsDeleted
                && organization.IsActive
                && candidateOrganizationIds.Contains(organization.Id))
            .Select(organization => organization.Id)
            .ToHashSetAsync(ct);
    }

    public async Task<IReadOnlyList<PortfolioOrganizationLookupDto>> GetEligibleOrganizationsForCreationAsync(CancellationToken ct = default)
    {
        var tenantId = TenantId();
        var context = _managementContext?.Current;
        if (tenantId == Guid.Empty || context is null || context.TenantId != tenantId) return [];

        var organizationIds = await GetAuthorizedProjectOrganizationsAsync(context, ct);
        if (organizationIds.Count == 0) return [];

        return await _db.Set<OrganizationEntity>().AsNoTracking()
            .Where(organization => organization.TenantId == tenantId
                && !organization.IsDeleted
                && organization.IsActive
                && organizationIds.Contains(organization.Id))
            .OrderBy(organization => organization.Name)
            .ThenBy(organization => organization.Id)
            .Select(organization => new PortfolioOrganizationLookupDto
            {
                Id = organization.Id,
                Name = organization.Name
            })
            .ToListAsync(ct);
    }

    public async Task<Result<PortfolioDto>> CreateAsync(CreatePortfolioDto dto, string userId, CancellationToken ct = default)
    {
        var tenantId = TenantId();
        if (tenantId == Guid.Empty || !HasManagementScope(tenantId))
            return Result<PortfolioDto>.Failure(Error.Forbidden("A valid tenant management context is required."));
        var validation = Validate(dto); if (validation is not null) return Result<PortfolioDto>.Failure(validation);
        var managementContext = _managementContext?.Current;
        if (managementContext is null || managementContext.TenantId != tenantId)
            return Result<PortfolioDto>.Failure(Error.Forbidden("A valid Organization-scoped management context is required."));

        var eligibleOrganizations = await GetAuthorizedProjectOrganizationsAsync(managementContext, ct);
        if (eligibleOrganizations.Count == 0)
            return Result<PortfolioDto>.Failure(Error.Forbidden("The creator must belong to an active Organization to create a Portfolio."));

        var organizationId = dto.OrganizationId is { } requestedOrganizationId && requestedOrganizationId != Guid.Empty
            ? requestedOrganizationId
            : eligibleOrganizations.Count == 1 ? eligibleOrganizations.Single() : Guid.Empty;
        if (organizationId == Guid.Empty)
            return Result<PortfolioDto>.Failure(Error.Validation("Select an owning Organization for this Portfolio."));
        if (!eligibleOrganizations.Contains(organizationId))
            return Result<PortfolioDto>.Failure(Error.Validation("Select an active Organization within your authorized scope."));
        var ownerId = NormalizeOwnerId(dto.OwnerId);
        if (ownerId.HasValue && !CanAssignPortfolioManager())
            return Result<PortfolioDto>.Failure(Error.Forbidden("Handover.AdminInitiate is required to assign an initial Portfolio Manager."));
        if (ownerId.HasValue && !await IsEligiblePortfolioManagerAsync(ownerId.Value, ct)) return Result<PortfolioDto>.Failure(Error.Validation("Select an eligible active Portfolio Manager in this tenant."));
        for (var attempt = 0; attempt < 3; attempt++)
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            try
            {
                var next = await NextCodeAsync(ct);
                var requestedCurrency = NormalizeCurrency(dto.Currency);
                if (!IsValidCurrency(requestedCurrency)) return Result<PortfolioDto>.Failure(Error.Validation("Currency must be an active configured 3-letter currency code"));
                var entity = new Entity { TenantId = tenantId, OrganizationId = organizationId, Code = next, Name = dto.Name.Trim(), Description = dto.Description?.Trim(), OwnerId = ownerId, Status = dto.Status, Currency = requestedCurrency, StrategicObjective = dto.StrategicObjective?.Trim(), Priority = dto.Priority?.Trim(), Health = dto.Health?.Trim(), ConfidenceLevel = dto.ConfidenceLevel?.Trim(), CreatedBy = userId };
                _db.Set<Entity>().Add(entity); await _db.SaveChangesAsync(ct);
                if (ownerId.HasValue)
                {
                    _db.Set<PortfolioManagerAssignment>().Add(new PortfolioManagerAssignment { TenantId = entity.TenantId, PortfolioId = entity.Id, PortfolioManagerId = ownerId.Value, StartDate = DateTime.UtcNow, IsCurrent = true, CreatedBy = userId });
                    await _db.SaveChangesAsync(ct);
                }
                await transaction.CommitAsync(ct);
                return Result<PortfolioDto>.Success(await MapAsync(entity, true, ct));
            }
            catch (DbUpdateException)
            {
                await transaction.RollbackAsync(ct);
                _db.ChangeTracker.Clear();
                if (attempt == 2) throw;
            }
        }
        return Result<PortfolioDto>.Failure(Error.Conflict("Portfolio creation conflicted; retry exhausted"));
    }

    public async Task<Result<PortfolioDto>> UpdateAsync(Guid id, SavePortfolioDto dto, string userId, CancellationToken ct = default)
    {
        var validation = Validate(dto); if (validation is not null) return Result<PortfolioDto>.Failure(validation);
        var requestedCurrency = NormalizeCurrency(dto.Currency);
        if (!IsValidCurrency(requestedCurrency)) return Result<PortfolioDto>.Failure(Error.Validation("Currency must be an active configured 3-letter currency code"));
        var entity = await InScope(_db.Set<Entity>()).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null) return Result<PortfolioDto>.Failure(Error.NotFound("Portfolio not found"));
        var dtoOwnerId = NormalizeOwnerId(dto.OwnerId);
        if (entity.OwnerId.HasValue)
        {
            if (dtoOwnerId.HasValue && dtoOwnerId.Value != entity.OwnerId.Value) return Result<PortfolioDto>.Failure(Error.Validation("Portfolio Manager changes must use the governed handover process"));
        }
        else if (dtoOwnerId.HasValue)
        {
            if (!CanAssignPortfolioManager())
                return Result<PortfolioDto>.Failure(Error.Forbidden("Handover.AdminInitiate is required to assign an initial Portfolio Manager."));
            if (!await IsEligiblePortfolioManagerAsync(dtoOwnerId.Value, ct)) return Result<PortfolioDto>.Failure(Error.Validation("Select an eligible active Portfolio Manager in this tenant."));
            entity.OwnerId = dtoOwnerId.Value;
            _db.Set<PortfolioManagerAssignment>().Add(new PortfolioManagerAssignment { TenantId = entity.TenantId, PortfolioId = entity.Id, PortfolioManagerId = dtoOwnerId.Value, StartDate = DateTime.UtcNow, IsCurrent = true, CreatedBy = userId });
        }
        entity.Name = dto.Name.Trim(); entity.Description = dto.Description?.Trim(); entity.Status = dto.Status; entity.Currency = requestedCurrency; entity.StrategicObjective = dto.StrategicObjective?.Trim(); entity.Priority = dto.Priority?.Trim(); entity.Health = dto.Health?.Trim(); entity.ConfidenceLevel = dto.ConfidenceLevel?.Trim(); entity.UpdatedBy = userId;
        await _db.SaveChangesAsync(ct); return Result<PortfolioDto>.Success(await MapAsync(entity, true, ct));
    }

    public Task<Result> ArchiveAsync(Guid id, string userId, CancellationToken ct = default) => SetStatusAsync(id, "Archived", userId, false, ct);
    public Task<Result> ReactivateAsync(Guid id, string userId, CancellationToken ct = default) => SetStatusAsync(id, "Active", userId, true, ct);

    private async Task<Result> SetStatusAsync(Guid id, string status, string userId, bool requireArchived, CancellationToken ct)
    {
        var entity = await InScope(_db.Set<Entity>()).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null) return Result.Failure(Error.NotFound("Portfolio not found"));
        if (requireArchived && entity.Status != "Archived") return Result.Failure(Error.Validation("Only archived portfolios can be reactivated"));
        entity.Status = status; entity.UpdatedBy = userId; await _db.SaveChangesAsync(ct); return Result.Success();
    }

    private async Task<string> NextCodeAsync(CancellationToken ct)
    {
        var tenantId = TenantId();
        if (tenantId == Guid.Empty) return "PF-0001";
        var codes = await _db.Set<Entity>().Where(x => x.TenantId == tenantId && x.Code != null).Select(x => x.Code!).ToListAsync(ct);
        var next = codes.Select(x => x.StartsWith("PF-", StringComparison.OrdinalIgnoreCase) && int.TryParse(x[3..], out var n) ? n : 0).DefaultIfEmpty(0).Max() + 1;
        return $"PF-{next:0000}";
    }

    private async Task<bool> IsEligiblePortfolioManagerAsync(Guid id, CancellationToken ct)
    {
        var tenantId = TenantId();
        if (tenantId == Guid.Empty || id == Guid.Empty || _effectivePermissions is null) return false;

        var hasApprovedRole = await _db.Set<User>().AnyAsync(user =>
            user.Id == id && user.TenantId == tenantId && user.IsActive && !user.IsDeleted
            && user.UserRoles.Any(userRole => userRole.TenantId == tenantId && !userRole.IsDeleted
                && userRole.Role.TenantId == tenantId && !userRole.Role.IsDeleted
                && userRole.Role.Category == "Management"), ct);
        if (!hasApprovedRole) return false;

        var permissions = await _effectivePermissions.GetEffectivePermissionsAsync(id, tenantId, ct);
        return permissions.Contains("Handover.Approve") && permissions.Contains("Portfolio.View");
    }

    private async Task<PortfolioDto> MapAsync(Entity entity, bool includeProjects, CancellationToken ct)
    {
        var owner = await _db.Set<User>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == entity.OwnerId && x.TenantId == entity.TenantId, ct);
        var organizationName = entity.OrganizationId.HasValue
            ? await _db.Set<OrganizationEntity>().AsNoTracking()
                .Where(organization => organization.Id == entity.OrganizationId.Value && organization.TenantId == entity.TenantId)
                .Select(organization => organization.Name)
                .FirstOrDefaultAsync(ct)
            : null;
        var projects = await LoadProjectsAsync(entity.Id, ct);
        var metrics = BuildMetrics(projects);
        return new PortfolioDto { Id = entity.Id, OrganizationId = entity.OrganizationId, OrganizationName = organizationName, Name = entity.Name, Code = entity.Code ?? "", Description = entity.Description, Status = entity.Status ?? "Active", OwnerId = entity.OwnerId ?? Guid.Empty, OwnerName = owner?.FullName, OwnerEmail = owner?.Email, Currency = entity.Currency, StartDate = entity.StartDate, EndDate = entity.EndDate, StrategicObjective = entity.StrategicObjective, Priority = entity.Priority, Health = entity.Health, ConfidenceLevel = entity.ConfidenceLevel, Metrics = metrics, Projects = includeProjects ? projects : Array.Empty<PortfolioProjectDto>(), CreatedAt = entity.CreatedAt, UpdatedAt = entity.UpdatedAt, CreatedBy = entity.CreatedBy, UpdatedBy = entity.UpdatedBy };
    }

    private async Task<List<PortfolioProjectDto>> LoadProjectsAsync(Guid portfolioId, CancellationToken ct)
    {
        var tenant = TenantId();
        const string sql = "SELECT p.Id, p.Name, p.Code, p.Status, p.Health, p.RagStatus, p.Progress, p.StartDate, p.EndDate, p.Currency, p.ProgramId FROM Projects p LEFT JOIN Programs g ON p.ProgramId = g.Id AND g.TenantId = p.TenantId WHERE p.TenantId = {0} AND p.IsDeleted = 0 AND ((p.ProgramId IS NULL AND p.PortfolioId = {1}) OR (p.ProgramId IS NOT NULL AND g.PortfolioId = {1} AND g.IsDeleted = 0))";
        return await _db.Database.SqlQueryRaw<PortfolioProjectRow>(sql, tenant, portfolioId).Select(x => new PortfolioProjectDto { Id=x.Id, Name=x.Name, Code=x.Code, Status=x.Status, Health=x.Health, RagStatus=x.RagStatus, Progress=x.Progress, StartDate=x.StartDate, EndDate=x.EndDate, Currency=x.Currency, ProgramId=x.ProgramId, IsDirectProject=!x.ProgramId.HasValue }).ToListAsync(ct);
    }

    private static PortfolioMetricsDto BuildMetrics(List<PortfolioProjectDto> projects)
    {
        var active = projects.Count(x => !new[] { "Completed", "Cancelled", "Closed" }.Contains(x.Status, StringComparer.OrdinalIgnoreCase));
        var completed = projects.Count(x => string.Equals(x.Status, "Completed", StringComparison.OrdinalIgnoreCase));
        var delayed = projects.Count(x => x.EndDate.HasValue && x.EndDate.Value.Date < DateTime.UtcNow.Date && !new[] { "Completed", "Cancelled", "Closed" }.Contains(x.Status, StringComparer.OrdinalIgnoreCase));
        return new PortfolioMetricsDto { ProjectCount=projects.Count, ActiveProjects=active, CompletedProjects=completed, DelayedProjects=delayed, AverageProgress=projects.Count == 0 ? 0 : projects.Average(x=>x.Progress), EarliestStartDate=projects.Where(x=>x.StartDate.HasValue).Select(x=>x.StartDate).Min(), LatestEndDate=projects.Where(x=>x.EndDate.HasValue).Select(x=>x.EndDate).Max(), ProjectsByStatus=projects.GroupBy(x=>x.Status).ToDictionary(x=>x.Key,x=>x.Count()), ProjectsByHealth=projects.GroupBy(x=>string.IsNullOrWhiteSpace(x.RagStatus)?x.Health:x.RagStatus).ToDictionary(x=>x.Key,x=>x.Count()) };
    }

    private static Error? Validate(SavePortfolioDto dto) => string.IsNullOrWhiteSpace(dto.Name) || dto.Name.Trim().Length > 300 ? Error.Validation("Name is required and limited to 300 characters")         : dto.Status is not ("Active" or "Archived") ? Error.Validation("Status must be Active or Archived") : !IsValidCurrency(NormalizeCurrency(dto.Currency)) ? Error.Validation("Currency must be a valid 3-letter ISO code") : null;
    private static Guid? NormalizeOwnerId(Guid? ownerId) => ownerId.HasValue && ownerId.Value != Guid.Empty ? ownerId.Value : null;
    private static string NormalizeCurrency(string? value) => (value ?? string.Empty).Trim().ToUpperInvariant();
    private static bool IsValidCurrency(string? value) => !string.IsNullOrWhiteSpace(value) && value.Length == 3 && value.All(character => character is >= 'A' and <= 'Z');
    private bool CanAssignPortfolioManager() =>
        _http.HttpContext?.User.HasClaim("permission", "Handover.AdminInitiate") == true;
    private Guid TenantId()
    {
        var httpContext = _http.HttpContext;
        if (httpContext is not null)
        {
            if (httpContext.User.Identity?.IsAuthenticated != true) return Guid.Empty;
            var claim = httpContext.User.FindFirst("tenantId")?.Value;
            return Guid.TryParse(claim, out var id) && id != Guid.Empty ? id : Guid.Empty;
        }

        return TrustedTenantExecutionContext.CurrentTenantId ?? Guid.Empty;
    }

    private bool HasManagementScope(Guid tenantId)
    {
        if (_http.HttpContext is null) return TrustedTenantExecutionContext.CurrentTenantId == tenantId;
        var context = _managementContext?.Current;
        return context is not null && context.TenantId == tenantId;
    }

    private IQueryable<Entity> InScope(IQueryable<Entity> query)
    {
        var tenantId = TenantId();
        if (tenantId == Guid.Empty) return query.Where(_ => false);

        query = query.Where(x => x.TenantId == tenantId);
        if (_http.HttpContext is null)
            return TrustedTenantExecutionContext.CurrentTenantId == tenantId ? query : query.Where(_ => false);

        var context = _managementContext?.Current;
        if (context is null || context.TenantId != tenantId) return query.Where(_ => false);

        if (context.CanReadAllTenantPortfolioProgramData || context.HasTenantAdministratorPortfolioWriteScope)
            return query;

        return query.Where(x => context.Scope.PortfolioIds.Contains(x.Id));
    }
    private sealed class PortfolioProjectRow { public Guid Id { get; set; } public string Name { get; set; } = ""; public string Code { get; set; } = ""; public string Status { get; set; } = ""; public string Health { get; set; } = ""; public string RagStatus { get; set; } = ""; public decimal Progress { get; set; } public DateTime? StartDate { get; set; } public DateTime? EndDate { get; set; } public string Currency { get; set; } = ""; public Guid? ProgramId { get; set; } }
}
