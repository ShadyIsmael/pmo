using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Organization.Domain;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Helpers;

namespace NEXUS.Modules.Organization.Application;

public sealed class OrganizationalUnitService : IOrganizationalUnitService
{
    private const string DefaultTypeCategory = ReferenceDataCategories.OrganizationalUnitType;
    private readonly DbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IResourceReferenceLookup _resourceLookup;

    public OrganizationalUnitService(DbContext context, IHttpContextAccessor httpContextAccessor, IResourceReferenceLookup resourceLookup)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
        _resourceLookup = resourceLookup;
    }

    public async Task<IReadOnlyList<OrganizationalUnitDto>> GetUnitsAsync(bool includeInactive, CancellationToken cancellationToken = default)
    {
        await EnsureDefaultTypesAsync(cancellationToken);
        var tenantId = CurrentTenantId();
        var units = await _context.Set<OrganizationalUnit>()
            .Where(unit => unit.TenantId == tenantId && (includeInactive || unit.IsActive))
            .OrderBy(unit => unit.SortOrder).ThenBy(unit => unit.Name)
            .ToListAsync(cancellationToken);
        return await MapUnitsAsync(units, cancellationToken);
    }

    public async Task<IReadOnlyList<OrganizationalUnitLookupDto>> GetProjectLookupAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = CurrentTenantId();
        return await _context.Set<OrganizationalUnit>()
            .Where(unit => unit.TenantId == tenantId && unit.IsActive)
            .OrderBy(unit => unit.SortOrder)
            .ThenBy(unit => unit.Name)
            .Select(unit => new OrganizationalUnitLookupDto
            {
                Id = unit.Id,
                Name = unit.Name
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<Result<OrganizationalUnitDto>> GetUnitAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var unit = await _context.Set<OrganizationalUnit>().FirstOrDefaultAsync(item => item.TenantId == CurrentTenantId() && item.Id == id, cancellationToken);
        if (unit is null) return Result<OrganizationalUnitDto>.Failure(Error.NotFound("Organizational unit not found"));
        return Result<OrganizationalUnitDto>.Success((await MapUnitsAsync(new[] { unit }, cancellationToken)).Single());
    }

    public async Task<Result<OrganizationalUnitDto>> CreateUnitAsync(SaveOrganizationalUnitDto request, string userId, CancellationToken cancellationToken = default)
    {
        await EnsureDefaultTypesAsync(cancellationToken);
        var validation = await ValidateUnitAsync(request, null, cancellationToken);
        if (validation is not null) return Result<OrganizationalUnitDto>.Failure(validation);
        var unit = new OrganizationalUnit
        {
            TenantId = CurrentTenantId(),
            Code = await NextCodeAsync(cancellationToken),
            Name = request.Name.Trim(),
            TypeCode = request.TypeCode.Trim(),
            Description = Normalize(request.Description),
            ParentOrganizationalUnitId = request.ParentOrganizationalUnitId,
            SortOrder = request.SortOrder,
            IsActive = request.IsActive,
            CreatedBy = userId
        };
        _context.Set<OrganizationalUnit>().Add(unit);
        await _context.SaveChangesAsync(cancellationToken);
        return Result<OrganizationalUnitDto>.Success((await MapUnitsAsync(new[] { unit }, cancellationToken)).Single());
    }

    public async Task<Result<OrganizationalUnitDto>> UpdateUnitAsync(Guid id, SaveOrganizationalUnitDto request, string userId, CancellationToken cancellationToken = default)
    {
        await EnsureDefaultTypesAsync(cancellationToken);
        var unit = await _context.Set<OrganizationalUnit>().FirstOrDefaultAsync(item => item.TenantId == CurrentTenantId() && item.Id == id, cancellationToken);
        if (unit is null) return Result<OrganizationalUnitDto>.Failure(Error.NotFound("Organizational unit not found"));
        var validation = await ValidateUnitAsync(request, id, cancellationToken);
        if (validation is not null) return Result<OrganizationalUnitDto>.Failure(validation);
        unit.Name = request.Name.Trim();
        unit.TypeCode = request.TypeCode.Trim();
        unit.Description = Normalize(request.Description);
        unit.ParentOrganizationalUnitId = request.ParentOrganizationalUnitId;
        unit.SortOrder = request.SortOrder;
        unit.IsActive = request.IsActive;
        unit.UpdatedBy = userId;
        await _context.SaveChangesAsync(cancellationToken);
        return Result<OrganizationalUnitDto>.Success((await MapUnitsAsync(new[] { unit }, cancellationToken)).Single());
    }

    public Task<Result> DeactivateUnitAsync(Guid id, string userId, CancellationToken cancellationToken = default) => SetUnitActiveAsync(id, false, userId, cancellationToken);
    public Task<Result> ReactivateUnitAsync(Guid id, string userId, CancellationToken cancellationToken = default) => SetUnitActiveAsync(id, true, userId, cancellationToken);

    public async Task<Result> DeleteUnitAsync(Guid id, string userId, CancellationToken cancellationToken = default)
    {
        var tenantId = CurrentTenantId();
        var unit = await _context.Set<OrganizationalUnit>().FirstOrDefaultAsync(item => item.TenantId == tenantId && item.Id == id, cancellationToken);
        if (unit is null) return Result.Failure(Error.NotFound("Organizational unit not found"));
        if (await _context.Set<OrganizationalUnit>().AnyAsync(item => item.TenantId == tenantId && item.ParentOrganizationalUnitId == id, cancellationToken))
            return Result.Failure(Error.Conflict("An organizational unit with children cannot be deleted; deactivate it instead"));
        if (await _context.Set<ResourceOrganizationalUnit>().AnyAsync(item => item.TenantId == tenantId && item.OrganizationalUnitId == id && item.IsActive, cancellationToken))
            return Result.Failure(Error.Conflict("An organizational unit with active resource memberships cannot be deleted"));
        unit.IsDeleted = true;
        unit.IsActive = false;
        unit.UpdatedBy = userId;
        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<IReadOnlyList<OrganizationalUnitTypeDto>> GetTypesAsync(bool includeInactive, CancellationToken cancellationToken = default)
    {
        await EnsureDefaultTypesAsync(cancellationToken);
        var query = _context.Set<Lookup>().Where(item => item.Category == DefaultTypeCategory);
        if (!includeInactive) query = query.Where(item => item.IsActive);
        return await query.OrderBy(item => item.DisplayOrder).ThenBy(item => item.Code).Select(item => ToTypeDto(item)).ToListAsync(cancellationToken);
    }

    public async Task<Result<OrganizationalUnitTypeDto>> CreateTypeAsync(SaveOrganizationalUnitTypeDto request, string userId, CancellationToken cancellationToken = default)
    {
        var validation = ValidateType(request, out var code, out var nameEn, out var nameAr);
        if (validation is not null) return Result<OrganizationalUnitTypeDto>.Failure(validation);
        var tenantId = CurrentTenantId();
        if (await _context.Set<Lookup>().IgnoreQueryFilters().AnyAsync(item => item.TenantId == tenantId && item.Category == DefaultTypeCategory && item.Code == code && !item.IsDeleted, cancellationToken))
            return Result<OrganizationalUnitTypeDto>.Failure(Error.Conflict("An organizational unit type with this code already exists"));
        var type = new Lookup { TenantId = tenantId, Category = DefaultTypeCategory, Code = code, Name = nameEn, NameEn = nameEn, NameAr = nameAr, DisplayOrder = request.DisplayOrder, IsActive = request.IsActive, CreatedBy = userId };
        _context.Set<Lookup>().Add(type);
        await _context.SaveChangesAsync(cancellationToken);
        return Result<OrganizationalUnitTypeDto>.Success(ToTypeDto(type));
    }

    public async Task<Result<OrganizationalUnitTypeDto>> UpdateTypeAsync(Guid id, SaveOrganizationalUnitTypeDto request, string userId, CancellationToken cancellationToken = default)
    {
        var validation = ValidateType(request, out var code, out var nameEn, out var nameAr);
        if (validation is not null) return Result<OrganizationalUnitTypeDto>.Failure(validation);
        var type = await _context.Set<Lookup>().FirstOrDefaultAsync(item => item.TenantId == CurrentTenantId() && item.Id == id && item.Category == DefaultTypeCategory, cancellationToken);
        if (type is null) return Result<OrganizationalUnitTypeDto>.Failure(Error.NotFound("Organizational unit type not found"));
        if (await _context.Set<Lookup>().IgnoreQueryFilters().AnyAsync(item => item.Id != id && item.TenantId == type.TenantId && item.Category == DefaultTypeCategory && item.Code == code && !item.IsDeleted, cancellationToken))
            return Result<OrganizationalUnitTypeDto>.Failure(Error.Conflict("An organizational unit type with this code already exists"));
        if (!request.IsActive && await _context.Set<OrganizationalUnit>().AnyAsync(unit => unit.TypeCode == type.Code && unit.IsActive, cancellationToken))
            return Result<OrganizationalUnitTypeDto>.Failure(Error.Conflict("An active organizational unit still uses this type"));
        type.Code = code; type.Name = nameEn; type.NameEn = nameEn; type.NameAr = nameAr; type.DisplayOrder = request.DisplayOrder; type.IsActive = request.IsActive; type.UpdatedBy = userId;
        await _context.SaveChangesAsync(cancellationToken);
        return Result<OrganizationalUnitTypeDto>.Success(ToTypeDto(type));
    }

    public async Task<IReadOnlyList<ResourceOrganizationalUnitDto>> GetMembershipsAsync(Guid? organizationalUnitId, Guid? resourceId, bool includeHistory, CancellationToken cancellationToken = default)
    {
        var tenantId = CurrentTenantId();
        var query = _context.Set<ResourceOrganizationalUnit>()
            .Include(item => item.OrganizationalUnit)
            .Where(item => item.TenantId == tenantId && item.OrganizationalUnit.TenantId == tenantId)
            .AsQueryable();
        if (organizationalUnitId.HasValue) query = query.Where(item => item.OrganizationalUnitId == organizationalUnitId.Value);
        if (resourceId.HasValue) query = query.Where(item => item.ResourceId == resourceId.Value);
        if (!includeHistory) query = query.Where(item => item.IsActive && (item.EffectiveTo == null || item.EffectiveTo > DateTime.UtcNow));
        var rows = await query.OrderByDescending(item => item.IsActive).ThenByDescending(item => item.EffectiveFrom).ToListAsync(cancellationToken);
        var names = await _resourceLookup.GetDetailsAsync(rows.Select(item => item.ResourceId), cancellationToken);
        return rows.Select(item => new ResourceOrganizationalUnitDto { Id = item.Id, ResourceId = item.ResourceId, ResourceName = names.TryGetValue(item.ResourceId, out var resource) ? resource.Name : "", OrganizationalUnitId = item.OrganizationalUnitId, OrganizationalUnitName = item.OrganizationalUnit.Name, IsPrimary = item.IsPrimary, EffectiveFrom = item.EffectiveFrom, EffectiveTo = item.EffectiveTo, IsActive = item.IsActive }).ToList();
    }

    public async Task<Result<ResourceOrganizationalUnitDto>> AssignMembershipAsync(SaveResourceOrganizationalUnitDto request, string userId, CancellationToken cancellationToken = default)
    {
        var tenantId = CurrentTenantId();
        var unit = await _context.Set<OrganizationalUnit>().FirstOrDefaultAsync(item => item.TenantId == tenantId && item.Id == request.OrganizationalUnitId && item.IsActive, cancellationToken);
        if (unit is null) return Result<ResourceOrganizationalUnitDto>.Failure(Error.NotFound("Active organizational unit not found"));
        var resource = await _resourceLookup.FindAsync(request.ResourceId, cancellationToken);
        if (resource is null || !resource.IsActive) return Result<ResourceOrganizationalUnitDto>.Failure(Error.Validation("The resource does not exist in the current tenant or is inactive"));
        var effectiveFrom = request.EffectiveFrom.ToUniversalTime();
        if (request.IsPrimary)
        {
            var current = await _context.Set<ResourceOrganizationalUnit>().Where(item => item.TenantId == tenantId && item.ResourceId == request.ResourceId && item.IsPrimary && item.IsActive && (item.EffectiveTo == null || item.EffectiveTo > effectiveFrom)).ToListAsync(cancellationToken);
            foreach (var existing in current) { existing.EffectiveTo = effectiveFrom; existing.IsActive = false; existing.UpdatedBy = userId; }
        }
        var membership = new ResourceOrganizationalUnit { TenantId = CurrentTenantId(), ResourceId = request.ResourceId, OrganizationalUnitId = unit.Id, IsPrimary = request.IsPrimary, EffectiveFrom = effectiveFrom, CreatedBy = userId };
        _context.Set<ResourceOrganizationalUnit>().Add(membership);
        await _context.SaveChangesAsync(cancellationToken);
        membership.OrganizationalUnit = unit;
        return Result<ResourceOrganizationalUnitDto>.Success(new ResourceOrganizationalUnitDto { Id = membership.Id, ResourceId = membership.ResourceId, ResourceName = resource.Name, OrganizationalUnitId = membership.OrganizationalUnitId, OrganizationalUnitName = unit.Name, IsPrimary = membership.IsPrimary, EffectiveFrom = membership.EffectiveFrom, IsActive = membership.IsActive });
    }

    public async Task<Result> EndMembershipAsync(Guid membershipId, DateTime effectiveTo, string userId, CancellationToken cancellationToken = default)
    {
        var membership = await _context.Set<ResourceOrganizationalUnit>().FirstOrDefaultAsync(item => item.TenantId == CurrentTenantId() && item.Id == membershipId && item.IsActive, cancellationToken);
        if (membership is null) return Result.Failure(Error.NotFound("Current organizational membership not found"));
        var end = effectiveTo.ToUniversalTime();
        if (end <= membership.EffectiveFrom) return Result.Failure(Error.Validation("Membership end must be after its start"));
        membership.EffectiveTo = end; membership.IsActive = false; membership.UpdatedBy = userId;
        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<IReadOnlyList<ResourceReportingLineDto>> GetReportingLinesAsync(Guid? resourceId, bool includeHistory, CancellationToken cancellationToken = default)
    {
        var query = _context.Set<ResourceReportingLine>().Where(item => item.TenantId == CurrentTenantId()).AsQueryable();
        if (resourceId.HasValue) query = query.Where(item => item.ResourceId == resourceId.Value || item.ManagerResourceId == resourceId.Value);
        if (!includeHistory) query = query.Where(item => item.IsActive && (item.EffectiveTo == null || item.EffectiveTo > DateTime.UtcNow));
        var rows = await query.OrderByDescending(item => item.IsActive).ThenByDescending(item => item.EffectiveFrom).ToListAsync(cancellationToken);
        var ids = rows.SelectMany(item => new[] { item.ResourceId, item.ManagerResourceId });
        var names = await _resourceLookup.GetDetailsAsync(ids, cancellationToken);
        return rows.Select(item => new ResourceReportingLineDto { Id = item.Id, ResourceId = item.ResourceId, ResourceName = names.TryGetValue(item.ResourceId, out var resource) ? resource.Name : "", ManagerResourceId = item.ManagerResourceId, ManagerResourceName = names.TryGetValue(item.ManagerResourceId, out var manager) ? manager.Name : "", ReportingType = item.ReportingType, IsPrimary = item.IsPrimary, EffectiveFrom = item.EffectiveFrom, EffectiveTo = item.EffectiveTo, IsActive = item.IsActive }).ToList();
    }

    public async Task<Result<ResourceReportingLineDto>> AssignReportingLineAsync(SaveResourceReportingLineDto request, string userId, CancellationToken cancellationToken = default)
    {
        if (request.ResourceId == request.ManagerResourceId) return Result<ResourceReportingLineDto>.Failure(Error.Validation("A resource cannot report to itself"));
        var details = await _resourceLookup.GetDetailsAsync(new[] { request.ResourceId, request.ManagerResourceId }, cancellationToken);
        if (!details.TryGetValue(request.ResourceId, out var resource) || !resource.IsActive || !details.TryGetValue(request.ManagerResourceId, out var manager) || !manager.IsActive)
            return Result<ResourceReportingLineDto>.Failure(Error.Validation("Both resources must exist in the current tenant and be active"));
        if (await CreatesReportingCycleAsync(request.ResourceId, request.ManagerResourceId, cancellationToken))
            return Result<ResourceReportingLineDto>.Failure(Error.Conflict("This reporting line would create a management cycle"));
        var effectiveFrom = request.EffectiveFrom.ToUniversalTime();
        if (request.IsPrimary)
        {
            var current = await _context.Set<ResourceReportingLine>().Where(item => item.TenantId == CurrentTenantId() && item.ResourceId == request.ResourceId && item.IsPrimary && item.IsActive && (item.EffectiveTo == null || item.EffectiveTo > effectiveFrom)).ToListAsync(cancellationToken);
            foreach (var existing in current) { existing.EffectiveTo = effectiveFrom; existing.IsActive = false; existing.UpdatedBy = userId; }
        }
        var line = new ResourceReportingLine { TenantId = CurrentTenantId(), ResourceId = request.ResourceId, ManagerResourceId = request.ManagerResourceId, ReportingType = string.IsNullOrWhiteSpace(request.ReportingType) ? "LineManager" : request.ReportingType.Trim(), IsPrimary = request.IsPrimary, EffectiveFrom = effectiveFrom, CreatedBy = userId };
        _context.Set<ResourceReportingLine>().Add(line);
        await _context.SaveChangesAsync(cancellationToken);
        return Result<ResourceReportingLineDto>.Success(ToReportingDto(line, resource.Name, manager.Name));
    }

    public async Task<Result> EndReportingLineAsync(Guid reportingLineId, DateTime effectiveTo, string userId, CancellationToken cancellationToken = default)
    {
        var line = await _context.Set<ResourceReportingLine>().FirstOrDefaultAsync(item => item.TenantId == CurrentTenantId() && item.Id == reportingLineId && item.IsActive, cancellationToken);
        if (line is null) return Result.Failure(Error.NotFound("Current reporting line not found"));
        var end = effectiveTo.ToUniversalTime();
        if (end <= line.EffectiveFrom) return Result.Failure(Error.Validation("Reporting line end must be after its start"));
        line.EffectiveTo = end; line.IsActive = false; line.UpdatedBy = userId;
        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private async Task<Result> SetUnitActiveAsync(Guid id, bool active, string userId, CancellationToken cancellationToken)
    {
        var tenantId = CurrentTenantId();
        var unit = await _context.Set<OrganizationalUnit>().FirstOrDefaultAsync(item => item.TenantId == tenantId && item.Id == id, cancellationToken);
        if (unit is null) return Result.Failure(Error.NotFound("Organizational unit not found"));
        if (!active && await _context.Set<OrganizationalUnit>().AnyAsync(item => item.TenantId == tenantId && item.ParentOrganizationalUnitId == id && item.IsActive, cancellationToken))
            return Result.Failure(Error.Conflict("Deactivate child units before deactivating their parent"));
        if (active && unit.ParentOrganizationalUnitId.HasValue && !await _context.Set<OrganizationalUnit>().AnyAsync(item => item.TenantId == tenantId && item.Id == unit.ParentOrganizationalUnitId && item.IsActive, cancellationToken))
            return Result.Failure(Error.Validation("An active organizational unit must have an active parent"));
        unit.IsActive = active; unit.UpdatedBy = userId;
        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private async Task<Error?> ValidateUnitAsync(SaveOrganizationalUnitDto request, Guid? id, CancellationToken cancellationToken)
    {
        var tenantId = CurrentTenantId();
        var name = request.Name.Trim(); var type = request.TypeCode.Trim();
        if (name.Length == 0 || name.Length > 200) return Error.Validation("Organizational unit name is required");
        if (type.Length == 0 || !await _context.Set<Lookup>().AnyAsync(item => item.TenantId == tenantId && item.Category == DefaultTypeCategory && item.Code == type && item.IsActive, cancellationToken)) return Error.Validation("Select an active organizational unit type");
        if (id.HasValue && request.ParentOrganizationalUnitId == id) return Error.Validation("An organizational unit cannot be its own parent");
        if (request.ParentOrganizationalUnitId.HasValue && !await _context.Set<OrganizationalUnit>().AnyAsync(item => item.TenantId == tenantId && item.Id == request.ParentOrganizationalUnitId.Value && item.IsActive, cancellationToken)) return Error.Validation("The selected parent does not exist or is inactive in the current tenant");
        if (id.HasValue && await CreatesUnitCycleAsync(id.Value, request.ParentOrganizationalUnitId, cancellationToken)) return Error.Conflict("This parent would create an organizational hierarchy cycle");
        return null;
    }

    private async Task<bool> CreatesUnitCycleAsync(Guid unitId, Guid? parentId, CancellationToken cancellationToken)
    {
        var visited = new HashSet<Guid>();
        while (parentId.HasValue && visited.Add(parentId.Value))
        {
            if (parentId.Value == unitId) return true;
            parentId = await _context.Set<OrganizationalUnit>().Where(item => item.TenantId == CurrentTenantId() && item.Id == parentId.Value).Select(item => item.ParentOrganizationalUnitId).FirstOrDefaultAsync(cancellationToken);
        }
        return false;
    }

    private async Task<bool> CreatesReportingCycleAsync(Guid resourceId, Guid managerResourceId, CancellationToken cancellationToken)
    {
        var visited = new HashSet<Guid> { resourceId };
        var current = managerResourceId;
        while (visited.Add(current))
        {
            var next = await _context.Set<ResourceReportingLine>().Where(line => line.ResourceId == current && line.IsPrimary && line.IsActive && (line.EffectiveTo == null || line.EffectiveTo > DateTime.UtcNow)).Select(line => (Guid?)line.ManagerResourceId).FirstOrDefaultAsync(cancellationToken);
            if (!next.HasValue) return false;
            current = next.Value;
        }
        return true;
    }

    private async Task<IReadOnlyList<OrganizationalUnitDto>> MapUnitsAsync(IReadOnlyList<OrganizationalUnit> units, CancellationToken cancellationToken)
    {
        var ids = units.Select(unit => unit.Id).ToHashSet();
        var tenantId = CurrentTenantId();
        var allUnits = await _context.Set<OrganizationalUnit>().AsNoTracking().Where(unit => unit.TenantId == tenantId).ToListAsync(cancellationToken);
        var unitById = allUnits.ToDictionary(unit => unit.Id);
        // Build child counts once. Counting all units for every returned unit
        // turns a large hierarchy load into an O(n²) operation and can make
        // the Organization screen appear frozen while the API is responding.
        var childCounts = allUnits
            .Where(unit => unit.ParentOrganizationalUnitId.HasValue)
            .GroupBy(unit => unit.ParentOrganizationalUnitId!.Value)
            .ToDictionary(group => group.Key, group => group.Count());
        var pathCache = new Dictionary<Guid, string>();
        var types = await _context.Set<Lookup>().Where(item => item.TenantId == tenantId && item.Category == DefaultTypeCategory).AsNoTracking().ToDictionaryAsync(item => item.Code, cancellationToken);
        var memberCounts = await _context.Set<ResourceOrganizationalUnit>().Where(item => item.TenantId == tenantId && ids.Contains(item.OrganizationalUnitId) && item.IsActive && (item.EffectiveTo == null || item.EffectiveTo > DateTime.UtcNow)).GroupBy(item => item.OrganizationalUnitId).Select(group => new { group.Key, Count = group.Count() }).ToDictionaryAsync(item => item.Key, item => item.Count, cancellationToken);
        return units.Select(unit =>
        {
            types.TryGetValue(unit.TypeCode, out var type);
            return new OrganizationalUnitDto { Id = unit.Id, Code = unit.Code, Name = unit.Name, TypeCode = unit.TypeCode, TypeNameEn = type?.NameEn, TypeNameAr = type?.NameAr, Description = unit.Description, HierarchyPath = BuildHierarchyPath(unit, unitById, pathCache), ParentOrganizationalUnitId = unit.ParentOrganizationalUnitId, ParentName = unit.ParentOrganizationalUnitId.HasValue && unitById.TryGetValue(unit.ParentOrganizationalUnitId.Value, out var parent) ? parent.Name : null, IsActive = unit.IsActive, SortOrder = unit.SortOrder, ChildCount = childCounts.TryGetValue(unit.Id, out var childCount) ? childCount : 0, CurrentMemberCount = memberCounts.TryGetValue(unit.Id, out var count) ? count : 0, CreatedAt = unit.CreatedAt, UpdatedAt = unit.UpdatedAt };
        }).ToList();
    }

    private static string BuildHierarchyPath(OrganizationalUnit unit, IReadOnlyDictionary<Guid, OrganizationalUnit> units, IDictionary<Guid, string> cache)
    {
        if (cache.TryGetValue(unit.Id, out var cached)) return cached;
        var chain = new List<OrganizationalUnit>();
        var visited = new HashSet<Guid>();
        OrganizationalUnit? current = unit;
        var prefix = string.Empty;
        while (current is not null && visited.Add(current.Id) && !cache.TryGetValue(current.Id, out prefix))
        {
            chain.Add(current);
            current = current.ParentOrganizationalUnitId.HasValue && units.TryGetValue(current.ParentOrganizationalUnitId.Value, out var parent) ? parent : null;
        }
        for (var index = chain.Count - 1; index >= 0; index--)
        {
            prefix = string.IsNullOrEmpty(prefix) ? chain[index].Name : $"{prefix} / {chain[index].Name}";
            cache[chain[index].Id] = prefix;
        }
        return cache.TryGetValue(unit.Id, out var path) ? path : unit.Name;
    }

    private async Task EnsureDefaultTypesAsync(CancellationToken cancellationToken)
    {
        var tenantId = CurrentTenantId();
        var defaults = new[] { ("Division", "Division", "قطاع"), ("Department", "Department", "إدارة"), ("Function", "Function", "وظيفة"), ("Office", "Office", "مكتب") };
        var existingCodes = await _context.Set<Lookup>().IgnoreQueryFilters()
            .Where(item => item.TenantId == tenantId && item.Category == DefaultTypeCategory && !item.IsDeleted)
            .Select(item => item.Code)
            .ToHashSetAsync(cancellationToken);
        var added = false;
        for (var index = 0; index < defaults.Length; index++)
        {
            var definition = defaults[index];
            if (existingCodes.Contains(definition.Item1)) continue;
            _context.Set<Lookup>().Add(new Lookup { TenantId = tenantId, Category = DefaultTypeCategory, Code = definition.Item1, Name = definition.Item2, NameEn = definition.Item2, NameAr = definition.Item3, DisplayOrder = (index + 1) * 10, IsActive = true, CreatedBy = "System" });
            added = true;
        }
        if (added) await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task<string> NextCodeAsync(CancellationToken cancellationToken)
    {
        var existing = await _context.Set<OrganizationalUnit>().IgnoreQueryFilters().Where(item => item.TenantId == CurrentTenantId() && item.Code.StartsWith("ORG-")).Select(item => item.Code).ToListAsync(cancellationToken);
        var next = existing.Select(code => int.TryParse(code[4..], out var number) ? number : 0).DefaultIfEmpty().Max() + 1;
        return $"ORG-{next:000000}";
    }

    private static Error? ValidateType(SaveOrganizationalUnitTypeDto request, out string code, out string nameEn, out string nameAr)
    {
        code = request.Code.Trim(); nameEn = request.NameEn.Trim(); nameAr = request.NameAr.Trim();
        if (code.Length == 0 || code.Length > 100 || code.Any(character => !(char.IsLetterOrDigit(character) || character is '_' or '-' or '.'))) return Error.Validation("Type code must contain only letters, numbers, hyphens, underscores, or dots");
        if (nameEn.Length == 0 || nameEn.Length > 200) return Error.Validation("English type name is required");
        if (nameAr.Length == 0 || nameAr.Length > 200) return Error.Validation("Arabic type name is required");
        return null;
    }

    private static OrganizationalUnitTypeDto ToTypeDto(Lookup type) => new() { Id = type.Id, Code = type.Code, NameEn = type.NameEn, NameAr = type.NameAr, DisplayOrder = type.DisplayOrder, IsActive = type.IsActive };
    private static ResourceReportingLineDto ToReportingDto(ResourceReportingLine line, string resourceName, string managerName) => new() { Id = line.Id, ResourceId = line.ResourceId, ResourceName = resourceName, ManagerResourceId = line.ManagerResourceId, ManagerResourceName = managerName, ReportingType = line.ReportingType, IsPrimary = line.IsPrimary, EffectiveFrom = line.EffectiveFrom, EffectiveTo = line.EffectiveTo, IsActive = line.IsActive };
    private Guid CurrentTenantId() => Guid.TryParse(_httpContextAccessor.HttpContext?.User.FindFirstValue("tenantId"), out var tenantId) && tenantId != Guid.Empty
        ? tenantId
        : throw new InvalidOperationException("A trusted tenant context is required.");
    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

}
