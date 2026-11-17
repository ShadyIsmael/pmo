using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.WorkItems.Application.DTOs;
using NEXUS.Modules.WorkItems.Domain;
using NEXUS.Modules.WorkflowEngine.Application.Contracts;
using NEXUS.Modules.WorkflowEngine.Application.Services;
using NEXUS.Modules.WorkflowEngine.Domain;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Helpers;
using NEXUS.SharedKernel.Management;

namespace NEXUS.Modules.WorkItems.Application;

public sealed class WorkItemService : IWorkItemService
{
    private readonly DbContext _context;
    private readonly IAuditService _auditService;
    private readonly IProjectReferenceLookup _projectLookup;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IWorkItemCommentAccess _commentAccess;
    private readonly IWorkItemCommentAuthorLookup _commentAuthorLookup;
    private readonly IWorkItemAttachmentStorage _attachmentStorage;
    private readonly WorkItemAttachmentOptions _attachmentOptions;
    private readonly IWorkItemStatusReferenceLookup _statusLookup;
    private readonly IManagementContextAccessor _managementContext;
    private readonly IWorkItemAssigneeLookup _assigneeLookup;
    private readonly IResourceReferenceLookup _resourceLookup;
    private readonly IUserReferenceLookup _userLookup;
    private readonly IWorkItemWorkflowProvisioningService _workflowProvisioning;
    private readonly IWorkflowInstanceService _workflowInstanceService;
    private readonly IProjectWorkflowConfigurationService _projectWorkflowConfiguration;

    public WorkItemService(DbContext context, IAuditService auditService, IProjectReferenceLookup projectLookup, IHttpContextAccessor httpContextAccessor, IWorkItemCommentAccess commentAccess, IWorkItemCommentAuthorLookup commentAuthorLookup, IWorkItemAttachmentStorage attachmentStorage, IOptions<WorkItemAttachmentOptions> attachmentOptions, IWorkItemStatusReferenceLookup statusLookup, IManagementContextAccessor managementContext, IWorkItemAssigneeLookup assigneeLookup, IResourceReferenceLookup resourceLookup, IUserReferenceLookup userLookup, IWorkItemWorkflowProvisioningService workflowProvisioning, IWorkflowInstanceService workflowInstanceService, IProjectWorkflowConfigurationService projectWorkflowConfiguration)
    {
        _context = context;
        _auditService = auditService;
        _projectLookup = projectLookup;
        _httpContextAccessor = httpContextAccessor;
        _commentAccess = commentAccess;
        _commentAuthorLookup = commentAuthorLookup;
        _attachmentStorage = attachmentStorage;
        _attachmentOptions = attachmentOptions.Value;
        _statusLookup = statusLookup;
        _managementContext = managementContext;
        _assigneeLookup = assigneeLookup;
        _resourceLookup = resourceLookup;
        _userLookup = userLookup;
        _workflowProvisioning = workflowProvisioning;
        _workflowInstanceService = workflowInstanceService;
        _projectWorkflowConfiguration = projectWorkflowConfiguration;
    }

    public async Task<Result<WorkItemDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var item = await ScopedWorkItems().FirstOrDefaultAsync(workItem => workItem.Id == id, cancellationToken);
        return item is null ? Result<WorkItemDto>.Failure(Error.NotFound("Work item not found")) : Result<WorkItemDto>.Success(await ToDtoAsync(item, cancellationToken));
    }

    public async Task<Result<IReadOnlyList<WorkItemAssigneeDto>>> GetAssigneesAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        if (await _projectLookup.FindAsync(projectId, cancellationToken) is null)
            return Result<IReadOnlyList<WorkItemAssigneeDto>>.Failure(Error.NotFound("Project not found"));

        var allowedProjectIds = _managementContext.Current?.Scope.ProjectIds;
        if (allowedProjectIds is null || !allowedProjectIds.Contains(projectId))
            return Result<IReadOnlyList<WorkItemAssigneeDto>>.Failure(Error.Forbidden("The project is outside the current management scope"));

        var candidates = await _assigneeLookup.GetEligibleAsync(projectId, cancellationToken);
        var teams = await _assigneeLookup.GetEligibleTeamsAsync(projectId, cancellationToken);
        var targets = candidates.Select(candidate => new WorkItemAssigneeDto
        {
            TargetType = "Resource",
            TargetId = candidate.ResourceId,
            ResourceId = candidate.ResourceId,
            Name = candidate.Name,
            Email = candidate.Email,
            JobTitle = candidate.JobTitle
        }).Concat(teams.Select(team => new WorkItemAssigneeDto
        {
            TargetType = "Team",
            TargetId = team.TeamId,
            TeamId = team.TeamId,
            Name = team.Name,
            Email = team.Code is null ? null : team.Code,
            JobTitle = "Team"
        })).ToList();
        return Result<IReadOnlyList<WorkItemAssigneeDto>>.Success(targets);
    }

    public async Task<PagedResult<WorkItemDto>> GetPagedAsync(int page, int pageSize, Guid? projectId = null, string? status = null, string? type = null, string? priority = null, string? search = null, Guid? assignedResourceId = null, Guid? assignedTeamId = null, Guid? legacyAssigneeUserId = null, Guid? reporterId = null, DateTime? dueDateFrom = null, DateTime? dueDateTo = null, bool? overdue = null, bool? unassigned = null, bool? highPriority = null, bool myWork = false, string? viewerUserId = null, string? sortBy = null, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = ApplyFilters(ScopedWorkItems(), projectId, status, type, priority, search, assignedResourceId, assignedTeamId, reporterId, dueDateFrom, dueDateTo, overdue, unassigned, highPriority, await GetTerminalStatusCodesAsync(cancellationToken), cancellationToken);
        if (legacyAssigneeUserId.HasValue)
        {
            var legacyResourceIds = (await _resourceLookup.GetDetailsByUserIdsAsync(new[] { legacyAssigneeUserId.Value }, cancellationToken)).Keys.ToArray();
            query = query.Where(item => item.AssignedResourceId.HasValue && legacyResourceIds.Contains(item.AssignedResourceId.Value));
        }
        if (myWork && Guid.TryParse(viewerUserId, out var viewerId))
        {
            var ownResourceIds = (await _resourceLookup.GetDetailsByUserIdsAsync(new[] { viewerId }, cancellationToken)).Keys.ToArray();
            var activeTeamIds = await _assigneeLookup.GetActiveTeamIdsForUserAsync(viewerId, cancellationToken);
            query = query.Where(item => (item.AssignedResourceId.HasValue && ownResourceIds.Contains(item.AssignedResourceId.Value)) || (item.AssignedTeamId.HasValue && activeTeamIds.Contains(item.AssignedTeamId.Value)));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await ApplySort(query, sortBy).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        var projectReferences = await _projectLookup.GetReferencesAsync(items.Where(item => item.ProjectId.HasValue).Select(item => item.ProjectId!.Value), cancellationToken);
        var resourceNames = await _resourceLookup.GetNamesAsync(items.Where(item => item.AssignedResourceId.HasValue).Select(item => item.AssignedResourceId!.Value), cancellationToken);
        var teamNames = new Dictionary<Guid, string>();
        foreach (var currentProjectId in items.Where(item => item.ProjectId.HasValue).Select(item => item.ProjectId!.Value).Distinct())
        {
            foreach (var team in await _assigneeLookup.GetEligibleTeamsAsync(currentProjectId, cancellationToken))
                teamNames.TryAdd(team.TeamId, team.Name);
        }
        var dtos = items.Select(item => ToDto(
            item,
            item.ProjectId.HasValue ? projectReferences.GetValueOrDefault(item.ProjectId.Value)?.Name : null,
            item.ProjectId.HasValue ? projectReferences.GetValueOrDefault(item.ProjectId.Value)?.PublicId : null,
            item.AssignedResourceId.HasValue ? resourceNames.GetValueOrDefault(item.AssignedResourceId.Value) : null,
            item.AssignedTeamId.HasValue ? teamNames.GetValueOrDefault(item.AssignedTeamId.Value) : null)).ToList();
        return new PagedResult<WorkItemDto>(dtos, page, pageSize, total);
    }

    public async Task<WorkItemInsightsDto> GetInsightsAsync(Guid? projectId = null, string? status = null, string? type = null, string? priority = null, string? search = null, Guid? assignedResourceId = null, Guid? assignedTeamId = null, Guid? legacyAssigneeUserId = null, Guid? reporterId = null, DateTime? dueDateFrom = null, DateTime? dueDateTo = null, bool? overdue = null, bool? unassigned = null, bool? highPriority = null, CancellationToken cancellationToken = default)
    {
        var terminalStatuses = await GetTerminalStatusCodesAsync(cancellationToken);
        var query = ApplyFilters(ScopedWorkItems().AsNoTracking(), projectId, status, type, priority, search, assignedResourceId, assignedTeamId, reporterId, dueDateFrom, dueDateTo, overdue, unassigned, highPriority, terminalStatuses, cancellationToken);
        if (legacyAssigneeUserId.HasValue)
        {
            var legacyResourceIds = (await _resourceLookup.GetDetailsByUserIdsAsync(new[] { legacyAssigneeUserId.Value }, cancellationToken)).Keys.ToArray();
            query = query.Where(item => item.AssignedResourceId.HasValue && legacyResourceIds.Contains(item.AssignedResourceId.Value));
        }
        var now = DateTime.UtcNow;
        var today = now.Date;
        var weekEnd = today.AddDays(7);
        return new WorkItemInsightsDto
        {
            Total = await query.CountAsync(cancellationToken),
            Open = await query.CountAsync(item => !terminalStatuses.Contains(item.Status), cancellationToken),
            Overdue = await query.CountAsync(item => !terminalStatuses.Contains(item.Status) && (item.Type == WorkItemType.Issue.ToString() ? item.TargetResolutionDate ?? item.DueDate : item.DueDate) < now, cancellationToken),
            Unassigned = await query.CountAsync(item => !item.AssignedResourceId.HasValue && !item.AssignedTeamId.HasValue, cancellationToken),
            DueThisWeek = await query.CountAsync(item => !terminalStatuses.Contains(item.Status) && (item.Type == WorkItemType.Issue.ToString() ? item.TargetResolutionDate ?? item.DueDate : item.DueDate) >= today && (item.Type == WorkItemType.Issue.ToString() ? item.TargetResolutionDate ?? item.DueDate : item.DueDate) < weekEnd, cancellationToken),
            ByStatus = await query.GroupBy(item => item.Status).Select(group => new WorkItemInsightBucketDto { Key = group.Key, Count = group.Count() }).OrderBy(bucket => bucket.Key).ToListAsync(cancellationToken),
            ByPriority = await query.GroupBy(item => item.Priority).Select(group => new WorkItemInsightBucketDto { Key = group.Key, Count = group.Count() }).OrderBy(bucket => bucket.Key).ToListAsync(cancellationToken)
        };
    }

    private IQueryable<WorkItem> ScopedWorkItems()
    {
        var query = _context.Set<WorkItem>().AsQueryable();
        var projectIds = _managementContext.Current?.Scope.ProjectIds?.ToArray();
        return projectIds is null
            ? query.Where(_ => false)
            : query.Where(item => item.ProjectId.HasValue && projectIds.Contains(item.ProjectId.Value));
    }

    private static IQueryable<WorkItem> ApplyFilters(IQueryable<WorkItem> query, Guid? projectId, string? status, string? type, string? priority, string? search, Guid? assignedResourceId, Guid? assignedTeamId, Guid? reporterId, DateTime? dueDateFrom, DateTime? dueDateTo, bool? overdue, bool? unassigned, bool? highPriority, string[] terminalStatusCodes, CancellationToken cancellationToken)
    {
        if (projectId.HasValue) query = query.Where(item => item.ProjectId == projectId.Value);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(item => item.Status == status);
        if (!string.IsNullOrWhiteSpace(type)) query = query.Where(item => item.Type == type);
        if (!string.IsNullOrWhiteSpace(priority)) query = query.Where(item => item.Priority == priority);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim();
            query = query.Where(item => item.Title.Contains(normalizedSearch) || (item.Description != null && item.Description.Contains(normalizedSearch)));
        }
        if (assignedResourceId.HasValue) query = query.Where(item => item.AssignedResourceId == assignedResourceId.Value);
        if (assignedTeamId.HasValue) query = query.Where(item => item.AssignedTeamId == assignedTeamId.Value);
        if (reporterId.HasValue) query = query.Where(item => item.ReporterId == reporterId.Value);
        if (unassigned == true) query = query.Where(item => !item.AssignedResourceId.HasValue && !item.AssignedTeamId.HasValue);
        if (highPriority == true) query = query.Where(item => item.Priority == WorkItemPriority.Critical.ToString() || item.Priority == WorkItemPriority.High.ToString());
        if (dueDateFrom.HasValue)
        {
            var from = dueDateFrom.Value.Date;
            query = query.Where(item => (item.Type == WorkItemType.Issue.ToString() ? item.TargetResolutionDate ?? item.DueDate : item.DueDate) >= from);
        }
        if (dueDateTo.HasValue)
        {
            var toExclusive = dueDateTo.Value.Date.AddDays(1);
            query = query.Where(item => (item.Type == WorkItemType.Issue.ToString() ? item.TargetResolutionDate ?? item.DueDate : item.DueDate) < toExclusive);
        }
        if (overdue == true)
        {
            var now = DateTime.UtcNow;
            // Terminal detection comes from the published workflow IsFinal states
            // passed in by GetTerminalStatusCodesAsync, not from hardcoded codes.
            query = query.Where(item => !terminalStatusCodes.Contains(item.Status) && (item.Type == WorkItemType.Issue.ToString() ? item.TargetResolutionDate ?? item.DueDate : item.DueDate) < now);
        }
        return query;
    }

    private static IQueryable<WorkItem> ApplySort(IQueryable<WorkItem> query, string? sortBy) => sortBy?.Trim().ToLowerInvariant() switch
    {
        "updatedat" => query.OrderByDescending(item => item.UpdatedAt).ThenBy(item => item.Id),
        "title" => query.OrderBy(item => item.Title).ThenBy(item => item.Id),
        "priority" => query.OrderBy(item => item.Priority == WorkItemPriority.Critical.ToString() ? 0 : item.Priority == WorkItemPriority.High.ToString() ? 1 : item.Priority == WorkItemPriority.Medium.ToString() ? 2 : item.Priority == WorkItemPriority.Low.ToString() ? 3 : 4).ThenBy(item => item.Id),
        "status" => query.OrderBy(item => item.Status).ThenBy(item => item.Id),
        "duedate" => query.OrderBy(item => item.Type == WorkItemType.Issue.ToString() ? item.TargetResolutionDate ?? item.DueDate : item.DueDate).ThenBy(item => item.Id),
        _ => query.OrderByDescending(item => item.CreatedAt).ThenBy(item => item.Id)
    };

    /// <summary>
    /// Terminal status codes for the current tenant, derived from the
    /// <c>IsActive &amp;&amp; IsFinal</c> states of the active published versions of
    /// every workflow definition whose <c>EntityType</c> is <c>WorkItem</c> (the
    /// standard compatibility workflow plus custom project-selected workflows).
    /// Falls back to the documented legacy set when no published final state is
    /// visible; see <see cref="WorkItemTerminalStateLookup"/> for the fallback rule.
    /// </summary>
    private Task<string[]> GetTerminalStatusCodesAsync(CancellationToken cancellationToken) =>
        WorkItemTerminalStateLookup.GetAsync(_context, _httpContextAccessor, cancellationToken);

    public async Task<ProjectWorkItemOverviewDto> GetProjectOverviewAsync(Guid projectId, int previewLimit = 3, CancellationToken cancellationToken = default)
    {
        previewLimit = Math.Clamp(previewLimit, 1, 10);
        var issueQuery = ScopedWorkItems().AsNoTracking()
            .Where(item => item.ProjectId == projectId && item.Type == WorkItemType.Issue.ToString());
        var terminalStatusCodes = await GetTerminalStatusCodesAsync(cancellationToken);
        var openIssueQuery = issueQuery
            .Where(item => item.ResolvedDate == null
                && item.CompletedAt == null
                // Terminal detection is driven by published workflow IsFinal
                // definitions (with the documented legacy fallback).
                && !terminalStatusCodes.Contains(item.Status)
                // "Resolved" and "Closed" are legacy Work Item domain statuses,
                // not workflow terminal states; they stay excluded exactly as
                // before so historical project overview numbers do not shift.
                && item.Status != "Resolved"
                && item.Status != "Closed");

        var now = DateTime.UtcNow;
        var groupedStatusCounts = await issueQuery
            .GroupBy(item => item.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);
        var statusReferences = await _statusLookup.GetAsync(cancellationToken: cancellationToken);
        var statusCounts = groupedStatusCounts
            .Select(group =>
            {
                var reference = statusReferences.FirstOrDefault(status => string.Equals(status.Code, group.Status, StringComparison.OrdinalIgnoreCase));
                return new ProjectWorkItemStatusCountDto
                {
                    Status = group.Status,
                    Count = group.Count,
                    NameEn = reference?.NameEn,
                    NameAr = reference?.NameAr,
                    DisplayOrder = reference?.DisplayOrder ?? int.MaxValue,
                    Color = reference?.Color
                };
            })
            .OrderBy(status => status.DisplayOrder)
            .ThenBy(status => status.Status)
            .ToList();
        var openIssueCount = await openIssueQuery.CountAsync(cancellationToken);
        var overdueOpenIssueCount = await openIssueQuery.CountAsync(
            item => (item.TargetResolutionDate ?? item.DueDate) < now,
            cancellationToken);
        var topIssues = await openIssueQuery
            .OrderBy(item => item.TargetResolutionDate == null && item.DueDate == null)
            .ThenBy(item => item.TargetResolutionDate ?? item.DueDate)
            .ThenByDescending(item => item.CreatedAt)
            .Take(previewLimit)
            .Select(item => new ProjectWorkItemOverviewItemDto
            {
                Id = item.Id,
                Title = item.Title,
                Status = item.Status,
                Priority = item.Priority,
                DueDate = item.TargetResolutionDate ?? item.DueDate
            })
            .ToListAsync(cancellationToken);

        return new ProjectWorkItemOverviewDto
        {
            OpenIssueCount = openIssueCount,
            OverdueOpenIssueCount = overdueOpenIssueCount,
            StatusCounts = statusCounts,
            TopIssues = topIssues
        };
    }

    public async Task<Result<WorkItemDto>> CreateAsync(CreateWorkItemDto dto, string userId, CancellationToken cancellationToken = default)
    {
        if (!dto.ProjectId.HasValue)
            return Result<WorkItemDto>.Failure(Error.Validation("A project is required for a Work Item."));
        if (_managementContext.Current?.Scope.ProjectIds.Contains(dto.ProjectId.Value) != true)
            return Result<WorkItemDto>.Failure(Error.NotFound("Project not found"));

        var projectSelection = await _projectWorkflowConfiguration.ResolveForNewRecordAsync(dto.ProjectId, WorkflowDomains.Task, WorkItemWorkflow.EntityType, BuildWorkflowContext(userId), cancellationToken);
        if (projectSelection.IsFailure) return Result<WorkItemDto>.Failure(projectSelection.Error!);
        var workflow = projectSelection.Value is { IsCustom: true }
            ? Result<WorkItemWorkflowProvisioningResult>.Success(new WorkItemWorkflowProvisioningResult(projectSelection.Value.WorkflowDefinitionId!.Value, projectSelection.Value.WorkflowVersionId!.Value, projectSelection.Value.InitialStateCode!, 0, 0))
            : await _workflowProvisioning.EnsureCurrentTenantAsync(userId, cancellationToken);
        if (workflow.IsFailure) return Result<WorkItemDto>.Failure(workflow.Error!);
        if (!string.IsNullOrWhiteSpace(dto.Status) && !string.Equals(dto.Status.Trim(), workflow.Value!.InitialStateCode, StringComparison.OrdinalIgnoreCase))
            return Result<WorkItemDto>.Failure(Error.Validation("A new Work Item starts at the configured workflow initial state."));
        var status = await _statusLookup.FindAsync(workflow.Value!.InitialStateCode, cancellationToken);
        if (status is null || !status.IsActive) return Result<WorkItemDto>.Failure(Error.Validation("The configured workflow initial Work Item status is not active"));
        var validation = await ValidateAsync(dto.Title, dto.Type, dto.Priority, status.Code, dto.ProjectId, dto.ParentId, null, dto.AssignedResourceId, dto.AssignedTeamId, cancellationToken);
        if (validation is not null) return Result<WorkItemDto>.Failure(validation);

        var now = DateTime.UtcNow;
        var item = new WorkItem
        {
            TenantId = GetCurrentTenantId(), Title = dto.Title.Trim(), Description = Normalize(dto.Description), Type = dto.Type.ToString(), Priority = dto.Priority.ToString(), Status = status.Code, Source = WorkItemSource.Internal.ToString(), ProjectId = dto.ProjectId, AssignedResourceId = dto.AssignedResourceId, AssignedTeamId = dto.AssignedTeamId, ReporterId = TryParseGuid(userId), ParentId = dto.ParentId, StoryPoints = dto.StoryPoints, EstimatedHours = dto.EstimatedHours, StartDate = dto.StartDate, DueDate = dto.DueDate, Severity = Normalize(dto.Severity), IdentifiedDate = dto.Type == WorkItemType.Issue ? now : null, TargetResolutionDate = dto.TargetResolutionDate, CompletedAt = IsDone(status) ? now : null, ResolvedDate = dto.Type == WorkItemType.Issue && IsDone(status) ? now : null, Tags = Normalize(dto.Tags), Labels = Normalize(dto.Labels), CreatedBy = userId
        };

        await using var transaction = _context.Database.ProviderName?.Contains("Relational", StringComparison.OrdinalIgnoreCase) == true
            ? await _context.Database.BeginTransactionAsync(cancellationToken)
            : null;
        try
        {
            _context.Set<WorkItem>().Add(item);
            await _context.SaveChangesAsync(cancellationToken);

            var instance = projectSelection.Value is { IsCustom: true } custom
                ? await _workflowInstanceService.ProvisionAsync(WorkItemWorkflow.EntityType, item.Id, custom.WorkflowDefinitionId!.Value, custom.WorkflowVersionId!.Value, BuildWorkflowContext(userId), cancellationToken)
                : await _workflowInstanceService.ProvisionAsync(WorkItemWorkflow.EntityType, item.Id, WorkItemWorkflow.DefinitionCode, BuildWorkflowContext(userId), cancellationToken);
            if (instance.IsFailure)
            {
                if (transaction is not null) await transaction.RollbackAsync(cancellationToken);
                return Result<WorkItemDto>.Failure(instance.Error!);
            }

            await LogActivityAsync(item, "Created", new { type = item.Type, title = item.Title, assignment = await GetTargetLabelAsync(item.ProjectId, item.AssignedResourceId, item.AssignedTeamId, cancellationToken) }, userId, cancellationToken);
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
            return Result<WorkItemDto>.Success(await ToDtoAsync(item, cancellationToken));
        }
        catch
        {
            if (transaction is not null) await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<Result<WorkItemDto>> UpdateAsync(Guid id, UpdateWorkItemDto dto, string userId, CancellationToken cancellationToken = default)
    {
        var item = await ScopedWorkItems().FirstOrDefaultAsync(workItem => workItem.Id == id, cancellationToken);
        if (item is null) return Result<WorkItemDto>.Failure(Error.NotFound("Work item not found"));

        if (!string.IsNullOrWhiteSpace(dto.Status) && !string.Equals(dto.Status.Trim(), item.Status, StringComparison.OrdinalIgnoreCase))
            return Result<WorkItemDto>.Failure(Error.Validation("Work Item status must be changed through a workflow transition."));
        var validation = await ValidateAsync(dto.Title, dto.Type, dto.Priority, item.Status, dto.ProjectId, dto.ParentId, item.Id, dto.AssignedResourceId, dto.AssignedTeamId, cancellationToken);
        if (validation is not null) return Result<WorkItemDto>.Failure(validation);
        if (dto.RowVersion.Length > 0) _context.Entry(item).Property(workItem => workItem.RowVersion).OriginalValue = dto.RowVersion;

        var oldValues = Snapshot(item);
        var oldStatus = item.Status;
        var oldType = item.Type;
        var oldAssignedResourceId = item.AssignedResourceId;
        var oldAssignedTeamId = item.AssignedTeamId;
        var oldProjectId = item.ProjectId;
        item.Title = dto.Title.Trim(); item.Description = Normalize(dto.Description); item.Type = dto.Type.ToString(); item.Priority = dto.Priority.ToString(); item.ProjectId = dto.ProjectId; item.AssignedResourceId = dto.AssignedResourceId; item.AssignedTeamId = dto.AssignedTeamId; item.ParentId = dto.ParentId; item.StoryPoints = dto.StoryPoints; item.EstimatedHours = dto.EstimatedHours; item.ActualHours = dto.ActualHours; item.StartDate = dto.StartDate; item.DueDate = dto.DueDate; item.Severity = Normalize(dto.Severity); item.IdentifiedDate = dto.Type == WorkItemType.Issue ? item.IdentifiedDate ?? DateTime.UtcNow : null; item.TargetResolutionDate = dto.TargetResolutionDate; item.Resolution = Normalize(dto.Resolution); item.UpdatedBy = userId;

        try { await _context.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { return Result<WorkItemDto>.Failure(Error.Conflict("Work item was changed by another user")); }

        var changes = BuildChanges(oldValues, Snapshot(item));
        var assignmentChanged = oldAssignedResourceId != item.AssignedResourceId || oldAssignedTeamId != item.AssignedTeamId;
        if (assignmentChanged)
        {
            changes.Remove("assignedResourceId");
            changes.Remove("assignedTeamId");
            changes["assignment"] = new
            {
                from = await GetTargetLabelAsync(oldProjectId, oldAssignedResourceId, oldAssignedTeamId, cancellationToken),
                to = await GetTargetLabelAsync(item.ProjectId, item.AssignedResourceId, item.AssignedTeamId, cancellationToken)
            };
        }
        if (changes.Count > 0)
        {
            var action = assignmentChanged
                ? oldAssignedResourceId is null && oldAssignedTeamId is null ? "Assigned" : item.AssignedResourceId is null && item.AssignedTeamId is null ? "Unassigned" : "Reassigned"
                : oldStatus == item.Status && oldType == item.Type ? "Updated" : "Transitioned";
            await LogActivityAsync(item, action, changes, userId, cancellationToken);
        }
        return Result<WorkItemDto>.Success(await ToDtoAsync(item, cancellationToken));
    }

    public async Task<Result> DeleteAsync(Guid id, string userId, CancellationToken cancellationToken = default)
    {
        var item = await ScopedWorkItems().FirstOrDefaultAsync(workItem => workItem.Id == id, cancellationToken);
        if (item is null) return Result.Failure(Error.NotFound("Work item not found"));
        item.IsDeleted = true; item.UpdatedBy = userId;
        await _context.SaveChangesAsync(cancellationToken);
        await LogActivityAsync(item, "Deleted", new { title = item.Title }, userId, cancellationToken);
        return Result.Success();
    }

    public async Task<Result<PagedResult<WorkItemActivityDto>>> GetActivityAsync(Guid id, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var exists = await ScopedWorkItems().AnyAsync(workItem => workItem.Id == id, cancellationToken);
        if (!exists) return Result<PagedResult<WorkItemActivityDto>>.Failure(Error.NotFound("Work item not found"));
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);
        var query = _context.Set<AuditEntry>().Where(entry => entry.EntityType == "WorkItem" && entry.EntityId == id);
        var total = await query.CountAsync(cancellationToken);
        var entries = await query.OrderByDescending(entry => entry.Timestamp).ThenByDescending(entry => entry.Id).Skip((page - 1) * pageSize).Take(pageSize).Select(entry => new WorkItemActivityDto { Id = entry.Id, Action = entry.Action, Changes = entry.Changes, UserId = entry.UserId, UserName = entry.UserName, Timestamp = entry.Timestamp }).ToListAsync(cancellationToken);
        return Result<PagedResult<WorkItemActivityDto>>.Success(new PagedResult<WorkItemActivityDto>(entries, page, pageSize, total));
    }

    public async Task<Result<PagedResult<WorkItemCommentDto>>> GetCommentsAsync(Guid id, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var item = await GetCommentableWorkItemAsync(id, cancellationToken);
        if (item is null) return Result<PagedResult<WorkItemCommentDto>>.Failure(Error.NotFound("Work item not found"));

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);
        var rootsQuery = _context.Set<WorkItemComment>()
            .Where(comment => comment.WorkItemId == id && !comment.ParentCommentId.HasValue);
        var total = await rootsQuery.CountAsync(cancellationToken);
        var roots = await rootsQuery
            .OrderByDescending(comment => comment.CreatedAt)
            .ThenByDescending(comment => comment.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        var rootIds = roots.Select(comment => comment.Id).ToArray();
        var replies = rootIds.Length == 0
            ? new List<WorkItemComment>()
            : await _context.Set<WorkItemComment>()
                .Where(comment => comment.WorkItemId == id && comment.ParentCommentId.HasValue && rootIds.Contains(comment.ParentCommentId.Value))
                .OrderByDescending(comment => comment.CreatedAt)
                .ThenByDescending(comment => comment.Id)
                .ToListAsync(cancellationToken);
        var comments = roots.Concat(replies).ToList();
        return Result<PagedResult<WorkItemCommentDto>>.Success(new PagedResult<WorkItemCommentDto>(await ToCommentDtosAsync(comments, cancellationToken), page, pageSize, total));
    }

    public async Task<Result<WorkItemCommentDto>> AddCommentAsync(Guid id, CreateWorkItemCommentDto dto, string userId, CancellationToken cancellationToken = default)
    {
        var item = await GetCommentableWorkItemAsync(id, cancellationToken);
        if (item is null) return Result<WorkItemCommentDto>.Failure(Error.NotFound("Work item not found"));
        if (!await CanCommentAsync(item, userId, cancellationToken)) return Result<WorkItemCommentDto>.Failure(Error.Forbidden("You are not allowed to comment on this task"));

        var commentText = dto.CommentText?.Trim();
        if (string.IsNullOrWhiteSpace(commentText)) return Result<WorkItemCommentDto>.Failure(Error.Validation("Comment text is required"));
        if (commentText.Length > 4000) return Result<WorkItemCommentDto>.Failure(Error.Validation("Comment text must not exceed 4000 characters"));

        if (dto.ParentCommentId.HasValue)
        {
            var parent = await _context.Set<WorkItemComment>()
                .FirstOrDefaultAsync(comment => comment.Id == dto.ParentCommentId.Value && comment.WorkItemId == id, cancellationToken);
            if (parent is null) return Result<WorkItemCommentDto>.Failure(Error.NotFound("Parent comment not found"));
            if (parent.ParentCommentId.HasValue) return Result<WorkItemCommentDto>.Failure(Error.Validation("Replies can only be one level deep"));
        }

        var comment = new WorkItemComment
        {
            TenantId = item.TenantId,
            WorkItemId = item.Id,
            ParentCommentId = dto.ParentCommentId,
            CommentText = commentText,
            CreatedBy = userId
        };
        _context.Set<WorkItemComment>().Add(comment);
        await _context.SaveChangesAsync(cancellationToken);
        return Result<WorkItemCommentDto>.Success((await ToCommentDtosAsync(new[] { comment }, cancellationToken))[0]);
    }

    public async Task<Result<IReadOnlyList<WorkItemAttachmentDto>>> GetAttachmentsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var item = await GetAttachableWorkItemAsync(id, cancellationToken);
        if (item is null) return Result<IReadOnlyList<WorkItemAttachmentDto>>.Failure(Error.NotFound("Work item not found"));

        var attachments = await _context.Set<WorkItemAttachment>()
            .Where(attachment => attachment.WorkItemId == id)
            .OrderBy(attachment => attachment.CreatedAt)
            .ThenBy(attachment => attachment.Id)
            .ToListAsync(cancellationToken);
        return Result<IReadOnlyList<WorkItemAttachmentDto>>.Success(attachments.Select(ToAttachmentDto).ToList());
    }

    public async Task<Result<IReadOnlyList<WorkItemAttachmentDto>>> AddAttachmentsAsync(Guid id, IReadOnlyList<IFormFile> files, string userId, CancellationToken cancellationToken = default)
    {
        var item = await GetAttachableWorkItemAsync(id, cancellationToken);
        if (item is null) return Result<IReadOnlyList<WorkItemAttachmentDto>>.Failure(Error.NotFound("Work item not found"));
        if (!CanManageAttachments(item, userId)) return Result<IReadOnlyList<WorkItemAttachmentDto>>.Failure(Error.Forbidden("You are not allowed to add attachments to this work item"));
        if (files.Count == 0) return Result<IReadOnlyList<WorkItemAttachmentDto>>.Failure(Error.Validation("Choose at least one file"));

        var allowedContentTypes = (_attachmentOptions.AllowedContentTypes ?? Array.Empty<string>())
            .Where(contentType => !string.IsNullOrWhiteSpace(contentType))
            .Select(contentType => contentType.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (allowedContentTypes.Count == 0) return Result<IReadOnlyList<WorkItemAttachmentDto>>.Failure(Error.Validation("No attachment file types are configured"));

        foreach (var file in files)
        {
            var fileName = Path.GetFileName(file.FileName);
            var contentType = file.ContentType?.Trim() ?? string.Empty;
            if (file.Length <= 0) return Result<IReadOnlyList<WorkItemAttachmentDto>>.Failure(Error.Validation("Empty files cannot be attached"));
            if (file.Length > _attachmentOptions.MaximumFileSizeBytes) return Result<IReadOnlyList<WorkItemAttachmentDto>>.Failure(Error.Validation("One or more files exceed the configured attachment size limit"));
            if (string.IsNullOrWhiteSpace(fileName) || fileName.Length > 255) return Result<IReadOnlyList<WorkItemAttachmentDto>>.Failure(Error.Validation("Attachment file name is invalid"));
            if (!allowedContentTypes.Contains(contentType)) return Result<IReadOnlyList<WorkItemAttachmentDto>>.Failure(Error.Validation("One or more attachment file types are not allowed"));
        }

        var savedPaths = new List<string>();
        var attachments = new List<WorkItemAttachment>();
        try
        {
            foreach (var file in files)
            {
                var fileName = Path.GetFileName(file.FileName);
                var contentType = file.ContentType?.Trim() ?? string.Empty;

                await using var input = file.OpenReadStream();
                await using var buffer = new MemoryStream();
                await input.CopyToAsync(buffer, cancellationToken);
                var storageFileName = $"{Guid.NewGuid():N}{Path.GetExtension(fileName).ToLowerInvariant()}";
                var storagePath = await _attachmentStorage.SaveAsync(storageFileName, buffer.ToArray(), $"work-items/{item.TenantId:N}/{item.Id:N}", cancellationToken);
                savedPaths.Add(storagePath);
                attachments.Add(new WorkItemAttachment
                {
                    TenantId = item.TenantId,
                    WorkItemId = item.Id,
                    FileName = fileName,
                    ContentType = contentType,
                    FileSize = file.Length,
                    StoragePath = storagePath,
                    CreatedBy = userId
                });
            }

            _context.Set<WorkItemAttachment>().AddRange(attachments);
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            foreach (var path in savedPaths)
            {
                try { await _attachmentStorage.DeleteAsync(path, cancellationToken); } catch { }
            }
            throw;
        }

        // Audit after the attachment rows are committed so a failed audit write never
        // deletes stored files for attachments that already exist in the database.
        foreach (var attachment in attachments)
        {
            await LogAttachmentAsync(item, attachment, "AttachmentAdded", userId, cancellationToken);
        }

        return Result<IReadOnlyList<WorkItemAttachmentDto>>.Success(attachments.Select(ToAttachmentDto).ToList());
    }

    public async Task<Result<WorkItemAttachmentContent>> GetAttachmentContentAsync(Guid id, Guid attachmentId, CancellationToken cancellationToken = default)
    {
        var item = await GetAttachableWorkItemAsync(id, cancellationToken);
        if (item is null) return Result<WorkItemAttachmentContent>.Failure(Error.NotFound("Work item not found"));

        var attachment = await _context.Set<WorkItemAttachment>().FirstOrDefaultAsync(candidate => candidate.Id == attachmentId && candidate.WorkItemId == item.Id, cancellationToken);
        if (attachment is null) return Result<WorkItemAttachmentContent>.Failure(Error.NotFound("Attachment not found"));
        try
        {
            return Result<WorkItemAttachmentContent>.Success(new WorkItemAttachmentContent
            {
                Content = await _attachmentStorage.GetAsync(attachment.StoragePath, cancellationToken),
                FileName = attachment.FileName,
                ContentType = attachment.ContentType
            });
        }
        catch (FileNotFoundException)
        {
            return Result<WorkItemAttachmentContent>.Failure(Error.NotFound("Attachment file not found"));
        }
    }

    public async Task<Result> DeleteAttachmentAsync(Guid id, Guid attachmentId, string userId, CancellationToken cancellationToken = default)
    {
        var item = await GetAttachableWorkItemAsync(id, cancellationToken);
        if (item is null) return Result.Failure(Error.NotFound("Work item not found"));
        if (!CanManageAttachments(item, userId)) return Result.Failure(Error.Forbidden("You are not allowed to remove attachments from this work item"));

        var attachment = await _context.Set<WorkItemAttachment>().FirstOrDefaultAsync(candidate => candidate.Id == attachmentId && candidate.WorkItemId == item.Id, cancellationToken);
        if (attachment is null) return Result.Failure(Error.NotFound("Attachment not found"));
        try { await _attachmentStorage.DeleteAsync(attachment.StoragePath, cancellationToken); } catch (FileNotFoundException) { }
        attachment.IsDeleted = true;
        attachment.UpdatedBy = userId;
        await _context.SaveChangesAsync(cancellationToken);
        await LogAttachmentAsync(item, attachment, "AttachmentRemoved", userId, cancellationToken);
        return Result.Success();
    }

    private async Task<Error?> ValidateAsync(string title, WorkItemType type, WorkItemPriority priority, string status, Guid? projectId, Guid? parentId, Guid? updatingWorkItemId, Guid? assignedResourceId, Guid? assignedTeamId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(title)) return Error.Validation("Title is required");
        if (title.Trim().Length > 500) return Error.Validation("Title must not exceed 500 characters");
        if (!Enum.IsDefined(type)) return Error.Validation("Work item type is invalid");
        if (!Enum.IsDefined(priority)) return Error.Validation("Work item priority is invalid");
        if (string.IsNullOrWhiteSpace(status)) return Error.Validation("Work item status is invalid");
        if (projectId.HasValue && await _projectLookup.FindAsync(projectId.Value, cancellationToken) is null) return Error.NotFound("Project not found");
        var allowedProjectIds = _managementContext.Current?.Scope.ProjectIds;
        if (allowedProjectIds is null || !projectId.HasValue || !allowedProjectIds.Contains(projectId.Value)) return Error.Forbidden("The project is outside the current management scope");
        if (parentId.HasValue)
        {
            if (updatingWorkItemId == parentId.Value) return Error.Validation("A work item cannot be its own parent.");
            var parent = await _context.Set<WorkItem>().FirstOrDefaultAsync(item => item.Id == parentId.Value && !item.IsDeleted, cancellationToken);
            if (parent is null) return Error.NotFound("Parent work item not found.");
            if (parent.ProjectId != projectId) return Error.Validation("Parent and child work items must belong to the same project.");
        }
        if (assignedResourceId.HasValue && assignedTeamId.HasValue)
            return Error.Validation("A Work Item can be assigned to one Resource or one Team, not both.");
        if (assignedResourceId.HasValue || assignedTeamId.HasValue)
        {
            if (!projectId.HasValue) return Error.Validation("Select a project before assigning a Work Item");
            if (assignedResourceId.HasValue && !await _assigneeLookup.IsActiveProjectMemberAsync(projectId.Value, assignedResourceId.Value, cancellationToken))
                return Error.Validation("Selected Resource is not an active member of this project.");
            if (assignedTeamId.HasValue && !await _assigneeLookup.IsActiveProjectTeamAsync(projectId.Value, assignedTeamId.Value, cancellationToken))
                return Error.Validation("Selected Team is not an active member of this project.");
        }
        return null;
    }

    private Task<WorkItem?> GetCommentableWorkItemAsync(Guid id, CancellationToken cancellationToken) =>
        ScopedWorkItems().FirstOrDefaultAsync(workItem => workItem.Id == id && (workItem.Type == WorkItemType.Task.ToString() || workItem.Type == WorkItemType.Issue.ToString()), cancellationToken);

    private Task<WorkItem?> GetAttachableWorkItemAsync(Guid id, CancellationToken cancellationToken) =>
        ScopedWorkItems().FirstOrDefaultAsync(workItem => workItem.Id == id && (workItem.Type == WorkItemType.Task.ToString() || workItem.Type == WorkItemType.Issue.ToString()), cancellationToken);

    private bool CanManageAttachments(WorkItem item, string userId) =>
        _httpContextAccessor.HttpContext?.User.HasClaim("permission", "WorkItem.Edit") == true || string.Equals(item.CreatedBy, userId, StringComparison.OrdinalIgnoreCase);

    private async Task<IReadOnlyList<WorkItemCommentDto>> ToCommentDtosAsync(IEnumerable<WorkItemComment> comments, CancellationToken cancellationToken)
    {
        var commentList = comments.ToList();
        var names = await _commentAuthorLookup.GetNamesAsync(commentList.Select(comment => comment.CreatedBy), cancellationToken);

        return commentList.Select(comment => new WorkItemCommentDto
        {
            Id = comment.Id,
            WorkItemId = comment.WorkItemId,
            ParentCommentId = comment.ParentCommentId,
            CommentText = comment.CommentText,
            CreatedBy = comment.CreatedBy,
            AuthorName = names.GetValueOrDefault(comment.CreatedBy),
            CreatedAt = comment.CreatedAt
        }).ToList();
    }

    private async Task<bool> CanCommentAsync(WorkItem item, string userId, CancellationToken cancellationToken)
    {
        var currentUser = TryParseGuid(userId);
        if (_httpContextAccessor.HttpContext?.User.HasClaim("permission", "WorkItem.Edit") == true) return true;
        if (string.Equals(item.CreatedBy, userId, StringComparison.OrdinalIgnoreCase)) return true;
        if (!currentUser.HasValue) return false;
        var currentResource = currentUser.HasValue ? await _resourceLookup.FindByUserIdAsync(currentUser.Value, cancellationToken) : null;
        if (item.ReporterId == currentUser || (currentResource?.Id == item.AssignedResourceId)) return true;
        if (currentResource is not null && item.AssignedTeamId.HasValue && await _assigneeLookup.IsCurrentTeamMemberAsync(item.AssignedTeamId.Value, currentResource.Id, cancellationToken)) return true;
        return await _commentAccess.CanCommentAsync(item, currentUser.Value, cancellationToken);
    }

    private async Task<WorkItemStatusReference?> ResolveStatusAsync(string? requestedCode, string? existingCode, bool allowCurrentInactive, CancellationToken cancellationToken)
    {
        var code = string.IsNullOrWhiteSpace(requestedCode) ? existingCode : requestedCode.Trim();
        var status = string.IsNullOrWhiteSpace(code)
            ? (await _statusLookup.GetAsync(false, cancellationToken)).OrderByDescending(candidate => candidate.IsDefault).ThenBy(candidate => candidate.DisplayOrder).FirstOrDefault()
            : await _statusLookup.FindAsync(code, cancellationToken);
        if (status is null || (!status.IsActive && !(allowCurrentInactive && string.Equals(status.Code, existingCode, StringComparison.Ordinal)))) return null;
        return status;
    }

    private async Task<WorkItemDto> ToDtoAsync(WorkItem item, CancellationToken cancellationToken)
    {
        var project = item.ProjectId.HasValue ? await _projectLookup.FindAsync(item.ProjectId.Value, cancellationToken) : null;
        var resourceName = item.AssignedResourceId.HasValue ? (await _resourceLookup.FindAsync(item.AssignedResourceId.Value, cancellationToken))?.Name : null;
        var teamName = item.AssignedTeamId.HasValue && item.ProjectId.HasValue
            ? (await _assigneeLookup.GetEligibleTeamsAsync(item.ProjectId.Value, cancellationToken)).FirstOrDefault(team => team.TeamId == item.AssignedTeamId.Value)?.Name
            : null;
        return ToDto(item, project?.Name, project?.PublicId, resourceName, teamName);
    }

    private static WorkItemDto ToDto(WorkItem item, string? projectName, Guid? projectPublicId = null, string? resourceName = null, string? teamName = null) => new()
    {
        Id = item.Id, Title = item.Title, Description = item.Description, Type = Parse(item.Type, WorkItemType.Task), Priority = Parse(item.Priority, WorkItemPriority.Medium), Status = item.Status, Source = item.Source, ProjectId = item.ProjectId, ProjectPublicId = projectPublicId, ProjectName = projectName, AssignedResourceId = item.AssignedResourceId, AssignedResourceName = resourceName, AssignedTeamId = item.AssignedTeamId, AssignedTeamName = teamName, ReporterId = item.ReporterId, ParentId = item.ParentId, StoryPoints = item.StoryPoints, EstimatedHours = item.EstimatedHours, ActualHours = item.ActualHours, StartDate = item.StartDate, DueDate = item.DueDate, CompletedAt = item.CompletedAt, Severity = item.Severity, IdentifiedDate = item.IdentifiedDate, TargetResolutionDate = item.TargetResolutionDate, ResolvedDate = item.ResolvedDate, Resolution = item.Resolution, Tags = item.Tags, Labels = item.Labels, CreatedAt = item.CreatedAt, UpdatedAt = item.UpdatedAt, CreatedBy = item.CreatedBy, UpdatedBy = item.UpdatedBy, RowVersion = item.RowVersion
    };

    private static WorkItemAttachmentDto ToAttachmentDto(WorkItemAttachment attachment) => new()
    {
        Id = attachment.Id,
        WorkItemId = attachment.WorkItemId,
        FileName = attachment.FileName,
        ContentType = attachment.ContentType,
        FileSize = attachment.FileSize,
        CreatedBy = attachment.CreatedBy,
        CreatedAt = attachment.CreatedAt
    };

    private async Task LogActivityAsync(WorkItem item, string action, object changes, string userId, CancellationToken cancellationToken)
    {
        var actorName = _httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.Name)?.Value;
        await _auditService.LogAsync(new AuditEntry { TenantId = item.TenantId, EntityType = "WorkItem", EntityId = item.Id, Action = action, Changes = JsonSerializer.Serialize(changes), UserId = TryParseGuid(userId), UserName = actorName, Timestamp = DateTime.UtcNow, CreatedBy = userId }, cancellationToken);
    }

    /// <summary>
    /// Attachment events are attributed to the parent work item's project and never
    /// carry the server storage path, which is an internal implementation detail.
    /// </summary>
    private async Task LogAttachmentAsync(WorkItem item, WorkItemAttachment attachment, string action, string userId, CancellationToken cancellationToken)
    {
        var actorName = _httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.Name)?.Value;
        await _auditService.LogAsync(new AuditEntry
        {
            TenantId = item.TenantId,
            ProjectId = item.ProjectId,
            EntityType = "WorkItemAttachment",
            EntityId = attachment.Id,
            Action = action,
            Changes = JsonSerializer.Serialize(new
            {
                attachment.WorkItemId,
                attachment.FileName,
                attachment.ContentType,
                attachment.FileSize
            }),
            UserId = TryParseGuid(userId),
            UserName = actorName,
            Timestamp = DateTime.UtcNow,
            CreatedBy = userId
        }, cancellationToken);
    }

    private static Dictionary<string, object?> BuildChanges(Dictionary<string, object?> oldValues, Dictionary<string, object?> newValues) => newValues.Where(pair => !Equals(oldValues.GetValueOrDefault(pair.Key), pair.Value)).ToDictionary(pair => pair.Key, pair => (object?)new { from = oldValues.GetValueOrDefault(pair.Key), to = pair.Value });

    private static Dictionary<string, object?> Snapshot(WorkItem item) => new() { ["title"] = item.Title, ["description"] = item.Description, ["type"] = item.Type, ["priority"] = item.Priority, ["status"] = item.Status, ["projectId"] = item.ProjectId, ["assignedResourceId"] = item.AssignedResourceId, ["assignedTeamId"] = item.AssignedTeamId, ["dueDate"] = item.DueDate, ["severity"] = item.Severity, ["targetResolutionDate"] = item.TargetResolutionDate, ["resolvedDate"] = item.ResolvedDate, ["resolution"] = item.Resolution };

    private async Task<string> GetTargetLabelAsync(Guid? projectId, Guid? resourceId, Guid? teamId, CancellationToken cancellationToken)
    {
        if (resourceId.HasValue) return (await _resourceLookup.FindAsync(resourceId.Value, cancellationToken))?.Name ?? "Unknown Resource";
        if (teamId.HasValue && projectId.HasValue) return (await _assigneeLookup.GetEligibleTeamsAsync(projectId.Value, cancellationToken)).FirstOrDefault(team => team.TeamId == teamId.Value)?.Name ?? "Unknown Team";
        return "Unassigned";
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static Guid? TryParseGuid(string? value) => Guid.TryParse(value, out var parsed) ? parsed : null;
    private static bool IsDone(WorkItemStatusReference status) => string.Equals(status.Code, "Done", StringComparison.OrdinalIgnoreCase);
    private WorkflowExecutionContext BuildWorkflowContext(string userId)
    {
        var principal = _httpContextAccessor.HttpContext?.User;
        var tenantId = Guid.TryParse(principal?.FindFirst("tenantId")?.Value, out var parsedTenant) ? parsedTenant : GetCurrentTenantId();
        var permissions = principal?.FindFirst("permissions")?.Value;
        var roles = principal?.FindFirst("roles")?.Value;
        return new WorkflowExecutionContext(
            tenantId,
            userId,
            principal?.FindFirst(ClaimTypes.Name)?.Value ?? principal?.FindFirst("name")?.Value,
            principal?.FindFirst("readOnly")?.Value == "true" || principal?.FindFirst("viewAs")?.Value == "true",
            principal?.FindFirst("correlationId")?.Value,
            null,
            new Dictionary<string, string?> { ["permissions"] = permissions, ["roles"] = roles });
    }
    private Guid GetCurrentTenantId()
    {
        var tenantId = _httpContextAccessor.HttpContext?.User.FindFirst("tenantId")?.Value;
        return Guid.TryParse(tenantId, out var parsed) && parsed != Guid.Empty
            ? parsed
            : throw new InvalidOperationException("A trusted tenant context is required.");
    }
    private static T Parse<T>(string value, T fallback) where T : struct, Enum => Enum.TryParse<T>(value, true, out var parsed) ? parsed : fallback;
}
