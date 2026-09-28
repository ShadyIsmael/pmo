using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.ProjectManagement.Application.DTOs;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.Modules.ProjectPlanning.Domain;
using NEXUS.Modules.ResourceManagement.Domain;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Management;

namespace NEXUS.Modules.ProjectManagement.Application;

public sealed class TimerService : ITimerService
{
    private static readonly string[] ActiveStatuses = { TimerSession.Running, TimerSession.OnBreak, TimerSession.Paused, TimerSession.IdlePaused };
    private readonly DbContext _context;
    private readonly TimeEntryService _timeEntries;
    private readonly IManagementContextAccessor _managementContext;
    private readonly IAuditService _audit;
    private readonly IHttpContextAccessor _http;
    private readonly TimeTrackingOptions _options;

    public TimerService(DbContext context, TimeEntryService timeEntries, IManagementContextAccessor managementContext, IAuditService audit, IHttpContextAccessor http, IOptions<TimeTrackingOptions> options)
    {
        _context = context;
        _timeEntries = timeEntries;
        _managementContext = managementContext;
        _audit = audit;
        _http = http;
        _options = options.Value;
    }

    public async Task<Result<TimerSessionDto?>> GetCurrentAsync(Guid actorId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var session = await _context.Set<TimerSession>().AsNoTracking()
            .Include(item => item.Segments)
            .Where(item => item.TenantId == tenantId && item.UserId == actorId && !item.IsDeleted && (ActiveStatuses.Contains(item.Status) || item.Status == TimerSession.AwaitingFinalization))
            .OrderByDescending(item => item.StartedAt)
            .FirstOrDefaultAsync(cancellationToken);
        return Result<TimerSessionDto?>.Success(session is null ? null : await MapAsync(session, actorId, tenantId, cancellationToken));
    }

    public async Task<Result<TimerSessionDto>> GetByIdAsync(Guid id, Guid actorId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var session = await FindOwnedAsync(id, actorId, tenantId, cancellationToken);
        return session is null
            ? Result<TimerSessionDto>.Failure(Error.NotFound("Timer session not found"))
            : Result<TimerSessionDto>.Success(await MapAsync(session, actorId, tenantId, cancellationToken));
    }

    public async Task<Result<TimerSessionDto>> StartAsync(Guid actorId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var guard = EnsureWritable();
        if (guard is not null) return Result<TimerSessionDto>.Failure(guard);
        if (!HasPermission(TimeTrackingPermissions.ManageOwn))
            return Result<TimerSessionDto>.Failure(Error.Forbidden("You are not allowed to control a live timer"));

        var existing = await _context.Set<TimerSession>().FirstOrDefaultAsync(item => item.TenantId == tenantId && item.UserId == actorId && !item.IsDeleted && (ActiveStatuses.Contains(item.Status) || item.Status == TimerSession.AwaitingFinalization), cancellationToken);
        if (existing is not null) return Result<TimerSessionDto>.Failure(Error.Conflict("Finish or cancel the existing timer before starting another one"));

        var resource = await _context.Set<Resource>().FirstOrDefaultAsync(item => item.TenantId == tenantId && item.UserId == actorId && item.IsActive && !item.IsDeleted, cancellationToken);
        if (resource is null) return Result<TimerSessionDto>.Failure(Error.NotFound("No active Resource is linked to the authenticated user"));
        var now = DateTimeOffset.UtcNow;
        var session = new TimerSession { TenantId = tenantId, ResourceId = resource.Id, UserId = actorId, StartedAt = now, LastHeartbeatAt = now, LastActivityAt = now, Status = TimerSession.Running, CreatedBy = actorId.ToString() };
        session.Segments.Add(NewSegment(session, TimerSegment.Work, now, actorId));
        _context.Set<TimerSession>().Add(session);
        _context.Set<TimerSegment>().AddRange(session.Segments);
        await AddAuditAsync(session, "Started", actorId, new { session.ResourceId, session.StartedAt }, cancellationToken);
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Result<TimerSessionDto>.Failure(Error.Conflict("Another active timer already exists for this Resource or user"));
        }
        return Result<TimerSessionDto>.Success(await MapAsync(session, actorId, tenantId, cancellationToken));
    }

    public Task<Result<TimerSessionDto>> BreakAsync(Guid id, Guid actorId, Guid tenantId, CancellationToken cancellationToken = default)
        => TransitionAsync(id, TimerSession.OnBreak, TimerSegment.Break, actorId, tenantId, cancellationToken);

    public Task<Result<TimerSessionDto>> PauseAsync(Guid id, Guid actorId, Guid tenantId, CancellationToken cancellationToken = default)
        => TransitionAsync(id, TimerSession.Paused, TimerSegment.Pause, actorId, tenantId, cancellationToken);

    public Task<Result<TimerSessionDto>> ResumeAsync(Guid id, Guid actorId, Guid tenantId, CancellationToken cancellationToken = default)
        => TransitionAsync(id, TimerSession.Running, TimerSegment.Work, actorId, tenantId, cancellationToken);

    private async Task<Result<TimerSessionDto>> TransitionAsync(Guid id, string nextStatus, string nextSegmentType, Guid actorId, Guid tenantId, CancellationToken cancellationToken)
    {
        var guard = EnsureWritable();
        if (guard is not null) return Result<TimerSessionDto>.Failure(guard);

        for (var attempt = 0; attempt < 2; attempt++)
        {
            var session = await FindOwnedAsync(id, actorId, tenantId, cancellationToken);
            if (session is null) return Result<TimerSessionDto>.Failure(Error.NotFound("Timer session not found"));
            if (session.Status == nextStatus) return Result<TimerSessionDto>.Success(await MapAsync(session, actorId, tenantId, cancellationToken));
            if (!ActiveStatuses.Contains(session.Status)) return Result<TimerSessionDto>.Failure(Error.Conflict("The timer is no longer active"));

            var now = DateTimeOffset.UtcNow;
            CloseOpenSegment(session, now);
            AttachNewSegment(session, nextSegmentType, now, actorId);
            session.Status = nextStatus;
            session.LastHeartbeatAt = now;
            session.LastActivityAt = now;
            session.UpdatedBy = actorId.ToString();
            await AddAuditAsync(session, nextStatus, actorId, new { Status = nextStatus }, cancellationToken);
            try
            {
                await _context.SaveChangesAsync(cancellationToken);
                return Result<TimerSessionDto>.Success(await MapAsync(session, actorId, tenantId, cancellationToken));
            }
            catch (DbUpdateConcurrencyException) when (attempt == 0)
            {
                _context.ChangeTracker.Clear();
                continue;
            }
            catch (DbUpdateConcurrencyException)
            {
                return Result<TimerSessionDto>.Failure(Error.Conflict("The timer changed in another tab; reload and try again"));
            }
        }

        return Result<TimerSessionDto>.Failure(Error.Conflict("The timer changed in another tab; reload and try again"));
    }

    public async Task<Result<TimerSessionDto>> HeartbeatAsync(Guid id, TimerHeartbeatDto dto, Guid actorId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var guard = EnsureWritable();
        if (guard is not null) return Result<TimerSessionDto>.Failure(guard);
        var session = await FindOwnedAsync(id, actorId, tenantId, cancellationToken);
        if (session is null) return Result<TimerSessionDto>.Failure(Error.NotFound("Timer session not found"));
        if (!ActiveStatuses.Contains(session.Status)) return Result<TimerSessionDto>.Success(await MapAsync(session, actorId, tenantId, cancellationToken));

        var now = DateTimeOffset.UtcNow;
        session.LastHeartbeatAt = now;
        var activity = dto.LastActivityAt.HasValue && dto.LastActivityAt.Value <= now ? dto.LastActivityAt.Value : now;
        if (dto.IsIdle && session.Status == TimerSession.Running && now - activity >= TimeSpan.FromMinutes(Math.Max(1, _options.IdleMinutes)))
        {
            var idleStart = ClampToOpenSegmentStart(session, activity);
            CloseOpenSegment(session, idleStart);
            var idleSegment = AttachNewSegment(session, TimerSegment.Idle, idleStart, actorId);
            idleSegment.EndedAt = now;
            session.Status = TimerSession.IdlePaused;
            session.LastActivityAt = activity;
        }
        else if (!dto.IsIdle && session.Status == TimerSession.Running)
        {
            session.LastActivityAt = activity;
        }
        session.UpdatedBy = actorId.ToString();
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Heartbeat concurrency is benign; return current authoritative state
            return Result<TimerSessionDto>.Success(await MapAsync(session, actorId, tenantId, cancellationToken));
        }
        return Result<TimerSessionDto>.Success(await MapAsync(session, actorId, tenantId, cancellationToken));
    }

    public async Task<Result<TimerSessionDto>> FinishAsync(Guid id, Guid actorId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var guard = EnsureWritable();
        if (guard is not null) return Result<TimerSessionDto>.Failure(guard);

        for (var attempt = 0; attempt < 2; attempt++)
        {
            var session = await FindOwnedAsync(id, actorId, tenantId, cancellationToken);
            if (session is null) return Result<TimerSessionDto>.Failure(Error.NotFound("Timer session not found"));
            if (session.Status == TimerSession.AwaitingFinalization) return Result<TimerSessionDto>.Success(await MapAsync(session, actorId, tenantId, cancellationToken));
            if (!ActiveStatuses.Contains(session.Status)) return Result<TimerSessionDto>.Failure(Error.Conflict("The timer is no longer active"));
            var now = DateTimeOffset.UtcNow;
            CloseOpenSegment(session, now);
            session.EndedAt = now;
            session.Status = TimerSession.AwaitingFinalization;
            session.UpdatedBy = actorId.ToString();
            await AddAuditAsync(session, "FinishedAwaitingFinalization", actorId, new { session.EndedAt }, cancellationToken);
            try
            {
                await _context.SaveChangesAsync(cancellationToken);
                return Result<TimerSessionDto>.Success(await MapAsync(session, actorId, tenantId, cancellationToken));
            }
            catch (DbUpdateConcurrencyException) when (attempt == 0)
            {
                // Heartbeat may have updated RowVersion; reload and retry once
                continue;
            }
            catch (DbUpdateConcurrencyException)
            {
                return Result<TimerSessionDto>.Failure(Error.Conflict("The timer changed in another tab; reload and try again"));
            }
        }

        return Result<TimerSessionDto>.Failure(Error.Conflict("The timer changed in another tab; reload and try again"));
    }

    public async Task<Result<IReadOnlyList<TimeEntryDto>>> FinalizeAsync(Guid id, FinalizeTimerSessionDto dto, Guid actorId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var guard = EnsureWritable();
        if (guard is not null) return Result<IReadOnlyList<TimeEntryDto>>.Failure(guard);
        var session = await FindOwnedAsync(id, actorId, tenantId, cancellationToken, includeSegments: true);
        if (session is null) return Result<IReadOnlyList<TimeEntryDto>>.Failure(Error.NotFound("Timer session not found"));
        if (session.Status == TimerSession.Finished)
        {
            var existing = await _context.Set<TimeEntry>().AsNoTracking().Where(entry => entry.TimerSessionId == id && !entry.IsDeleted).ToListAsync(cancellationToken);
            return Result<IReadOnlyList<TimeEntryDto>>.Success(await MapEntriesAsync(existing, cancellationToken));
        }
        if (session.Status != TimerSession.AwaitingFinalization) return Result<IReadOnlyList<TimeEntryDto>>.Failure(Error.Conflict("Finish the timer before finalizing it"));
        if (dto.ProjectId == Guid.Empty) return Result<IReadOnlyList<TimeEntryDto>>.Failure(Error.Validation("Project is required to finalize a timer"));

        var resource = await _context.Set<Resource>().FirstOrDefaultAsync(item => item.Id == session.ResourceId && item.TenantId == tenantId && !item.IsDeleted, cancellationToken);
        if (resource is null) return Result<IReadOnlyList<TimeEntryDto>>.Failure(Error.NotFound("Resource not found"));
        var timezone = ResolveTimezone(resource.Timezone);
        var daily = BuildDailyTotals(session.Segments.OrderBy(item => item.StartedAt).ToList(), timezone);
        var workDays = daily.Where(item => item.ActualSeconds > 0).ToList();
        if (workDays.Count == 0) return Result<IReadOnlyList<TimeEntryDto>>.Failure(Error.Validation("The timer contains no actual work to finalize"));

        await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
        foreach (var day in workDays)
        {
            var reference = await _timeEntries.ValidateTimerReferencesAsync(session.ResourceId, dto.ProjectId, day.WorkDate, dto.PlanActivityId, dto.WorkItemId, dto.ProjectRoleId, actorId, tenantId, cancellationToken);
            if (reference.IsFailure) return Result<IReadOnlyList<TimeEntryDto>>.Failure(reference.Error!);
            var normalized = TimeEntryDuration.Normalize(day.StartTime, day.EndTime, day.ActualSeconds, day.BreakSeconds, day.PauseSeconds);
            if (normalized.IsFailure) return Result<IReadOnlyList<TimeEntryDto>>.Failure(normalized.Error!);
            if (await _timeEntries.HasTimerOverlapAsync(session.ResourceId, day.WorkDate, normalized.Value!, cancellationToken))
                return Result<IReadOnlyList<TimeEntryDto>>.Failure(Error.Conflict("The Resource already has an overlapping time entry for this date"));
        }

        var entries = new List<TimeEntry>();
        foreach (var day in workDays)
        {
            var normalized = TimeEntryDuration.Normalize(day.StartTime, day.EndTime, day.ActualSeconds, day.BreakSeconds, day.PauseSeconds).Value!;
            var entry = new TimeEntry
            {
                TenantId = tenantId, ResourceId = session.ResourceId, ProjectId = dto.ProjectId,
                PlanActivityId = dto.PlanActivityId, WorkItemId = dto.WorkItemId, ProjectRoleId = dto.ProjectRoleId,
                TimerSessionId = session.Id, WorkDate = day.WorkDate, StartTime = normalized.StartTime, EndTime = normalized.EndTime,
                ElapsedSeconds = normalized.ElapsedSeconds, ActualSeconds = normalized.ActualSeconds,
                BreakSeconds = normalized.BreakSeconds, PauseSeconds = normalized.PauseSeconds,
                Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim(),
                EntrySource = TimeEntry.SourceTimer, CreatedBy = actorId.ToString()
            };
            entries.Add(entry);
            _context.Set<TimeEntry>().Add(entry);
            await AddAuditAsync(entry, "CreatedByTimer", actorId, new { entry.TimerSessionId, entry.ActualSeconds, entry.WorkDate }, cancellationToken);
        }
        session.Status = TimerSession.Finished;
        session.UpdatedBy = actorId.ToString();
        await AddAuditAsync(session, "Finalized", actorId, new { session.Status, TimeEntryCount = entries.Count }, cancellationToken);
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<IReadOnlyList<TimeEntryDto>>.Failure(Error.Conflict("The timer changed in another tab; reload and try again"));
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<IReadOnlyList<TimeEntryDto>>.Failure(Error.Conflict("The timer could not be finalized because another time entry overlaps it"));
        }
        return Result<IReadOnlyList<TimeEntryDto>>.Success(await MapEntriesAsync(entries, cancellationToken));
    }

    public async Task<Result<TimerSessionDto>> CancelAsync(Guid id, Guid actorId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var guard = EnsureWritable();
        if (guard is not null) return Result<TimerSessionDto>.Failure(guard);
        var session = await FindOwnedAsync(id, actorId, tenantId, cancellationToken);
        if (session is null) return Result<TimerSessionDto>.Failure(Error.NotFound("Timer session not found"));
        if (session.Status == TimerSession.Cancelled) return Result<TimerSessionDto>.Success(await MapAsync(session, actorId, tenantId, cancellationToken));
        if (session.Status == TimerSession.Finished) return Result<TimerSessionDto>.Failure(Error.Conflict("A finalized timer cannot be cancelled"));
        if (ActiveStatuses.Contains(session.Status)) CloseOpenSegment(session, DateTimeOffset.UtcNow);
        session.Status = TimerSession.Cancelled;
        session.EndedAt ??= DateTimeOffset.UtcNow;
        session.UpdatedBy = actorId.ToString();
        await AddAuditAsync(session, "Cancelled", actorId, new { session.Status }, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return Result<TimerSessionDto>.Success(await MapAsync(session, actorId, tenantId, cancellationToken));
    }

    private async Task<TimerSession?> FindOwnedAsync(Guid id, Guid actorId, Guid tenantId, CancellationToken cancellationToken, bool includeSegments = true)
    {
        var query = _context.Set<TimerSession>().Where(item => item.Id == id && item.TenantId == tenantId && item.UserId == actorId && !item.IsDeleted);
        if (includeSegments) query = query.Include(item => item.Segments);
        return await query.FirstOrDefaultAsync(cancellationToken);
    }

    private static TimerSegment NewSegment(TimerSession session, string type, DateTimeOffset startedAt, Guid actorId)
        => new() { TenantId = session.TenantId, TimerSessionId = session.Id, SegmentType = type, StartedAt = startedAt, CreatedBy = actorId.ToString() };

    private TimerSegment AttachNewSegment(TimerSession session, string type, DateTimeOffset startedAt, Guid actorId)
    {
        var segment = NewSegment(session, type, startedAt, actorId);
        session.Segments.Add(segment);
        _context.Set<TimerSegment>().Add(segment);
        return segment;
    }

    private static void CloseOpenSegment(TimerSession session, DateTimeOffset endedAt)
    {
        var open = session.Segments.OrderByDescending(item => item.StartedAt).FirstOrDefault(item => item.EndedAt is null);
        if (open is not null) open.EndedAt = endedAt < open.StartedAt ? open.StartedAt : endedAt;
    }

    private static DateTimeOffset ClampToOpenSegmentStart(TimerSession session, DateTimeOffset candidate)
        => session.Segments.OrderByDescending(item => item.StartedAt).FirstOrDefault(item => item.EndedAt is null) is { } open && candidate < open.StartedAt ? open.StartedAt : candidate;

    private async Task<TimerSessionDto> MapAsync(TimerSession session, Guid actorId, Guid tenantId, CancellationToken cancellationToken)
    {
        var resource = await _context.Set<Resource>().AsNoTracking().FirstOrDefaultAsync(item => item.Id == session.ResourceId && item.TenantId == tenantId, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        return new TimerSessionDto
        {
            Id = session.Id, ResourceId = session.ResourceId, Status = session.Status, StartedAt = session.StartedAt, EndedAt = session.EndedAt,
            ServerNow = now, Timezone = resource?.Timezone ?? "UTC", Metrics = BuildMetrics(session.Segments, now),
            Segments = session.Segments.OrderBy(item => item.StartedAt).Select(item => new TimerSegmentDto { Id = item.Id, SegmentType = item.SegmentType, StartedAt = item.StartedAt, EndedAt = item.EndedAt, DurationSeconds = DurationSeconds(item.StartedAt, item.EndedAt ?? now) }).ToList()
        };
    }

    private static TimerMetricsDto BuildMetrics(IEnumerable<TimerSegment> segments, DateTimeOffset now)
    {
        var values = segments.Select(segment => new { segment.SegmentType, Seconds = DurationSeconds(segment.StartedAt, segment.EndedAt ?? now) }).ToList();
        var work = values.Where(item => item.SegmentType == TimerSegment.Work).Select(item => item.Seconds).ToList();
        return new TimerMetricsDto { WorkSeconds = values.Where(item => item.SegmentType == TimerSegment.Work).Sum(item => item.Seconds), BreakSeconds = values.Where(item => item.SegmentType == TimerSegment.Break).Sum(item => item.Seconds), PauseSeconds = values.Where(item => item.SegmentType == TimerSegment.Pause).Sum(item => item.Seconds), IdleSeconds = values.Where(item => item.SegmentType == TimerSegment.Idle).Sum(item => item.Seconds), BreakCount = values.Count(item => item.SegmentType == TimerSegment.Break), WorkSessionCount = work.Count, LongestWorkSeconds = work.Count == 0 ? 0 : work.Max(), AverageWorkSeconds = work.Count == 0 ? 0 : (int)Math.Round(work.Average()) };
    }

    private static int DurationSeconds(DateTimeOffset start, DateTimeOffset end) => Math.Max(0, (int)Math.Round((end - start).TotalSeconds));

    private sealed class DailyTotal
    {
        public DateTime WorkDate { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public int ActualSeconds { get; set; }
        public int BreakSeconds { get; set; }
        public int PauseSeconds { get; set; }
    }

    private static List<DailyTotal> BuildDailyTotals(IReadOnlyList<TimerSegment> segments, TimeZoneInfo timezone)
    {
        var totals = new Dictionary<DateTime, DailyTotal>();
        foreach (var segment in segments.Where(item => item.EndedAt.HasValue && item.EndedAt.Value > item.StartedAt))
        {
            var cursor = segment.StartedAt;
            var end = segment.EndedAt!.Value;
            while (cursor < end)
            {
                var local = TimeZoneInfo.ConvertTime(cursor, timezone);
                var date = local.Date;
                var nextLocalMidnight = DateTime.SpecifyKind(date.AddDays(1), DateTimeKind.Unspecified);
                var boundaryUtc = TimeZoneInfo.ConvertTimeToUtc(nextLocalMidnight, timezone);
                var boundary = new DateTimeOffset(boundaryUtc);
                var sliceEnd = boundary < end ? boundary : end;
                var seconds = DurationSeconds(cursor, sliceEnd);
                if (seconds > 0)
                {
                    var localEnd = TimeZoneInfo.ConvertTime(sliceEnd, timezone);
                    var total = totals.TryGetValue(date, out var current) ? current : totals[date] = new DailyTotal { WorkDate = date, StartTime = local.TimeOfDay, EndTime = local.TimeOfDay };
                    total.StartTime = total.StartTime < local.TimeOfDay ? total.StartTime : local.TimeOfDay;
                    var endTime = localEnd.TimeOfDay == TimeSpan.Zero ? TimeSpan.FromDays(1).Subtract(TimeSpan.FromSeconds(1)) : localEnd.TimeOfDay;
                    total.EndTime = total.EndTime > endTime ? total.EndTime : endTime;
                    if (segment.SegmentType == TimerSegment.Work) total.ActualSeconds += seconds;
                    else if (segment.SegmentType == TimerSegment.Break) total.BreakSeconds += seconds;
                    else total.PauseSeconds += seconds;
                }
                cursor = sliceEnd;
            }
        }
        foreach (var total in totals.Values)
        {
            var accounted = total.ActualSeconds + total.BreakSeconds + total.PauseSeconds;
            var envelope = Math.Max(1, (int)Math.Round((total.EndTime - total.StartTime).TotalSeconds));
            // SQL time values cannot represent 24:00. When a slice ends exactly
            // at local midnight, move the envelope start back by the one-second
            // representation gap rather than losing actual work seconds.
            if (accounted > envelope)
            {
                total.StartTime = total.StartTime.Subtract(TimeSpan.FromSeconds(accounted - envelope));
                envelope = Math.Max(1, (int)Math.Round((total.EndTime - total.StartTime).TotalSeconds));
            }
            total.PauseSeconds = Math.Max(0, envelope - total.ActualSeconds - total.BreakSeconds);
        }
        return totals.Values.OrderBy(item => item.WorkDate).ToList();
    }

    private async Task<List<TimeEntryDto>> MapEntriesAsync(IReadOnlyList<TimeEntry> entries, CancellationToken cancellationToken)
    {
        var resourceIds = entries.Select(item => item.ResourceId).Distinct().ToList();
        var projectIds = entries.Select(item => item.ProjectId).Distinct().ToList();
        var activities = entries.Where(item => item.PlanActivityId.HasValue).Select(item => item.PlanActivityId!.Value).Distinct().ToList();
        var resources = await _context.Set<Resource>().AsNoTracking().Where(item => resourceIds.Contains(item.Id)).ToDictionaryAsync(item => item.Id, cancellationToken);
        var projects = await _context.Set<Project>().AsNoTracking().Where(item => projectIds.Contains(item.Id)).ToDictionaryAsync(item => item.Id, cancellationToken);
        var activityNames = await _context.Set<PlanActivity>().AsNoTracking().Where(item => activities.Contains(item.Id)).ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken);
        return entries.Select(item => new TimeEntryDto { Id = item.Id, ResourceId = item.ResourceId, ResourceName = resources.GetValueOrDefault(item.ResourceId)?.Name ?? string.Empty, ProjectId = item.ProjectId, ProjectName = projects.GetValueOrDefault(item.ProjectId)?.Name ?? string.Empty, PlanActivityId = item.PlanActivityId, PlanActivityName = item.PlanActivityId.HasValue ? activityNames.GetValueOrDefault(item.PlanActivityId.Value) : null, WorkItemId = item.WorkItemId, ProjectRoleId = item.ProjectRoleId, TimerSessionId = item.TimerSessionId, WorkDate = item.WorkDate, StartTime = item.StartTime, EndTime = item.EndTime, ElapsedSeconds = item.ElapsedSeconds, ActualSeconds = item.ActualSeconds, BreakSeconds = item.BreakSeconds, PauseSeconds = item.PauseSeconds, Description = item.Description, EntrySource = item.EntrySource, CreatedBy = item.CreatedBy, CreatedAt = item.CreatedAt, UpdatedAt = item.UpdatedAt, RowVersion = item.RowVersion }).ToList();
    }

    private async Task AddAuditAsync(TimerSession session, string action, Guid actorId, object changes, CancellationToken cancellationToken)
        => await _audit.AppendAsync(new AuditEntry { TenantId = session.TenantId, EntityType = "TimerSession", EntityId = session.Id, Action = action, UserId = actorId, Timestamp = DateTime.UtcNow, CreatedBy = actorId.ToString(), Changes = JsonSerializer.Serialize(changes) }, cancellationToken);

    private async Task AddAuditAsync(TimeEntry entry, string action, Guid actorId, object changes, CancellationToken cancellationToken)
        => await _audit.AppendAsync(new AuditEntry { TenantId = entry.TenantId, EntityType = "TimeEntry", EntityId = entry.Id, Action = action, UserId = actorId, Timestamp = DateTime.UtcNow, CreatedBy = actorId.ToString(), Changes = JsonSerializer.Serialize(changes) }, cancellationToken);

    private Error? EnsureWritable()
    {
        if (_managementContext.Current?.EffectiveReadOnly == true)
            return Error.Forbidden("This action is not available while using read-only perspective mode.");
        return HasPermission(TimeTrackingPermissions.ManageOwn)
            ? null
            : Error.Forbidden("Only the authenticated Resource owner can control a live timer");
    }
    private bool HasPermission(string permission) => _http.HttpContext?.User.HasClaim("permission", permission) == true;
    private static TimeZoneInfo ResolveTimezone(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return TimeZoneInfo.Utc;
        try { return TimeZoneInfo.FindSystemTimeZoneById(id); } catch { return TimeZoneInfo.Utc; }
    }

    public async Task<Result<IReadOnlyList<EligibleProjectDto>>> GetEligibleProjectsForFinalizeAsync(Guid id, Guid actorId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var guard = EnsureWritable();
        if (guard is not null) return Result<IReadOnlyList<EligibleProjectDto>>.Failure(guard);
        var session = await FindOwnedAsync(id, actorId, tenantId, cancellationToken, includeSegments: true);
        if (session is null) return Result<IReadOnlyList<EligibleProjectDto>>.Failure(Error.NotFound("Timer session not found"));
        if (session.Status != TimerSession.AwaitingFinalization) return Result<IReadOnlyList<EligibleProjectDto>>.Failure(Error.Conflict("Finish the timer before finalizing it"));

        var resource = await _context.Set<Resource>().FirstOrDefaultAsync(item => item.Id == session.ResourceId && item.TenantId == tenantId && !item.IsDeleted, cancellationToken);
        if (resource is null) return Result<IReadOnlyList<EligibleProjectDto>>.Failure(Error.NotFound("Resource not found"));
        var timezone = ResolveTimezone(resource.Timezone);
        var daily = BuildDailyTotals(session.Segments.OrderBy(item => item.StartedAt).ToList(), timezone);
        var workDates = daily.Where(item => item.ActualSeconds > 0).Select(item => item.WorkDate).Distinct().ToList();
        if (workDates.Count == 0) return Result<IReadOnlyList<EligibleProjectDto>>.Success(Array.Empty<EligibleProjectDto>());

        var projectIds = await _context.Set<ProjectMember>().AsNoTracking()
            .Where(pm => pm.TenantId == tenantId && pm.ResourceId == session.ResourceId && !pm.IsDeleted && pm.Status == "Active" && workDates.All(wd => pm.StartDate.Date <= wd && pm.EndDate.Date >= wd))
            .Select(pm => pm.ProjectId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var projects = await _context.Set<Project>().AsNoTracking()
            .Where(p => projectIds.Contains(p.Id) && p.TenantId == tenantId && !p.IsDeleted)
            .Select(p => new EligibleProjectDto { Id = p.Id, Code = p.Code, Name = p.Name })
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<EligibleProjectDto>>.Success(projects);
    }
}
