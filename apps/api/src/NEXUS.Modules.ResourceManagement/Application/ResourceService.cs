using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using FluentValidation;
using System.Security.Claims;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.Identity.Domain;
using NEXUS.Modules.Organization.Domain;
using NEXUS.Modules.ResourceManagement.Application.DTOs;
using NEXUS.Modules.ResourceManagement.Application.Mapping;
using NEXUS.Modules.ResourceManagement.Application.Validators;
using NEXUS.Modules.ResourceManagement.Domain;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Helpers;
using NEXUS.SharedKernel.Management;

namespace NEXUS.Modules.ResourceManagement.Application;

public class ResourceService : IResourceService, IResourceReferenceLookup, IResourceIdentityLinker
{
    private readonly DbContext _context;
    private readonly IAuditService _auditService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IValidator<CreateResourceDto> _createValidator;
    private readonly IValidator<UpdateResourceDto> _updateValidator;
    private readonly IManagementContextAccessor? _managementContextAccessor;

    public ResourceService(
        DbContext context,
        IAuditService auditService,
        IUserReferenceLookup userLookup,
        IHttpContextAccessor httpContextAccessor,
        IValidator<CreateResourceDto> createValidator,
        IValidator<UpdateResourceDto> updateValidator,
        IManagementContextAccessor? managementContextAccessor = null)
    {
        _context = context;
        _auditService = auditService;
        _httpContextAccessor = httpContextAccessor;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _managementContextAccessor = managementContextAccessor;
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

    public Task<ResourceReference?> FindInTenantAsync(Guid resourceId, Guid tenantId, CancellationToken cancellationToken = default) =>
        _context.Set<Resource>()
            .Where(resource => resource.Id == resourceId && resource.TenantId == tenantId && !resource.IsDeleted)
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

        var user = await _context.Set<User>().AsNoTracking().FirstOrDefaultAsync(item => item.Id == userId
            && item.TenantId == resource.TenantId && item.IsActive && !item.IsDeleted, cancellationToken);
        if (user is null) return Result<ResourceReference>.Failure(Error.Validation("The system user must be active in the Resource tenant"));

        var existing = await _context.Set<Resource>()
            .AnyAsync(item => item.Id != resourceId && item.UserId == userId && !item.IsDeleted, cancellationToken);
        if (existing) return Result<ResourceReference>.Failure(Error.Conflict("This system user is already linked to a Resource"));

        if (IsStaffResourceType(resource.Type) && !HasValidStaffIdentity(user))
            return Result<ResourceReference>.Failure(Error.Validation(
                "The linked system user's name and email must fit the Resource identity fields.", nameof(userId)));

        resource.UserId = userId;
        if (IsStaffResourceType(resource.Type)) ApplyStaffIdentity(resource, user);
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

    public async Task<IReadOnlyDictionary<Guid, ResourceReference>> GetDetailsByUserIdsInTenantAsync(IEnumerable<Guid> userIds, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var ids = userIds.Distinct().ToList();
        if (tenantId == Guid.Empty || ids.Count == 0) return new Dictionary<Guid, ResourceReference>();
        return await _context.Set<Resource>()
            .Where(resource => resource.TenantId == tenantId && resource.UserId.HasValue && resource.UserId != Guid.Empty
                && ids.Contains(resource.UserId.Value) && !resource.IsDeleted)
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
        await EnrichStaffIdentityAsync(new[] { detailDto }, resource.TenantId, cancellationToken);
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
        if (items.Count != 0)
            await EnrichStaffIdentityAsync(resultItems, items[0].TenantId, cancellationToken);
        await EnrichOrganizationAsync(resultItems, cancellationToken);
        return new PagedResult<ResourceDto>(resultItems, filter.Page, filter.PageSize, total);
    }

    private sealed record WorkingCalendarResolution(WorkingCalendar? Calendar, Error? Error);

    private async Task<WorkingCalendarResolution> ResolveWorkingCapacityCalendarAsync(
        Guid? baseCalendarId,
        CancellationToken cancellationToken)
    {
        var calendars = _context.Set<WorkingCalendar>()
            .AsNoTracking()
            .Include(calendar => calendar.DayRules)
            .Where(calendar => calendar.TenantId == CurrentTenant() && !calendar.IsDeleted);

        if (baseCalendarId.HasValue)
        {
            var selected = await calendars.FirstOrDefaultAsync(
                calendar => calendar.Id == baseCalendarId.Value,
                cancellationToken);
            if (selected is null)
                return new WorkingCalendarResolution(null, Error.BusinessValidation(
                    "RESOURCE_WORKING_CALENDAR_UNAVAILABLE",
                    "The selected working calendar is unavailable in this tenant. Choose an available calendar or leave the field empty to use the tenant default.",
                    nameof(CreateResourceDto.BaseCalendarId)));
            if (!selected.HasValidSchedule)
                return new WorkingCalendarResolution(null, Error.BusinessValidation(
                    "RESOURCE_WORKING_CALENDAR_INVALID",
                    "The selected working calendar has no valid schedule. Ask an administrator to correct it or choose another calendar.",
                    nameof(CreateResourceDto.BaseCalendarId)));
            return new WorkingCalendarResolution(selected, null);
        }

        var defaults = await calendars
            .Where(calendar => calendar.IsDefault)
            .ToListAsync(cancellationToken);
        if (defaults.Count == 0)
            return new WorkingCalendarResolution(null, Error.BusinessValidation(
                "RESOURCE_WORKING_CALENDAR_DEFAULT_MISSING",
                "No valid tenant Standard working calendar is configured. Ask a tenant administrator to configure one, then try again.",
                nameof(CreateResourceDto.BaseCalendarId)));
        if (defaults.Count != 1 || !defaults[0].HasValidSchedule)
            return new WorkingCalendarResolution(null, Error.BusinessValidation(
                "RESOURCE_WORKING_CALENDAR_DEFAULT_INVALID",
                "The tenant Standard working calendar is incomplete or ambiguous. Ask a tenant administrator to repair it or select another calendar.",
                nameof(CreateResourceDto.BaseCalendarId)));

        return new WorkingCalendarResolution(defaults[0], null);
    }

    public async Task<IReadOnlyList<ResourceBaseCalendarDto>> GetBaseCalendarsAsync(CancellationToken cancellationToken = default)
    {
        var calendars = await _context.Set<NEXUS.Modules.Organization.Domain.WorkingCalendar>()
            .AsNoTracking()
            .Include(calendar => calendar.DayRules)
            .Include(calendar => calendar.Holidays)
            .Where(calendar => calendar.TenantId == CurrentTenant() && !calendar.IsDeleted)
            .OrderByDescending(calendar => calendar.IsDefault)
            .ThenBy(calendar => calendar.Name)
            .ToListAsync(cancellationToken);

        return calendars
            .Select(calendar => new ResourceBaseCalendarDto
            {
                Id = calendar.Id,
                Name = calendar.Name,
                IsDefault = calendar.IsDefault,
                IsConfigured = calendar.HasValidSchedule,
                WorkingDaysPerWeek = ResourceWorkingCapacityCalculator.GetWorkingDaysPerWeek(calendar)
            })
            .ToList();
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
                    ResourceType.Outsourced => "متعاقد خارجي",
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
        var actorError = ResourceManagementWriteGuard.RequireActorContext(_httpContextAccessor, _managementContextAccessor, userId);
        if (actorError is not null) return Result<ResourceDto>.Failure(actorError);

        var inputValidation = await _createValidator.ValidateAsync(dto, cancellationToken);
        if (!inputValidation.IsValid)
            return Result<ResourceDto>.Failure(Error.Validation(
                string.Join("; ", inputValidation.Errors.Select(error => $"{error.PropertyName}: {error.ErrorMessage}")),
                inputValidation.Errors.FirstOrDefault()?.PropertyName));

        User? staffUser = null;
        if (dto.Type == ResourceType.Staff)
        {
            if (!dto.UserId.HasValue)
                return Result<ResourceDto>.Failure(Error.Validation(
                    "Select an active system user for Staff resources.", nameof(dto.UserId)));

            staffUser = await FindActiveTenantUserAsync(dto.UserId.Value, CurrentTenant(), cancellationToken);
            if (staffUser is null)
                return Result<ResourceDto>.Failure(Error.Validation(
                    "The selected system user must be active in the Resource tenant.", nameof(dto.UserId)));
            if (!HasValidStaffIdentity(staffUser))
                return Result<ResourceDto>.Failure(Error.Validation(
                    "The linked system user's name and email must fit the Resource identity fields.", nameof(dto.UserId)));
        }

        var identityValidation = await ValidateIdentityAsync(
            dto.UserId, null, staffUser?.Email ?? dto.Email, null, cancellationToken);
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

        var calendarResolution = await ResolveWorkingCapacityCalendarAsync(dto.BaseCalendarId, cancellationToken);
        if (dto.BaseCalendarId.HasValue && calendarResolution.Error is not null)
            return Result<ResourceDto>.Failure(calendarResolution.Error);
        var capacityCalendar = calendarResolution.Calendar;
        var capacityError = ResourceWorkingCapacityCalculator.ValidateConfiguration(
            dto.HoursPerDay,
            dto.HoursPerWeek,
            capacityCalendar);
        if (capacityError is not null)
            return Result<ResourceDto>.Failure(MapCapacityValidationError(capacityError, calendarResolution.Error));

        var organizationValidation = await ValidateOrganizationalUnitAsync(dto.PrimaryOrganizationalUnitId, cancellationToken);
        if (organizationValidation.IsFailure)
            return Result<ResourceDto>.Failure(organizationValidation.Error!);

        var resource = dto.ToEntity();
        resource.BaseCalendarId = dto.BaseCalendarId;
        if (staffUser is not null) ApplyStaffIdentity(resource, staffUser);
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
        await EnrichStaffIdentityAsync(new[] { createdDto }, created.TenantId, cancellationToken);
        await EnrichOrganizationAsync(new[] { createdDto }, cancellationToken);
        return Result<ResourceDto>.Success(createdDto);
    }

    public async Task<Result<ResourceDto>> UpdateAsync(Guid id, UpdateResourceDto dto, string userId, CancellationToken cancellationToken = default)
    {
        var actorError = ResourceManagementWriteGuard.RequireActorContext(_httpContextAccessor, _managementContextAccessor, userId);
        if (actorError is not null) return Result<ResourceDto>.Failure(actorError);

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

        var willBeStaff = dto.Type == ResourceType.Staff
            || (!dto.Type.HasValue && IsStaffResourceType(resource.Type));
        User? staffUser = null;
        Guid? userToValidate;
        if (willBeStaff)
        {
            if (dto.RemoveUserAssociation == true)
                return Result<ResourceDto>.Failure(Error.Validation(
                    "Staff resources must remain linked to an active system user.", nameof(dto.UserId)));

            var staffUserId = dto.UserId ?? resource.UserId;
            if (!staffUserId.HasValue)
                return Result<ResourceDto>.Failure(Error.Validation(
                    "Select an active system user for Staff resources.", nameof(dto.UserId)));

            staffUser = await FindActiveTenantUserAsync(staffUserId.Value, resource.TenantId, cancellationToken);
            if (staffUser is null)
                return Result<ResourceDto>.Failure(Error.Validation(
                    "The selected system user must be active in the Resource tenant.", nameof(dto.UserId)));
            if (!HasValidStaffIdentity(staffUser))
                return Result<ResourceDto>.Failure(Error.Validation(
                    "The linked system user's name and email must fit the Resource identity fields.", nameof(dto.UserId)));
            userToValidate = staffUserId;
        }
        else
        {
            var associationChanged = dto.RemoveUserAssociation == true || dto.UserId.HasValue;
            var requestedUserId = dto.RemoveUserAssociation == true ? null : dto.UserId;
            userToValidate = associationChanged ? requestedUserId : dto.IsActive == true ? resource.UserId : null;
        }

        var identityValidation = await ValidateIdentityAsync(
            userToValidate, resource.Id, staffUser?.Email ?? dto.Email, resource.Email, cancellationToken);
        if (identityValidation.IsFailure) return Result<ResourceDto>.Failure(identityValidation.Error!);

        var originalValues = System.Text.Json.JsonSerializer.Serialize(new
        {
            resource.Name, resource.JobTitle, resource.Email, resource.Type, resource.Status,
            resource.DepartmentId, resource.LocationId, resource.HoursPerDay, resource.HoursPerWeek
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

        var resultingHoursPerDay = dto.ClearHoursPerDay ? null : dto.HoursPerDay ?? resource.HoursPerDay;
        var resultingHoursPerWeek = dto.ClearHoursPerWeek ? null : dto.HoursPerWeek ?? resource.HoursPerWeek;
        var requestedCalendarId = dto.BaseCalendarId ?? resource.BaseCalendarId;
        var calendarResolution = await ResolveWorkingCapacityCalendarAsync(requestedCalendarId, cancellationToken);
        if (requestedCalendarId.HasValue && calendarResolution.Error is not null)
            return Result<ResourceDto>.Failure(calendarResolution.Error);
        var capacityCalendar = calendarResolution.Calendar;
        var capacityError = ResourceWorkingCapacityCalculator.ValidateConfiguration(
            resultingHoursPerDay,
            resultingHoursPerWeek,
            capacityCalendar);
        if (capacityError is not null)
            return Result<ResourceDto>.Failure(MapCapacityValidationError(capacityError, calendarResolution.Error));

        var organizationValidation = await ValidateOrganizationalUnitAsync(dto.PrimaryOrganizationalUnitId, cancellationToken);
        if (organizationValidation.IsFailure)
            return Result<ResourceDto>.Failure(organizationValidation.Error!);

        resource.ApplyUpdate(dto);
        if (requestedCalendarId.HasValue && capacityCalendar is not null)
            resource.BaseCalendarId = capacityCalendar.Id;
        if (staffUser is not null)
        {
            resource.UserId = staffUser.Id;
            ApplyStaffIdentity(resource, staffUser);
        }
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
        await EnrichStaffIdentityAsync(new[] { updatedDto }, resource.TenantId, cancellationToken);
        await EnrichOrganizationAsync(new[] { updatedDto }, cancellationToken);
        return Result<ResourceDto>.Success(updatedDto);
    }

    public async Task<Result> DeleteAsync(Guid id, string userId, CancellationToken cancellationToken = default)
    {
        var actorError = ResourceManagementWriteGuard.RequireActorContext(_httpContextAccessor, _managementContextAccessor, userId);
        if (actorError is not null) return Result.Failure(actorError);

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

    private async Task<User?> FindActiveTenantUserAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty || tenantId == Guid.Empty) return null;
        return await _context.Set<User>().AsNoTracking()
            .FirstOrDefaultAsync(user => user.Id == userId && user.TenantId == tenantId
                && user.IsActive && !user.IsDeleted, cancellationToken);
    }

    private static bool IsStaffResourceType(string type) =>
        string.Equals(ResourceTypeConstants.ToApiCode(type), nameof(ResourceType.Staff), StringComparison.OrdinalIgnoreCase);

    private static bool HasValidStaffIdentity(User user)
    {
        var name = user.FullName.Trim();
        var email = user.Email.Trim();
        return !string.IsNullOrWhiteSpace(name)
            && name.Length <= ResourceFieldLengths.Name
            && !string.IsNullOrWhiteSpace(email)
            && email.Length <= ResourceFieldLengths.Email;
    }

    private static void ApplyStaffIdentity(Resource resource, User user)
    {
        resource.Name = user.FullName.Trim();
        resource.Email = user.Email.Trim();
    }

    private async Task EnrichStaffIdentityAsync(
        IReadOnlyCollection<ResourceDto> resources,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var staffResources = resources
            .Where(resource => resource.UserId.HasValue && IsStaffResourceType(resource.Type))
            .ToArray();
        var userIds = staffResources.Select(resource => resource.UserId!.Value).Distinct().ToArray();
        if (userIds.Length == 0 || tenantId == Guid.Empty) return;

        var users = await _context.Set<User>().AsNoTracking()
            .Where(user => userIds.Contains(user.Id) && user.TenantId == tenantId && !user.IsDeleted)
            .Select(user => new { user.Id, user.FirstName, user.LastName, user.Email })
            .ToDictionaryAsync(user => user.Id, cancellationToken);

        foreach (var resource in staffResources)
        {
            if (!users.TryGetValue(resource.UserId!.Value, out var user)) continue;
            var name = (user.FirstName + " " + user.LastName).Trim();
            if (!string.IsNullOrWhiteSpace(name)) resource.Name = name;
            if (!string.IsNullOrWhiteSpace(user.Email)) resource.Email = user.Email;
        }
    }

    private async Task<Result> ValidateIdentityAsync(Guid? userId, Guid? excludeResourceId, string? email, string? existingEmail, CancellationToken cancellationToken)
    {
        if (userId.HasValue)
        {
            if (userId.Value == Guid.Empty) return Result.Failure(Error.Validation("User association must reference a valid system user"));
            var tenantId = CurrentTenant();
            var userExists = await _context.Set<User>().AsNoTracking().AnyAsync(user => user.Id == userId.Value
                && user.TenantId == tenantId && user.IsActive && !user.IsDeleted, cancellationToken);
            if (!userExists)
                return Result.Failure(Error.Validation("The selected system user must be active in the Resource tenant"));

            var linked = await _context.Set<Resource>().AnyAsync(resource =>
                resource.Id != (excludeResourceId ?? Guid.Empty) &&
                resource.UserId == userId.Value && !resource.IsDeleted,
                cancellationToken);
            if (linked) return Result.Failure(Error.Conflict("This system user is already linked to a Resource"));
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

    private static Error MapCapacityValidationError(
        ResourceCapacityValidationError validationError,
        Error? calendarResolutionError)
    {
        if (validationError.Code == ResourceWorkingCapacityCalculator.WorkingCalendarRequiredCode
            && calendarResolutionError is not null)
            return calendarResolutionError;

        return Error.BusinessValidation(
            validationError.Code,
            validationError.Message,
            validationError.FieldName);
    }

    private Guid CurrentTenant() =>
        Guid.TryParse(_httpContextAccessor.HttpContext?.User.FindFirstValue("tenantId"), out var tenantId) && tenantId != Guid.Empty
            ? tenantId
            : throw new InvalidOperationException("A trusted tenant context is required.");
}
