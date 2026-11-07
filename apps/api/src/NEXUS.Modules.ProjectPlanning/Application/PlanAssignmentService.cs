using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Data;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.Organization.Domain;
using NEXUS.Modules.ProjectPlanning.Application.DTOs;
using NEXUS.Modules.ProjectPlanning.Application.Mapping;
using NEXUS.Modules.ProjectPlanning.Domain;
using NEXUS.Modules.ResourceManagement.Application;
using NEXUS.Modules.ResourceManagement.Domain;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Helpers;
using NEXUS.SharedKernel.Management;

namespace NEXUS.Modules.ProjectPlanning.Application;

public class PlanAssignmentService : IPlanAssignmentService
{
    private readonly DbContext _context;
    private readonly IResourceService _resourceService;
    private readonly IAuditService _auditService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IProjectResourcePlanningContextLookup _planningContextLookup;
    private readonly IManagementContextAccessor? _managementContext;

    public PlanAssignmentService(DbContext context, IResourceService resourceService, IAuditService auditService, IHttpContextAccessor httpContextAccessor, IProjectResourcePlanningContextLookup planningContextLookup, IManagementContextAccessor? managementContext = null)
    {
        _context = context;
        _resourceService = resourceService;
        _auditService = auditService;
        _httpContextAccessor = httpContextAccessor;
        _planningContextLookup = planningContextLookup;
        _managementContext = managementContext;
    }

    public async Task<Result<PlanAssignmentDto>> CreateAsync(Guid planId, CreatePlanAssignmentDto dto, string userId, CancellationToken cancellationToken = default)
    {
        if (_managementContext?.Current?.EffectiveReadOnly == true)
            return Result<PlanAssignmentDto>.Failure(Error.Validation("Cannot modify data in ReadOnly management view"));

        var workingVersion = await GetActiveWorkingVersionAsync(planId, cancellationToken);
        if (workingVersion is null)
            return Result<PlanAssignmentDto>.Failure(Error.NotFound(await GetActiveWorkingVersionNotAvailableMessageAsync(planId, cancellationToken)));

        var validationError = await ValidateAsync(dto, planId, workingVersion.Id, null, cancellationToken);
        if (validationError is not null) return Result<PlanAssignmentDto>.Failure(validationError);

        var assignment = dto.ToEntity(GetCurrentTenantId(), workingVersion.Id, userId);
        assignment.AllocationPct = null;
        assignment.HoursPerWeek = null;
        var activity = await _context.Set<PlanActivity>().AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == dto.ActivityId && item.PlanVersionId == workingVersion.Id, cancellationToken);
        assignment.StartDate = dto.StartDate ?? activity?.StartDate;
        assignment.EndDate = dto.EndDate ?? activity?.EndDate;
        assignment.PlannedHours = ResolveRequestedWorkHours(dto, assignment.StartDate, assignment.EndDate);
        _context.Set<PlanAssignment>().Add(assignment);
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Result<PlanAssignmentDto>.Failure(Error.Conflict("This resource is already assigned to the activity in this plan version"));
        }

        await SyncResourceAllocationAsync(assignment, userId, cancellationToken);

        await AuditAsync("Create", assignment, null, dto, userId, cancellationToken);
        return Result<PlanAssignmentDto>.Success(assignment.ToDto());
    }

    public async Task<Result<IReadOnlyList<PlanAssignmentDto>>> CreateManyAsync(
        Guid planId,
        CreatePlanAssignmentsBatchDto dto,
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (_managementContext?.Current?.EffectiveReadOnly == true)
            return Result<IReadOnlyList<PlanAssignmentDto>>.Failure(Error.Validation("Cannot modify data in ReadOnly management view"));
        if (dto.ActivityId == Guid.Empty || dto.Assignments is null || dto.Assignments.Count is < 1 or > 100)
            return Result<IReadOnlyList<PlanAssignmentDto>>.Failure(Error.Validation("Select one activity and between 1 and 100 resources."));
        if (dto.Assignments.Any(item => item.ResourceId == Guid.Empty || item.AssignedWorkHours <= 0m))
            return Result<IReadOnlyList<PlanAssignmentDto>>.Failure(Error.Validation("Every resource must have positive assigned work hours."));
        if (dto.Assignments.Select(item => item.ResourceId).Distinct().Count() != dto.Assignments.Count)
            return Result<IReadOnlyList<PlanAssignmentDto>>.Failure(Error.Conflict("A resource can only be included once in an assignment batch."));

        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var workingVersion = await GetActiveWorkingVersionAsync(planId, cancellationToken);
            if (workingVersion is null)
                return Result<IReadOnlyList<PlanAssignmentDto>>.Failure(Error.NotFound(await GetActiveWorkingVersionNotAvailableMessageAsync(planId, cancellationToken)));

            var activity = await _context.Set<PlanActivity>().AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == dto.ActivityId && item.PlanVersionId == workingVersion.Id, cancellationToken);
            if (activity is null)
                return Result<IReadOnlyList<PlanAssignmentDto>>.Failure(Error.Validation("Activity must belong to the active plan version."));
            if (activity.Type != PlanActivityType.Task)
                return Result<IReadOnlyList<PlanAssignmentDto>>.Failure(Error.Validation("Resources can only be assigned to task activities."));
            var isSummaryTask = activity.WbsId.HasValue && await _context.Set<PlanWbsNode>().AsNoTracking()
                .AnyAsync(node => node.Id == activity.WbsId.Value && node.PlanVersionId == workingVersion.Id && !node.IsDeleted
                    && _context.Set<PlanWbsNode>().Any(child => child.ParentWbsId == node.Id && !child.IsDeleted), cancellationToken);
            if (isSummaryTask)
                return Result<IReadOnlyList<PlanAssignmentDto>>.Failure(Error.Validation("Resources cannot be assigned to a summary task."));
            if (activity.EstimateHours is not > 0m)
                return Result<IReadOnlyList<PlanAssignmentDto>>.Failure(Error.Validation("Set positive planned work hours on the task before assigning resources."));

            var existingWorkHours = await _context.Set<PlanAssignment>().AsNoTracking()
                .Where(item => item.PlanVersionId == workingVersion.Id && item.ActivityId == activity.Id && !item.IsDeleted)
                .SumAsync(item => item.PlannedHours ?? 0m, cancellationToken);
            var proposedWorkHours = dto.Assignments.Sum(item => item.AssignedWorkHours);
            if (existingWorkHours + proposedWorkHours > activity.EstimateHours.Value + 0.0001m)
                return Result<IReadOnlyList<PlanAssignmentDto>>.Failure(Error.Validation(
                    $"Combined assigned work cannot exceed the task's planned work of {activity.EstimateHours.Value:0.##} hours."));

            var requests = dto.Assignments.Select(item => new CreatePlanAssignmentDto
            {
                ActivityId = activity.Id,
                ResourceId = item.ResourceId,
                AssignedWorkHours = item.AssignedWorkHours,
                StartDate = item.StartDate ?? activity.StartDate,
                EndDate = item.EndDate ?? activity.EndDate,
                ProjectRoleId = item.ProjectRoleId
            }).ToList();

            foreach (var request in requests)
            {
                var validationError = await ValidateAsync(request, planId, workingVersion.Id, null, cancellationToken);
                if (validationError is not null)
                    return Result<IReadOnlyList<PlanAssignmentDto>>.Failure(validationError);
            }

            var created = new List<PlanAssignmentDto>(requests.Count);
            foreach (var request in requests)
            {
                var result = await CreateAsync(planId, request, userId, cancellationToken);
                if (result.IsFailure || result.Value is null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Result<IReadOnlyList<PlanAssignmentDto>>.Failure(result.Error ?? Error.Conflict("The assignment batch could not be saved."));
                }
                if (!result.Value.ResourceAllocationId.HasValue)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Result<IReadOnlyList<PlanAssignmentDto>>.Failure(Error.Conflict("The resource allocation could not be synchronized with the task assignment."));
                }
                created.Add(result.Value);
            }

            await transaction.CommitAsync(cancellationToken);
            return Result<IReadOnlyList<PlanAssignmentDto>>.Success(created);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<Result<PlanAssignmentDto>> UpdateAsync(Guid planId, Guid assignmentId, UpdatePlanAssignmentDto dto, string userId, CancellationToken cancellationToken = default)
    {
        if (_managementContext?.Current?.EffectiveReadOnly == true)
            return Result<PlanAssignmentDto>.Failure(Error.Validation("Cannot modify data in ReadOnly management view"));

        var assignment = await _context.Set<PlanAssignment>().FirstOrDefaultAsync(a => a.Id == assignmentId, cancellationToken);
        if (assignment is null) return Result<PlanAssignmentDto>.Failure(Error.NotFound("Assignment not found"));

        var editError = await GetEditErrorAsync(assignment, planId, cancellationToken);
        if (editError is not null) return Result<PlanAssignmentDto>.Failure(editError);

        var validationError = await ValidateAsync(dto, planId, assignment.PlanVersionId, assignment.Id, cancellationToken);
        if (validationError is not null) return Result<PlanAssignmentDto>.Failure(validationError);

        var originalValues = assignment.ToDto();
        var priorActivityId = assignment.ActivityId;
        assignment.ActivityId = dto.ActivityId;
        assignment.ResourceId = dto.ResourceId;
        assignment.AllocationPct = null;
        assignment.HoursPerWeek = null;
        var activity = await _context.Set<PlanActivity>().AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == dto.ActivityId && item.PlanVersionId == assignment.PlanVersionId, cancellationToken);
        assignment.StartDate = dto.StartDate ?? (priorActivityId == dto.ActivityId ? assignment.StartDate : null) ?? activity?.StartDate;
        assignment.EndDate = dto.EndDate ?? (priorActivityId == dto.ActivityId ? assignment.EndDate : null) ?? activity?.EndDate;
        assignment.PlannedHours = ResolveRequestedWorkHours(dto, assignment.StartDate, assignment.EndDate);
        var originalRoleId = assignment.ProjectRoleId;
        assignment.ProjectRoleId = dto.ProjectRoleId ?? originalRoleId;
        assignment.UpdatedBy = userId;
        _context.Entry(assignment).Property(a => a.RowVersion).OriginalValue = dto.RowVersion;

        if (dto.ProjectRoleId.HasValue && originalRoleId != dto.ProjectRoleId)
        {
            var projectId = await _context.Set<Plan>()
                .Where(p => p.Id == planId)
                .Select(p => p.ProjectId)
                .FirstOrDefaultAsync(cancellationToken);
            var roleValid = await _planningContextLookup.HasActiveProjectMemberRoleAsync(projectId, dto.ResourceId, dto.ProjectRoleId.Value, cancellationToken);
            if (!roleValid) return Result<PlanAssignmentDto>.Failure(Error.Validation("The specified ProjectRole is not assigned to this resource as an active member of the project"));
        }

        if (assignment.ResourceAllocationId.HasValue)
        {
            var linkedAlloc = await _context.Set<ResourceAllocation>()
                .FirstOrDefaultAsync(a => a.Id == assignment.ResourceAllocationId.Value && !a.IsDeleted, cancellationToken);
            if (linkedAlloc is not null)
            {
                linkedAlloc.ProjectRoleId = dto.ProjectRoleId;
            }
        }

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<PlanAssignmentDto>.Failure(Error.Conflict("The assignment was modified by another user. Reload and try again."));
        }
        catch (DbUpdateException)
        {
            return Result<PlanAssignmentDto>.Failure(Error.Conflict("This resource is already assigned to the activity in this plan version"));
        }

        await SyncResourceAllocationAsync(assignment, userId, cancellationToken);

        await AuditAsync("Update", assignment, originalValues, assignment.ToDto(), userId, cancellationToken);
        return Result<PlanAssignmentDto>.Success(assignment.ToDto());
    }

    public async Task<Result> DeleteAsync(Guid planId, Guid assignmentId, byte[] rowVersion, string userId, CancellationToken cancellationToken = default)
    {
        var assignment = await _context.Set<PlanAssignment>().FirstOrDefaultAsync(a => a.Id == assignmentId, cancellationToken);
        if (assignment is null) return Result.Failure(Error.NotFound("Assignment not found"));

        var editError = await GetEditErrorAsync(assignment, planId, cancellationToken);
        if (editError is not null) return Result.Failure(editError);

        assignment.IsDeleted = true;
        assignment.UpdatedBy = userId;
        _context.Entry(assignment).Property(a => a.RowVersion).OriginalValue = rowVersion;

        await CancelResourceAllocationAsync(assignment, userId, cancellationToken);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure(Error.Conflict("The assignment was modified by another user. Reload and try again."));
        }

        await AuditAsync("Delete", assignment, null, null, userId, cancellationToken);
        return Result.Success();
    }

    public async Task<Result<IReadOnlyList<PlanAssignmentDto>>> GetByPlanAsync(Guid planId, CancellationToken cancellationToken = default)
    {
        var workingVersion = await GetActiveWorkingVersionAsync(planId, cancellationToken);
        if (workingVersion is null)
            return Result<IReadOnlyList<PlanAssignmentDto>>.Failure(Error.NotFound(await GetActiveWorkingVersionNotAvailableMessageAsync(planId, cancellationToken)));

        var assignments = await _context.Set<PlanAssignment>()
            .Where(a => a.PlanVersionId == workingVersion.Id)
            .OrderBy(a => a.ActivityId).ThenBy(a => a.ResourceId)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        return Result<IReadOnlyList<PlanAssignmentDto>>.Success(assignments.Select(a => a.ToDto()).ToList());
    }

    public async Task<Result<PlanAssignmentCapacityDto>> GetCapacityAsync(
        Guid projectId,
        Guid planId,
        Guid resourceId,
        DateTime startDate,
        DateTime endDate,
        Guid? excludeAssignmentId = null,
        CancellationToken cancellationToken = default)
    {
        return await CalculateAssignmentCapacityAsync(
            projectId, planId, resourceId, startDate, endDate, excludeAssignmentId, cancellationToken);
    }

    private async Task<Result<PlanAssignmentCapacityDto>> CalculateAssignmentCapacityAsync(
        Guid expectedProjectId,
        Guid planId,
        Guid resourceId,
        DateTime startDate,
        DateTime endDate,
        Guid? excludeAssignmentId,
        CancellationToken cancellationToken)
    {
        if (resourceId == Guid.Empty || endDate.Date < startDate.Date)
            return Result<PlanAssignmentCapacityDto>.Failure(Error.Validation("A resource and valid assignment date range are required."));

        var plan = await _context.Set<Plan>().AsNoTracking()
            .Where(item => item.Id == planId && !item.IsDeleted)
            .Select(item => new { item.Id, item.ProjectId, item.TenantId })
            .FirstOrDefaultAsync(cancellationToken);
        if (plan is null || plan.ProjectId != expectedProjectId)
            return Result<PlanAssignmentCapacityDto>.Failure(Error.NotFound("Plan not found for this project."));

        var member = await _planningContextLookup.GetPlanningContextAsync(plan.ProjectId, resourceId, cancellationToken);
        if (member is null || !member.IsActive || member.ProjectId != plan.ProjectId || member.ResourceId != resourceId)
            return Result<PlanAssignmentCapacityDto>.Failure(Error.Validation("Add this resource as an active project team member before assigning task work."));

        var resourceResult = await _resourceService.GetByIdAsync(resourceId, cancellationToken);
        if (resourceResult.IsFailure || resourceResult.Value is null)
            return Result<PlanAssignmentCapacityDto>.Failure(Error.Validation("Resource was not found or is not available in this tenant."));

        var calendars = await _context.Set<WorkingCalendar>().AsNoTracking()
            .Include(calendar => calendar.Holidays)
            .Include(calendar => calendar.DayRules)
            .Where(calendar => calendar.TenantId == plan.TenantId && !calendar.IsDeleted)
            .ToListAsync(cancellationToken);
        var resourceCalendarCandidate = resourceResult.Value.BaseCalendarId.HasValue
            ? calendars.FirstOrDefault(item => item.Id == resourceResult.Value.BaseCalendarId.Value)
            : null;
        var resourceCalendar = resourceCalendarCandidate?.HasValidSchedule == true ? resourceCalendarCandidate : null;
        var projectCalendarCandidate = member.ProjectCalendarId.HasValue
            ? calendars.FirstOrDefault(item => item.Id == member.ProjectCalendarId.Value)
            : null;
        var projectCalendar = projectCalendarCandidate?.HasValidSchedule == true ? projectCalendarCandidate : null;
        var systemCalendar = calendars.FirstOrDefault(item => item.IsDefault && item.HasValidSchedule);
        var followsProjectCalendar = string.Equals(member.CalendarPolicy, "FollowProjectCalendar", StringComparison.Ordinal);
        var calendar = followsProjectCalendar
            ? projectCalendar ?? resourceCalendar ?? systemCalendar
            : resourceCalendar ?? projectCalendar ?? systemCalendar;
        if (member.AllocationPercentage is < 0m or > 100m || member.EndDate.Date < member.StartDate.Date)
            return Result<PlanAssignmentCapacityDto>.Failure(Error.Validation("The project allocation or membership dates are invalid."));
        if (calendar is null || !calendar.HasValidSchedule)
            return Result<PlanAssignmentCapacityDto>.Failure(Error.Validation(
                "No valid working calendar applies to this assignment. Configure a resource calendar, project calendar, or the tenant's Standard calendar."));
        var resourceCapacityCalendar = resourceCalendar ?? systemCalendar ?? calendar;

        var loads = await (
            from assignment in _context.Set<PlanAssignment>().AsNoTracking()
            join version in _context.Set<PlanVersion>().AsNoTracking() on assignment.PlanVersionId equals version.Id
            join ownerPlan in _context.Set<Plan>().AsNoTracking() on version.PlanId equals ownerPlan.Id
            join activity in _context.Set<PlanActivity>().AsNoTracking() on assignment.ActivityId equals activity.Id
            where assignment.TenantId == plan.TenantId
                && assignment.ResourceId == resourceId
                && assignment.Id != excludeAssignmentId
                && !assignment.IsDeleted
                && !version.IsDeleted
                && version.Type == PlanVersionType.Working
                && version.Status == PlanVersionStatus.Active
                && ownerPlan.TenantId == plan.TenantId
                && ownerPlan.ProjectId == plan.ProjectId
                && !ownerPlan.IsDeleted
            select new AssignmentWorkload(
                assignment.Id,
                assignment.PlannedHours,
                assignment.HoursPerWeek,
                assignment.AllocationPct,
                assignment.StartDate,
                assignment.EndDate,
                activity.StartDate,
                activity.EndDate))
            .ToListAsync(cancellationToken);

        var effectiveLoads = new List<EffectiveAssignmentWorkload>(loads.Count);
        var weeklyResourceCapacity = ResourceWorkingCapacityCalculator.GetWeeklyCapacity(
            resourceResult.Value.HoursPerDay,
            resourceResult.Value.HoursPerWeek,
            resourceCapacityCalendar,
            calendar);
        foreach (var load in loads)
        {
            var workHours = ResolveExistingWorkHours(load, weeklyResourceCapacity);
            if (workHours <= 0m) continue;
            var loadStart = load.StartDate ?? load.ActivityStartDate;
            var loadEnd = load.EndDate ?? load.ActivityEndDate;
            if (!loadStart.HasValue || !loadEnd.HasValue)
                return Result<PlanAssignmentCapacityDto>.Failure(Error.Validation("An existing resource assignment has work hours but no schedule. Add task dates before assigning more work."));
            if (loadEnd.Value.Date < loadStart.Value.Date)
                return Result<PlanAssignmentCapacityDto>.Failure(Error.Validation("An existing resource assignment has an invalid schedule."));
            effectiveLoads.Add(new EffectiveAssignmentWorkload(
                load.Id,
                workHours,
                loadStart.Value.Date,
                loadEnd.Value.Date.AddDays(1)));
        }

        var requestedStart = startDate.Date;
        var requestedEnd = endDate.Date.AddDays(1);
        var workloadStart = effectiveLoads.Count == 0
            ? requestedStart
            : effectiveLoads.Min(load => load.StartDate) < requestedStart ? effectiveLoads.Min(load => load.StartDate) : requestedStart;
        var workloadEnd = effectiveLoads.Count == 0
            ? requestedEnd
            : effectiveLoads.Max(load => load.EndDate) > requestedEnd ? effectiveLoads.Max(load => load.EndDate) : requestedEnd;
        var resourceCalendarEntries = await _context.Set<ResourceCalendar>().AsNoTracking()
            .Where(entry => entry.TenantId == plan.TenantId && entry.ResourceId == resourceId
                && entry.Date >= workloadStart && entry.Date < workloadEnd && !entry.IsDeleted)
            .ToListAsync(cancellationToken);

        var requestedWeight = CalculateWorkingCalendarHours(
            resourceResult.Value.HoursPerDay,
            resourceResult.Value.HoursPerWeek,
            resourceCapacityCalendar,
            calendar,
            requestedStart,
            requestedEnd,
            resourceCalendarEntries);
        if (requestedWeight <= 0m)
            return Result<PlanAssignmentCapacityDto>.Failure(Error.Validation("The selected assignment period has no working capacity in the resource calendar."));

        var memberStart = member.StartDate.Date;
        var memberEnd = member.EndDate.Date;
        var memberEndExclusive = memberEnd.AddDays(1);
        var eligibleStart = requestedStart > memberStart ? requestedStart : memberStart;
        var eligibleEnd = requestedEnd < memberEndExclusive ? requestedEnd : memberEndExclusive;
        var isScheduleWithinMembership = requestedStart >= memberStart && endDate.Date <= memberEnd;
        var eligibleRequestedWeight = eligibleStart < eligibleEnd
            ? CalculateWorkingCalendarHours(
                resourceResult.Value.HoursPerDay,
                resourceResult.Value.HoursPerWeek,
                resourceCapacityCalendar,
                calendar,
                eligibleStart,
                eligibleEnd,
                resourceCalendarEntries)
            : 0m;
        var weeklyProjectCapacity = weeklyResourceCapacity * member.AllocationPercentage / 100m;
        var totalProjectCapacity = 0m;
        var assignedInRange = 0m;
        var maximumAssignable = decimal.MaxValue;
        var weekStart = requestedStart.AddDays(-(int)requestedStart.DayOfWeek);

        while (weekStart < requestedEnd)
        {
            var weekEnd = weekStart.AddDays(7);
            var targetSliceStart = requestedStart > weekStart ? requestedStart : weekStart;
            var targetSliceEnd = requestedEnd < weekEnd ? requestedEnd : weekEnd;
            var eligibleSliceStart = targetSliceStart > memberStart ? targetSliceStart : memberStart;
            var eligibleSliceEnd = targetSliceEnd < memberEndExclusive ? targetSliceEnd : memberEndExclusive;
            var targetWeight = eligibleSliceStart < eligibleSliceEnd
                ? CalculateWorkingCalendarHours(
                    resourceResult.Value.HoursPerDay,
                    resourceResult.Value.HoursPerWeek,
                    resourceCapacityCalendar,
                    calendar,
                    eligibleSliceStart,
                    eligibleSliceEnd,
                    resourceCalendarEntries)
                : 0m;
            var demandFraction = eligibleRequestedWeight > 0m ? targetWeight / eligibleRequestedWeight : 0m;
            var projectCapacity = targetWeight * member.AllocationPercentage / 100m;
            totalProjectCapacity += projectCapacity;

            var assigned = 0m;
            foreach (var load in effectiveLoads)
            {
                var fullWeight = CalculateWorkingCalendarHours(
                    resourceResult.Value.HoursPerDay,
                    resourceResult.Value.HoursPerWeek,
                    resourceCapacityCalendar,
                    calendar,
                    load.StartDate,
                    load.EndDate,
                    resourceCalendarEntries);
                if (fullWeight <= 0m) continue;
                var overlapStart = load.StartDate > eligibleSliceStart ? load.StartDate : eligibleSliceStart;
                var overlapEnd = load.EndDate < eligibleSliceEnd ? load.EndDate : eligibleSliceEnd;
                if (overlapStart < overlapEnd)
                    assigned += load.WorkHours * CalculateWorkingCalendarHours(
                        resourceResult.Value.HoursPerDay,
                        resourceResult.Value.HoursPerWeek,
                        resourceCapacityCalendar,
                        calendar,
                        overlapStart,
                        overlapEnd,
                        resourceCalendarEntries) / fullWeight;
            }
            assignedInRange += assigned;
            var remaining = Math.Max(0m, projectCapacity - assigned);
            if (demandFraction > 0m)
                maximumAssignable = Math.Min(maximumAssignable, remaining / demandFraction);
            weekStart = weekEnd;
        }

        if (maximumAssignable == decimal.MaxValue) maximumAssignable = 0m;
        return Result<PlanAssignmentCapacityDto>.Success(new PlanAssignmentCapacityDto
        {
            ResourceId = resourceId,
            StartDate = requestedStart,
            EndDate = endDate.Date,
            MembershipStartDate = memberStart,
            MembershipEndDate = memberEnd,
            IsScheduleWithinMembership = isScheduleWithinMembership,
            ProjectAllocationPercentage = member.AllocationPercentage,
            ResourceCapacityHoursPerWeek = weeklyResourceCapacity,
            ProjectCapacityHoursPerWeek = weeklyProjectCapacity,
            ProjectCapacityHours = Math.Round(totalProjectCapacity, 2, MidpointRounding.AwayFromZero),
            AssignedWorkHours = Math.Round(assignedInRange, 2, MidpointRounding.AwayFromZero),
            RemainingAssignableHours = Math.Round(Math.Max(0m, maximumAssignable), 2, MidpointRounding.AwayFromZero)
        });
    }

    private static decimal ResolveExistingWorkHours(AssignmentWorkload load, decimal weeklyResourceCapacity)
    {
        if (load.PlannedHours is > 0m) return load.PlannedHours.Value;
        var effectiveStart = load.StartDate ?? load.ActivityStartDate;
        var effectiveEnd = load.EndDate ?? load.ActivityEndDate;
        if (load.HoursPerWeek is > 0m && effectiveStart.HasValue && effectiveEnd.HasValue)
        {
            var days = (effectiveEnd.Value.Date - effectiveStart.Value.Date).Days + 1;
            return load.HoursPerWeek.Value * Math.Max(1, (int)Math.Ceiling(days / 7m));
        }
        if (load.AllocationPct is > 0m)
        {
            var weeks = effectiveStart.HasValue && effectiveEnd.HasValue
                ? Math.Max(1m, ((effectiveEnd.Value.Date - effectiveStart.Value.Date).Days + 1) / 7m)
                : 1m;
            return weeklyResourceCapacity * load.AllocationPct.Value / 100m * weeks;
        }
        return 0m;
    }

    private static decimal CalculateWorkingCalendarHours(
        decimal? hoursPerDay,
        decimal? hoursPerWeek,
        WorkingCalendar? capacityCalendar,
        WorkingCalendar calendar,
        DateTime startDate,
        DateTime endDateExclusive,
        IReadOnlyCollection<ResourceCalendar> resourceCalendarEntries)
    {
        if (endDateExclusive <= startDate) return 0m;
        var total = 0m;
        var exceptionsByDate = resourceCalendarEntries
            .Where(entry => entry.Date.Date >= startDate.Date && entry.Date.Date < endDateExclusive.Date)
            .GroupBy(entry => entry.Date.Date)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(item => item.UpdatedAt ?? item.CreatedAt).First());
        for (var date = startDate.Date; date < endDateExclusive.Date; date = date.AddDays(1))
        {
            exceptionsByDate.TryGetValue(date, out var exception);
            total += ResourceWorkingCapacityCalculator.GetAvailableDailyCapacity(
                hoursPerDay,
                hoursPerWeek,
                capacityCalendar,
                calendar,
                date,
                exception);
        }

        return total;
    }

    private sealed record AssignmentWorkload(
        Guid Id,
        decimal? PlannedHours,
        decimal? HoursPerWeek,
        decimal? AllocationPct,
        DateTime? StartDate,
        DateTime? EndDate,
        DateTime? ActivityStartDate,
        DateTime? ActivityEndDate);

    private sealed record EffectiveAssignmentWorkload(Guid Id, decimal WorkHours, DateTime StartDate, DateTime EndDate);

    private async Task SyncResourceAllocationAsync(PlanAssignment assignment, string userId, CancellationToken cancellationToken)
    {
        var projectId = await _context.Set<PlanVersion>()
            .Where(version => version.Id == assignment.PlanVersionId
                && version.TenantId == assignment.TenantId
                && !version.IsDeleted)
            .Join(
                _context.Set<Plan>().Where(plan => plan.TenantId == assignment.TenantId && !plan.IsDeleted),
                version => version.PlanId,
                plan => plan.Id,
                (_, plan) => (Guid?)plan.ProjectId)
            .FirstOrDefaultAsync(cancellationToken);
        if (!projectId.HasValue || projectId.Value == Guid.Empty) return;

        var effectivePlannedHours = assignment.PlannedHours ?? 0m;

        var linkedAllocation = assignment.ResourceAllocationId.HasValue
            ? await _context.Set<ResourceAllocation>()
                .FirstOrDefaultAsync(a => a.Id == assignment.ResourceAllocationId.Value
                    && a.TenantId == assignment.TenantId
                    && !a.IsDeleted, cancellationToken)
            : null;

        if (linkedAllocation is not null && linkedAllocation.ResourceId != assignment.ResourceId)
        {
            linkedAllocation.Status = ResourceAllocation.StatusCancelled;
            linkedAllocation.UpdatedBy = userId;
            assignment.ResourceAllocationId = null;
            _context.Entry(assignment).Property(item => item.ResourceAllocationId).IsModified = true;
            await _context.SaveChangesAsync(cancellationToken);
            linkedAllocation = null;
        }

        if (linkedAllocation is not null)
        {
            linkedAllocation.ProjectId = projectId.Value;
            linkedAllocation.ProjectRoleId = assignment.ProjectRoleId;
            linkedAllocation.AllocationPercentage = 0m;
            linkedAllocation.HoursPerWeek = null;
            linkedAllocation.PlannedHours = effectivePlannedHours;
            linkedAllocation.StartDate = assignment.StartDate!.Value.Date;
            linkedAllocation.EndDate = assignment.EndDate!.Value.Date;
            linkedAllocation.Status = ResourceAllocation.StatusConfirmed;
            linkedAllocation.UpdatedBy = userId;
            await _context.SaveChangesAsync(cancellationToken);
        }
        else
        {
            await ReconcileOrCreateAllocationAsync(assignment, projectId.Value, effectivePlannedHours, userId, cancellationToken);
        }
    }

    private async Task ReconcileOrCreateAllocationAsync(PlanAssignment assignment, Guid projectId, decimal effectivePlannedHours, string userId, CancellationToken cancellationToken)
    {
        var allocationCandidates = await _context.Set<ResourceAllocation>()
            .Where(a => a.ResourceId == assignment.ResourceId
                && a.TenantId == assignment.TenantId
                && (a.ProjectId == projectId || a.ProjectId == assignment.PlanVersionId)
                && a.StartDate == assignment.StartDate!.Value.Date
                && a.EndDate == assignment.EndDate!.Value.Date
                && a.AllocationSource == ResourceAllocation.SourceProjectPlan
                && !a.IsDeleted)
            .ToListAsync(cancellationToken);
        var exactCandidate = allocationCandidates.Where(item => item.SourceReferenceId == assignment.Id).ToList();
        var legacyCandidates = exactCandidate.Count > 0
            ? exactCandidate
            : allocationCandidates.Where(item => !item.SourceReferenceId.HasValue).ToList();

        ResourceAllocation alloc;

        if (legacyCandidates.Count == 1)
        {
            alloc = legacyCandidates[0];
            alloc.ProjectId = projectId;
            alloc.ProjectRoleId = assignment.ProjectRoleId;
            alloc.AllocationPercentage = 0m;
            alloc.HoursPerWeek = null;
            alloc.PlannedHours = effectivePlannedHours;
            alloc.StartDate = assignment.StartDate!.Value.Date;
            alloc.EndDate = assignment.EndDate!.Value.Date;
            alloc.Status = ResourceAllocation.StatusConfirmed;
            alloc.UpdatedBy = userId;
            assignment.ResourceAllocationId = alloc.Id;
        }
        else if (legacyCandidates.Count == 0)
        {
            alloc = new ResourceAllocation
            {
                TenantId = assignment.TenantId,
                ResourceId = assignment.ResourceId,
                ProjectId = projectId,
                ProjectRoleId = assignment.ProjectRoleId,
                AllocationPercentage = 0m,
                HoursPerWeek = null,
                PlannedHours = effectivePlannedHours,
                StartDate = assignment.StartDate!.Value.Date,
                EndDate = assignment.EndDate!.Value.Date,
                Status = ResourceAllocation.StatusConfirmed,
                IsBillable = true,
                AllocationSource = ResourceAllocation.SourceProjectPlan,
                SourceReferenceId = assignment.Id,
                CreatedBy = userId,
                UpdatedBy = userId
            };
            _context.Set<ResourceAllocation>().Add(alloc);
            try { await _context.SaveChangesAsync(cancellationToken); }
            catch (DbUpdateException) { return; }
            assignment.ResourceAllocationId = alloc.Id;
        }
        else
        {
            return;
        }

        _context.Entry(assignment).Property(a => a.ResourceAllocationId).IsModified = true;
        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task CancelResourceAllocationAsync(PlanAssignment assignment, string userId, CancellationToken cancellationToken)
    {
        if (!assignment.ResourceAllocationId.HasValue) return;

        var alloc = await _context.Set<ResourceAllocation>()
            .FirstOrDefaultAsync(a => a.Id == assignment.ResourceAllocationId.Value && !a.IsDeleted, cancellationToken);
        if (alloc is null) return;

        alloc.Status = ResourceAllocation.StatusCancelled;
        alloc.UpdatedBy = userId;
    }

    public async Task<decimal> GetAssignedPlannedHoursAsync(Guid planVersionId, Guid activityId, CancellationToken cancellationToken = default)
    {
        return await _context.Set<PlanAssignment>()
            .Where(a => a.PlanVersionId == planVersionId && a.ActivityId == activityId && !a.IsDeleted)
            .SumAsync(a => a.PlannedHours ?? 0m, cancellationToken);
    }

    public async Task<(decimal assignedPlannedHours, decimal effortVariance, string varianceLabel)> CalculateEffortVarianceAsync(
        Guid planVersionId, Guid activityId, CancellationToken cancellationToken = default)
    {
        var activity = await _context.Set<PlanActivity>()
            .FirstOrDefaultAsync(a => a.PlanVersionId == planVersionId && a.Id == activityId, cancellationToken);
        var estimateHours = activity?.EstimateHours ?? 0m;

        var assignedPlannedHours = await GetAssignedPlannedHoursAsync(planVersionId, activityId, cancellationToken);
        var effortVariance = assignedPlannedHours - estimateHours;
        string varianceLabel;
        if (effortVariance > 0) varianceLabel = "OverAssigned";
        else if (effortVariance < 0) varianceLabel = "UnderAssigned";
        else varianceLabel = "FullyAssigned";

        return (assignedPlannedHours, effortVariance, varianceLabel);
    }

    private async Task<Guid?> GetEffectiveCalendarIdAsync(Guid projectId, string calendarPolicy, CancellationToken cancellationToken)
    {
        if (calendarPolicy == "FollowProjectCalendar")
        {
            var defaultCalendar = await _context.Set<WorkingCalendar>()
                .AsNoTracking()
                .Where(c => c.TenantId == GetCurrentTenantId() && c.IsDefault && !c.IsDeleted)
                .FirstOrDefaultAsync(cancellationToken);
            return defaultCalendar?.Id;
        }

        return null;
    }

    private async Task<Error?> ValidateScheduleAsync(DateTime? startDate, DateTime? endDate, Guid? calendarId, CancellationToken cancellationToken)
    {
        if (!startDate.HasValue || !endDate.HasValue || !calendarId.HasValue) return null;

        var holidays = await _context.Set<Holiday>()
            .AsNoTracking()
            .Where(h => h.WorkingCalendarId == calendarId.Value
                && h.Date >= startDate.Value.Date
                && h.Date <= endDate.Value.Date
                && !h.IsDeleted)
            .ToListAsync(cancellationToken);

        if (holidays.Any())
            return Error.Validation("Assignment dates contain project holidays that make scheduling unavailable");

        return null;
    }

    private async Task<Error?> ValidateAsync(CreatePlanAssignmentDto dto, Guid planId, Guid planVersionId, Guid? excludedAssignmentId, CancellationToken cancellationToken)
    {
        if (dto.ActivityId == Guid.Empty) return Error.Validation("Activity is required");
        if (dto.ResourceId == Guid.Empty) return Error.Validation("Resource is required");
        if (dto.AssignedWorkHours is < 0m || dto.PlannedHours is < 0m || dto.HoursPerWeek is < 0m)
            return Error.Validation("Assigned work hours cannot be negative.");

        var activity = await _context.Set<PlanActivity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == dto.ActivityId && a.PlanVersionId == planVersionId, cancellationToken);
        if (activity is null) return Error.Validation("Activity must belong to the same plan version");

        var existingAssignment = excludedAssignmentId.HasValue
            ? await _context.Set<PlanAssignment>().AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == excludedAssignmentId.Value && !a.IsDeleted, cancellationToken)
            : null;
        var sameActivity = existingAssignment?.ActivityId == dto.ActivityId;
        var startDate = dto.StartDate ?? (sameActivity ? existingAssignment?.StartDate : null) ?? activity.StartDate;
        var endDate = dto.EndDate ?? (sameActivity ? existingAssignment?.EndDate : null) ?? activity.EndDate;
        if (!startDate.HasValue || !endDate.HasValue)
            return Error.Validation("Set start and finish dates for the task before assigning work hours.");
        if (endDate.Value.Date < startDate.Value.Date)
            return Error.Validation("Assignment end date cannot be earlier than its start date.");

        var assignedWorkHours = ResolveRequestedWorkHours(dto, startDate, endDate);
        if (!assignedWorkHours.HasValue || assignedWorkHours.Value <= 0m)
            return Error.Validation("Assigned work hours must be greater than zero.");

        if (activity.EstimateHours is >= 0m)
        {
            var existingTaskWorkHours = await _context.Set<PlanAssignment>().AsNoTracking()
                .Where(assignment => assignment.PlanVersionId == planVersionId
                    && assignment.ActivityId == activity.Id
                    && assignment.Id != excludedAssignmentId
                    && !assignment.IsDeleted)
                .SumAsync(assignment => assignment.PlannedHours ?? 0m, cancellationToken);
            if (existingTaskWorkHours + assignedWorkHours.Value > activity.EstimateHours.Value + 0.0001m)
                return Error.Validation($"Combined assigned work cannot exceed the task's planned work of {activity.EstimateHours.Value:0.##} hours.");
        }

        var resourceResult = await _resourceService.GetByIdAsync(dto.ResourceId, cancellationToken);
        if (resourceResult.IsFailure) return Error.Validation("Resource was not found or is not available in this tenant");

        var duplicateExists = await _context.Set<PlanAssignment>()
            .AnyAsync(a => a.PlanVersionId == planVersionId && a.ActivityId == dto.ActivityId && a.ResourceId == dto.ResourceId && a.Id != excludedAssignmentId, cancellationToken);
        if (duplicateExists) return Error.Conflict("This resource is already assigned to the activity in this plan version");

        if (dto.ProjectRoleId.HasValue)
        {
            var roleProjectId = await _context.Set<Plan>()
                .Where(p => p.Id == planId)
                .Select(p => p.ProjectId)
                .FirstOrDefaultAsync(cancellationToken);
            var roleValid = await _planningContextLookup.HasActiveProjectMemberRoleAsync(roleProjectId, dto.ResourceId, dto.ProjectRoleId.Value, cancellationToken);
            if (!roleValid) return Error.Validation("The specified ProjectRole is not assigned to this resource as an active member of the project");
        }

        // Project calendar holiday validation
        var projectId = await _context.Set<Plan>()
            .Where(p => p.Id == planId)
            .Select(p => p.ProjectId)
            .FirstOrDefaultAsync(cancellationToken);

        if (startDate.HasValue && endDate.HasValue)
        {
            var planningContext = await _planningContextLookup.GetPlanningContextAsync(projectId, dto.ResourceId, cancellationToken);
            if (planningContext?.ProjectCalendarId.HasValue == true
                && string.Equals(planningContext.CalendarPolicy, "FollowProjectCalendar", StringComparison.Ordinal))
            {
                var error = await ValidateScheduleAsync(startDate, endDate, planningContext.ProjectCalendarId.Value, cancellationToken);
                if (error is not null) return error;
            }
        }

        var capacity = await CalculateAssignmentCapacityAsync(
            projectId, planId, dto.ResourceId, startDate.Value, endDate.Value, excludedAssignmentId, cancellationToken);
        if (capacity.IsFailure || capacity.Value is null)
            return capacity.Error ?? Error.Validation("Project resource capacity could not be calculated.");
        if (!capacity.Value.IsScheduleWithinMembership)
            return Error.Validation("Assignment dates must fall within the resource's active project membership period.");
        if (assignedWorkHours.Value > capacity.Value.RemainingAssignableHours + 0.0001m)
            return Error.Validation($"Assigned work exceeds the resource's remaining project capacity of {capacity.Value.RemainingAssignableHours:0.##} hours for the selected schedule.");

        return null;
    }

    private static decimal? ResolveRequestedWorkHours(CreatePlanAssignmentDto dto, DateTime? startDate, DateTime? endDate) =>
        PlanAssignmentHoursCalculator.ResolvePlannedHours(
            dto.AssignedWorkHours ?? dto.PlannedHours,
            dto.HoursPerWeek,
            startDate,
            endDate);

    private async Task<Error?> GetEditErrorAsync(PlanAssignment assignment, Guid planId, CancellationToken cancellationToken)
    {
        if (!await _context.Set<Plan>().AnyAsync(p => p.Id == planId, cancellationToken)) return Error.NotFound("Plan not found");
        var workingVersion = await GetActiveWorkingVersionAsync(planId, cancellationToken);
        if (workingVersion is null) return Error.NotFound("No active working version exists for this plan");
        if (assignment.PlanVersionId == workingVersion.Id) return null;

        var assignmentVersion = await _context.Set<PlanVersion>().FirstOrDefaultAsync(v => v.Id == assignment.PlanVersionId, cancellationToken);
        return assignmentVersion?.Type == PlanVersionType.Baseline
            ? Error.Validation("Baseline versions are immutable; edits are only allowed in the active Working version")
            : Error.Validation("Only the active Working version is editable");
    }

    private Task<PlanVersion?> GetActiveWorkingVersionAsync(Guid planId, CancellationToken cancellationToken) =>
        _context.Set<PlanVersion>().FirstOrDefaultAsync(v => v.PlanId == planId && v.Type == PlanVersionType.Working && v.Status == PlanVersionStatus.Active, cancellationToken);

    private async Task<string> GetActiveWorkingVersionNotAvailableMessageAsync(Guid planId, CancellationToken cancellationToken)
    {
        var planExists = await _context.Set<Plan>().AnyAsync(p => p.Id == planId, cancellationToken);
        return planExists ? "No active working version exists for this plan" : "Plan not found";
    }

    private Task AuditAsync(string action, PlanAssignment assignment, object? originalValues, object? newValues, string userId, CancellationToken cancellationToken) =>
        _auditService.LogAsync(new AuditEntry
        {
            EntityType = "PlanAssignment", EntityId = assignment.Id, Action = action, UserId = TryParseGuid(userId),
            OriginalValues = originalValues is null ? null : System.Text.Json.JsonSerializer.Serialize(originalValues),
            NewValues = newValues is null ? null : System.Text.Json.JsonSerializer.Serialize(newValues),
            Timestamp = DateTime.UtcNow, CreatedBy = userId
        }, cancellationToken);

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
