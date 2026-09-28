using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.ProjectPlanning.Application.DTOs;
using NEXUS.Modules.ProjectPlanning.Application.Mapping;
using NEXUS.Modules.ProjectPlanning.Domain;
using NEXUS.Modules.ProjectPlanning.Domain.Scheduling;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Helpers;

namespace NEXUS.Modules.ProjectPlanning.Application;

public class PlanDependencyService : IPlanDependencyService
{
    private readonly DbContext _context;
    private readonly IAuditService _auditService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IPlanSchedulingService _schedulingService;

    public PlanDependencyService(DbContext context, IAuditService auditService, IHttpContextAccessor httpContextAccessor, IPlanSchedulingService? schedulingService = null)
    {
        _context = context;
        _auditService = auditService;
        _httpContextAccessor = httpContextAccessor;
        _schedulingService = schedulingService ?? new PlanSchedulingService(context, new AutoSchedulingEngine());
    }

    public async Task<Result<PlanDependencyDto>> CreateAsync(Guid planId, CreatePlanDependencyDto dto, string userId, CancellationToken cancellationToken = default)
    {
        var workingVersion = await GetActiveWorkingVersionAsync(planId, cancellationToken);
        if (workingVersion is null)
        {
            return Result<PlanDependencyDto>.Failure(Error.NotFound(await GetActiveWorkingVersionNotAvailableMessageAsync(planId, cancellationToken)));
        }

        var validationError = await ValidateEdgeAsync(dto.ActivityId, dto.PredecessorActivityId, workingVersion.Id, cancellationToken);
        if (validationError is not null)
        {
            return Result<PlanDependencyDto>.Failure(validationError);
        }

        var tenantId = GetCurrentTenantId();
        var existing = await _context.Set<PlanDependency>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(d => d.TenantId == tenantId &&
                                      d.PlanVersionId == workingVersion.Id &&
                                      d.ActivityId == dto.ActivityId &&
                                      d.PredecessorActivityId == dto.PredecessorActivityId, cancellationToken);
        if (existing is not null && !existing.IsDeleted)
        {
            return Result<PlanDependencyDto>.Failure(Error.Conflict("This dependency already exists in the plan version"));
        }

        var dependency = existing ?? dto.ToEntity(tenantId, workingVersion.Id, userId);
        if (existing is not null)
        {
            // The unique edge index includes soft-deleted rows. Reuse a deleted
            // edge when the same predecessor is assigned again instead of
            // inserting a duplicate database key.
            dependency.IsDeleted = false;
            dependency.Type = dto.Type;
            dependency.Lag = dto.Lag;
            dependency.UpdatedBy = userId;
        }
        else
        {
            _context.Set<PlanDependency>().Add(dependency);
        }
        var createSchedule = await _schedulingService.RecalculateAsync(
            workingVersion.Id,
            [new SchedulingSeed(dto.ActivityId)],
            userId,
            cancellationToken);
        if (createSchedule.IsFailure) return Result<PlanDependencyDto>.Failure(createSchedule.Error!);
        await _context.SaveChangesAsync(cancellationToken);

        await _auditService.LogAsync(new AuditEntry
        {
            EntityType = "PlanDependency",
            EntityId = dependency.Id,
            Action = "Create",
            UserId = TryParseGuid(userId),
            NewValues = System.Text.Json.JsonSerializer.Serialize(dto),
            Timestamp = DateTime.UtcNow,
            CreatedBy = userId
        }, cancellationToken);

        return Result<PlanDependencyDto>.Success(dependency.ToDto());
    }

    public async Task<Result<PlanDependencyDto>> UpdateAsync(Guid planId, Guid dependencyId, UpdatePlanDependencyDto dto, string userId, CancellationToken cancellationToken = default)
    {
        var dependency = await _context.Set<PlanDependency>()
            .FirstOrDefaultAsync(d => d.Id == dependencyId, cancellationToken);
        if (dependency is null) return Result<PlanDependencyDto>.Failure(Error.NotFound("Dependency not found"));

        var editError = await GetEditErrorAsync(dependency, planId, cancellationToken);
        if (editError is not null) return Result<PlanDependencyDto>.Failure(editError);

        var validationError = await ValidateEdgeAsync(dto.ActivityId, dto.PredecessorActivityId, dependency.PlanVersionId, cancellationToken);
        if (validationError is not null)
        {
            return Result<PlanDependencyDto>.Failure(validationError);
        }

        var exists = await _context.Set<PlanDependency>()
            .AnyAsync(d => d.PlanVersionId == dependency.PlanVersionId &&
                           d.ActivityId == dto.ActivityId &&
                           d.PredecessorActivityId == dto.PredecessorActivityId &&
                           d.Id != dependency.Id, cancellationToken);
        if (exists)
        {
            return Result<PlanDependencyDto>.Failure(Error.Conflict("This dependency already exists in the plan version"));
        }

        var originalValues = System.Text.Json.JsonSerializer.Serialize(dependency.ToDto());
        var originalSuccessorId = dependency.ActivityId;
        dependency.ActivityId = dto.ActivityId;
        dependency.PredecessorActivityId = dto.PredecessorActivityId;
        dependency.Type = dto.Type;
        dependency.Lag = dto.Lag;
        dependency.UpdatedBy = userId;

        _context.Entry(dependency).Property(d => d.RowVersion).OriginalValue = dto.RowVersion;

        var updateSchedule = await _schedulingService.RecalculateAsync(
            dependency.PlanVersionId,
            [new SchedulingSeed(originalSuccessorId), new SchedulingSeed(dependency.ActivityId)],
            userId,
            cancellationToken);
        if (updateSchedule.IsFailure) return Result<PlanDependencyDto>.Failure(updateSchedule.Error!);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<PlanDependencyDto>.Failure(Error.Conflict("The dependency was modified by another user. Reload and try again."));
        }

        await _auditService.LogAsync(new AuditEntry
        {
            EntityType = "PlanDependency",
            EntityId = dependency.Id,
            Action = "Update",
            UserId = TryParseGuid(userId),
            OriginalValues = originalValues,
            NewValues = System.Text.Json.JsonSerializer.Serialize(dependency.ToDto()),
            Timestamp = DateTime.UtcNow,
            CreatedBy = userId
        }, cancellationToken);

        return Result<PlanDependencyDto>.Success(dependency.ToDto());
    }

    public async Task<Result> DeleteAsync(Guid planId, Guid dependencyId, byte[] rowVersion, string userId, CancellationToken cancellationToken = default)
    {
        var dependency = await _context.Set<PlanDependency>()
            .FirstOrDefaultAsync(d => d.Id == dependencyId, cancellationToken);
        if (dependency is null) return Result.Failure(Error.NotFound("Dependency not found"));

        var editError = await GetEditErrorAsync(dependency, planId, cancellationToken);
        if (editError is not null) return Result.Failure(editError);

        dependency.IsDeleted = true;
        dependency.UpdatedBy = userId;

        _context.Entry(dependency).Property(d => d.RowVersion).OriginalValue = rowVersion;

        var deleteSchedule = await _schedulingService.RecalculateAsync(
            dependency.PlanVersionId,
            [new SchedulingSeed(dependency.ActivityId)],
            userId,
            cancellationToken);
        if (deleteSchedule.IsFailure) return Result.Failure(deleteSchedule.Error!);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure(Error.Conflict("The dependency was modified by another user. Reload and try again."));
        }

        await _auditService.LogAsync(new AuditEntry
        {
            EntityType = "PlanDependency",
            EntityId = dependency.Id,
            Action = "Delete",
            UserId = TryParseGuid(userId),
            Timestamp = DateTime.UtcNow,
            CreatedBy = userId
        }, cancellationToken);

        return Result.Success();
    }

    public async Task<Result<ReplacePlanPredecessorsResultDto>> ReplacePredecessorsAsync(
        Guid planId,
        ReplacePlanPredecessorsDto dto,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var workingVersion = await GetActiveWorkingVersionAsync(planId, cancellationToken);
        if (workingVersion is null)
            return Result<ReplacePlanPredecessorsResultDto>.Failure(Error.NotFound(await GetActiveWorkingVersionNotAvailableMessageAsync(planId, cancellationToken)));

        var desired = dto.Predecessors ?? [];
        var duplicatePredecessor = desired.GroupBy(item => item.PredecessorActivityId).FirstOrDefault(group => group.Count() > 1);
        if (duplicatePredecessor is not null)
            return Result<ReplacePlanPredecessorsResultDto>.Failure(Error.Validation("An activity cannot have duplicate predecessors"));
        if (desired.Any(item => item.PredecessorActivityId == dto.ActivityId))
            return Result<ReplacePlanPredecessorsResultDto>.Failure(Error.Validation("An activity cannot depend on itself"));

        var activityIds = desired.Select(item => item.PredecessorActivityId).Append(dto.ActivityId).ToHashSet();
        var existingActivityIds = await _context.Set<PlanActivity>()
            .Where(activity => activity.PlanVersionId == workingVersion.Id && activityIds.Contains(activity.Id))
            .Select(activity => activity.Id)
            .ToListAsync(cancellationToken);
        if (!existingActivityIds.Contains(dto.ActivityId))
            return Result<ReplacePlanPredecessorsResultDto>.Failure(Error.Validation("Successor activity must belong to the same plan version"));
        var missingPredecessor = desired.FirstOrDefault(item => !existingActivityIds.Contains(item.PredecessorActivityId));
        if (missingPredecessor is not null)
            return Result<ReplacePlanPredecessorsResultDto>.Failure(Error.Validation("Predecessor activity must belong to the same plan version"));

        var tenantId = GetCurrentTenantId();
        var current = await _context.Set<PlanDependency>()
            .IgnoreQueryFilters()
            .Where(dependency => dependency.TenantId == tenantId &&
                                 dependency.PlanVersionId == workingVersion.Id &&
                                 dependency.ActivityId == dto.ActivityId)
            .ToListAsync(cancellationToken);
        var currentByPredecessor = current
            .GroupBy(dependency => dependency.PredecessorActivityId)
            .ToDictionary(group => group.Key, group => group.OrderBy(dependency => dependency.IsDeleted).First());
        var desiredIds = desired.Select(item => item.PredecessorActivityId).ToHashSet();
        var changed = false;

        foreach (var item in desired)
        {
            if (currentByPredecessor.TryGetValue(item.PredecessorActivityId, out var existing))
            {
                if (!existing.IsDeleted && existing.Type == item.Type && existing.Lag == item.Lag) continue;
                existing.IsDeleted = false;
                existing.Type = item.Type;
                existing.Lag = item.Lag;
                existing.UpdatedBy = userId;
                changed = true;
                continue;
            }

            var dependency = new PlanDependency
            {
                TenantId = GetCurrentTenantId(),
                PlanVersionId = workingVersion.Id,
                ActivityId = dto.ActivityId,
                PredecessorActivityId = item.PredecessorActivityId,
                Type = item.Type,
                Lag = item.Lag,
                CreatedBy = userId,
            };
            _context.Set<PlanDependency>().Add(dependency);
            current.Add(dependency);
            changed = true;
        }

        foreach (var existing in current.Where(dependency => !dependency.IsDeleted && !desiredIds.Contains(dependency.PredecessorActivityId)))
        {
            existing.IsDeleted = true;
            existing.UpdatedBy = userId;
            changed = true;
        }

        if (!changed)
        {
            return Result<ReplacePlanPredecessorsResultDto>.Success(new ReplacePlanPredecessorsResultDto
            {
                Dependencies = current.Where(dependency => !dependency.IsDeleted).Select(dependency => dependency.ToDto()).ToList(),
                SchedulingRecalculated = false,
            });
        }

        var schedule = await _schedulingService.RecalculateAsync(
            workingVersion.Id,
            [new SchedulingSeed(dto.ActivityId)],
            userId,
            cancellationToken);
        if (schedule.IsFailure)
            return Result<ReplacePlanPredecessorsResultDto>.Failure(schedule.Error!);

        _context.Set<AuditEntry>().Add(new AuditEntry
        {
            TenantId = GetCurrentTenantId(),
            EntityType = "PlanDependency",
            EntityId = dto.ActivityId,
            Action = "ReplacePredecessors",
            UserId = TryParseGuid(userId),
            NewValues = System.Text.Json.JsonSerializer.Serialize(dto),
            Timestamp = DateTime.UtcNow,
            CreatedBy = userId,
        });

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<ReplacePlanPredecessorsResultDto>.Failure(Error.Conflict("The predecessor set was modified by another user. Reload and try again."));
        }

        return Result<ReplacePlanPredecessorsResultDto>.Success(new ReplacePlanPredecessorsResultDto
        {
            Dependencies = current.Where(dependency => !dependency.IsDeleted).Select(dependency => dependency.ToDto()).ToList(),
            ScheduledActivities = schedule.Value!.ChangedActivities,
            SchedulingRecalculated = true,
        });
    }

    public async Task<Result<IReadOnlyList<PlanDependencyDto>>> GetByPlanAsync(Guid planId, CancellationToken cancellationToken = default)
    {
        var workingVersion = await GetActiveWorkingVersionAsync(planId, cancellationToken);
        if (workingVersion is null)
        {
            return Result<IReadOnlyList<PlanDependencyDto>>.Failure(Error.NotFound(await GetActiveWorkingVersionNotAvailableMessageAsync(planId, cancellationToken)));
        }

        var dependencies = await _context.Set<PlanDependency>()
            .Where(d => d.PlanVersionId == workingVersion.Id)
            .OrderBy(d => d.PredecessorActivityId)
            .ThenBy(d => d.ActivityId)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<PlanDependencyDto>>.Success(dependencies.Select(d => d.ToDto()).ToList());
    }

    /// <summary>
    /// Validates that both endpoints are distinct activities that exist in the
    /// given PlanVersion.
    /// </summary>
    private async Task<Error?> ValidateEdgeAsync(Guid activityId, Guid predecessorActivityId, Guid planVersionId, CancellationToken cancellationToken)
    {
        if (activityId == predecessorActivityId)
        {
            return Error.Validation("An activity cannot depend on itself");
        }

        var existingIds = await _context.Set<PlanActivity>()
            .Where(a => a.PlanVersionId == planVersionId && (a.Id == activityId || a.Id == predecessorActivityId))
            .Select(a => a.Id)
            .ToListAsync(cancellationToken);

        if (!existingIds.Contains(activityId))
        {
            return Error.Validation("Successor activity must belong to the same plan version");
        }

        if (!existingIds.Contains(predecessorActivityId))
        {
            return Error.Validation("Predecessor activity must belong to the same plan version");
        }

        return null;
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
    /// Returns a failure <see cref="Error"/> when a dependency cannot be edited:
    /// its plan is missing, it has no active Working version, or the dependency
    /// lives in a version other than that Working version (e.g. an immutable
    /// Baseline).
    /// </summary>
    private async Task<Error?> GetEditErrorAsync(PlanDependency dependency, Guid planId, CancellationToken cancellationToken)
    {
        var planExists = await _context.Set<Plan>().AnyAsync(p => p.Id == planId, cancellationToken);
        if (!planExists) return Error.NotFound("Plan not found");

        var workingVersion = await _context.Set<PlanVersion>()
            .FirstOrDefaultAsync(v => v.PlanId == planId && v.Type == PlanVersionType.Working && v.Status == PlanVersionStatus.Active, cancellationToken);
        if (workingVersion is null) return Error.NotFound("No active working version exists for this plan");

        if (dependency.PlanVersionId != workingVersion.Id)
        {
            var dependencyVersion = await _context.Set<PlanVersion>()
                .FirstOrDefaultAsync(v => v.Id == dependency.PlanVersionId, cancellationToken);
            return dependencyVersion?.Type == PlanVersionType.Baseline
                ? Error.Validation("Baseline versions are immutable; edits are only allowed in the active Working version")
                : Error.Validation("Only the active Working version is editable");
        }

        return null;
    }

    private Guid GetCurrentTenantId()
    {
        var tenantId = _httpContextAccessor.HttpContext?.User.FindFirst("tenantId")?.Value;
        if (!string.IsNullOrWhiteSpace(tenantId) && Guid.TryParse(tenantId, out var parsed))
        {
            return parsed;
        }

        return TenantConstants.DemoTenantId;
    }

    private static Guid? TryParseGuid(string value) => GuidHelper.TryParse(value);
}
