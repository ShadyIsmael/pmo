using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.ProjectPlanning.Application.DTOs;
using NEXUS.Modules.ProjectPlanning.Domain;
using NEXUS.Modules.ProjectPlanning.Domain.Scheduling;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.ProjectPlanning.Application;

/// <summary>Loads a Working Version graph and applies the pure scheduling result to tracked entities.</summary>
public sealed class PlanSchedulingService : IPlanSchedulingService
{
    private readonly DbContext _context;
    private readonly AutoSchedulingEngine _engine;
    private readonly SummaryTaskRollupService _summaryRollup;

    public PlanSchedulingService(DbContext context, AutoSchedulingEngine engine, SummaryTaskRollupService? summaryRollup = null)
    {
        _context = context;
        _engine = engine;
        _summaryRollup = summaryRollup ?? new SummaryTaskRollupService(context);
    }

    public async Task<Result<PlanSchedulingResult>> RecalculateAsync(
        Guid planVersionId,
        IReadOnlyCollection<SchedulingSeed> seeds,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var version = await _context.Set<PlanVersion>().FirstOrDefaultAsync(item => item.Id == planVersionId, cancellationToken);
        if (version is null) return Result<PlanSchedulingResult>.Failure(Error.NotFound("Plan version not found"));
        if (version.Type != PlanVersionType.Working || version.Status != PlanVersionStatus.Active)
            return Result<PlanSchedulingResult>.Failure(Error.Validation("Auto scheduling is only allowed in the active Working version"));

        var activities = await _context.Set<PlanActivity>().Where(item => item.PlanVersionId == planVersionId).ToListAsync(cancellationToken);
        MergeTracked(activities, planVersionId);
        activities = activities.Where(item => !item.IsDeleted).DistinctBy(item => item.Id).ToList();

        var dependencies = await _context.Set<PlanDependency>().Where(item => item.PlanVersionId == planVersionId).ToListAsync(cancellationToken);
        // Include restored soft-deleted dependencies from the current unit of
        // work. Their database row is still filtered until SaveChanges, but
        // scheduling must use the authoritative in-memory graph now.
        MergeTracked(dependencies, planVersionId);
        dependencies = dependencies.Where(item => !item.IsDeleted).DistinctBy(item => item.Id).ToList();

        var calendars = await _context.Set<PlanCalendar>()
            .Include(item => item.DayRules)
            .Include(item => item.Holidays)
            .Where(item => item.PlanVersionId == planVersionId && !item.IsDeleted)
            .ToListAsync(cancellationToken);

        var anchorByActivity = seeds.GroupBy(seed => seed.ActivityId).ToDictionary(
            group => group.Key,
            group => group.Select(seed => seed.Anchor).FirstOrDefault(anchor => anchor != SchedulingAnchor.None));

        var taskModels = activities.Select(activity => new AutoScheduleTask(
            activity.Id,
            activity.Type,
            activity.Duration,
            activity.StartDate,
            activity.EndDate,
            activity.CalendarId,
            anchorByActivity.GetValueOrDefault(activity.Id),
            activity.SchedulingMode,
            activity.ConstraintType,
            activity.ConstraintDate,
            activity.Deadline)).ToList();
        var linkModels = dependencies.Select(link => new AutoScheduleDependency(
            link.PredecessorActivityId,
            link.ActivityId,
            link.Type,
            link.Lag ?? 0)).ToList();
        var calendarModels = calendars.Select(calendar => new AutoScheduleCalendar(
            calendar.Id,
            calendar.DayRules.Where(rule => !rule.IsDeleted).Select(rule => new CalendarDayDefinition(
                rule.DayOfWeek, rule.IsWorkingDay, rule.StartTime, rule.EndTime, rule.BreakStartTime, rule.BreakEndTime)).ToList(),
            calendar.Holidays.Where(holiday => !holiday.IsDeleted).Select(holiday => new CalendarHolidayDefinition(holiday.Date, holiday.EndDate, holiday.IsRecurring)).ToList())).ToList();
        var defaultCalendarId = calendars.OrderByDescending(calendar => calendar.IsDefault).ThenBy(calendar => calendar.Name).Select(calendar => (Guid?)calendar.Id).FirstOrDefault();

        var calculation = _engine.Recalculate(taskModels, linkModels, calendarModels, defaultCalendarId, seeds.Select(seed => seed.ActivityId).ToList());
        var activityById = activities.ToDictionary(activity => activity.Id);
        if (calculation.HasCycle)
        {
            var affected = calculation.CycleActivityIds
                .Select(id => activityById.TryGetValue(id, out var activity) ? $"{activity.Code} ({activity.Name})" : id.ToString())
                .ToList();
            return Result<PlanSchedulingResult>.Failure(Error.Validation($"Dependency cycle affects: {string.Join(", ", affected)}"));
        }
        if (calculation.HasValidationError)
        {
            var affected = activityById.Values.FirstOrDefault(activity => calculation.ValidationError!.Contains(activity.Id.ToString(), StringComparison.OrdinalIgnoreCase));
            var prefix = affected is null ? "Scheduling conflict" : $"Scheduling conflict for '{affected.Code}' ({affected.Name})";
            return Result<PlanSchedulingResult>.Failure(Error.Validation($"{prefix}: {calculation.ValidationError}"));
        }

        foreach (var scheduled in calculation.ChangedTasks)
        {
            var activity = activityById[scheduled.Id];
            activity.StartDate = scheduled.Start;
            activity.EndDate = scheduled.Finish;
            activity.Duration = scheduled.Duration;
            activity.UpdatedBy = userId;
        }

        var summaryActivityIds = await _summaryRollup.RecalculateAsync(planVersionId, userId, cancellationToken: cancellationToken);
        var recalculatedIds = calculation.ChangedTasks.Select(task => task.Id).ToHashSet();
        recalculatedIds.UnionWith(summaryActivityIds);

        return Result<PlanSchedulingResult>.Success(new PlanSchedulingResult(
            recalculatedIds.Count,
            calculation.ChangedTasks.Select(task => new PlanScheduledActivityStateDto
            {
                ActivityId = task.Id,
                StartDate = task.Start,
                EndDate = task.Finish,
                Duration = task.Duration,
            }).ToList()));
    }

    private void MergeTracked<T>(ICollection<T> entities, Guid planVersionId) where T : BaseEntity
    {
        foreach (var entry in _context.ChangeTracker.Entries<T>().Where(entry => entry.State is EntityState.Added or EntityState.Modified))
        {
            var entityVersionId = entry.Entity switch
            {
                PlanActivity activity => activity.PlanVersionId,
                PlanDependency dependency => dependency.PlanVersionId,
                _ => Guid.Empty
            };
            if (entityVersionId == planVersionId && entities.All(entity => entity.Id != entry.Entity.Id)) entities.Add(entry.Entity);
        }
    }
}
