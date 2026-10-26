using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.ProjectPlanning.Domain;
using NEXUS.Modules.ProjectPlanning.Domain.Scheduling;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.ProjectPlanning.Application;

public sealed class PlanSchedulingAnalysisService : IPlanSchedulingAnalysisService
{
    private readonly DbContext _context;
    private readonly CriticalPathAnalyzer _analyzer;

    public PlanSchedulingAnalysisService(DbContext context, CriticalPathAnalyzer analyzer)
    {
        _context = context;
        _analyzer = analyzer;
    }

    public async Task<Result<Guid>> GetActiveWorkingVersionIdAsync(Guid projectId, Guid planId, CancellationToken cancellationToken = default)
    {
        var planExistsInProject = await _context.Set<Plan>()
            .AsNoTracking()
            .AnyAsync(item => item.Id == planId && item.ProjectId == projectId, cancellationToken);
        if (!planExistsInProject)
            return Result<Guid>.Failure(Error.NotFound("Plan not found"));

        var version = await _context.Set<PlanVersion>()
            .FirstOrDefaultAsync(item => item.PlanId == planId && item.Type == PlanVersionType.Working && item.Status == PlanVersionStatus.Active, cancellationToken);
        return version is null
            ? Result<Guid>.Failure(Error.NotFound("No active working version exists for this plan"))
            : Result<Guid>.Success(version.Id);
    }

    public async Task<Result<CriticalPathAnalysisResult>> AnalyzeAsync(Guid planVersionId, CancellationToken cancellationToken = default)
    {
        var version = await _context.Set<PlanVersion>().AsNoTracking().FirstOrDefaultAsync(item => item.Id == planVersionId, cancellationToken);
        if (version is null) return Result<CriticalPathAnalysisResult>.Failure(Error.NotFound("Plan version not found"));
        if (version.Type != PlanVersionType.Working || version.Status != PlanVersionStatus.Active)
            return Result<CriticalPathAnalysisResult>.Failure(Error.Validation("Scheduling analysis is only available for the active Working version"));

        var activities = await _context.Set<PlanActivity>()
            .Where(item => item.PlanVersionId == planVersionId && !item.IsDeleted)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        var dependencies = await _context.Set<PlanDependency>()
            .Where(item => item.PlanVersionId == planVersionId && !item.IsDeleted)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        var calendars = await _context.Set<PlanCalendar>()
            .Include(item => item.DayRules)
            .Include(item => item.Holidays)
            .Where(item => item.PlanVersionId == planVersionId && !item.IsDeleted)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var taskModels = activities.Select(activity => new AutoScheduleTask(
            activity.Id,
            activity.Type,
            activity.Duration,
            activity.StartDate,
            activity.EndDate,
            activity.CalendarId,
            SchedulingAnchor.None,
            activity.SchedulingMode,
            activity.ConstraintType,
            activity.ConstraintDate,
            activity.Deadline)).ToList();
        var linkModels = dependencies.Select(link => new AutoScheduleDependency(
            link.PredecessorActivityId,
            link.ActivityId,
            link.Type,
            link.Lag ?? 0,
            link.Id)).ToList();
        var calendarModels = calendars.Select(calendar => new AutoScheduleCalendar(
            calendar.Id,
            calendar.DayRules.Where(rule => !rule.IsDeleted).Select(rule => new CalendarDayDefinition(
                rule.DayOfWeek, rule.IsWorkingDay, rule.StartTime, rule.EndTime, rule.BreakStartTime, rule.BreakEndTime)).ToList(),
            calendar.Holidays.Where(holiday => !holiday.IsDeleted).Select(holiday => new CalendarHolidayDefinition(holiday.Date, holiday.EndDate, holiday.IsRecurring)).ToList())).ToList();
        var defaultCalendarId = calendars
            .OrderByDescending(calendar => calendar.IsDefault)
            .ThenBy(calendar => calendar.Name)
            .Select(calendar => (Guid?)calendar.Id)
            .FirstOrDefault();

        return Result<CriticalPathAnalysisResult>.Success(_analyzer.Analyze(taskModels, linkModels, calendarModels, defaultCalendarId));
    }

    public async Task<Result<SchedulingExplanation>> ExplainAsync(Guid planVersionId, Guid activityId, CancellationToken cancellationToken = default)
    {
        var version = await _context.Set<PlanVersion>().AsNoTracking().FirstOrDefaultAsync(item => item.Id == planVersionId, cancellationToken);
        if (version is null) return Result<SchedulingExplanation>.Failure(Error.NotFound("Plan version not found"));
        if (version.Type != PlanVersionType.Working || version.Status != PlanVersionStatus.Active)
            return Result<SchedulingExplanation>.Failure(Error.Validation("Scheduling explanations are only available for the active Working version"));

        var activities = await _context.Set<PlanActivity>().AsNoTracking().Where(item => item.PlanVersionId == planVersionId && !item.IsDeleted).ToListAsync(cancellationToken);
        var activity = activities.FirstOrDefault(item => item.Id == activityId);
        if (activity is null) return Result<SchedulingExplanation>.Failure(Error.NotFound("Activity not found"));
        var dependencies = await _context.Set<PlanDependency>().AsNoTracking().Where(item => item.PlanVersionId == planVersionId && !item.IsDeleted).ToListAsync(cancellationToken);
        var calendars = await _context.Set<PlanCalendar>().Include(item => item.DayRules).Include(item => item.Holidays).AsNoTracking().Where(item => item.PlanVersionId == planVersionId && !item.IsDeleted).ToListAsync(cancellationToken);
        var taskModels = activities.Select(ToTaskModel).ToList();
        var links = dependencies.Select(link => new AutoScheduleDependency(link.PredecessorActivityId, link.ActivityId, link.Type, link.Lag ?? 0, link.Id)).ToList();
        var calendarModels = calendars.Select(ToCalendarModel).ToList();
        var defaultCalendarId = calendars.OrderByDescending(item => item.IsDefault).ThenBy(item => item.Name).Select(item => (Guid?)item.Id).FirstOrDefault();
        var analysis = _analyzer.Analyze(taskModels, links, calendarModels, defaultCalendarId);
        var task = taskModels.First(item => item.Id == activityId);
        var metrics = analysis.ActivityMetrics.GetValueOrDefault(activityId);
        return Result<SchedulingExplanation>.Success(SchedulingExplanationBuilder.Build(task, taskModels, links, calendarModels, defaultCalendarId, metrics, analysis.HasCycle));
    }

    private static AutoScheduleTask ToTaskModel(PlanActivity activity) => new(activity.Id, activity.Type, activity.Duration, activity.StartDate, activity.EndDate, activity.CalendarId, SchedulingAnchor.None, activity.SchedulingMode, activity.ConstraintType, activity.ConstraintDate, activity.Deadline);

    private static AutoScheduleCalendar ToCalendarModel(PlanCalendar calendar) => new(calendar.Id,
        calendar.DayRules.Where(rule => !rule.IsDeleted).Select(rule => new CalendarDayDefinition(rule.DayOfWeek, rule.IsWorkingDay, rule.StartTime, rule.EndTime, rule.BreakStartTime, rule.BreakEndTime)).ToList(),
        calendar.Holidays.Where(holiday => !holiday.IsDeleted).Select(holiday => new CalendarHolidayDefinition(holiday.Date, holiday.EndDate, holiday.IsRecurring)).ToList());
}
