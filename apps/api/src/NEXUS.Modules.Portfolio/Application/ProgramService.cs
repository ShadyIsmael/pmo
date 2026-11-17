using System.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Identity.Application;
using NEXUS.Modules.Identity.Domain;
using NEXUS.Modules.Portfolio.Domain;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Management;
using Entity = NEXUS.Modules.Portfolio.Domain.Program;

namespace NEXUS.Modules.Portfolio.Application;

public sealed class ProgramService : IProgramService
{
    private const string ActivePortfolioStatus = "Active";

    private readonly DbContext _db;
    private readonly IHttpContextAccessor _http;
    private readonly IManagementContextAccessor? _managementContext;
    private readonly IManagementScopeResolver? _managementScopeResolver;
    private readonly IUserEffectivePermissionService? _effectivePermissions;

    public ProgramService(DbContext db, IHttpContextAccessor http, IManagementContextAccessor? managementContext = null, IManagementScopeResolver? managementScopeResolver = null, IUserEffectivePermissionService? effectivePermissions = null)
    {
        _db = db;
        _http = http;
        _managementContext = managementContext;
        _managementScopeResolver = managementScopeResolver;
        _effectivePermissions = effectivePermissions;
    }

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

    public async Task<PagedResult<ProgramLookupDto>> GetProjectLookupAsync(int page, int pageSize, string? search, string? status, Guid? portfolioId, CancellationToken ct = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var tenantId = TenantId();
        var context = _managementContext?.Current;
        if (context is not null && context.TenantId != tenantId)
            return new PagedResult<ProgramLookupDto>(Array.Empty<ProgramLookupDto>(), page, pageSize, 0);

        var query = InScope(_db.Set<Entity>().AsNoTracking().Where(item => item.TenantId == tenantId));
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(item => item.Name.Contains(term) || (item.Code != null && item.Code.Contains(term)));
        }
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(item => item.Status == status);
        if (portfolioId.HasValue) query = query.Where(item => item.PortfolioId == portfolioId.Value);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(item => item.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new ProgramLookupDto
            {
                Id = item.Id,
                Name = item.Name,
                PortfolioId = item.PortfolioId,
                Currency = item.Currency
            })
            .ToListAsync(ct);
        return new PagedResult<ProgramLookupDto>(items, page, pageSize, total);
    }

    public async Task<Result<IReadOnlyList<ProgramPortfolioLookupDto>>> GetEligiblePortfoliosForCreationAsync(CancellationToken ct = default)
    {
        var context = _managementContext?.Current;
        if (context is null)
            return Result<IReadOnlyList<ProgramPortfolioLookupDto>>.Failure(Error.Unauthorized("A valid tenant management context is required."));
        if (context.EffectiveReadOnly)
            return Result<IReadOnlyList<ProgramPortfolioLookupDto>>.Failure(Error.Forbidden("Program creation is unavailable in a read-only management context."));
        if (_managementScopeResolver is null)
            return Result<IReadOnlyList<ProgramPortfolioLookupDto>>.Failure(Error.Forbidden("Portfolio assignment scope is unavailable."));

        var creationScope = await GetProgramPortfolioAssignmentScopeAsync(context, ct);
        if (creationScope is null)
            return Result<IReadOnlyList<ProgramPortfolioLookupDto>>.Failure(Error.Forbidden("Portfolio creation scope is unavailable."));

        var permissions = await GetEffectivePermissionsForContextAsync(context, context.UserId, ct);
        if (!permissions.Contains("Program.Create"))
            return Result<IReadOnlyList<ProgramPortfolioLookupDto>>.Failure(Error.Forbidden(
                "Program creation permission is required."));
        var hasCreateAcross = permissions.Contains("Program.CreateAcrossPortfolios");

        if (creationScope.NativePortfolioIds.Count == 0 && !hasCreateAcross)
            return Result<IReadOnlyList<ProgramPortfolioLookupDto>>.Success(Array.Empty<ProgramPortfolioLookupDto>());

        var query = _db.Set<Portfolio.Domain.Portfolio>()
            .AsNoTracking()
            .Where(item => item.TenantId == context.TenantId && !item.IsDeleted && item.Status == ActivePortfolioStatus);
        query = query.Where(item => creationScope.NativePortfolioIds.Contains(item.Id)
            || (hasCreateAcross && item.OrganizationId.HasValue
                && creationScope.OrganizationIds.Contains(item.OrganizationId.Value)));

        var portfolios = await query
            .OrderBy(item => item.Name)
            .ThenBy(item => item.Code)
            .Select(item => new ProgramPortfolioLookupDto
            {
                Id = item.Id,
                Name = item.Name,
                Code = item.Code,
                Status = ActivePortfolioStatus,
                Currency = item.Currency
            })
            .ToListAsync(ct);

        return Result<IReadOnlyList<ProgramPortfolioLookupDto>>.Success(portfolios);
    }

    public async Task<Result<IReadOnlyList<ProgramPortfolioLookupDto>>> GetEligiblePortfoliosForEditingAsync(CancellationToken ct = default)
    {
        var context = _managementContext?.Current;
        if (context is null)
            return Result<IReadOnlyList<ProgramPortfolioLookupDto>>.Failure(Error.Unauthorized("A valid tenant management context is required."));
        if (context.EffectiveReadOnly)
            return Result<IReadOnlyList<ProgramPortfolioLookupDto>>.Failure(Error.Forbidden("Program editing is unavailable in a read-only management context."));
        if (_managementScopeResolver is null)
            return Result<IReadOnlyList<ProgramPortfolioLookupDto>>.Failure(Error.Forbidden("Portfolio assignment scope is unavailable."));

        var permissions = await GetEffectivePermissionsForContextAsync(context, context.UserId, ct);
        if (!permissions.Contains("Program.Edit"))
            return Result<IReadOnlyList<ProgramPortfolioLookupDto>>.Failure(Error.Forbidden("Program edit permission is required."));

        var assignmentScope = await GetProgramPortfolioAssignmentScopeAsync(context, ct);
        if (assignmentScope is null)
            return Result<IReadOnlyList<ProgramPortfolioLookupDto>>.Failure(Error.Forbidden("Portfolio assignment scope is unavailable."));
        if (assignmentScope.NativePortfolioIds.Count == 0)
            return Result<IReadOnlyList<ProgramPortfolioLookupDto>>.Success(Array.Empty<ProgramPortfolioLookupDto>());

        var portfolios = await _db.Set<Portfolio.Domain.Portfolio>()
            .AsNoTracking()
            .Where(item => item.TenantId == context.TenantId
                && !item.IsDeleted
                && item.Status == ActivePortfolioStatus
                && assignmentScope.NativePortfolioIds.Contains(item.Id))
            .OrderBy(item => item.Name)
            .ThenBy(item => item.Code)
            .Select(item => new ProgramPortfolioLookupDto
            {
                Id = item.Id,
                Name = item.Name,
                Code = item.Code,
                Status = ActivePortfolioStatus,
                Currency = item.Currency
            })
            .ToListAsync(ct);

        return Result<IReadOnlyList<ProgramPortfolioLookupDto>>.Success(portfolios);
    }

    public async Task<Result<ProgramDto>> CreateAsync(SaveProgramDto dto, string userId, CancellationToken ct = default)
    {
        if (!IsTrustedActor(userId))
            return Result<ProgramDto>.Failure(Error.Unauthorized("A valid authenticated actor and management context are required."));

        var tenantId = TenantId();
        if (tenantId == Guid.Empty || !HasManagementScope(tenantId))
            return Result<ProgramDto>.Failure(Error.Forbidden("A valid tenant management context is required."));
        if (dto.ProgramManagerId.HasValue && !CanAssignInitialProgramManager())
            return Result<ProgramDto>.Failure(Error.Forbidden("Handover.AdminInitiate is required to assign an initial Program Manager."));
        var validation = Validate(dto); if (validation is not null) return Result<ProgramDto>.Failure(validation);
        if (!await CanCreateProgramInPortfolioAsync(dto.PortfolioId, ct)) return Result<ProgramDto>.Failure(Error.NotFound("Portfolio not found"));
        if (!await IsValidPortfolioAsync(dto.PortfolioId, ct)) return Result<ProgramDto>.Failure(Error.Validation("Invalid Portfolio"));
        if (!await IsActivePortfolioAsync(dto.PortfolioId, ct)) return Result<ProgramDto>.Failure(Error.Validation("A Program must be assigned to an active Portfolio"));
        if (dto.ProgramManagerId.HasValue && !await IsEligibleProgramManagerAsync(dto.ProgramManagerId.Value, dto.PortfolioId, ct)) return Result<ProgramDto>.Failure(Error.Validation("Select an eligible active Program Manager in this tenant."));
        if (await IsDuplicateCodeAsync(dto.Name, null, ct)) return Result<ProgramDto>.Failure(Error.Validation("A program with this name already exists"));

        var code = await NextCodeAsync(ct);

        var portfolio = await _db.Set<Portfolio.Domain.Portfolio>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == dto.PortfolioId, ct);
        var currency = !string.IsNullOrWhiteSpace(dto.Currency)
            ? dto.Currency.Trim().ToUpperInvariant()
            : (portfolio?.Currency ?? string.Empty);
        if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3 || currency.Any(character => character is < 'A' or > 'Z') || !await IsValidCurrencyAsync(currency, ct)) return Result<ProgramDto>.Failure(Error.Validation("Currency must be an active configured 3-letter currency code"));

        for (var attempt = 0; attempt < 3; attempt++)
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            try
            {
                var attemptCode = attempt == 0 ? code : await NextCodeAsync(ct);
                var entity = new Entity
                {
                    TenantId = tenantId,
                    Name = dto.Name.Trim(),
                    Code = attemptCode,
                    Description = dto.Description?.Trim(),
                    PortfolioId = dto.PortfolioId,
                    ProgramManagerId = dto.ProgramManagerId,
                    Status = dto.Status,
                    Currency = currency,
                    CreatedBy = userId
                };
                _db.Set<Entity>().Add(entity);
                await _db.SaveChangesAsync(ct);

                if (dto.ProgramManagerId.HasValue)
                {
                    var assignment = new ProgramManagerAssignment
                    {
                        TenantId = tenantId,
                        ProgramId = entity.Id,
                        ProgramManagerId = dto.ProgramManagerId.Value,
                        StartDate = DateTime.UtcNow,
                        IsCurrent = true,
                        CreatedBy = userId
                    };
                    _db.Set<ProgramManagerAssignment>().Add(assignment);
                    await _db.SaveChangesAsync(ct);
                }

                await transaction.CommitAsync(ct);
                return Result<ProgramDto>.Success(await MapAsync(entity, ct));
            }
            catch (DbUpdateException)
            {
                await transaction.RollbackAsync(ct);
                _db.ChangeTracker.Clear();
                if (attempt == 2) throw;
            }
        }
        return Result<ProgramDto>.Failure(Error.Conflict("Program creation conflicted; retry exhausted"));
    }

    public async Task<Result<ProgramDto>> UpdateAsync(Guid id, SaveProgramDto dto, string userId, CancellationToken ct = default)
    {
        if (!IsTrustedActor(userId))
            return Result<ProgramDto>.Failure(Error.Unauthorized("A valid authenticated actor and management context are required."));

        var validation = Validate(dto); if (validation is not null) return Result<ProgramDto>.Failure(validation);
        var entity = await InScope(_db.Set<Entity>()).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null) return Result<ProgramDto>.Failure(Error.NotFound("Program not found"));
        if (entity.ProgramManagerId.HasValue && entity.ProgramManagerId != dto.ProgramManagerId)
            return Result<ProgramDto>.Failure(Error.Validation("Program Manager changes must use the governed handover process"));
        if (!entity.ProgramManagerId.HasValue && dto.ProgramManagerId.HasValue && !CanAssignInitialProgramManager())
            return Result<ProgramDto>.Failure(Error.Forbidden("Handover.AdminInitiate is required to assign an initial Program Manager."));
        if (!await IsValidPortfolioAsync(dto.PortfolioId, ct)) return Result<ProgramDto>.Failure(Error.Validation("Invalid Portfolio"));
        if (entity.PortfolioId != dto.PortfolioId)
        {
            if (!await CanAssignPortfolioAsync(dto.PortfolioId, ct)) return Result<ProgramDto>.Failure(Error.NotFound("Portfolio not found"));
            if (!await IsActivePortfolioAsync(dto.PortfolioId, ct)) return Result<ProgramDto>.Failure(Error.Validation("A Program can only be moved to an active Portfolio"));
            if (await HasProjectsAsync(id, ct)) return Result<ProgramDto>.Failure(Error.Validation("A Program with Projects cannot be moved to another Portfolio"));
        }
        if (dto.ProgramManagerId.HasValue && dto.ProgramManagerId != entity.ProgramManagerId && !await IsEligibleProgramManagerAsync(dto.ProgramManagerId.Value, entity.PortfolioId, ct)) return Result<ProgramDto>.Failure(Error.Validation("Select an eligible active Program Manager in this tenant."));

        if (dto.RowVersion.Length > 0) _db.Entry(entity).Property(x => x.RowVersion).OriginalValue = dto.RowVersion;

        entity.Name = dto.Name.Trim();
        entity.Description = dto.Description?.Trim();
        entity.PortfolioId = dto.PortfolioId;
        entity.Status = dto.Status;
        if (!string.IsNullOrWhiteSpace(dto.Currency))
        {
            var currency = dto.Currency.Trim().ToUpperInvariant();
            if (!await IsValidCurrencyAsync(currency, ct)) return Result<ProgramDto>.Failure(Error.Validation("Currency must be an active configured currency"));
            entity.Currency = currency;
        }
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
                    TenantId = entity.TenantId,
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
        if (!IsTrustedActor(userId))
            return Result.Failure(Error.Unauthorized("A valid authenticated actor and management context are required."));

        var entity = await InScope(_db.Set<Entity>()).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null) return Result.Failure(Error.NotFound("Program not found"));
        if (requireArchived && entity.Status != "Archived") return Result.Failure(Error.Validation("Only archived programs can be reactivated"));
        entity.Status = status; entity.UpdatedBy = userId; await _db.SaveChangesAsync(ct); return Result.Success();
    }

    private async Task<string> NextCodeAsync(CancellationToken ct)
    {
        var tenantId = TenantId();
        if (tenantId == Guid.Empty) return "PGM-0001";
        var codes = await _db.Set<Entity>().Where(x => x.TenantId == tenantId && x.Code != null).Select(x => x.Code!).ToListAsync(ct);
        var next = codes.Select(x => x.StartsWith("PGM-", StringComparison.OrdinalIgnoreCase) && int.TryParse(x[4..], out var n) ? n : 0).DefaultIfEmpty(0).Max() + 1;
        return $"PGM-{next:0000}";
    }

    private async Task<bool> IsValidPortfolioAsync(Guid id, CancellationToken ct)
    {
        var tenantId = TenantId();
        return tenantId != Guid.Empty && id != Guid.Empty
            && await _db.Set<Portfolio.Domain.Portfolio>().AnyAsync(x => x.Id == id && x.TenantId == tenantId && !x.IsDeleted, ct);
    }

    private async Task<bool> IsActivePortfolioAsync(Guid id, CancellationToken ct)
    {
        var tenantId = TenantId();
        return tenantId != Guid.Empty && id != Guid.Empty
            && await _db.Set<Portfolio.Domain.Portfolio>().AnyAsync(x => x.Id == id && x.TenantId == tenantId && !x.IsDeleted && x.Status == ActivePortfolioStatus, ct);
    }
    private async Task<bool> CanAssignPortfolioAsync(Guid id, CancellationToken ct)
    {
        var context = _managementContext?.Current;
        if (context is null) return false;
        if (context.EffectiveReadOnly) return false;
        if (_managementScopeResolver is not null)
            return await _managementScopeResolver.CanAccessPortfolioAsync(context.UserId, context.TenantId, id, ct);

        // Preserve the strict existing fallback for hosts that do not register
        // the resolver; API requests use the native assignment resolver above.
        return context.SelectedPerspective == ManagementPerspective.PortfolioManager
            && context.Scope.PortfolioIds.Contains(id);
    }

    private async Task<bool> CanCreateProgramInPortfolioAsync(Guid id, CancellationToken ct)
    {
        var context = _managementContext?.Current;
        if (context is null || context.EffectiveReadOnly || id == Guid.Empty) return false;

        var creationScope = await GetProgramPortfolioAssignmentScopeAsync(context, ct);
        if (creationScope is not null && creationScope.NativePortfolioIds.Contains(id))
        {
            if (_effectivePermissions is null) return false;
            var nativePermissions = await GetEffectivePermissionsForPortfolioAsync(context.UserId, context.TenantId, id, ct);
            return nativePermissions.Contains("Program.Create");
        }

        // Exceptional path: explicit permission Program.CreateAcrossPortfolios
        if (_effectivePermissions is not null)
        {
            var permissions = await GetEffectivePermissionsForPortfolioAsync(context.UserId, context.TenantId, id, ct);
            if (permissions.Contains("Program.Create")
                && permissions.Contains("Program.CreateAcrossPortfolios")
                && permissions.Contains("Portfolio.View"))
            {
                var portfolio = await _db.Set<Portfolio.Domain.Portfolio>().AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == context.TenantId && !x.IsDeleted && x.Status == ActivePortfolioStatus, ct);
                return creationScope is not null
                    && portfolio?.OrganizationId is Guid organizationId
                    && creationScope.OrganizationIds.Contains(organizationId);
            }
        }

        return false;
    }

    private async Task<ProgramPortfolioAssignmentScope?> GetProgramPortfolioAssignmentScopeAsync(ManagementContext context, CancellationToken ct)
    {
        if (_managementScopeResolver is null) return null;
        // Resolve native Portfolio Manager assignments independently of a selected
        // Program or Project context so perspective changes do not drop valid options.
        var snapshot = await _managementScopeResolver.GetPerspectiveSnapshotAsync(
            context.UserId,
            context.TenantId,
            cancellationToken: ct);
        var portfolioIds = snapshot.AvailablePerspectives
            .Where(item => item.Perspective == ManagementPerspective.PortfolioManager && item.IsNative)
            .SelectMany(item => item.Contexts)
            .Select(item => item.Id)
            .ToHashSet();
        var organizationIds = snapshot.Scope.OrganizationIds.ToHashSet();
        if (context.Scope.OrganizationIds.Count == 1)
        {
            var currentOrganizationId = context.Scope.OrganizationIds.Single();
            organizationIds.IntersectWith([currentOrganizationId]);
            portfolioIds = await _db.Set<Portfolio.Domain.Portfolio>().IgnoreQueryFilters().AsNoTracking()
                .Where(portfolio => portfolio.TenantId == context.TenantId && !portfolio.IsDeleted
                    && portfolio.OrganizationId == currentOrganizationId && portfolioIds.Contains(portfolio.Id))
                .Select(portfolio => portfolio.Id)
                .ToHashSetAsync(ct);
        }
        return new ProgramPortfolioAssignmentScope(portfolioIds, organizationIds);
    }
    private async Task<bool> HasProjectsAsync(Guid programId, CancellationToken ct)
    {
        var connection = _db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync(ct);
        using var command = connection.CreateCommand(); command.CommandText = "SELECT COUNT(*) FROM Projects WHERE TenantId = @tenant AND ProgramId = @pid AND IsDeleted = 0";
        var tenant = command.CreateParameter(); tenant.ParameterName = "@tenant"; tenant.Value = TenantId(); command.Parameters.Add(tenant);
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
    private async Task<bool> IsEligibleProgramManagerAsync(Guid id, Guid portfolioId, CancellationToken ct)
    {
        var tenantId = TenantId();
        if (tenantId == Guid.Empty || id == Guid.Empty || _effectivePermissions is null) return false;

        var isActiveSameTenantUser = await _db.Set<User>().AnyAsync(user =>
            user.Id == id && user.TenantId == tenantId && user.IsActive && !user.IsDeleted
            && user.UserRoles.Any(userRole => userRole.TenantId == tenantId && !userRole.IsDeleted
                && userRole.Role.TenantId == tenantId && !userRole.Role.IsDeleted
                && userRole.Role.Category == "Management"), ct);
        if (!isActiveSameTenantUser) return false;

        var permissions = await GetEffectivePermissionsForPortfolioAsync(id, tenantId, portfolioId, ct);
        return permissions.Contains("Program.View") && permissions.Contains("Handover.Approve");
    }

    private async Task<IReadOnlySet<string>> GetEffectivePermissionsForContextAsync(
        ManagementContext context,
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (_effectivePermissions is null)
            return new HashSet<string>(StringComparer.Ordinal);
        if (context.Scope.OrganizationIds.Count == 1)
            return await _effectivePermissions.GetEffectivePermissionsForOrganizationAsync(
                userId, context.TenantId, context.Scope.OrganizationIds.Single(), cancellationToken);
        return await _effectivePermissions.GetEffectivePermissionsAsync(userId, context.TenantId, cancellationToken);
    }

    private async Task<IReadOnlySet<string>> GetEffectivePermissionsForPortfolioAsync(
        Guid userId,
        Guid tenantId,
        Guid portfolioId,
        CancellationToken cancellationToken)
    {
        if (_effectivePermissions is null)
            return new HashSet<string>(StringComparer.Ordinal);
        var organizationId = await _db.Set<Portfolio.Domain.Portfolio>().IgnoreQueryFilters().AsNoTracking()
            .Where(portfolio => portfolio.Id == portfolioId && portfolio.TenantId == tenantId
                && !portfolio.IsDeleted && portfolio.Status == ActivePortfolioStatus)
            .Select(portfolio => portfolio.OrganizationId)
            .FirstOrDefaultAsync(cancellationToken);
        if (organizationId.HasValue)
            return await _effectivePermissions.GetEffectivePermissionsForOrganizationAsync(
                userId, tenantId, organizationId.Value, cancellationToken);
        return await _effectivePermissions.GetEffectivePermissionsAsync(userId, tenantId, cancellationToken);
    }

    private async Task<bool> IsDuplicateCodeAsync(string name, Guid? excludeId, CancellationToken ct)
    {
        var normalizedName = name.Trim().ToLowerInvariant();
        var tenantId = TenantId();
        return tenantId != Guid.Empty && await _db.Set<Entity>().AnyAsync(x => x.TenantId == tenantId && x.Name.ToLower() == normalizedName && !x.IsDeleted && (!excludeId.HasValue || x.Id != excludeId.Value), ct);
    }

    private async Task<ProgramDto> MapAsync(Entity entity, CancellationToken ct)
    {
        var portfolio = entity.PortfolioId != Guid.Empty ? await _db.Set<Portfolio.Domain.Portfolio>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == entity.PortfolioId && x.TenantId == entity.TenantId, ct) : null;
        var effectiveCurrency = string.IsNullOrWhiteSpace(entity.Currency) ? (portfolio?.Currency ?? "") : entity.Currency;
        User? manager = null;
        if (entity.ProgramManagerId.HasValue) manager = await _db.Set<User>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == entity.ProgramManagerId.Value && x.TenantId == entity.TenantId, ct);
        var projectCount = 0;
        try
        {
            var cmd = _db.Database.GetDbConnection();
            if (cmd.State != System.Data.ConnectionState.Open) await cmd.OpenAsync(ct);
            using var command = cmd.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM Projects WHERE TenantId = @tenant AND ProgramId = @pid AND IsDeleted = 0";
            var tenantParam = command.CreateParameter();
            tenantParam.ParameterName = "@tenant";
            tenantParam.Value = entity.TenantId;
            command.Parameters.Add(tenantParam);
            var pidParam = command.CreateParameter();
            pidParam.ParameterName = "@pid";
            pidParam.Value = entity.Id;
            command.Parameters.Add(pidParam);
            var result = await command.ExecuteScalarAsync(ct);
            projectCount = Convert.ToInt32(result);
        }
        catch { /* projects table may not have ProgramId column in all environments */ }

        const string projectsSql = "SELECT p.Id, p.Name, p.Code, p.Status, p.Currency FROM Projects p WHERE p.TenantId = {0} AND p.ProgramId = {1} AND p.IsDeleted = 0";
        var projects = await _db.Database.SqlQueryRaw<ProgramProjectRow>(projectsSql, entity.TenantId, entity.Id)
            .Select(x => new ProgramProjectDto { Id = x.Id, Name = x.Name, Code = x.Code, Status = x.Status, Currency = x.Currency })
            .ToListAsync(ct);
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
            Health = entity.Health,
            ProjectCount = projectCount,
            Projects = projects,
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
        : dto.Status is not ("Active" or "Planning" or "On Hold" or "Completed" or "Archived") ? Error.Validation("Status must be Active, Planning, On Hold, Completed, or Archived")
        : null;

    private bool CanAssignInitialProgramManager() =>
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
        return context is not null
            && context.TenantId == tenantId
            && TryGetAuthenticatedActor(out var actorId, out var claimTenantId)
            && context.UserId == actorId
            && claimTenantId == tenantId;
    }

    private IQueryable<Entity> InScope(IQueryable<Entity> query)
    {
        var tenantId = TenantId();
        if (tenantId == Guid.Empty) return query.Where(_ => false);

        query = query.Where(x => x.TenantId == tenantId);
        if (_http.HttpContext is null)
            return TrustedTenantExecutionContext.CurrentTenantId == tenantId ? query : query.Where(_ => false);

        var context = _managementContext?.Current;
        if (context is null
            || context.TenantId != tenantId
            || !TryGetAuthenticatedActor(out var actorId, out var claimTenantId)
            || context.UserId != actorId
            || claimTenantId != tenantId) return query.Where(_ => false);
        if (context.CanReadAllTenantPortfolioProgramData || context.HasTenantAdministratorPortfolioWriteScope)
            return query;
        return query.Where(x => context.Scope.ProgramIds.Contains(x.Id));
    }

    private bool IsTrustedActor(string userId)
    {
        if (!Guid.TryParse(userId, out var actorId) || actorId == Guid.Empty)
            return false;

        var httpContext = _http.HttpContext;
        if (httpContext is null)
            return TrustedTenantExecutionContext.CurrentTenantId.HasValue;

        return TryGetAuthenticatedActor(out var claimActorId, out var claimTenantId)
            && claimActorId == actorId
            && _managementContext?.Current is { } context
            && context.UserId == actorId
            && context.TenantId == claimTenantId;
    }

    private bool TryGetAuthenticatedActor(out Guid actorId, out Guid tenantId)
    {
        actorId = Guid.Empty;
        tenantId = Guid.Empty;
        var principal = _http.HttpContext?.User;
        return principal?.Identity?.IsAuthenticated == true
            && Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out actorId)
            && actorId != Guid.Empty
            && Guid.TryParse(principal.FindFirst("tenantId")?.Value, out tenantId)
            && tenantId != Guid.Empty;
    }
    private sealed record ProgramPortfolioAssignmentScope(HashSet<Guid> NativePortfolioIds, HashSet<Guid> OrganizationIds);
    private sealed class ProgramProjectRow { public Guid Id { get; set; } public string Name { get; set; } = ""; public string Code { get; set; } = ""; public string Status { get; set; } = ""; public string Currency { get; set; } = ""; }
}
