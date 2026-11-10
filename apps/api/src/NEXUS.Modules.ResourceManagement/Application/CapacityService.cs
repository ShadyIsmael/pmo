using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Organization.Domain;
using NEXUS.Modules.ResourceManagement.Application.DTOs;
using NEXUS.Modules.ResourceManagement.Domain;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Management;

namespace NEXUS.Modules.ResourceManagement.Application;

public class CapacityService : ICapacityService
{
    private readonly DbContext _context;
    private readonly IHttpContextAccessor? _httpContextAccessor;
    private readonly IManagementContextAccessor? _managementContext;

    public CapacityService(
        DbContext context,
        IHttpContextAccessor? httpContextAccessor = null,
        IManagementContextAccessor? managementContext = null)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
        _managementContext = managementContext;
    }

    /// <summary>
    /// Cost rates are commercially sensitive. Capacity data itself is available with
    /// <c>Resource.View</c>, but the derived labour cost and hourly rate must only be
    /// returned to callers that hold a cost-rate permission.
    /// </summary>
    private bool CanViewCostRate()
    {
        var principal = _httpContextAccessor?.HttpContext?.User;
        if (principal?.Identity?.IsAuthenticated != true) return false;
        return principal.HasClaim("permission", "Resource.ViewCostRate");
    }

    private IQueryable<ResourceAllocation> ApplyManagementScope(IQueryable<ResourceAllocation> query)
    {
        var projectIds = _managementContext?.Current?.Scope.ProjectIds;
        if (projectIds is null) return query.Where(_ => false);
        if (projectIds.Count == 0) return query.Where(allocation => allocation.ProjectId == null);
        var scopedProjectIds = projectIds.ToArray();
        return query.Where(allocation => allocation.ProjectId == null || scopedProjectIds.Contains(allocation.ProjectId.Value));
    }

    public Task<Result<CapacityMatrixDto>> GetMatrixAsync(CapacityFilterDto filter, CancellationToken cancellationToken = default)
        => GetMatrixCoreAsync(filter, null, applyManagementScope: true, cancellationToken: cancellationToken);

    private async Task<Result<CapacityMatrixDto>> GetMatrixCoreAsync(
        CapacityFilterDto filter,
        IReadOnlyDictionary<Guid, ProjectTeamCapacityMemberInputDto>? teamMembers,
        bool applyManagementScope,
        CancellationToken cancellationToken)
    {
        var startDate = filter.StartDate ?? new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        var endDate = filter.EndDate ?? startDate.AddMonths(3);
        var periods = GeneratePeriods(filter.PeriodType, startDate, endDate);
        var canViewCostRate = CanViewCostRate();

        var resourcesQuery = _context.Set<Resource>().Where(r => !r.IsDeleted && r.IsActive);
        if (teamMembers is not null)
        {
            var resourceIds = teamMembers.Keys.ToArray();
            resourcesQuery = resourcesQuery.Where(resource => resourceIds.Contains(resource.Id));
        }
        if (!string.IsNullOrEmpty(filter.DepartmentId) && Guid.TryParse(filter.DepartmentId, out var deptId))
            resourcesQuery = resourcesQuery.Where(r => r.DepartmentId == deptId);
        if (!string.IsNullOrEmpty(filter.LocationId) && Guid.TryParse(filter.LocationId, out var locId))
            resourcesQuery = resourcesQuery.Where(r => r.LocationId == locId);
        if (!string.IsNullOrEmpty(filter.ResourceType))
        {
            var resourceType = ResourceTypeConstants.TryGetKnownStoredCode(filter.ResourceType, out var storedType)
                ? storedType
                : filter.ResourceType;
            resourcesQuery = resourcesQuery.Where(r => r.Type == resourceType);
        }
        if (filter.ResourceId.HasValue)
            resourcesQuery = resourcesQuery.Where(r => r.Id == filter.ResourceId.Value);

        var resources = await resourcesQuery.ToListAsync(cancellationToken);
        if (resources.Count == 0)
            return Result<CapacityMatrixDto>.Success(new CapacityMatrixDto { Periods = periods });

        var allocationsQuery = _context.Set<ResourceAllocation>()
            .Include(a => a.Resource)
            .Where(a => a.Status != ResourceAllocation.StatusCancelled && !a.IsDeleted &&
                a.StartDate < endDate && (a.EndDate > startDate || (a.AllocationSource == ResourceAllocation.SourceProjectPlan && a.EndDate >= startDate)));
        if (applyManagementScope)
            allocationsQuery = ApplyManagementScope(allocationsQuery);
        var allocations = await allocationsQuery.ToListAsync(cancellationToken);

        var calendars = await _context.Set<WorkingCalendar>()
            .Include(calendar => calendar.Holidays)
            .Include(calendar => calendar.DayRules)
            .ToListAsync(cancellationToken);
        var defaultCalendar = calendars.FirstOrDefault(c => c.IsDefault);

        var resourceCalendars = await _context.Set<ResourceCalendar>()
            .Where(rc => rc.Date >= startDate && rc.Date < endDate)
            .ToListAsync(cancellationToken);

        var contracts = await _context.Set<Contract>()
            .Where(c => c.Status == "Active" && c.StartDate < endDate && (!c.EndDate.HasValue || c.EndDate.Value >= startDate))
            .ToListAsync(cancellationToken);

        var costProfiles = await _context.Set<ResourceCostProfile>()
            .Where(cp => cp.EffectiveFrom <= endDate && (!cp.EffectiveTo.HasValue || cp.EffectiveTo.Value >= startDate))
            .ToListAsync(cancellationToken);

        var rows = new List<CapacityMatrixRowDto>();
        decimal totalGross = 0, totalNet = 0, totalConfirmed = 0, totalProposed = 0, totalPlanned = 0;

        foreach (var resource in resources)
        {
            var teamMember = teamMembers?.GetValueOrDefault(resource.Id);
            var resourceAllocs = allocations.Where(a => a.ResourceId == resource.Id
                && (teamMember?.ProjectId is null || a.ProjectId == teamMember.ProjectId)).ToList();
            var resourceCalendar = resource.BaseCalendarId.HasValue
                ? calendars.FirstOrDefault(calendar => calendar.Id == resource.BaseCalendarId.Value)
                : null;
            if (resourceCalendar?.HasValidSchedule != true) resourceCalendar = null;
            var projectCalendar = teamMember?.ProjectCalendarId is Guid projectCalendarId
                ? calendars.FirstOrDefault(calendar => calendar.Id == projectCalendarId)
                : null;
            if (projectCalendar?.HasValidSchedule != true) projectCalendar = null;
            if (defaultCalendar?.HasValidSchedule != true) defaultCalendar = null;
            var requestedCalendar = teamMember?.UseProjectCalendar == true
                ? projectCalendar ?? resourceCalendar ?? defaultCalendar
                : resourceCalendar ?? projectCalendar ?? defaultCalendar;
            var hasValidRequestedCalendar = requestedCalendar?.HasValidSchedule == true;
            var capacityCalendar = resourceCalendar ?? defaultCalendar ?? requestedCalendar;
            var hasValidMemberDedication = teamMember?.AllocationPercentage is >= 0m and <= 100m;
            decimal? teamBaseWeeklyCapacity = teamMember is null || !hasValidMemberDedication
                ? null
                : hasValidRequestedCalendar ? CalculateWeeklyCapacity(resource, capacityCalendar, requestedCalendar) : null;
            var hasWorkingCalendarData = teamMember is null
                ? hasValidRequestedCalendar && CalculateWeeklyCapacity(resource, capacityCalendar, requestedCalendar) > 0m
                : hasValidMemberDedication && teamBaseWeeklyCapacity is > 0m;
            var teamDedicationFactor = teamMember?.AllocationPercentage / 100m ?? 1m;
            var effectiveCalendar = hasValidRequestedCalendar ? requestedCalendar : null;
            var cells = new List<CapacityCellDto>();

            foreach (var period in periods)
            {
                var activeContract = contracts
                    .Where(contract => contract.ResourceId == resource.Id
                        && contract.StartDate < period.End
                        && (!contract.EndDate.HasValue || contract.EndDate.Value >= period.Start))
                    .OrderByDescending(contract => contract.StartDate)
                    .FirstOrDefault();
                var effectiveStart = period.Start;
                var effectiveEnd = period.End;
                if (teamMember is not null)
                {
                    var memberStart = teamMember.StartDate.Date;
                    var memberEnd = teamMember.EndDate.Date.AddDays(1);
                    if (memberStart > effectiveStart) effectiveStart = memberStart;
                    if (memberEnd < effectiveEnd) effectiveEnd = memberEnd;
                }
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
                    if (teamMember is not null)
                    {
                        if (teamBaseWeeklyCapacity.HasValue && hasValidMemberDedication)
                        {
                            var baseGrossHours = CalculateWeeklyCapacityOverRange(
                                resource,
                                effectiveStart,
                                effectiveEnd,
                                requestedCalendar,
                                capacityCalendar);
                            var baseNonWorkingHours = requestedCalendar is null
                                ? 0m
                                : GetNonWorkingHours(resource, effectiveStart, effectiveEnd, requestedCalendar, capacityCalendar);
                            var memberExceptions = resourceCalendars.Where(entry => entry.ResourceId == resource.Id
                                && entry.Date >= effectiveStart && entry.Date < effectiveEnd).ToList();
                            var calendarAdjustment = requestedCalendar is null
                                ? (AdjustmentHours: 0m, IsComplete: memberExceptions.Count == 0)
                                : CalculateResourceCalendarAdjustment(
                                    resource,
                                    effectiveStart,
                                    effectiveEnd,
                                    requestedCalendar,
                                    memberExceptions,
                                    capacityCalendar);
                            hasWorkingCalendarData &= calendarAdjustment.IsComplete;
                            var baseNetHours = Math.Max(
                                0m,
                                baseGrossHours - baseNonWorkingHours + calendarAdjustment.AdjustmentHours);
                            grossHours = baseGrossHours * teamDedicationFactor;
                            nonWorkingHours = baseNonWorkingHours * teamDedicationFactor;
                            netHours = baseNetHours * teamDedicationFactor;
                            leaveHours = Math.Max(0m, baseGrossHours - baseNonWorkingHours - baseNetHours)
                                * teamDedicationFactor;
                        }
                        else
                        {
                            grossHours = 0m;
                            nonWorkingHours = 0m;
                            netHours = 0m;
                            leaveHours = 0m;
                            hasWorkingCalendarData = false;
                        }
                    }
                    else
                    {
                        grossHours = CalculateGrossCapacity(resource, effectiveStart, effectiveEnd, effectiveCalendar, capacityCalendar);
                        nonWorkingHours = effectiveCalendar is null ? 0m : GetNonWorkingHours(resource, effectiveStart, effectiveEnd, effectiveCalendar, capacityCalendar);
                        var calendarAdjustment = CalculateResourceCalendarAdjustment(
                            resource,
                            effectiveStart,
                            effectiveEnd,
                            effectiveCalendar,
                            resourceCalendars.Where(entry => entry.ResourceId == resource.Id),
                            capacityCalendar);
                        hasWorkingCalendarData &= calendarAdjustment.IsComplete;
                        netHours = Math.Max(0, grossHours - nonWorkingHours + calendarAdjustment.AdjustmentHours);
                        leaveHours = Math.Max(0, grossHours - nonWorkingHours - netHours);
                    }
                }
                else
                {
                    grossHours = 0;
                    nonWorkingHours = 0;
                    leaveHours = 0;
                    netHours = 0;
                }

                var periodAllocs = resourceAllocs
                    .Where(allocation => allocation.StartDate < effectiveEnd
                        && (allocation.EndDate > effectiveStart
                            || (allocation.AllocationSource == ResourceAllocation.SourceProjectPlan && allocation.EndDate >= effectiveStart)))
                    .ToList();

                var allocationWeeklyCapacity = teamMember is null
                    ? null
                    : teamBaseWeeklyCapacity;
                var allocationHoursById = periodAllocs.ToDictionary(
                    allocation => allocation.Id,
                    allocation => CalculateAllocationHours(
                        allocation,
                        effectiveStart,
                        effectiveEnd,
                        resource,
                        effectiveCalendar,
                        allocationWeeklyCapacity,
                        capacityCalendar));

                var confirmedHours = periodAllocs
                    .Where(a => a.Status == ResourceAllocation.StatusConfirmed)
                    .Sum(a => allocationHoursById[a.Id]);

                var proposedHours = periodAllocs
                    .Where(a => a.Status == ResourceAllocation.StatusProposed || a.Status == ResourceAllocation.StatusTentative)
                    .Sum(a => allocationHoursById[a.Id]);

                var plannedHoursTotal = periodAllocs.Sum(a => a.PlannedHours ?? 0m);
                totalPlanned += plannedHoursTotal;

                var availableHours = netHours - confirmedHours - (filter.IncludeProposed ? proposedHours : 0);
                var utilizationPercent = netHours > 0 ? Math.Round((confirmedHours + proposedHours) / netHours * 100, 1) : 0;

                totalGross += grossHours;
                totalNet += netHours;
                totalConfirmed += confirmedHours;
                totalProposed += proposedHours;

                var overallocatedHours = Math.Max(0, confirmedHours + proposedHours - netHours);
                var resourceActiveCostProfile = canViewCostRate
                    ? costProfiles
                        .Where(cp => cp.ResourceId == resource.Id)
                        .OrderByDescending(cp => cp.EffectiveFrom)
                        .FirstOrDefault()
                    : null;
                var allocCurrency = resourceActiveCostProfile?.Currency;

                var allocCosts = new List<(Guid allocationId, decimal? plannedLaborCost, string? currency)>();
                foreach (var alloc in periodAllocs)
                {
                    var cost = canViewCostRate
                        ? await CalculatePlannedLaborCostAsync(alloc.ResourceId, alloc.PlannedHours ?? 0m, alloc.StartDate, alloc.EndDate, cancellationToken)
                        : 0m;
                    allocCosts.Add((alloc.Id, cost > 0 ? cost : null, allocCurrency));
                }

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
                    OverallocatedHours = Math.Round(overallocatedHours, 1),
                    Allocations = periodAllocs.Select(a =>
                    {
                        var costInfo = allocCosts.FirstOrDefault(c => c.allocationId == a.Id);
                        return new AllocationBreakdownDto
                        {
                            AllocationId = a.Id,
                            ProjectId = a.ProjectId,
                            ProjectName = a.ProjectName ?? "Unnamed",
                            Percentage = a.AllocationPercentage,
                            Hours = Math.Round(allocationHoursById[a.Id], 1),
                            PlannedHours = a.PlannedHours,
                            PlannedLaborCost = costInfo.plannedLaborCost,
                            Status = a.Status,
                            IsBillable = a.IsBillable,
                            Currency = costInfo.currency
                        };
                    }).ToList()
                });
            }

            var weeklyHours = CalculateWeeklyCapacity(resource, capacityCalendar, effectiveCalendar);
            var currentUtil = resource.CurrentUtilization ?? 0;
            var earliestDate = CalculateEarliestAvailableDate(resource, resourceAllocs, startDate);

            var activeCostProfile = canViewCostRate
                ? costProfiles
                    .Where(cp => cp.ResourceId == resource.Id)
                    .OrderByDescending(cp => cp.EffectiveFrom)
                    .FirstOrDefault()
                : null;

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
                HasCapacityData = hasWorkingCalendarData,
                Cells = cells,
                CostPerHour = activeCostProfile?.Amount,
                Currency = activeCostProfile?.Currency
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

    public async Task<Result<ProjectTeamCapacityOverviewDto>> GetProjectTeamOverviewAsync(
        IReadOnlyCollection<ProjectTeamCapacityMemberInputDto> members,
        CapacityFilterDto filter,
        CancellationToken cancellationToken = default)
    {
        var startDate = filter.StartDate?.Date ?? new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        var endDate = filter.EndDate?.Date ?? startDate.AddMonths(1);
        var normalizedPeriod = filter.PeriodType?.Trim();
        if (startDate >= endDate || (endDate - startDate).TotalDays > 93
            || !(string.Equals(normalizedPeriod, "Week", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizedPeriod, "Month", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalizedPeriod, "Quarter", StringComparison.OrdinalIgnoreCase)))
        {
            return Result<ProjectTeamCapacityOverviewDto>.Failure(
                Error.Validation("A valid week, month, or quarter reporting period is required."));
        }

        var activeMembers = members
            .Where(member => member.ResourceId != Guid.Empty
                && member.EndDate.Date >= startDate
                && member.StartDate.Date < endDate)
            .GroupBy(member => member.ResourceId)
            .Select(group => group.OrderBy(member => member.StartDate).ThenBy(member => member.EndDate).First())
            .ToDictionary(member => member.ResourceId);

        if (activeMembers.Count == 0)
        {
            return Result<ProjectTeamCapacityOverviewDto>.Success(new ProjectTeamCapacityOverviewDto
            {
                StartDate = startDate,
                EndDate = endDate,
                ActiveResourceCount = 0,
                IdleResourceCount = 0,
                TotalCapacityHours = 0m,
                UtilizedHours = 0m,
                AvailableHours = 0m,
                OverallocatedHours = 0m,
                UtilizedPercent = 0m,
                AvailablePercent = 0m,
                IsCapacityDataAvailable = true,
                IsAllocationDataAvailable = true
            });
        }

        var matrixResult = await GetMatrixCoreAsync(new CapacityFilterDto
        {
            PeriodType = normalizedPeriod!,
            StartDate = startDate,
            EndDate = endDate,
            IncludeProposed = true
        }, activeMembers, applyManagementScope: false, cancellationToken: cancellationToken);
        if (matrixResult.IsFailure || matrixResult.Value is null)
            return Result<ProjectTeamCapacityOverviewDto>.Failure(matrixResult.Error ?? Error.Validation("Capacity data is unavailable."));

        var rows = matrixResult.Value.Rows;
        var cells = rows.SelectMany(row => row.Cells).ToList();
        var allocationDataAvailable = cells
            .SelectMany(cell => cell.Allocations)
            .All(allocation => allocation.Status == ResourceAllocation.StatusConfirmed
                || allocation.Status == ResourceAllocation.StatusProposed
                || allocation.Status == ResourceAllocation.StatusTentative);
        var capacityDataAvailable = rows.Count == 0 || rows.All(row => row.HasCapacityData);
        var totalCapacity = rows.Sum(row => row.Cells.Sum(cell => cell.NetCapacityHours));
        var utilized = cells.Sum(cell => cell.ConfirmedHours + cell.ProposedHours);
        var available = cells.Sum(cell => cell.AvailableHours);
        var overallocated = cells.Sum(cell => cell.OverallocatedHours);
        if (overallocated == 0m)
            available = Math.Max(0m, totalCapacity - utilized);
        var metricsAvailable = capacityDataAvailable && allocationDataAvailable;

        return Result<ProjectTeamCapacityOverviewDto>.Success(new ProjectTeamCapacityOverviewDto
        {
            StartDate = startDate,
            EndDate = endDate,
            ActiveResourceCount = rows.Count,
            IdleResourceCount = allocationDataAvailable
                ? rows.Count(row => row.Cells.Sum(cell => cell.ConfirmedHours + cell.ProposedHours) == 0m)
                : null,
            TotalCapacityHours = capacityDataAvailable ? totalCapacity : null,
            UtilizedHours = metricsAvailable ? utilized : null,
            AvailableHours = metricsAvailable ? available : null,
            OverallocatedHours = metricsAvailable ? overallocated : null,
            UtilizedPercent = metricsAvailable && totalCapacity > 0m
                ? Math.Round(utilized / totalCapacity * 100m, 1)
                : metricsAvailable && utilized == 0m ? 0m : null,
            AvailablePercent = metricsAvailable && totalCapacity > 0m ? Math.Round(available / totalCapacity * 100m, 1) : metricsAvailable ? 0m : null,
            IsCapacityDataAvailable = capacityDataAvailable,
            IsAllocationDataAvailable = allocationDataAvailable
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

        var allAllocations = await ApplyManagementScope(_context.Set<ResourceAllocation>())
            .Where(a => a.Status != ResourceAllocation.StatusCancelled && !a.IsDeleted &&
                a.StartDate < endDate && (a.EndDate > startDate || (a.AllocationSource == ResourceAllocation.SourceProjectPlan && a.EndDate >= startDate)))
            .ToListAsync(cancellationToken);

        var calendars = await _context.Set<WorkingCalendar>()
            .Include(calendar => calendar.Holidays)
            .Include(calendar => calendar.DayRules)
            .ToListAsync(cancellationToken);
        var defaultCalendar = calendars.FirstOrDefault(calendar => calendar.IsDefault && calendar.HasValidSchedule);

        var resourceCalendars = await _context.Set<ResourceCalendar>()
            .Where(rc => rc.Date >= startDate && rc.Date < endDate && !rc.IsDeleted)
            .ToListAsync(cancellationToken);

        var contracts = await _context.Set<Contract>()
            .Where(c => c.Status == "Active")
            .ToListAsync(cancellationToken);

        var resourceCostProfiles = await _context.Set<ResourceCostProfile>()
            .Where(cp => cp.EffectiveFrom <= endDate && (!cp.EffectiveTo.HasValue || cp.EffectiveTo.Value >= startDate))
            .ToListAsync(cancellationToken);

        var items = resources.Select(r =>
        {
            var allocs = allAllocations.Where(a => a.ResourceId == r.Id).ToList();
            var selectedCalendar = r.BaseCalendarId.HasValue
                ? calendars.FirstOrDefault(c => c.Id == r.BaseCalendarId)
                : defaultCalendar;
            var resourceCalendar = selectedCalendar?.HasValidSchedule == true
                ? selectedCalendar
                : defaultCalendar;
            var capacityCalendar = resourceCalendar ?? defaultCalendar;

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
            var hasCapacityData = resourceCalendar is not null
                && CalculateWeeklyCapacity(r, capacityCalendar, resourceCalendar) > 0m;
            if (effectiveStart < effectiveEnd)
            {
                grossCapacity = CalculateGrossCapacity(r, effectiveStart, effectiveEnd, resourceCalendar, capacityCalendar);
                var nonWorking = resourceCalendar is null ? 0m : GetNonWorkingHours(r, effectiveStart, effectiveEnd, resourceCalendar, capacityCalendar);
                var calendarAdjustment = CalculateResourceCalendarAdjustment(
                    r,
                    effectiveStart,
                    effectiveEnd,
                    resourceCalendar,
                    resourceCalendars.Where(rc => rc.ResourceId == r.Id),
                    capacityCalendar);
                netCapacity = Math.Max(0m, grossCapacity - nonWorking + calendarAdjustment.AdjustmentHours);
                hasCapacityData &= calendarAdjustment.IsComplete;
            }
            else
            {
                grossCapacity = 0;
                netCapacity = 0;
            }

            var confirmed = allocs.Where(a => a.Status == ResourceAllocation.StatusConfirmed)
                .Sum(a => a.PlannedHours ?? a.HoursPerWeek ?? (a.AllocationPercentage / 100m * grossCapacity));
            var available = netCapacity - confirmed;
            var utilization = netCapacity > 0 ? Math.Round(confirmed / netCapacity * 100, 1) : 0;
            var activeCostProfile = CanViewCostRate()
                ? resourceCostProfiles.Where(cp => cp.ResourceId == r.Id).OrderByDescending(cp => cp.EffectiveFrom).FirstOrDefault()
                : null;

            return new ResourceAvailabilityDto
            {
                ResourceId = r.Id,
                ResourceName = r.Name,
                JobTitle = r.JobTitle,
                WeeklyCapacityHours = Math.Round(CalculateWeeklyCapacity(r, capacityCalendar, resourceCalendar), 1),
                HasCapacityData = hasCapacityData,
                CurrentUtilization = r.CurrentUtilization ?? utilization,
                AvailableCapacityHours = Math.Round(Math.Max(0, available), 1),
                EarliestAvailableDate = CalculateEarliestAvailableDate(r, allocs, startDate),
                CostPerHour = activeCostProfile?.Amount,
                Currency = activeCostProfile?.Currency
            };
        }).OrderByDescending(r => r.AvailableCapacityHours).ToList();

        return new PagedResult<ResourceAvailabilityDto>(items, 1, items.Count, total);
    }

    public async Task<Result<DemandVsCapacityDto>> GetDemandVsCapacityAsync(CapacityFilterDto filter, CancellationToken cancellationToken = default)
    {
        var startDate = filter.StartDate ?? new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        var endDate = filter.EndDate ?? startDate.AddMonths(1);

        var allocations = await ApplyManagementScope(_context.Set<ResourceAllocation>())
            .Include(a => a.Resource)
            .Where(a => a.Status != ResourceAllocation.StatusCancelled && !a.IsDeleted &&
                a.StartDate < endDate && (a.EndDate > startDate || (a.AllocationSource == ResourceAllocation.SourceProjectPlan && a.EndDate >= startDate)))
            .ToListAsync(cancellationToken);

        var groups = new List<DemandVsCapacityGroupDto>();

        // Group by ProjectRoleId instead of free-text Role
        var roleIds = allocations
            .Where(allocation => allocation.ProjectRoleId.HasValue)
            .Select(allocation => allocation.ProjectRoleId.GetValueOrDefault())
            .Distinct()
            .ToList();
        var roleNames = new Dictionary<Guid, string>();
        if (roleIds.Count > 0)
        {
            var lookups = await _context.Set<NEXUS.Modules.Organization.Domain.Lookup>()
                .AsNoTracking()
                .Where(l => roleIds.Contains(l.Id) && l.Category == "ProjectRole")
                .ToDictionaryAsync(l => l.Id, l => string.IsNullOrWhiteSpace(l.NameEn) ? l.Name : l.NameEn, cancellationToken);
            foreach (var kvp in lookups) roleNames[kvp.Key] = kvp.Value;
        }

        var projectRoleGroups = allocations
            .Where(allocation => allocation.ProjectRoleId.HasValue)
            .GroupBy(allocation => allocation.ProjectRoleId.GetValueOrDefault());
        foreach (var group in projectRoleGroups)
        {
            var demand = group.Sum(a => a.PlannedHours ?? a.HoursPerWeek ?? CalculateHoursFromPercentage(a));
            groups.Add(new DemandVsCapacityGroupDto
            {
                GroupType = "ProjectRole",
                GroupName = roleNames.TryGetValue(group.Key, out var name) ? name : group.Key.ToString(),
                DemandHours = Math.Round(demand, 1),
                CapacityHours = Math.Round(demand * 0.85m, 1),
                GapHours = Math.Round(demand - demand * 0.85m, 1),
                Status = demand > 0 ? "Under" : "Balanced"
            });
        }

        // Include allocations without ProjectRoleId as "Unassigned Role"
        var unassigned = allocations.Where(a => !a.ProjectRoleId.HasValue).ToList();
        if (unassigned.Count > 0)
        {
            var demand = unassigned.Sum(a => a.PlannedHours ?? a.HoursPerWeek ?? CalculateHoursFromPercentage(a));
            groups.Add(new DemandVsCapacityGroupDto
            {
                GroupType = "ProjectRole",
                GroupName = "Unassigned Role",
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
            var demand = group.Sum(a => a.PlannedHours ?? a.HoursPerWeek ?? CalculateHoursFromPercentage(a));
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

        var query = ApplyManagementScope(_context.Set<ResourceAllocation>())
            .Include(a => a.Resource)
            .Where(a => a.Status != ResourceAllocation.StatusCancelled && !a.IsDeleted &&
                a.StartDate < endDate && (a.EndDate > startDate || (a.AllocationSource == ResourceAllocation.SourceProjectPlan && a.EndDate >= startDate)));

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

        var allocations = await ApplyManagementScope(_context.Set<ResourceAllocation>())
            .Where(a => a.ResourceId == resourceId && a.Status != ResourceAllocation.StatusCancelled && !a.IsDeleted &&
                a.StartDate < endDate && (a.EndDate > startDate || (a.AllocationSource == ResourceAllocation.SourceProjectPlan && a.EndDate >= startDate)))
            .ToListAsync(cancellationToken);

        var calendars = await _context.Set<WorkingCalendar>()
            .Include(calendar => calendar.Holidays)
            .Include(calendar => calendar.DayRules)
            .ToListAsync(cancellationToken);
        var defaultCalendar = calendars.FirstOrDefault(calendar => calendar.IsDefault && calendar.HasValidSchedule);

        var resourceCalendars = await _context.Set<ResourceCalendar>()
            .Where(rc => rc.ResourceId == resourceId && !rc.IsDeleted)
            .Where(rc => rc.Date >= startDate && rc.Date < endDate)
            .ToListAsync(cancellationToken);

        var activeContract = await _context.Set<Contract>()
            .FirstOrDefaultAsync(c => c.ResourceId == resourceId && c.Status == "Active", cancellationToken);

        var selectedCalendar = resource.BaseCalendarId.HasValue
            ? calendars.FirstOrDefault(c => c.Id == resource.BaseCalendarId)
            : defaultCalendar;
        var resourceCalendar = selectedCalendar?.HasValidSchedule == true
            ? selectedCalendar
            : calendars.FirstOrDefault(calendar => calendar.IsDefault && calendar.HasValidSchedule);
        var capacityCalendar = resourceCalendar;

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
        var hasCapacityData = resourceCalendar is not null
            && CalculateWeeklyCapacity(resource, capacityCalendar, resourceCalendar) > 0m;
        if (effectiveStart < effectiveEnd)
        {
            grossCapacity = CalculateGrossCapacity(resource, effectiveStart, effectiveEnd, resourceCalendar, capacityCalendar);
            var nonWorking = resourceCalendar is null ? 0m : GetNonWorkingHours(resource, effectiveStart, effectiveEnd, resourceCalendar, capacityCalendar);
            var calendarAdjustment = CalculateResourceCalendarAdjustment(
                resource,
                effectiveStart,
                effectiveEnd,
                resourceCalendar,
                resourceCalendars,
                capacityCalendar);
            netCapacity = Math.Max(0m, grossCapacity - nonWorking + calendarAdjustment.AdjustmentHours);
            hasCapacityData &= calendarAdjustment.IsComplete;
        }
        else
        {
            grossCapacity = 0;
            netCapacity = 0;
        }

        var confirmed = allocations.Where(a => a.Status == ResourceAllocation.StatusConfirmed)
            .Sum(a => a.PlannedHours ?? a.HoursPerWeek ?? (a.AllocationPercentage / 100m * grossCapacity));

        var activeCostProfile = CanViewCostRate()
            ? await _context.Set<ResourceCostProfile>()
                .Where(cp => cp.ResourceId == resourceId && cp.EffectiveFrom <= endDate && (!cp.EffectiveTo.HasValue || cp.EffectiveTo.Value >= startDate))
                .OrderByDescending(cp => cp.EffectiveFrom)
                .FirstOrDefaultAsync(cancellationToken)
            : null;

        return Result<ResourceAvailabilityDto>.Success(new ResourceAvailabilityDto
        {
            ResourceId = resource.Id,
            ResourceName = resource.Name,
            JobTitle = resource.JobTitle,
            WeeklyCapacityHours = Math.Round(CalculateWeeklyCapacity(resource, capacityCalendar, resourceCalendar), 1),
            HasCapacityData = hasCapacityData,
            CurrentUtilization = netCapacity > 0 ? Math.Round(confirmed / netCapacity * 100, 1) : 0,
            AvailableCapacityHours = Math.Round(Math.Max(0, netCapacity - confirmed), 1),
            EarliestAvailableDate = CalculateEarliestAvailableDate(resource, allocations, startDate),
            CostPerHour = activeCostProfile?.Amount,
            Currency = activeCostProfile?.Currency
        });
    }

    private static decimal CalculateGrossCapacity(
        Resource resource,
        DateTime start,
        DateTime end,
        WorkingCalendar? scheduleCalendar,
        WorkingCalendar? capacityCalendar)
    {
        if (scheduleCalendar?.HasValidSchedule != true || end.Date <= start.Date) return 0m;
        var capacity = 0m;
        for (var date = start.Date; date < end.Date; date = date.AddDays(1))
            capacity += ResourceWorkingCapacityCalculator.GetScheduledDailyCapacity(
                resource, capacityCalendar, scheduleCalendar, date);
        return capacity;
    }

    private static decimal CalculateWeeklyCapacity(
        Resource resource,
        WorkingCalendar? capacityCalendar,
        WorkingCalendar? scheduleCalendar) =>
        ResourceWorkingCapacityCalculator.GetWeeklyCapacity(resource, capacityCalendar, scheduleCalendar);

    private decimal GetNonWorkingHours(
        Resource resource,
        DateTime start,
        DateTime end,
        WorkingCalendar calendar,
        WorkingCalendar? capacityCalendar)
    {
        var holidayCapacity = 0m;
        for (var date = start.Date; date < end.Date; date = date.AddDays(1))
        {
            if (calendar.IsHoliday(date))
                holidayCapacity += ResourceWorkingCapacityCalculator.GetScheduledDailyCapacity(
                    resource, capacityCalendar, calendar, date);
        }
        return holidayCapacity;
    }

    private static decimal CalculateWeeklyCapacityOverRange(
        Resource resource,
        DateTime start,
        DateTime end,
        WorkingCalendar? scheduleCalendar,
        WorkingCalendar? capacityCalendar)
    {
        return CalculateGrossCapacity(resource, start, end, scheduleCalendar, capacityCalendar);
    }

    private static decimal CalculateAllocationHours(
        ResourceAllocation allocation,
        DateTime periodStart,
        DateTime periodEnd,
        Resource resource,
        WorkingCalendar? calendar,
        decimal? weeklyCapacityOverride = null,
        WorkingCalendar? capacityCalendar = null)
    {
        if (calendar?.HasValidSchedule != true && !weeklyCapacityOverride.HasValue) return 0m;
        var allocationEndExclusive = allocation.AllocationSource == ResourceAllocation.SourceProjectPlan
            ? allocation.EndDate.Date.AddDays(1)
            : allocation.EndDate;
        var allocationStart = allocation.StartDate > periodStart ? allocation.StartDate : periodStart;
        var allocationEnd = allocationEndExclusive < periodEnd ? allocationEndExclusive : periodEnd;
        if (allocationStart >= allocationEnd) return 0m;

        var overlappingCapacity = weeklyCapacityOverride.HasValue
            ? CalculateWeeklyCapacityOverRange(resource, allocationStart, allocationEnd, calendar, capacityCalendar ?? calendar)
            : CalculateGrossCapacity(resource, allocationStart, allocationEnd, calendar, capacityCalendar ?? calendar);
        if (allocation.PlannedHours.HasValue)
        {
            var fullAllocationCapacity = weeklyCapacityOverride.HasValue
                ? CalculateWeeklyCapacityOverRange(resource, allocation.StartDate, allocationEndExclusive, calendar, capacityCalendar ?? calendar)
                : CalculateGrossCapacity(resource, allocation.StartDate, allocationEndExclusive, calendar, capacityCalendar ?? calendar);
            return fullAllocationCapacity > 0m
                ? allocation.PlannedHours.Value * Math.Min(1m, overlappingCapacity / fullAllocationCapacity)
                : 0m;
        }

        if (allocation.HoursPerWeek.HasValue)
        {
            var weeklyCapacity = weeklyCapacityOverride ?? CalculateWeeklyCapacity(resource, capacityCalendar ?? calendar, calendar);
            return weeklyCapacity > 0m ? allocation.HoursPerWeek.Value * overlappingCapacity / weeklyCapacity : 0m;
        }

        return allocation.AllocationPercentage / 100m * overlappingCapacity;
    }

    private static (decimal AdjustmentHours, bool IsComplete) CalculateResourceCalendarAdjustment(
        Resource resource,
        DateTime start,
        DateTime end,
        WorkingCalendar? calendar,
        IEnumerable<ResourceCalendar> resourceCalendarEntries,
        WorkingCalendar? capacityCalendar)
    {
        var entriesByDate = resourceCalendarEntries
            .Where(entry => entry.Date >= start && entry.Date < end)
            .GroupBy(entry => entry.Date.Date)
            .Select(group => group.OrderByDescending(entry => entry.UpdatedAt ?? entry.CreatedAt).First());
        var adjustment = 0m;
        var complete = true;

        foreach (var entry in entriesByDate)
        {
            var dayType = (entry.DayType ?? string.Empty).Trim();
            var isWorking = string.Equals(dayType, "WorkingDay", StringComparison.OrdinalIgnoreCase);
            var isUnavailable = ResourceWorkingCapacityCalculator.IsUnavailableDayType(dayType);

            if (!isWorking && !isUnavailable)
            {
                complete = false;
                continue;
            }

            if (entry.AvailableHours is < 0m or > 24m)
            {
                complete = false;
                continue;
            }

            var baselineHours = calendar is null || calendar.IsHoliday(entry.Date)
                ? 0m
                : ResourceWorkingCapacityCalculator.GetScheduledDailyCapacity(resource, capacityCalendar, calendar, entry.Date);
            var adjustedHours = calendar is null
                ? 0m
                : ResourceWorkingCapacityCalculator.GetAvailableDailyCapacity(resource, capacityCalendar, calendar, entry.Date, entry);
            adjustment += adjustedHours - baselineHours;
        }

        return (adjustment, complete);
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
        if (confirmed + proposed - gross > 0.05m) return "Overallocated";
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
            ResourceType = a.Resource is null ? null : ResourceTypeConstants.ToApiCode(a.Resource.Type),
            ProjectId = a.ProjectId,
            ProjectName = a.ProjectName,
            WorkItemId = a.WorkItemId,
            ProjectRoleId = a.ProjectRoleId,
            AllocationPercentage = a.AllocationPercentage,
            HoursPerWeek = a.HoursPerWeek,
            PlannedHours = a.PlannedHours,
            PlannedLaborCost = null,
            StartDate = a.StartDate,
            EndDate = a.EndDate,
            Status = a.Status,
            IsBillable = a.IsBillable,
            Notes = a.Notes,
            CreatedAt = a.CreatedAt,
            UpdatedAt = a.UpdatedAt
        };
    }

    private async Task<ResourceCostProfile?> GetEffectiveCostProfileAsync(Guid resourceId, DateTime date, CancellationToken cancellationToken)
    {
        return await _context.Set<ResourceCostProfile>()
            .Where(cp => cp.ResourceId == resourceId
                && cp.EffectiveFrom <= date
                && (!cp.EffectiveTo.HasValue || cp.EffectiveTo.Value >= date)
                && !cp.IsDeleted)
            .OrderByDescending(cp => cp.EffectiveFrom)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<decimal> CalculatePlannedLaborCostAsync(
        Guid resourceId, decimal plannedHours, DateTime startDate, DateTime endDate, CancellationToken cancellationToken)
    {
        if (plannedHours <= 0) return 0m;

        // Period-aware cost profile resolution: split across profile boundaries
        var totalCost = 0m;
        var currentDate = startDate;

        while (currentDate < endDate)
        {
            var profile = await GetEffectiveCostProfileAsync(resourceId, currentDate, cancellationToken);
            if (profile is null)
            {
                currentDate = currentDate.AddDays(1);
                continue;
            }

            var periodEnd = profile.EffectiveTo.HasValue
                ? profile.EffectiveTo.Value.AddDays(1)
                : endDate;
            var effectiveEnd = periodEnd > endDate ? endDate : periodEnd;

            if (profile.CompensationType == CompensationType.Hourly)
            {
                totalCost += plannedHours * profile.Amount;
            }
            else if (profile.CompensationType == CompensationType.Weekly)
            {
                var productiveHoursPerWeek = profile.EffectiveFrom.DayOfWeek == DayOfWeek.Saturday
                    ? 5m * profile.Amount
                    : profile.Amount;
                var weeksInPeriod = Math.Max(1m, (effectiveEnd - currentDate).Days / 7m);
                totalCost += productiveHoursPerWeek * weeksInPeriod / 5m * profile.Amount / 5m;
            }
            else if (profile.CompensationType == CompensationType.Monthly)
            {
                var productiveHoursPerMonth = 22m * profile.Amount;
                var daysInMonth = (effectiveEnd - currentDate).Days;
                totalCost += (daysInMonth / 30m) * profile.Amount;
            }

            currentDate = effectiveEnd;
        }

        return Math.Round(totalCost, 2);
    }

    private async Task<decimal> CalculateEffectiveHourlyCostAsync(
        Guid resourceId, DateTime date, CancellationToken cancellationToken)
    {
        var profile = await GetEffectiveCostProfileAsync(resourceId, date, cancellationToken);
        if (profile is null) return 0m;

        if (profile.CompensationType == CompensationType.Hourly)
        {
            return profile.Amount;
        }
        else if (profile.CompensationType == CompensationType.Weekly)
        {
            return profile.Amount / 5m / 8m;
        }
        else if (profile.CompensationType == CompensationType.Monthly)
        {
            return profile.Amount / 22m / 8m;
        }

        return 0m;
    }
}
