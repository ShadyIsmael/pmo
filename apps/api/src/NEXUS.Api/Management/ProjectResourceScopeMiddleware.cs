using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using NEXUS.Modules.CostManagement.Domain;
using NEXUS.Modules.Documents.Domain;
using NEXUS.Modules.IssueManagement.Domain;
using NEXUS.Modules.Meetings.Domain;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.Modules.ProjectPlanning.Domain;
using NEXUS.Modules.ResourceManagement.Domain;
using NEXUS.Modules.RiskManagement.Domain;
using NEXUS.Modules.WorkItems.Domain;
using NEXUS.Persistence;
using NEXUS.SharedKernel.Management;

namespace NEXUS.Api.Management;

/// <summary>
/// Adds object-level management-scope enforcement to project-linked resources
/// whose public endpoints address the child object directly (rather than through
/// /api/projects/{projectId}/...).
/// </summary>
public sealed class ProjectResourceScopeMiddleware
{
    private readonly RequestDelegate _next;

    public ProjectResourceScopeMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext httpContext, AppDbContext db, IManagementContextAccessor managementContext)
    {
        if (httpContext.User.Identity?.IsAuthenticated != true)
        {
            await _next(httpContext);
            return;
        }

        var path = httpContext.Request.Path.Value?.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];
        // Collection endpoints (for example POST /api/work-items) still need
        // project-scope validation from the request body even without a resource id.
        if (path.Length < 2 || !path[0].Equals("api", StringComparison.OrdinalIgnoreCase))
        {
            await _next(httpContext);
            return;
        }

        var current = managementContext.Current;
        // Assigned Change Request approvals are resource-scoped by the current
        // approval assignment in the workflow service. They must not require
        // ordinary project scope, which would incorrectly prevent a reviewer
        // from opening or deciding an approval without granting project access.
        var assignedChangeRequestRoute = IsAssignedChangeRequestApprovalRoute(httpContext, path);
        var resourceId = assignedChangeRequestRoute ? null : GetResourceId(httpContext, path);
        var hasNestedProjectResource = IsNestedProjectResource(path);
        var inspectProjectBody = ShouldInspectProjectBody(httpContext, path);
        if (current is null && (resourceId.HasValue || hasNestedProjectResource || inspectProjectBody))
        {
            await WriteNotFoundAsync(httpContext);
            return;
        }

        if (current is not null && hasNestedProjectResource
            && !await HasValidNestedOwnershipAsync(db, httpContext, path, current.TenantId))
        {
            await WriteNotFoundAsync(httpContext);
            return;
        }

        if (resourceId.HasValue && current is not null)
        {
            var isBudgetRevisionByBudget = path[1].Equals("budget-revisions", StringComparison.OrdinalIgnoreCase)
                && path.Length >= 4 && path[2].Equals("by-budget", StringComparison.OrdinalIgnoreCase);
            var owner = isBudgetRevisionByBudget
                ? await ResolveBudgetOwnerAsync(db, resourceId.Value, current.TenantId, httpContext.RequestAborted)
                : await ResolveResourceOwnerAsync(db, path[1], resourceId.Value, current.TenantId, httpContext.RequestAborted);
            if (owner is null)
            {
                await WriteNotFoundAsync(httpContext);
                return;
            }
            if (owner.ProjectId.HasValue && !current.Scope.ProjectIds.Contains(owner.ProjectId.Value))
            {
                await WriteNotFoundAsync(httpContext);
                return;
            }
            if (owner.PortfolioId.HasValue && !current.Scope.PortfolioIds.Contains(owner.PortfolioId.Value))
            {
                await WriteNotFoundAsync(httpContext);
                return;
            }
        }

        var bodyProjectId = inspectProjectBody
            ? await ReadProjectIdAsync(httpContext)
            : null;
        if (bodyProjectId == Guid.Empty)
        {
            await WriteNotFoundAsync(httpContext);
            return;
        }
        if (bodyProjectId.HasValue)
        {
            if (current is null || !current.Scope.ProjectIds.Contains(bodyProjectId.Value))
            {
                await WriteNotFoundAsync(httpContext);
                return;
            }
        }

        await _next(httpContext);
    }

    private static bool IsAssignedChangeRequestApprovalRoute(HttpContext context, IReadOnlyList<string> path)
    {
        if (path.Count != 4 || !path[1].Equals("project-change-requests", StringComparison.OrdinalIgnoreCase)) return false;
        if (!Guid.TryParse(Convert.ToString(context.Request.RouteValues["id"]), out _)) return false;
        var action = path[3];
        return (HttpMethods.IsGet(context.Request.Method) && action.Equals("review", StringComparison.OrdinalIgnoreCase))
            || (HttpMethods.IsPost(context.Request.Method)
                && (action.Equals("approve", StringComparison.OrdinalIgnoreCase) || action.Equals("reject", StringComparison.OrdinalIgnoreCase)));
    }

    private static bool ShouldInspectProjectBody(HttpContext context, IReadOnlyList<string> path)
    {
        if (!(HttpMethods.IsPost(context.Request.Method) || HttpMethods.IsPut(context.Request.Method) || HttpMethods.IsPatch(context.Request.Method))) return false;
        if (path.Count < 2 || context.Request.ContentType is null) return false;
        var contentType = context.Request.ContentType;
        if (!contentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase)
            && !contentType.StartsWith("multipart/form-data", StringComparison.OrdinalIgnoreCase)
            && !contentType.StartsWith("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase)) return false;
        return path[1].ToLowerInvariant() is "budgets" or "costs" or "commitments" or "budget-revisions" or "work-items" or "documents" or "time-entries" or "meetings" or "risks" or "issues" or "allocations" or "payment-obligations" or "resource-finder";
    }

    private static bool IsNestedProjectResource(IReadOnlyList<string> path)
    {
        if (path.Count >= 4
            && path[1].Equals("projects", StringComparison.OrdinalIgnoreCase)
            && (path[3].Equals("plans", StringComparison.OrdinalIgnoreCase)
                || path[3].Equals("plan-imports", StringComparison.OrdinalIgnoreCase)
                || path[3].Equals("status-reports", StringComparison.OrdinalIgnoreCase)
                || path[3].Equals("reporting", StringComparison.OrdinalIgnoreCase)
                || path[3].Equals("team", StringComparison.OrdinalIgnoreCase)
                || path[3].Equals("people", StringComparison.OrdinalIgnoreCase)
                || path[3].Equals("stakeholders", StringComparison.OrdinalIgnoreCase)
                || path[3].Equals("snapshots", StringComparison.OrdinalIgnoreCase)))
            return true;

        if (path.Count < 4) return false;
        if (path[1].Equals("risks", StringComparison.OrdinalIgnoreCase))
            return path[3].Equals("impacts", StringComparison.OrdinalIgnoreCase)
                || path[3].Equals("actions", StringComparison.OrdinalIgnoreCase);
        if (path[1].Equals("work-items", StringComparison.OrdinalIgnoreCase))
            return path[3].Equals("attachments", StringComparison.OrdinalIgnoreCase);
        return path[1].Equals("meetings", StringComparison.OrdinalIgnoreCase)
            && (path[3].Equals("attendees", StringComparison.OrdinalIgnoreCase)
                || path[3].Equals("minutes", StringComparison.OrdinalIgnoreCase));
    }

    private static async Task<Guid?> ReadProjectIdAsync(HttpContext context)
    {
        if (!context.Request.Body.CanSeek) context.Request.EnableBuffering();
        var originalPosition = context.Request.Body.Position;
        try
        {
            if (context.Request.ContentType?.StartsWith("application/json", StringComparison.OrdinalIgnoreCase) != true)
            {
                var form = await context.Request.ReadFormAsync(context.RequestAborted);
                if (!form.TryGetValue("projectId", out var formValue)) return null;
                if (formValue.Count != 1) return Guid.Empty;
                var rawFormProjectId = formValue[0];
                if (string.IsNullOrWhiteSpace(rawFormProjectId)) return null;
                return Guid.TryParse(rawFormProjectId, out var formProjectId) ? formProjectId : Guid.Empty;
            }

            using var document = await JsonDocument.ParseAsync(context.Request.Body, new JsonDocumentOptions { MaxDepth = 32 }, context.RequestAborted);
            if (document.RootElement.ValueKind != JsonValueKind.Object) return null;
            var foundProjectId = false;
            Guid? projectId = null;
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (!property.Name.Equals("projectId", StringComparison.OrdinalIgnoreCase)) continue;
                if (foundProjectId) return Guid.Empty;
                foundProjectId = true;
                if (property.Value.ValueKind == JsonValueKind.Null) continue;
                if (property.Value.ValueKind != JsonValueKind.String
                    || !Guid.TryParse(property.Value.GetString(), out var parsedProjectId)) return Guid.Empty;
                projectId = parsedProjectId;
            }
            return projectId;
        }
        catch (Exception exception) when (exception is JsonException or InvalidDataException or IOException)
        {
            return null;
        }
        finally
        {
            context.Request.Body.Position = originalPosition;
        }
    }

    private static async Task<bool> HasValidNestedOwnershipAsync(AppDbContext db, HttpContext context, IReadOnlyList<string> path, Guid tenantId)
    {
        if (path.Count >= 4 && path[1].Equals("work-items", StringComparison.OrdinalIgnoreCase)
            && path[3].Equals("attachments", StringComparison.OrdinalIgnoreCase))
        {
            if (path.Count < 5) return true;
            if (!Guid.TryParse(Convert.ToString(context.Request.RouteValues["attachmentId"]), out var attachmentId)) return false;
            var workItemId = Guid.TryParse(Convert.ToString(context.Request.RouteValues["id"]), out var parsedWorkItemId) ? parsedWorkItemId : Guid.Empty;
            return workItemId != Guid.Empty && await db.Set<WorkItemAttachment>().IgnoreQueryFilters().AsNoTracking()
                .AnyAsync(attachment => attachment.Id == attachmentId && attachment.WorkItemId == workItemId
                    && attachment.TenantId == tenantId && !attachment.IsDeleted, context.RequestAborted);
        }

        if (path.Count >= 4 && path[1].Equals("meetings", StringComparison.OrdinalIgnoreCase)
            && !await HasValidMeetingChildOwnershipAsync(db, context, path, tenantId)) return false;

        if (path.Count >= 4 && path[1].Equals("risks", StringComparison.OrdinalIgnoreCase))
        {
            if (!Guid.TryParse(Convert.ToString(context.Request.RouteValues["riskId"] ?? context.Request.RouteValues["id"]), out var riskId)) return false;

            if (path[3].Equals("impacts", StringComparison.OrdinalIgnoreCase))
            {
                if (path.Count < 5) return true;
                if (!Guid.TryParse(Convert.ToString(context.Request.RouteValues["impactId"]), out var impactId)) return false;
                return await db.Set<RiskImpactAssessment>().IgnoreQueryFilters().AsNoTracking()
                    .AnyAsync(impact => impact.Id == impactId && impact.RiskId == riskId && impact.TenantId == tenantId && !impact.IsDeleted, context.RequestAborted);
            }

            if (path[3].Equals("actions", StringComparison.OrdinalIgnoreCase))
            {
                if (path.Count < 5) return true;
                if (!Guid.TryParse(Convert.ToString(context.Request.RouteValues["actionId"]), out var actionId)) return false;
                return await db.Set<RiskAction>().IgnoreQueryFilters().AsNoTracking()
                    .AnyAsync(action => action.Id == actionId && action.RiskId == riskId && action.TenantId == tenantId && !action.IsDeleted, context.RequestAborted);
            }

            return true;
        }

        if (path.Count < 5 || !path[1].Equals("projects", StringComparison.OrdinalIgnoreCase)) return true;
        var addressedProjectId = ProjectRequestContext.GetInternalProjectId(context);
        if (!addressedProjectId.HasValue) return false;

        if (path[3].Equals("status-reports", StringComparison.OrdinalIgnoreCase)
            && context.Request.RouteValues["reportId"] is { } rawReportId)
        {
            if (!Guid.TryParse(Convert.ToString(rawReportId), out var reportId)) return false;
            var reportBelongsToProject = await db.Set<ProjectStatusReport>().IgnoreQueryFilters().AsNoTracking()
                .AnyAsync(report => report.Id == reportId && report.ProjectId == addressedProjectId.Value
                    && report.TenantId == tenantId && !report.IsDeleted, context.RequestAborted);
            if (!reportBelongsToProject) return false;
        }

        if (path[3].Equals("team", StringComparison.OrdinalIgnoreCase)
            && context.Request.RouteValues["memberId"] is { } rawMemberId)
        {
            if (!Guid.TryParse(Convert.ToString(rawMemberId), out var memberId)) return false;
            var memberBelongsToProject = await db.Set<ProjectMember>().IgnoreQueryFilters().AsNoTracking()
                .AnyAsync(member => member.Id == memberId && member.ProjectId == addressedProjectId.Value
                    && member.TenantId == tenantId && !member.IsDeleted, context.RequestAborted);
            if (!memberBelongsToProject) return false;
        }

        if (path[3].Equals("people", StringComparison.OrdinalIgnoreCase)
            && context.Request.RouteValues["personId"] is { } rawPersonId)
        {
            if (!Guid.TryParse(Convert.ToString(rawPersonId), out var personId)) return false;
            var isOwnedPerson = await db.Set<ProjectMember>().IgnoreQueryFilters().AsNoTracking()
                .AnyAsync(member => member.Id == personId && member.ProjectId == addressedProjectId.Value
                    && member.TenantId == tenantId && !member.IsDeleted, context.RequestAborted);
            isOwnedPerson = isOwnedPerson || await db.Set<ProjectStakeholder>().IgnoreQueryFilters().AsNoTracking()
                .AnyAsync(stakeholder => stakeholder.Id == personId && stakeholder.ProjectId == addressedProjectId.Value
                    && stakeholder.TenantId == tenantId && !stakeholder.IsDeleted, context.RequestAborted);
            if (!isOwnedPerson) return false;
        }

        if (path[3].Equals("stakeholders", StringComparison.OrdinalIgnoreCase)
            && context.Request.RouteValues["stakeholderId"] is { } rawStakeholderId)
        {
            if (!Guid.TryParse(Convert.ToString(rawStakeholderId), out var stakeholderId)) return false;
            var stakeholderBelongsToProject = await db.Set<ProjectStakeholder>().IgnoreQueryFilters().AsNoTracking()
                .AnyAsync(stakeholder => stakeholder.Id == stakeholderId && stakeholder.ProjectId == addressedProjectId.Value
                    && stakeholder.TenantId == tenantId && !stakeholder.IsDeleted, context.RequestAborted);
            if (!stakeholderBelongsToProject) return false;
        }

        if (path[3].Equals("snapshots", StringComparison.OrdinalIgnoreCase)
            && context.Request.RouteValues["snapshotId"] is { } rawSnapshotId)
        {
            if (!Guid.TryParse(Convert.ToString(rawSnapshotId), out var snapshotId)) return false;
            var snapshotBelongsToProject = await db.Set<ProjectSnapshot>().IgnoreQueryFilters().AsNoTracking()
                .AnyAsync(snapshot => snapshot.Id == snapshotId && snapshot.ProjectId == addressedProjectId.Value
                    && snapshot.TenantId == tenantId && !snapshot.IsDeleted, context.RequestAborted);
            if (!snapshotBelongsToProject) return false;
        }

        if (path[3].Equals("plans", StringComparison.OrdinalIgnoreCase)
            && Guid.TryParse(Convert.ToString(context.Request.RouteValues["planId"] ?? context.Request.RouteValues["id"] ?? path.ElementAtOrDefault(4)), out var planId))
        {
            var planProjectId = await db.Set<Plan>().IgnoreQueryFilters().AsNoTracking()
                .Where(plan => plan.Id == planId && plan.TenantId == tenantId && !plan.IsDeleted)
                .Select(plan => (Guid?)plan.ProjectId)
                .FirstOrDefaultAsync(context.RequestAborted);
            if (planProjectId != addressedProjectId) return false;

            foreach (var routeName in new[] { "planVersionId", "fromVersionId", "toVersionId" })
            {
                if (!Guid.TryParse(Convert.ToString(context.Request.RouteValues[routeName]), out var versionId)) continue;
                var belongsToPlan = await db.Set<PlanVersion>().IgnoreQueryFilters().AsNoTracking()
                    .AnyAsync(version => version.Id == versionId && version.PlanId == planId && version.TenantId == tenantId && !version.IsDeleted, context.RequestAborted);
                if (!belongsToPlan) return false;
            }

            if (!await HasValidPlanContentOwnershipAsync(db, context, path, planId, tenantId)) return false;
        }

        if (path.Count >= 6
            && path[3].Equals("plan-imports", StringComparison.OrdinalIgnoreCase)
            && path[4].Equals("sessions", StringComparison.OrdinalIgnoreCase)
            && Guid.TryParse(Convert.ToString(context.Request.RouteValues["sessionId"]), out var sessionId))
        {
            var sessionProjectId = await db.Set<PlanImportSession>().IgnoreQueryFilters().AsNoTracking()
                .Where(session => session.Id == sessionId && session.TenantId == tenantId && !session.IsDeleted)
                .Select(session => (Guid?)session.ProjectId)
                .FirstOrDefaultAsync(context.RequestAborted);
            if (sessionProjectId != addressedProjectId) return false;
        }

        return true;
    }

    private static async Task<bool> HasValidMeetingChildOwnershipAsync(AppDbContext db, HttpContext context, IReadOnlyList<string> path, Guid tenantId)
    {
        if (!Guid.TryParse(Convert.ToString(context.Request.RouteValues["id"]), out var meetingId)) return false;

        if (path[3].Equals("attendees", StringComparison.OrdinalIgnoreCase))
        {
            if (context.Request.RouteValues["participantId"] is not { } rawParticipantId) return true;
            if (!Guid.TryParse(Convert.ToString(rawParticipantId), out var participantId)) return false;
            return await db.Set<MeetingParticipant>().IgnoreQueryFilters().AsNoTracking()
                .AnyAsync(participant => participant.Id == participantId && participant.MeetingId == meetingId
                    && participant.TenantId == tenantId && !participant.IsDeleted, context.RequestAborted);
        }

        if (path.Count < 5 || !path[4].Equals("attachments", StringComparison.OrdinalIgnoreCase)
            && !path[4].Equals("checklist", StringComparison.OrdinalIgnoreCase)
            && !path[4].Equals("work-items", StringComparison.OrdinalIgnoreCase)) return true;

        if (path[4].Equals("attachments", StringComparison.OrdinalIgnoreCase)
            && context.Request.RouteValues["attachmentId"] is { } rawAttachmentId)
        {
            if (!Guid.TryParse(Convert.ToString(rawAttachmentId), out var attachmentId)) return false;
            return await (from attachment in db.Set<MeetingMinutesAttachment>().IgnoreQueryFilters().AsNoTracking()
                          join minutes in db.Set<MeetingMinutes>().IgnoreQueryFilters().AsNoTracking()
                              on attachment.MeetingMinutesId equals minutes.Id
                          where attachment.Id == attachmentId && attachment.TenantId == tenantId && !attachment.IsDeleted
                              && minutes.MeetingId == meetingId && minutes.TenantId == tenantId && !minutes.IsDeleted
                          select attachment.Id).AnyAsync(context.RequestAborted);
        }

        if (path[4].Equals("checklist", StringComparison.OrdinalIgnoreCase)
            && context.Request.RouteValues["itemId"] is { } rawItemId)
        {
            if (!Guid.TryParse(Convert.ToString(rawItemId), out var itemId)) return false;
            return await (from item in db.Set<MeetingMinutesChecklistItem>().IgnoreQueryFilters().AsNoTracking()
                          join minutes in db.Set<MeetingMinutes>().IgnoreQueryFilters().AsNoTracking()
                              on item.MeetingMinutesId equals minutes.Id
                          where item.Id == itemId && item.TenantId == tenantId && !item.IsDeleted
                              && minutes.MeetingId == meetingId && minutes.TenantId == tenantId && !minutes.IsDeleted
                          select item.Id).AnyAsync(context.RequestAborted);
        }

        if (path[4].Equals("work-items", StringComparison.OrdinalIgnoreCase)
            && context.Request.RouteValues["workItemId"] is { } rawWorkItemId)
        {
            if (!Guid.TryParse(Convert.ToString(rawWorkItemId), out var workItemId)) return false;
            return await (from link in db.Set<MeetingMinutesWorkItemLink>().IgnoreQueryFilters().AsNoTracking()
                          join minutes in db.Set<MeetingMinutes>().IgnoreQueryFilters().AsNoTracking()
                              on link.MeetingMinutesId equals minutes.Id
                          where link.WorkItemId == workItemId && link.TenantId == tenantId && !link.IsDeleted
                              && minutes.MeetingId == meetingId && minutes.TenantId == tenantId && !minutes.IsDeleted
                          select link.Id).AnyAsync(context.RequestAborted);
        }

        return true;
    }

    private static async Task<bool> HasValidPlanContentOwnershipAsync(
        AppDbContext db,
        HttpContext context,
        IReadOnlyList<string> path,
        Guid planId,
        Guid tenantId)
    {
        if (path.Count < 6) return true;
        if (path[5].Equals("dependencies", StringComparison.OrdinalIgnoreCase)
            && path.ElementAtOrDefault(6)?.Equals("predecessors", StringComparison.OrdinalIgnoreCase) == true)
            return true;
        if (path.Count >= 7
            && path[5].Equals("calendars", StringComparison.OrdinalIgnoreCase)
            && path[6].Equals("versions", StringComparison.OrdinalIgnoreCase))
            return true;

        // Capacity is a plan-owned collection action, not an assignment detail
        // route. The project and plan ownership checks have already run above;
        // there is intentionally no assignmentId route value to resolve here.
        if (path.Count == 7
            && path[5].Equals("assignments", StringComparison.OrdinalIgnoreCase)
            && path[6].Equals("capacity", StringComparison.OrdinalIgnoreCase)
            && HttpMethods.IsGet(context.Request.Method)
            && context.Request.RouteValues["assignmentId"] is null)
            return true;

        // Batch creation has no assignmentId route value. The project and plan
        // ownership checks above are the parent-resource authorization boundary.
        if (path.Count == 7
            && path[5].Equals("assignments", StringComparison.OrdinalIgnoreCase)
            && path[6].Equals("batch", StringComparison.OrdinalIgnoreCase)
            && HttpMethods.IsPost(context.Request.Method)
            && context.Request.RouteValues["assignmentId"] is null)
            return true;

        var childRoute = path[5].ToLowerInvariant() switch
        {
            "activities" => ("activityId", (Func<Guid, Task<Guid?>>)(async id => await db.Set<PlanActivity>().IgnoreQueryFilters().AsNoTracking()
                .Where(item => item.Id == id && item.TenantId == tenantId && !item.IsDeleted)
                .Select(item => (Guid?)item.PlanVersionId).FirstOrDefaultAsync(context.RequestAborted))),
            "scheduling-analysis" => ("activityId", (Func<Guid, Task<Guid?>>)(async id => await db.Set<PlanActivity>().IgnoreQueryFilters().AsNoTracking()
                .Where(item => item.Id == id && item.TenantId == tenantId && !item.IsDeleted)
                .Select(item => (Guid?)item.PlanVersionId).FirstOrDefaultAsync(context.RequestAborted))),
            "wbs" => ("nodeId", (Func<Guid, Task<Guid?>>)(async id => await db.Set<PlanWbsNode>().IgnoreQueryFilters().AsNoTracking()
                .Where(item => item.Id == id && item.TenantId == tenantId && !item.IsDeleted)
                .Select(item => (Guid?)item.PlanVersionId).FirstOrDefaultAsync(context.RequestAborted))),
            "dependencies" => ("dependencyId", (Func<Guid, Task<Guid?>>)(async id => await db.Set<PlanDependency>().IgnoreQueryFilters().AsNoTracking()
                .Where(item => item.Id == id && item.TenantId == tenantId && !item.IsDeleted)
                .Select(item => (Guid?)item.PlanVersionId).FirstOrDefaultAsync(context.RequestAborted))),
            "assignments" => ("assignmentId", (Func<Guid, Task<Guid?>>)(async id => await db.Set<PlanAssignment>().IgnoreQueryFilters().AsNoTracking()
                .Where(item => item.Id == id && item.TenantId == tenantId && !item.IsDeleted)
                .Select(item => (Guid?)item.PlanVersionId).FirstOrDefaultAsync(context.RequestAborted))),
            "calendars" => ("calendarId", (Func<Guid, Task<Guid?>>)(async id => await db.Set<PlanCalendar>().IgnoreQueryFilters().AsNoTracking()
                .Where(item => item.Id == id && item.TenantId == tenantId && !item.IsDeleted)
                .Select(item => (Guid?)item.PlanVersionId).FirstOrDefaultAsync(context.RequestAborted))),
            _ => default
        };

        if (childRoute.Item2 is null) return true;
        var rawChildId = context.Request.RouteValues[childRoute.Item1];
        if (rawChildId is null) return path.Count <= 6;
        if (!Guid.TryParse(Convert.ToString(rawChildId), out var childId)) return false;

        var versionId = await childRoute.Item2(childId);
        if (!versionId.HasValue) return false;
        return await db.Set<PlanVersion>().IgnoreQueryFilters().AsNoTracking()
            .AnyAsync(version => version.Id == versionId.Value && version.PlanId == planId
                && version.TenantId == tenantId && !version.IsDeleted, context.RequestAborted);
    }

    private static Guid? GetResourceId(HttpContext context, IReadOnlyList<string> path)
    {
        // Budget revisions expose a collection lookup through a budget identifier.
        if (path[1].Equals("budget-revisions", StringComparison.OrdinalIgnoreCase)
            && path.Count >= 4
            && path[2].Equals("by-budget", StringComparison.OrdinalIgnoreCase)
            && Guid.TryParse(Convert.ToString(context.Request.RouteValues["budgetId"]), out var budgetId))
            return budgetId;

        var routeValue = context.Request.RouteValues["id"] ?? context.Request.RouteValues["riskId"];
        if (!Guid.TryParse(Convert.ToString(routeValue), out var id)) return null;

        return path[1].ToLowerInvariant() switch
        {
            "budgets" or "costs" or "commitments" or "budget-revisions" or "baselines" or "milestones" or "risks"
                or "time-entries" or "work-items" or "documents" or "meetings" or "issues"
                or "project-change-requests" or "allocations" or "payment-obligations" => id,
            _ => null
        };
    }

    private static async Task<ResourceOwner?> ResolveResourceOwnerAsync(AppDbContext db, string resource, Guid id, Guid tenantId, CancellationToken cancellationToken)
    {
        return resource.ToLowerInvariant() switch
        {
            "budgets" => await db.Set<Budget>().IgnoreQueryFilters().AsNoTracking().Where(item => item.Id == id && item.TenantId == tenantId && !item.IsDeleted).Select(item => new ResourceOwner(item.ProjectId, item.PortfolioId)).FirstOrDefaultAsync(cancellationToken),
            "costs" => await ResolveCostOwnerAsync(db, id, tenantId, cancellationToken),
            "commitments" => await ResolveCommitmentOwnerAsync(db, id, tenantId, cancellationToken),
            "budget-revisions" => await db.Set<BudgetRevision>().IgnoreQueryFilters().AsNoTracking().Where(item => item.Id == id && item.TenantId == tenantId && !item.IsDeleted).Select(item => new ResourceOwner(item.ProjectId, null)).FirstOrDefaultAsync(cancellationToken),
            "baselines" => await db.Set<Baseline>().IgnoreQueryFilters().AsNoTracking().Where(item => item.Id == id && item.TenantId == tenantId && !item.IsDeleted).Select(item => new ResourceOwner(item.ProjectId, null)).FirstOrDefaultAsync(cancellationToken),
            "milestones" => await db.Set<Milestone>().IgnoreQueryFilters().AsNoTracking().Where(item => item.Id == id && item.TenantId == tenantId && !item.IsDeleted).Select(item => new ResourceOwner(item.ProjectId, null)).FirstOrDefaultAsync(cancellationToken),
            "risks" => await db.Set<Risk>().IgnoreQueryFilters().AsNoTracking().Where(item => item.Id == id && item.TenantId == tenantId && !item.IsDeleted).Select(item => new ResourceOwner((Guid?)item.ProjectId, null)).FirstOrDefaultAsync(cancellationToken),
            "time-entries" => await db.Set<TimeEntry>().IgnoreQueryFilters().AsNoTracking().Where(item => item.Id == id && item.TenantId == tenantId && !item.IsDeleted).Select(item => new ResourceOwner(item.ProjectId, null)).FirstOrDefaultAsync(cancellationToken),
            "work-items" => await db.Set<WorkItem>().IgnoreQueryFilters().AsNoTracking().Where(item => item.Id == id && item.TenantId == tenantId && !item.IsDeleted).Select(item => new ResourceOwner(item.ProjectId, null)).FirstOrDefaultAsync(cancellationToken),
            "documents" => await db.Set<Document>().IgnoreQueryFilters().AsNoTracking().Where(item => item.Id == id && item.TenantId == tenantId && !item.IsDeleted).Select(item => new ResourceOwner(item.ProjectId, null)).FirstOrDefaultAsync(cancellationToken),
            "meetings" => await db.Set<Meeting>().IgnoreQueryFilters().AsNoTracking().Where(item => item.Id == id && item.TenantId == tenantId && !item.IsDeleted).Select(item => new ResourceOwner(item.ProjectId, null)).FirstOrDefaultAsync(cancellationToken),
            "issues" => await db.Set<Issue>().IgnoreQueryFilters().AsNoTracking().Where(item => item.Id == id && item.TenantId == tenantId && !item.IsDeleted).Select(item => new ResourceOwner(item.ProjectId, null)).FirstOrDefaultAsync(cancellationToken),
            "project-change-requests" => await db.Set<ProjectChangeRequest>().IgnoreQueryFilters().AsNoTracking().Where(item => item.Id == id && item.TenantId == tenantId && !item.IsDeleted).Select(item => new ResourceOwner(item.ProjectId, null)).FirstOrDefaultAsync(cancellationToken),
            "allocations" => await ResolveAllocationOwnerAsync(db, id, tenantId, cancellationToken),
            "payment-obligations" => await db.Set<PaymentObligation>().IgnoreQueryFilters().AsNoTracking().Where(item => item.Id == id && item.TenantId == tenantId && !item.IsDeleted).Select(item => new ResourceOwner(item.ProjectId, null)).FirstOrDefaultAsync(cancellationToken),
            _ => new ResourceOwner(null, null)
        };
    }

    /// <summary>
    /// Allocations may be project-linked or purely resource-level. A resource-level allocation
    /// has no project owner, so it is only addressable when the caller can already see that
    /// resource; anything else falls closed.
    /// </summary>
    private static async Task<ResourceOwner?> ResolveAllocationOwnerAsync(AppDbContext db, Guid id, Guid tenantId, CancellationToken cancellationToken)
    {
        var allocation = await db.Set<ResourceAllocation>().IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.Id == id && item.TenantId == tenantId && !item.IsDeleted)
            .Select(item => new { item.ProjectId })
            .FirstOrDefaultAsync(cancellationToken);
        return allocation is null ? null : new ResourceOwner(allocation.ProjectId, null);
    }

    private static async Task<ResourceOwner?> ResolveBudgetOwnerAsync(AppDbContext db, Guid budgetId, Guid tenantId, CancellationToken cancellationToken) =>
        await db.Set<Budget>().IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.Id == budgetId && item.TenantId == tenantId && !item.IsDeleted)
            .Select(item => new ResourceOwner(item.ProjectId, item.PortfolioId))
            .FirstOrDefaultAsync(cancellationToken);

    private static async Task<ResourceOwner?> ResolveCostOwnerAsync(AppDbContext db, Guid costId, Guid tenantId, CancellationToken cancellationToken)
    {
        var cost = await db.Set<CostEntry>().IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.Id == costId && item.TenantId == tenantId && !item.IsDeleted)
            .Select(item => new { item.ProjectId, item.BudgetId })
            .FirstOrDefaultAsync(cancellationToken);
        if (cost is null) return null;
        if (cost.ProjectId.HasValue) return new ResourceOwner(cost.ProjectId, null);
        return cost.BudgetId.HasValue
            ? await ResolveBudgetOwnerAsync(db, cost.BudgetId.Value, tenantId, cancellationToken)
            : new ResourceOwner(null, null);
    }

    private static async Task<ResourceOwner?> ResolveCommitmentOwnerAsync(AppDbContext db, Guid commitmentId, Guid tenantId, CancellationToken cancellationToken)
    {
        var commitment = await db.Set<PurchaseCommitment>().IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.Id == commitmentId && item.TenantId == tenantId && !item.IsDeleted)
            .Select(item => new { item.ProjectId, item.BudgetId })
            .FirstOrDefaultAsync(cancellationToken);
        if (commitment is null) return null;
        if (commitment.ProjectId.HasValue) return new ResourceOwner(commitment.ProjectId, null);
        return commitment.BudgetId.HasValue
            ? await ResolveBudgetOwnerAsync(db, commitment.BudgetId.Value, tenantId, cancellationToken)
            : new ResourceOwner(null, null);
    }

    private sealed record ResourceOwner(Guid? ProjectId, Guid? PortfolioId);

    private static async Task WriteNotFoundAsync(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(new Microsoft.AspNetCore.Mvc.ProblemDetails
        {
            Status = StatusCodes.Status404NotFound,
            Title = "Not Found",
            Detail = "The requested resource was not found."
        }, options: null, contentType: "application/problem+json", cancellationToken: context.RequestAborted);
    }
}
