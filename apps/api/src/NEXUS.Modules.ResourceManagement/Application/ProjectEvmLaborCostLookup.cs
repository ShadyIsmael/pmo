using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Organization.Domain;
using NEXUS.Modules.ResourceManagement.Domain;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.ResourceManagement.Application;

/// <summary>
/// Prices baseline planned hours and persisted actual TimeEntry work using the
/// same profiles, calendar capacity, and canonical calculator used by resource
/// cost analytics. All output remains in the project's currency; no FX is
/// inferred here.
/// </summary>
public sealed class ProjectEvmLaborCostLookup : IProjectEvmLaborCostLookup
{
    private const int MaxAssignmentDays = 3660;
    private readonly DbContext _context;
    private readonly IActualWorkCostLookup _actualWork;
    private readonly ResourceLaborCostCalculator _calculator;

    public ProjectEvmLaborCostLookup(DbContext context, IActualWorkCostLookup actualWork, ResourceLaborCostCalculator calculator)
    {
        _context = context;
        _actualWork = actualWork;
        _calculator = calculator;
    }

    public async Task<ProjectEvmLaborCostSummary> GetAsync(
        Guid tenantId,
        Guid projectId,
        string projectCurrency,
        DateTime statusDate,
        IReadOnlyList<ProjectEvmAssignmentInput> assignments,
        CancellationToken cancellationToken = default,
        IReadOnlyCollection<Guid>? includedPlanActivityIds = null,
        bool includeUnassignedProjectActuals = false)
    {
        var currency = NormalizeCurrency(projectCurrency);
        var datedAssignments = assignments.Where(item => item.PlannedHours.GetValueOrDefault() > 0m).ToList();
        var plannedValueComplete = true;
        var earnedValueComplete = true;
        var hasPlannedAssignments = assignments.Count > 0;
        var plannedReason = hasPlannedAssignments ? null : "NO_BASELINE_ASSIGNMENTS";
        var earnedReason = plannedReason;
        decimal bac = 0m;
        decimal pv = 0m;
        decimal ev = 0m;

        foreach (var item in assignments)
        {
            if (!item.PlannedHours.HasValue)
            {
                plannedValueComplete = earnedValueComplete = false;
                plannedReason ??= "PLANNED_HOURS_MISSING";
                earnedReason ??= "PLANNED_HOURS_MISSING";
            }
            else if (item.PlannedHours.Value < 0m)
            {
                plannedValueComplete = earnedValueComplete = false;
                plannedReason ??= "PLANNED_HOURS_INVALID";
                earnedReason ??= "PLANNED_HOURS_INVALID";
            }

            if (item.PlannedHours.GetValueOrDefault() > 0m && !item.WorkingProgress.HasValue)
            {
                earnedValueComplete = false;
                earnedReason ??= "ACTIVITY_PROGRESS_MISSING";
            }
        }

        var rangeError = datedAssignments.Any(item =>
            !item.StartDate.HasValue || !item.EndDate.HasValue || item.EndDate.Value.Date < item.StartDate.Value.Date
            || (item.EndDate.Value.Date - item.StartDate.Value.Date).TotalDays + 1 > MaxAssignmentDays);
        if (rangeError)
        {
            plannedValueComplete = earnedValueComplete = false;
            plannedReason ??= "BASELINE_DATES_INVALID";
            earnedReason ??= "BASELINE_DATES_INVALID";
        }

        var validAssignments = rangeError ? new List<ProjectEvmAssignmentInput>() : datedAssignments;
        var earliestDate = validAssignments.Count == 0
            ? statusDate.Date
            : validAssignments.Min(item => item.StartDate!.Value.Date);
        var actualSlices = await _actualWork.GetAsync(
            tenantId,
            DateTime.MinValue.Date,
            statusDate.Date,
            projectIds: [projectId],
            cancellationToken: cancellationToken);
        var actualEntries = actualSlices.Where(item => item.ActualSeconds > 0 && item.WorkDate.Date <= statusDate.Date
            && (includedPlanActivityIds is null
                || item.PlanActivityId.HasValue && includedPlanActivityIds.Contains(item.PlanActivityId.Value)
                || includeUnassignedProjectActuals && !item.PlanActivityId.HasValue))
            .ToList();
        if (actualEntries.Count > 0 && actualEntries.Min(item => item.WorkDate.Date) < earliestDate)
            earliestDate = actualEntries.Min(item => item.WorkDate.Date);
        var resourceIds = validAssignments.Select(item => item.ResourceId)
            .Concat(actualEntries.Select(item => item.ResourceId)).Distinct().ToList();

        List<Resource> resources = resourceIds.Count == 0
            ? new List<Resource>()
            : await _context.Set<Resource>().AsNoTracking()
                .Where(item => item.TenantId == tenantId && !item.IsDeleted && resourceIds.Contains(item.Id))
                .ToListAsync(cancellationToken);
        var resourceById = resources.ToDictionary(item => item.Id);

        var maxScheduleDate = validAssignments.Count == 0
            ? statusDate.Date
            : validAssignments.Max(item => item.EndDate!.Value.Date);
        var profileEnd = maxScheduleDate > statusDate.Date ? maxScheduleDate : statusDate.Date;
        List<ResourceCostProfile> profiles = resourceIds.Count == 0
            ? new List<ResourceCostProfile>()
            : await _context.Set<ResourceCostProfile>().AsNoTracking()
                .Where(item => item.TenantId == tenantId && !item.IsDeleted && resourceIds.Contains(item.ResourceId)
                    && item.EffectiveFrom.Date <= profileEnd
                    && (!item.EffectiveTo.HasValue || item.EffectiveTo.Value.Date >= earliestDate))
                .OrderBy(item => item.ResourceId).ThenBy(item => item.EffectiveFrom)
                .ToListAsync(cancellationToken);
        var profilesByResource = profiles.GroupBy(item => item.ResourceId).ToDictionary(group => group.Key, group => group.ToList());

        var calendars = await _context.Set<WorkingCalendar>().AsNoTracking()
            .Include(item => item.Holidays)
            .Include(item => item.DayRules)
            .Where(item => item.TenantId == tenantId && !item.IsDeleted).ToListAsync(cancellationToken);
        var defaultCalendar = calendars.FirstOrDefault(item => item.IsDefault && item.HasValidSchedule);
        var calendarById = calendars.ToDictionary(item => item.Id);
        List<ResourceCalendar> resourceCalendars = resourceIds.Count == 0
            ? new List<ResourceCalendar>()
            : await _context.Set<ResourceCalendar>().AsNoTracking()
                .Where(item => item.TenantId == tenantId && !item.IsDeleted && resourceIds.Contains(item.ResourceId)
                    && item.Date >= earliestDate && item.Date <= profileEnd)
                .ToListAsync(cancellationToken);
        var exceptions = resourceCalendars.GroupBy(item => (item.ResourceId, Date: item.Date.Date))
            .ToDictionary(group => group.Key, group => group.ToList());
        List<Contract> contracts = resourceIds.Count == 0
            ? new List<Contract>()
            : await _context.Set<Contract>().AsNoTracking()
                .Where(item => item.TenantId == tenantId && !item.IsDeleted && resourceIds.Contains(item.ResourceId)
                    && item.Status == "Active" && item.StartDate.Date <= profileEnd
                    && (!item.EndDate.HasValue || item.EndDate.Value.Date >= earliestDate))
                .ToListAsync(cancellationToken);
        var contractsByResource = contracts.GroupBy(item => item.ResourceId).ToDictionary(group => group.Key, group => group.ToList());

        foreach (var assignment in validAssignments)
        {
            if (assignment.ResourceId == Guid.Empty)
            {
                plannedValueComplete = earnedValueComplete = false;
                plannedReason ??= "RESOURCE_ASSIGNMENT_MISSING";
                earnedReason ??= "RESOURCE_ASSIGNMENT_MISSING";
                continue;
            }
            if (!resourceById.ContainsKey(assignment.ResourceId))
            {
                plannedValueComplete = earnedValueComplete = false;
                plannedReason ??= "RESOURCE_NOT_FOUND";
                earnedReason ??= "RESOURCE_NOT_FOUND";
                continue;
            }

            var dates = Enumerable.Range(0, (assignment.EndDate!.Value.Date - assignment.StartDate!.Value.Date).Days + 1)
                .Select(offset => assignment.StartDate.Value.Date.AddDays(offset)).ToList();
            var capacityByDate = dates.ToDictionary(date => date, date => Capacity(assignment.ResourceId, date, resourceById, defaultCalendar, calendarById, exceptions, contractsByResource));
            var totalCapacity = capacityByDate.Values.Sum();
            if (totalCapacity <= 0m)
            {
                plannedValueComplete = earnedValueComplete = false;
                plannedReason ??= "RESOURCE_CAPACITY_MISSING";
                earnedReason ??= "RESOURCE_CAPACITY_MISSING";
                continue;
            }
            var lineBac = 0m;
            var linePv = 0m;
            var lineComplete = true;

            foreach (var date in dates)
            {
                var dailyHours = assignment.PlannedHours!.Value * capacityByDate[date] / totalCapacity;
                if (dailyHours <= 0m) continue;

                var profile = ResolveProfile(assignment.ResourceId, date, profilesByResource, out var overlap);
                if (overlap)
                {
                    lineComplete = false;
                    plannedReason ??= "RESOURCE_COST_PROFILE_AMBIGUOUS";
                    earnedReason ??= "RESOURCE_COST_PROFILE_AMBIGUOUS";
                    continue;
                }
                if (profile is null)
                {
                    lineComplete = false;
                    plannedReason ??= "RESOURCE_COST_PROFILE_MISSING";
                    earnedReason ??= "RESOURCE_COST_PROFILE_MISSING";
                    continue;
                }
                if (NormalizeCurrency(profile.Currency) != currency)
                {
                    lineComplete = false;
                    plannedReason ??= "CURRENCY_MISMATCH";
                    earnedReason ??= "CURRENCY_MISMATCH";
                    continue;
                }

                var periodCapacity = CompensationCapacity(assignment.ResourceId, date, profile, resourceById, defaultCalendar, calendarById, exceptions, contractsByResource);
                var result = _calculator.Calculate(new ResourceDayCostInput(profile, capacityByDate[date], dailyHours, 0m, 0m, periodCapacity));
                if (!result.PlannedLaborCost.HasValue)
                {
                    lineComplete = false;
                    plannedReason ??= "RESOURCE_COST_PROFILE_MISSING";
                    earnedReason ??= "RESOURCE_COST_PROFILE_MISSING";
                    continue;
                }
                lineBac += result.PlannedLaborCost.Value;
                if (date <= statusDate.Date) linePv += result.PlannedLaborCost.Value;
            }

            if (!lineComplete)
            {
                plannedValueComplete = earnedValueComplete = false;
                continue;
            }

            bac += lineBac;
            pv += linePv;
            if (assignment.WorkingProgress.HasValue)
                ev += lineBac * Math.Clamp(assignment.WorkingProgress.Value, 0m, 100m) / 100m;
        }

        var hasActualLabor = actualEntries.Count > 0;
        var actualCostComplete = true;
        var actualReason = hasActualLabor ? null : "NO_ACTUAL_LABOR_DATA";
        var actualLabor = 0m;
        var capacityByResourceDate = new Dictionary<(Guid ResourceId, DateTime Date), decimal>();
        foreach (var day in actualEntries.GroupBy(item => (item.ResourceId, Date: item.WorkDate.Date)))
        {
            if (!resourceById.ContainsKey(day.Key.ResourceId))
            {
                actualCostComplete = false;
                actualReason ??= "RESOURCE_NOT_FOUND";
                continue;
            }
            var profile = ResolveProfile(day.Key.ResourceId, day.Key.Date, profilesByResource, out var overlap);
            if (overlap)
            {
                actualCostComplete = false;
                actualReason ??= "RESOURCE_COST_PROFILE_AMBIGUOUS";
                continue;
            }
            if (profile is null)
            {
                actualCostComplete = false;
                actualReason ??= "RESOURCE_COST_PROFILE_MISSING";
                continue;
            }
            if (NormalizeCurrency(profile.Currency) != currency)
            {
                actualCostComplete = false;
                actualReason ??= "CURRENCY_MISMATCH";
                continue;
            }

            var capacity = Capacity(day.Key.ResourceId, day.Key.Date, resourceById, defaultCalendar, calendarById, exceptions, contractsByResource);
            var remainingCapacity = capacity;
            var regular = 0m;
            var excess = 0m;
            foreach (var entry in day.OrderBy(item => item.StartTime).ThenBy(item => item.TimeEntryId))
            {
                var hours = Math.Max(0m, entry.ActualSeconds / 3600m);
                var regularHours = Math.Min(hours, remainingCapacity);
                remainingCapacity -= regularHours;
                regular += regularHours;
                excess += hours - regularHours;
            }
            var periodCapacity = CompensationCapacity(day.Key.ResourceId, day.Key.Date, profile, resourceById, defaultCalendar, calendarById, exceptions, contractsByResource);
            var result = _calculator.Calculate(new ResourceDayCostInput(profile, capacity, 0m, regular, excess, periodCapacity));
            if (!result.TotalActualLaborCost.HasValue)
            {
                actualCostComplete = false;
                actualReason ??= "RESOURCE_COST_PROFILE_MISSING";
            }
            else actualLabor += result.TotalActualLaborCost.Value;
        }

        if (!hasPlannedAssignments)
        {
            plannedValueComplete = earnedValueComplete = false;
        }
        if (!plannedValueComplete && plannedReason is null) plannedReason = "EVM_DATA_INCOMPLETE";
        if (!earnedValueComplete && earnedReason is null) earnedReason = "EVM_DATA_INCOMPLETE";
        if (hasActualLabor && !actualCostComplete && actualReason is null) actualReason = "EVM_DATA_INCOMPLETE";

        return new ProjectEvmLaborCostSummary(
            plannedValueComplete ? bac : null,
            plannedValueComplete ? pv : null,
            earnedValueComplete ? ev : null,
            hasActualLabor && actualCostComplete ? actualLabor : null,
            plannedValueComplete ? null : plannedReason,
            earnedValueComplete ? null : earnedReason,
            hasActualLabor && actualCostComplete ? null : actualReason,
            hasActualLabor);
    }

    private static decimal Capacity(
        Guid resourceId,
        DateTime date,
        IReadOnlyDictionary<Guid, Resource> resources,
        WorkingCalendar? defaultCalendar,
        IReadOnlyDictionary<Guid, WorkingCalendar> calendars,
        IReadOnlyDictionary<(Guid ResourceId, DateTime Date), List<ResourceCalendar>> exceptions,
        IReadOnlyDictionary<Guid, List<Contract>> contracts)
    {
        if (!resources.TryGetValue(resourceId, out var resource)) return 0m;
        var calendar = resource.BaseCalendarId.HasValue
            && calendars.TryGetValue(resource.BaseCalendarId.Value, out var configured)
            && configured.HasValidSchedule
                ? configured
                : defaultCalendar;
        if (calendar?.HasValidSchedule != true) return 0m;
        var resourceContracts = contracts.GetValueOrDefault(resourceId);
        if (resourceContracts is { Count: > 0 } && !resourceContracts.Any(item =>
                item.StartDate.Date <= date.Date && (!item.EndDate.HasValue || item.EndDate.Value.Date >= date.Date))) return 0m;
        ResourceCalendar? exception = exceptions.TryGetValue((resourceId, date.Date), out var dayExceptions)
            ? dayExceptions.OrderByDescending(item => item.UpdatedAt ?? item.CreatedAt).First()
            : null;
        return ResourceWorkingCapacityCalculator.GetAvailableDailyCapacity(resource, calendar, calendar, date, exception);
    }

    private static decimal CompensationCapacity(
        Guid resourceId,
        DateTime date,
        ResourceCostProfile profile,
        IReadOnlyDictionary<Guid, Resource> resources,
        WorkingCalendar? defaultCalendar,
        IReadOnlyDictionary<Guid, WorkingCalendar> calendars,
        IReadOnlyDictionary<(Guid ResourceId, DateTime Date), List<ResourceCalendar>> exceptions,
        IReadOnlyDictionary<Guid, List<Contract>> contracts)
    {
        if (profile.CompensationType == CompensationType.Weekly)
        {
            var monday = date.Date.AddDays(-(((int)date.DayOfWeek + 6) % 7));
            return Enumerable.Range(0, 7).Sum(offset => Capacity(resourceId, monday.AddDays(offset), resources, defaultCalendar, calendars, exceptions, contracts));
        }
        if (profile.CompensationType == CompensationType.Monthly)
        {
            var first = new DateTime(date.Year, date.Month, 1);
            return Enumerable.Range(0, DateTime.DaysInMonth(date.Year, date.Month))
                .Sum(offset => Capacity(resourceId, first.AddDays(offset), resources, defaultCalendar, calendars, exceptions, contracts));
        }
        return 1m;
    }

    private static ResourceCostProfile? ResolveProfile(
        Guid resourceId,
        DateTime date,
        IReadOnlyDictionary<Guid, List<ResourceCostProfile>> profiles,
        out bool overlap)
    {
        var matches = profiles.GetValueOrDefault(resourceId)?.Where(item => item.EffectiveFrom.Date <= date.Date
            && (!item.EffectiveTo.HasValue || item.EffectiveTo.Value.Date >= date.Date)).ToList() ?? [];
        overlap = matches.Count > 1;
        return matches.Count == 1 ? matches[0] : null;
    }

    private static bool IsWeekend(DateTime date, int workingDaysPerWeek)
        => workingDaysPerWeek >= 6 ? date.DayOfWeek == DayOfWeek.Sunday : date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

    private static string NormalizeCurrency(string? value) => (value ?? string.Empty).Trim().ToUpperInvariant();
}
