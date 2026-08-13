using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.ProjectPlanning.Application.DTOs;
using NEXUS.Modules.ProjectPlanning.Application.Mapping;
using NEXUS.Modules.ProjectPlanning.Domain;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.ProjectPlanning.Application;

public class PlanActivityService : IPlanActivityService
{
    private static readonly Guid DemoTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly DbContext _context;
    private readonly IAuditService _auditService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public PlanActivityService(DbContext context, IAuditService auditService, IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _auditService = auditService;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<Result<PlanActivityDto>> CreateAsync(Guid planId, CreatePlanActivityDto dto, string userId, CancellationToken cancellationToken = default)
    {
        var workingVersion = await GetActiveWorkingVersionAsync(planId, cancellationToken);
        if (workingVersion is null)
        {
            return Result<PlanActivityDto>.Failure(Error.NotFound(await GetActiveWorkingVersionNotAvailableMessageAsync(planId, cancellationToken)));
        }

        var name = dto.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result<PlanActivityDto>.Failure(Error.Validation("Activity name is required"));
        }

        var code = dto.Code?.Trim();
        if (string.IsNullOrWhiteSpace(code))
        {
            return Result<PlanActivityDto>.Failure(Error.Validation("Activity code is required"));
        }

        if (dto.Type == PlanActivityType.Milestone && dto.Duration != 0)
        {
            return Result<PlanActivityDto>.Failure(Error.Validation("Milestones always have zero duration"));
        }

        if (dto.WbsId.HasValue &&
            !await WbsNodeExistsInVersionAsync(dto.WbsId.Value, workingVersion.Id, cancellationToken))
        {
            return Result<PlanActivityDto>.Failure(Error.Validation("WBS node must belong to the same plan version"));
        }

        var codeExists = await _context.Set<PlanActivity>()
            .AnyAsync(a => a.PlanVersionId == workingVersion.Id && a.Code == code, cancellationToken);
        if (codeExists)
        {
            return Result<PlanActivityDto>.Failure(Error.Conflict($"An activity code '{code}' already exists in this plan version"));
        }

        var tenantId = GetCurrentTenantId();
        var sortOrder = dto.SortOrder ?? await NextSortOrderAsync(workingVersion.Id, cancellationToken);

        var activity = dto.ToEntity(tenantId, workingVersion.Id, userId);
        activity.SortOrder = sortOrder;
        _context.Set<PlanActivity>().Add(activity);
        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(new AuditEntry
        {
            EntityType = "PlanActivity",
            EntityId = activity.Id,
            Action = "Create",
            UserId = TryParseGuid(userId),
            NewValues = System.Text.Json.JsonSerializer.Serialize(dto),
            Timestamp = DateTime.UtcNow,
            CreatedBy = userId
        }, cancellationToken);

        return Result<PlanActivityDto>.Success(activity.ToDto());
    }

    public async Task<Result<PlanActivityDto>> UpdateAsync(Guid planId, Guid activityId, UpdatePlanActivityDto dto, string userId, CancellationToken cancellationToken = default)
    {
        var activity = await _context.Set<PlanActivity>()
            .FirstOrDefaultAsync(a => a.Id == activityId, cancellationToken);
        if (activity is null) return Result<PlanActivityDto>.Failure(Error.NotFound("Activity not found"));

        var editError = await GetEditErrorAsync(activity, planId, cancellationToken);
        if (editError is not null) return Result<PlanActivityDto>.Failure(editError);

        var name = dto.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result<PlanActivityDto>.Failure(Error.Validation("Activity name is required"));
        }

        var code = dto.Code?.Trim();
        if (string.IsNullOrWhiteSpace(code))
        {
            return Result<PlanActivityDto>.Failure(Error.Validation("Activity code is required"));
        }

        if (dto.Type == PlanActivityType.Milestone && dto.Duration != 0)
        {
            return Result<PlanActivityDto>.Failure(Error.Validation("Milestones always have zero duration"));
        }

        if (dto.WbsId.HasValue &&
            !await WbsNodeExistsInVersionAsync(dto.WbsId.Value, activity.PlanVersionId, cancellationToken))
        {
            return Result<PlanActivityDto>.Failure(Error.Validation("WBS node must belong to the same plan version"));
        }

        var codeExists = await _context.Set<PlanActivity>()
            .AnyAsync(a => a.PlanVersionId == activity.PlanVersionId && a.Code == code && a.Id != activity.Id, cancellationToken);
        if (codeExists)
        {
            return Result<PlanActivityDto>.Failure(Error.Conflict($"An activity code '{code}' already exists in this plan version"));
        }

        var originalValues = System.Text.Json.JsonSerializer.Serialize(activity.ToDto());
        activity.Code = code;
        activity.Name = name;
        activity.Description = dto.Description?.Trim();
        activity.Type = dto.Type;
        activity.StartDate = dto.StartDate;
        activity.EndDate = dto.EndDate;
        activity.Duration = dto.Duration;
        activity.EstimateHours = dto.EstimateHours;
        activity.Progress = dto.Progress;
        activity.WbsId = dto.WbsId;
        activity.SortOrder = dto.SortOrder;
        activity.UpdatedBy = userId;

        _context.Entry(activity).Property(a => a.RowVersion).OriginalValue = dto.RowVersion;

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<PlanActivityDto>.Failure(Error.Conflict("The activity was modified by another user. Reload and try again."));
        }

        await _auditService.LogAsync(new AuditEntry
        {
            EntityType = "PlanActivity",
            EntityId = activity.Id,
            Action = "Update",
            UserId = TryParseGuid(userId),
            OriginalValues = originalValues,
            NewValues = System.Text.Json.JsonSerializer.Serialize(activity.ToDto()),
            Timestamp = DateTime.UtcNow,
            CreatedBy = userId
        }, cancellationToken);

        return Result<PlanActivityDto>.Success(activity.ToDto());
    }

    public async Task<Result> DeleteAsync(Guid planId, Guid activityId, byte[] rowVersion, string userId, CancellationToken cancellationToken = default)
    {
        var activity = await _context.Set<PlanActivity>()
            .FirstOrDefaultAsync(a => a.Id == activityId, cancellationToken);
        if (activity is null) return Result.Failure(Error.NotFound("Activity not found"));

        var editError = await GetEditErrorAsync(activity, planId, cancellationToken);
        if (editError is not null) return Result.Failure(editError);

        activity.IsDeleted = true;
        activity.UpdatedBy = userId;

        _context.Entry(activity).Property(a => a.RowVersion).OriginalValue = rowVersion;

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure(Error.Conflict("The activity was modified by another user. Reload and try again."));
        }

        await _auditService.LogAsync(new AuditEntry
        {
            EntityType = "PlanActivity",
            EntityId = activity.Id,
            Action = "Delete",
            UserId = TryParseGuid(userId),
            Timestamp = DateTime.UtcNow,
            CreatedBy = userId
        }, cancellationToken);

        return Result.Success();
    }

    public async Task<Result<IReadOnlyList<PlanActivityDto>>> GetByPlanAsync(Guid planId, CancellationToken cancellationToken = default)
    {
        var workingVersion = await GetActiveWorkingVersionAsync(planId, cancellationToken);
        if (workingVersion is null)
        {
            return Result<IReadOnlyList<PlanActivityDto>>.Failure(Error.NotFound(await GetActiveWorkingVersionNotAvailableMessageAsync(planId, cancellationToken)));
        }

        var activities = await _context.Set<PlanActivity>()
            .Where(a => a.PlanVersionId == workingVersion.Id)
            .OrderBy(a => a.SortOrder)
            .ThenBy(a => a.Code)
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<PlanActivityDto>>.Success(activities.Select(a => a.ToDto()).ToList());
    }

    private async Task<PlanVersion?> GetActiveWorkingVersionAsync(Guid planId, CancellationToken cancellationToken)
    {
        return await _context.Set<PlanVersion>()
            .FirstOrDefaultAsync(v => v.PlanId == planId && v.Type == PlanVersionType.Working && v.Status == PlanVersionStatus.Active, cancellationToken);
    }

    private async Task<string> GetActiveWorkingVersionNotAvailableMessageAsync(Guid planId, CancellationToken cancellationToken)
    {
        var planExists = await _context.Set<Plan>().AnyAsync(p => p.Id == planId, cancellationToken);
        return planExists ? "No active working version exists for this plan" : "Plan not found";
    }

    /// <summary>
    /// Returns a failure <see cref="Error"/> when an activity cannot be edited: its
    /// plan is missing, it has no active Working version, or the activity lives in a
    /// version other than that Working version (e.g. an immutable Baseline).
    /// </summary>
    private async Task<Error?> GetEditErrorAsync(PlanActivity activity, Guid planId, CancellationToken cancellationToken)
    {
        var planExists = await _context.Set<Plan>().AnyAsync(p => p.Id == planId, cancellationToken);
        if (!planExists) return Error.NotFound("Plan not found");

        var workingVersion = await _context.Set<PlanVersion>()
            .FirstOrDefaultAsync(v => v.PlanId == planId && v.Type == PlanVersionType.Working && v.Status == PlanVersionStatus.Active, cancellationToken);
        if (workingVersion is null) return Error.NotFound("No active working version exists for this plan");

        if (activity.PlanVersionId != workingVersion.Id)
        {
            var activityVersion = await _context.Set<PlanVersion>()
                .FirstOrDefaultAsync(v => v.Id == activity.PlanVersionId, cancellationToken);
            return activityVersion?.Type == PlanVersionType.Baseline
                ? Error.Validation("Baseline versions are immutable; edits are only allowed in the active Working version")
                : Error.Validation("Only the active Working version is editable");
        }

        return null;
    }

    private async Task<bool> WbsNodeExistsInVersionAsync(Guid wbsId, Guid planVersionId, CancellationToken cancellationToken)
    {
        return await _context.Set<PlanWbsNode>()
            .AnyAsync(n => n.Id == wbsId && n.PlanVersionId == planVersionId, cancellationToken);
    }

    private async Task<int> NextSortOrderAsync(Guid planVersionId, CancellationToken cancellationToken)
    {
        var max = await _context.Set<PlanActivity>()
            .Where(a => a.PlanVersionId == planVersionId)
            .Select(a => (int?)a.SortOrder)
            .MaxAsync(cancellationToken);
        return (max ?? -1) + 1;
    }

    private Guid GetCurrentTenantId()
    {
        var tenantId = _httpContextAccessor.HttpContext?.User.FindFirst("tenantId")?.Value;
        if (!string.IsNullOrWhiteSpace(tenantId) && Guid.TryParse(tenantId, out var parsed))
        {
            return parsed;
        }

        return DemoTenantId;
    }

    private static Guid? TryParseGuid(string value)
    {
        return Guid.TryParse(value, out var parsed) ? parsed : null;
    }
}