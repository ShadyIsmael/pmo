using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.ResourceManagement.Application.DTOs;
using NEXUS.Modules.ResourceManagement.Application.Mapping;
using NEXUS.Modules.ResourceManagement.Domain;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.ResourceManagement.Application;

public class ResourceService : IResourceService
{
    private readonly DbContext _context;
    private readonly IAuditService _auditService;

    public ResourceService(DbContext context, IAuditService auditService)
    {
        _context = context;
        _auditService = auditService;
    }

    public async Task<Result<ResourceDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var resource = await _context.Set<Resource>()
            .Include(r => r.Skills).ThenInclude(rs => rs.Skill)
            .Include(r => r.Allocations)
            .Include(r => r.Vendor)
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

    public async Task<Result<ResourceDto>> CreateAsync(CreateResourceDto dto, string userId, CancellationToken cancellationToken = default)
    {
        var resource = dto.ToEntity();
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

        var originalValues = System.Text.Json.JsonSerializer.Serialize(new
        {
            resource.Name, resource.JobTitle, resource.Email, resource.Type, resource.Status,
            resource.CostPerHour, resource.BillableRate, resource.DepartmentId, resource.LocationId
        });

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

        resource.IsDeleted = true;
        resource.UpdatedBy = userId;
        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(new AuditEntry
        {
            EntityType = "Resource",
            EntityId = resource.Id,
            Action = "Delete",
            UserId = Guid.Parse(userId),
            Timestamp = DateTime.UtcNow,
            CreatedBy = userId
        }, cancellationToken);

        return Result.Success();
    }
}
