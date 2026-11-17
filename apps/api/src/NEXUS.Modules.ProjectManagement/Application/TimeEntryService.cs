using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.Organization.Domain;
using NEXUS.Modules.ProjectManagement.Application.DTOs;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.Modules.ProjectPlanning.Domain;
using NEXUS.Modules.ResourceManagement.Application;
using NEXUS.Modules.ResourceManagement.Domain;
using NEXUS.Modules.WorkItems.Domain;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Management;

namespace NEXUS.Modules.ProjectManagement.Application;

public sealed class TimeEntryService : ITimeEntryService
{
    private sealed record TimeEntryReadScope(
        IReadOnlyList<Guid> OwnResourceIds,
        HashSet<Guid> ProjectIds,
        bool CanViewOwn,
        bool CanViewTeam);

    private readonly DbContext _context;
    private readonly IResourceReferenceLookup _resources;
    private readonly IProjectResourcePlanningContextLookup _planning;
    private readonly IWorkItemReferenceLookup _workItems;
    private readonly IWorkItemAssigneeLookup _workItemTargets;
    private readonly IManagementScopeResolver _scopeResolver;
    private readonly IManagementContextAccessor _managementContext;
    private readonly IAuditService _audit;
    private readonly IHttpContextAccessor _http;

    public TimeEntryService(
        DbContext context,
        IResourceReferenceLookup resources,
        IProjectResourcePlanningContextLookup planning,
        IWorkItemReferenceLookup workItems,
        IWorkItemAssigneeLookup workItemTargets,
        IManagementScopeResolver scopeResolver,
        IManagementContextAccessor managementContext,
        IAuditService audit,
        IHttpContextAccessor http)
    {
        _context = context;
        _resources = resources;
        _planning = planning;
        _workItems = workItems;
        _workItemTargets = workItemTargets;
        _scopeResolver = scopeResolver;
        _managementContext = managementContext;
        _audit = audit;
        _http = http;
    }

    public async Task<Result<TimeEntryDto>> CreateManualAsync(CreateTimeEntryDto dto, Guid actorId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var guard = EnsureWritable();
        if (guard is not null) return Result<TimeEntryDto>.Failure(guard);

        var resourceId = dto.ResourceId;
        if (!resourceId.HasValue)
        {
            resourceId = await _context.Set<Resource>()
                .Where(resource => resource.TenantId == tenantId && resource.UserId == actorId && !resource.IsDeleted)
                .Select(resource => (Guid?)resource.Id)
                .FirstOrDefaultAsync(cancellationToken);
            if (!resourceId.HasValue) return Result<TimeEntryDto>.Failure(Error.NotFound("No Resource is linked to the authenticated user"));
        }

        var duration = TimeEntryDuration.Normalize(dto.StartTime, dto.EndTime, dto.ActualSeconds, dto.BreakSeconds, dto.PauseSeconds);
        if (duration.IsFailure) return Result<TimeEntryDto>.Failure(duration.Error!);

        var validation = await ValidateReferencesAsync(resourceId.Value, dto.ProjectId, dto.WorkDate, dto.PlanActivityId, dto.WorkItemId, dto.ProjectRoleId, actorId, tenantId, true, cancellationToken);
        if (validation.IsFailure) return Result<TimeEntryDto>.Failure(validation.Error!);

        var overlap = await HasOverlapAsync(resourceId.Value, dto.WorkDate, duration.Value!, null, cancellationToken);
        if (overlap) return Result<TimeEntryDto>.Failure(Error.Conflict("The Resource already has an overlapping time entry for this date"));

        var entry = new TimeEntry
        {
            TenantId = tenantId,
            ResourceId = resourceId.Value,
            ProjectId = dto.ProjectId,
            PlanActivityId = dto.PlanActivityId,
            WorkItemId = dto.WorkItemId,
            ProjectRoleId = dto.ProjectRoleId,
            WorkDate = dto.WorkDate.Date,
            StartTime = duration.Value!.StartTime,
            EndTime = duration.Value.EndTime,
            ElapsedSeconds = duration.Value.ElapsedSeconds,
            ActualSeconds = duration.Value.ActualSeconds,
            BreakSeconds = duration.Value.BreakSeconds,
            PauseSeconds = duration.Value.PauseSeconds,
            Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim(),
            EntrySource = TimeEntry.SourceManual,
            CreatedBy = actorId.ToString()
        };
        _context.Set<TimeEntry>().Add(entry);
        await AddAuditAsync(entry, "Created", actorId, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return Result<TimeEntryDto>.Success(await MapAsync(entry, cancellationToken));
    }

    public async Task<Result<TimeEntryDto>> UpdateManualAsync(Guid id, UpdateTimeEntryDto dto, Guid actorId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var guard = EnsureWritable();
        if (guard is not null) return Result<TimeEntryDto>.Failure(guard);

        var entry = await _context.Set<TimeEntry>().FirstOrDefaultAsync(item => item.Id == id && item.TenantId == tenantId && !item.IsDeleted, cancellationToken);
        if (entry is null) return Result<TimeEntryDto>.Failure(Error.NotFound("Time entry not found"));
        var access = await CanMutateAsync(entry.ResourceId, entry.ProjectId, actorId, tenantId, cancellationToken);
        if (access.IsFailure) return Result<TimeEntryDto>.Failure(access.Error!);
        if (dto.RowVersion is null || dto.RowVersion.Length == 0)
            return Result<TimeEntryDto>.Failure(Error.Validation("RowVersion is required"));

        var projectId = dto.ProjectId ?? entry.ProjectId;
        var workDate = (dto.WorkDate ?? entry.WorkDate).Date;
        var breakSeconds = dto.BreakSeconds ?? entry.BreakSeconds;
        var pauseSeconds = dto.PauseSeconds ?? entry.PauseSeconds;
        var duration = TimeEntryDuration.Normalize(dto.StartTime ?? entry.StartTime, dto.EndTime ?? entry.EndTime, dto.ActualSeconds ?? entry.ActualSeconds, breakSeconds, pauseSeconds);
        if (duration.IsFailure) return Result<TimeEntryDto>.Failure(duration.Error!);

        var validation = await ValidateReferencesAsync(entry.ResourceId, projectId, workDate, dto.PlanActivityId ?? entry.PlanActivityId, dto.WorkItemId ?? entry.WorkItemId, dto.ProjectRoleId ?? entry.ProjectRoleId, actorId, tenantId, true, cancellationToken);
        if (validation.IsFailure) return Result<TimeEntryDto>.Failure(validation.Error!);
        if (await HasOverlapAsync(entry.ResourceId, workDate, duration.Value!, entry.Id, cancellationToken))
            return Result<TimeEntryDto>.Failure(Error.Conflict("The Resource already has an overlapping time entry for this date"));

        _context.Entry(entry).Property(item => item.RowVersion).OriginalValue = dto.RowVersion;
        entry.ProjectId = projectId;
        entry.PlanActivityId = dto.PlanActivityId ?? entry.PlanActivityId;
        entry.WorkItemId = dto.WorkItemId ?? entry.WorkItemId;
        entry.ProjectRoleId = dto.ProjectRoleId ?? entry.ProjectRoleId;
        entry.WorkDate = workDate;
        entry.StartTime = duration.Value!.StartTime;
        entry.EndTime = duration.Value.EndTime;
        entry.ElapsedSeconds = duration.Value.ElapsedSeconds;
        entry.ActualSeconds = duration.Value.ActualSeconds;
        entry.BreakSeconds = duration.Value.BreakSeconds;
        entry.PauseSeconds = duration.Value.PauseSeconds;
        if (dto.Description is not null) entry.Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim();
        entry.UpdatedBy = actorId.ToString();
        await AddAuditAsync(entry, "Updated", actorId, cancellationToken);
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<TimeEntryDto>.Failure(Error.Conflict("This time entry was changed by another user"));
        }
        return Result<TimeEntryDto>.Success(await MapAsync(entry, cancellationToken));
    }

    public async Task<Result> DeleteAsync(Guid id, Guid actorId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var guard = EnsureWritable();
        if (guard is not null) return Result.Failure(guard);
        var entry = await _context.Set<TimeEntry>().FirstOrDefaultAsync(item => item.Id == id && item.TenantId == tenantId && !item.IsDeleted, cancellationToken);
        if (entry is null) return Result.Failure(Error.NotFound("Time entry not found"));
        var access = await CanMutateAsync(entry.ResourceId, entry.ProjectId, actorId, tenantId, cancellationToken);
        if (access.IsFailure) return access;
        entry.IsDeleted = true;
        entry.UpdatedBy = actorId.ToString();
        await AddAuditAsync(entry, "Deleted", actorId, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result<TimeEntryDto>> GetByIdAsync(Guid id, Guid actorId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var entry = await _context.Set<TimeEntry>().AsNoTracking().FirstOrDefaultAsync(item => item.Id == id && item.TenantId == tenantId && !item.IsDeleted, cancellationToken);
        if (entry is null) return Result<TimeEntryDto>.Failure(Error.NotFound("Time entry not found"));
        var access = await CanReadAsync(entry.ResourceId, entry.ProjectId, actorId, tenantId, cancellationToken);
        if (access.IsFailure) return Result<TimeEntryDto>.Failure(access.Error!);
        return Result<TimeEntryDto>.Success(await MapAsync(entry, cancellationToken));
    }

    public async Task<Result<PagedResult<TimeEntryDto>>> ListAsync(TimeEntryFilterDto filter, Guid actorId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var scope = await GetReadScopeAsync(actorId, tenantId, cancellationToken);
        if (!IsFilterInScope(filter, scope))
            return Result<PagedResult<TimeEntryDto>>.Failure(Error.Forbidden("The requested time entries are outside your permitted scope"));
        var query = await BuildAuthorizedQueryAsync(filter, actorId, tenantId, cancellationToken, scope);
        var page = Math.Max(1, filter.Page);
        var pageSize = Math.Clamp(filter.PageSize, 1, 200);
        var total = await query.CountAsync(cancellationToken);
        var entries = await query.OrderByDescending(item => item.WorkDate).ThenByDescending(item => item.StartTime)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        var items = await MapManyAsync(entries, cancellationToken);
        return Result<PagedResult<TimeEntryDto>>.Success(new PagedResult<TimeEntryDto>(items, page, pageSize, total));
    }

    public async Task<Result<TimeEntrySummaryDto>> GetSummaryAsync(TimeEntryFilterDto filter, Guid actorId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var from = (filter.From ?? DateTime.UtcNow.Date.AddDays(-6)).Date;
        var to = (filter.To ?? DateTime.UtcNow.Date).Date;
        if (to < from) return Result<TimeEntrySummaryDto>.Failure(Error.Validation("The summary end date must be on or after the start date"));
        filter.From = from;
        filter.To = to;
        var scope = await GetReadScopeAsync(actorId, tenantId, cancellationToken);
        if (!IsFilterInScope(filter, scope))
            return Result<TimeEntrySummaryDto>.Failure(Error.Forbidden("The requested time summary is outside your permitted scope"));
        var query = await BuildAuthorizedQueryAsync(filter, actorId, tenantId, cancellationToken, scope);
        var entries = await query.AsNoTracking().ToListAsync(cancellationToken);
        var actualHours = entries.Sum(entry => entry.ActualSeconds) / 3600m;
        var plannedHours = await PlannedHoursAsync(filter, scope, tenantId, cancellationToken);
        var capacityHours = filter.ResourceId.HasValue
            ? await CapacityHoursAsync(filter.ResourceId.Value, filter.ProjectId, tenantId, from, to, cancellationToken)
            : 0m;
        var summary = new TimeEntrySummaryDto
        {
            From = from,
            To = to,
            ActualHours = Math.Round(actualHours, 2),
            PlannedHours = Math.Round(plannedHours, 2),
            WorkVarianceHours = Math.Round(actualHours - plannedHours, 2),
            CapacityHours = Math.Round(capacityHours, 2),
            PlannedUtilization = capacityHours > 0 ? Math.Round(plannedHours / capacityHours * 100m, 2) : 0m,
            ActualUtilization = capacityHours > 0 ? Math.Round(actualHours / capacityHours * 100m, 2) : 0m,
            ByResource = await BuildBreakdownAsync(entries.GroupBy(entry => entry.ResourceId).Select(group => (group.Key, group.Sum(entry => entry.ActualSeconds) / 3600m)), "Resource", cancellationToken),
            ByProject = await BuildBreakdownAsync(entries.GroupBy(entry => entry.ProjectId).Select(group => (group.Key, group.Sum(entry => entry.ActualSeconds) / 3600m)), "Project", cancellationToken),
            ByPlanActivity = await BuildBreakdownAsync(entries.Where(entry => entry.PlanActivityId.HasValue).GroupBy(entry => entry.PlanActivityId!.Value).Select(group => (group.Key, group.Sum(entry => entry.ActualSeconds) / 3600m)), "Plan Activity", cancellationToken),
            ByWorkItem = await BuildBreakdownAsync(entries.Where(entry => entry.WorkItemId.HasValue).GroupBy(entry => entry.WorkItemId!.Value).Select(group => (group.Key, group.Sum(entry => entry.ActualSeconds) / 3600m)), "Work Item", cancellationToken),
            ByTeam = await BuildTeamBreakdownAsync(entries, cancellationToken),
            ByProjectRole = await BuildBreakdownAsync(entries.Where(entry => entry.ProjectRoleId.HasValue).GroupBy(entry => entry.ProjectRoleId!.Value).Select(group => (group.Key, group.Sum(entry => entry.ActualSeconds) / 3600m)), "Project Role", cancellationToken)
        };
        return Result<TimeEntrySummaryDto>.Success(summary);
    }

    private async Task<TimeEntryReadScope> GetReadScopeAsync(Guid actorId, Guid tenantId, CancellationToken cancellationToken)
    {
        var ownResourceIds = await _context.Set<Resource>().Where(resource => resource.TenantId == tenantId && resource.UserId == actorId && !resource.IsDeleted).Select(resource => resource.Id).ToListAsync(cancellationToken);
        var canViewOwn = HasPermission(TimeTrackingPermissions.ViewOwn);
        var canViewTeam = HasPermission(TimeTrackingPermissions.ViewTeam);
        var projectIds = new HashSet<Guid>();
        if (canViewTeam)
        {
            var current = _managementContext.Current;
            if (current is not null && current.UserId == actorId && current.TenantId == tenantId)
            {
                var snapshot = await _scopeResolver.GetPerspectiveSnapshotAsync(
                    actorId, tenantId, current, cancellationToken);
                projectIds = snapshot.Scope.ProjectIds
                    .Intersect(current.Scope.ProjectIds)
                    .ToHashSet();
            }
        }
        return new TimeEntryReadScope(ownResourceIds, projectIds, canViewOwn, canViewTeam);
    }

    private async Task<IQueryable<TimeEntry>> BuildAuthorizedQueryAsync(
        TimeEntryFilterDto filter,
        Guid actorId,
        Guid tenantId,
        CancellationToken cancellationToken,
        TimeEntryReadScope? readScope = null)
    {
        var scope = readScope ?? await GetReadScopeAsync(actorId, tenantId, cancellationToken);
        var query = _context.Set<TimeEntry>().AsNoTracking().Where(entry => entry.TenantId == tenantId && !entry.IsDeleted);

        if (filter.ResourceId.HasValue)
        {
            var isOwnResource = scope.OwnResourceIds.Contains(filter.ResourceId.Value);
            if (isOwnResource && scope.CanViewOwn)
            {
                query = query.Where(entry => entry.ResourceId == filter.ResourceId.Value);
            }
            else if (!isOwnResource && scope.CanViewTeam && scope.ProjectIds.Count > 0)
            {
                query = query.Where(entry => entry.ResourceId == filter.ResourceId.Value
                    && !scope.OwnResourceIds.Contains(entry.ResourceId)
                    && scope.ProjectIds.Contains(entry.ProjectId));
            }
            else return query.Where(_ => false);
        }
        else
        {
            query = query.Where(entry =>
                (scope.CanViewOwn && scope.OwnResourceIds.Contains(entry.ResourceId))
                || (scope.CanViewTeam && !scope.OwnResourceIds.Contains(entry.ResourceId)
                    && scope.ProjectIds.Contains(entry.ProjectId)));
        }

        if (filter.ProjectId.HasValue) query = query.Where(entry => entry.ProjectId == filter.ProjectId.Value);
        if (filter.PlanActivityId.HasValue) query = query.Where(entry => entry.PlanActivityId == filter.PlanActivityId.Value);
        if (filter.WorkItemId.HasValue) query = query.Where(entry => entry.WorkItemId == filter.WorkItemId.Value);
        if (filter.ProjectRoleId.HasValue) query = query.Where(entry => entry.ProjectRoleId == filter.ProjectRoleId.Value);
        if (filter.From.HasValue) query = query.Where(entry => entry.WorkDate >= filter.From.Value.Date);
        if (filter.To.HasValue) query = query.Where(entry => entry.WorkDate < filter.To.Value.Date.AddDays(1));
        if (!string.IsNullOrWhiteSpace(filter.EntrySource)) query = query.Where(entry => entry.EntrySource == filter.EntrySource);
        return query;
    }

    private static bool IsFilterInScope(TimeEntryFilterDto filter, TimeEntryReadScope scope)
    {
        if (filter.ResourceId.HasValue)
        {
            var isOwnResource = scope.OwnResourceIds.Contains(filter.ResourceId.Value);
            if (isOwnResource) return scope.CanViewOwn;
            return scope.CanViewTeam && scope.ProjectIds.Count > 0
                && (!filter.ProjectId.HasValue || scope.ProjectIds.Contains(filter.ProjectId.Value));
        }

        if (filter.ProjectId.HasValue && !scope.CanViewOwn
            && (!scope.CanViewTeam || !scope.ProjectIds.Contains(filter.ProjectId.Value)))
            return false;

        return scope.CanViewOwn || (scope.CanViewTeam && scope.ProjectIds.Count > 0);
    }

    private async Task<Result> ValidateReferencesAsync(Guid resourceId, Guid projectId, DateTime workDate, Guid? activityId, Guid? workItemId, Guid? projectRoleId, Guid actorId, Guid tenantId, bool mutation, CancellationToken cancellationToken)
    {
        var resource = await _context.Set<Resource>().FirstOrDefaultAsync(item => item.Id == resourceId && item.TenantId == tenantId && !item.IsDeleted, cancellationToken);
        if (resource is null) return Result.Failure(Error.NotFound("Resource not found"));
        if (!resource.IsActive || string.Equals(resource.Status, "Inactive", StringComparison.OrdinalIgnoreCase)) return Result.Failure(Error.Validation("Resource is not active"));
        var project = await _context.Set<Project>().FirstOrDefaultAsync(item => item.Id == projectId && item.TenantId == tenantId && !item.IsDeleted, cancellationToken);
        if (project is null) return Result.Failure(Error.NotFound("Project not found"));
        var access = mutation ? await CanMutateAsync(resourceId, projectId, actorId, tenantId, cancellationToken) : await CanReadAsync(resourceId, projectId, actorId, tenantId, cancellationToken);
        if (access.IsFailure) return access;
        var workDay = workDate.Date;
        var directMember = await _context.Set<ProjectMember>().AnyAsync(item => item.TenantId == tenantId
            && item.ProjectId == projectId && item.ResourceId == resourceId && !item.IsDeleted
            && item.Status == "Active" && item.StartDate.Date <= workDay && item.EndDate.Date >= workDay, cancellationToken);
        var groupMember = false;
        if (!directMember)
        {
            var activeTeamIds = _context.Set<Team>().AsNoTracking()
                .Where(team => team.TenantId == tenantId && team.IsActive && !team.IsDeleted)
                .Select(team => team.Id);
            groupMember = await _context.Set<ProjectMember>().AnyAsync(projectMember =>
                projectMember.TenantId == tenantId && projectMember.ProjectId == projectId
                && projectMember.TeamId.HasValue && activeTeamIds.Contains(projectMember.TeamId.Value)
                && !projectMember.IsDeleted && projectMember.Status == "Active"
                && projectMember.StartDate.Date <= workDay && projectMember.EndDate.Date >= workDay
                && _context.Set<TeamMember>().Any(teamMember => teamMember.TeamId == projectMember.TeamId
                    && teamMember.TenantId == tenantId
                    && teamMember.ResourceId == resourceId && teamMember.IsActive && !teamMember.IsDeleted
                    && teamMember.EffectiveFrom.Date <= workDay
                    && (!teamMember.EffectiveTo.HasValue || teamMember.EffectiveTo.Value.Date >= workDay)), cancellationToken);
        }
        var member = directMember || groupMember;
        if (!member) return Result.Failure(Error.Validation("The Resource is not an active Project Member for the work date"));

        if (activityId.HasValue)
        {
            var validActivity = await _context.Set<PlanActivity>().AnyAsync(activity => activity.Id == activityId.Value && activity.TenantId == tenantId && !activity.IsDeleted && activity.PlanVersion != null && activity.PlanVersion.Plan != null && activity.PlanVersion.Plan.ProjectId == projectId, cancellationToken);
            if (!validActivity) return Result.Failure(Error.Validation("Plan Activity does not belong to the selected Project"));
        }
        if (workItemId.HasValue)
        {
            var workItem = await _workItems.FindAsync(workItemId.Value, cancellationToken);
            if (workItem is null || workItem.ProjectId != projectId) return Result.Failure(Error.Validation("Work Item does not belong to the selected Project"));
            if (mutation && resource.UserId == actorId && (workItem.AssignedResourceId.HasValue || workItem.AssignedTeamId.HasValue))
            {
                var isAssignedResource = workItem.AssignedResourceId == resourceId;
                var isAssignedTeamMember = workItem.AssignedTeamId.HasValue && await _workItemTargets.IsCurrentTeamMemberAsync(workItem.AssignedTeamId.Value, resourceId, cancellationToken);
                if (!isAssignedResource && !isAssignedTeamMember)
                    return Result.Failure(Error.Forbidden("You can only record time against Work Items assigned to you or one of your active Teams"));
            }
        }
        if (projectRoleId.HasValue && !await _planning.HasActiveProjectMemberRoleAsync(projectId, resourceId, projectRoleId.Value, cancellationToken))
            return Result.Failure(Error.Validation("The selected Project Role is not active for this Resource on the Project"));
        return Result.Success();
    }

    private async Task<Result> CanReadAsync(Guid resourceId, Guid projectId, Guid actorId, Guid tenantId, CancellationToken cancellationToken)
    {
        var resource = await _context.Set<Resource>().AsNoTracking().FirstOrDefaultAsync(item => item.Id == resourceId && item.TenantId == tenantId && !item.IsDeleted, cancellationToken);
        if (resource?.UserId == actorId)
            return HasPermission(TimeTrackingPermissions.ViewOwn)
                ? Result.Success()
                : Result.Failure(Error.Forbidden("You are not allowed to view your own time"));
        if (!HasPermission(TimeTrackingPermissions.ViewTeam)) return Result.Failure(Error.Forbidden("You are not allowed to view this Resource's time"));
        return await CanAccessProjectScopeAsync(projectId, actorId, tenantId, cancellationToken)
            ? Result.Success()
            : Result.Failure(Error.Forbidden("The Project is outside your management scope"));
    }

    private async Task<Result> CanMutateAsync(Guid resourceId, Guid projectId, Guid actorId, Guid tenantId, CancellationToken cancellationToken)
    {
        if (_managementContext.Current?.EffectiveReadOnly == true) return Result.Failure(Error.Forbidden("This action is not available while using read-only perspective mode."));
        var resource = await _context.Set<Resource>().AsNoTracking().FirstOrDefaultAsync(item => item.Id == resourceId && item.TenantId == tenantId && !item.IsDeleted, cancellationToken);
        if (resource?.UserId == actorId)
            return HasPermission(TimeTrackingPermissions.ManageOwn) ? Result.Success() : Result.Failure(Error.Forbidden("You are not allowed to manage your own time"));
        if (!HasPermission(TimeTrackingPermissions.ManageTeam)) return Result.Failure(Error.Forbidden("You are not allowed to enter time for another Resource"));
        return await CanAccessProjectScopeAsync(projectId, actorId, tenantId, cancellationToken)
            ? Result.Success()
            : Result.Failure(Error.Forbidden("The Project is outside your management scope"));
    }

    private async Task<bool> CanAccessProjectScopeAsync(Guid? projectId, Guid actorId, Guid tenantId, CancellationToken cancellationToken)
        => projectId.HasValue && await _scopeResolver.CanAccessProjectAsync(actorId, tenantId, projectId.Value, cancellationToken);

    private Error? EnsureWritable() => _managementContext.Current?.EffectiveReadOnly == true
        ? Error.Forbidden("This action is not available while using read-only perspective mode.")
        : null;

    internal Task<Result> ValidateTimerReferencesAsync(Guid resourceId, Guid projectId, DateTime workDate, Guid? activityId, Guid? workItemId, Guid? projectRoleId, Guid actorId, Guid tenantId, CancellationToken cancellationToken)
        => ValidateReferencesAsync(resourceId, projectId, workDate, activityId, workItemId, projectRoleId, actorId, tenantId, true, cancellationToken);

    internal Task<bool> HasTimerOverlapAsync(Guid resourceId, DateTime workDate, NormalizedTimeEntryDuration duration, CancellationToken cancellationToken)
        => HasOverlapAsync(resourceId, workDate, duration, null, cancellationToken);

    private bool HasPermission(string permission) => _http.HttpContext?.User.HasClaim("permission", permission) == true;

    private async Task<bool> HasOverlapAsync(Guid resourceId, DateTime workDate, NormalizedTimeEntryDuration duration, Guid? excludeId, CancellationToken cancellationToken)
    {
        var start = duration.StartTime;
        var end = duration.EndTime;
        var existing = await _context.Set<TimeEntry>().AsNoTracking()
            .Where(entry => entry.ResourceId == resourceId && entry.WorkDate == workDate.Date && !entry.IsDeleted
                && (!excludeId.HasValue || entry.Id != excludeId.Value))
            .Select(entry => new { entry.StartTime, entry.EndTime })
            .ToListAsync(cancellationToken);
        return existing.Any(entry => start < entry.EndTime && end > entry.StartTime);
    }

    private async Task AddAuditAsync(TimeEntry entry, string action, Guid actorId, CancellationToken cancellationToken)
    {
        await _audit.AppendAsync(new AuditEntry
        {
            TenantId = entry.TenantId,
            EntityType = "TimeEntry",
            EntityId = entry.Id,
            Action = action,
            UserId = actorId,
            Timestamp = DateTime.UtcNow,
            CreatedBy = actorId.ToString(),
            Changes = JsonSerializer.Serialize(new { entry.ResourceId, entry.ProjectId, entry.WorkDate, entry.ActualSeconds })
        }, cancellationToken);
    }

    private async Task<TimeEntryDto> MapAsync(TimeEntry entry, CancellationToken cancellationToken)
        => (await MapManyAsync(new[] { entry }, cancellationToken)).Single();

    private async Task<List<TimeEntryDto>> MapManyAsync(IReadOnlyList<TimeEntry> entries, CancellationToken cancellationToken)
    {
        var resourceIds = entries.Select(entry => entry.ResourceId).Distinct().ToList();
        var projectIds = entries.Select(entry => entry.ProjectId).Distinct().ToList();
        var resources = await _context.Set<Resource>().AsNoTracking().Where(resource => resourceIds.Contains(resource.Id)).ToDictionaryAsync(resource => resource.Id, cancellationToken);
        var projects = await _context.Set<Project>().AsNoTracking().Where(project => projectIds.Contains(project.Id)).ToDictionaryAsync(project => project.Id, cancellationToken);
        var activityIds = entries.Where(entry => entry.PlanActivityId.HasValue).Select(entry => entry.PlanActivityId!.Value).Distinct().ToList();
        var activities = await _context.Set<PlanActivity>().AsNoTracking().Where(activity => activityIds.Contains(activity.Id)).ToDictionaryAsync(activity => activity.Id, cancellationToken);
        return entries.Select(entry => new TimeEntryDto
        {
            Id = entry.Id, ResourceId = entry.ResourceId, ResourceName = resources.GetValueOrDefault(entry.ResourceId)?.Name ?? string.Empty,
            ProjectId = entry.ProjectId, ProjectName = projects.GetValueOrDefault(entry.ProjectId)?.Name ?? string.Empty,
            PlanActivityId = entry.PlanActivityId, PlanActivityName = entry.PlanActivityId.HasValue ? activities.GetValueOrDefault(entry.PlanActivityId.Value)?.Name : null,
            WorkItemId = entry.WorkItemId, ProjectRoleId = entry.ProjectRoleId, TimerSessionId = entry.TimerSessionId, WorkDate = entry.WorkDate,
            StartTime = entry.StartTime, EndTime = entry.EndTime, ElapsedSeconds = entry.ElapsedSeconds, ActualSeconds = entry.ActualSeconds,
            BreakSeconds = entry.BreakSeconds, PauseSeconds = entry.PauseSeconds, Description = entry.Description, EntrySource = entry.EntrySource,
            CreatedBy = entry.CreatedBy, CreatedAt = entry.CreatedAt, UpdatedAt = entry.UpdatedAt, RowVersion = entry.RowVersion
        }).ToList();
    }

    private async Task<decimal> PlannedHoursAsync(TimeEntryFilterDto filter, TimeEntryReadScope scope, Guid tenantId, CancellationToken cancellationToken)
    {
        var from = (filter.From ?? DateTime.UtcNow.Date.AddDays(-6)).Date;
        var to = (filter.To ?? DateTime.UtcNow.Date).Date;
        var allocations = _context.Set<ResourceAllocation>().Where(allocation => allocation.TenantId == tenantId && !allocation.IsDeleted && allocation.Status != ResourceAllocation.StatusCancelled && allocation.StartDate.Date <= to && allocation.EndDate.Date >= from);
        var assignments = _context.Set<PlanAssignment>().Where(assignment => assignment.TenantId == tenantId && !assignment.IsDeleted && assignment.PlannedHours.HasValue && (!assignment.StartDate.HasValue || assignment.StartDate.Value.Date <= to) && (!assignment.EndDate.HasValue || assignment.EndDate.Value.Date >= from) && !assignment.ResourceAllocationId.HasValue);

        if (filter.ResourceId.HasValue)
        {
            if (scope.OwnResourceIds.Contains(filter.ResourceId.Value))
            {
                allocations = scope.CanViewOwn
                    ? allocations.Where(allocation => allocation.ResourceId == filter.ResourceId.Value)
                    : allocations.Where(_ => false);
                assignments = scope.CanViewOwn
                    ? assignments.Where(assignment => assignment.ResourceId == filter.ResourceId.Value)
                    : assignments.Where(_ => false);
            }
            else
            {
                allocations = scope.CanViewTeam && scope.ProjectIds.Count > 0
                    ? allocations.Where(allocation => allocation.ResourceId == filter.ResourceId.Value
                        && allocation.ProjectId.HasValue && scope.ProjectIds.Contains(allocation.ProjectId.Value))
                    : allocations.Where(_ => false);
                assignments = scope.CanViewTeam && scope.ProjectIds.Count > 0
                    ? assignments.Where(assignment => assignment.ResourceId == filter.ResourceId.Value
                        && assignment.PlanVersion != null && assignment.PlanVersion.Plan != null
                        && scope.ProjectIds.Contains(assignment.PlanVersion.Plan.ProjectId))
                    : assignments.Where(_ => false);
            }
        }
        else if (filter.ProjectId.HasValue)
        {
            var inTeamScope = scope.CanViewTeam && scope.ProjectIds.Contains(filter.ProjectId.Value);
            if (inTeamScope)
            {
                allocations = allocations.Where(allocation => allocation.ProjectId == filter.ProjectId.Value
                    && (scope.CanViewOwn || !scope.OwnResourceIds.Contains(allocation.ResourceId)));
                assignments = assignments.Where(assignment => assignment.PlanVersion != null && assignment.PlanVersion.Plan != null
                    && assignment.PlanVersion.Plan.ProjectId == filter.ProjectId.Value
                    && (scope.CanViewOwn || !scope.OwnResourceIds.Contains(assignment.ResourceId)));
            }
            else if (scope.CanViewOwn)
            {
                allocations = allocations.Where(allocation => allocation.ProjectId == filter.ProjectId.Value
                    && scope.OwnResourceIds.Contains(allocation.ResourceId));
                assignments = assignments.Where(assignment => assignment.PlanVersion != null && assignment.PlanVersion.Plan != null
                    && assignment.PlanVersion.Plan.ProjectId == filter.ProjectId.Value
                    && scope.OwnResourceIds.Contains(assignment.ResourceId));
            }
            else
            {
                allocations = allocations.Where(_ => false);
                assignments = assignments.Where(_ => false);
            }
        }

        else
        {
            allocations = allocations.Where(allocation =>
                (scope.CanViewOwn && scope.OwnResourceIds.Contains(allocation.ResourceId))
                || (scope.CanViewTeam && allocation.ProjectId.HasValue && scope.ProjectIds.Contains(allocation.ProjectId.Value)
                    && (scope.CanViewOwn || !scope.OwnResourceIds.Contains(allocation.ResourceId))));
            assignments = assignments.Where(assignment =>
                (scope.CanViewOwn && scope.OwnResourceIds.Contains(assignment.ResourceId))
                || (scope.CanViewTeam && assignment.PlanVersion != null && assignment.PlanVersion.Plan != null
                    && scope.ProjectIds.Contains(assignment.PlanVersion.Plan.ProjectId)
                    && (scope.CanViewOwn || !scope.OwnResourceIds.Contains(assignment.ResourceId))));
        }

        if (filter.ProjectId.HasValue && filter.ResourceId.HasValue)
        {
            allocations = allocations.Where(allocation => allocation.ProjectId == filter.ProjectId.Value);
            assignments = assignments.Where(assignment => assignment.PlanVersion != null && assignment.PlanVersion.Plan != null
                && assignment.PlanVersion.Plan.ProjectId == filter.ProjectId.Value);
        }

        var allocationHours = await allocations.Where(allocation => allocation.PlannedHours.HasValue).SumAsync(allocation => (decimal?)allocation.PlannedHours, cancellationToken) ?? 0m;
        var assignmentHours = await assignments.SumAsync(assignment => (decimal?)assignment.PlannedHours, cancellationToken) ?? 0m;
        return allocationHours + assignmentHours;
    }

    private async Task<decimal> CapacityHoursAsync(
        Guid resourceId,
        Guid? projectId,
        Guid tenantId,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken)
    {
        var resource = await _context.Set<Resource>().AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == resourceId && item.TenantId == tenantId && !item.IsDeleted, cancellationToken);
        if (resource is null) return 0m;

        var projectCalendarId = projectId.HasValue
            ? await _context.Set<Project>().AsNoTracking()
                .Where(project => project.Id == projectId.Value && project.TenantId == tenantId && !project.IsDeleted)
                .Select(project => project.WorkingCalendarId)
                .FirstOrDefaultAsync(cancellationToken)
            : null;
        var planningContext = projectId.HasValue
            ? await _planning.GetPlanningContextAsync(projectId.Value, resourceId, cancellationToken)
            : null;

        var calendars = await _context.Set<WorkingCalendar>().AsNoTracking()
            .Include(calendar => calendar.Holidays)
            .Include(calendar => calendar.DayRules)
            .Where(calendar => calendar.TenantId == tenantId && !calendar.IsDeleted
                && (calendar.IsDefault
                    || calendar.Id == resource.BaseCalendarId
                    || calendar.Id == projectCalendarId))
            .ToListAsync(cancellationToken);
        WorkingCalendar? FindValid(Guid? id) => id.HasValue
            ? calendars.FirstOrDefault(calendar => calendar.Id == id.Value && calendar.HasValidSchedule)
            : null;

        var resourceCalendar = FindValid(resource.BaseCalendarId);
        var projectCalendar = FindValid(projectCalendarId);
        var systemCalendar = calendars.FirstOrDefault(calendar => calendar.IsDefault && calendar.HasValidSchedule);
        var followsProject = string.Equals(planningContext?.CalendarPolicy, "FollowProjectCalendar", StringComparison.Ordinal);
        var calendar = followsProject
            ? projectCalendar ?? resourceCalendar ?? systemCalendar
            : resourceCalendar ?? projectCalendar ?? systemCalendar;
        if (calendar is null) return 0m;
        var capacityCalendar = resourceCalendar ?? systemCalendar ?? calendar;

        var resourceExceptions = await _context.Set<ResourceCalendar>().AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.ResourceId == resourceId && !item.IsDeleted
                && item.Date >= from.Date && item.Date < to.Date.AddDays(1))
            .ToListAsync(cancellationToken);
        var exceptionByDate = resourceExceptions
            .GroupBy(item => item.Date.Date)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(item => item.UpdatedAt ?? item.CreatedAt).First());
        var total = 0m;
        for (var day = from.Date; day <= to.Date; day = day.AddDays(1))
        {
            exceptionByDate.TryGetValue(day, out var exception);
            total += ResourceWorkingCapacityCalculator.GetAvailableDailyCapacity(
                resource, capacityCalendar, calendar, day, exception);
        }
        return total;
    }

    private async Task<List<ActualWorkBreakdownDto>> BuildBreakdownAsync(IEnumerable<(Guid Id, decimal Hours)> values, string label, CancellationToken cancellationToken)
    {
        var amounts = values.GroupBy(item => item.Id).ToDictionary(group => group.Key, group => group.Sum(item => item.Hours));
        var keys = amounts.Keys.ToList();
        if (keys.Count == 0) return new List<ActualWorkBreakdownDto>();
        Dictionary<Guid, string> names;
        if (label == "Resource")
            names = await _context.Set<Resource>().AsNoTracking().Where(item => keys.Contains(item.Id)).ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken);
        else if (label == "Project")
            names = await _context.Set<Project>().AsNoTracking().Where(item => keys.Contains(item.Id)).ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken);
        else if (label == "Plan Activity")
            names = await _context.Set<PlanActivity>().AsNoTracking().Where(item => keys.Contains(item.Id)).ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken);
        else if (label == "Work Item")
            names = (await _workItems.GetManyAsync(keys, cancellationToken)).ToDictionary(item => item.Key, item => item.Value.Title);
        else if (label == "Project Role")
            names = await _context.Set<NEXUS.Modules.Organization.Domain.Lookup>().AsNoTracking().Where(item => keys.Contains(item.Id)).ToDictionaryAsync(item => item.Id, item => string.IsNullOrWhiteSpace(item.NameEn) ? item.Name : item.NameEn, cancellationToken);
        else if (label == "Team")
            names = await _context.Set<NEXUS.Modules.Organization.Domain.Team>().AsNoTracking().Where(item => keys.Contains(item.Id)).ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken);
        else
            names = new Dictionary<Guid, string>();
        return keys.Select(id => new ActualWorkBreakdownDto { Id = id, Label = names.GetValueOrDefault(id, $"{label} {id}"), ActualHours = Math.Round(amounts[id], 2) }).ToList();
    }

    private async Task<List<ActualWorkBreakdownDto>> BuildTeamBreakdownAsync(IReadOnlyList<TimeEntry> entries, CancellationToken cancellationToken)
    {
        var workItemIds = entries.Where(entry => entry.WorkItemId.HasValue).Select(entry => entry.WorkItemId!.Value).Distinct().ToList();
        if (workItemIds.Count == 0) return new List<ActualWorkBreakdownDto>();
        var teamByWorkItem = await _context.Set<NEXUS.Modules.WorkItems.Domain.WorkItem>().AsNoTracking()
            .Where(item => workItemIds.Contains(item.Id) && item.AssignedTeamId.HasValue)
            .ToDictionaryAsync(item => item.Id, item => item.AssignedTeamId!.Value, cancellationToken);
        var totals = entries.Where(entry => entry.WorkItemId.HasValue && teamByWorkItem.ContainsKey(entry.WorkItemId.Value))
            .GroupBy(entry => teamByWorkItem[entry.WorkItemId!.Value])
            .Select(group => (group.Key, group.Sum(entry => entry.ActualSeconds) / 3600m));
        return await BuildBreakdownAsync(totals, "Team", cancellationToken);
    }
}
