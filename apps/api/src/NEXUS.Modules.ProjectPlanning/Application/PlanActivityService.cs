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
    private readonly IPlanGridCommandHistoryService? _commandHistory;

    public PlanActivityService(DbContext context, IAuditService auditService, IHttpContextAccessor httpContextAccessor, IPlanSchedulingService? schedulingService = null, IPlanSchedulingAnalysisService? analysisService = null, SummaryTaskRollupService? summaryRollup = null, IPlanGridCommandHistoryService? commandHistory = null)
    {
        _context = context;
        _auditService = auditService;
        _httpContextAccessor = httpContextAccessor;
        _schedulingService = schedulingService ?? new PlanSchedulingService(context, new AutoSchedulingEngine());
        _analysisService = analysisService ?? new PlanSchedulingAnalysisService(context, new CriticalPathAnalyzer());
        _summaryRollup = summaryRollup ?? new SummaryTaskRollupService(context);
        _commandHistory = commandHistory;
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

        if (dto.Type == PlanActivityType.Task && dto.Duration == 0)
        {
            // New tasks default to one working day when a caller omits duration.
            dto.Duration = 1;
        }

        if (dto.ActualFinish.HasValue && !dto.ActualStart.HasValue)
        {
            return Result<PlanActivityDto>.Failure(Error.Validation("Actual Finish requires Actual Start"));
        }

        if (dto.SchedulingMode == PlanSchedulingMode.Manual && dto.StartDate.HasValue && dto.EndDate.HasValue &&
            dto.EndDate.Value.Date < dto.StartDate.Value.Date)
        {
            return Result<PlanActivityDto>.Failure(Error.Validation("End date must be on or after start date"));
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

        var hasExistingActivities = await _context.Set<PlanActivity>()
            .AnyAsync(a => a.PlanVersionId == workingVersion.Id && !a.IsDeleted, cancellationToken)
            || _context.ChangeTracker.Entries<PlanActivity>()
                .Any(entry => entry.State == EntityState.Added &&
                              entry.Entity.PlanVersionId == workingVersion.Id &&
                              !entry.Entity.IsDeleted);
        if (!hasExistingActivities)
        {
            // Keep the first plan activity anchored to the project start until a
            // user explicitly changes its scheduling mode.
            dto.SchedulingMode = PlanSchedulingMode.Manual;
        }

        var tenantId = GetCurrentTenantId();
        var sortOrder = dto.SortOrder ?? await NextSortOrderAsync(workingVersion.Id, cancellationToken);

        var activity = dto.ToEntity(tenantId, workingVersion.Id, userId);
        activity.SortOrder = sortOrder;
        if (activity.SchedulingMode == PlanSchedulingMode.Automatic &&
            !activity.StartDate.HasValue && !activity.EndDate.HasValue && activity.WbsId.HasValue)
        {
            activity.StartDate = await GetParentActivityStartDateAsync(activity.WbsId.Value, workingVersion.Id, cancellationToken);
        }
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

        if (_commandHistory is not null)
            await _commandHistory.RecordBarrierAsync(planId, workingVersion.Id, userId, "CreateActivity", cancellationToken);

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

        if (dto.SchedulingMode == PlanSchedulingMode.Manual && dto.StartDate.HasValue && dto.EndDate.HasValue &&
            dto.EndDate.Value.Date < dto.StartDate.Value.Date)
        {
            return Result<PlanActivityDto>.Failure(Error.Validation("End date must be on or after start date"));
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
        var historyBefore = _commandHistory is null || _commandHistory.IsReplaying
            ? null
            : PlanGridCommandHistoryService.CaptureActivityFields(activity);
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

        await using var transaction = _commandHistory is { IsReplaying: false } && _context.Database.CurrentTransaction is null
            ? await _context.Database.BeginTransactionAsync(cancellationToken)
            : null;
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
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
            if (historyBefore is not null && _commandHistory is not null)
                await _commandHistory.RecordActivityUpdateAsync(planId, activity.PlanVersionId, userId, historyBefore, activity, cancellationToken);
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            if (transaction is not null) await transaction.RollbackAsync(cancellationToken);
            return Result<PlanActivityDto>.Failure(Error.Conflict("The activity was modified by another user. Reload and try again."));
        }
        catch
        {
            if (transaction is not null) await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Result<PlanActivityDto>.Success(await BuildComputedDtoAsync(activity, cancellationToken));
    }

    public async Task<Result<IReadOnlyList<PlanActivityDto>>> BulkUpdateAsync(Guid planId, BulkUpdatePlanActivitiesDto dto, string userId, CancellationToken cancellationToken = default)
    {
        if (dto.Items is null || dto.Items.Count is < 1 or > 500)
            return Result<IReadOnlyList<PlanActivityDto>>.Failure(Error.Validation("Bulk updates must contain between 1 and 500 activities"));
        if (dto.Items.Any(item => item.ActivityId == Guid.Empty || item.Update is null || item.Update.RowVersion.Length == 0)
            || dto.Items.Select(item => item.ActivityId).Distinct().Count() != dto.Items.Count)
            return Result<IReadOnlyList<PlanActivityDto>>.Failure(Error.Validation("Each activity must be unique and include its row version"));

        var workingVersion = await GetActiveWorkingVersionAsync(planId, cancellationToken);
        if (workingVersion is null)
            return Result<IReadOnlyList<PlanActivityDto>>.Failure(Error.NotFound(await GetActiveWorkingVersionNotAvailableMessageAsync(planId, cancellationToken)));

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        using var commandCollection = _commandHistory?.BeginCollection("BulkEdit");
        var results = new List<PlanActivityDto>(dto.Items.Count);
        foreach (var item in dto.Items)
        {
            var result = await UpdateAsync(planId, item.ActivityId, item.Update, userId, cancellationToken);
            if (result.IsFailure)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<IReadOnlyList<PlanActivityDto>>.Failure(result.Error!);
            }
            results.Add(result.Value!);
        }

        if (_commandHistory is not null)
            await _commandHistory.CommitCollectionAsync(planId, workingVersion.Id, userId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result<IReadOnlyList<PlanActivityDto>>.Success(results);
    }

    public async Task<Result<PlanSchedulingResult>> RecalculateScheduleAsync(Guid planId, string userId, CancellationToken cancellationToken = default)
    {
        var workingVersion = await GetActiveWorkingVersionAsync(planId, cancellationToken);
        if (workingVersion is null)
            return Result<PlanSchedulingResult>.Failure(Error.NotFound(await GetActiveWorkingVersionNotAvailableMessageAsync(planId, cancellationToken)));

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        using var commandCollection = _commandHistory?.BeginCollection("RecalculateSchedule");
        var activities = await _context.Set<PlanActivity>()
            .Where(activity => activity.PlanVersionId == workingVersion.Id && !activity.IsDeleted)
            .ToListAsync(cancellationToken);
        if (activities.Count == 0)
        {
            await transaction.CommitAsync(cancellationToken);
            return Result<PlanSchedulingResult>.Success(new PlanSchedulingResult(0, []));
        }

        var beforeValues = activities.ToDictionary(activity => activity.Id, PlanGridCommandHistoryService.CaptureActivityFields);
        try
        {
            var recalculation = await _schedulingService.RecalculateAsync(
                workingVersion.Id,
                activities.Select(activity => new SchedulingSeed(activity.Id)).ToList(),
                userId,
                cancellationToken);
            if (recalculation.IsFailure)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<PlanSchedulingResult>.Failure(recalculation.Error!);
            }

            // Persist schedule and summary changes first so the command history
            // records the post-save RowVersions used for optimistic concurrency.
            await _context.SaveChangesAsync(cancellationToken);
            foreach (var activity in activities)
            {
                var before = beforeValues[activity.Id];
                var after = PlanGridCommandHistoryService.CaptureActivityFields(activity);
                var beforeJson = System.Text.Json.JsonSerializer.Serialize(before);
                var afterJson = System.Text.Json.JsonSerializer.Serialize(after);
                if (string.Equals(beforeJson, afterJson, StringComparison.Ordinal)) continue;

                await _auditService.LogAsync(new AuditEntry
                {
                    EntityType = "PlanActivity",
                    EntityId = activity.Id,
                    Action = "RecalculateSchedule",
                    UserId = TryParseGuid(userId),
                    OriginalValues = beforeJson,
                    NewValues = afterJson,
                    Timestamp = DateTime.UtcNow,
                    CreatedBy = userId
                }, cancellationToken);
                if (_commandHistory is not null)
                    await _commandHistory.RecordActivityUpdateAsync(planId, workingVersion.Id, userId, before, activity, cancellationToken);
            }

            if (_commandHistory is not null)
                await _commandHistory.CommitCollectionAsync(planId, workingVersion.Id, userId, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<PlanSchedulingResult>.Success(recalculation.Value!);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<PlanSchedulingResult>.Failure(Error.Conflict("The plan changed while its schedule was being recalculated. Reload and try again."));
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<Result<PlanActivityDto>> MoveAsync(Guid planId, Guid activityId, MovePlanActivityDto dto, string userId, CancellationToken cancellationToken = default)
    {
        var activity = await _context.Set<PlanActivity>().FirstOrDefaultAsync(item => item.Id == activityId && !item.IsDeleted, cancellationToken);
        if (activity is null) return Result<PlanActivityDto>.Failure(Error.NotFound("Activity not found"));
        var editError = await GetEditErrorAsync(activity, planId, cancellationToken);
        if (editError is not null) return Result<PlanActivityDto>.Failure(editError);
        if (dto.TargetActivityId == activityId) return Result<PlanActivityDto>.Failure(Error.Validation("An activity cannot be moved relative to itself"));

        var nodes = await _context.Set<PlanWbsNode>().Where(node => node.PlanVersionId == activity.PlanVersionId && !node.IsDeleted).ToListAsync(cancellationToken);
        var activities = await _context.Set<PlanActivity>().Where(item => item.PlanVersionId == activity.PlanVersionId && !item.IsDeleted).ToListAsync(cancellationToken);
        var sourceNode = ResolveActivityWbsNode(activity, nodes, out var sourceNodeError);
        if (sourceNodeError is not null) return Result<PlanActivityDto>.Failure(Error.Validation(sourceNodeError));

        var targetActivity = dto.TargetActivityId.HasValue ? activities.FirstOrDefault(item => item.Id == dto.TargetActivityId.Value) : null;
        if (dto.TargetActivityId.HasValue && targetActivity is null) return Result<PlanActivityDto>.Failure(Error.NotFound("Drop target activity not found"));
        string? targetNodeError = null;
        var targetActivityNode = targetActivity is null ? null : ResolveActivityWbsNode(targetActivity, nodes, out targetNodeError);
        if (targetNodeError is not null) return Result<PlanActivityDto>.Failure(Error.Validation(targetNodeError));

        var explicitTargetNode = dto.TargetWbsId.HasValue ? nodes.FirstOrDefault(node => node.Id == dto.TargetWbsId.Value) : null;
        if (dto.TargetWbsId.HasValue && explicitTargetNode is null)
            return Result<PlanActivityDto>.Failure(Error.Validation("The target WBS node must belong to the active plan version."));
        var isBeforeOrAfter = dto.Position is PlanActivityMovePosition.Before or PlanActivityMovePosition.After;
        if (!isBeforeOrAfter && dto.Position != PlanActivityMovePosition.Child)
            return Result<PlanActivityDto>.Failure(Error.Validation("Choose a valid activity move position."));

        var sourceIsNodeRow = sourceNode is not null && GetPrimaryActivityId(sourceNode, activities) == activity.Id;
        var sourceEntryId = sourceIsNodeRow ? sourceNode!.Id : activity.Id;
        var oldParentWbsId = sourceIsNodeRow ? sourceNode!.ParentWbsId : activity.WbsId;
        Guid? destinationParentWbsId;
        Guid targetEntryId;
        PlanWbsNode? targetVersionNode;

        if (dto.Position == PlanActivityMovePosition.Child)
        {
            destinationParentWbsId = explicitTargetNode?.Id ?? targetActivityNode?.Id;
            if (!destinationParentWbsId.HasValue)
                return Result<PlanActivityDto>.Failure(Error.Validation("Choose a valid WBS node as the parent."));
            targetEntryId = destinationParentWbsId.Value;
            targetVersionNode = nodes.First(node => node.Id == destinationParentWbsId.Value);
        }
        else if (explicitTargetNode is not null)
        {
            if (targetActivityNode is not null && targetActivityNode.Id != explicitTargetNode.Id)
                return Result<PlanActivityDto>.Failure(Error.Validation("The activity and WBS drop targets do not match."));
            targetVersionNode = explicitTargetNode;
            if (targetActivity is not null && GetPrimaryActivityId(explicitTargetNode, activities) != targetActivity.Id)
            {
                targetEntryId = targetActivity.Id;
                destinationParentWbsId = explicitTargetNode.Id;
            }
            else
            {
                targetEntryId = explicitTargetNode.Id;
                destinationParentWbsId = explicitTargetNode.ParentWbsId;
            }
        }
        else if (targetActivity is not null)
        {
            if (targetActivityNode is null)
            {
                targetEntryId = targetActivity.Id;
                destinationParentWbsId = null;
                targetVersionNode = null;
            }
            else
            {
                targetVersionNode = targetActivityNode;
                if (GetPrimaryActivityId(targetActivityNode, activities) == targetActivity.Id)
                {
                    targetEntryId = targetActivityNode.Id;
                    destinationParentWbsId = targetActivityNode.ParentWbsId;
                }
                else
                {
                    targetEntryId = targetActivity.Id;
                    destinationParentWbsId = targetActivityNode.Id;
                }
            }
        }
        else return Result<PlanActivityDto>.Failure(Error.Validation("Choose a target activity or WBS row."));

        if (destinationParentWbsId.HasValue && nodes.All(node => node.Id != destinationParentWbsId.Value))
            return Result<PlanActivityDto>.Failure(Error.Validation("The target parent must belong to the active plan version."));
        if (isBeforeOrAfter && targetEntryId == sourceEntryId)
            return Result<PlanActivityDto>.Failure(Error.Validation("An activity cannot be moved relative to itself."));
        if (sourceIsNodeRow && destinationParentWbsId.HasValue && WouldCreateWbsCycle(sourceNode!.Id, destinationParentWbsId.Value, nodes))
            return Result<PlanActivityDto>.Failure(Error.Validation("An activity cannot be moved under itself or one of its descendants."));

        if (sourceIsNodeRow)
        {
            if (dto.WbsRowVersion is null || dto.WbsRowVersion.Length == 0 || !sourceNode!.RowVersion.SequenceEqual(dto.WbsRowVersion))
                return Result<PlanActivityDto>.Failure(Error.Conflict("The WBS structure changed. Reload and try again."));
        }
        else
        {
            if (dto.RowVersion is null || dto.RowVersion.Length == 0 || !activity.RowVersion.SequenceEqual(dto.RowVersion))
                return Result<PlanActivityDto>.Failure(Error.Conflict("The activity was modified by another user. Reload and try again."));
            if (sourceNode is not null && (dto.WbsRowVersion is null || dto.WbsRowVersion.Length == 0 || !sourceNode.RowVersion.SequenceEqual(dto.WbsRowVersion)))
                return Result<PlanActivityDto>.Failure(Error.Conflict("The WBS structure changed. Reload and try again."));
        }
        if (targetVersionNode is not null &&
            (dto.TargetWbsRowVersion is null || dto.TargetWbsRowVersion.Length == 0 || !targetVersionNode.RowVersion.SequenceEqual(dto.TargetWbsRowVersion)))
            return Result<PlanActivityDto>.Failure(Error.Conflict("The target WBS structure changed. Reload and try again."));

        var destinationSiblings = BuildSiblingOrderItems(destinationParentWbsId, nodes, activities).Where(item => item.Id != sourceEntryId).ToList();
        var insertIndex = destinationSiblings.Count;
        if (isBeforeOrAfter)
        {
            var targetIndex = destinationSiblings.FindIndex(item => item.Id == targetEntryId);
            if (targetIndex < 0)
                return Result<PlanActivityDto>.Failure(Error.Validation("The target is no longer in the current sibling structure. Refresh the plan and try again."));
            insertIndex = dto.Position == PlanActivityMovePosition.Before ? targetIndex : targetIndex + 1;
        }
        else if (dto.SortOrder.HasValue)
        {
            insertIndex = Math.Clamp(dto.SortOrder.Value, 0, destinationSiblings.Count);
        }

        var previousSummaryIds = await _summaryRollup.RecalculateAsync(activity.PlanVersionId, userId, cancellationToken: cancellationToken);
        var historyBeforeNodes = nodes.ToDictionary(node => node.Id, PlanGridCommandHistoryService.CaptureWbsFields);
        var historyBeforeActivities = activities.ToDictionary(item => item.Id, PlanGridCommandHistoryService.CaptureActivityFields);
        var originalValues = sourceIsNodeRow ? System.Text.Json.JsonSerializer.Serialize(sourceNode!.ToDto()) : System.Text.Json.JsonSerializer.Serialize(activity.ToDto());

        if (sourceIsNodeRow)
        {
            sourceNode!.ParentWbsId = destinationParentWbsId;
            sourceNode.UpdatedBy = userId;
        }
        else
        {
            activity.WbsId = destinationParentWbsId;
            activity.UpdatedBy = userId;
        }

        var sourceOrderItem = new MoveOrderItem(sourceEntryId, sourceIsNodeRow ? sourceNode!.SortOrder : activity.SortOrder,
            sourceIsNodeRow ? sourceNode!.Code : activity.Code, sourceIsNodeRow ? sourceNode : null, sourceIsNodeRow ? null : activity);
        destinationSiblings.Insert(Math.Clamp(insertIndex, 0, destinationSiblings.Count), sourceOrderItem);
        ReindexSiblingOrderItems(destinationSiblings, userId);
        if (oldParentWbsId != destinationParentWbsId)
        {
            var oldSiblings = BuildSiblingOrderItems(oldParentWbsId, nodes, activities).Where(item => item.Id != sourceEntryId).ToList();
            ReindexSiblingOrderItems(oldSiblings, userId);
        }

        if (targetVersionNode is not null)
        {
            targetVersionNode.UpdatedBy = userId;
            _context.Entry(targetVersionNode).Property(node => node.UpdatedBy).IsModified = true;
            _context.Entry(targetVersionNode).Property(node => node.RowVersion).OriginalValue = dto.TargetWbsRowVersion;
        }
        if (sourceNode is not null && !sourceIsNodeRow)
        {
            sourceNode.UpdatedBy = userId;
            _context.Entry(sourceNode).Property(node => node.UpdatedBy).IsModified = true;
            _context.Entry(sourceNode).Property(node => node.RowVersion).OriginalValue = dto.WbsRowVersion;
        }
        else if (sourceIsNodeRow)
        {
            _context.Entry(sourceNode!).Property(node => node.RowVersion).OriginalValue = dto.WbsRowVersion;
        }
        else
        {
            _context.Entry(activity).Property(item => item.RowVersion).OriginalValue = dto.RowVersion;
        }

        await _summaryRollup.RecalculateAsync(activity.PlanVersionId, userId, previousSummaryIds, cancellationToken);
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            await _auditService.LogAsync(new AuditEntry
            {
                EntityType = sourceIsNodeRow ? "PlanWbsNode" : "PlanActivity",
                EntityId = sourceIsNodeRow ? sourceNode!.Id : activity.Id,
                Action = "Move",
                UserId = TryParseGuid(userId),
                OriginalValues = originalValues,
                NewValues = System.Text.Json.JsonSerializer.Serialize(new { activity.Id, sourceEntryId, targetEntryId, oldParentWbsId, destinationParentWbsId, dto.Position }),
                Timestamp = DateTime.UtcNow,
                CreatedBy = userId
            }, cancellationToken);
            if (_commandHistory is not null)
            {
                using var collection = _commandHistory.BeginCollection("MoveActivity");
                foreach (var node in nodes)
                    await _commandHistory.RecordWbsUpdateAsync(planId, activity.PlanVersionId, userId, historyBeforeNodes[node.Id], node, cancellationToken);
                foreach (var item in activities)
                    await _commandHistory.RecordActivityUpdateAsync(planId, activity.PlanVersionId, userId, historyBeforeActivities[item.Id], item, cancellationToken);
                await _commandHistory.CommitCollectionAsync(planId, activity.PlanVersionId, userId, cancellationToken);
            }
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<PlanActivityDto>.Failure(Error.Conflict("The plan structure was modified concurrently. Reload and try again."));
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Result<PlanActivityDto>.Success(await BuildComputedDtoAsync(activity, cancellationToken));
    }

    private static Guid? GetPrimaryActivityId(PlanWbsNode node, IReadOnlyCollection<PlanActivity> activities)
    {
        if (node.ActivityId.HasValue && activities.Any(item => item.Id == node.ActivityId.Value))
            return node.ActivityId.Value;
        return activities.Where(item => item.WbsId == node.Id)
            .OrderBy(item => item.SortOrder).ThenBy(item => item.Code, StringComparer.Ordinal).ThenBy(item => item.Id)
            .Select(item => (Guid?)item.Id).FirstOrDefault();
    }

    private static PlanWbsNode? ResolveActivityWbsNode(PlanActivity activity, IReadOnlyCollection<PlanWbsNode> nodes, out string? error)
    {
        error = null;
        var nodeByWbsId = activity.WbsId.HasValue ? nodes.FirstOrDefault(node => node.Id == activity.WbsId.Value) : null;
        var reverseMatches = nodes.Where(node => node.ActivityId == activity.Id).Take(2).ToList();
        if (reverseMatches.Count > 1)
        {
            error = "This activity is linked from more than one WBS node. An administrator must repair the association before moving it.";
            return null;
        }
        if (activity.WbsId.HasValue && nodeByWbsId is null)
        {
            error = "This activity points to a WBS node that is missing, deleted, or outside the active plan version. Refresh the plan; an administrator must repair the association before moving it.";
            return null;
        }
        var reverseMatch = reverseMatches.SingleOrDefault();
        if (nodeByWbsId is not null && reverseMatch is not null && nodeByWbsId.Id != reverseMatch.Id)
        {
            error = "This activity's WBS links disagree. An administrator must repair the association before moving it.";
            return null;
        }
        return nodeByWbsId ?? reverseMatch;
    }

    private static List<MoveOrderItem> BuildSiblingOrderItems(Guid? parentWbsId, IReadOnlyCollection<PlanWbsNode> nodes, IReadOnlyCollection<PlanActivity> activities)
    {
        var parentNode = parentWbsId.HasValue ? nodes.FirstOrDefault(node => node.Id == parentWbsId.Value) : null;
        var primaryActivityId = parentNode is null ? null : GetPrimaryActivityId(parentNode, activities);
        var representedActivityIds = parentWbsId.HasValue
            ? primaryActivityId.HasValue ? new HashSet<Guid> { primaryActivityId.Value } : new HashSet<Guid>()
            : nodes.Where(node => node.ActivityId.HasValue).Select(node => node.ActivityId!.Value).ToHashSet();
        return nodes.Where(node => node.ParentWbsId == parentWbsId)
            .Select(node => new MoveOrderItem(node.Id, node.SortOrder, node.Code, node, null))
            .Concat(activities.Where(item => item.WbsId == parentWbsId && !representedActivityIds.Contains(item.Id))
                .Select(item => new MoveOrderItem(item.Id, item.SortOrder, item.Code, null, item)))
            .OrderBy(item => item.SortOrder).ThenBy(item => item.Code, StringComparer.Ordinal).ThenBy(item => item.Id)
            .ToList();
    }

    private static void ReindexSiblingOrderItems(IReadOnlyList<MoveOrderItem> items, string userId)
    {
        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            if (item.Node is not null && item.Node.SortOrder != index)
            {
                item.Node.SortOrder = index;
                item.Node.UpdatedBy = userId;
            }
            else if (item.Activity is not null && item.Activity.SortOrder != index)
            {
                item.Activity.SortOrder = index;
                item.Activity.UpdatedBy = userId;
            }
        }
    }

    private sealed record MoveOrderItem(Guid Id, int SortOrder, string Code, PlanWbsNode? Node, PlanActivity? Activity);
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

        var dependencies = await _context.Set<PlanDependency>()
            .Where(dependency => dependency.PlanVersionId == activity.PlanVersionId
                && (dependency.ActivityId == activity.Id || dependency.PredecessorActivityId == activity.Id))
            .ToListAsync(cancellationToken);
        var assignments = await _context.Set<PlanAssignment>()
            .Where(assignment => assignment.PlanVersionId == activity.PlanVersionId && assignment.ActivityId == activity.Id)
            .ToListAsync(cancellationToken);

        var deletedAt = DateTime.UtcNow;
        var previousSummaryIds = await _summaryRollup.RecalculateAsync(activity.PlanVersionId, userId, cancellationToken: cancellationToken);
        foreach (var dependency in dependencies)
        {
            dependency.IsDeleted = true;
            dependency.UpdatedBy = userId;
            dependency.UpdatedAt = deletedAt;
        }
        foreach (var assignment in assignments)
        {
            assignment.IsDeleted = true;
            assignment.UpdatedBy = userId;
            assignment.UpdatedAt = deletedAt;
        }
        activity.IsDeleted = true;
        activity.UpdatedBy = userId;
        activity.UpdatedAt = deletedAt;
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

        if (_commandHistory is not null)
            await _commandHistory.RecordBarrierAsync(planId, activity.PlanVersionId, userId, "DeleteActivity", cancellationToken);

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

    private async Task<DateTime?> GetParentActivityStartDateAsync(Guid wbsId, Guid planVersionId, CancellationToken cancellationToken)
    {
        var node = await _context.Set<PlanWbsNode>()
            .Where(item => item.Id == wbsId && item.PlanVersionId == planVersionId && !item.IsDeleted)
            .Select(item => new { item.ParentWbsId, item.ActivityId })
            .FirstOrDefaultAsync(cancellationToken);
        if (node?.ParentWbsId is not Guid parentWbsId) return null;

        if (node.ActivityId.HasValue)
        {
            var linkedActivity = await _context.Set<PlanActivity>()
                .Where(activity => activity.Id == node.ActivityId.Value && activity.PlanVersionId == planVersionId && !activity.IsDeleted)
                .Select(activity => new { activity.Id, activity.StartDate })
                .FirstOrDefaultAsync(cancellationToken);
            if (linkedActivity is not null) return linkedActivity.StartDate;
        }

        return await _context.Set<PlanActivity>()
            .Where(activity => activity.PlanVersionId == planVersionId && !activity.IsDeleted && activity.WbsId == parentWbsId)
            .OrderBy(activity => activity.SortOrder)
            .ThenBy(activity => activity.Code)
            .Select(activity => activity.StartDate)
            .FirstOrDefaultAsync(cancellationToken);
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
        if (_httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated == true
            && Guid.TryParse(tenantId, out var parsed)
            && parsed != Guid.Empty)
        {
            return parsed;
        }

        throw new InvalidOperationException("A trusted tenant context is required for project planning operations.");
    }

    private static Guid? TryParseGuid(string value) => GuidHelper.TryParse(value);
}
