using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Organization.Domain;
using NEXUS.Modules.ResourceManagement.Application.DTOs;
using NEXUS.Modules.ResourceManagement.Domain;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Management;

namespace NEXUS.Modules.ResourceManagement.Application;

public sealed class CostUtilizationAnalyticsService : ICostUtilizationAnalyticsService
{
    private const int MaxReportDays = 366;
    private readonly DbContext _context;
    private readonly IActualWorkCostLookup _actualWork;
    private readonly IPlannedWorkCostLookup _plannedWork;
    private readonly IProjectReferenceLookup _projects;
    private readonly IManagementContextAccessor _managementContext;
    private readonly IManagementScopeResolver _scopeResolver;
    private readonly IHttpContextAccessor _http;
    private readonly ResourceLaborCostCalculator _calculator;

    public CostUtilizationAnalyticsService(
        DbContext context,
        IActualWorkCostLookup actualWork,
        IPlannedWorkCostLookup plannedWork,
        IProjectReferenceLookup projects,
        IManagementContextAccessor managementContext,
        IManagementScopeResolver scopeResolver,
        IHttpContextAccessor http,
        ResourceLaborCostCalculator calculator)
    {
        _context = context;
        _actualWork = actualWork;
        _plannedWork = plannedWork;
        _projects = projects;
        _managementContext = managementContext;
        _scopeResolver = scopeResolver;
        _http = http;
        _calculator = calculator;
    }

    public async Task<Result<CostUtilizationReportDto>> GetAsync(
        CostUtilizationFilterDto filter,
        Guid actorId,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var from = (filter.From ?? DateTime.UtcNow.Date.AddDays(-29)).Date;
        var to = (filter.To ?? DateTime.UtcNow.Date).Date;
        if (to < from) return Result<CostUtilizationReportDto>.Failure(Error.Validation("The analytics end date must be on or after the start date"));
        if ((to - from).TotalDays + 1 > MaxReportDays)
            return Result<CostUtilizationReportDto>.Failure(Error.Validation($"The analytics period cannot exceed {MaxReportDays} days"));

        var managementScope = _managementContext.Current?.Scope;
        if (filter.ProjectId.HasValue && managementScope is not null && managementScope.ProjectIds.Count > 0 && !managementScope.ProjectIds.Contains(filter.ProjectId.Value))
        {
            if (!await _scopeResolver.CanAccessProjectAsync(actorId, tenantId, filter.ProjectId.Value, cancellationToken))
                return Result<CostUtilizationReportDto>.Failure(Error.Forbidden("The Project is outside your management scope"));
        }

        IReadOnlyCollection<Guid>? scopedProjectIds = filter.ProjectId.HasValue
            ? new[] { filter.ProjectId.Value }
            : managementScope?.ProjectIds.Count > 0 ? managementScope.ProjectIds.ToArray() : null;

        var actualSlices = await _actualWork.GetAsync(
            tenantId, from, to, projectIds: scopedProjectIds, projectRoleId: filter.ProjectRoleId, cancellationToken: cancellationToken);
        actualSlices = actualSlices
            .Where(slice => (!filter.PlanActivityId.HasValue || slice.PlanActivityId == filter.PlanActivityId)
                && (!filter.WorkItemId.HasValue || slice.WorkItemId == filter.WorkItemId))
            .ToList();

        var plannedAssignments = await _plannedWork.GetPlannedAsync(
            tenantId, from, to, projectIds: scopedProjectIds, projectRoleId: filter.ProjectRoleId, cancellationToken: cancellationToken);

        var allocationQuery = _context.Set<ResourceAllocation>()
            .AsNoTracking()
            .Where(allocation => allocation.TenantId == tenantId
                && !allocation.IsDeleted
                && allocation.Status != ResourceAllocation.StatusCancelled
                && allocation.StartDate.Date <= to
                && allocation.EndDate.Date >= from);
        if (filter.ResourceId.HasValue) allocationQuery = allocationQuery.Where(allocation => allocation.ResourceId == filter.ResourceId.Value);
        if (filter.VendorId.HasValue) allocationQuery = allocationQuery.Where(allocation => allocation.Resource.VendorId == filter.VendorId.Value);
        if (filter.ProjectId.HasValue) allocationQuery = allocationQuery.Where(allocation => allocation.ProjectId == filter.ProjectId.Value);
        if (scopedProjectIds is { Count: > 0 }) allocationQuery = allocationQuery.Where(allocation => allocation.ProjectId.HasValue && scopedProjectIds.Contains(allocation.ProjectId.Value));
        if (filter.ProjectRoleId.HasValue) allocationQuery = allocationQuery.Where(allocation => allocation.ProjectRoleId == filter.ProjectRoleId.Value);
        // A PlanActivity or WorkItem filter cannot be represented by a
        // ResourceAllocation row without inventing attribution.
        if (filter.PlanActivityId.HasValue || filter.WorkItemId.HasValue) allocationQuery = allocationQuery.Where(_ => false);
        var allocations = await allocationQuery.ToListAsync(cancellationToken);

        var resourceIds = allocations.Select(item => item.ResourceId)
            .Concat(actualSlices.Select(item => item.ResourceId))
            .Concat(plannedAssignments.Select(item => item.ResourceId))
            .Distinct()
            .ToHashSet();

        var resourceQuery = _context.Set<Resource>()
            .AsNoTracking()
            .Where(resource => resource.TenantId == tenantId && !resource.IsDeleted && resource.IsActive);
        if (filter.ResourceId.HasValue) resourceQuery = resourceQuery.Where(resource => resource.Id == filter.ResourceId.Value);
        if (filter.VendorId.HasValue) resourceQuery = resourceQuery.Where(resource => resource.VendorId == filter.VendorId.Value);
        if (filter.DepartmentId.HasValue) resourceQuery = resourceQuery.Where(resource => resource.DepartmentId == filter.DepartmentId.Value);

        if (managementScope is not null && managementScope.ProjectIds.Count > 0)
        {
            var ownResourceIds = await resourceQuery.Where(resource => resource.UserId == actorId).Select(resource => resource.Id).ToListAsync(cancellationToken);
            resourceIds.UnionWith(ownResourceIds);
            if (resourceIds.Count == 0) return Result<CostUtilizationReportDto>.Success(EmptyReport(from, to, CanViewCosts()));
            resourceQuery = resourceQuery.Where(resource => resourceIds.Contains(resource.Id));
        }

        var resources = await resourceQuery.OrderBy(resource => resource.Name).ToListAsync(cancellationToken);
        if (resources.Count == 0) return Result<CostUtilizationReportDto>.Success(EmptyReport(from, to, CanViewCosts()));

        var resourceById = resources.ToDictionary(resource => resource.Id);

        // Fetch department names for resources
        var departmentIds = resources
            .Where(resource => resource.DepartmentId.HasValue)
            .Select(resource => resource.DepartmentId.GetValueOrDefault())
            .Distinct()
            .ToList();
        var departmentNames = new Dictionary<Guid, string>();
        if (departmentIds.Count > 0)
        {
            var departments = await _context.Set<NEXUS.Modules.Organization.Domain.Department>()
                .AsNoTracking()
                .Where(d => departmentIds.Contains(d.Id))
                .ToDictionaryAsync(d => d.Id, d => d.Name, cancellationToken);
            foreach (var kvp in departments) departmentNames[kvp.Key] = kvp.Value;
        }
        actualSlices = actualSlices.Where(slice => resourceById.ContainsKey(slice.ResourceId)).ToList();
        plannedAssignments = plannedAssignments.Where(slice => resourceById.ContainsKey(slice.ResourceId)).ToList();
        allocations = allocations.Where(allocation => resourceById.ContainsKey(allocation.ResourceId)).ToList();

        var calendars = await _context.Set<WorkingCalendar>()
            .AsNoTracking()
            .Include(calendar => calendar.Holidays)
            .Where(calendar => calendar.TenantId == tenantId && !calendar.IsDeleted)
            .ToListAsync(cancellationToken);
        var defaultCalendar = calendars.FirstOrDefault(calendar => calendar.IsDefault)
            ?? calendars.FirstOrDefault()
            ?? new WorkingCalendar();
        var calendarById = calendars.ToDictionary(calendar => calendar.Id);

        var resourceCalendars = await _context.Set<ResourceCalendar>()
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && !item.IsDeleted && resourceById.Keys.Contains(item.ResourceId) && item.Date >= from && item.Date <= to)
            .ToListAsync(cancellationToken);
        var contracts = await _context.Set<Contract>()
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && !item.IsDeleted && resourceById.Keys.Contains(item.ResourceId) && item.Status == "Active")
            .ToListAsync(cancellationToken);
        var profiles = await _context.Set<ResourceCostProfile>()
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && !item.IsDeleted && resourceById.Keys.Contains(item.ResourceId)
                && item.EffectiveFrom.Date <= to
                && (!item.EffectiveTo.HasValue || item.EffectiveTo.Value.Date >= from))
            .OrderBy(item => item.ResourceId)
            .ThenBy(item => item.EffectiveFrom)
            .ToListAsync(cancellationToken);

        var profilesByResource = profiles.GroupBy(profile => profile.ResourceId).ToDictionary(group => group.Key, group => group.ToList());
        var exceptionsByResourceDate = resourceCalendars
            .GroupBy(item => (item.ResourceId, Date: item.Date.Date))
            .ToDictionary(group => group.Key, group => group.ToList());
        var contractsByResource = contracts.GroupBy(item => item.ResourceId).ToDictionary(group => group.Key, group => group.ToList());

        var days = resources.SelectMany(resource => Enumerable.Range(0, (to - from).Days + 1)
            .Select(offset =>
            {
                var date = from.AddDays(offset).Date;
                var calendar = resource.BaseCalendarId.HasValue && calendarById.TryGetValue(resource.BaseCalendarId.Value, out var selectedCalendar)
                    ? selectedCalendar
                    : defaultCalendar;
                return new DayState
                {
                    Resource = resource,
                    WorkDate = date,
                    CapacityHours = CalculateCapacity(resource, date, calendar, exceptionsByResourceDate, contractsByResource),
                    Profile = ResolveProfile(resource.Id, date, profilesByResource, out var overlapMessage),
                    ProfileOverlapMessage = overlapMessage
                };
            })).ToList();

        var overlap = days.FirstOrDefault(day => day.ProfileOverlapMessage is not null);
        if (overlap is not null)
            return Result<CostUtilizationReportDto>.Failure(Error.Conflict(overlap.ProfileOverlapMessage!));

        var daysByKey = days.ToDictionary(day => (day.Resource.Id, day.WorkDate));
        foreach (var allocation in allocations)
        {
            AddPlannedHours(
                daysByKey,
                allocation.ResourceId,
                allocation.ProjectId,
                allocation.ProjectRoleId,
                null,
                allocation.StartDate,
                allocation.EndDate,
                ResolveAllocationHours(allocation, daysByKey, from, to),
                from,
                to);
        }

        foreach (var assignment in plannedAssignments)
        {
            if (filter.PlanActivityId.HasValue && assignment.PlanActivityId != filter.PlanActivityId.Value) continue;
            AddPlannedHours(
                daysByKey,
                assignment.ResourceId,
                assignment.ProjectId,
                assignment.ProjectRoleId,
                assignment.PlanActivityId,
                assignment.StartDate ?? from,
                assignment.EndDate ?? to,
                assignment.PlannedHours,
                from,
                to);
        }

        var filteredActual = actualSlices.Where(slice =>
            (!filter.ResourceId.HasValue || slice.ResourceId == filter.ResourceId.Value)
            && (!filter.ProjectId.HasValue || slice.ProjectId == filter.ProjectId.Value)
            && (!filter.ProjectRoleId.HasValue || slice.ProjectRoleId == filter.ProjectRoleId.Value)).ToList();
        foreach (var group in filteredActual.GroupBy(slice => (slice.ResourceId, Date: slice.WorkDate.Date)))
        {
            if (daysByKey.TryGetValue(group.Key, out var day)) day.ActualEntries.AddRange(group);
        }

        var projectIds = allocations.Where(item => item.ProjectId.HasValue).Select(item => item.ProjectId!.Value)
            .Concat(plannedAssignments.Select(item => item.ProjectId))
            .Concat(filteredActual.Select(item => item.ProjectId))
            .Distinct()
            .ToList();
        var projectNames = projectIds.Count == 0
            ? new Dictionary<Guid, string>()
            : (await _projects.GetNamesAsync(projectIds, cancellationToken)).ToDictionary(pair => pair.Key, pair => pair.Value);
        var projectReferences = projectIds.Count == 0
            ? new Dictionary<Guid, ProjectReference>()
            : await _projects.GetReferencesAsync(projectIds, cancellationToken);
        var vendorIds = resources.Where(resource => resource.VendorId.HasValue).Select(resource => resource.VendorId!.Value).Distinct().ToList();
        var vendorNames = vendorIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await _context.Set<Vendor>().AsNoTracking().Where(vendor => vendorIds.Contains(vendor.Id) && !vendor.IsDeleted).ToDictionaryAsync(vendor => vendor.Id, vendor => vendor.Name, cancellationToken);
        var roleIds = allocations.Where(item => item.ProjectRoleId.HasValue).Select(item => item.ProjectRoleId!.Value)
            .Concat(plannedAssignments.Where(item => item.ProjectRoleId.HasValue).Select(item => item.ProjectRoleId!.Value))
            .Concat(filteredActual.Where(item => item.ProjectRoleId.HasValue).Select(item => item.ProjectRoleId!.Value))
            .Distinct()
            .ToList();
        var roleNames = roleIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await _context.Set<Lookup>().AsNoTracking().Where(role => roleIds.Contains(role.Id) && role.Category == "ProjectRole")
                .ToDictionaryAsync(role => role.Id, role => string.IsNullOrWhiteSpace(role.NameEn) ? role.Name : role.NameEn, cancellationToken);

        var operational = new MetricAccumulator();
        var currencyMetrics = new Dictionary<string, MetricAccumulator>(StringComparer.OrdinalIgnoreCase);
        var resourceMetrics = new Dictionary<(Guid ResourceId, string Currency), MetricAccumulator>();
        var projectMetrics = new Dictionary<(Guid? Id, string Currency), AttributionAccumulator>();
        var vendorMetrics = new Dictionary<(Guid? Id, string Currency), MetricAccumulator>();
        var roleMetrics = new Dictionary<(Guid? Id, string Currency), AttributionAccumulator>();
        var daily = new List<CostUtilizationDailyDto>();
        var warnings = new List<CostUtilizationWarningDto>();
        var warnedResources = new HashSet<Guid>();

        foreach (var day in days)
        {
            var orderedEntries = day.ActualEntries
                .Where(entry => entry.ActualSeconds >= 0)
                .OrderBy(entry => entry.StartTime)
                .ThenBy(entry => entry.TimeEntryId)
                .ToList();
            var remainingCapacity = Math.Max(0m, day.CapacityHours);
            var regularHours = 0m;
            var excessHours = 0m;
            foreach (var entry in orderedEntries)
            {
                var actualHours = Math.Max(0m, entry.ActualSeconds / 3600m);
                var regular = Math.Min(actualHours, remainingCapacity);
                var excess = actualHours - regular;
                remainingCapacity -= regular;
                regularHours += regular;
                excessHours += excess;
            }

            var fullCompensationCapacity = GetCompensationPeriodCapacity(day, calendarById, defaultCalendar, exceptionsByResourceDate, contractsByResource);
            var dayResult = _calculator.Calculate(new ResourceDayCostInput(
                day.Profile,
                day.CapacityHours,
                day.PlannedHours,
                regularHours,
                excessHours,
                fullCompensationCapacity));
            day.Result = dayResult;
            operational.Add(dayResult);

            var metricCurrency = dayResult.Currency ?? string.Empty;
            if (dayResult.Currency is not null)
                GetOrCreate(currencyMetrics, dayResult.Currency).Add(dayResult);
            var resourceKey = (day.Resource.Id, metricCurrency);
            GetOrCreate(resourceMetrics, resourceKey).Add(dayResult);
            GetOrCreate(vendorMetrics, (day.Resource.VendorId, metricCurrency)).Add(dayResult);

            if (dayResult.CostProfileMissing && (dayResult.ActualHours > 0m || dayResult.PlannedHours > 0m))
            {
                if (warnedResources.Add(day.Resource.Id))
                {
                    var hasAnyProfile = profilesByResource.ContainsKey(day.Resource.Id);
                    warnings.Add(new CostUtilizationWarningDto
                    {
                        Code = hasAnyProfile ? "COST_PROFILE_PARTIAL" : "COST_PROFILE_MISSING",
                        Message = hasAnyProfile
                            ? $"ResourceCostProfile coverage is partial for work in the selected period for {day.Resource.Name}."
                            : $"No effective ResourceCostProfile covers work in the selected period for {day.Resource.Name}.",
                        ResourceId = day.Resource.Id,
                        ResourceName = day.Resource.Name
                    });
                }
            }

            foreach (var line in day.PlannedLines)
            {
                var plannedCost = dayResult.BaseEffectiveHourlyCost.HasValue ? line.Hours * dayResult.BaseEffectiveHourlyCost.Value : (decimal?)null;
                AddAttribution(projectMetrics, (line.ProjectId, dayResult.Currency ?? string.Empty), line.Hours, 0m, 0m, 0m, 0m, plannedCost, null, null, dayResult.CostProfileMissing && line.Hours > 0m);
                AddAttribution(roleMetrics, (line.ProjectRoleId, dayResult.Currency ?? string.Empty), line.Hours, 0m, 0m, 0m, 0m, plannedCost, null, null, dayResult.CostProfileMissing && line.Hours > 0m);
            }

            foreach (var (entry, regular, excess) in SplitEntries(orderedEntries, day.CapacityHours))
            {
                var entryResult = _calculator.Calculate(new ResourceDayCostInput(
                    day.Profile,
                    day.CapacityHours,
                    0m,
                    regular,
                    excess,
                    fullCompensationCapacity));
                var projectKey = ((Guid?)entry.ProjectId, entryResult.Currency ?? string.Empty);
                var roleKey = (entry.ProjectRoleId, entryResult.Currency ?? string.Empty);
                AddAttribution(projectMetrics, projectKey, 0m, entryResult.ActualHours, entryResult.RegularActualHours, entryResult.ExcessHours, entryResult.OvertimeHours, null, entryResult.TotalActualLaborCost, entryResult.OvertimeCost, entryResult.CostProfileMissing && entryResult.ActualHours > 0m);
                AddAttribution(roleMetrics, roleKey, 0m, entryResult.ActualHours, entryResult.RegularActualHours, entryResult.ExcessHours, entryResult.OvertimeHours, null, entryResult.TotalActualLaborCost, entryResult.OvertimeCost, entryResult.CostProfileMissing && entryResult.ActualHours > 0m);
            }

            daily.Add(new CostUtilizationDailyDto
            {
                ResourceId = day.Resource.Id,
                ResourceName = day.Resource.Name,
                WorkDate = day.WorkDate,
                Currency = dayResult.Currency,
                CapacityHours = dayResult.CapacityHours,
                PlannedHours = dayResult.PlannedHours,
                ActualHours = dayResult.ActualHours,
                RegularHours = dayResult.RegularActualHours,
                PlanningGapHours = dayResult.PlanningGapHours,
                ExecutionGapHours = dayResult.ExecutionGapHours,
                UnusedCapacityHours = dayResult.UnusedCapacityHours,
                ExcessHours = dayResult.ExcessHours,
                OvertimeHours = dayResult.OvertimeHours,
                PlannedUtilization = dayResult.PlannedUtilization,
                ActualUtilization = dayResult.ActualUtilization,
                PlannedLaborCost = dayResult.PlannedLaborCost,
                ActualLaborCost = dayResult.TotalActualLaborCost,
                LaborCostVariance = dayResult.TotalActualLaborCost.HasValue && dayResult.PlannedLaborCost.HasValue ? dayResult.TotalActualLaborCost.Value - dayResult.PlannedLaborCost.Value : null,
                OvertimeCost = dayResult.OvertimeCost,
                UnusedCapacityCost = dayResult.UnusedCapacityCost,
                CommittedCapacityCost = dayResult.CommittedCapacityCost,
                TotalResourceCost = dayResult.TotalResourceCost,
                CostProfileMissing = dayResult.CostProfileMissing,
                CostCoverageComplete = dayResult.CostCoverageComplete,
                UncostedActualHours = dayResult.UncostedActualHours,
                UncostedPlannedHours = dayResult.UncostedPlannedHours
            });
        }

        AddVendorResourceMetrics(vendorMetrics, resources, resourceMetrics);
        var report = new CostUtilizationReportDto
        {
            From = from,
            To = to,
            CanViewCosts = CanViewCosts(),
            Operational = operational.ToDto(null),
            ByCurrency = currencyMetrics.OrderBy(item => item.Key).Select(item => item.Value.ToDto(item.Key)).ToList(),
            Resources = resourceMetrics.OrderBy(item => resourceById[item.Key.ResourceId].Name).ThenBy(item => item.Key.Currency).Select(item => new CostUtilizationResourceDto
            {
                ResourceId = item.Key.ResourceId,
                ResourceName = resourceById[item.Key.ResourceId].Name,
                ResourceType = ResourceTypeConstants.ToApiCode(resourceById[item.Key.ResourceId].Type),
                DepartmentName = resourceById[item.Key.ResourceId].DepartmentId is { } departmentId && departmentNames.TryGetValue(departmentId, out var deptName) ? deptName : null,
                VendorId = resourceById[item.Key.ResourceId].VendorId,
                VendorName = GetVendorName(resourceById[item.Key.ResourceId], vendorNames),
                Metrics = item.Value.ToDto(item.Key.Currency)
            }).ToList(),
            Projects = BuildAttributions(projectMetrics, projectNames, "Unassigned Project", projectReferences),
            Vendors = vendorMetrics.OrderBy(item => item.Key.Currency).ThenBy(item => item.Key.Id).Select(item => new CostUtilizationAttributionDto
            {
                Id = item.Key.Id,
                Name = item.Key.Id.HasValue ? vendorNames.GetValueOrDefault(item.Key.Id.Value, "Unknown Vendor") : "Unassigned Vendor",
                Currency = item.Key.Currency,
                PlannedHours = item.Value.PlannedHours,
                ActualHours = item.Value.ActualHours,
                RegularHours = item.Value.RegularHours,
                ExcessHours = item.Value.ExcessHours,
                OvertimeHours = item.Value.OvertimeHours,
                PlannedLaborCost = item.Value.ToDto(item.Key.Currency).PlannedLaborCost,
                ActualLaborCost = item.Value.ToDto(item.Key.Currency).ActualLaborCost,
                OvertimeCost = item.Value.ToDto(item.Key.Currency).OvertimeCost,
                LaborCostVariance = item.Value.ToDto(item.Key.Currency).LaborCostVariance,
                CostProfileMissing = item.Value.ToDto(item.Key.Currency).CostProfileMissing,
                CostCoverageComplete = item.Value.ToDto(item.Key.Currency).CostCoverageComplete
            }).ToList(),
            Roles = BuildAttributions(roleMetrics, roleNames, "Unassigned Role"),
            Daily = daily.OrderBy(item => item.WorkDate).ThenBy(item => item.ResourceName).ToList(),
            Warnings = warnings
        };

        return Result<CostUtilizationReportDto>.Success(CanViewCosts() ? report : RedactCosts(report));
    }

    private static void AddVendorResourceMetrics(
        Dictionary<(Guid? Id, string Currency), MetricAccumulator> target,
        IReadOnlyList<Resource> resources,
        Dictionary<(Guid ResourceId, string Currency), MetricAccumulator> resourceMetrics)
    {
        foreach (var item in resourceMetrics)
        {
            var resource = resources.FirstOrDefault(resource => resource.Id == item.Key.ResourceId);
            if (resource is null) continue;
            var vendor = GetOrCreate(target, (resource.VendorId, item.Key.Currency));
            vendor.Add(item.Value);
        }
    }

    private static IReadOnlyList<CostUtilizationAttributionDto> BuildAttributions(
        Dictionary<(Guid? Id, string Currency), AttributionAccumulator> source,
        IReadOnlyDictionary<Guid, string> names,
        string unassignedLabel,
        IReadOnlyDictionary<Guid, ProjectReference>? projectReferences = null)
        => source.OrderBy(item => item.Key.Currency).ThenBy(item => item.Key.Id).Select(item => item.Value.ToDto(
            item.Key.Id.HasValue ? names.GetValueOrDefault(item.Key.Id.Value, item.Key.Id.Value.ToString()) : unassignedLabel,
            item.Key.Id,
            item.Key.Currency,
            item.Key.Id.HasValue ? projectReferences?.GetValueOrDefault(item.Key.Id.Value)?.PublicId : null)).ToList();

    private static string? GetVendorName(Resource resource, IReadOnlyDictionary<Guid, string> vendorNames)
        => resource.VendorId.HasValue && vendorNames.TryGetValue(resource.VendorId.Value, out var name) ? name : null;

    private static void AddAttribution(
        Dictionary<(Guid? Id, string Currency), AttributionAccumulator> target,
        (Guid? Id, string Currency) key,
        decimal plannedHours,
        decimal actualHours,
        decimal regularHours,
        decimal excessHours,
        decimal overtimeHours,
        decimal? plannedCost,
        decimal? actualCost,
        decimal? overtimeCost,
        bool missing)
    {
        var accumulator = GetOrCreate(target, key);
        accumulator.PlannedHours += plannedHours;
        accumulator.ActualHours += actualHours;
        accumulator.RegularHours += regularHours;
        accumulator.ExcessHours += excessHours;
        accumulator.OvertimeHours += overtimeHours;
        accumulator.AddCost(plannedCost, actualCost, overtimeCost, missing);
    }

    private static IEnumerable<(ActualWorkCostSlice Entry, decimal Regular, decimal Excess)> SplitEntries(
        IReadOnlyList<ActualWorkCostSlice> entries,
        decimal capacityHours)
    {
        var remaining = Math.Max(0m, capacityHours);
        foreach (var entry in entries)
        {
            var hours = Math.Max(0m, entry.ActualSeconds / 3600m);
            var regular = Math.Min(hours, remaining);
            remaining -= regular;
            yield return (entry, regular, hours - regular);
        }
    }

    private static void AddPlannedHours(
        Dictionary<(Guid ResourceId, DateTime Date), DayState> days,
        Guid resourceId,
        Guid? projectId,
        Guid? projectRoleId,
        Guid? activityId,
        DateTime startDate,
        DateTime endDate,
        decimal plannedHours,
        DateTime reportFrom,
        DateTime reportTo)
    {
        if (plannedHours <= 0m || !days.ContainsKey((resourceId, reportFrom.Date)))
        {
            // The first condition is the normal path for a zero assignment.
            // The second check is only a cheap guard for malformed ids; the
            // actual overlap is calculated below from the dictionary keys.
            if (plannedHours <= 0m) return;
        }

        var start = startDate.Date < reportFrom.Date ? reportFrom.Date : startDate.Date;
        var end = endDate.Date > reportTo.Date ? reportTo.Date : endDate.Date;
        if (end < start) return;
        var dates = Enumerable.Range(0, (end - start).Days + 1)
            .Select(offset => start.AddDays(offset))
            .Where(date => days.ContainsKey((resourceId, date)))
            .ToList();
        if (dates.Count == 0) return;
        var totalCapacity = dates.Sum(date => Math.Max(0m, days[(resourceId, date)].CapacityHours));
        var useCapacityWeight = totalCapacity > 0m;
        foreach (var date in dates)
        {
            var weight = useCapacityWeight
                ? Math.Max(0m, days[(resourceId, date)].CapacityHours) / totalCapacity
                : 1m / dates.Count;
            days[(resourceId, date)].PlannedLines.Add(new PlannedLine(projectId, projectRoleId, activityId, plannedHours * weight));
            days[(resourceId, date)].PlannedHours += plannedHours * weight;
        }
    }

    private static decimal ResolveAllocationHours(
        ResourceAllocation allocation,
        Dictionary<(Guid ResourceId, DateTime Date), DayState> days,
        DateTime reportFrom,
        DateTime reportTo)
    {
        if (allocation.PlannedHours.HasValue) return Math.Max(0m, allocation.PlannedHours.Value);
        var start = allocation.StartDate.Date < reportFrom.Date ? reportFrom.Date : allocation.StartDate.Date;
        var end = allocation.EndDate.Date > reportTo.Date ? reportTo.Date : allocation.EndDate.Date;
        var dates = Enumerable.Range(0, Math.Max(0, (end - start).Days + 1))
            .Select(offset => start.AddDays(offset))
            .Where(date => days.ContainsKey((allocation.ResourceId, date)))
            .ToList();
        if (allocation.HoursPerWeek.HasValue) return Math.Max(0m, allocation.HoursPerWeek.Value) * Math.Max(1m, dates.Count / 7m);
        var capacity = dates.Sum(date => Math.Max(0m, days[(allocation.ResourceId, date)].CapacityHours));
        return Math.Max(0m, allocation.AllocationPercentage / 100m * capacity);
    }

    private static decimal CalculateCapacity(
        Resource resource,
        DateTime date,
        WorkingCalendar calendar,
        Dictionary<(Guid ResourceId, DateTime Date), List<ResourceCalendar>> exceptions,
        Dictionary<Guid, List<Contract>> contracts)
    {
        var activeContract = contracts.GetValueOrDefault(resource.Id)?.FirstOrDefault(contract => contract.StartDate.Date <= date.Date && (!contract.EndDate.HasValue || contract.EndDate.Value.Date >= date.Date));
        if (contracts.ContainsKey(resource.Id) && activeContract is null) return 0m;
        if (IsWeekend(date, calendar.WorkingDaysPerWeek)) return 0m;
        if ((calendar.Holidays ?? []).Any(holiday => holiday.Date.Date == date.Date)) return 0m;

        var productiveHours = Math.Max(0m, calendar.ProductiveHoursPerDay);
        if (exceptions.TryGetValue((resource.Id, date.Date), out var resourceExceptions))
        {
            var deduction = resourceExceptions
                .Where(item => item.DayType is "Leave" or "NonWorking")
                .Sum(item => item.AvailableHours ?? productiveHours);
            productiveHours = Math.Max(0m, productiveHours - deduction);
        }
        return productiveHours;
    }

    private static decimal GetCompensationPeriodCapacity(
        DayState day,
        IReadOnlyDictionary<Guid, WorkingCalendar> calendarById,
        WorkingCalendar defaultCalendar,
        Dictionary<(Guid ResourceId, DateTime Date), List<ResourceCalendar>> exceptions,
        Dictionary<Guid, List<Contract>> contracts)
    {
        var calendar = day.Resource.BaseCalendarId.HasValue && calendarById.TryGetValue(day.Resource.BaseCalendarId.Value, out var selectedCalendar)
            ? selectedCalendar
            : defaultCalendar;
        if (day.Profile?.CompensationType == CompensationType.Weekly)
        {
            var monday = day.WorkDate.AddDays(-(((int)day.WorkDate.DayOfWeek + 6) % 7));
            return Enumerable.Range(0, 7).Sum(offset => CalculateCapacity(day.Resource, monday.AddDays(offset), calendar, exceptions, contracts));
        }
        if (day.Profile?.CompensationType == CompensationType.Monthly)
        {
            var first = new DateTime(day.WorkDate.Year, day.WorkDate.Month, 1);
            var days = DateTime.DaysInMonth(day.WorkDate.Year, day.WorkDate.Month);
            return Enumerable.Range(0, days).Sum(offset => CalculateCapacity(day.Resource, first.AddDays(offset), calendar, exceptions, contracts));
        }
        return 1m;
    }

    private static bool IsWeekend(DateTime date, int workingDaysPerWeek)
        => workingDaysPerWeek >= 6 ? date.DayOfWeek == DayOfWeek.Sunday : date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

    private static ResourceCostProfile? ResolveProfile(
        Guid resourceId,
        DateTime date,
        Dictionary<Guid, List<ResourceCostProfile>> profiles,
        out string? overlapMessage)
    {
        overlapMessage = null;
        var matches = profiles.GetValueOrDefault(resourceId)?.Where(profile => profile.EffectiveFrom.Date <= date.Date && (!profile.EffectiveTo.HasValue || profile.EffectiveTo.Value.Date >= date.Date)).ToList() ?? [];
        if (matches.Count > 1)
        {
            overlapMessage = $"Overlapping ResourceCostProfile records were found for resource {resourceId} on {date:yyyy-MM-dd}; analytics refused to choose one.";
            return null;
        }
        return matches.SingleOrDefault();
    }

    private bool CanViewCosts()
        => _http.HttpContext?.User.HasClaim("permission", "Resource.ViewCostRate") == true
            || _http.HttpContext?.User.HasClaim("permission", "Resource.ManageRates") == true
            || _http.HttpContext?.User.HasClaim("permission", "Resource.Manage") == true;

    private static CostUtilizationReportDto EmptyReport(DateTime from, DateTime to, bool canViewCosts)
        => new() { From = from, To = to, CanViewCosts = canViewCosts };

    private static CostUtilizationReportDto RedactCosts(CostUtilizationReportDto report)
    {
        static CostUtilizationMetricDto Redact(CostUtilizationMetricDto item) => new()
        {
            Currency = item.Currency,
            CapacityHours = item.CapacityHours,
            PlannedHours = item.PlannedHours,
            ActualHours = item.ActualHours,
            RegularHours = item.RegularHours,
            PlanningGapHours = item.PlanningGapHours,
            ExecutionGapHours = item.ExecutionGapHours,
            UnusedCapacityHours = item.UnusedCapacityHours,
            ExcessHours = item.ExcessHours,
            OvertimeHours = item.OvertimeHours,
            PlannedUtilization = item.PlannedUtilization,
            ActualUtilization = item.ActualUtilization,
            CostProfileMissing = item.CostProfileMissing,
            CostCoverageComplete = item.CostCoverageComplete,
            UncostedActualHours = item.UncostedActualHours,
            UncostedPlannedHours = item.UncostedPlannedHours
        };
        return new CostUtilizationReportDto
        {
            From = report.From,
            To = report.To,
            CanViewCosts = false,
            Operational = Redact(report.Operational),
            ByCurrency = report.ByCurrency.Select(Redact).ToList(),
            Resources = report.Resources.Select(resource => new CostUtilizationResourceDto { ResourceId = resource.ResourceId, ResourceName = resource.ResourceName, ResourceType = resource.ResourceType, VendorId = resource.VendorId, VendorName = resource.VendorName, Metrics = Redact(resource.Metrics) }).ToList(),
            Projects = report.Projects.Select(project => new CostUtilizationAttributionDto { Id = project.Id, ProjectPublicId = project.ProjectPublicId, Name = project.Name, Currency = project.Currency, PlannedHours = project.PlannedHours, ActualHours = project.ActualHours, RegularHours = project.RegularHours, ExcessHours = project.ExcessHours, OvertimeHours = project.OvertimeHours, CostProfileMissing = project.CostProfileMissing, CostCoverageComplete = project.CostCoverageComplete }).ToList(),
            Vendors = report.Vendors.Select(vendor => new CostUtilizationAttributionDto { Id = vendor.Id, Name = vendor.Name, Currency = vendor.Currency, PlannedHours = vendor.PlannedHours, ActualHours = vendor.ActualHours, RegularHours = vendor.RegularHours, ExcessHours = vendor.ExcessHours, OvertimeHours = vendor.OvertimeHours, CostProfileMissing = vendor.CostProfileMissing, CostCoverageComplete = vendor.CostCoverageComplete }).ToList(),
            Roles = report.Roles.Select(role => new CostUtilizationAttributionDto { Id = role.Id, Name = role.Name, Currency = role.Currency, PlannedHours = role.PlannedHours, ActualHours = role.ActualHours, RegularHours = role.RegularHours, ExcessHours = role.ExcessHours, OvertimeHours = role.OvertimeHours, CostProfileMissing = role.CostProfileMissing, CostCoverageComplete = role.CostCoverageComplete }).ToList(),
            Daily = report.Daily.Select(day => new CostUtilizationDailyDto { ResourceId = day.ResourceId, ResourceName = day.ResourceName, WorkDate = day.WorkDate, Currency = day.Currency, CapacityHours = day.CapacityHours, PlannedHours = day.PlannedHours, ActualHours = day.ActualHours, RegularHours = day.RegularHours, PlanningGapHours = day.PlanningGapHours, ExecutionGapHours = day.ExecutionGapHours, UnusedCapacityHours = day.UnusedCapacityHours, ExcessHours = day.ExcessHours, OvertimeHours = day.OvertimeHours, PlannedUtilization = day.PlannedUtilization, ActualUtilization = day.ActualUtilization, CostProfileMissing = day.CostProfileMissing, CostCoverageComplete = day.CostCoverageComplete, UncostedActualHours = day.UncostedActualHours, UncostedPlannedHours = day.UncostedPlannedHours }).ToList(),
            Warnings = report.Warnings
        };
    }

    private static T GetOrCreate<T>(Dictionary<(Guid? Id, string Currency), T> map, (Guid? Id, string Currency) key) where T : new()
    {
        if (!map.TryGetValue(key, out var value)) { value = new T(); map[key] = value; }
        return value;
    }

    private static T GetOrCreate<T>(Dictionary<(Guid ResourceId, string Currency), T> map, (Guid ResourceId, string Currency) key) where T : new()
    {
        if (!map.TryGetValue(key, out var value)) { value = new T(); map[key] = value; }
        return value;
    }

    private static T GetOrCreate<T>(Dictionary<string, T> map, string key) where T : new()
    {
        if (!map.TryGetValue(key, out var value)) { value = new T(); map[key] = value; }
        return value;
    }

    private sealed class DayState
    {
        public Resource Resource { get; init; } = null!;
        public DateTime WorkDate { get; init; }
        public decimal CapacityHours { get; init; }
        public decimal PlannedHours { get; set; }
        public ResourceCostProfile? Profile { get; init; }
        public string? ProfileOverlapMessage { get; init; }
        public List<PlannedLine> PlannedLines { get; } = [];
        public List<ActualWorkCostSlice> ActualEntries { get; } = [];
        public ResourceDayCostResult Result { get; set; } = null!;
    }

    private sealed record PlannedLine(Guid? ProjectId, Guid? ProjectRoleId, Guid? ActivityId, decimal Hours);

    private sealed class MetricAccumulator
    {
        public decimal CapacityHours;
        public decimal PlannedHours;
        public decimal ActualHours;
        public decimal RegularHours;
        public decimal PlanningGapHours;
        public decimal ExecutionGapHours;
        public decimal UnusedCapacityHours;
        public decimal ExcessHours;
        public decimal OvertimeHours;
        public decimal PlannedLaborCost;
        public decimal ActualLaborCost;
        public decimal OvertimeCost;
        public decimal UnusedCapacityCost;
        public decimal CommittedCapacityCost;
        public decimal UncostedActualHours;
        public decimal UncostedPlannedHours;
        public bool CostProfileMissing;
        public bool CostCoverageComplete = true;
        public bool HasCostValues;

        public void Add(ResourceDayCostResult result)
        {
            CapacityHours += result.CapacityHours;
            PlannedHours += result.PlannedHours;
            ActualHours += result.ActualHours;
            RegularHours += result.RegularActualHours;
            PlanningGapHours += result.PlanningGapHours;
            ExecutionGapHours += result.ExecutionGapHours;
            UnusedCapacityHours += result.UnusedCapacityHours;
            ExcessHours += result.ExcessHours;
            OvertimeHours += result.OvertimeHours;
            if (result.CostProfileMissing)
            {
                CostProfileMissing |= result.ActualHours > 0m || result.PlannedHours > 0m;
                CostCoverageComplete &= result.ActualHours <= 0m && result.PlannedHours <= 0m;
                UncostedActualHours += result.UncostedActualHours;
                UncostedPlannedHours += result.UncostedPlannedHours;
            }
            else
            {
                HasCostValues = true;
                PlannedLaborCost += result.PlannedLaborCost ?? 0m;
                ActualLaborCost += result.TotalActualLaborCost ?? 0m;
                OvertimeCost += result.OvertimeCost ?? 0m;
                UnusedCapacityCost += result.UnusedCapacityCost ?? 0m;
                CommittedCapacityCost += result.CommittedCapacityCost ?? 0m;
            }
        }

        public void Add(MetricAccumulator other)
        {
            CapacityHours += other.CapacityHours;
            PlannedHours += other.PlannedHours;
            ActualHours += other.ActualHours;
            RegularHours += other.RegularHours;
            PlanningGapHours += other.PlanningGapHours;
            ExecutionGapHours += other.ExecutionGapHours;
            UnusedCapacityHours += other.UnusedCapacityHours;
            ExcessHours += other.ExcessHours;
            OvertimeHours += other.OvertimeHours;
            PlannedLaborCost += other.PlannedLaborCost;
            ActualLaborCost += other.ActualLaborCost;
            OvertimeCost += other.OvertimeCost;
            UnusedCapacityCost += other.UnusedCapacityCost;
            CommittedCapacityCost += other.CommittedCapacityCost;
            UncostedActualHours += other.UncostedActualHours;
            UncostedPlannedHours += other.UncostedPlannedHours;
            CostProfileMissing |= other.CostProfileMissing;
            CostCoverageComplete &= other.CostCoverageComplete;
            HasCostValues |= other.HasCostValues;
        }

        public CostUtilizationMetricDto ToDto(string? currency)
        {
            var costAvailable = HasCostValues && CostCoverageComplete && !CostProfileMissing;
            return new CostUtilizationMetricDto
            {
                Currency = string.IsNullOrWhiteSpace(currency) ? null : currency,
                CapacityHours = CapacityHours,
                PlannedHours = PlannedHours,
                ActualHours = ActualHours,
                RegularHours = RegularHours,
                PlanningGapHours = PlanningGapHours,
                ExecutionGapHours = ExecutionGapHours,
                UnusedCapacityHours = UnusedCapacityHours,
                ExcessHours = ExcessHours,
                OvertimeHours = OvertimeHours,
                PlannedUtilization = CapacityHours > 0m ? PlannedHours / CapacityHours * 100m : null,
                ActualUtilization = CapacityHours > 0m ? ActualHours / CapacityHours * 100m : null,
                PlannedLaborCost = costAvailable ? PlannedLaborCost : null,
                ActualLaborCost = costAvailable ? ActualLaborCost : null,
                LaborCostVariance = costAvailable ? ActualLaborCost - PlannedLaborCost : null,
                OvertimeCost = costAvailable ? OvertimeCost : null,
                UnusedCapacityCost = costAvailable ? UnusedCapacityCost : null,
                CommittedCapacityCost = costAvailable ? CommittedCapacityCost : null,
                TotalResourceCost = costAvailable ? ActualLaborCost + UnusedCapacityCost : null,
                CostProfileMissing = CostProfileMissing,
                CostCoverageComplete = CostCoverageComplete,
                UncostedActualHours = UncostedActualHours,
                UncostedPlannedHours = UncostedPlannedHours
            };
        }
    }

    private sealed class AttributionAccumulator
    {
        public decimal PlannedHours;
        public decimal ActualHours;
        public decimal RegularHours;
        public decimal ExcessHours;
        public decimal OvertimeHours;
        public decimal PlannedLaborCost;
        public decimal ActualLaborCost;
        public decimal OvertimeCost;
        public bool CostProfileMissing;
        public bool CostCoverageComplete = true;
        public bool HasCostValues;

        public void AddCost(decimal? planned, decimal? actual, decimal? overtime, bool missing)
        {
            if (missing)
            {
                CostProfileMissing = true;
                CostCoverageComplete = false;
                return;
            }
            HasCostValues = true;
            PlannedLaborCost += planned ?? 0m;
            ActualLaborCost += actual ?? 0m;
            OvertimeCost += overtime ?? 0m;
        }

        public CostUtilizationAttributionDto ToDto(string name, Guid? id, string currency, Guid? projectPublicId = null)
        {
            var costAvailable = HasCostValues && CostCoverageComplete && !CostProfileMissing;
            return new CostUtilizationAttributionDto
            {
                Id = id,
                ProjectPublicId = projectPublicId,
                Name = name,
                Currency = currency,
                PlannedHours = PlannedHours,
                ActualHours = ActualHours,
                RegularHours = RegularHours,
                ExcessHours = ExcessHours,
                OvertimeHours = OvertimeHours,
                PlannedLaborCost = costAvailable ? PlannedLaborCost : null,
                ActualLaborCost = costAvailable ? ActualLaborCost : null,
                OvertimeCost = costAvailable ? OvertimeCost : null,
                LaborCostVariance = costAvailable ? ActualLaborCost - PlannedLaborCost : null,
                CostProfileMissing = CostProfileMissing,
                CostCoverageComplete = CostCoverageComplete
            };
        }
    }
}
