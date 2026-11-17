using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Management;
using OrganizationEntity = NEXUS.Modules.Organization.Domain.Organization;

namespace NEXUS.Modules.Organization.Application;

public class OrganizationService : IOrganizationService
{
    private readonly DbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuditService _auditService;
    private readonly IOrganizationSettingsAccessService _settingsAccess;
    private readonly IManagementContextAccessor? _managementContextAccessor;

    public OrganizationService(
        DbContext context,
        IHttpContextAccessor httpContextAccessor,
        IAuditService auditService,
        IOrganizationSettingsAccessService settingsAccess,
        IManagementContextAccessor? managementContextAccessor = null)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
        _auditService = auditService;
        _settingsAccess = settingsAccess;
        _managementContextAccessor = managementContextAccessor;
    }

    public async Task<Result<OrganizationResponseDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var org = await _context.Set<OrganizationEntity>()
            .AsNoTracking()
            .AsSplitQuery()
            .Include(o => o.BusinessUnits)
                .ThenInclude(b => b.Departments)
                    .ThenInclude(d => d.Teams)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
        return org is null
            ? Result<OrganizationResponseDto>.Failure(Error.NotFound())
            : Result<OrganizationResponseDto>.Success(OrganizationResponseMapper.ToResponse(org, includeDepartments: true));
    }

    public async Task<PagedResult<OrganizationResponseDto>> GetPagedAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _context.Set<OrganizationEntity>()
            .AsNoTracking()
            .Include(o => o.BusinessUnits);
        var total = await query.CountAsync(cancellationToken);
        var organizations = await query
            .OrderBy(organization => organization.Name).ThenBy(organization => organization.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        var items = organizations.Select(organization =>
            OrganizationResponseMapper.ToResponse(organization, includeDepartments: false)).ToArray();
        return new PagedResult<OrganizationResponseDto>(items, page, pageSize, total);
    }

    public async Task<PagedResult<OrganizationDirectoryItemDto>> GetPagedDirectoryAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Set<OrganizationEntity>().AsNoTracking();
        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(organization => organization.Name).ThenBy(organization => organization.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(organization => new OrganizationDirectoryItemDto(organization.Id, organization.Name))
            .ToArrayAsync(cancellationToken);
        return new PagedResult<OrganizationDirectoryItemDto>(items, page, pageSize, total);
    }

    public async Task<IReadOnlyList<OrganizationProfileDto>> GetProfilesAsync(CancellationToken cancellationToken = default)
    {
        if (!TryGetTenantId(out var tenantId)) return Array.Empty<OrganizationProfileDto>();
        var authorizedOrganizationIds = await _settingsAccess.GetReadableOrganizationIdsAsync(cancellationToken);
        if (authorizedOrganizationIds.Count == 0) return Array.Empty<OrganizationProfileDto>();
        var manageableOrganizationIds = await _settingsAccess.GetManageableOrganizationIdsAsync(cancellationToken);

        var organizations = await _context.Set<OrganizationEntity>()
            .AsNoTracking()
            .Where(organization => organization.TenantId == tenantId && !organization.IsDeleted
                && authorizedOrganizationIds.Contains(organization.Id))
            .OrderBy(organization => organization.Name)
            .ToListAsync(cancellationToken);
        var manageable = manageableOrganizationIds.ToHashSet();
        return organizations.Select(organization => ToProfile(organization) with
        {
            CanManageSettings = manageable.Contains(organization.Id)
        }).ToArray();
    }

    public async Task<OrganizationProfileUpdateResult> UpdateProfileAsync(
        Guid id,
        UpdateOrganizationProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        if (_managementContextAccessor?.Current?.EffectiveReadOnly == true)
            return new OrganizationProfileUpdateResult(OrganizationProfileUpdateCode.AccessDenied,
                Error: "Organization changes are unavailable in read-only management mode.");
        if (!TryGetTenantId(out var tenantId))
            return new OrganizationProfileUpdateResult(OrganizationProfileUpdateCode.TenantContextMissing,
                Error: "A trusted tenant context is required.");
        if (!await _settingsAccess.HasAccessAsync(id, manage: true, cancellationToken: cancellationToken))
            return new OrganizationProfileUpdateResult(OrganizationProfileUpdateCode.NotFound);

        var actor = _httpContextAccessor.HttpContext?.User;
        var actorId = Guid.TryParse(actor?.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedActorId)
            ? parsedActorId
            : Guid.TryParse(actor?.FindFirstValue("sub"), out parsedActorId) ? parsedActorId : (Guid?)null;
        if (actorId is null || actorId == Guid.Empty)
            return new OrganizationProfileUpdateResult(OrganizationProfileUpdateCode.TenantContextMissing,
                Error: "A trusted authenticated actor is required for audited updates.");
        var current = await _context.Set<OrganizationEntity>()
            .AsNoTracking()
            .Where(organization => organization.Id == id && organization.TenantId == tenantId && !organization.IsDeleted)
            .Select(organization => new OrganizationProfileDto(
                organization.Id,
                organization.Name,
                organization.Description,
                organization.Website,
                organization.Email,
                organization.Phone,
                organization.AddressLine1,
                organization.AddressLine2,
                organization.City,
                organization.Country,
                organization.Postcode,
                organization.UpdatedAt,
                true))
            .FirstOrDefaultAsync(cancellationToken);
        if (current is null)
            return new OrganizationProfileUpdateResult(OrganizationProfileUpdateCode.NotFound);
        if (current.UpdatedAt != request.ExpectedUpdatedAt)
            return new OrganizationProfileUpdateResult(OrganizationProfileUpdateCode.ConcurrencyConflict,
                Error: "The organization changed after it was loaded. Reload before saving.");

        var updatedAt = DateTime.UtcNow;
        if (request.ExpectedUpdatedAt is { } expected && updatedAt <= expected) updatedAt = expected.AddTicks(1);
        var name = request.Name.Trim();
        var description = TrimOrNull(request.Description);
        var website = TrimOrNull(request.Website);
        var email = TrimOrNull(request.Email);
        var phone = TrimOrNull(request.Phone);
        var addressLine1 = TrimOrNull(request.AddressLine1);
        var addressLine2 = TrimOrNull(request.AddressLine2);
        var city = TrimOrNull(request.City);
        var country = TrimOrNull(request.Country);
        var postcode = TrimOrNull(request.Postcode);

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        var updated = await _context.Set<OrganizationEntity>()
            .Where(organization => organization.Id == id
                && organization.TenantId == tenantId
                && !organization.IsDeleted
                && organization.UpdatedAt == request.ExpectedUpdatedAt)
            .ExecuteUpdateAsync(updates => updates
                .SetProperty(organization => organization.Name, name)
                .SetProperty(organization => organization.Description, description)
                .SetProperty(organization => organization.Website, website)
                .SetProperty(organization => organization.Email, email)
                .SetProperty(organization => organization.Phone, phone)
                .SetProperty(organization => organization.AddressLine1, addressLine1)
                .SetProperty(organization => organization.AddressLine2, addressLine2)
                .SetProperty(organization => organization.City, city)
                .SetProperty(organization => organization.Country, country)
                .SetProperty(organization => organization.Postcode, postcode)
                .SetProperty(organization => organization.UpdatedAt, updatedAt)
                .SetProperty(organization => organization.UpdatedBy, actorId?.ToString()), cancellationToken);

        if (updated == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new OrganizationProfileUpdateResult(OrganizationProfileUpdateCode.ConcurrencyConflict,
                Error: "The organization changed after it was loaded. Reload before saving.");
        }

        var updatedProfile = current with
        {
            Name = name,
            Description = description,
            Website = website,
            Email = email,
            Phone = phone,
            AddressLine1 = addressLine1,
            AddressLine2 = addressLine2,
            City = city,
            Country = country,
            Postcode = postcode,
            UpdatedAt = updatedAt
        };
        var changedFields = new List<string>();
        if (!string.Equals(current.Name, updatedProfile.Name, StringComparison.Ordinal)) changedFields.Add(nameof(OrganizationEntity.Name));
        if (!string.Equals(current.Description, updatedProfile.Description, StringComparison.Ordinal)) changedFields.Add(nameof(OrganizationEntity.Description));
        if (!string.Equals(current.Website, updatedProfile.Website, StringComparison.Ordinal)) changedFields.Add(nameof(OrganizationEntity.Website));
        if (!string.Equals(current.Email, updatedProfile.Email, StringComparison.Ordinal)) changedFields.Add(nameof(OrganizationEntity.Email));
        if (!string.Equals(current.Phone, updatedProfile.Phone, StringComparison.Ordinal)) changedFields.Add(nameof(OrganizationEntity.Phone));
        if (!string.Equals(current.AddressLine1, updatedProfile.AddressLine1, StringComparison.Ordinal)) changedFields.Add(nameof(OrganizationEntity.AddressLine1));
        if (!string.Equals(current.AddressLine2, updatedProfile.AddressLine2, StringComparison.Ordinal)) changedFields.Add(nameof(OrganizationEntity.AddressLine2));
        if (!string.Equals(current.City, updatedProfile.City, StringComparison.Ordinal)) changedFields.Add(nameof(OrganizationEntity.City));
        if (!string.Equals(current.Country, updatedProfile.Country, StringComparison.Ordinal)) changedFields.Add(nameof(OrganizationEntity.Country));
        if (!string.Equals(current.Postcode, updatedProfile.Postcode, StringComparison.Ordinal)) changedFields.Add(nameof(OrganizationEntity.Postcode));
        if (changedFields.Count == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new OrganizationProfileUpdateResult(OrganizationProfileUpdateCode.Updated, current);
        }
        var oldValues = ProfileAuditValues(current);
        var newValues = ProfileAuditValues(updatedProfile);
        var audit = new AuditEntry
        {
            TenantId = tenantId,
            EntityType = nameof(OrganizationEntity),
            EntityId = id,
            Action = "Updated",
            ChangedFields = string.Join(",", changedFields),
            Description = "Organization profile settings updated.",
            OriginalValues = JsonSerializer.Serialize(oldValues.Where(pair => changedFields.Contains(pair.Key))
                .ToDictionary(pair => pair.Key, pair => pair.Value)),
            NewValues = JsonSerializer.Serialize(newValues.Where(pair => changedFields.Contains(pair.Key))
                .ToDictionary(pair => pair.Key, pair => pair.Value)),
            Changes = JsonSerializer.Serialize(changedFields.ToDictionary(field => field,
                field => (object)new { from = oldValues[field], to = newValues[field] })),
            Source = AuditSources.User,
            CorrelationId = _httpContextAccessor.HttpContext?.Request.Headers["X-Correlation-Id"].FirstOrDefault()
                ?? _httpContextAccessor.HttpContext?.TraceIdentifier,
            UserId = actorId,
            UserName = actor?.Identity?.Name,
            CreatedBy = actorId?.ToString() ?? actor?.Identity?.Name ?? "authenticated-user"
        };
        var auditResult = await _auditService.AppendAsync(audit, cancellationToken);
        if (auditResult.IsFailure)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new OrganizationProfileUpdateResult(OrganizationProfileUpdateCode.AuditFailed,
                Error: auditResult.Error?.Message ?? "The audit event could not be recorded.");
        }

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new OrganizationProfileUpdateResult(OrganizationProfileUpdateCode.Updated, updatedProfile);
    }

    private bool TryGetTenantId(out Guid tenantId)
    {
        tenantId = Guid.Empty;
        var principal = _httpContextAccessor.HttpContext?.User;
        return principal?.Identity?.IsAuthenticated == true
            && Guid.TryParse(principal.FindFirstValue("tenantId"), out tenantId)
            && tenantId != Guid.Empty;
    }

    private static OrganizationProfileDto ToProfile(OrganizationEntity organization) => new(
        organization.Id,
        organization.Name,
        organization.Description,
        organization.Website,
        organization.Email,
        organization.Phone,
        organization.AddressLine1,
        organization.AddressLine2,
        organization.City,
        organization.Country,
        organization.Postcode,
        organization.UpdatedAt);

    private static Dictionary<string, object?> ProfileAuditValues(OrganizationProfileDto profile) => new()
    {
        [nameof(OrganizationEntity.Name)] = profile.Name,
        [nameof(OrganizationEntity.Description)] = profile.Description,
        [nameof(OrganizationEntity.Website)] = profile.Website,
        [nameof(OrganizationEntity.Email)] = profile.Email,
        [nameof(OrganizationEntity.Phone)] = profile.Phone,
        [nameof(OrganizationEntity.AddressLine1)] = profile.AddressLine1,
        [nameof(OrganizationEntity.AddressLine2)] = profile.AddressLine2,
        [nameof(OrganizationEntity.City)] = profile.City,
        [nameof(OrganizationEntity.Country)] = profile.Country,
        [nameof(OrganizationEntity.Postcode)] = profile.Postcode
    };

    private static string? TrimOrNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
