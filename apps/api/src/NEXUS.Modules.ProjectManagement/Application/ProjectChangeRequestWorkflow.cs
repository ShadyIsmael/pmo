using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Identity.Application;
using NEXUS.Modules.Identity.Domain;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.Modules.WorkflowEngine.Application.Contracts;
using NEXUS.Modules.WorkflowEngine.Domain;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Management;

namespace NEXUS.Modules.ProjectManagement.Application;

public static class ProjectChangeRequestWorkflow
{
    public const string EntityType = "ProjectChangeRequest";
    public const string DefinitionCode = "PROJECT_CHANGE_REQUEST_STANDARD";
    public const string Submit = "SUBMIT";
    public const string ApproveStep = "APPROVE_STEP";
    public const string Complete = "COMPLETE";
    public const string Reject = "REJECT";
    public const string CancelDraft = "CANCEL_DRAFT";
    public const string CancelPending = "CANCEL_PENDING";

    public static Result ValidateVersion(WorkflowDefinitionVersion version)
    {
        var activeStates = version.States.Where(item => item.IsActive && !item.IsDeleted).ToList();
        var stateCodes = activeStates.Select(item => item.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!new[] { "Draft", "Pending", "Approved", "Rejected", "Cancelled" }.All(stateCodes.Contains))
            return Result.Failure(Error.Validation("Change Request workflows require Draft, Pending, Approved, Rejected, and Cancelled states."));
        if (activeStates.Count(item => item.IsInitial) != 1 || !activeStates.Single(item => item.IsInitial).Code.Equals("Draft", StringComparison.OrdinalIgnoreCase))
            return Result.Failure(Error.Validation("The Change Request workflow must have Draft as its only initial state."));
        foreach (var code in new[] { "Approved", "Rejected", "Cancelled" })
            if (!activeStates.Single(item => item.Code.Equals(code, StringComparison.OrdinalIgnoreCase)).IsFinal)
                return Result.Failure(Error.Validation($"The Change Request workflow state '{code}' must be final."));

        var transitions = version.Transitions.Where(item => item.IsActive && !item.IsDeleted)
            .Select(item => (item.Code, From: item.FromState.Code, To: item.ToState.Code))
            .ToHashSet();
        var required = new[]
        {
            (Submit, "Draft", "Pending"),
            (ApproveStep, "Pending", "Pending"),
            (Complete, "Pending", "Approved"),
            (Reject, "Pending", "Rejected"),
            (CancelDraft, "Draft", "Cancelled"),
            (CancelPending, "Pending", "Cancelled")
        };
        var missing = required.Where(expected => !transitions.Any(actual =>
            actual.Code.Equals(expected.Item1, StringComparison.OrdinalIgnoreCase)
            && actual.From.Equals(expected.Item2, StringComparison.OrdinalIgnoreCase)
            && actual.To.Equals(expected.Item3, StringComparison.OrdinalIgnoreCase))).ToList();
        return missing.Count == 0
            ? Result.Success()
            : Result.Failure(Error.Validation($"Change Request workflow is missing required transition(s): {string.Join(", ", missing.Select(item => item.Item1))}."));
    }
}

public sealed record ProjectChangeRequestWorkflowDefinition(Guid DefinitionId, Guid VersionId);

public interface IProjectChangeRequestWorkflowProvisioningService
{
    Task<Result<ProjectChangeRequestWorkflowDefinition>> EnsureAsync(Guid tenantId, string actor, CancellationToken cancellationToken = default);
}

public sealed class ProjectChangeRequestWorkflowProvisioningService(DbContext context) : IProjectChangeRequestWorkflowProvisioningService
{
    private static readonly (string Code, string NameEn, string NameAr, bool Final)[] States =
    [
        ("Draft", "Draft", "مسودة", false),
        ("Pending", "Pending Approval", "بانتظار الموافقة", false),
        ("Approved", "Approved", "معتمد", true),
        ("Rejected", "Rejected", "مرفوض", true),
        ("Cancelled", "Cancelled", "ملغي", true)
    ];

    private static readonly (string Code, string From, string To, string NameEn, string NameAr)[] Transitions =
    [
        (ProjectChangeRequestWorkflow.Submit, "Draft", "Pending", "Submit for review", "إرسال للمراجعة"),
        (ProjectChangeRequestWorkflow.ApproveStep, "Pending", "Pending", "Record approval", "تسجيل الموافقة"),
        (ProjectChangeRequestWorkflow.Complete, "Pending", "Approved", "Complete approval", "إتمام الموافقة"),
        (ProjectChangeRequestWorkflow.Reject, "Pending", "Rejected", "Reject request", "رفض الطلب"),
        (ProjectChangeRequestWorkflow.CancelDraft, "Draft", "Cancelled", "Cancel draft", "إلغاء المسودة"),
        (ProjectChangeRequestWorkflow.CancelPending, "Pending", "Cancelled", "Cancel request", "إلغاء الطلب")
    ];

    public async Task<Result<ProjectChangeRequestWorkflowDefinition>> EnsureAsync(Guid tenantId, string actor, CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
            return Result<ProjectChangeRequestWorkflowDefinition>.Failure(Error.Validation("A valid tenant is required to provision the Change Request workflow."));

        var definition = await context.Set<WorkflowDefinition>().FirstOrDefaultAsync(item =>
            item.TenantId == tenantId && item.Code == ProjectChangeRequestWorkflow.DefinitionCode && !item.IsDeleted, cancellationToken);
        if (definition is { IsActive: false })
            return Result<ProjectChangeRequestWorkflowDefinition>.Failure(Error.Conflict("The tenant's standard Change Request workflow is disabled."));

        if (definition?.ActivePublishedVersionId is Guid activeVersionId)
        {
            var active = await context.Set<WorkflowDefinitionVersion>()
                .Include(item => item.States)
                .Include(item => item.Transitions).ThenInclude(item => item.FromState)
                .Include(item => item.Transitions).ThenInclude(item => item.ToState)
                .FirstOrDefaultAsync(item => item.Id == activeVersionId && item.WorkflowDefinitionId == definition.Id
                    && item.LifecycleStatus == WorkflowDefinitionVersionLifecycleStatus.Published && !item.IsDeleted, cancellationToken);
            if (active is not null)
            {
                var validation = ProjectChangeRequestWorkflow.ValidateVersion(active);
                return validation.IsSuccess
                    ? Result<ProjectChangeRequestWorkflowDefinition>.Success(new(definition.Id, active.Id))
                    : Result<ProjectChangeRequestWorkflowDefinition>.Failure(validation.Error!);
            }
        }

        var currentMaxVersion = definition is null ? 0 : await context.Set<WorkflowDefinitionVersion>()
            .Where(item => item.WorkflowDefinitionId == definition.Id && !item.IsDeleted)
            .Select(item => (int?)item.VersionNumber).MaxAsync(cancellationToken) ?? 0;
        var version = new WorkflowDefinitionVersion
        {
            TenantId = tenantId,
            VersionNumber = currentMaxVersion + 1,
            LifecycleStatus = WorkflowDefinitionVersionLifecycleStatus.Published,
            ChangeLog = "Provisioned canonical Change Request approval lifecycle.",
            PublishedAt = DateTime.UtcNow,
            PublishedBy = actor,
            CreatedBy = actor
        };
        var stateByCode = new Dictionary<string, WorkflowState>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < States.Length; index++)
        {
            var item = States[index];
            var state = new WorkflowState
            {
                TenantId = tenantId,
                WorkflowDefinitionVersionId = version.Id,
                Code = item.Code,
                NameEn = item.NameEn,
                NameAr = item.NameAr,
                DisplayOrder = index + 1,
                IsInitial = item.Code == "Draft",
                IsFinal = item.Final,
                CreatedBy = actor
            };
            version.States.Add(state);
            stateByCode.Add(state.Code, state);
        }
        for (var index = 0; index < Transitions.Length; index++)
        {
            var item = Transitions[index];
            var transition = new WorkflowTransition
            {
                TenantId = tenantId,
                WorkflowDefinitionVersionId = version.Id,
                Code = item.Code,
                NameEn = item.NameEn,
                NameAr = item.NameAr,
                FromStateId = stateByCode[item.From].Id,
                ToStateId = stateByCode[item.To].Id,
                DisplayOrder = index + 1,
                CreatedBy = actor
            };
            transition.Actions.Add(new WorkflowTransitionAction
            {
                TenantId = tenantId,
                WorkflowTransitionId = transition.Id,
                ActionType = "DomainProjection",
                ConfigurationJson = "{}",
                ExecutionOrder = 10,
                CreatedBy = actor
            });
            version.Transitions.Add(transition);
        }

        definition ??= new WorkflowDefinition
        {
            TenantId = tenantId,
            Code = ProjectChangeRequestWorkflow.DefinitionCode,
            Name = "Project Change Request Approval",
            Description = "Canonical Change Request draft, submission, multi-approver, and decision lifecycle.",
            Domain = WorkflowDomains.ChangeRequest,
            EntityType = ProjectChangeRequestWorkflow.EntityType,
            CreatedBy = actor
        };
        definition.ActivePublishedVersionId = null;
        definition.ActivePublishedVersion = null;
        definition.Versions.Add(version);
        if (context.Entry(definition).State == EntityState.Detached) context.Set<WorkflowDefinition>().Add(definition);

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            definition.ActivePublishedVersionId = version.Id;
            definition.ActivePublishedVersion = version;
            definition.UpdatedAt = DateTime.UtcNow;
            definition.UpdatedBy = actor;
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<ProjectChangeRequestWorkflowDefinition>.Success(new(definition.Id, version.Id));
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            context.ChangeTracker.Clear();
            var published = await context.Set<WorkflowDefinition>().AsNoTracking().FirstOrDefaultAsync(item =>
                item.TenantId == tenantId && item.Code == ProjectChangeRequestWorkflow.DefinitionCode && item.IsActive && !item.IsDeleted
                && item.ActivePublishedVersionId.HasValue, cancellationToken);
            if (published?.ActivePublishedVersionId is Guid publishedVersionId)
                return Result<ProjectChangeRequestWorkflowDefinition>.Success(new(published.Id, publishedVersionId));
            throw;
        }
    }
}

public sealed class ProjectChangeRequestWorkflowAdapter(
    DbContext context,
    IHttpContextAccessor httpContextAccessor,
    IManagementContextAccessor managementContext,
    IUserEffectivePermissionService effectivePermissions) : IWorkflowEntityAdapter
{
    public string EntityType => ProjectChangeRequestWorkflow.EntityType;

    public async Task<Result<WorkflowEntitySnapshot>> GetSnapshotAsync(Guid entityId, WorkflowExecutionContext execution, CancellationToken cancellationToken = default)
    {
        var request = await FindAccessibleAsync(entityId, execution, cancellationToken);
        request ??= await FindAssignedApprovalAsync(entityId, execution, cancellationToken);
        return request is null
            ? Result<WorkflowEntitySnapshot>.Failure(WorkflowErrors.EntityNotAccessible(EntityType, entityId))
            : Result<WorkflowEntitySnapshot>.Success(new WorkflowEntitySnapshot(EntityType, entityId, new Dictionary<string, object?>
            {
                ["StateCode"] = request.Status.ToString(),
                ["StateNameEn"] = request.Status.ToString(),
                ["StateNameAr"] = request.Status.ToString(),
                ["ProjectId"] = request.ProjectId,
                ["RequestedBy"] = request.RequestedBy,
                ["ApprovalStrategy"] = request.ApprovalStrategy
            }));
    }

    public async Task<Result> AuthorizeAsync(WorkflowExecutionContext execution, Guid entityId, CancellationToken cancellationToken = default)
    {
        if (execution.ReadOnly || managementContext.Current?.EffectiveReadOnly == true)
            return Result.Failure(WorkflowErrors.ReadOnlyViewAs);
        var request = await FindAccessibleAsync(entityId, execution, cancellationToken);
        request ??= await FindAssignedApprovalAsync(entityId, execution, cancellationToken);
        if (request is null) return Result.Failure(WorkflowErrors.EntityNotAccessible(EntityType, entityId));
        var principal = httpContextAccessor.HttpContext?.User;
        var userId = ParseUserId(execution.UserId);
        if (principal?.Identity?.IsAuthenticated != true || userId == Guid.Empty)
            return Result.Failure(Error.Unauthorized("An authenticated user is required."));
        var permissions = await effectivePermissions.GetEffectivePermissionsAsync(userId, execution.TenantId, cancellationToken);
        return permissions.Overlaps(new[] { "ChangeRequest.View", "ChangeRequest.Manage", "ChangeRequest.Approve" })
            ? Result.Success()
            : Result.Failure(Error.Forbidden("Change Request access is required."));
    }

    /// <summary>
    /// Transition-level gate: reading a change request (<c>ChangeRequest.View</c>) does not
    /// authorize submitting, cancelling, approving or rejecting it. The granular, assignment
    /// aware rules still run in <see cref="ValidateTransitionAsync"/>.
    /// </summary>
    public async Task<Result> AuthorizeTransitionAsync(WorkflowTransitionContext transition, CancellationToken cancellationToken = default)
    {
        if (transition.Execution.ReadOnly || managementContext.Current?.EffectiveReadOnly == true)
            return Result.Failure(WorkflowErrors.ReadOnlyViewAs);
        var request = await FindAccessibleAsync(transition.EntityId, transition.Execution, cancellationToken)
            ?? await FindAssignedApprovalAsync(transition.EntityId, transition.Execution, cancellationToken);
        if (request is null) return Result.Failure(WorkflowErrors.EntityNotAccessible(EntityType, transition.EntityId));
        var actor = ParseUserId(transition.Execution.UserId);
        if (httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated != true || actor == Guid.Empty)
            return Result.Failure(Error.Unauthorized("An authenticated user is required."));
        var permissions = await effectivePermissions.GetEffectivePermissionsAsync(actor, transition.Execution.TenantId, cancellationToken);
        return permissions.Overlaps(new[] { "ChangeRequest.Manage", "ChangeRequest.Approve" })
            ? Result.Success()
            : Result.Failure(Error.Forbidden("Change Request management or approval permission is required for workflow transitions."));
    }

    public async Task<Result> ValidateTransitionAsync(WorkflowTransitionContext transition, CancellationToken cancellationToken = default)
    {
        var code = transition.Transition.Code;
        var request = await FindAccessibleAsync(transition.EntityId, transition.Execution, cancellationToken);
        if (request is null && IsDecisionTransition(code))
            request = await FindAssignedApprovalAsync(transition.EntityId, transition.Execution, cancellationToken);
        if (request is null) return Result.Failure(WorkflowErrors.EntityNotAccessible(EntityType, transition.EntityId));
        if (!string.Equals(request.Status.ToString(), transition.FromState.Code, StringComparison.OrdinalIgnoreCase))
            return Result.Failure(WorkflowErrors.WorkflowDomainStateMismatch(transition.FromState.Code, request.Status.ToString()));

        if (code.Equals(ProjectChangeRequestWorkflow.Submit, StringComparison.OrdinalIgnoreCase))
        {
            if (request.Status != ProjectChangeRequest.Statuses.Draft
                || !await HasEffectivePermissionAsync(ParseUserId(transition.Execution.UserId), transition.Execution.TenantId, "ChangeRequest.Manage", cancellationToken))
                return Result.Failure(Error.Forbidden("Only an authorized Change Request manager can submit a draft."));
            return await ValidateSavedAssignmentsAsync(request, cancellationToken);
        }
        if (code.Equals(ProjectChangeRequestWorkflow.CancelDraft, StringComparison.OrdinalIgnoreCase)
            || code.Equals(ProjectChangeRequestWorkflow.CancelPending, StringComparison.OrdinalIgnoreCase))
            return request.RequestedBy == ParseUserId(transition.Execution.UserId)
                || await HasEffectivePermissionAsync(ParseUserId(transition.Execution.UserId), transition.Execution.TenantId, "ChangeRequest.Manage", cancellationToken)
                ? Result.Success()
                : Result.Failure(Error.Forbidden("Only the requester or an authorized manager can cancel this Change Request."));

        if (code.Equals(ProjectChangeRequestWorkflow.ApproveStep, StringComparison.OrdinalIgnoreCase)
            || code.Equals(ProjectChangeRequestWorkflow.Complete, StringComparison.OrdinalIgnoreCase)
            || code.Equals(ProjectChangeRequestWorkflow.Reject, StringComparison.OrdinalIgnoreCase))
        {
            var assignment = await GetActionableAssignmentAsync(request, transition.Execution, cancellationToken);
            if (assignment is null) return Result.Failure(Error.Forbidden("This user has no actionable approval assignment."));
            var effective = await effectivePermissions.GetEffectivePermissionsAsync(assignment.AssignedToUserId, request.TenantId, cancellationToken);
            if (!effective.Contains("ChangeRequest.Approve"))
                return Result.Failure(Error.Forbidden("Current Change Request approval permission is required."));
            if (assignment.AssignedToUserId == request.RequestedBy)
                return Result.Failure(Error.Forbidden("A Change Request requester cannot approve their own request."));

            if (code.Equals(ProjectChangeRequestWorkflow.Reject, StringComparison.OrdinalIgnoreCase)) return Result.Success();
            var completes = await WouldCompleteAfterApprovalAsync(request, assignment, cancellationToken);
            if (code.Equals(ProjectChangeRequestWorkflow.Complete, StringComparison.OrdinalIgnoreCase) != completes)
                return Result.Failure(Error.Conflict("The selected workflow transition does not match the approval completion rule."));
            return Result.Success();
        }
        return Result.Failure(Error.Forbidden("This transition is not part of the Change Request approval lifecycle."));
    }

    public async Task<Result> ApplyTransitionAsync(WorkflowTransitionContext transition, CancellationToken cancellationToken = default)
    {
        var code = transition.Transition.Code;
        var request = await FindAccessibleAsync(transition.EntityId, transition.Execution, cancellationToken, tracked: true);
        if (request is null && IsDecisionTransition(code))
            request = await FindAssignedApprovalAsync(transition.EntityId, transition.Execution, cancellationToken, tracked: true);
        if (request is null) return Result.Failure(WorkflowErrors.EntityNotAccessible(EntityType, transition.EntityId));
        if (!Enum.TryParse<ProjectChangeRequest.Statuses>(transition.ToState.Code, true, out var target))
            return Result.Failure(Error.Validation("The target workflow state is not a Change Request status."));

        if (code.Equals(ProjectChangeRequestWorkflow.ApproveStep, StringComparison.OrdinalIgnoreCase)
            || code.Equals(ProjectChangeRequestWorkflow.Complete, StringComparison.OrdinalIgnoreCase)
            || code.Equals(ProjectChangeRequestWorkflow.Reject, StringComparison.OrdinalIgnoreCase))
        {
            var assignment = await GetActionableAssignmentAsync(request, transition.Execution, cancellationToken, tracked: true);
            if (assignment is null) return Result.Failure(Error.Forbidden("This user has no actionable approval assignment."));
            var approved = code.Equals(ProjectChangeRequestWorkflow.ApproveStep, StringComparison.OrdinalIgnoreCase)
                || code.Equals(ProjectChangeRequestWorkflow.Complete, StringComparison.OrdinalIgnoreCase);
            assignment.Status = approved ? ProjectChangeRequestApproval.Statuses.Approved : ProjectChangeRequestApproval.Statuses.Rejected;
            assignment.DecidedAt = DateTime.UtcNow;
            assignment.DecisionActorId = ParseUserId(transition.Execution.UserId);
            assignment.DecisionComment = transition.Execution.Comment;
            assignment.UpdatedAt = DateTime.UtcNow;
            assignment.UpdatedBy = transition.Execution.UserId;

            if (code.Equals(ProjectChangeRequestWorkflow.Complete, StringComparison.OrdinalIgnoreCase)
                && request.ApprovalStrategy.Equals(ProjectChangeRequestApprovalStrategies.AnyOneOptional, StringComparison.OrdinalIgnoreCase))
            {
                var optional = await context.Set<ProjectChangeRequestApproval>().Where(item => item.ProjectChangeRequestId == request.Id
                    && !item.IsMandatory && item.Status == ProjectChangeRequestApproval.Statuses.Pending && !item.IsDeleted).ToListAsync(cancellationToken);
                foreach (var item in optional)
                {
                    item.Status = ProjectChangeRequestApproval.Statuses.Satisfied;
                    item.UpdatedAt = DateTime.UtcNow;
                    item.UpdatedBy = transition.Execution.UserId;
                }
            }
            else if (code.Equals(ProjectChangeRequestWorkflow.Reject, StringComparison.OrdinalIgnoreCase))
            {
                await ClosePendingAssignmentsAsync(request.Id, transition.Execution.UserId, cancellationToken);
            }

            if (target is ProjectChangeRequest.Statuses.Approved or ProjectChangeRequest.Statuses.Rejected)
            {
                request.ReviewedBy = assignment.AssignedToUserId;
                request.ReviewedAt = DateTime.UtcNow;
                request.DecisionNotes = transition.Execution.Comment;
            }
        }
        else if (target == ProjectChangeRequest.Statuses.Cancelled)
        {
            await ClosePendingAssignmentsAsync(request.Id, transition.Execution.UserId, cancellationToken);
        }
        else if (target == ProjectChangeRequest.Statuses.Pending)
        {
            request.SubmittedAt ??= DateTime.UtcNow;
        }

        request.Status = target;
        request.UpdatedAt = DateTime.UtcNow;
        request.UpdatedBy = transition.Execution.UserId;
        return Result.Success();
    }

    private async Task<Result> ValidateSavedAssignmentsAsync(ProjectChangeRequest request, CancellationToken cancellationToken)
    {
        if (!ProjectChangeRequestApprovalStrategies.IsSupported(request.ApprovalStrategy))
            return Result.Failure(Error.Validation("Select a supported Change Request approval strategy."));
        var ids = ParseApproverIds(request.SelectedApproverUserIdsJson);
        if (ids.Count == 0 || ids.Count != ids.Distinct().Count())
            return Result.Failure(Error.Validation("At least one unique project approver must be selected before submission."));
        var assignmentIds = await context.Set<ProjectChangeRequestApproval>().Where(item => item.ProjectChangeRequestId == request.Id
            && item.WorkflowInstanceId == request.WorkflowInstanceId && !item.IsDeleted).Select(item => item.AssignedToUserId).ToListAsync(cancellationToken);
        return ids.Order().SequenceEqual(assignmentIds.Order())
            ? Result.Success()
            : Result.Failure(Error.Forbidden("The approval assignment snapshot does not match the saved draft selection."));
    }

    private async Task<ProjectChangeRequestApproval?> GetActionableAssignmentAsync(ProjectChangeRequest request, WorkflowExecutionContext execution, CancellationToken cancellationToken, bool tracked = false)
    {
        var userId = ParseUserId(execution.UserId);
        var query = context.Set<ProjectChangeRequestApproval>().Where(item => item.ProjectChangeRequestId == request.Id
            && item.TenantId == request.TenantId && item.AssignedToUserId == userId
            && item.Status == ProjectChangeRequestApproval.Statuses.Pending && !item.IsDeleted);
        if (!tracked) query = query.AsNoTracking();
        var candidates = await query.OrderBy(item => item.Sequence).ToListAsync(cancellationToken);
        var assignment = candidates.FirstOrDefault();
        if (assignment is null) return null;
        if (request.ApprovalStrategy.Equals(ProjectChangeRequestApprovalStrategies.Sequential, StringComparison.OrdinalIgnoreCase))
        {
            var earlierPending = await context.Set<ProjectChangeRequestApproval>().AnyAsync(item => item.ProjectChangeRequestId == request.Id
                && item.Sequence < assignment.Sequence && item.Status != ProjectChangeRequestApproval.Statuses.Approved
                && item.Status != ProjectChangeRequestApproval.Statuses.Satisfied && !item.IsDeleted, cancellationToken);
            if (earlierPending) return null;
        }
        if (request.ApprovalStrategy.Equals(ProjectChangeRequestApprovalStrategies.AnyOneOptional, StringComparison.OrdinalIgnoreCase)
            && !assignment.IsMandatory)
        {
            var mandatoryPending = await context.Set<ProjectChangeRequestApproval>().AnyAsync(item => item.ProjectChangeRequestId == request.Id
                && item.IsMandatory && item.Status != ProjectChangeRequestApproval.Statuses.Approved && !item.IsDeleted, cancellationToken);
            var optionalSatisfied = await context.Set<ProjectChangeRequestApproval>().AnyAsync(item => item.ProjectChangeRequestId == request.Id
                && !item.IsMandatory && item.Status == ProjectChangeRequestApproval.Statuses.Approved && !item.IsDeleted, cancellationToken);
            if (mandatoryPending || optionalSatisfied) return null;
        }
        return assignment;
    }

    private async Task<bool> WouldCompleteAfterApprovalAsync(ProjectChangeRequest request, ProjectChangeRequestApproval acting, CancellationToken cancellationToken)
    {
        var assignments = await context.Set<ProjectChangeRequestApproval>().AsNoTracking().Where(item => item.ProjectChangeRequestId == request.Id && !item.IsDeleted).ToListAsync(cancellationToken);
        var statuses = assignments.ToDictionary(item => item.Id, item => item.Status);
        statuses[acting.Id] = ProjectChangeRequestApproval.Statuses.Approved;
        if (request.ApprovalStrategy.Equals(ProjectChangeRequestApprovalStrategies.AnyOneOptional, StringComparison.OrdinalIgnoreCase))
            return assignments.Where(item => item.IsMandatory).All(item => statuses[item.Id] == ProjectChangeRequestApproval.Statuses.Approved)
                && assignments.Any(item => !item.IsMandatory && statuses[item.Id] == ProjectChangeRequestApproval.Statuses.Approved);
        return assignments.All(item => statuses[item.Id] == ProjectChangeRequestApproval.Statuses.Approved);
    }

    private async Task ClosePendingAssignmentsAsync(Guid requestId, string actor, CancellationToken cancellationToken)
    {
        var pending = await context.Set<ProjectChangeRequestApproval>().Where(item => item.ProjectChangeRequestId == requestId
            && item.Status == ProjectChangeRequestApproval.Statuses.Pending && !item.IsDeleted).ToListAsync(cancellationToken);
        foreach (var item in pending)
        {
            item.Status = ProjectChangeRequestApproval.Statuses.Cancelled;
            item.UpdatedAt = DateTime.UtcNow;
            item.UpdatedBy = actor;
        }
    }

    private async Task<ProjectChangeRequest?> FindAccessibleAsync(Guid id, WorkflowExecutionContext execution, CancellationToken cancellationToken, bool tracked = false)
    {
        var query = context.Set<ProjectChangeRequest>().AsQueryable();
        if (!tracked) query = query.AsNoTracking();
        var request = await query.FirstOrDefaultAsync(item => item.Id == id && item.TenantId == execution.TenantId && !item.IsDeleted
            && item.Project != null && item.Project.TenantId == execution.TenantId && !item.Project.IsDeleted, cancellationToken);
        if (request is null || execution.TenantId == Guid.Empty) return null;
        // An empty project scope means "no project assignments", never "all projects".
        var scope = managementContext.Current?.Scope.ProjectIds;
        return scope is not null && scope.Contains(request.ProjectId) ? request : null;
    }

    private async Task<ProjectChangeRequest?> FindAssignedApprovalAsync(Guid id, WorkflowExecutionContext execution, CancellationToken cancellationToken, bool tracked = false)
    {
        var actor = ParseUserId(execution.UserId);
        if (execution.TenantId == Guid.Empty || actor == Guid.Empty) return null;
        var query = context.Set<ProjectChangeRequest>().AsQueryable();
        if (!tracked) query = query.AsNoTracking();
        var request = await query.FirstOrDefaultAsync(item => item.Id == id && item.TenantId == execution.TenantId
            && item.Status == ProjectChangeRequest.Statuses.Pending && item.WorkflowInstanceId.HasValue && !item.IsDeleted
            && item.Project != null && item.Project.TenantId == execution.TenantId && !item.Project.IsDeleted, cancellationToken);
        if (request is null || request.RequestedBy == actor) return null;
        if (!await ActiveUserAsync(execution.TenantId, actor, cancellationToken)
            || !await HasEffectivePermissionAsync(actor, execution.TenantId, "ChangeRequest.Approve", cancellationToken)) return null;
        var assignment = await GetActionableAssignmentAsync(request, execution, cancellationToken);
        return assignment is not null && assignment.WorkflowInstanceId == request.WorkflowInstanceId!.Value ? request : null;
    }

    private static bool IsDecisionTransition(string code) =>
        code.Equals(ProjectChangeRequestWorkflow.ApproveStep, StringComparison.OrdinalIgnoreCase)
        || code.Equals(ProjectChangeRequestWorkflow.Complete, StringComparison.OrdinalIgnoreCase)
        || code.Equals(ProjectChangeRequestWorkflow.Reject, StringComparison.OrdinalIgnoreCase);

    private Task<bool> ActiveUserAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken) => context.Set<User>()
        .AnyAsync(item => item.Id == userId && item.TenantId == tenantId && item.IsActive && !item.IsDeleted, cancellationToken);

    private async Task<bool> HasEffectivePermissionAsync(Guid userId, Guid tenantId, string permission, CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty || tenantId == Guid.Empty || httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated != true)
            return false;
        var effective = await effectivePermissions.GetEffectivePermissionsAsync(userId, tenantId, cancellationToken);
        return effective.Contains(permission);
    }

    private static List<Guid> ParseApproverIds(string json)
    {
        try { return System.Text.Json.JsonSerializer.Deserialize<List<Guid>>(json) ?? []; }
        catch (System.Text.Json.JsonException) { return []; }
    }

    private static Guid ParseUserId(string value) => Guid.TryParse(value, out var parsed) ? parsed : Guid.Empty;
}

public sealed class ProjectChangeRequestWorkflowMetadataProvider : IWorkflowEntityMetadataProvider
{
    public string EntityType => ProjectChangeRequestWorkflow.EntityType;
    public WorkflowEntityTypeMetadata GetMetadata() => new(WorkflowDomains.ChangeRequest, EntityType, "Project Change Requests", "طلبات تغيير المشروع", new[]
    {
        new WorkflowFactMetadata("ProjectId", "Project", "المشروع", "string"),
        new WorkflowFactMetadata("RequestedBy", "Requested by", "مقدم الطلب", "string"),
        new WorkflowFactMetadata("ApprovalStrategy", "Approval strategy", "استراتيجية الموافقة", "string")
    });
}

public sealed class ProjectChangeRequestWorkflowConsumerValidator : IWorkflowDefinitionConsumerValidator
{
    public string EntityType => ProjectChangeRequestWorkflow.EntityType;
    public Task<Result> ValidateAsync(WorkflowDefinitionVersion version, CancellationToken cancellationToken = default)
        => Task.FromResult(ProjectChangeRequestWorkflow.ValidateVersion(version));
}
