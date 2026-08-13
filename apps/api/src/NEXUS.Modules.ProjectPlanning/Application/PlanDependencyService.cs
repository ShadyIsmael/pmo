using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.ProjectPlanning.Application.DTOs;
using NEXUS.Modules.ProjectPlanning.Application.Mapping;
using NEXUS.Modules.ProjectPlanning.Domain;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.ProjectPlanning.Application;

public class PlanDependencyService : IPlanDependencyService
{
    private static readonly Guid DemoTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly DbContext _context;
    private readonly IAuditService _auditService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public PlanDependencyService(DbContext context, IAuditService auditService, IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _auditService = auditService;
        _httpContextAccessor = httpContextAccessor;
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

        var exists = await _context.Set<PlanDependency>()
            .AnyAsync(d => d.PlanVersionId == workingVersion.Id &&
                           d.ActivityId == dto.ActivityId &&
                           d.PredecessorActivityId == dto.PredecessorActivityId, cancellationToken);
        if (exists)
        {
            return Result<PlanDependencyDto>.Failure(Error.Conflict("This dependency already exists in the plan version"));
        }

        var cycleError = await WouldCreateCycleAsync(dto.ActivityId, dto.PredecessorActivityId, workingVersion.Id, null, cancellationToken);
        if (cycleError is not null)
        {
            return Result<PlanDependencyDto>.Failure(cycleError);
        }

        var tenantId = GetCurrentTenantId();
        var dependency = dto.ToEntity(tenantId, workingVersion.Id, userId);
        _context.Set<PlanDependency>().Add(dependency);
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

        var cycleError = await WouldCreateCycleAsync(dto.ActivityId, dto.PredecessorActivityId, dependency.PlanVersionId, dependency.Id, cancellationToken);
        if (cycleError is not null)
        {
            return Result<PlanDependencyDto>.Failure(cycleError);
        }

        var originalValues = System.Text.Json.JsonSerializer.Serialize(dependency.ToDto());
        dependency.ActivityId = dto.ActivityId;
        dependency.PredecessorActivityId = dto.PredecessorActivityId;
        dependency.Type = dto.Type;
        dependency.Lag = dto.Lag;
        dependency.UpdatedBy = userId;

        _context.Entry(dependency).Property(d => d.RowVersion).OriginalValue = dto.RowVersion;

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

    /// <summary>
    /// Checks whether adding (or updating to) the edge
    /// predecessor -&gt; activity creates a cycle in the dependency graph. A cycle is
    /// formed exactly when a path already exists from the successor back to the
    /// predecessor. The dependency being updated (<paramref name="excludedId"/>) is
    /// ignored so its existing edge does not count against the proposal.
    /// </summary>
    private async Task<Error?> WouldCreateCycleAsync(Guid activityId, Guid predecessorActivityId, Guid planVersionId, Guid? excludedId, CancellationToken cancellationToken)
    {
        var edges = await _context.Set<PlanDependency>()
            .Where(d => d.PlanVersionId == planVersionId && d.Id != excludedId)
            .Select(d => new { d.ActivityId, d.PredecessorActivityId })
            .ToListAsync(cancellationToken);

        var outgoing = edges
            .GroupBy(e => e.PredecessorActivityId)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ActivityId).ToList());

        var visited = new HashSet<Guid>();

        bool Reachable(Guid node)
        {
            if (!visited.Add(node)) return false;
            if (!outgoing.TryGetValue(node, out var next)) return false;

            foreach (var target in next)
            {
                if (target == predecessorActivityId || Reachable(target)) return true;
            }

            return false;
        }

        // The proposed edge is predecessor -> activity. It creates a cycle only if
        // the successor can already reach the predecessor.
        return Reachable(activityId) ? Error.Validation("This dependency would create a cycle") : null;
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

        return DemoTenantId;
    }

    private static Guid? TryParseGuid(string value)
    {
        return Guid.TryParse(value, out var parsed) ? parsed : null;
    }
}