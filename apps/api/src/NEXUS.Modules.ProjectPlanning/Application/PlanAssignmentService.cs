using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
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
        _context.Set<PlanAssignment>().Add(assignment);
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Result<PlanAssignmentDto>.Failure(Error.Conflict("This resource is already assigned to the activity in this plan version"));
        }

        await SyncResourceAllocationAsync(assignment, dto, userId, cancellationToken);

        await AuditAsync("Create", assignment, null, dto, userId, cancellationToken);
        return Result<PlanAssignmentDto>.Success(assignment.ToDto());
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
        assignment.ActivityId = dto.ActivityId;
        assignment.ResourceId = dto.ResourceId;
        assignment.AllocationPct = dto.AllocationPct;
        assignment.HoursPerWeek = dto.HoursPerWeek;
        assignment.PlannedHours = dto.PlannedHours;
        assignment.StartDate = dto.StartDate;
        assignment.EndDate = dto.EndDate;
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

        await SyncResourceAllocationAsync(assignment, dto, userId, cancellationToken);

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

    private async Task SyncResourceAllocationAsync(PlanAssignment assignment, CreatePlanAssignmentDto createDto, string userId, CancellationToken cancellationToken)
    {
        await SyncResourceAllocationAsync(assignment, createDto?.AllocationPct, createDto?.HoursPerWeek, createDto?.PlannedHours, createDto?.StartDate, createDto?.EndDate, createDto?.ProjectRoleId, userId, cancellationToken);
    }

    private async Task SyncResourceAllocationAsync(PlanAssignment assignment, UpdatePlanAssignmentDto updateDto, string userId, CancellationToken cancellationToken)
    {
        await SyncResourceAllocationAsync(assignment, updateDto?.AllocationPct, updateDto?.HoursPerWeek, updateDto?.PlannedHours, updateDto?.StartDate, updateDto?.EndDate, updateDto?.ProjectRoleId, userId, cancellationToken);
    }

    private async Task SyncResourceAllocationAsync(PlanAssignment assignment, decimal? allocationPct, decimal? hoursPerWeek, decimal? plannedHours, DateTime? startDate, DateTime? endDate, Guid? projectRoleId, string userId, CancellationToken cancellationToken)
    {
        var effectivePlannedHours = plannedHours.HasValue && plannedHours.Value > 0
            ? plannedHours.Value
            : CalculatePlannedHours(allocationPct, hoursPerWeek, startDate, endDate);

        var existingAlloc = assignment.ResourceAllocationId.HasValue
            ? await _context.Set<ResourceAllocation>()
                .FirstOrDefaultAsync(a => a.Id == assignment.ResourceAllocationId.Value && !a.IsDeleted, cancellationToken)
            : null;

        if (existingAlloc is not null)
        {
            existingAlloc.ProjectRoleId = projectRoleId;
            existingAlloc.AllocationPercentage = allocationPct ?? 0m;
            existingAlloc.HoursPerWeek = hoursPerWeek;
            existingAlloc.PlannedHours = effectivePlannedHours;
            existingAlloc.StartDate = startDate ?? DateTime.UtcNow;
            existingAlloc.EndDate = endDate ?? DateTime.UtcNow.AddMonths(1);
            existingAlloc.Status = ResourceAllocation.StatusConfirmed;
            existingAlloc.UpdatedBy = userId;
        }
        else
        {
            await ReconcileOrCreateAllocationAsync(assignment, effectivePlannedHours, allocationPct, hoursPerWeek, startDate, endDate, projectRoleId, userId, cancellationToken);
        }
    }

    private async Task ReconcileOrCreateAllocationAsync(PlanAssignment assignment, decimal effectivePlannedHours, decimal? allocationPct, decimal? hoursPerWeek, DateTime? startDate, DateTime? endDate, Guid? projectRoleId, string userId, CancellationToken cancellationToken)
    {
        var legacyCandidates = await _context.Set<ResourceAllocation>()
            .Where(a => a.ResourceId == assignment.ResourceId
                && a.ProjectId == assignment.PlanVersionId
                && a.StartDate == (startDate ?? DateTime.UtcNow)
                && a.EndDate == (endDate ?? DateTime.UtcNow.AddMonths(1))
                && a.AllocationSource == ResourceAllocation.SourceProjectPlan
                && !a.IsDeleted)
            .ToListAsync(cancellationToken);

        ResourceAllocation alloc;

        if (legacyCandidates.Count == 1)
        {
            alloc = legacyCandidates[0];
            alloc.ProjectRoleId = projectRoleId;
            alloc.AllocationPercentage = allocationPct ?? 0m;
            alloc.HoursPerWeek = hoursPerWeek;
            alloc.PlannedHours = effectivePlannedHours;
            alloc.StartDate = startDate ?? DateTime.UtcNow;
            alloc.EndDate = endDate ?? DateTime.UtcNow.AddMonths(1);
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
                ProjectRoleId = projectRoleId,
                AllocationPercentage = allocationPct ?? 0m,
                HoursPerWeek = hoursPerWeek,
                PlannedHours = effectivePlannedHours,
                StartDate = startDate ?? DateTime.UtcNow,
                EndDate = endDate ?? DateTime.UtcNow.AddMonths(1),
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

    private static decimal CalculatePlannedHours(decimal? allocationPct, decimal? hoursPerWeek, DateTime? startDate, DateTime? endDate)
    {
        if (hoursPerWeek.HasValue && hoursPerWeek.Value > 0)
        {
            if (startDate.HasValue && endDate.HasValue)
            {
                var weeks = Math.Max(1, (int)Math.Ceiling((endDate.Value - startDate.Value).Days / 7.0));
                return hoursPerWeek.Value * weeks;
            }
            return hoursPerWeek.Value;
        }
        if (allocationPct.HasValue && allocationPct.Value > 0)
        {
            return allocationPct.Value / 100m * 40m;
        }
        return 0m;
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
        if (dto.AllocationPct is < 0 or > 100) return Error.Validation("Allocation percentage must be between 0 and 100");
        if (dto.HoursPerWeek < 0) return Error.Validation("Hours per week cannot be negative");
        if (dto.StartDate.HasValue && dto.EndDate.HasValue && dto.EndDate < dto.StartDate)
            return Error.Validation("Assignment end date cannot be earlier than its start date");

        var activityExists = await _context.Set<PlanActivity>()
            .AnyAsync(a => a.Id == dto.ActivityId && a.PlanVersionId == planVersionId, cancellationToken);
        if (!activityExists) return Error.Validation("Activity must belong to the same plan version");

        var resourceResult = await _resourceService.GetByIdAsync(dto.ResourceId, cancellationToken);
        if (resourceResult.IsFailure) return Error.Validation("Resource was not found or is not available in this tenant");

        var duplicateExists = await _context.Set<PlanAssignment>()
            .AnyAsync(a => a.PlanVersionId == planVersionId && a.ActivityId == dto.ActivityId && a.ResourceId == dto.ResourceId && a.Id != excludedAssignmentId, cancellationToken);
        if (duplicateExists) return Error.Conflict("This resource is already assigned to the activity in this plan version");

        if (dto.ProjectRoleId.HasValue)
        {
            var projectId = await _context.Set<Plan>()
                .Where(p => p.Id == planId)
                .Select(p => p.ProjectId)
                .FirstOrDefaultAsync(cancellationToken);
            var roleValid = await _planningContextLookup.HasActiveProjectMemberRoleAsync(projectId, dto.ResourceId, dto.ProjectRoleId.Value, cancellationToken);
            if (!roleValid) return Error.Validation("The specified ProjectRole is not assigned to this resource as an active member of the project");
        }

        // Project calendar holiday validation
        if (dto.StartDate.HasValue && dto.EndDate.HasValue)
        {
            var projectId = await _context.Set<Plan>()
                .Where(p => p.Id == planId)
                .Select(p => p.ProjectId)
                .FirstOrDefaultAsync(cancellationToken);

            var planningContext = await _planningContextLookup.GetPlanningContextAsync(projectId, dto.ResourceId, cancellationToken);
            if (planningContext?.ProjectCalendarId.HasValue == true)
            {
                var error = await ValidateScheduleAsync(dto.StartDate, dto.EndDate, planningContext.ProjectCalendarId.Value, cancellationToken);
                if (error is not null) return error;
            }
        }

        return null;
    }

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
        return Guid.TryParse(tenantId, out var parsed) ? parsed : TenantConstants.DemoTenantId;
    }

    private static Guid? TryParseGuid(string value) => GuidHelper.TryParse(value);
}
