using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.ProjectPlanning.Application.DTOs;
using NEXUS.Modules.ProjectPlanning.Application.Mapping;
using NEXUS.Modules.ProjectPlanning.Domain;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Helpers;

namespace NEXUS.Modules.ProjectPlanning.Application;

public sealed class PlanVersionService : IPlanVersionService
{
    private readonly DbContext _context;
    private readonly IAuditService _auditService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public PlanVersionService(
        DbContext context,
        IAuditService auditService,
        IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _auditService = auditService;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<Result<PlanVersionDto>> GetActiveWorkingVersionAsync(
        Guid planId,
        CancellationToken cancellationToken = default)
    {
        var plan = await GetPlanAsync(planId, cancellationToken);
        if (plan is null) return Result<PlanVersionDto>.Failure(Error.NotFound("Plan not found"));

        var working = await GetActiveWorkingAsync(planId, cancellationToken);
        return working is null
            ? Result<PlanVersionDto>.Failure(Error.NotFound("No active working version found"))
            : Result<PlanVersionDto>.Success(working.ToDto());
    }

    public async Task<Result<PlanVersionDto>> CreateWorkingFromCurrentBaselineAsync(
        Guid planId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var plan = await GetPlanAsync(planId, cancellationToken);
        if (plan is null) return Result<PlanVersionDto>.Failure(Error.NotFound("Plan not found"));

        if (await GetActiveWorkingAsync(planId, cancellationToken) is not null)
            return Result<PlanVersionDto>.Failure(Error.Conflict("An active Working version already exists for this plan"));

        var baseline = await GetCurrentBaselineWithModelAsync(planId, cancellationToken);
        if (baseline is null)
            return Result<PlanVersionDto>.Failure(Error.Validation("A current approved Baseline is required before opening a Working version"));

        var tenantId = GetCurrentTenantId();
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            if (await GetActiveWorkingAsync(planId, cancellationToken) is not null)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return Result<PlanVersionDto>.Failure(Error.Conflict("An active Working version already exists for this plan"));
            }

            var versionNumber = await NextVersionNumberAsync(planId, tenantId, cancellationToken);
            var working = CloneVersion(
                baseline,
                PlanVersionType.Working,
                PlanVersionStatus.Active,
                versionNumber,
                $"Working v{versionNumber}",
                tenantId,
                userId,
                sourceVersionId: null,
                sourceBaselineId: baseline.Id);

            _context.Set<PlanVersion>().Add(working);
            await _context.SaveChangesAsync(cancellationToken);
            await AuditAsync("Create", working, null, working.ToDto(), userId, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<PlanVersionDto>.Success(working.ToDto());
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return Result<PlanVersionDto>.Failure(Error.Conflict("A Working version already exists for this plan"));
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<Result> DiscardWorkingVersionAsync(
        Guid planId,
        byte[] rowVersion,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var plan = await GetPlanAsync(planId, cancellationToken);
        if (plan is null) return Result.Failure(Error.NotFound("Plan not found"));

        var working = await GetActiveWorkingAsync(planId, cancellationToken);
        if (working is null) return Result.Failure(Error.NotFound("No active working version found"));
        if (!MatchesRowVersion(working, rowVersion))
            return Result.Failure(Error.Conflict("The Working version was modified by another user. Reload and try again."));

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            working.IsDeleted = true;
            working.UpdatedBy = userId;
            working.UpdatedAt = DateTime.UtcNow;
            _context.Entry(working).Property(v => v.RowVersion).OriginalValue = rowVersion;
            await _context.SaveChangesAsync(cancellationToken);
            await AuditAsync("Delete", working, null, null, userId, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result.Success();
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return Result.Failure(Error.Conflict("The Working version was modified by another user. Reload and try again."));
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<Result<PlanVersionDto>> CreateInitialBaselineAsync(
        Guid planId,
        byte[] rowVersion,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var plan = await GetPlanAsync(planId, cancellationToken);
        if (plan is null) return Result<PlanVersionDto>.Failure(Error.NotFound("Plan not found"));
        if (plan.PlanType != PlanType.Execution)
            return Result<PlanVersionDto>.Failure(Error.Validation("Only an Execution plan can create the initial Baseline"));

        if (await _context.Set<PlanVersion>().AnyAsync(
                v => v.PlanId == planId && v.Type == PlanVersionType.Baseline,
                cancellationToken))
        {
            return Result<PlanVersionDto>.Failure(Error.Conflict("The initial Baseline has already been created for this plan"));
        }

        var working = await GetActiveWorkingWithModelAsync(planId, cancellationToken);
        if (working is null) return Result<PlanVersionDto>.Failure(Error.NotFound("No active working version found"));
        if (!MatchesRowVersion(working, rowVersion))
            return Result<PlanVersionDto>.Failure(Error.Conflict("The Working version was modified by another user. Reload and try again."));

        var tenantId = GetCurrentTenantId();
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var baseline = CloneVersion(
                working,
                PlanVersionType.Baseline,
                PlanVersionStatus.Approved,
                1,
                "Baseline v1",
                tenantId,
                userId,
                sourceVersionId: working.Id,
                sourceBaselineId: null);

            working.IsDeleted = true;
            working.UpdatedBy = userId;
            working.UpdatedAt = DateTime.UtcNow;
            _context.Entry(working).Property(v => v.RowVersion).OriginalValue = rowVersion;
            _context.Set<PlanVersion>().Add(baseline);
            await _context.SaveChangesAsync(cancellationToken);
            await AuditAsync("InitialBaselineCreated", baseline, null, baseline.ToDto(), userId, cancellationToken);
            await AuditAsync("Delete", working, working.ToDto(), null, userId, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<PlanVersionDto>.Success(baseline.ToDto());
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return Result<PlanVersionDto>.Failure(Error.Conflict("The Working version was modified by another user. Reload and try again."));
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return Result<PlanVersionDto>.Failure(Error.Conflict("The initial Baseline already exists for this plan"));
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<Result<PlanVersionDto>> PromoteWorkingToBaselineAsync(
        Guid planId,
        byte[] rowVersion,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var plan = await GetPlanAsync(planId, cancellationToken);
        if (plan is null) return Result<PlanVersionDto>.Failure(Error.NotFound("Plan not found"));

        var working = await GetActiveWorkingWithModelAsync(planId, cancellationToken);
        if (working is null) return Result<PlanVersionDto>.Failure(Error.NotFound("No active working version found"));
        if (!MatchesRowVersion(working, rowVersion))
            return Result<PlanVersionDto>.Failure(Error.Conflict("The Working version was modified by another user. Reload and try again."));

        var currentBaseline = await GetCurrentBaselineAsync(planId, cancellationToken);
        if (currentBaseline is null)
            return Result<PlanVersionDto>.Failure(Error.Validation("Create the initial Baseline before promoting a Working version"));

        var tenantId = GetCurrentTenantId();
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            currentBaseline.Status = PlanVersionStatus.Superseded;
            currentBaseline.UpdatedBy = userId;
            currentBaseline.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);

            var baselineNumber = await NextBaselineVersionNumberAsync(planId, tenantId, cancellationToken);
            var baseline = CloneVersion(
                working,
                PlanVersionType.Baseline,
                PlanVersionStatus.Approved,
                baselineNumber,
                $"Baseline v{baselineNumber}",
                tenantId,
                userId,
                sourceVersionId: working.Id,
                sourceBaselineId: currentBaseline.Id);

            working.IsDeleted = true;
            working.UpdatedBy = userId;
            working.UpdatedAt = DateTime.UtcNow;
            _context.Entry(working).Property(v => v.RowVersion).OriginalValue = rowVersion;
            _context.Set<PlanVersion>().Add(baseline);
            await _context.SaveChangesAsync(cancellationToken);

            var workingNumber = await NextVersionNumberAsync(planId, tenantId, cancellationToken);
            var nextWorking = CloneVersion(
                baseline,
                PlanVersionType.Working,
                PlanVersionStatus.Active,
                workingNumber,
                $"Working v{workingNumber}",
                tenantId,
                userId,
                sourceVersionId: null,
                sourceBaselineId: baseline.Id);
            _context.Set<PlanVersion>().Add(nextWorking);
            await _context.SaveChangesAsync(cancellationToken);

            await AuditAsync("Supersede", currentBaseline, null, currentBaseline.ToDto(), userId, cancellationToken);
            await AuditAsync("Promote", baseline, currentBaseline.ToDto(), baseline.ToDto(), userId, cancellationToken);
            await AuditAsync("Delete", working, working.ToDto(), null, userId, cancellationToken);
            await AuditAsync("Create", nextWorking, null, nextWorking.ToDto(), userId, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<PlanVersionDto>.Success(baseline.ToDto());
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return Result<PlanVersionDto>.Failure(Error.Conflict("The Working or current Baseline version was modified by another user. Reload and try again."));
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return Result<PlanVersionDto>.Failure(Error.Conflict("The Working version could not be promoted"));
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<Result<PlanVersionDto>> CaptureBaselineAsync(
        Guid planId,
        byte[] rowVersion,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var plan = await GetPlanAsync(planId, cancellationToken);
        if (plan is null) return Result<PlanVersionDto>.Failure(Error.NotFound("Plan not found"));

        var working = await GetActiveWorkingWithModelAsync(planId, cancellationToken);
        if (working is null) return Result<PlanVersionDto>.Failure(Error.NotFound("No active working version found"));
        if (!MatchesRowVersion(working, rowVersion))
            return Result<PlanVersionDto>.Failure(Error.Conflict("The Working version was modified by another user. Reload and try again."));

        var tenantId = GetCurrentTenantId();
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var baselineNumber = await NextBaselineVersionNumberAsync(planId, tenantId, cancellationToken);
            var baseline = CloneVersion(
                working,
                PlanVersionType.Baseline,
                PlanVersionStatus.Approved,
                baselineNumber,
                $"Baseline v{baselineNumber}",
                tenantId,
                userId,
                sourceVersionId: working.Id,
                sourceBaselineId: null);

            _context.Set<PlanVersion>().Add(baseline);
            await _context.SaveChangesAsync(cancellationToken);
            await AuditAsync("BaselineCaptured", baseline, null, baseline.ToDto(), userId, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<PlanVersionDto>.Success(baseline.ToDto());
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return Result<PlanVersionDto>.Failure(Error.Conflict("The Working version was modified by another user. Reload and try again."));
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return Result<PlanVersionDto>.Failure(Error.Conflict("The Baseline could not be created. No partial snapshot was saved."));
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private async Task<Plan?> GetPlanAsync(Guid planId, CancellationToken cancellationToken) =>
        await _context.Set<Plan>().FirstOrDefaultAsync(p => p.Id == planId, cancellationToken);

    private async Task<PlanVersion?> GetActiveWorkingAsync(Guid planId, CancellationToken cancellationToken) =>
        await _context.Set<PlanVersion>().FirstOrDefaultAsync(
            v => v.PlanId == planId && v.Type == PlanVersionType.Working && v.Status == PlanVersionStatus.Active,
            cancellationToken);

    private async Task<PlanVersion?> GetActiveWorkingWithModelAsync(Guid planId, CancellationToken cancellationToken) =>
        await VersionModelQuery()
            .FirstOrDefaultAsync(
                v => v.PlanId == planId && v.Type == PlanVersionType.Working && v.Status == PlanVersionStatus.Active,
                cancellationToken);

    private async Task<PlanVersion?> GetCurrentBaselineAsync(Guid planId, CancellationToken cancellationToken) =>
        await _context.Set<PlanVersion>()
            .Where(v => v.PlanId == planId && v.Type == PlanVersionType.Baseline && v.Status == PlanVersionStatus.Approved)
            .OrderByDescending(v => v.VersionNumber)
            .FirstOrDefaultAsync(cancellationToken);

    private async Task<PlanVersion?> GetCurrentBaselineWithModelAsync(Guid planId, CancellationToken cancellationToken) =>
        await VersionModelQuery()
            .Where(v => v.PlanId == planId && v.Type == PlanVersionType.Baseline && v.Status == PlanVersionStatus.Approved)
            .OrderByDescending(v => v.VersionNumber)
            .FirstOrDefaultAsync(cancellationToken);

    private IQueryable<PlanVersion> VersionModelQuery() =>
        _context.Set<PlanVersion>()
            .Include(v => v.WbsNodes)
            .Include(v => v.Activities)
            .Include(v => v.Deliverables)
            .Include(v => v.Dependencies)
            .Include(v => v.Assignments)
            .Include(v => v.Calendars)
                .ThenInclude(c => c.DayRules)
            .Include(v => v.Calendars)
                .ThenInclude(c => c.Holidays);

    private async Task<int> NextVersionNumberAsync(Guid planId, Guid tenantId, CancellationToken cancellationToken)
    {
        var max = await _context.Set<PlanVersion>()
            .IgnoreQueryFilters()
            .Where(v => v.PlanId == planId && v.TenantId == tenantId)
            .Select(v => (int?)v.VersionNumber)
            .MaxAsync(cancellationToken) ?? 0;
        return max + 1;
    }

    private async Task<int> NextBaselineVersionNumberAsync(Guid planId, Guid tenantId, CancellationToken cancellationToken)
    {
        var max = await _context.Set<PlanVersion>()
            .IgnoreQueryFilters()
            .Where(v => v.PlanId == planId && v.TenantId == tenantId && v.Type == PlanVersionType.Baseline)
            .Select(v => (int?)v.VersionNumber)
            .MaxAsync(cancellationToken) ?? 0;
        return max + 1;
    }

    private static bool MatchesRowVersion(PlanVersion version, byte[] expected) =>
        expected is not null && version.RowVersion.SequenceEqual(expected);

    private static PlanVersion CloneVersion(
        PlanVersion source,
        PlanVersionType type,
        PlanVersionStatus status,
        int versionNumber,
        string name,
        Guid tenantId,
        string userId,
        Guid? sourceVersionId,
        Guid? sourceBaselineId)
    {
        var activityIds = source.Activities.ToDictionary(a => a.Id, _ => Guid.NewGuid());
        var wbsIds = source.WbsNodes.ToDictionary(n => n.Id, _ => Guid.NewGuid());
        var deliverableIds = source.Deliverables.ToDictionary(d => d.Id, _ => Guid.NewGuid());
        var calendarIds = source.Calendars.ToDictionary(c => c.Id, _ => Guid.NewGuid());

        var clone = new PlanVersion
        {
            TenantId = tenantId,
            PlanId = source.PlanId,
            VersionNumber = versionNumber,
            Type = type,
            Status = status,
            Name = name,
            SourceVersionId = sourceVersionId,
            SourceBaselineId = sourceBaselineId,
            CreatedBy = userId
        };

        clone.WbsNodes = source.WbsNodes.Select(n => new PlanWbsNode
        {
            Id = wbsIds[n.Id],
            TenantId = tenantId,
            PlanVersionId = clone.Id,
            ParentWbsId = n.ParentWbsId.HasValue && wbsIds.TryGetValue(n.ParentWbsId.Value, out var parentId) ? parentId : null,
            SortOrder = n.SortOrder,
            Code = n.Code,
            Name = n.Name,
            ActivityId = n.ActivityId.HasValue && activityIds.TryGetValue(n.ActivityId.Value, out var activityId) ? activityId : null,
            DeliverableId = n.DeliverableId.HasValue && deliverableIds.TryGetValue(n.DeliverableId.Value, out var deliverableId) ? deliverableId : null,
            TextFormat = n.TextFormat?.Clone(),
            CreatedBy = userId
        }).ToList();

        clone.Activities = source.Activities.Select(a => new PlanActivity
        {
            Id = activityIds[a.Id],
            TenantId = tenantId,
            PlanVersionId = clone.Id,
            SourceActivityId = a.SourceActivityId ?? a.Id,
            Code = a.Code,
            Name = a.Name,
            Description = a.Description,
            Type = a.Type,
            SchedulingMode = a.SchedulingMode,
            ConstraintType = a.ConstraintType,
            ConstraintDate = a.ConstraintDate,
            Deadline = a.Deadline,
            StartDate = a.StartDate,
            EndDate = a.EndDate,
            ActualStart = a.ActualStart,
            ActualFinish = a.ActualFinish,
            CalendarId = a.CalendarId.HasValue && calendarIds.TryGetValue(a.CalendarId.Value, out var calendarId) ? calendarId : null,
            Duration = a.Duration,
            SortOrder = a.SortOrder,
            EstimateHours = a.EstimateHours,
            Progress = a.Progress,
            WbsId = a.WbsId.HasValue && wbsIds.TryGetValue(a.WbsId.Value, out var wbsId) ? wbsId : null,
            TextFormat = a.TextFormat?.Clone(),
            CreatedBy = userId
        }).ToList();

        clone.Deliverables = source.Deliverables.Select(d => new PlanDeliverable
        {
            Id = deliverableIds[d.Id],
            TenantId = tenantId,
            PlanVersionId = clone.Id,
            Name = d.Name,
            Description = d.Description,
            AcceptanceCriteria = d.AcceptanceCriteria,
            DueDate = d.DueDate,
            WbsId = d.WbsId.HasValue && wbsIds.TryGetValue(d.WbsId.Value, out var wbsId) ? wbsId : null,
            ActivityId = d.ActivityId.HasValue && activityIds.TryGetValue(d.ActivityId.Value, out var activityId) ? activityId : null,
            CreatedBy = userId
        }).ToList();

        clone.Dependencies = source.Dependencies.Select(d => new PlanDependency
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PlanVersionId = clone.Id,
            ActivityId = activityIds[d.ActivityId],
            PredecessorActivityId = activityIds[d.PredecessorActivityId],
            Type = d.Type,
            Lag = d.Lag,
            CreatedBy = userId
        }).ToList();

        clone.Assignments = source.Assignments.Select(a => new PlanAssignment
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PlanVersionId = clone.Id,
            ActivityId = activityIds[a.ActivityId],
            ResourceId = a.ResourceId,
            AllocationPct = a.AllocationPct,
            HoursPerWeek = a.HoursPerWeek,
            StartDate = a.StartDate,
            EndDate = a.EndDate,
            CreatedBy = userId
        }).ToList();

        clone.Calendars = source.Calendars.Select(c =>
        {
            var calendar = new PlanCalendar
            {
                Id = calendarIds[c.Id],
                TenantId = tenantId,
                ProjectId = c.ProjectId,
                PlanVersionId = clone.Id,
                TemplateId = c.TemplateId,
                Name = c.Name,
                Description = c.Description,
                EffectiveFrom = c.EffectiveFrom,
                EffectiveTo = c.EffectiveTo,
                IsDefault = c.IsDefault,
                CreatedBy = userId
            };
            calendar.DayRules = c.DayRules.Where(r => !r.IsDeleted).Select(r => new CalendarDayRule
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                PlanningCalendarId = calendar.Id,
                DayOfWeek = r.DayOfWeek,
                IsWorkingDay = r.IsWorkingDay,
                StartTime = r.StartTime,
                EndTime = r.EndTime,
                BreakStartTime = r.BreakStartTime,
                BreakEndTime = r.BreakEndTime,
                CreatedBy = userId
            }).ToList();
            calendar.Holidays = c.Holidays.Where(h => !h.IsDeleted).Select(h => new CalendarHoliday
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                PlanningCalendarId = calendar.Id,
                Name = h.Name,
                Description = h.Description,
                Date = h.Date,
                EndDate = h.EndDate,
                IsRecurring = h.IsRecurring,
                CreatedBy = userId
            }).ToList();
            return calendar;
        }).ToList();

        return clone;
    }

    private Task AuditAsync(
        string action,
        PlanVersion version,
        object? original,
        object? current,
        string userId,
        CancellationToken cancellationToken) =>
        _auditService.LogAsync(new AuditEntry
        {
            EntityType = "PlanVersion",
            EntityId = version.Id,
            Action = action,
            TenantId = GetCurrentTenantId(),
            UserId = Guid.TryParse(userId, out var userIdValue) ? userIdValue : null,
            OriginalValues = original is null ? null : System.Text.Json.JsonSerializer.Serialize(original),
            NewValues = current is null ? null : System.Text.Json.JsonSerializer.Serialize(current),
            Timestamp = DateTime.UtcNow,
            CreatedBy = userId
        }, cancellationToken);

    private Guid GetCurrentTenantId()
    {
        var tenantClaim = _httpContextAccessor.HttpContext?.User.FindFirst("tenantId")?.Value;
        return Guid.TryParse(tenantClaim, out var tenantId) ? tenantId : TenantConstants.DemoTenantId;
    }
}
