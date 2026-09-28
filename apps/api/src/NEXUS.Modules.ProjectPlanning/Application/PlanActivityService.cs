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

public class PlanActivityService : IPlanActivityService
{
    private readonly DbContext _context;
    private readonly IAuditService _auditService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IPlanSchedulingService _schedulingService;
    private readonly IPlanSchedulingAnalysisService _analysisService;
    private readonly SummaryTaskRollupService _summaryRollup;

    public PlanActivityService(DbContext context, IAuditService auditService, IHttpContextAccessor httpContextAccessor, IPlanSchedulingService? schedulingService = null, IPlanSchedulingAnalysisService? analysisService = null, SummaryTaskRollupService? summaryRollup = null)
    {
        _context = context;
        _auditService = auditService;
        _httpContextAccessor = httpContextAccessor;
        _schedulingService = schedulingService ?? new PlanSchedulingService(context, new AutoSchedulingEngine());
        _analysisService = analysisService ?? new PlanSchedulingAnalysisService(context, new CriticalPathAnalyzer());
        _summaryRollup = summaryRollup ?? new SummaryTaskRollupService(context);
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

        if (dto.Duration < 0)
        {
            return Result<PlanActivityDto>.Failure(Error.Validation("Duration cannot be negative"));
        }

        if (dto.Type == PlanActivityType.Milestone && dto.Duration != 0)
        {
            return Result<PlanActivityDto>.Failure(Error.Validation("Milestones always have zero duration"));
        }

        if (dto.ActualFinish.HasValue && !dto.ActualStart.HasValue)
        {
            return Result<PlanActivityDto>.Failure(Error.Validation("Actual Finish requires Actual Start"));
        }

        if (RequiresConstraintDate(dto.ConstraintType) && !dto.ConstraintDate.HasValue)
            return Result<PlanActivityDto>.Failure(Error.Validation($"{dto.ConstraintType} requires a constraint date"));

        if (dto.WbsId.HasValue &&
            !await WbsNodeExistsInVersionAsync(dto.WbsId.Value, workingVersion.Id, cancellationToken))
        {
            return Result<PlanActivityDto>.Failure(Error.Validation("WBS node must belong to the same plan version"));
        }

        if (dto.CalendarId.HasValue &&
            !await CalendarExistsInVersionAsync(dto.CalendarId.Value, workingVersion.Id, cancellationToken))
        {
            return Result<PlanActivityDto>.Failure(Error.Validation("Calendar must belong to the same plan version"));
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
        if (activity.SchedulingMode == PlanSchedulingMode.Automatic)
        {
            var createSchedule = await _schedulingService.RecalculateAsync(
                workingVersion.Id,
                [new SchedulingSeed(activity.Id, activity.StartDate.HasValue ? SchedulingAnchor.Start : SchedulingAnchor.Finish)],
                userId,
                cancellationToken);
            if (createSchedule.IsFailure) return Result<PlanActivityDto>.Failure(createSchedule.Error!);
        }
        await _summaryRollup.RecalculateAsync(workingVersion.Id, userId, cancellationToken: cancellationToken);
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

        return Result<PlanActivityDto>.Success(await BuildComputedDtoAsync(activity, cancellationToken));
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

        if (dto.Duration < 0)
        {
            return Result<PlanActivityDto>.Failure(Error.Validation("Duration cannot be negative"));
        }

        if (dto.Type == PlanActivityType.Milestone && dto.Duration != 0)
        {
            return Result<PlanActivityDto>.Failure(Error.Validation("Milestones always have zero duration"));
        }

        if (dto.ActualFinish.HasValue && !dto.ActualStart.HasValue)
        {
            return Result<PlanActivityDto>.Failure(Error.Validation("Actual Finish requires Actual Start"));
        }

        if (RequiresConstraintDate(dto.ConstraintType) && !dto.ConstraintDate.HasValue)
            return Result<PlanActivityDto>.Failure(Error.Validation($"{dto.ConstraintType} requires a constraint date"));

        if (dto.WbsId.HasValue &&
            !await WbsNodeExistsInVersionAsync(dto.WbsId.Value, activity.PlanVersionId, cancellationToken))
        {
            return Result<PlanActivityDto>.Failure(Error.Validation("WBS node must belong to the same plan version"));
        }

        if (dto.CalendarId.HasValue &&
            !await CalendarExistsInVersionAsync(dto.CalendarId.Value, activity.PlanVersionId, cancellationToken))
        {
            return Result<PlanActivityDto>.Failure(Error.Validation("Calendar must belong to the same plan version"));
        }

        var codeExists = await _context.Set<PlanActivity>()
            .AnyAsync(a => a.PlanVersionId == activity.PlanVersionId && a.Code == code && a.Id != activity.Id, cancellationToken);
        if (codeExists)
        {
            return Result<PlanActivityDto>.Failure(Error.Conflict($"An activity code '{code}' already exists in this plan version"));
        }

        var originalValues = System.Text.Json.JsonSerializer.Serialize(activity.ToDto());
        var previousSummaryIds = await _summaryRollup.RecalculateAsync(activity.PlanVersionId, userId, cancellationToken: cancellationToken);
        var isSummaryTask = previousSummaryIds.Contains(activity.Id);
        // Planning editors submit date-only values, while persisted schedule
        // values normally contain working-hour times (e.g. 08:00/17:00).
        // Compare the planning date, not the time component, otherwise a
        // Finish-only edit is misclassified as a Start edit and the scheduler
        // anchors from Start, effectively discarding the new Finish date.
        var startChanged = PlanningDateChanged(activity.StartDate, dto.StartDate);
        var finishChanged = PlanningDateChanged(activity.EndDate, dto.EndDate);
        var durationChanged = activity.Duration != dto.Duration;
        var schedulingModeChanged = activity.SchedulingMode != dto.SchedulingMode;
        var constraintChanged = activity.ConstraintType != dto.ConstraintType || PlanningDateChanged(activity.ConstraintDate, dto.ConstraintDate);
        var scheduleChanged = startChanged || finishChanged || durationChanged || schedulingModeChanged || constraintChanged || activity.Type != dto.Type || activity.CalendarId != dto.CalendarId;
        activity.Code = code;
        activity.Name = name;
        activity.Description = dto.Description?.Trim();
        activity.Type = isSummaryTask ? PlanActivityType.Task : dto.Type;
        activity.SchedulingMode = dto.SchedulingMode;
        activity.ConstraintType = dto.ConstraintType;
        activity.ConstraintDate = dto.ConstraintDate;
        activity.Deadline = dto.Deadline;
        if (!isSummaryTask)
        {
            activity.StartDate = dto.StartDate;
            activity.EndDate = dto.EndDate;
        }
        activity.ActualStart = dto.ActualStart;
        activity.ActualFinish = dto.ActualFinish;
        activity.CalendarId = dto.CalendarId;
        if (!isSummaryTask) activity.Duration = dto.Duration;
        activity.EstimateHours = dto.EstimateHours;
        if (!isSummaryTask) activity.Progress = dto.Progress;
        activity.WbsId = dto.WbsId;
        activity.TextFormat = dto.TextFormat.ToEntity();
        activity.SortOrder = dto.SortOrder;
        activity.UpdatedBy = userId;

        _context.Entry(activity).Property(a => a.RowVersion).OriginalValue = dto.RowVersion;

        if (!isSummaryTask && scheduleChanged && activity.SchedulingMode == PlanSchedulingMode.Automatic)
        {
            var anchor = startChanged ? SchedulingAnchor.Start
                : finishChanged ? SchedulingAnchor.Finish
                : durationChanged && activity.StartDate.HasValue ? SchedulingAnchor.Duration
                : activity.StartDate.HasValue ? SchedulingAnchor.Start
                : SchedulingAnchor.Finish;
            var schedule = await _schedulingService.RecalculateAsync(
                activity.PlanVersionId,
                [new SchedulingSeed(activity.Id, anchor)],
                userId,
                cancellationToken);
            if (schedule.IsFailure) return Result<PlanActivityDto>.Failure(schedule.Error!);
        }

        await _summaryRollup.RecalculateAsync(activity.PlanVersionId, userId, previousSummaryIds, cancellationToken);

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

        return Result<PlanActivityDto>.Success(await BuildComputedDtoAsync(activity, cancellationToken));
    }

    public async Task<Result<PlanActivityDto>> MoveAsync(Guid planId, Guid activityId, MovePlanActivityDto dto, string userId, CancellationToken cancellationToken = default)
    {
        var activity = await _context.Set<PlanActivity>()
            .FirstOrDefaultAsync(item => item.Id == activityId && !item.IsDeleted, cancellationToken);
        if (activity is null) return Result<PlanActivityDto>.Failure(Error.NotFound("Activity not found"));

        var editError = await GetEditErrorAsync(activity, planId, cancellationToken);
        if (editError is not null) return Result<PlanActivityDto>.Failure(editError);
        var previousSummaryIds = await _summaryRollup.RecalculateAsync(activity.PlanVersionId, userId, cancellationToken: cancellationToken);
        if (dto.TargetActivityId == activityId) return Result<PlanActivityDto>.Failure(Error.Validation("An activity cannot be moved relative to itself"));

        var nodes = await _context.Set<PlanWbsNode>()
            .Where(node => node.PlanVersionId == activity.PlanVersionId && !node.IsDeleted)
            .ToListAsync(cancellationToken);
        var sourceNode = activity.WbsId.HasValue ? nodes.FirstOrDefault(node => node.Id == activity.WbsId.Value) : null;
        var targetActivity = dto.TargetActivityId.HasValue
            ? await _context.Set<PlanActivity>().FirstOrDefaultAsync(item => item.Id == dto.TargetActivityId.Value && !item.IsDeleted, cancellationToken)
            : null;
        if (dto.TargetActivityId.HasValue && targetActivity is null)
            return Result<PlanActivityDto>.Failure(Error.NotFound("Drop target activity not found"));
        if (targetActivity is not null && targetActivity.PlanVersionId != activity.PlanVersionId)
            return Result<PlanActivityDto>.Failure(Error.Validation("Activities must belong to the same plan version"));

        var targetNode = targetActivity?.WbsId is Guid targetWbsId
            ? nodes.FirstOrDefault(node => node.Id == targetWbsId)
            : null;
        if (targetActivity is not null && targetActivity.WbsId.HasValue && targetNode is null)
            return Result<PlanActivityDto>.Failure(Error.Validation("Drop target WBS node is not in this plan version"));

        if (sourceNode is null)
        {
            if (dto.Position == PlanActivityMovePosition.Child || targetActivity?.WbsId is not null)
                return Result<PlanActivityDto>.Failure(Error.Validation("This activity is not linked to a movable WBS node"));
            if (targetActivity is null || targetActivity.WbsId is not null)
                return Result<PlanActivityDto>.Failure(Error.Validation("A root activity can only move relative to another root activity"));
            if (dto.RowVersion.Length == 0)
                return Result<PlanActivityDto>.Failure(Error.Conflict("The activity version is required. Reload and try again."));

            var rootActivities = await _context.Set<PlanActivity>()
                .Where(item => item.PlanVersionId == activity.PlanVersionId && !item.IsDeleted && item.WbsId == null)
                .OrderBy(item => item.SortOrder).ThenBy(item => item.Code)
                .ToListAsync(cancellationToken);
            var sourceIndex = rootActivities.FindIndex(item => item.Id == activity.Id);
            var targetIndex = rootActivities.FindIndex(item => item.Id == targetActivity!.Id);
            if (sourceIndex < 0 || targetIndex < 0)
                return Result<PlanActivityDto>.Failure(Error.Validation("The activity is not in the current root structure"));
            rootActivities.RemoveAt(sourceIndex);
            targetIndex = rootActivities.FindIndex(item => item.Id == targetActivity!.Id);
            rootActivities.Insert(Math.Clamp(dto.Position == PlanActivityMovePosition.Before ? targetIndex : targetIndex + 1, 0, rootActivities.Count), activity);
            var originalValues = System.Text.Json.JsonSerializer.Serialize(activity.ToDto());
            for (var index = 0; index < rootActivities.Count; index++) rootActivities[index].SortOrder = index;
            _context.Entry(activity).Property(item => item.RowVersion).OriginalValue = dto.RowVersion;
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
                EntityType = "PlanActivity", EntityId = activity.Id, Action = "Move", UserId = TryParseGuid(userId),
                OriginalValues = originalValues,
                NewValues = System.Text.Json.JsonSerializer.Serialize(new { activity.Id, dto.Position, dto.TargetActivityId }),
                Timestamp = DateTime.UtcNow, CreatedBy = userId
            }, cancellationToken);
            return Result<PlanActivityDto>.Success(await BuildComputedDtoAsync(activity, cancellationToken));
        }

        // A WBS node can legally contain more than one activity. In that case
        // Before/After reorders the activities inside the node while preserving
        // the node hierarchy; moving the node itself is reserved for a drop
        // whose target belongs to a different WBS node.
        if (targetActivity is not null && targetActivity.WbsId == sourceNode.Id && (dto.Position is PlanActivityMovePosition.Before or PlanActivityMovePosition.After))
        {
            if (dto.RowVersion.Length == 0)
                return Result<PlanActivityDto>.Failure(Error.Conflict("The activity version is required. Reload and try again."));

            var activitiesInNode = await _context.Set<PlanActivity>()
                .Where(item => item.PlanVersionId == activity.PlanVersionId && !item.IsDeleted && item.WbsId == sourceNode.Id)
                .OrderBy(item => item.SortOrder).ThenBy(item => item.Code)
                .ToListAsync(cancellationToken);
            var sourceIndex = activitiesInNode.FindIndex(item => item.Id == activity.Id);
            var targetIndex = activitiesInNode.FindIndex(item => item.Id == targetActivity.Id);
            if (sourceIndex < 0 || targetIndex < 0)
                return Result<PlanActivityDto>.Failure(Error.Validation("The activity is not in the current WBS structure"));

            var originalValues = System.Text.Json.JsonSerializer.Serialize(activity.ToDto());
            activitiesInNode.RemoveAt(sourceIndex);
            targetIndex = activitiesInNode.FindIndex(item => item.Id == targetActivity.Id);
            activitiesInNode.Insert(Math.Clamp(dto.Position == PlanActivityMovePosition.Before ? targetIndex : targetIndex + 1, 0, activitiesInNode.Count), activity);
            for (var index = 0; index < activitiesInNode.Count; index++) activitiesInNode[index].SortOrder = index;
            _context.Entry(activity).Property(item => item.RowVersion).OriginalValue = dto.RowVersion;
            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                return Result<PlanActivityDto>.Failure(Error.Conflict("The plan structure was modified by another user. Reload and try again."));
            }

            await _auditService.LogAsync(new AuditEntry
            {
                EntityType = "PlanActivity", EntityId = activity.Id, Action = "Move", UserId = TryParseGuid(userId),
                OriginalValues = originalValues,
                NewValues = System.Text.Json.JsonSerializer.Serialize(new { activity.Id, dto.Position, dto.TargetActivityId, sourceWbsId = sourceNode.Id }),
                Timestamp = DateTime.UtcNow, CreatedBy = userId
            }, cancellationToken);
            return Result<PlanActivityDto>.Success(await BuildComputedDtoAsync(activity, cancellationToken));
        }

        if (dto.WbsRowVersion.Length == 0)
            return Result<PlanActivityDto>.Failure(Error.Conflict("The WBS version is required. Reload and try again."));

        Guid? parentWbsId;
        if (dto.Position == PlanActivityMovePosition.Child)
        {
            parentWbsId = dto.TargetWbsId ?? targetNode?.Id;
            if (!parentWbsId.HasValue && dto.TargetActivityId.HasValue)
                return Result<PlanActivityDto>.Failure(Error.Validation("Choose a valid WBS parent"));
            if (parentWbsId.HasValue && nodes.All(node => node.Id != parentWbsId.Value))
                return Result<PlanActivityDto>.Failure(Error.Validation("The target WBS node must belong to this plan version"));
        }
        else
        {
            if (targetNode is null)
                return Result<PlanActivityDto>.Failure(Error.Validation("Before/after drops require a target activity with a WBS node"));
            parentWbsId = targetNode.ParentWbsId;
        }

        if (sourceNode.Id == parentWbsId || (parentWbsId.HasValue && WouldCreateWbsCycle(sourceNode.Id, parentWbsId.Value, nodes)))
            return Result<PlanActivityDto>.Failure(Error.Validation("An activity cannot be moved under itself or one of its descendants"));

        var originalNodeValues = System.Text.Json.JsonSerializer.Serialize(sourceNode.ToDto());
        var oldParent = sourceNode.ParentWbsId;
        var siblings = nodes.Where(node => node.ParentWbsId == parentWbsId && node.Id != sourceNode.Id)
            .OrderBy(node => node.SortOrder).ThenBy(node => node.Code).ToList();
        var insertIndex = siblings.Count;
        if (dto.Position is PlanActivityMovePosition.Before or PlanActivityMovePosition.After)
        {
            var targetIndex = siblings.FindIndex(node => node.Id == targetNode!.Id);
            if (targetIndex < 0) return Result<PlanActivityDto>.Failure(Error.Validation("The target is no longer in the current WBS structure"));
            insertIndex = dto.Position == PlanActivityMovePosition.Before ? targetIndex : targetIndex + 1;
        }
        else if (dto.SortOrder.HasValue)
        {
            insertIndex = Math.Clamp(dto.SortOrder.Value, 0, siblings.Count);
        }
        siblings.Insert(insertIndex, sourceNode);
        sourceNode.ParentWbsId = parentWbsId;
        for (var index = 0; index < siblings.Count; index++) siblings[index].SortOrder = index;

        if (oldParent != parentWbsId)
        {
            var oldSiblings = nodes.Where(node => node.ParentWbsId == oldParent && node.Id != sourceNode.Id)
                .OrderBy(node => node.SortOrder).ThenBy(node => node.Code).ToList();
            for (var index = 0; index < oldSiblings.Count; index++) oldSiblings[index].SortOrder = index;
        }

        await _summaryRollup.RecalculateAsync(activity.PlanVersionId, userId, previousSummaryIds, cancellationToken);

        _context.Entry(sourceNode).Property(node => node.RowVersion).OriginalValue = dto.WbsRowVersion;
        if (targetNode is not null && targetNode.Id != sourceNode.Id && dto.TargetWbsRowVersion.Length > 0)
            _context.Entry(targetNode).Property(node => node.RowVersion).OriginalValue = dto.TargetWbsRowVersion;

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<PlanActivityDto>.Failure(Error.Conflict("The plan structure was modified by another user. Reload and try again."));
        }

        await _auditService.LogAsync(new AuditEntry
        {
            EntityType = "PlanActivity", EntityId = activity.Id, Action = "Move", UserId = TryParseGuid(userId),
            OriginalValues = originalNodeValues,
            NewValues = System.Text.Json.JsonSerializer.Serialize(new { activity.Id, oldParent, parentWbsId, dto.Position, dto.TargetActivityId }),
            Timestamp = DateTime.UtcNow, CreatedBy = userId
        }, cancellationToken);
        return Result<PlanActivityDto>.Success(await BuildComputedDtoAsync(activity, cancellationToken));
    }

    private static bool WouldCreateWbsCycle(Guid nodeId, Guid parentId, IReadOnlyCollection<PlanWbsNode> nodes)
    {
        var byId = nodes.ToDictionary(node => node.Id);
        var visited = new HashSet<Guid> { nodeId };
        Guid? current = parentId;
        while (current.HasValue)
        {
            if (!visited.Add(current.Value)) return true;
            if (!byId.TryGetValue(current.Value, out var parent)) return false;
            current = parent.ParentWbsId;
        }
        return false;
    }

    public async Task<Result> DeleteAsync(Guid planId, Guid activityId, byte[] rowVersion, string userId, CancellationToken cancellationToken = default)
    {
        var activity = await _context.Set<PlanActivity>()
            .FirstOrDefaultAsync(a => a.Id == activityId, cancellationToken);
        if (activity is null) return Result.Failure(Error.NotFound("Activity not found"));

        var editError = await GetEditErrorAsync(activity, planId, cancellationToken);
        if (editError is not null) return Result.Failure(editError);

        var previousSummaryIds = await _summaryRollup.RecalculateAsync(activity.PlanVersionId, userId, cancellationToken: cancellationToken);
        activity.IsDeleted = true;
        activity.UpdatedBy = userId;
        await _summaryRollup.RecalculateAsync(activity.PlanVersionId, userId, previousSummaryIds, cancellationToken);

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

        var summaryIds = await _summaryRollup.RecalculateAsync(workingVersion.Id, "System", cancellationToken: cancellationToken);
        var activities = await _context.Set<PlanActivity>()
            .Where(a => a.PlanVersionId == workingVersion.Id)
            .OrderBy(a => a.SortOrder)
            .ThenBy(a => a.Code)
            .ToListAsync(cancellationToken);

        var analysis = await _analysisService.AnalyzeAsync(workingVersion.Id, cancellationToken);
        var metrics = analysis.IsSuccess ? analysis.Value!.ActivityMetrics : new Dictionary<Guid, CriticalPathTaskMetrics>();
        var baseline = await GetCurrentBaselineActivitiesAsync(planId, cancellationToken);
        var workingCalendar = await GetCurrentWorkingCalendarAsync(planId, cancellationToken);
        return Result<IReadOnlyList<PlanActivityDto>>.Success(activities.Select(activity => EnrichWithBaseline(activity.ToDto(metrics.GetValueOrDefault(activity.Id), summaryIds.Contains(activity.Id)), baseline, workingCalendar)).ToList());
    }

    private async Task<PlanActivityDto> BuildComputedDtoAsync(PlanActivity activity, CancellationToken cancellationToken)
    {
        var summaryIds = await _summaryRollup.RecalculateAsync(activity.PlanVersionId, activity.UpdatedBy ?? "System", cancellationToken: cancellationToken);
        var analysis = await _analysisService.AnalyzeAsync(activity.PlanVersionId, cancellationToken);
        var metrics = analysis.IsSuccess
            ? analysis.Value!.ActivityMetrics.GetValueOrDefault(activity.Id)
            : null;
        var baseline = await GetCurrentBaselineActivitiesAsync(
            await _context.Set<PlanVersion>()
                .Where(version => version.Id == activity.PlanVersionId)
                .Select(version => version.PlanId)
                .FirstAsync(cancellationToken),
            cancellationToken);
        var workingCalendar = await GetCurrentWorkingCalendarAsync(
            await _context.Set<PlanVersion>()
                .Where(version => version.Id == activity.PlanVersionId)
                .Select(version => version.PlanId)
                .FirstAsync(cancellationToken),
            cancellationToken);
        return EnrichWithBaseline(activity.ToDto(metrics, summaryIds.Contains(activity.Id)), baseline, workingCalendar);
    }

    private sealed record BaselineActivityLookup(
        IReadOnlyDictionary<Guid, PlanActivityBaselineComparison.Snapshot> ByLineage,
        IReadOnlyDictionary<string, PlanActivityBaselineComparison.Snapshot> LegacyByCode);

    private async Task<BaselineActivityLookup> GetCurrentBaselineActivitiesAsync(Guid planId, CancellationToken cancellationToken)
    {
        var baseline = await _context.Set<PlanVersion>()
            .Where(version => version.PlanId == planId && version.Type == PlanVersionType.Baseline && version.Status == PlanVersionStatus.Approved)
            .OrderByDescending(version => version.VersionNumber)
            .Select(version => new { version.Id })
            .FirstOrDefaultAsync(cancellationToken);
        if (baseline is null) return new BaselineActivityLookup(new Dictionary<Guid, PlanActivityBaselineComparison.Snapshot>(), new Dictionary<string, PlanActivityBaselineComparison.Snapshot>(StringComparer.OrdinalIgnoreCase));
        var snapshots = await _context.Set<PlanActivity>()
            .Where(activity => activity.PlanVersionId == baseline.Id)
            .Select(activity => new { activity.Id, activity.SourceActivityId, activity.Code, activity.StartDate, activity.EndDate, activity.Duration })
            .ToListAsync(cancellationToken);
        var byLineage = snapshots
            .Select(activity => new { Key = activity.SourceActivityId ?? activity.Id, Snapshot = new PlanActivityBaselineComparison.Snapshot(activity.StartDate, activity.EndDate, activity.Duration) })
            .GroupBy(item => item.Key)
            .Where(group => group.Count() == 1)
            .ToDictionary(group => group.Key, group => group.Single().Snapshot);
        var legacyByCode = snapshots
            .Where(activity => !activity.SourceActivityId.HasValue && !string.IsNullOrWhiteSpace(activity.Code))
            .GroupBy(activity => activity.Code, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() == 1)
            .ToDictionary(group => group.Key, group => new PlanActivityBaselineComparison.Snapshot(group.Single().StartDate, group.Single().EndDate, group.Single().Duration), StringComparer.OrdinalIgnoreCase);
        return new BaselineActivityLookup(byLineage, legacyByCode);
    }

    private async Task<PlanningWorkingCalendar> GetCurrentWorkingCalendarAsync(Guid planId, CancellationToken cancellationToken)
    {
        var workingVersionId = await _context.Set<PlanVersion>()
            .Where(version => version.PlanId == planId && version.Type == PlanVersionType.Working && version.Status == PlanVersionStatus.Active)
            .Select(version => (Guid?)version.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (!workingVersionId.HasValue) return new PlanningWorkingCalendar(null);

        var calendar = await _context.Set<PlanCalendar>()
            .Include(item => item.DayRules)
            .Include(item => item.Holidays)
            .Where(item => item.PlanVersionId == workingVersionId.Value && !item.IsDeleted)
            .OrderByDescending(item => item.IsDefault)
            .ThenBy(item => item.Name)
            .FirstOrDefaultAsync(cancellationToken);
        if (calendar is null) return new PlanningWorkingCalendar(null);

        var definition = new AutoScheduleCalendar(
            calendar.Id,
            calendar.DayRules.Where(rule => !rule.IsDeleted).Select(rule => new CalendarDayDefinition(
                rule.DayOfWeek, rule.IsWorkingDay, rule.StartTime, rule.EndTime, rule.BreakStartTime, rule.BreakEndTime)).ToList(),
            calendar.Holidays.Where(holiday => !holiday.IsDeleted).Select(holiday => new CalendarHolidayDefinition(
                holiday.Date, holiday.EndDate, holiday.IsRecurring)).ToList());
        return new PlanningWorkingCalendar(definition);
    }

    private static PlanActivityDto EnrichWithBaseline(
        PlanActivityDto dto,
        BaselineActivityLookup baseline,
        PlanningWorkingCalendar workingCalendar)
    {
        var lineageKey = dto.SourceActivityId ?? dto.Id;
        if (baseline.ByLineage.TryGetValue(lineageKey, out var snapshot))
            return PlanActivityBaselineComparison.Apply(dto, snapshot, workingCalendar);

        // Compatibility only for baselines created before SourceActivityId was introduced.
        if (!dto.SourceActivityId.HasValue && baseline.LegacyByCode.TryGetValue(dto.Code, out var legacySnapshot))
            return PlanActivityBaselineComparison.Apply(dto, legacySnapshot, workingCalendar);
        return dto;
    }

    private async Task<PlanVersion?> GetActiveWorkingVersionAsync(Guid planId, CancellationToken cancellationToken)
    {
        return await _context.Set<PlanVersion>()
            .FirstOrDefaultAsync(v => v.PlanId == planId && v.Type == PlanVersionType.Working && v.Status == PlanVersionStatus.Active, cancellationToken);
    }

    private static bool RequiresConstraintDate(PlanConstraintType type) => type is not PlanConstraintType.AsSoonAsPossible and not PlanConstraintType.AsLateAsPossible;

    private static bool PlanningDateChanged(DateTime? current, DateTime? requested) => current?.Date != requested?.Date;

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

    private async Task<bool> CalendarExistsInVersionAsync(Guid calendarId, Guid planVersionId, CancellationToken cancellationToken)
    {
        return await _context.Set<PlanCalendar>()
            .AnyAsync(calendar => calendar.Id == calendarId && calendar.PlanVersionId == planVersionId, cancellationToken);
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

        return TenantConstants.DemoTenantId;
    }

    private static Guid? TryParseGuid(string value) => GuidHelper.TryParse(value);
}
