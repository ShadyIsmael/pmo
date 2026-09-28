using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.ResourceManagement.Application.DTOs;
using NEXUS.Modules.ResourceManagement.Application.Mapping;
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

    public ResourceService(
        DbContext context,
        IAuditService auditService,
        IUserReferenceLookup userLookup,
        IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _auditService = auditService;
        _userLookup = userLookup;
        _httpContextAccessor = httpContextAccessor;
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
            .Include(r => r.CostProfiles.Where(cp => !cp.IsDeleted))
            .FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted, cancellationToken);

        if (resource is null)
            return Result<ResourceDto>.Failure(Error.NotFound("Resource not found"));

        return Result<ResourceDto>.Success(resource.ToDto());
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
            query = query.Where(r => r.Type == filter.Type);
        if (filter.UserId.HasValue)
            query = query.Where(r => r.UserId == filter.UserId);
        if (filter.DepartmentId.HasValue)
            query = query.Where(r => r.DepartmentId == filter.DepartmentId);
        if (filter.LocationId.HasValue)
            query = query.Where(r => r.LocationId == filter.LocationId);
        if (filter.BusinessUnitId.HasValue)
            query = query.Where(r => r.BusinessUnitId == filter.BusinessUnitId);
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

        return new PagedResult<ResourceDto>(items.Select(r => r.ToDto()).ToList(), filter.Page, filter.PageSize, total);
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

    public async Task<Result<ResourceDto>> CreateAsync(CreateResourceDto dto, string userId, CancellationToken cancellationToken = default)
    {
        var identityValidation = await ValidateIdentityAsync(dto.UserId, null, dto.Email, null, cancellationToken);
        if (identityValidation.IsFailure) return Result<ResourceDto>.Failure(identityValidation.Error!);

        if (!ResourceTypeConstants.IsValid(dto.Type))
            return Result<ResourceDto>.Failure(Error.Validation("Invalid resource type. Must be 'Staff' or 'VendorResource'"));

        if (dto.Type == ResourceTypeConstants.Staff && dto.VendorId.HasValue)
            return Result<ResourceDto>.Failure(Error.Validation("Staff resources cannot reference a Vendor"));

        if (dto.Type == ResourceTypeConstants.VendorResource && !dto.VendorId.HasValue)
            return Result<ResourceDto>.Failure(Error.Validation("VendorResource requires a Vendor"));

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

        return Result<ResourceDto>.Success(created.ToDto());
    }

    public async Task<Result<ResourceDto>> UpdateAsync(Guid id, UpdateResourceDto dto, string userId, CancellationToken cancellationToken = default)
    {
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

        if (dto.Type != null && !ResourceTypeConstants.IsValid(dto.Type))
            return Result<ResourceDto>.Failure(Error.Validation("Invalid resource type. Must be 'Staff' or 'VendorResource'"));

        if (dto.Type == ResourceTypeConstants.Staff && dto.VendorId.HasValue)
            return Result<ResourceDto>.Failure(Error.Validation("Staff resources cannot reference a Vendor"));

        if (dto.Type == ResourceTypeConstants.VendorResource && dto.VendorId == null && resource.VendorId == null)
            return Result<ResourceDto>.Failure(Error.Validation("VendorResource requires a Vendor"));

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

        return Result<ResourceDto>.Success(resource.ToDto());
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

    private Guid CurrentTenant() =>
        Guid.TryParse(_httpContextAccessor.HttpContext?.User.FindFirstValue("tenantId"), out var tenantId)
            ? tenantId
            : TenantConstants.DemoTenantId;
}
