using System.Data;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.ProjectPlanning.Application.DTOs;
using NEXUS.Modules.ProjectPlanning.Domain;
using NEXUS.Modules.ProjectPlanning.Domain.Scheduling;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Helpers;

namespace NEXUS.Modules.ProjectPlanning.Application;

/// <summary>
/// Keeps a short, actor-scoped undo/redo stack in the existing append-only audit
/// store. Only changed PlanActivity fields are kept; secrets and unrelated fields
/// never enter command history.
/// </summary>
public sealed class PlanGridCommandHistoryService : IPlanGridCommandHistoryService
{
    private const string HistoryEntityType = "PlanGridCommand";
    private const int SchemaVersion = 1;
    private const int MaximumRecentEvents = 1000;
    private static readonly TimeSpan Retention = TimeSpan.FromDays(30);

    private readonly DbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IPlanSchedulingService _schedulingService;
    private readonly SummaryTaskRollupService _summaryRollup;
    private CollectionState? _collection;

    public PlanGridCommandHistoryService(
        DbContext context,
        IHttpContextAccessor httpContextAccessor,
        IPlanSchedulingService schedulingService,
        SummaryTaskRollupService summaryRollup)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
        _schedulingService = schedulingService;
        _summaryRollup = summaryRollup;
    }

    public bool IsReplaying => false;

    public IDisposable BeginCollection(string operation)
    {
        if (_collection is not null)
            throw new InvalidOperationException("A planning command collection is already active.");

        _collection = new CollectionState(operation);
        return new CollectionScope(this);
    }

    public async Task RecordActivityUpdateAsync(
        Guid planId,
        Guid planVersionId,
        string userId,
        IReadOnlyDictionary<string, JsonElement> before,
        PlanActivity after,
        CancellationToken cancellationToken = default)
    {
        if (IsReplaying) return;

        var afterValues = CaptureActivityFields(after);
        var beforeChanged = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        var afterChanged = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var pair in afterValues)
        {
            if (!before.TryGetValue(pair.Key, out var previous) || JsonEquals(previous, pair.Value)) continue;
            beforeChanged[pair.Key] = previous.Clone();
            afterChanged[pair.Key] = pair.Value.Clone();
        }

        if (beforeChanged.Count == 0) return;

        var change = new ActivityChange
        {
            ActivityId = after.Id,
            BeforeValues = beforeChanged,
            AfterValues = afterChanged,
            AfterRowVersion = Convert.ToBase64String(after.RowVersion)
        };

        if (_collection is not null)
        {
            _collection.Changes.Add(change);
            return;
        }

        await WriteCommandAsync(planId, planVersionId, userId, "EditActivity", [change], cancellationToken);
    }

    public async Task RecordDependencyUpdateAsync(
        Guid planId,
        Guid planVersionId,
        string userId,
        Guid successorActivityId,
        IReadOnlyList<PlanGridDependencyEdgeSnapshot> before,
        IReadOnlyList<PlanGridDependencyEdgeSnapshot> after,
        CancellationToken cancellationToken = default)
    {
        if (IsReplaying) return;
        var beforeById = before.ToDictionary(edge => edge.Id);
        var afterById = after.ToDictionary(edge => edge.Id);
        var changedIds = beforeById.Keys.Union(afterById.Keys)
            .Where(id => !Equivalent(beforeById.GetValueOrDefault(id), afterById.GetValueOrDefault(id)))
            .ToList();
        if (changedIds.Count == 0) return;

        var change = new DependencyChange
        {
            SuccessorActivityId = successorActivityId,
            BeforeEdges = changedIds.Where(beforeById.ContainsKey).Select(id => beforeById[id]).ToList(),
            AfterEdges = changedIds.Where(afterById.ContainsKey).Select(id => afterById[id]).ToList()
        };
        if (_collection is not null)
        {
            _collection.DependencyChanges.Add(change);
            return;
        }
        await WriteCommandAsync(planId, planVersionId, userId, "EditDependencies", [], cancellationToken, [change]);
    }

    public async Task RecordWbsUpdateAsync(
        Guid planId,
        Guid planVersionId,
        string userId,
        IReadOnlyDictionary<string, JsonElement> before,
        PlanWbsNode after,
        CancellationToken cancellationToken = default)
    {
        if (IsReplaying) return;

        var afterValues = CaptureWbsFields(after);
        var beforeChanged = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        var afterChanged = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var pair in afterValues)
        {
            if (!before.TryGetValue(pair.Key, out var previous) || JsonEquals(previous, pair.Value)) continue;
            beforeChanged[pair.Key] = previous.Clone();
            afterChanged[pair.Key] = pair.Value.Clone();
        }
        if (beforeChanged.Count == 0) return;

        var change = new WbsChange
        {
            NodeId = after.Id,
            BeforeValues = beforeChanged,
            AfterValues = afterChanged,
            AfterRowVersion = Convert.ToBase64String(after.RowVersion)
        };
        if (_collection is not null)
        {
            _collection.WbsChanges.Add(change);
            return;
        }
        await WriteCommandAsync(planId, planVersionId, userId, "MoveWbs", [], cancellationToken, null, [change]);
    }

    public async Task CommitCollectionAsync(Guid planId, Guid planVersionId, string userId, CancellationToken cancellationToken = default)
    {
        if (_collection is null || (_collection.Changes.Count == 0 && _collection.DependencyChanges.Count == 0 && _collection.WbsChanges.Count == 0)) return;
        var collection = _collection;
        _collection = null;
        await WriteCommandAsync(planId, planVersionId, userId, collection.Operation, collection.Changes, cancellationToken, collection.DependencyChanges, collection.WbsChanges);
    }

    public async Task RecordBarrierAsync(Guid planId, Guid planVersionId, string userId, string operation, CancellationToken cancellationToken = default)
    {
        if (IsReplaying || !TryGetActor(userId, out var actorId)) return;
        var context = await GetWorkingContextAsync(null, planId, cancellationToken);
        if (context is null || context.Version.Id != planVersionId) return;
        var payload = new BarrierPayload { SchemaVersion = SchemaVersion, PlanVersionId = planVersionId, Operation = operation };
        _context.Set<AuditEntry>().Add(NewHistoryEntry(planId, actorId, userId, "Barrier", Serialize(payload)));
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<Result<PlanGridCommandHistoryStateDto>> GetStateAsync(
        Guid projectId,
        Guid planId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var context = await GetWorkingContextAsync(projectId, planId, cancellationToken);
        if (context is null) return Result<PlanGridCommandHistoryStateDto>.Failure(Error.NotFound("Working plan was not found"));
        if (!TryGetActor(userId, out var actorId)) return Result<PlanGridCommandHistoryStateDto>.Success(new PlanGridCommandHistoryStateDto());

        var events = await LoadEventsAsync(planId, actorId, cancellationToken);
        var stack = BuildStack(events, context.Version.Id);
        return Result<PlanGridCommandHistoryStateDto>.Success(ToDto(stack));
    }

    public Task<Result<PlanGridCommandHistoryActionDto>> UndoAsync(
        Guid projectId,
        Guid planId,
        Guid commandId,
        string userId,
        CancellationToken cancellationToken = default) =>
        ApplyHistoryActionAsync(projectId, planId, commandId, userId, isRedo: false, cancellationToken);

    public Task<Result<PlanGridCommandHistoryActionDto>> RedoAsync(
        Guid projectId,
        Guid planId,
        Guid commandId,
        string userId,
        CancellationToken cancellationToken = default) =>
        ApplyHistoryActionAsync(projectId, planId, commandId, userId, isRedo: true, cancellationToken);

    private async Task<Result<PlanGridCommandHistoryActionDto>> ApplyHistoryActionAsync(
        Guid projectId,
        Guid planId,
        Guid commandId,
        string userId,
        bool isRedo,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(userId, out var actorId))
            return Result<PlanGridCommandHistoryActionDto>.Failure(Error.NotFound("Command history was not found"));

        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var context = await GetWorkingContextAsync(projectId, planId, cancellationToken);
            if (context is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<PlanGridCommandHistoryActionDto>.Failure(Error.NotFound("Working plan was not found"));
            }

            var events = await LoadEventsAsync(planId, actorId, cancellationToken);
            var stack = BuildStack(events, context.Version.Id);
            var availableCommandId = isRedo ? stack.RedoCommandId : stack.UndoCommandId;
            if (availableCommandId != commandId)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<PlanGridCommandHistoryActionDto>.Failure(Error.Conflict("Command history changed or the command is no longer available."));
            }

            var commandEntry = events.FirstOrDefault(entry => entry.Id == commandId && entry.Action == "Commit");
            var command = commandEntry is null ? null : Deserialize<CommandPayload>(commandEntry.NewValues);
            if (command is null || command.SchemaVersion != SchemaVersion || command.PlanId != planId ||
                command.ProjectId != projectId || command.PlanVersionId != context.Version.Id || command.TenantId != GetCurrentTenantId())
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<PlanGridCommandHistoryActionDto>.Failure(Error.NotFound("Command history was not found"));
            }

            if (command.ActivityChanges.Count == 0 && command.DependencyChanges.Count == 0 && command.WbsChanges.Count == 0)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<PlanGridCommandHistoryActionDto>.Failure(Error.Validation("This command cannot be replayed."));
            }

            var expectedVersions = stack.ExpectedVersions.GetValueOrDefault(commandId) ?? command.AfterRowVersions;
            var activityIds = command.ActivityChanges.Select(change => change.ActivityId).Distinct().ToHashSet();
            var activities = await _context.Set<PlanActivity>()
                .Where(activity => activity.PlanVersionId == context.Version.Id && activityIds.Contains(activity.Id))
                .ToListAsync(cancellationToken);
            if (activities.Count != activityIds.Count)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<PlanGridCommandHistoryActionDto>.Failure(Error.Conflict("A task in this command no longer exists in the active Working version."));
            }

            foreach (var activity in activities)
            {
                if (!expectedVersions.TryGetValue($"activity:{activity.Id}", out var expectedBase64) ||
                    !TryDecodeVersion(expectedBase64, out var expected) ||
                    !activity.RowVersion.SequenceEqual(expected))
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Result<PlanGridCommandHistoryActionDto>.Failure(Error.Conflict("A task changed after this command. Reload the plan before undoing or redoing."));
                }
                _context.Entry(activity).Property(item => item.RowVersion).OriginalValue = expected;
            }

            var activityById = activities.ToDictionary(activity => activity.Id);
            var wbsIds = command.WbsChanges.Select(change => change.NodeId).Distinct().ToHashSet();
            var wbsNodes = wbsIds.Count == 0
                ? new List<PlanWbsNode>()
                : await _context.Set<PlanWbsNode>()
                    .Where(node => node.PlanVersionId == context.Version.Id && wbsIds.Contains(node.Id))
                    .ToListAsync(cancellationToken);
            if (wbsNodes.Count != wbsIds.Count)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<PlanGridCommandHistoryActionDto>.Failure(Error.Conflict("A WBS node in this command no longer exists in the active Working version."));
            }
            foreach (var node in wbsNodes)
            {
                if (!expectedVersions.TryGetValue($"wbs:{node.Id}", out var expectedBase64) ||
                    !TryDecodeVersion(expectedBase64, out var expected) ||
                    !node.RowVersion.SequenceEqual(expected))
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Result<PlanGridCommandHistoryActionDto>.Failure(Error.Conflict("The WBS structure changed after this command. Reload before undoing or redoing."));
                }
                _context.Entry(node).Property(item => item.RowVersion).OriginalValue = expected;
            }

            var previousSummaryIds = await _summaryRollup.RecalculateAsync(context.Version.Id, userId, cancellationToken: cancellationToken);
            var scheduleSeeds = new List<SchedulingSeed>();
            var auditEntries = new List<AuditEntry>();
            foreach (var change in command.ActivityChanges)
            {
                var activity = activityById[change.ActivityId];
                var currentValues = CaptureActivityFields(activity);
                var targetValues = isRedo ? change.AfterValues : change.BeforeValues;
                foreach (var pair in targetValues) ApplyActivityField(activity, pair.Key, pair.Value);
                activity.UpdatedBy = userId;
                // RecalculateSchedule itself snapshots authoritative schedule
                // before/after values. On undo/redo replay those exact persisted
                // values instead of immediately running the scheduler again and
                // erasing the requested historical state.
                if (!string.Equals(command.Operation, "RecalculateSchedule", StringComparison.Ordinal) &&
                    IsSchedulingChange(targetValues.Keys) && activity.SchedulingMode == PlanSchedulingMode.Automatic)
                    scheduleSeeds.Add(new SchedulingSeed(activity.Id, SchedulingAnchor.Start));

                var updatedValues = CaptureActivityFields(activity);
                auditEntries.Add(new AuditEntry
                {
                    TenantId = command.TenantId,
                    EntityType = "PlanActivity",
                    EntityId = activity.Id,
                    Action = isRedo ? "Redo" : "Undo",
                    UserId = actorId,
                    OriginalValues = SerializeSelectedFields(currentValues, targetValues.Keys),
                    NewValues = SerializeSelectedFields(updatedValues, targetValues.Keys),
                    Timestamp = DateTime.UtcNow,
                    CreatedBy = userId
                });
            }

            var wbsById = wbsNodes.ToDictionary(node => node.Id);
            foreach (var change in command.WbsChanges)
            {
                var node = wbsById[change.NodeId];
                var currentValues = CaptureWbsFields(node);
                var targetValues = isRedo ? change.AfterValues : change.BeforeValues;
                foreach (var pair in targetValues) ApplyWbsField(node, pair.Key, pair.Value);
                node.UpdatedBy = userId;
                var updatedValues = CaptureWbsFields(node);
                auditEntries.Add(new AuditEntry
                {
                    TenantId = command.TenantId,
                    EntityType = "PlanWbsNode",
                    EntityId = node.Id,
                    Action = isRedo ? "Redo" : "Undo",
                    UserId = actorId,
                    OriginalValues = SerializeSelectedFields(currentValues, targetValues.Keys),
                    NewValues = SerializeSelectedFields(updatedValues, targetValues.Keys),
                    Timestamp = DateTime.UtcNow,
                    CreatedBy = userId
                });
            }

            var dependencyIds = command.DependencyChanges
                .SelectMany(change => change.BeforeEdges.Concat(change.AfterEdges))
                .Select(edge => edge.Id)
                .Distinct()
                .ToHashSet();
            List<PlanDependency> dependencies = dependencyIds.Count == 0
                ? new List<PlanDependency>()
                : await _context.Set<PlanDependency>().IgnoreQueryFilters()
                    .Where(dependency => dependency.TenantId == command.TenantId &&
                                         dependency.PlanVersionId == context.Version.Id && dependencyIds.Contains(dependency.Id))
                    .ToListAsync(cancellationToken);
            if (dependencies.Count != dependencyIds.Count)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<PlanGridCommandHistoryActionDto>.Failure(Error.Conflict("A dependency in this command no longer exists in the active Working version."));
            }

            var dependencyById = dependencies.ToDictionary(dependency => dependency.Id);
            var dependencyAuditBefore = new Dictionary<Guid, List<PlanGridDependencyEdgeSnapshot>>();
            var dependencyAuditAfter = new Dictionary<Guid, List<PlanGridDependencyEdgeSnapshot>>();
            foreach (var change in command.DependencyChanges)
            {
                var touchedEdges = change.BeforeEdges.Concat(change.AfterEdges).DistinctBy(edge => edge.Id).ToList();
                var targetById = (isRedo ? change.AfterEdges : change.BeforeEdges).ToDictionary(edge => edge.Id);
                var currentSnapshot = new List<PlanGridDependencyEdgeSnapshot>(touchedEdges.Count);
                foreach (var edge in touchedEdges)
                {
                    var dependency = dependencyById[edge.Id];
                    if (!expectedVersions.TryGetValue($"dependency:{dependency.Id}", out var expectedBase64) ||
                        !TryDecodeVersion(expectedBase64, out var expected) ||
                        !dependency.RowVersion.SequenceEqual(expected))
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return Result<PlanGridCommandHistoryActionDto>.Failure(Error.Conflict("A dependency changed after this command. Reload the plan before undoing or redoing."));
                    }
                    _context.Entry(dependency).Property(item => item.RowVersion).OriginalValue = expected;
                    currentSnapshot.Add(ToSnapshot(dependency));
                    if (targetById.TryGetValue(edge.Id, out var target))
                    {
                        dependency.ActivityId = target.ActivityId;
                        dependency.PredecessorActivityId = target.PredecessorActivityId;
                        dependency.Type = target.Type;
                        dependency.Lag = target.Lag;
                        dependency.IsDeleted = target.IsDeleted;
                    }
                    else
                    {
                        dependency.IsDeleted = true;
                    }
                    dependency.UpdatedBy = userId;
                }
                dependencyAuditBefore[change.SuccessorActivityId] = currentSnapshot;
                dependencyAuditAfter[change.SuccessorActivityId] = touchedEdges.Select(edge => ToSnapshot(dependencyById[edge.Id])).ToList();
                scheduleSeeds.Add(new SchedulingSeed(change.SuccessorActivityId));
            }
            foreach (var (successorActivityId, beforeEdges) in dependencyAuditBefore)
            {
                auditEntries.Add(new AuditEntry
                {
                    TenantId = command.TenantId,
                    EntityType = "PlanDependency",
                    EntityId = successorActivityId,
                    Action = isRedo ? "Redo" : "Undo",
                    UserId = actorId,
                    OriginalValues = JsonSerializer.Serialize(beforeEdges),
                    NewValues = JsonSerializer.Serialize(dependencyAuditAfter[successorActivityId]),
                    Timestamp = DateTime.UtcNow,
                    CreatedBy = userId
                });
            }

            if (scheduleSeeds.Count > 0)
            {
                var schedule = await _schedulingService.RecalculateAsync(context.Version.Id, scheduleSeeds, userId, cancellationToken);
                if (schedule.IsFailure)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Result<PlanGridCommandHistoryActionDto>.Failure(schedule.Error!);
                }
            }
            await _summaryRollup.RecalculateAsync(context.Version.Id, userId, previousSummaryIds, cancellationToken);

            context.Version.UpdatedBy = userId;
            _context.Entry(context.Version).Property(version => version.RowVersion).OriginalValue = context.Version.RowVersion.ToArray();
            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<PlanGridCommandHistoryActionDto>.Failure(Error.Conflict("The plan or one of its tasks changed concurrently. Reload and try again."));
            }

            var transition = new TransitionPayload
            {
                SchemaVersion = SchemaVersion,
                CommandId = commandId,
                PlanVersionId = context.Version.Id,
                State = isRedo ? "Redo" : "Undo",
                ExpectedRowVersions = activities.ToDictionary(activity => $"activity:{activity.Id}", activity => Convert.ToBase64String(activity.RowVersion))
            };
            foreach (var dependency in dependencies)
                transition.ExpectedRowVersions[$"dependency:{dependency.Id}"] = Convert.ToBase64String(dependency.RowVersion);
            foreach (var node in wbsNodes)
                transition.ExpectedRowVersions[$"wbs:{node.Id}"] = Convert.ToBase64String(node.RowVersion);
            _context.Set<AuditEntry>().AddRange(auditEntries);
            _context.Set<AuditEntry>().Add(NewHistoryEntry(planId, actorId, userId, transition.State, Serialize(transition)));
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            var latest = await GetStateAsync(projectId, planId, userId, cancellationToken);
            return Result<PlanGridCommandHistoryActionDto>.Success(new PlanGridCommandHistoryActionDto
            {
                CommandId = commandId,
                State = transition.State,
                History = latest.Value ?? new PlanGridCommandHistoryStateDto()
            });
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<PlanGridCommandHistoryActionDto>.Failure(Error.Conflict("The plan changed concurrently. Reload and try again."));
        }
    }

    private async Task WriteCommandAsync(
        Guid planId,
        Guid planVersionId,
        string userId,
        string operation,
        IReadOnlyList<ActivityChange> changes,
        CancellationToken cancellationToken,
        IReadOnlyList<DependencyChange>? dependencyChanges = null,
        IReadOnlyList<WbsChange>? wbsChanges = null)
    {
        dependencyChanges ??= [];
        wbsChanges ??= [];
        if ((changes.Count == 0 && dependencyChanges.Count == 0 && wbsChanges.Count == 0) || !TryGetActor(userId, out var actorId)) return;
        var context = await GetWorkingContextAsync(null, planId, cancellationToken);
        if (context is null || context.Version.Id != planVersionId) return;

        var payload = new CommandPayload
        {
            SchemaVersion = SchemaVersion,
            Operation = operation,
            TenantId = GetCurrentTenantId(),
            ProjectId = context.Plan.ProjectId,
            PlanId = planId,
            PlanVersionId = planVersionId,
            ActorId = actorId,
            ActivityChanges = changes.ToList(),
            DependencyChanges = dependencyChanges.ToList(),
            AfterRowVersions = changes.ToDictionary(change => $"activity:{change.ActivityId}", change => change.AfterRowVersion)
        };
        foreach (var edge in dependencyChanges.SelectMany(change => change.AfterEdges))
            payload.AfterRowVersions[$"dependency:{edge.Id}"] = Convert.ToBase64String(edge.RowVersion);
        payload.WbsChanges = wbsChanges.ToList();
        foreach (var change in wbsChanges)
            payload.AfterRowVersions[$"wbs:{change.NodeId}"] = change.AfterRowVersion;
        _context.Set<AuditEntry>().Add(NewHistoryEntry(planId, actorId, userId, "Commit", Serialize(payload)));
        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task<PlanContext?> GetWorkingContextAsync(Guid? projectId, Guid planId, CancellationToken cancellationToken)
    {
        var plan = await _context.Set<Plan>().FirstOrDefaultAsync(item => item.Id == planId, cancellationToken);
        if (plan is null || (projectId.HasValue && plan.ProjectId != projectId.Value)) return null;
        var version = await _context.Set<PlanVersion>().FirstOrDefaultAsync(item =>
            item.PlanId == planId && item.Type == PlanVersionType.Working && item.Status == PlanVersionStatus.Active,
            cancellationToken);
        return version is null ? null : new PlanContext(plan, version);
    }

    private async Task<List<AuditEntry>> LoadEventsAsync(Guid planId, Guid actorId, CancellationToken cancellationToken)
    {
        var cutoff = DateTime.UtcNow - Retention;
        return await _context.Set<AuditEntry>()
            .Where(entry => entry.EntityType == HistoryEntityType && entry.EntityId == planId &&
                            entry.UserId == actorId && entry.Timestamp >= cutoff)
            .OrderByDescending(entry => entry.Timestamp)
            .ThenByDescending(entry => entry.Id)
            .Take(MaximumRecentEvents)
            .ToListAsync(cancellationToken);
    }

    private static CommandStack BuildStack(IReadOnlyCollection<AuditEntry> newestFirst, Guid planVersionId)
    {
        var commits = new Dictionary<Guid, (AuditEntry Entry, CommandPayload Payload)>();
        var undo = new List<Guid>();
        var redo = new List<Guid>();
        var expected = new Dictionary<Guid, Dictionary<string, string>>();
        var ordered = newestFirst
            .OrderBy(entry => entry.Timestamp)
            .ThenBy(entry => entry.CreatedAt)
            .ThenBy(entry => entry.Id)
            .ToList();

        foreach (var entry in ordered)
        {
            if (entry.Action == "Commit")
            {
                var payload = Deserialize<CommandPayload>(entry.NewValues);
                if (payload is null || payload.SchemaVersion != SchemaVersion || payload.PlanVersionId != planVersionId ||
                    payload.ActivityChanges.Count == 0 && payload.DependencyChanges.Count == 0 && payload.WbsChanges.Count == 0) continue;
                commits[entry.Id] = (entry, payload);
                expected[entry.Id] = payload.AfterRowVersions;
                undo.Add(entry.Id);
                redo.Clear();
                continue;
            }

            if (entry.Action == "Barrier")
            {
                var barrier = Deserialize<BarrierPayload>(entry.NewValues);
                if (barrier?.SchemaVersion == SchemaVersion && barrier.PlanVersionId == planVersionId)
                {
                    undo.Clear();
                    redo.Clear();
                }
                continue;
            }

            if (entry.Action is not ("Undo" or "Redo")) continue;
            var transition = Deserialize<TransitionPayload>(entry.NewValues);
            if (transition is null || transition.SchemaVersion != SchemaVersion || transition.PlanVersionId != planVersionId ||
                !commits.ContainsKey(transition.CommandId)) continue;

            expected[transition.CommandId] = transition.ExpectedRowVersions;
            if (transition.State == "Undo")
            {
                if (undo.Count > 0 && undo[^1] == transition.CommandId) undo.RemoveAt(undo.Count - 1);
                redo.Add(transition.CommandId);
            }
            else if (transition.State == "Redo")
            {
                if (redo.Count > 0 && redo[^1] == transition.CommandId) redo.RemoveAt(redo.Count - 1);
                undo.Add(transition.CommandId);
            }
        }

        return new CommandStack(
            undo.Count == 0 ? null : undo[^1],
            redo.Count == 0 ? null : redo[^1],
            undo.Where(commits.ContainsKey).Select(id => new CommandReference(id, commits[id].Payload.Operation, commits[id].Entry.Timestamp)).ToList(),
            redo.Where(commits.ContainsKey).Select(id => new CommandReference(id, commits[id].Payload.Operation, commits[id].Entry.Timestamp)).ToList(),
            expected,
            commits);
    }

    private static PlanGridCommandHistoryStateDto ToDto(CommandStack stack)
    {
        var undo = stack.Undo.LastOrDefault();
        var redo = stack.Redo.LastOrDefault();
        return new PlanGridCommandHistoryStateDto
        {
            CanUndo = undo is not null,
            UndoCommandId = undo?.Id,
            UndoOperation = undo?.Operation,
            UndoCommittedAt = undo?.CommittedAt,
            CanRedo = redo is not null,
            RedoCommandId = redo?.Id,
            RedoOperation = redo?.Operation,
            RedoCommittedAt = redo?.CommittedAt
        };
    }

    private AuditEntry NewHistoryEntry(Guid planId, Guid actorId, string userId, string action, string payload) => new()
    {
        TenantId = GetCurrentTenantId(),
        EntityType = HistoryEntityType,
        EntityId = planId,
        Action = action,
        UserId = actorId,
        NewValues = payload,
        Timestamp = DateTime.UtcNow,
        CreatedBy = userId
    };

    public static IReadOnlyDictionary<string, JsonElement> CaptureActivityFields(PlanActivity activity)
    {
        var fields = JsonSerializer.SerializeToElement(new
        {
            activity.Code,
            activity.Name,
            activity.Description,
            activity.Type,
            activity.SchedulingMode,
            activity.ConstraintType,
            activity.ConstraintDate,
            activity.Deadline,
            activity.StartDate,
            EndDate = activity.EndDate,
            activity.ActualStart,
            activity.ActualFinish,
            activity.CalendarId,
            activity.Duration,
            activity.SortOrder,
            activity.EstimateHours,
            activity.Progress,
            activity.WbsId,
            TextFormat = activity.TextFormat?.Clone()
        });
        return fields.EnumerateObject().ToDictionary(property => property.Name, property => property.Value.Clone(), StringComparer.Ordinal);
    }

    public static IReadOnlyDictionary<string, JsonElement> CaptureWbsFields(PlanWbsNode node)
    {
        var fields = JsonSerializer.SerializeToElement(new
        {
            node.Code,
            node.Name,
            node.ParentWbsId,
            node.SortOrder,
            TextFormat = node.TextFormat?.Clone()
        });
        return fields.EnumerateObject().ToDictionary(property => property.Name, property => property.Value.Clone(), StringComparer.Ordinal);
    }

    private static void ApplyActivityField(PlanActivity activity, string name, JsonElement value)
    {
        switch (name)
        {
            case nameof(PlanActivity.Code): activity.Code = Read<string>(value); break;
            case nameof(PlanActivity.Name): activity.Name = Read<string>(value); break;
            case nameof(PlanActivity.Description): activity.Description = Read<string?>(value); break;
            case nameof(PlanActivity.Type): activity.Type = Read<PlanActivityType>(value); break;
            case nameof(PlanActivity.SchedulingMode): activity.SchedulingMode = Read<PlanSchedulingMode>(value); break;
            case nameof(PlanActivity.ConstraintType): activity.ConstraintType = Read<PlanConstraintType>(value); break;
            case nameof(PlanActivity.ConstraintDate): activity.ConstraintDate = Read<DateTime?>(value); break;
            case nameof(PlanActivity.Deadline): activity.Deadline = Read<DateTime?>(value); break;
            case nameof(PlanActivity.StartDate): activity.StartDate = Read<DateTime?>(value); break;
            case nameof(PlanActivity.EndDate): activity.EndDate = Read<DateTime?>(value); break;
            case nameof(PlanActivity.ActualStart): activity.ActualStart = Read<DateTime?>(value); break;
            case nameof(PlanActivity.ActualFinish): activity.ActualFinish = Read<DateTime?>(value); break;
            case nameof(PlanActivity.CalendarId): activity.CalendarId = Read<Guid?>(value); break;
            case nameof(PlanActivity.Duration): activity.Duration = Read<int>(value); break;
            case nameof(PlanActivity.SortOrder): activity.SortOrder = Read<int>(value); break;
            case nameof(PlanActivity.EstimateHours): activity.EstimateHours = Read<decimal?>(value); break;
            case nameof(PlanActivity.Progress): activity.Progress = Read<decimal?>(value); break;
            case nameof(PlanActivity.WbsId): activity.WbsId = Read<Guid?>(value); break;
            case "TextFormat": activity.TextFormat = Read<PlanTextFormat?>(value)?.Clone() ?? PlanTextFormat.Unspecified(); break;
        }
    }

    private static void ApplyWbsField(PlanWbsNode node, string name, JsonElement value)
    {
        switch (name)
        {
            case nameof(PlanWbsNode.Code): node.Code = Read<string>(value); break;
            case nameof(PlanWbsNode.Name): node.Name = Read<string>(value); break;
            case nameof(PlanWbsNode.ParentWbsId): node.ParentWbsId = Read<Guid?>(value); break;
            case nameof(PlanWbsNode.SortOrder): node.SortOrder = Read<int>(value); break;
            case "TextFormat": node.TextFormat = Read<PlanTextFormat?>(value)?.Clone() ?? PlanTextFormat.Unspecified(); break;
        }
    }

    private static bool IsSchedulingChange(IEnumerable<string> fields) => fields.Any(field => field is
        nameof(PlanActivity.Type) or nameof(PlanActivity.SchedulingMode) or nameof(PlanActivity.ConstraintType) or
        nameof(PlanActivity.ConstraintDate) or nameof(PlanActivity.Deadline) or nameof(PlanActivity.StartDate) or
        nameof(PlanActivity.EndDate) or nameof(PlanActivity.Duration) or nameof(PlanActivity.CalendarId));

    private static bool Equivalent(PlanGridDependencyEdgeSnapshot? left, PlanGridDependencyEdgeSnapshot? right) =>
        left is null ? right is null : right is not null && left.ActivityId == right.ActivityId &&
        left.PredecessorActivityId == right.PredecessorActivityId && left.Type == right.Type &&
        left.Lag == right.Lag && left.IsDeleted == right.IsDeleted;

    public static IReadOnlyList<PlanGridDependencyEdgeSnapshot> CaptureDependencyEdges(IEnumerable<PlanDependency> dependencies) =>
        dependencies.Select(ToSnapshot).ToList();

    private static PlanGridDependencyEdgeSnapshot ToSnapshot(PlanDependency dependency) => new(
        dependency.Id, dependency.ActivityId, dependency.PredecessorActivityId, dependency.Type,
        dependency.Lag, dependency.IsDeleted, dependency.RowVersion.ToArray());

    private static string SerializeSelectedFields(IReadOnlyDictionary<string, JsonElement> values, IEnumerable<string> keys) =>
        JsonSerializer.Serialize(keys.Where(values.ContainsKey).ToDictionary(key => key, key => values[key]));

    private static T Read<T>(JsonElement element) => element.Deserialize<T>()!;
    private static bool JsonEquals(JsonElement left, JsonElement right) => left.ValueKind == right.ValueKind && left.GetRawText() == right.GetRawText();
    private static string Serialize<T>(T value) => JsonSerializer.Serialize(value);
    private static T? Deserialize<T>(string? value) => string.IsNullOrWhiteSpace(value) ? default : JsonSerializer.Deserialize<T>(value);
    private static bool TryDecodeVersion(string? value, out byte[] version)
    {
        try { version = Convert.FromBase64String(value ?? string.Empty); return version.Length > 0; }
        catch (FormatException) { version = []; return false; }
    }

    private bool TryGetActor(string userId, out Guid actorId) => Guid.TryParse(userId, out actorId);

    private Guid GetCurrentTenantId()
    {
        var tenant = _httpContextAccessor.HttpContext?.User.FindFirst("tenantId")?.Value;
        if (_httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated == true
            && Guid.TryParse(tenant, out var tenantId)
            && tenantId != Guid.Empty)
        {
            return tenantId;
        }

        throw new InvalidOperationException("A trusted tenant context is required for project planning operations.");
    }

    private sealed class CollectionState(string operation)
    {
        public string Operation { get; } = operation;
        public List<ActivityChange> Changes { get; } = [];
        public List<DependencyChange> DependencyChanges { get; } = [];
        public List<WbsChange> WbsChanges { get; } = [];
    }

    private sealed class CollectionScope(PlanGridCommandHistoryService service) : IDisposable
    {
        public void Dispose() => service._collection = null;
    }

    private sealed record PlanContext(Plan Plan, PlanVersion Version);
    private sealed record CommandReference(Guid Id, string Operation, DateTime CommittedAt);
    private sealed record CommandStack(
        Guid? UndoCommandId,
        Guid? RedoCommandId,
        List<CommandReference> Undo,
        List<CommandReference> Redo,
        Dictionary<Guid, Dictionary<string, string>> ExpectedVersions,
        Dictionary<Guid, (AuditEntry Entry, CommandPayload Payload)> Commits);

    private sealed class ActivityChange
    {
        public Guid ActivityId { get; init; }
        public Dictionary<string, JsonElement> BeforeValues { get; init; } = new(StringComparer.Ordinal);
        public Dictionary<string, JsonElement> AfterValues { get; init; } = new(StringComparer.Ordinal);
        public string AfterRowVersion { get; init; } = string.Empty;
    }

    private sealed class DependencyChange
    {
        public Guid SuccessorActivityId { get; init; }
        public List<PlanGridDependencyEdgeSnapshot> BeforeEdges { get; init; } = [];
        public List<PlanGridDependencyEdgeSnapshot> AfterEdges { get; init; } = [];
    }

    private sealed class WbsChange
    {
        public Guid NodeId { get; init; }
        public Dictionary<string, JsonElement> BeforeValues { get; init; } = new(StringComparer.Ordinal);
        public Dictionary<string, JsonElement> AfterValues { get; init; } = new(StringComparer.Ordinal);
        public string AfterRowVersion { get; init; } = string.Empty;
    }

    private sealed class CommandPayload
    {
        public int SchemaVersion { get; init; }
        public string Operation { get; init; } = string.Empty;
        public Guid TenantId { get; init; }
        public Guid ProjectId { get; init; }
        public Guid PlanId { get; init; }
        public Guid PlanVersionId { get; init; }
        public Guid ActorId { get; init; }
        public List<ActivityChange> ActivityChanges { get; init; } = [];
        public List<DependencyChange> DependencyChanges { get; init; } = [];
        public List<WbsChange> WbsChanges { get; set; } = [];
        public Dictionary<string, string> AfterRowVersions { get; init; } = new(StringComparer.Ordinal);
    }

    private sealed class TransitionPayload
    {
        public int SchemaVersion { get; init; }
        public Guid CommandId { get; init; }
        public Guid PlanVersionId { get; init; }
        public string State { get; init; } = string.Empty;
        public Dictionary<string, string> ExpectedRowVersions { get; init; } = new(StringComparer.Ordinal);
    }

    private sealed class BarrierPayload
    {
        public int SchemaVersion { get; init; }
        public Guid PlanVersionId { get; init; }
        public string Operation { get; init; } = string.Empty;
    }
}
