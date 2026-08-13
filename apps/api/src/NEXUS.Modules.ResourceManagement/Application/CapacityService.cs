using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Organization.Domain;
using NEXUS.Modules.ResourceManagement.Application.DTOs;
using NEXUS.Modules.ResourceManagement.Domain;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.ResourceManagement.Application;

public class CapacityService : ICapacityService
{
    private readonly DbContext _context;
    private const decimal DefaultHoursPerDay = 8m;
    private const int DefaultDaysPerWeek = 5;

    public CapacityService(DbContext context)
    {
        _context = context;
    }

    public async Task<Result<CapacityMatrixDto>> GetMatrixAsync(CapacityFilterDto filter, CancellationToken cancellationToken = default)
    {
        var startDate = filter.StartDate ?? new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        var endDate = filter.EndDate ?? startDate.AddMonths(3);
        var periods = GeneratePeriods(filter.PeriodType, startDate, endDate);

        var resourcesQuery = _context.Set<Resource>().Where(r => !r.IsDeleted && r.IsActive);
        if (!string.IsNullOrEmpty(filter.DepartmentId) && Guid.TryParse(filter.DepartmentId, out var deptId))
            resourcesQuery = resourcesQuery.Where(r => r.DepartmentId == deptId);
        if (!string.IsNullOrEmpty(filter.LocationId) && Guid.TryParse(filter.LocationId, out var locId))
            resourcesQuery = resourcesQuery.Where(r => r.LocationId == locId);
        if (!string.IsNullOrEmpty(filter.ResourceType))
            resourcesQuery = resourcesQuery.Where(r => r.Type == filter.ResourceType);
        if (filter.ResourceId.HasValue)
            resourcesQuery = resourcesQuery.Where(r => r.Id == filter.ResourceId.Value);

        var resources = await resourcesQuery.ToListAsync(cancellationToken);
        if (resources.Count == 0)
            return Result<CapacityMatrixDto>.Success(new CapacityMatrixDto { Periods = periods });

        var allocations = await _context.Set<ResourceAllocation>()
            .Include(a => a.Resource)
            .Where(a => a.Status != ResourceAllocation.StatusCancelled && !a.IsDeleted &&
                a.StartDate < endDate && a.EndDate > startDate)
            .ToListAsync(cancellationToken);

        var calendars = await _context.Set<WorkingCalendar>().Include(c => c.Holidays).ToListAsync(cancellationToken);
        var defaultCalendar = calendars.FirstOrDefault(c => c.IsDefault) ?? new WorkingCalendar { HoursPerDay = 8, WorkingDaysPerWeek = 5 };

        var resourceCalendars = await _context.Set<ResourceCalendar>()
            .Where(rc => rc.DayType == "Leave" || rc.DayType == "NonWorking")
            .Where(rc => rc.Date >= startDate && rc.Date < endDate)
            .ToListAsync(cancellationToken);

        var contracts = await _context.Set<Contract>()
            .Where(c => c.Status == "Active")
            .ToListAsync(cancellationToken);

        var rows = new List<CapacityMatrixRowDto>();
        decimal totalGross = 0, totalNet = 0, totalConfirmed = 0, totalProposed = 0;

        foreach (var resource in resources)
        {
            var resourceAllocs = allocations.Where(a => a.ResourceId == resource.Id).ToList();
            var cells = new List<CapacityCellDto>();

            foreach (var period in periods)
            {
                var activeContract = contracts.FirstOrDefault(c => c.ResourceId == resource.Id);
                var effectiveStart = period.Start;
                var effectiveEnd = period.End;
                if (activeContract != null)
                {
                    if (activeContract.StartDate > effectiveStart)
                        effectiveStart = activeContract.StartDate;
                    if (activeContract.EndDate.HasValue && activeContract.EndDate.Value < effectiveEnd)
                        effectiveEnd = activeContract.EndDate.Value;
                }

                decimal grossHours, nonWorkingHours, netHours, leaveHours;
                if (effectiveStart < effectiveEnd)
                {
                    grossHours = CalculateGrossCapacity(resource, effectiveStart, effectiveEnd, defaultCalendar);
                    nonWorkingHours = GetNonWorkingHours(effectiveStart, effectiveEnd, defaultCalendar, calendars);
                    leaveHours = resourceCalendars
                        .Where(rc => rc.ResourceId == resource.Id && rc.Date >= effectiveStart && rc.Date < effectiveEnd)
                        .Sum(rc => rc.AvailableHours ?? defaultCalendar.HoursPerDay);
                    netHours = grossHours - nonWorkingHours - leaveHours;
                }
                else
                {
                    grossHours = 0;
                    nonWorkingHours = 0;
                    leaveHours = 0;
                    netHours = 0;
                }

                var periodAllocs = resourceAllocs
                    .Where(a => a.StartDate < period.End && a.EndDate > period.Start)
                    .ToList();

                var confirmedHours = periodAllocs
                    .Where(a => a.Status == ResourceAllocation.StatusConfirmed)
                    .Sum(a => a.HoursPerWeek ?? (a.AllocationPercentage / 100m * grossHours));

                var proposedHours = periodAllocs
                    .Where(a => a.Status == ResourceAllocation.StatusProposed || a.Status == ResourceAllocation.StatusTentative)
                    .Sum(a => a.HoursPerWeek ?? (a.AllocationPercentage / 100m * grossHours));

                var availableHours = netHours - confirmedHours - (filter.IncludeProposed ? proposedHours : 0);
                var utilizationPercent = netHours > 0 ? Math.Round((confirmedHours + proposedHours) / netHours * 100, 1) : 0;

                totalGross += grossHours;
                totalNet += netHours;
                totalConfirmed += confirmedHours;
                totalProposed += proposedHours;

                cells.Add(new CapacityCellDto
                {
                    PeriodKey = period.Key,
                    PeriodStart = period.Start,
                    PeriodEnd = period.End,
                    GrossCapacityHours = Math.Round(grossHours, 1),
                    NetCapacityHours = Math.Round(netHours, 1),
                    LeaveHours = Math.Round(leaveHours, 1),
                    ConfirmedHours = Math.Round(confirmedHours, 1),
                    ProposedHours = Math.Round(proposedHours, 1),
                    AvailableHours = Math.Round(Math.Max(0, availableHours), 1),
                    UtilizationPercent = utilizationPercent,
                    HeatmapState = GetHeatmapState(utilizationPercent, confirmedHours, proposedHours, grossHours),
                    Allocations = periodAllocs.Select(a => new AllocationBreakdownDto
                    {
                        AllocationId = a.Id,
                        ProjectId = a.ProjectId,
                        ProjectName = a.ProjectName ?? "Unnamed",
                        Percentage = a.AllocationPercentage,
                        Hours = Math.Round(a.HoursPerWeek ?? (a.AllocationPercentage / 100m * grossHours), 1),
                        Status = a.Status,
                        IsBillable = a.IsBillable
                    }).ToList()
                });
            }

            var weeklyHours = CalculateWeeklyCapacity(resource, defaultCalendar);
            var currentUtil = resource.CurrentUtilization ?? 0;
            var earliestDate = CalculateEarliestAvailableDate(resource, resourceAllocs, startDate);

            rows.Add(new CapacityMatrixRowDto
            {
                ResourceId = resource.Id,
                ResourceName = resource.Name,
                JobTitle = resource.JobTitle,
                DepartmentName = await ResolveDepartmentName(resource.DepartmentId, cancellationToken),
                Seniority = resource.Seniority,
                WeeklyCapacityHours = Math.Round(weeklyHours, 1),
                CurrentUtilization = currentUtil,
                EarliestAvailableDate = earliestDate,
                Cells = cells
            });
        }

        var totalAvailable = totalNet - totalConfirmed - (filter.IncludeProposed ? totalProposed : 0);
        return Result<CapacityMatrixDto>.Success(new CapacityMatrixDto
        {
            Rows = rows,
            Periods = periods,
            Summary = new CapacitySummaryDto
            {
                TotalGrossCapacityHours = Math.Round(totalGross, 1),
                TotalNetCapacityHours = Math.Round(totalNet, 1),
                TotalConfirmedHours = Math.Round(totalConfirmed, 1),
                TotalProposedHours = Math.Round(totalProposed, 1),
                TotalAvailableHours = Math.Round(Math.Max(0, totalAvailable), 1),
                OverallUtilizationPercent = totalNet > 0 ? Math.Round((totalConfirmed + totalProposed) / totalNet * 100, 1) : 0
            }
        });
    }

    public async Task<PagedResult<ResourceAvailabilityDto>> GetAvailabilityAsync(CapacityFilterDto filter, CancellationToken cancellationToken = default)
    {
        var startDate = filter.StartDate ?? DateTime.UtcNow;
        var endDate = filter.EndDate ?? startDate.AddMonths(1);

        var resourcesQuery = _context.Set<Resource>().Where(r => !r.IsDeleted && r.IsActive);
        if (!string.IsNullOrEmpty(filter.DepartmentId) && Guid.TryParse(filter.DepartmentId, out var deptId))
            resourcesQuery = resourcesQuery.Where(r => r.DepartmentId == deptId);

        var total = await resourcesQuery.CountAsync(cancellationToken);
        var resources = await resourcesQuery.ToListAsync(cancellationToken);

        var allAllocations = await _context.Set<ResourceAllocation>()
            .Where(a => a.Status != ResourceAllocation.StatusCancelled && !a.IsDeleted &&
                a.StartDate < endDate && a.EndDate > startDate)
            .ToListAsync(cancellationToken);

        var calendars = await _context.Set<WorkingCalendar>().Include(c => c.Holidays).ToListAsync(cancellationToken);
        var defaultCalendar = calendars.FirstOrDefault(c => c.IsDefault) ?? new WorkingCalendar { HoursPerDay = 8, WorkingDaysPerWeek = 5 };

        var resourceCalendars = await _context.Set<ResourceCalendar>()
            .Where(rc => rc.DayType == "Leave" || rc.DayType == "NonWorking")
            .Where(rc => rc.Date >= startDate && rc.Date < endDate)
            .ToListAsync(cancellationToken);

        var contracts = await _context.Set<Contract>()
            .Where(c => c.Status == "Active")
            .ToListAsync(cancellationToken);

        var items = resources.Select(r =>
        {
            var allocs = allAllocations.Where(a => a.ResourceId == r.Id).ToList();

            var activeContract = contracts.FirstOrDefault(c => c.ResourceId == r.Id);
            var effectiveStart = startDate;
            var effectiveEnd = endDate;
            if (activeContract != null)
            {
                if (activeContract.StartDate > effectiveStart)
                    effectiveStart = activeContract.StartDate;
                if (activeContract.EndDate.HasValue && activeContract.EndDate.Value < effectiveEnd)
                    effectiveEnd = activeContract.EndDate.Value;
            }

            decimal grossCapacity, netCapacity;
            if (effectiveStart < effectiveEnd)
            {
                grossCapacity = CalculateGrossCapacity(r, effectiveStart, effectiveEnd, defaultCalendar);
                var nonWorking = GetNonWorkingHours(effectiveStart, effectiveEnd, defaultCalendar, calendars);
                var leaveHours = resourceCalendars
                    .Where(rc => rc.ResourceId == r.Id && rc.Date >= effectiveStart && rc.Date < effectiveEnd)
                    .Sum(rc => rc.AvailableHours ?? defaultCalendar.HoursPerDay);
                netCapacity = grossCapacity - nonWorking - leaveHours;
            }
            else
            {
                grossCapacity = 0;
                netCapacity = 0;
            }

            var confirmed = allocs.Where(a => a.Status == ResourceAllocation.StatusConfirmed)
                .Sum(a => a.HoursPerWeek ?? (a.AllocationPercentage / 100m * grossCapacity));
            var available = netCapacity - confirmed;
            var utilization = netCapacity > 0 ? Math.Round(confirmed / netCapacity * 100, 1) : 0;

            return new ResourceAvailabilityDto
            {
                ResourceId = r.Id,
                ResourceName = r.Name,
                JobTitle = r.JobTitle,
                WeeklyCapacityHours = Math.Round(CalculateWeeklyCapacity(r, defaultCalendar), 1),
                CurrentUtilization = r.CurrentUtilization ?? utilization,
                AvailableCapacityHours = Math.Round(Math.Max(0, available), 1),
                EarliestAvailableDate = CalculateEarliestAvailableDate(r, allocs, startDate)
            };
        }).OrderByDescending(r => r.AvailableCapacityHours).ToList();

        return new PagedResult<ResourceAvailabilityDto>(items, 1, items.Count, total);
    }

    public async Task<Result<DemandVsCapacityDto>> GetDemandVsCapacityAsync(CapacityFilterDto filter, CancellationToken cancellationToken = default)
    {
        var startDate = filter.StartDate ?? new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        var endDate = filter.EndDate ?? startDate.AddMonths(1);

        var allocations = await _context.Set<ResourceAllocation>()
            .Include(a => a.Resource)
            .Where(a => a.Status != ResourceAllocation.StatusCancelled && !a.IsDeleted &&
                a.StartDate < endDate && a.EndDate > startDate)
            .ToListAsync(cancellationToken);

        var groups = new List<DemandVsCapacityGroupDto>();

        var roleGroups = allocations.GroupBy(a => a.Role);
        foreach (var group in roleGroups)
        {
            var demand = group.Sum(a => a.HoursPerWeek ?? CalculateHoursFromPercentage(a));
            groups.Add(new DemandVsCapacityGroupDto
            {
                GroupType = "Role",
                GroupName = group.Key,
                DemandHours = Math.Round(demand, 1),
                CapacityHours = Math.Round(demand * 0.85m, 1),
                GapHours = Math.Round(demand - demand * 0.85m, 1),
                Status = demand > 0 ? "Under" : "Balanced"
            });
        }

        var deptGroups = allocations.GroupBy(a => a.Resource?.DepartmentId);
        foreach (var group in deptGroups)
        {
            if (!group.Key.HasValue) continue;
            var deptName = await ResolveDepartmentName(group.Key, cancellationToken);
            var demand = group.Sum(a => a.HoursPerWeek ?? CalculateHoursFromPercentage(a));
            groups.Add(new DemandVsCapacityGroupDto
            {
                GroupType = "Department",
                GroupName = deptName ?? "Unknown",
                DemandHours = Math.Round(demand, 1),
                CapacityHours = Math.Round(demand * 0.85m, 1),
                GapHours = Math.Round(demand - demand * 0.85m, 1),
                Status = demand > 0 ? "Under" : "Balanced"
            });
        }

        var totalDemand = groups.Sum(g => g.DemandHours);
        var totalCapacity = groups.Sum(g => g.CapacityHours);
        return Result<DemandVsCapacityDto>.Success(new DemandVsCapacityDto
        {
            Groups = groups,
            Total = new DemandVsCapacityTotalDto
            {
                TotalDemandHours = Math.Round(totalDemand, 1),
                TotalCapacityHours = Math.Round(totalCapacity, 1),
                TotalGapHours = Math.Round(totalDemand - totalCapacity, 1)
            }
        });
    }

    public async Task<Result<List<AllocationDto>>> GetGanttDataAsync(CapacityFilterDto filter, CancellationToken cancellationToken = default)
    {
        var startDate = filter.StartDate ?? new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        var endDate = filter.EndDate ?? startDate.AddMonths(3);

        var query = _context.Set<ResourceAllocation>()
            .Include(a => a.Resource)
            .Where(a => a.Status != ResourceAllocation.StatusCancelled && !a.IsDeleted &&
                a.StartDate < endDate && a.EndDate > startDate);

        if (filter.ResourceId.HasValue)
            query = query.Where(a => a.ResourceId == filter.ResourceId.Value);

        if (!string.IsNullOrEmpty(filter.DepartmentId) && Guid.TryParse(filter.DepartmentId, out var deptId))
            query = query.Where(a => a.Resource!.DepartmentId == deptId);

        var items = await query.OrderBy(a => a.Resource!.Name).ThenBy(a => a.StartDate).ToListAsync(cancellationToken);

        return Result<List<AllocationDto>>.Success(items.Select(MapToDto).ToList());
    }

    public async Task<Result<ResourceAvailabilityDto>> GetResourceAvailabilityAsync(Guid resourceId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        var resource = await _context.Set<Resource>().FirstOrDefaultAsync(r => r.Id == resourceId && !r.IsDeleted, cancellationToken);
        if (resource is null) return Result<ResourceAvailabilityDto>.Failure(Error.NotFound("Resource not found"));

        var allocations = await _context.Set<ResourceAllocation>()
            .Where(a => a.ResourceId == resourceId && a.Status != ResourceAllocation.StatusCancelled && !a.IsDeleted &&
                a.StartDate < endDate && a.EndDate > startDate)
            .ToListAsync(cancellationToken);

        var calendars = await _context.Set<WorkingCalendar>().Include(c => c.Holidays).ToListAsync(cancellationToken);
        var defaultCalendar = calendars.FirstOrDefault(c => c.IsDefault) ?? new WorkingCalendar { HoursPerDay = 8, WorkingDaysPerWeek = 5 };

        var resourceCalendars = await _context.Set<ResourceCalendar>()
            .Where(rc => rc.ResourceId == resourceId && (rc.DayType == "Leave" || rc.DayType == "NonWorking"))
            .Where(rc => rc.Date >= startDate && rc.Date < endDate)
            .ToListAsync(cancellationToken);

        var activeContract = await _context.Set<Contract>()
            .FirstOrDefaultAsync(c => c.ResourceId == resourceId && c.Status == "Active", cancellationToken);

        var effectiveStart = startDate;
        var effectiveEnd = endDate;
        if (activeContract != null)
        {
            if (activeContract.StartDate > effectiveStart)
                effectiveStart = activeContract.StartDate;
            if (activeContract.EndDate.HasValue && activeContract.EndDate.Value < effectiveEnd)
                effectiveEnd = activeContract.EndDate.Value;
        }

        decimal grossCapacity, netCapacity;
        if (effectiveStart < effectiveEnd)
        {
            grossCapacity = CalculateGrossCapacity(resource, effectiveStart, effectiveEnd, defaultCalendar);
            var nonWorking = GetNonWorkingHours(effectiveStart, effectiveEnd, defaultCalendar, calendars);
            var leaveHours = resourceCalendars
                .Where(rc => rc.Date >= effectiveStart && rc.Date < effectiveEnd)
                .Sum(rc => rc.AvailableHours ?? defaultCalendar.HoursPerDay);
            netCapacity = grossCapacity - nonWorking - leaveHours;
        }
        else
        {
            grossCapacity = 0;
            netCapacity = 0;
        }

        var confirmed = allocations.Where(a => a.Status == ResourceAllocation.StatusConfirmed)
            .Sum(a => a.HoursPerWeek ?? (a.AllocationPercentage / 100m * grossCapacity));

        return Result<ResourceAvailabilityDto>.Success(new ResourceAvailabilityDto
        {
            ResourceId = resource.Id,
            ResourceName = resource.Name,
            JobTitle = resource.JobTitle,
            WeeklyCapacityHours = Math.Round(CalculateWeeklyCapacity(resource, defaultCalendar), 1),
            CurrentUtilization = netCapacity > 0 ? Math.Round(confirmed / netCapacity * 100, 1) : 0,
            AvailableCapacityHours = Math.Round(Math.Max(0, netCapacity - confirmed), 1),
            EarliestAvailableDate = CalculateEarliestAvailableDate(resource, allocations, startDate)
        });
    }

    private decimal CalculateGrossCapacity(Resource resource, DateTime start, DateTime end, WorkingCalendar calendar)
    {
        var days = (end - start).Days;
        var workingDays = days > 7
            ? (days / 7) * calendar.WorkingDaysPerWeek + Math.Min(days % 7, calendar.WorkingDaysPerWeek)
            : Math.Min(days, calendar.WorkingDaysPerWeek);
        return workingDays * calendar.HoursPerDay;
    }

    private decimal CalculateWeeklyCapacity(Resource resource, WorkingCalendar calendar)
    {
        return calendar.WorkingDaysPerWeek * calendar.HoursPerDay;
    }

    private decimal GetNonWorkingHours(DateTime start, DateTime end, WorkingCalendar defaultCalendar, List<WorkingCalendar> calendars)
    {
        var holidays = calendars.SelectMany(c => c.Holidays ?? new List<Holiday>())
            .Where(h => h.Date >= start && h.Date < end)
            .Select(h => h.Date)
            .Distinct()
            .Count();
        return holidays * defaultCalendar.HoursPerDay;
    }

    private DateTime? CalculateEarliestAvailableDate(Resource resource, List<ResourceAllocation> allocations, DateTime from)
    {
        var futureAllocs = allocations.Where(a => a.EndDate >= from)
            .OrderBy(a => a.EndDate)
            .ToList();

        if (futureAllocs.Count == 0) return from;

        var currentDate = from;
        foreach (var alloc in futureAllocs)
        {
            if (alloc.StartDate > currentDate) return currentDate;
            currentDate = alloc.EndDate > currentDate ? alloc.EndDate : currentDate;
        }
        return currentDate;
    }

    private static string GetHeatmapState(decimal utilization, decimal confirmed, decimal proposed, decimal gross)
    {
        if (gross == 0) return "Unavailable";
        if (confirmed + proposed > gross) return "Overallocated";
        if (utilization >= 100) return "FullyAllocated";
        if (utilization >= 80) return "NearCapacity";
        if (utilization >= 50) return "Balanced";
        if (utilization >= 20) return "Available";
        return "Available";
    }

    private static List<CapacityPeriodDto> GeneratePeriods(string periodType, DateTime start, DateTime end)
    {
        var periods = new List<CapacityPeriodDto>();
        var current = start;

        while (current < end)
        {
            DateTime periodEnd;
            string key, label;

            switch (periodType.ToLower())
            {
                case "month":
                    periodEnd = new DateTime(current.Year, current.Month, 1).AddMonths(1);
                    key = current.ToString("yyyy-MM");
                    label = current.ToString("MMM yyyy");
                    break;
                case "quarter":
                    var quarterStart = new DateTime(current.Year, ((current.Month - 1) / 3) * 3 + 1, 1);
                    periodEnd = quarterStart.AddMonths(3);
                    key = $"Q{(current.Month - 1) / 3 + 1}-{current.Year}";
                    label = key;
                    current = quarterStart;
                    break;
                default:
                    var weekStart = current.AddDays(-(int)current.DayOfWeek);
                    periodEnd = weekStart.AddDays(7);
                    key = weekStart.ToString("yyyy-MM-dd");
                    label = $"w{current:MMM dd}";
                    current = weekStart;
                    break;
            }

            periods.Add(new CapacityPeriodDto { Key = key, Label = label, Start = current, End = periodEnd < end ? periodEnd : end });
            current = periodEnd;
        }

        return periods;
    }

    private async Task<string?> ResolveDepartmentName(Guid? departmentId, CancellationToken cancellationToken)
    {
        if (!departmentId.HasValue) return null;
        try
        {
            var dept = await _context.Set<Department>().FirstOrDefaultAsync(d => d.Id == departmentId.Value, cancellationToken);
            return dept?.Name;
        }
        catch { return null; }
    }

    private static decimal CalculateHoursFromPercentage(ResourceAllocation a)
    {
        return a.AllocationPercentage / 100m * 40m;
    }

    private static AllocationDto MapToDto(ResourceAllocation a)
    {
        return new AllocationDto
        {
            Id = a.Id,
            ResourceId = a.ResourceId,
            ResourceName = a.Resource?.Name ?? string.Empty,
            ResourceJobTitle = a.Resource?.JobTitle,
            ResourceType = a.Resource?.Type,
            ProjectId = a.ProjectId,
            ProjectName = a.ProjectName,
            WorkItemId = a.WorkItemId,
            Role = a.Role,
            AllocationPercentage = a.AllocationPercentage,
            HoursPerWeek = a.HoursPerWeek,
            PlannedHours = a.PlannedHours,
            ActualHours = a.ActualHours,
            StartDate = a.StartDate,
            EndDate = a.EndDate,
            Status = a.Status,
            IsBillable = a.IsBillable,
            Notes = a.Notes,
            CreatedAt = a.CreatedAt,
            UpdatedAt = a.UpdatedAt
        };
    }
}
