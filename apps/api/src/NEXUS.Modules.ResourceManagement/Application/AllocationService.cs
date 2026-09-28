using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.Organization.Domain;
using NEXUS.Modules.ResourceManagement.Application.DTOs;
using NEXUS.Modules.ResourceManagement.Domain;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.ResourceManagement.Application;

public class AllocationService : IAllocationService
{
    private readonly DbContext _context;
    private readonly IAuditService _auditService;

    public AllocationService(DbContext context, IAuditService auditService)
    {
        _context = context;
        _auditService = auditService;
    }

    public async Task<PagedResult<AllocationDto>> GetPagedAsync(int page, int pageSize, Guid? resourceId = null, Guid? projectId = null, string? status = null, CancellationToken cancellationToken = default)
    {
        var query = BuildQuery();
        if (resourceId.HasValue) query = query.Where(a => a.ResourceId == resourceId.Value);
        if (projectId.HasValue) query = query.Where(a => a.ProjectId == projectId.Value);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(a => a.Status == status);
        return await ToPagedResultAsync(query, page, pageSize, cancellationToken);
    }

    public async Task<PagedResult<AllocationDto>> GetByResourceAsync(Guid resourceId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = BuildQuery().Where(a => a.ResourceId == resourceId);
        return await ToPagedResultAsync(query, page, pageSize, cancellationToken);
    }

    public async Task<PagedResult<AllocationDto>> GetByProjectAsync(Guid projectId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = BuildQuery().Where(a => a.ProjectId == projectId);
        return await ToPagedResultAsync(query, page, pageSize, cancellationToken);
    }

    public async Task<ProjectAllocationOverviewDto> GetProjectOverviewAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var query = _context.Set<ResourceAllocation>().AsNoTracking()
            .Where(allocation => allocation.ProjectId == projectId
                && !allocation.IsDeleted
                && allocation.Status != ResourceAllocation.StatusCancelled
                && allocation.StartDate <= now
                && allocation.EndDate >= now);

        return new ProjectAllocationOverviewDto
        {
            ActiveAllocationCount = await query.CountAsync(cancellationToken),
            ActiveResourceCount = await query.Select(allocation => allocation.ResourceId).Distinct().CountAsync(cancellationToken),
            AllocatedHoursPerWeek = await query.Select(allocation => allocation.HoursPerWeek ?? 0m).SumAsync(cancellationToken)
        };
    }

    public async Task<Result<AllocationDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var allocation = await BuildQuery().FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (allocation is null)
            return Result<AllocationDto>.Failure(Error.NotFound("Allocation not found"));
        return Result<AllocationDto>.Success(MapToDto(allocation));
    }

    public async Task<Result<List<AllocationDto>>> GetConflictsAsync(Guid resourceId, DateTime startDate, DateTime endDate, decimal? excludeAllocationId, CancellationToken cancellationToken = default)
    {
        var query = _context.Set<ResourceAllocation>()
            .Include(a => a.Resource)
            .Where(a => a.ResourceId == resourceId && a.Status != ResourceAllocation.StatusCancelled && !a.IsDeleted);

        if (excludeAllocationId.HasValue)
            query = query.Where(a => a.Id != Guid.Parse(excludeAllocationId.Value.ToString()!));

        var overlapping = await query
            .Where(a => a.StartDate < endDate && a.EndDate > startDate)
            .OrderBy(a => a.StartDate)
            .ToListAsync(cancellationToken);

        return Result<List<AllocationDto>>.Success(overlapping.Select(MapToDto).ToList());
    }

    public async Task<Result<AllocationDto>> CreateAsync(CreateAllocationDto dto, string userId, CancellationToken cancellationToken = default)
    {
        var validation = await ValidateAllocationAsync(dto.ResourceId, dto.StartDate, dto.EndDate, dto.AllocationPercentage, null, userId, cancellationToken);
        if (validation.IsFailure) return Result<AllocationDto>.Failure(validation.Error!);

        var resource = await _context.Set<Resource>().FirstOrDefaultAsync(r => r.Id == dto.ResourceId && !r.IsDeleted, cancellationToken);
        if (resource is null) return Result<AllocationDto>.Failure(Error.NotFound("Resource not found"));
        if (!resource.IsActive) return Result<AllocationDto>.Failure(Error.Validation("Resource is not active"));

        var allocation = new ResourceAllocation
        {
            ResourceId = dto.ResourceId,
            ProjectId = dto.ProjectId,
            ProjectName = dto.ProjectName ?? await ResolveProjectNameAsync(dto.ProjectId, cancellationToken),
            WorkItemId = dto.WorkItemId,
            ProjectRoleId = dto.ProjectRoleId,
            AllocationPercentage = dto.AllocationPercentage,
            HoursPerWeek = dto.HoursPerWeek,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            Status = dto.Status,
            IsBillable = dto.IsBillable,
            Notes = dto.Notes,
            CreatedBy = userId
        };

        _context.Set<ResourceAllocation>().Add(allocation);
        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(new AuditEntry
        {
            EntityType = "ResourceAllocation",
            EntityId = allocation.Id,
            Action = "Create",
            UserId = Guid.Parse(userId),
            NewValues = System.Text.Json.JsonSerializer.Serialize(dto),
            Timestamp = DateTime.UtcNow,
            CreatedBy = userId
        }, cancellationToken);

        return Result<AllocationDto>.Success(MapToDto(await BuildQuery().FirstAsync(a => a.Id == allocation.Id, cancellationToken)));
    }

    public async Task<Result<AllocationDto>> UpdateAsync(Guid id, UpdateAllocationDto dto, string userId, CancellationToken cancellationToken = default)
    {
        var allocation = await _context.Set<ResourceAllocation>()
            .Include(a => a.Resource)
            .FirstOrDefaultAsync(a => a.Id == id && !a.IsDeleted, cancellationToken);

        if (allocation is null)
            return Result<AllocationDto>.Failure(Error.NotFound("Allocation not found"));

        var startDate = dto.StartDate ?? allocation.StartDate;
        var endDate = dto.EndDate ?? allocation.EndDate;
        var percentage = dto.AllocationPercentage ?? allocation.AllocationPercentage;

        var validation = await ValidateAllocationAsync(allocation.ResourceId, startDate, endDate, percentage, id, userId, cancellationToken);
        if (validation.IsFailure) return Result<AllocationDto>.Failure(validation.Error!);

        if (dto.ProjectId.HasValue) allocation.ProjectId = dto.ProjectId;
        if (dto.ProjectName != null) allocation.ProjectName = dto.ProjectName;
        if (dto.WorkItemId.HasValue) allocation.WorkItemId = dto.WorkItemId;
        if (dto.ProjectRoleId.HasValue) allocation.ProjectRoleId = dto.ProjectRoleId.Value;
        if (dto.AllocationPercentage.HasValue) allocation.AllocationPercentage = dto.AllocationPercentage.Value;
        if (dto.HoursPerWeek.HasValue) allocation.HoursPerWeek = dto.HoursPerWeek;
        if (dto.StartDate.HasValue) allocation.StartDate = dto.StartDate.Value;
        if (dto.EndDate.HasValue) allocation.EndDate = dto.EndDate.Value;
        if (dto.Status != null) allocation.Status = dto.Status;
        if (dto.IsBillable.HasValue) allocation.IsBillable = dto.IsBillable.Value;
        if (dto.Notes != null) allocation.Notes = dto.Notes;
        allocation.UpdatedBy = userId;

        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(new AuditEntry
        {
            EntityType = "ResourceAllocation",
            EntityId = allocation.Id,
            Action = "Update",
            UserId = Guid.Parse(userId),
            NewValues = System.Text.Json.JsonSerializer.Serialize(dto),
            Timestamp = DateTime.UtcNow,
            CreatedBy = userId
        }, cancellationToken);

        return Result<AllocationDto>.Success(MapToDto(allocation));
    }

    public async Task<Result> DeleteAsync(Guid id, string userId, CancellationToken cancellationToken = default)
    {
        var allocation = await _context.Set<ResourceAllocation>()
            .FirstOrDefaultAsync(a => a.Id == id && !a.IsDeleted, cancellationToken);
        if (allocation is null) return Result.Failure(Error.NotFound("Allocation not found"));

        allocation.IsDeleted = true;
        allocation.UpdatedBy = userId;
        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(new AuditEntry
        {
            EntityType = "ResourceAllocation",
            EntityId = id,
            Action = "Delete",
            UserId = Guid.Parse(userId),
            Timestamp = DateTime.UtcNow,
            CreatedBy = userId
        }, cancellationToken);

        return Result.Success();
    }

    private async Task<Result> ValidateAllocationAsync(Guid resourceId, DateTime startDate, DateTime endDate, decimal allocationPercentage, Guid? excludeId, string userId, CancellationToken cancellationToken)
    {
        if (startDate >= endDate)
            return Result.Failure(Error.Validation("Start date must be before end date"));
        if (allocationPercentage <= 0 || allocationPercentage > 100)
            return Result.Failure(Error.Validation("Allocation percentage must be between 1 and 100"));
        if (allocationPercentage > 100)
            return Result.Failure(Error.Validation("Allocation cannot exceed 100%"));

        var resource = await _context.Set<Resource>().FirstOrDefaultAsync(r => r.Id == resourceId && !r.IsDeleted, cancellationToken);
        if (resource is null) return Result.Failure(Error.NotFound("Resource not found"));
        if (!resource.IsActive) return Result.Failure(Error.Validation("Resource is not active"));

        var query = _context.Set<ResourceAllocation>()
            .Where(a => a.ResourceId == resourceId && a.Status != ResourceAllocation.StatusCancelled && !a.IsDeleted);

        if (excludeId.HasValue)
            query = query.Where(a => a.Id != excludeId.Value);

        var overlapping = await query
            .Where(a => a.StartDate < endDate && a.EndDate > startDate)
            .ToListAsync(cancellationToken);

        if (overlapping.Count != 0)
        {
            var totalPercentage = overlapping.Sum(a => a.AllocationPercentage) + allocationPercentage;
            if (totalPercentage > 100)
            {
                var conflicts = overlapping.Select(a => $"{a.ProjectName ?? "Unnamed"} ({a.AllocationPercentage}%)");
                return Result.Failure(Error.Validation(
                    $"Over-allocation detected: total would be {totalPercentage}%. Conflicting allocations: {string.Join(", ", conflicts)}"));
            }
        }

        var contractsExist = await _context.Set<Contract>()
            .AnyAsync(c => c.ResourceId == resourceId && c.Status == "Active" && c.StartDate <= endDate &&
                (!c.EndDate.HasValue || c.EndDate >= startDate), cancellationToken);

        return Result.Success();
    }

    private IQueryable<ResourceAllocation> BuildQuery()
    {
        return _context.Set<ResourceAllocation>()
            .Include(a => a.Resource)
            .Where(a => !a.IsDeleted);
    }

    private async Task<PagedResult<AllocationDto>> ToPagedResultAsync(IQueryable<ResourceAllocation> query, int page, int pageSize, CancellationToken cancellationToken)
    {
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(a => a.StartDate).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new PagedResult<AllocationDto>(items.Select(MapToDto).ToList(), page, pageSize, total);
    }

    private async Task<string?> ResolveProjectNameAsync(Guid? projectId, CancellationToken cancellationToken)
    {
        if (!projectId.HasValue) return null;
        try
        {
            var project = await _context.Set<ResourceAllocation>()
                .Where(a => a.ProjectId == projectId.Value)
                .Select(a => a.ProjectName)
                .FirstOrDefaultAsync(cancellationToken);
            return project;
        }
        catch { return null; }
    }

    private static AllocationDto MapToDto(ResourceAllocation a)
    {
        return new AllocationDto
        {
            Id = a.Id,
            ResourceId = a.ResourceId,
            ResourceName = a.Resource?.Name ?? string.Empty,
            ResourceJobTitle = a.Resource?.JobTitle,
            ResourceType = a.Resource?.Type,
            ProjectId = a.ProjectId,
            ProjectName = a.ProjectName,
            WorkItemId = a.WorkItemId,
            ProjectRoleId = a.ProjectRoleId,
            AllocationPercentage = a.AllocationPercentage,
            HoursPerWeek = a.HoursPerWeek,
            PlannedHours = a.PlannedHours,
            StartDate = a.StartDate,
            EndDate = a.EndDate,
            Status = a.Status,
            IsBillable = a.IsBillable,
            Notes = a.Notes,
            CreatedAt = a.CreatedAt,
            UpdatedAt = a.UpdatedAt
        };
    }
}
