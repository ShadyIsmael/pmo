using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using FluentValidation;
using System.Security.Claims;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.Organization.Domain;
using NEXUS.Modules.ResourceManagement.Application.DTOs;
using NEXUS.Modules.ResourceManagement.Application.Mapping;
using NEXUS.Modules.ResourceManagement.Application.Validators;
using NEXUS.Modules.ResourceManagement.Domain;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Helpers;

namespace NEXUS.Modules.ResourceManagement.Application;

public class ResourceService : IResourceService, IResourceReferenceLookup, IResourceIdentityLinker
{
    private readonly DbContext _context;
    private readonly IAuditService _auditService;
    private readonly IUserReferenceLookup _userLookup;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IValidator<CreateResourceDto> _createValidator;
    private readonly IValidator<UpdateResourceDto> _updateValidator;

    public ResourceService(
        DbContext context,
        IAuditService auditService,
        IUserReferenceLookup userLookup,
        IHttpContextAccessor httpContextAccessor,
        IValidator<CreateResourceDto> createValidator,
        IValidator<UpdateResourceDto> updateValidator)
    {
        _context = context;
        _auditService = auditService;
        _userLookup = userLookup;
        _httpContextAccessor = httpContextAccessor;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    public Task<bool> ExistsAsync(Guid resourceId, CancellationToken cancellationToken = default)
    {
        return _context.Set<Resource>().AnyAsync(resource => resource.Id == resourceId, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, string>> GetNamesAsync(IEnumerable<Guid> resourceIds, CancellationToken cancellationToken = default)
    {
        var ids = resourceIds.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<Guid, string>();
        return await _context.Set<Resource>()
            .Where(resource => ids.Contains(resource.Id))
            .ToDictionaryAsync(resource => resource.Id, resource => resource.Name, cancellationToken);
    }

    public Task<ResourceReference?> FindAsync(Guid resourceId, CancellationToken cancellationToken = default) =>
        _context.Set<Resource>()
            .Where(resource => resource.Id == resourceId && !resource.IsDeleted)
            .Select(resource => new ResourceReference(resource.Id, resource.UserId == Guid.Empty ? null : resource.UserId, resource.Name, resource.Email, resource.JobTitle, resource.Type, resource.Status, resource.IsActive))
            .FirstOrDefaultAsync(cancellationToken);

    public Task<ResourceReference?> FindByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        _context.Set<Resource>()
            .Where(resource => resource.UserId == userId && !resource.IsDeleted)
            // A user can have historical/inactive Resource records.  Reference
            // lookups used for self-service must resolve the current active
            // record first instead of relying on database row order.
            .OrderByDescending(resource => resource.IsActive)
            .ThenByDescending(resource => resource.UpdatedAt ?? resource.CreatedAt)
            .Select(resource => new ResourceReference(resource.Id, resource.UserId == Guid.Empty ? null : resource.UserId, resource.Name, resource.Email, resource.JobTitle, resource.Type, resource.Status, resource.IsActive))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<Result<ResourceReference>> LinkUserAsync(Guid resourceId, Guid userId, string actorId, CancellationToken cancellationToken = default)
    {
        var resource = await _context.Set<Resource>().FirstOrDefaultAsync(item => item.Id == resourceId && !item.IsDeleted, cancellationToken);
        if (resource is null) return Result<ResourceReference>.Failure(Error.NotFound("Resource not found"));
        if (!resource.IsActive || string.Equals(resource.Status, "Inactive", StringComparison.OrdinalIgnoreCase))
            return Result<ResourceReference>.Failure(Error.Validation("The Resource is inactive"));
        if (resource.UserId.HasValue && resource.UserId.Value != userId)
            return Result<ResourceReference>.Failure(Error.Conflict("The Resource is already linked to another system user"));

        var existing = await _context.Set<Resource>()
            .AnyAsync(item => item.Id != resourceId && item.UserId == userId && item.IsActive && !item.IsDeleted, cancellationToken);
        if (existing) return Result<ResourceReference>.Failure(Error.Conflict("This system user is already linked to an active Resource"));

        resource.UserId = userId;
        resource.UpdatedBy = actorId;
        await _context.SaveChangesAsync(cancellationToken);
        return Result<ResourceReference>.Success(new ResourceReference(resource.Id, resource.UserId, resource.Name, resource.Email, resource.JobTitle, resource.Type, resource.Status, resource.IsActive));
    }

    public async Task<IReadOnlyDictionary<Guid, ResourceReference>> GetDetailsAsync(IEnumerable<Guid> resourceIds, CancellationToken cancellationToken = default)
    {
        var ids = resourceIds.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<Guid, ResourceReference>();
        return await _context.Set<Resource>()
            .Where(resource => ids.Contains(resource.Id) && !resource.IsDeleted)
            .Select(resource => new ResourceReference(resource.Id, resource.UserId == Guid.Empty ? null : resource.UserId, resource.Name, resource.Email, resource.JobTitle, resource.Type, resource.Status, resource.IsActive))
            .ToDictionaryAsync(resource => resource.Id, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, ResourceReference>> GetDetailsByUserIdsAsync(IEnumerable<Guid> userIds, CancellationToken cancellationToken = default)
    {
        var ids = userIds.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<Guid, ResourceReference>();
        return await _context.Set<Resource>()
            .Where(resource => resource.UserId.HasValue && resource.UserId != Guid.Empty && ids.Contains(resource.UserId.Value) && !resource.IsDeleted)
            .Select(resource => new ResourceReference(resource.Id, resource.UserId == Guid.Empty ? null : resource.UserId, resource.Name, resource.Email, resource.JobTitle, resource.Type, resource.Status, resource.IsActive))
            .ToDictionaryAsync(resource => resource.UserId!.Value, cancellationToken);
    }

    public async Task<Result<ResourceDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var resource = await _context.Set<Resource>()
            .Include(r => r.Skills).ThenInclude(rs => rs.Skill)
            .Include(r => r.Allocations)
            .Include(r => r.Vendor)
            .Include(r => r.BaseCalendar)
            .FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted, cancellationToken);

        if (resource is null)
            return Result<ResourceDto>.Failure(Error.NotFound("Resource not found"));

        var detailDto = resource.ToDto();
        await EnrichOrganizationAsync(new[] { detailDto }, cancellationToken);
        return Result<ResourceDto>.Success(detailDto);
    }

    public async Task<PagedResult<ResourceDto>> GetPagedAsync(ResourceFilterDto filter, CancellationToken cancellationToken = default)
    {
        var query = _context.Set<Resource>()
            .Include(r => r.Skills).ThenInclude(rs => rs.Skill)
            .Include(r => r.Vendor)
            .AsQueryable();

        query = query.Where(r => !r.IsDeleted);

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.ToLower();
            query = query.Where(r =>
                r.Name.ToLower().Contains(search) ||
                (r.Email != null && r.Email.ToLower().Contains(search)) ||
                (r.JobTitle != null && r.JobTitle.ToLower().Contains(search)));
        }

        if (!string.IsNullOrWhiteSpace(filter.Status))
            query = query.Where(r => r.Status == filter.Status);
        if (!string.IsNullOrWhiteSpace(filter.Type))
        {
            var storedType = ResourceTypeConstants.TryGetKnownStoredCode(filter.Type, out var canonicalType)
                ? canonicalType
                : filter.Type;
            query = query.Where(r => r.Type == storedType);
        }
        if (filter.UserId.HasValue)
            query = query.Where(r => r.UserId == filter.UserId);
        if (filter.DepartmentId.HasValue)
            query = query.Where(r => r.DepartmentId == filter.DepartmentId);
        if (filter.LocationId.HasValue)
            query = query.Where(r => r.LocationId == filter.LocationId);
        if (filter.BusinessUnitId.HasValue)
            query = query.Where(r => r.BusinessUnitId == filter.BusinessUnitId);
        if (filter.OrganizationalUnitId.HasValue)
            query = query.Where(r => _context.Set<NEXUS.Modules.Organization.Domain.ResourceOrganizationalUnit>().Any(membership => membership.ResourceId == r.Id && membership.OrganizationalUnitId == filter.OrganizationalUnitId.Value && membership.IsPrimary && membership.IsActive && (membership.EffectiveTo == null || membership.EffectiveTo > DateTime.UtcNow)));
        if (filter.VendorId.HasValue)
            query = query.Where(r => r.VendorId == filter.VendorId);
        if (filter.IsActive.HasValue)
            query = query.Where(r => r.IsActive == filter.IsActive.Value);

        query = (filter.SortBy?.ToLower()) switch
        {
            "name" => filter.SortDirection == "desc" ? query.OrderByDescending(r => r.Name) : query.OrderBy(r => r.Name),
            "type" => filter.SortDirection == "desc" ? query.OrderByDescending(r => r.Type) : query.OrderBy(r => r.Type),
            "status" => filter.SortDirection == "desc" ? query.OrderByDescending(r => r.Status) : query.OrderBy(r => r.Status),
            "createdat" => filter.SortDirection == "desc" ? query.OrderByDescending(r => r.CreatedAt) : query.OrderBy(r => r.CreatedAt),
            _ => query.OrderBy(r => r.Name)
        };

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken);

        var resultItems = items.Select(r => r.ToDto()).ToList();
        await EnrichOrganizationAsync(resultItems, cancellationToken);
        return new PagedResult<ResourceDto>(resultItems, filter.Page, filter.PageSize, total);
    }

    public async Task<IReadOnlyList<ResourceBaseCalendarDto>> GetBaseCalendarsAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Set<NEXUS.Modules.Organization.Domain.WorkingCalendar>()
            .Where(calendar => !calendar.IsDeleted)
            .OrderByDescending(calendar => calendar.IsDefault)
            .ThenBy(calendar => calendar.Name)
            .Select(calendar => new ResourceBaseCalendarDto
            {
                Id = calendar.Id,
                Name = calendar.Name,
                IsDefault = calendar.IsDefault,
            })
            .ToListAsync(cancellationToken);
    }

    public Task<ResourceReferenceDataDto> GetReferenceDataAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var resourceTypes = Enum.GetValues<ResourceType>()
            .Select(type => new ResourceTypeOptionDto(
                type.ToString(),
                type.ToString(),
                type switch
                {
                    ResourceType.Staff => "موظف",
                    ResourceType.Vendor => "مورد",
                    _ => type.ToString()
                },
                (int)type + 1,
                type == ResourceType.Staff,
                type == ResourceType.Vendor))
            .ToArray();

        var seniorityLevels = Enum.GetValues<ResourceSeniorityLevel>()
            .Select(level => new ResourceSeniorityOptionDto(
                level.ToString(),
                level == ResourceSeniorityLevel.Mid ? "Mid-Level" : level.ToString(),
                level switch
                {
                    ResourceSeniorityLevel.Junior => "مبتدئ",
                    ResourceSeniorityLevel.Mid => "متوسط",
                    ResourceSeniorityLevel.Senior => "خبير",
                    ResourceSeniorityLevel.Lead => "قائد",
                    ResourceSeniorityLevel.Principal => "خبير رئيسي",
                    _ => level.ToString()
                },
                (int)level + 1))
            .ToArray();

        return Task.FromResult(new ResourceReferenceDataDto
        {
            ResourceTypes = resourceTypes,
            SeniorityLevels = seniorityLevels,
            CompensationTypes = CompensationType.Values
        });
    }

    public async Task<Result<ResourceDto>> CreateAsync(CreateResourceDto dto, string userId, CancellationToken cancellationToken = default)
    {
        var inputValidation = await _createValidator.ValidateAsync(dto, cancellationToken);
        if (!inputValidation.IsValid)
            return Result<ResourceDto>.Failure(Error.Validation(
                string.Join("; ", inputValidation.Errors.Select(error => $"{error.PropertyName}: {error.ErrorMessage}")),
                inputValidation.Errors.FirstOrDefault()?.PropertyName));

        var identityValidation = await ValidateIdentityAsync(dto.UserId, null, dto.Email, null, cancellationToken);
        if (identityValidation.IsFailure) return Result<ResourceDto>.Failure(identityValidation.Error!);

        if (!Enum.IsDefined(dto.Type))
            return Result<ResourceDto>.Failure(Error.Validation("Select a supported resource type.", nameof(dto.Type)));

        if (dto.Type == ResourceType.Staff && dto.VendorId.HasValue)
            return Result<ResourceDto>.Failure(Error.Validation("Staff resources cannot reference a Vendor.", nameof(dto.Type)));

        if (dto.Type == ResourceType.Vendor && !dto.VendorId.HasValue)
            return Result<ResourceDto>.Failure(Error.Validation("Vendor resources require a Vendor.", nameof(dto.Type)));

        if (dto.VendorId.HasValue)
        {
            var vendor = await _context.Set<Vendor>().FirstOrDefaultAsync(v => v.Id == dto.VendorId.Value && !v.IsDeleted, cancellationToken);
            if (vendor is null)
                return Result<ResourceDto>.Failure(Error.Validation("The specified Vendor does not exist"));
            if (!vendor.IsActive)
                return Result<ResourceDto>.Failure(Error.Validation("The specified Vendor is inactive and cannot be selected for new resources"));
        }

        if (dto.BaseCalendarId.HasValue)
        {
            var calendarExists = await _context.Set<NEXUS.Modules.Organization.Domain.WorkingCalendar>().AnyAsync(c => c.Id == dto.BaseCalendarId.Value && !c.IsDeleted, cancellationToken);
            if (!calendarExists)
                return Result<ResourceDto>.Failure(Error.Validation("The specified Base Calendar does not exist"));
        }

        var organizationValidation = await ValidateOrganizationalUnitAsync(dto.PrimaryOrganizationalUnitId, cancellationToken);
        if (organizationValidation.IsFailure)
            return Result<ResourceDto>.Failure(organizationValidation.Error!);

        var resource = dto.ToEntity();
        resource.TenantId = CurrentTenant();
        resource.CreatedBy = userId;

        if (dto.Skills.Count != 0)
        {
            foreach (var skillDto in dto.Skills)
            {
                var skillExists = await _context.Set<Skill>().AnyAsync(s => s.Id == skillDto.SkillId && !s.IsDeleted, cancellationToken);
                if (!skillExists)
                    return Result<ResourceDto>.Failure(Error.Validation($"Skill with ID {skillDto.SkillId} not found"));

                resource.Skills.Add(new ResourceSkill
                {
                    ResourceId = resource.Id,
                    SkillId = skillDto.SkillId,
                    Proficiency = skillDto.Proficiency,
                    YearsOfExperience = skillDto.YearsOfExperience,
                    CreatedBy = userId
                });
            }
        }

        _context.Set<Resource>().Add(resource);
        await _context.SaveChangesAsync(cancellationToken);

        if (dto.PrimaryOrganizationalUnitId.HasValue)
        {
            var organizationResult = await SetPrimaryOrganizationalUnitAsync(resource.Id, dto.PrimaryOrganizationalUnitId.Value, userId, cancellationToken);
            if (organizationResult.IsFailure) return Result<ResourceDto>.Failure(organizationResult.Error!);
        }

        await _auditService.LogAsync(new AuditEntry
        {
            EntityType = "Resource",
            EntityId = resource.Id,
            Action = "Create",
            UserId = Guid.Parse(userId),
            Timestamp = DateTime.UtcNow,
            NewValues = System.Text.Json.JsonSerializer.Serialize(dto),
            CreatedBy = userId
        }, cancellationToken);

        var created = await _context.Set<Resource>()
            .Include(r => r.Skills).ThenInclude(rs => rs.Skill)
            .FirstAsync(r => r.Id == resource.Id, cancellationToken);

        var createdDto = created.ToDto();
        await EnrichOrganizationAsync(new[] { createdDto }, cancellationToken);
        return Result<ResourceDto>.Success(createdDto);
    }

    public async Task<Result<ResourceDto>> UpdateAsync(Guid id, UpdateResourceDto dto, string userId, CancellationToken cancellationToken = default)
    {
        var inputValidation = await _updateValidator.ValidateAsync(dto, cancellationToken);
        if (!inputValidation.IsValid)
            return Result<ResourceDto>.Failure(Error.Validation(
                string.Join("; ", inputValidation.Errors.Select(error => $"{error.PropertyName}: {error.ErrorMessage}")),
                inputValidation.Errors.FirstOrDefault()?.PropertyName));

        var resource = await _context.Set<Resource>()
            .Include(r => r.Skills).ThenInclude(rs => rs.Skill)
            .FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted, cancellationToken);

        if (resource is null)
            return Result<ResourceDto>.Failure(Error.NotFound("Resource not found"));

        var associationChanged = dto.RemoveUserAssociation == true || dto.UserId.HasValue;
        var requestedUserId = dto.RemoveUserAssociation == true ? null : dto.UserId;
        var userToValidate = associationChanged ? requestedUserId : dto.IsActive == true ? resource.UserId : null;
        var identityValidation = await ValidateIdentityAsync(userToValidate, resource.Id, dto.Email, resource.Email, cancellationToken);
        if (identityValidation.IsFailure) return Result<ResourceDto>.Failure(identityValidation.Error!);

        var originalValues = System.Text.Json.JsonSerializer.Serialize(new
        {
            resource.Name, resource.JobTitle, resource.Email, resource.Type, resource.Status,
            resource.DepartmentId, resource.LocationId
        });

        if (dto.Type.HasValue && !Enum.IsDefined(dto.Type.Value))
            return Result<ResourceDto>.Failure(Error.Validation("Select a supported resource type.", nameof(dto.Type)));

        if (dto.Type == ResourceType.Staff && (dto.VendorId ?? resource.VendorId).HasValue)
            return Result<ResourceDto>.Failure(Error.Validation("Staff resources cannot reference a Vendor.", nameof(dto.Type)));

        if (dto.Type == ResourceType.Vendor && dto.VendorId == null && resource.VendorId == null)
            return Result<ResourceDto>.Failure(Error.Validation("Vendor resources require a Vendor.", nameof(dto.Type)));

        if (dto.VendorId.HasValue)
        {
            var vendor = await _context.Set<Vendor>().FirstOrDefaultAsync(v => v.Id == dto.VendorId.Value && !v.IsDeleted, cancellationToken);
            if (vendor is null)
                return Result<ResourceDto>.Failure(Error.Validation("The specified Vendor does not exist"));
            // An existing VendorResource may legitimately retain a historical
            // reference to an inactive Vendor. Only a new assignment must be
            // active; changing to a different inactive Vendor is rejected.
            if (!vendor.IsActive && resource.VendorId != dto.VendorId.Value)
                return Result<ResourceDto>.Failure(Error.Validation("The specified Vendor is inactive and cannot be selected"));
        }

        if (dto.BaseCalendarId.HasValue)
        {
            var calendarExists = await _context.Set<NEXUS.Modules.Organization.Domain.WorkingCalendar>().AnyAsync(c => c.Id == dto.BaseCalendarId.Value && !c.IsDeleted, cancellationToken);
            if (!calendarExists)
                return Result<ResourceDto>.Failure(Error.Validation("The specified Base Calendar does not exist"));
        }

        var organizationValidation = await ValidateOrganizationalUnitAsync(dto.PrimaryOrganizationalUnitId, cancellationToken);
        if (organizationValidation.IsFailure)
            return Result<ResourceDto>.Failure(organizationValidation.Error!);

        resource.ApplyUpdate(dto);
        resource.UpdatedBy = userId;

        if (dto.Skills != null)
        {
            _context.Set<ResourceSkill>().RemoveRange(resource.Skills);
            resource.Skills.Clear();

            foreach (var skillDto in dto.Skills)
            {
                resource.Skills.Add(new ResourceSkill
                {
                    ResourceId = resource.Id,
                    SkillId = skillDto.SkillId,
                    Proficiency = skillDto.Proficiency,
                    YearsOfExperience = skillDto.YearsOfExperience,
                    CreatedBy = userId
                });
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        if (dto.PrimaryOrganizationalUnitId.HasValue)
        {
            var organizationResult = await SetPrimaryOrganizationalUnitAsync(resource.Id, dto.PrimaryOrganizationalUnitId.Value, userId, cancellationToken);
            if (organizationResult.IsFailure) return Result<ResourceDto>.Failure(organizationResult.Error!);
        }

        await _auditService.LogAsync(new AuditEntry
        {
            EntityType = "Resource",
            EntityId = resource.Id,
            Action = "Update",
            UserId = Guid.Parse(userId),
            Timestamp = DateTime.UtcNow,
            OriginalValues = originalValues,
            NewValues = System.Text.Json.JsonSerializer.Serialize(dto),
            CreatedBy = userId
        }, cancellationToken);

        var updatedDto = resource.ToDto();
        await EnrichOrganizationAsync(new[] { updatedDto }, cancellationToken);
        return Result<ResourceDto>.Success(updatedDto);
    }

    public async Task<Result> DeleteAsync(Guid id, string userId, CancellationToken cancellationToken = default)
    {
        var resource = await _context.Set<Resource>()
            .FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted, cancellationToken);

        if (resource is null)
            return Result.Failure(Error.NotFound("Resource not found"));

        resource.IsActive = false;
        resource.Status = "Inactive";
        resource.UpdatedBy = userId;
        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(new AuditEntry
        {
            EntityType = "Resource",
            EntityId = resource.Id,
            Action = "Deactivate",
            UserId = Guid.Parse(userId),
            Timestamp = DateTime.UtcNow,
            CreatedBy = userId
        }, cancellationToken);

        return Result.Success();
    }

    private async Task<Result> ValidateIdentityAsync(Guid? userId, Guid? excludeResourceId, string? email, string? existingEmail, CancellationToken cancellationToken)
    {
        if (userId.HasValue)
        {
            if (userId.Value == Guid.Empty) return Result.Failure(Error.Validation("User association must reference a valid system user"));
            if (await _userLookup.FindAsync(userId.Value, cancellationToken) is null)
                return Result.Failure(Error.Validation("The selected system user does not exist or is inactive"));

            var linked = await _context.Set<Resource>().AnyAsync(resource =>
                resource.Id != (excludeResourceId ?? Guid.Empty) &&
                resource.UserId == userId.Value && resource.IsActive && !resource.IsDeleted,
                cancellationToken);
            if (linked) return Result.Failure(Error.Conflict("This system user is already linked to an active Resource"));
        }

        var normalizedEmail = NormalizeEmail(email);
        if (!string.IsNullOrEmpty(normalizedEmail))
        {
            var currentNormalized = NormalizeEmail(existingEmail);
            if (!string.Equals(normalizedEmail, currentNormalized, StringComparison.Ordinal))
            {
                var duplicateEmail = await _context.Set<Resource>().AnyAsync(resource =>
                    resource.Id != (excludeResourceId ?? Guid.Empty) &&
                    resource.IsActive && !resource.IsDeleted && resource.Email != null &&
                    resource.Email.Trim().ToUpper() == normalizedEmail,
                    cancellationToken);
                if (duplicateEmail) return Result.Failure(Error.Conflict("An active Resource with this email already exists"));
            }
        }

        return Result.Success();
    }

    private static string? NormalizeEmail(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();

    private async Task<Result> ValidateOrganizationalUnitAsync(Guid? organizationalUnitId, CancellationToken cancellationToken)
    {
        if (!organizationalUnitId.HasValue) return Result.Success();
        var exists = await _context.Set<OrganizationalUnit>()
            .AnyAsync(unit => unit.Id == organizationalUnitId.Value && unit.IsActive, cancellationToken);
        return exists
            ? Result.Success()
            : Result.Failure(Error.Validation("The selected organizational unit does not exist or is inactive"));
    }

    private async Task<Result> SetPrimaryOrganizationalUnitAsync(Guid resourceId, Guid organizationalUnitId, string userId, CancellationToken cancellationToken)
    {
        var unit = await _context.Set<OrganizationalUnit>().FirstOrDefaultAsync(item => item.Id == organizationalUnitId && item.IsActive, cancellationToken);
        if (unit is null) return Result.Failure(Error.Validation("The selected organizational unit does not exist or is inactive"));
        var now = DateTime.UtcNow;
        var current = await _context.Set<ResourceOrganizationalUnit>().Where(item => item.ResourceId == resourceId && item.IsPrimary && item.IsActive && (item.EffectiveTo == null || item.EffectiveTo > now)).ToListAsync(cancellationToken);
        foreach (var membership in current) { membership.EffectiveTo = now; membership.IsActive = false; membership.UpdatedBy = userId; }
        _context.Set<ResourceOrganizationalUnit>().Add(new ResourceOrganizationalUnit { TenantId = CurrentTenant(), ResourceId = resourceId, OrganizationalUnitId = organizationalUnitId, IsPrimary = true, EffectiveFrom = now, CreatedBy = userId });
        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private async Task EnrichOrganizationAsync(IReadOnlyList<ResourceDto> resources, CancellationToken cancellationToken)
    {
        var ids = resources.Select(resource => resource.Id).ToHashSet();
        if (ids.Count == 0) return;
        var memberships = await _context.Set<ResourceOrganizationalUnit>()
            .Include(membership => membership.OrganizationalUnit)
            .Where(membership => ids.Contains(membership.ResourceId) && membership.IsPrimary && membership.IsActive && (membership.EffectiveTo == null || membership.EffectiveTo > DateTime.UtcNow))
            .ToListAsync(cancellationToken);
        var reporting = await _context.Set<ResourceReportingLine>()
            .Where(line => ids.Contains(line.ResourceId) && line.IsPrimary && line.IsActive && (line.EffectiveTo == null || line.EffectiveTo > DateTime.UtcNow))
            .ToListAsync(cancellationToken);
        var managerIds = reporting.Select(line => line.ManagerResourceId).Distinct().ToList();
        var managers = await _context.Set<Resource>().Where(resource => managerIds.Contains(resource.Id)).ToDictionaryAsync(resource => resource.Id, resource => resource.Name, cancellationToken);
        foreach (var resource in resources)
        {
            var membership = memberships.FirstOrDefault(item => item.ResourceId == resource.Id);
            if (membership is not null)
            {
                resource.PrimaryOrganizationalUnitId = membership.OrganizationalUnitId;
                resource.PrimaryOrganizationalUnitName = membership.OrganizationalUnit.Name;
                resource.PrimaryOrganizationalUnitCode = membership.OrganizationalUnit.Code;
            }
            var line = reporting.FirstOrDefault(item => item.ResourceId == resource.Id);
            if (line is not null)
            {
                resource.PrimaryManagerResourceId = line.ManagerResourceId;
                resource.PrimaryManagerResourceName = managers.TryGetValue(line.ManagerResourceId, out var name) ? name : null;
            }
        }
    }

    private Guid CurrentTenant() =>
        Guid.TryParse(_httpContextAccessor.HttpContext?.User.FindFirstValue("tenantId"), out var tenantId)
            ? tenantId
            : TenantConstants.DemoTenantId;
}
