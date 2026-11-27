using System.Data;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Approvals.Domain;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.CostManagement.Application.DTOs;
using NEXUS.Modules.CostManagement.Domain;
using NEXUS.Modules.Identity.Application;
using NEXUS.Modules.Identity.Domain;
using NEXUS.Modules.Notifications.Application;
using NEXUS.Modules.Notifications.Domain;
using NEXUS.Modules.ProjectManagement.Application.DTOs;
using NEXUS.Modules.ProjectManagement.Application.Mapping;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.Modules.ResourceManagement.Domain;
using NEXUS.Modules.WorkflowEngine.Application.Contracts;
using NEXUS.Modules.WorkflowEngine.Application.Services;
using NEXUS.Modules.WorkflowEngine.Domain;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Management;

namespace NEXUS.Modules.ProjectManagement.Application;

public sealed class ProjectChangeRequestWorkflowService : IProjectChangeRequestService
{
    private const string ManagePermission = "ChangeRequest.Manage";
    private const string ApprovePermission = "ChangeRequest.Approve";
    private const string ApproveFinancialCeilingChangePermission = "ProjectFinancialCeiling.ApproveChange";
    private readonly DbContext _db;
    private readonly IAuditService _audit;
    private readonly IHttpContextAccessor _http;
    private readonly IProjectWorkflowConfigurationService _projectWorkflows;
    private readonly IProjectChangeRequestWorkflowProvisioningService _provisioner;
    private readonly IWorkflowInstanceService _instances;
    private readonly IWorkflowExecutionService _workflow;
    private readonly IUserEffectivePermissionService _permissions;
    private readonly IManagementContextAccessor _management;
    private readonly INotificationService _notifications;
    private readonly IProjectFinancialLookup? _financialLookup;
    private readonly IProjectOrganizationLookup? _projectOrganizationLookup;

    public ProjectChangeRequestWorkflowService(
        DbContext db,
        IAuditService audit,
        IHttpContextAccessor http,
        IProjectWorkflowConfigurationService projectWorkflows,
        IProjectChangeRequestWorkflowProvisioningService provisioner,
        IWorkflowInstanceService instances,
        IWorkflowExecutionService workflow,
        IUserEffectivePermissionService permissions,
        IManagementContextAccessor management,
        INotificationService notifications,
        IProjectFinancialLookup? financialLookup = null,
        IProjectOrganizationLookup? projectOrganizationLookup = null)
    {
        _db = db;
        _audit = audit;
        _http = http;
        _projectWorkflows = projectWorkflows;
        _provisioner = provisioner;
        _instances = instances;
        _workflow = workflow;
        _permissions = permissions;
        _management = management;
        _notifications = notifications;
        _financialLookup = financialLookup;
        _projectOrganizationLookup = projectOrganizationLookup;
    }

    public async Task<Result<ProjectChangeRequestApprovalOptionsDto>> GetApprovalOptionsAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var tenant = CurrentTenant();
        var actor = CurrentUser();
        if (tenant.IsFailure) return Result<ProjectChangeRequestApprovalOptionsDto>.Failure(tenant.Error!);
        if (actor.IsFailure) return Result<ProjectChangeRequestApprovalOptionsDto>.Failure(actor.Error!);
        if (!await HasPermissionAsync(actor.Value, tenant.Value, ManagePermission, cancellationToken))
            return Result<ProjectChangeRequestApprovalOptionsDto>.Failure(Error.Forbidden("Change Request management permission is required."));
        var project = await FindProjectAsync(projectId, tenant.Value, cancellationToken);
        if (project is null) return Result<ProjectChangeRequestApprovalOptionsDto>.Failure(Error.NotFound());
        return await BuildApprovalOptionsAsync(project, actor.Value, cancellationToken);
    }

    public async Task<Result<ProjectChangeRequestDto>> CreateAsync(CreateProjectChangeRequestDto dto, string userId, CancellationToken cancellationToken = default)
    {
        var tenant = CurrentTenant();
        var actor = CurrentUser();
        if (tenant.IsFailure) return Result<ProjectChangeRequestDto>.Failure(tenant.Error!);
        if (actor.IsFailure) return Result<ProjectChangeRequestDto>.Failure(actor.Error!);
        if (!await HasPermissionAsync(actor.Value, tenant.Value, ManagePermission, cancellationToken))
            return Result<ProjectChangeRequestDto>.Failure(Error.Forbidden("Change Request management permission is required."));
        if (IsReadOnly()) return Result<ProjectChangeRequestDto>.Failure(Error.Forbidden("Cannot create a Change Request in read-only view."));
        var project = await FindProjectAsync(dto.ProjectId, tenant.Value, cancellationToken);
        if (project is null) return Result<ProjectChangeRequestDto>.Failure(Error.NotFound());
        var invalid = ValidateInput(dto, project);
        if (invalid is not null) return Result<ProjectChangeRequestDto>.Failure(invalid);
        var invalidApprovers = await ValidateDraftSelectionAsync(project, actor.Value, dto.ApproverUserIds, cancellationToken);
        if (invalidApprovers is not null) return Result<ProjectChangeRequestDto>.Failure(invalidApprovers);

        var selectedWorkflow = await ResolveWorkflowAsync(project.Id, tenant.Value, userId, cancellationToken);
        if (selectedWorkflow.IsFailure) return Result<ProjectChangeRequestDto>.Failure(selectedWorkflow.Error!);
        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var number = (await _db.Set<ProjectChangeRequest>().Where(item => item.ProjectId == project.Id && item.TenantId == tenant.Value)
            .Select(item => (int?)item.Number).MaxAsync(cancellationToken) ?? 0) + 1;
        var request = dto.ToEntity(actor.Value.ToString());
        request.TenantId = tenant.Value;
        request.ProjectId = project.Id;
        request.Number = number;
        request.Status = ProjectChangeRequest.Statuses.Draft;
        request.ApprovalStrategy = NormalizeStrategy(dto.ApprovalStrategy);
        request.SelectedApproverUserIdsJson = JsonSerializer.Serialize((dto.ApproverUserIds ?? []).Distinct().ToList());
        request.BudgetChangeAmount = dto.BudgetChangeAmount;
        request.BudgetBeforeChange = dto.BudgetChangeAmount.HasValue ? project.Budget : null;
        _db.Set<ProjectChangeRequest>().Add(request);
        await _db.SaveChangesAsync(cancellationToken);

        var workflowDefinition = selectedWorkflow.Value;
        if (workflowDefinition is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<ProjectChangeRequestDto>.Failure(Error.Conflict("A canonical Change Request workflow could not be selected."));
        }
        var instance = await _instances.ProvisionAsync(ProjectChangeRequestWorkflow.EntityType, request.Id,
            workflowDefinition.WorkflowDefinitionId!.Value, workflowDefinition.WorkflowVersionId!.Value,
            BuildWorkflowContext(userId), cancellationToken);
        if (instance.IsFailure)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<ProjectChangeRequestDto>.Failure(instance.Error!);
        }
        request.WorkflowInstanceId = instance.Value!.InstanceId;
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await LogAuditAsync(request, "CreateDraft", actor.Value, new { dto.Type, dto.Reason, request.ApprovalStrategy }, cancellationToken);
        return await BuildDtoAsync(request, cancellationToken);
    }

    public async Task<Result<ProjectChangeRequestDto>> UpdateDraftAsync(Guid id, UpdateProjectChangeRequestDto dto, string userId, CancellationToken cancellationToken = default)
    {
        var context = await LoadRequestContextAsync(id, ManagePermission, cancellationToken);
        if (context.Error is not null) return Result<ProjectChangeRequestDto>.Failure(context.Error);
        var request = context.Request!;
        if (IsReadOnly()) return Result<ProjectChangeRequestDto>.Failure(Error.Forbidden("Cannot edit a Change Request in read-only view."));
        if (request.Status != ProjectChangeRequest.Statuses.Draft) return Result<ProjectChangeRequestDto>.Failure(Error.Conflict("Only a draft Change Request can be edited."));
        var invalid = ValidateInput(dto, context.Project!);
        if (invalid is not null) return Result<ProjectChangeRequestDto>.Failure(invalid);
        var invalidApprovers = await ValidateDraftSelectionAsync(context.Project!, request.RequestedBy, dto.ApproverUserIds, cancellationToken);
        if (invalidApprovers is not null) return Result<ProjectChangeRequestDto>.Failure(invalidApprovers);
        if (dto.RowVersion is not { Length: > 0 } || !dto.RowVersion.SequenceEqual(request.RowVersion))
            return Result<ProjectChangeRequestDto>.Failure(Error.Conflict("This draft changed. Refresh and retry."));

        request.Type = dto.Type;
        request.Reason = dto.Reason.Trim();
        request.Justification = Normalize(dto.Justification);
        request.Impact = Normalize(dto.Impact);
        request.Attachments = Normalize(dto.Attachments);
        request.BudgetChangeAmount = dto.BudgetChangeAmount;
        request.BudgetBeforeChange = dto.BudgetChangeAmount.HasValue ? context.Project!.Budget : null;
        request.ApprovalStrategy = NormalizeStrategy(dto.ApprovalStrategy);
        request.SelectedApproverUserIdsJson = JsonSerializer.Serialize((dto.ApproverUserIds ?? []).Distinct().ToList());
        request.UpdatedAt = DateTime.UtcNow;
        request.UpdatedBy = userId;
        var saved = await SaveWithConcurrencyAsync(cancellationToken);
        if (saved.IsFailure) return Result<ProjectChangeRequestDto>.Failure(saved.Error!);
        await LogAuditAsync(request, "UpdateDraft", context.ActorId, new { dto.Type, dto.Reason, request.ApprovalStrategy }, cancellationToken);
        return await BuildDtoAsync(request, cancellationToken);
    }

    public async Task<Result<ProjectChangeRequestDto>> SubmitAsync(Guid id, SubmitProjectChangeRequestDto dto, string userId, CancellationToken cancellationToken = default)
    {
        var context = await LoadRequestContextAsync(id, ManagePermission, cancellationToken);
        if (context.Error is not null) return Result<ProjectChangeRequestDto>.Failure(context.Error);
        var request = context.Request!;
        if (IsReadOnly()) return Result<ProjectChangeRequestDto>.Failure(Error.Forbidden("Cannot submit a Change Request in read-only view."));
        if (request.Status == ProjectChangeRequest.Statuses.Pending)
        {
            var hasAssignments = await _db.Set<ProjectChangeRequestApproval>().AnyAsync(item => item.ProjectChangeRequestId == id && !item.IsDeleted, cancellationToken);
            return hasAssignments ? await BuildDtoAsync(request, cancellationToken)
                : Result<ProjectChangeRequestDto>.Failure(Error.Conflict("This legacy pending request has no approval assignments. Reopen it for approval setup."));
        }
        if (request.Status != ProjectChangeRequest.Statuses.Draft) return Result<ProjectChangeRequestDto>.Failure(Error.Conflict("Only a draft Change Request can be submitted."));
        if (request.RequestedBy != context.ActorId && !await HasPermissionAsync(context.ActorId, request.TenantId, ManagePermission, cancellationToken))
            return Result<ProjectChangeRequestDto>.Failure(Error.Forbidden("Only the requester or an authorized manager can submit the draft."));
        if (dto.RowVersion is not { Length: > 0 } || !dto.RowVersion.SequenceEqual(request.RowVersion))
            return Result<ProjectChangeRequestDto>.Failure(Error.Conflict("This draft changed. Refresh and retry submission."));

        var options = await BuildApprovalOptionsAsync(context.Project!, request.RequestedBy, cancellationToken);
        if (options.IsFailure) return Result<ProjectChangeRequestDto>.Failure(options.Error!);
        var ids = ParseIds(request.SelectedApproverUserIdsJson);
        if (ids.Count == 0 || ids.Count != ids.Distinct().Count()) return Result<ProjectChangeRequestDto>.Failure(Error.Validation("Select at least one unique eligible approver."));
        var strategy = NormalizeStrategy(request.ApprovalStrategy);
        if (!options.Value!.AllowedStrategies.Contains(strategy, StringComparer.OrdinalIgnoreCase))
            return Result<ProjectChangeRequestDto>.Failure(Error.Validation("The selected approval strategy is not allowed by this project's governance policy."));
        var byId = options.Value.Approvers.ToDictionary(item => item.UserId);
        if (ids.Any(item => !byId.ContainsKey(item))) return Result<ProjectChangeRequestDto>.Failure(Error.Validation("One or more selected approvers are no longer eligible project participants."));
        var mandatoryIds = options.Value.Approvers.Where(item => item.IsMandatory).Select(item => item.UserId).ToHashSet();
        if (options.Value.PolicyMessage is not null) return Result<ProjectChangeRequestDto>.Failure(Error.Validation(options.Value.PolicyMessage));
        if (mandatoryIds.Any(item => !ids.Contains(item))) return Result<ProjectChangeRequestDto>.Failure(Error.Validation("The mandatory Project Sponsor approval must be selected."));
        if (strategy == ProjectChangeRequestApprovalStrategies.AnyOneOptional && !ids.Any(item => !mandatoryIds.Contains(item)))
            return Result<ProjectChangeRequestDto>.Failure(Error.Validation("Any One Optional requires at least one optional approver in addition to any mandatory approvals."));
        if (request.WorkflowInstanceId is null) return Result<ProjectChangeRequestDto>.Failure(Error.Conflict("This draft has no workflow instance. Reopen it for approval setup."));

        var ordered = ids.Distinct().ToList();
        foreach (var sponsorId in mandatoryIds) { ordered.Remove(sponsorId); ordered.Insert(0, sponsorId); }
        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        if (await _db.Set<ProjectChangeRequestApproval>().AnyAsync(item => item.ProjectChangeRequestId == id && !item.IsDeleted, cancellationToken))
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<ProjectChangeRequestDto>.Failure(Error.Conflict("This request already has an approval snapshot."));
        }
        var sequence = 1;
        foreach (var approverId in ordered)
        {
            var approver = byId[approverId];
            _db.Set<ProjectChangeRequestApproval>().Add(new ProjectChangeRequestApproval
            {
                TenantId = request.TenantId,
                ProjectChangeRequestId = request.Id,
                WorkflowInstanceId = request.WorkflowInstanceId.Value,
                ProjectId = request.ProjectId,
                OriginalApproverUserId = approver.UserId,
                OriginalApproverNameSnapshot = approver.FullName,
                AssignedToUserId = approver.UserId,
                ApproverNameSnapshot = approver.FullName,
                RoleSnapshot = approver.ProjectRole,
                IsSponsor = approver.IsSponsor,
                IsMandatory = approver.IsMandatory,
                Sequence = sequence++,
                Status = ProjectChangeRequestApproval.Statuses.Pending,
                CreatedBy = userId
            });
        }
        request.SubmittedAt = DateTime.UtcNow;
        request.UpdatedAt = DateTime.UtcNow;
        request.UpdatedBy = userId;
        await _db.SaveChangesAsync(cancellationToken);
        var transition = await ExecuteTransitionAsync(request, ProjectChangeRequestWorkflow.Submit, null, context.ActorId, cancellationToken);
        if (transition.IsFailure)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<ProjectChangeRequestDto>.Failure(transition.Error!);
        }
        await transaction.CommitAsync(cancellationToken);
        await LogAuditAsync(request, "Submit", context.ActorId, new { request.ApprovalStrategy, Approvers = ordered, request.WorkflowInstanceId }, cancellationToken);
        await NotifyActionableAsync(request, context.Project!, cancellationToken);
        return await BuildDtoAsync(request, cancellationToken);
    }

    public async Task<Result<ProjectChangeRequestDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenant = CurrentTenant();
        if (tenant.IsFailure) return Result<ProjectChangeRequestDto>.Failure(tenant.Error!);
        var request = await _db.Set<ProjectChangeRequest>().FirstOrDefaultAsync(item => item.Id == id && item.TenantId == tenant.Value && !item.IsDeleted, cancellationToken);
        if (request is null) return Result<ProjectChangeRequestDto>.Failure(Error.NotFound());
        if (await FindProjectAsync(request.ProjectId, tenant.Value, cancellationToken) is null)
            return Result<ProjectChangeRequestDto>.Failure(Error.NotFound());
        return await BuildDtoAsync(request, cancellationToken);
    }

    public async Task<Result<ApprovalReviewDto>> GetApprovalReviewAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tenant = CurrentTenant();
        var actor = CurrentUser();
        if (tenant.IsFailure) return Result<ApprovalReviewDto>.Failure(tenant.Error!);
        if (actor.IsFailure) return Result<ApprovalReviewDto>.Failure(actor.Error!);

        var request = await _db.Set<ProjectChangeRequest>().AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id && item.TenantId == tenant.Value && !item.IsDeleted, cancellationToken);
        if (request is null || request.Status != ProjectChangeRequest.Statuses.Pending)
            return Result<ApprovalReviewDto>.Failure(Error.NotFound("This approval is no longer available."));
        if (!await CanActAsync(request, actor.Value, cancellationToken, allowOutOfScope: true))
            return Result<ApprovalReviewDto>.Failure(Error.NotFound("This approval is not assigned to the current user."));

        var project = await FindProjectAsync(request.ProjectId, tenant.Value, cancellationToken, allowOutOfScope: true);
        if (project is null) return Result<ApprovalReviewDto>.Failure(Error.NotFound("The project for this approval is unavailable."));
        var requestedByName = await _db.Set<User>().AsNoTracking()
            .Where(user => user.Id == request.RequestedBy && user.TenantId == tenant.Value && !user.IsDeleted)
            .Select(user => user.FullName)
            .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;

        return Result<ApprovalReviewDto>.Success(new ApprovalReviewDto
        {
            ApprovalId = request.Id,
            ApprovalType = "ProjectChangeRequest",
            BusinessObjectType = "Project",
            BusinessObjectPublicId = project.PublicId,
            BusinessObjectName = project.Name,
            BusinessObjectCode = project.Code,
            BusinessObjectStatus = project.Status,
            Title = $"Change Request #{request.Number}",
            Reason = request.Reason,
            RequestedByName = requestedByName,
            RequestedAt = request.RequestedAt,
            Status = request.Status.ToString(),
            ChangeNumber = request.Number,
            ChangeType = request.Type.ToString(),
            Justification = request.Justification,
            Impact = request.Impact,
            CurrentAmount = request.BudgetBeforeChange,
            AmountChange = request.BudgetChangeAmount,
            ProposedAmount = request.BudgetBeforeChange.HasValue && request.BudgetChangeAmount.HasValue
                ? request.BudgetBeforeChange.Value + request.BudgetChangeAmount.Value
                : null,
            Currency = project.Currency,
            IsReadOnly = true,
            CanDecide = !IsReadOnly()
        });
    }

    public async Task<Result<IReadOnlyList<ProjectChangeRequestDto>>> GetByProjectIdAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var tenant = CurrentTenant();
        if (tenant.IsFailure) return Result<IReadOnlyList<ProjectChangeRequestDto>>.Failure(tenant.Error!);
        if (await FindProjectAsync(projectId, tenant.Value, cancellationToken) is null)
            return Result<IReadOnlyList<ProjectChangeRequestDto>>.Failure(Error.NotFound());
        var requests = await _db.Set<ProjectChangeRequest>().Where(item => item.ProjectId == projectId && item.TenantId == tenant.Value && !item.IsDeleted)
            .OrderByDescending(item => item.CreatedAt).ToListAsync(cancellationToken);
        var result = new List<ProjectChangeRequestDto>();
        foreach (var item in requests)
        {
            var dto = await BuildDtoAsync(item, cancellationToken);
            if (dto.IsSuccess) result.Add(dto.Value!);
        }
        return Result<IReadOnlyList<ProjectChangeRequestDto>>.Success(result);
    }

    public async Task<Result<IReadOnlyList<PendingProjectChangeApprovalDto>>> GetPendingAsync(CancellationToken cancellationToken = default)
    {
        var tenant = CurrentTenant();
        var actor = CurrentUser();
        if (tenant.IsFailure) return Result<IReadOnlyList<PendingProjectChangeApprovalDto>>.Failure(tenant.Error!);
        if (actor.IsFailure) return Result<IReadOnlyList<PendingProjectChangeApprovalDto>>.Failure(actor.Error!);
        if (!await HasPermissionAsync(actor.Value, tenant.Value, ApprovePermission, cancellationToken))
            return Result<IReadOnlyList<PendingProjectChangeApprovalDto>>.Failure(Error.Forbidden("Change Request approval permission is required."));
        var ids = await _db.Set<ProjectChangeRequestApproval>().AsNoTracking().Where(item => item.TenantId == tenant.Value
            && item.AssignedToUserId == actor.Value && item.Status == ProjectChangeRequestApproval.Statuses.Pending && !item.IsDeleted)
            .Select(item => item.ProjectChangeRequestId).Distinct().ToListAsync(cancellationToken);
        var requests = await _db.Set<ProjectChangeRequest>().Where(item => ids.Contains(item.Id) && item.TenantId == tenant.Value
            && item.Status == ProjectChangeRequest.Statuses.Pending && !item.IsDeleted).OrderByDescending(item => item.SubmittedAt).ToListAsync(cancellationToken);
        var result = new List<PendingProjectChangeApprovalDto>();
        foreach (var request in requests)
        {
            if (!await CanActAsync(request, actor.Value, cancellationToken, allowOutOfScope: true)) continue;
            var project = await _db.Set<Project>().AsNoTracking().Where(item => item.Id == request.ProjectId
                    && item.TenantId == tenant.Value && !item.IsDeleted)
                .Select(item => new { item.Name })
                .FirstOrDefaultAsync(cancellationToken);
            if (project is null) continue;
            var requestedByName = await _db.Set<User>().AsNoTracking().Where(item => item.Id == request.RequestedBy
                    && item.TenantId == tenant.Value && !item.IsDeleted)
                .Select(item => item.FullName)
                .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;
            var assignments = await _db.Set<ProjectChangeRequestApproval>().AsNoTracking()
                .Where(item => item.ProjectChangeRequestId == request.Id && item.TenantId == tenant.Value && !item.IsDeleted)
                .Select(item => new { item.Status, item.IsMandatory })
                .ToListAsync(cancellationToken);
            var approvalCount = request.ApprovalStrategy == ProjectChangeRequestApprovalStrategies.AnyOneOptional
                ? assignments.Count(item => item.IsMandatory && item.Status == ProjectChangeRequestApproval.Statuses.Approved)
                    + (assignments.Any(item => !item.IsMandatory && item.Status == ProjectChangeRequestApproval.Statuses.Approved) ? 1 : 0)
                : assignments.Count(item => item.Status is ProjectChangeRequestApproval.Statuses.Approved or ProjectChangeRequestApproval.Statuses.Satisfied);
            var requiredCount = request.ApprovalStrategy == ProjectChangeRequestApprovalStrategies.AnyOneOptional
                ? assignments.Count(item => item.IsMandatory) + (assignments.Any(item => !item.IsMandatory) ? 1 : 0)
                : assignments.Count;
            result.Add(new PendingProjectChangeApprovalDto(request.Id, request.Status.ToString(), project.Name, request.Number, request.Type,
                request.Reason, requestedByName, request.RequestedAt, approvalCount, requiredCount));
        }
        return Result<IReadOnlyList<PendingProjectChangeApprovalDto>>.Success(result);
    }

    public Task<Result<ProjectChangeRequestDto>> ApproveAsync(Guid id, ApproveProjectChangeRequestDto dto, string userId, CancellationToken cancellationToken = default)
        => DecideAsync(id, true, dto.DecisionNotes, userId, cancellationToken);

    public Task<Result<ProjectChangeRequestDto>> RejectAsync(Guid id, RejectProjectChangeRequestDto dto, string userId, CancellationToken cancellationToken = default)
        => DecideAsync(id, false, dto.DecisionNotes, userId, cancellationToken);

    public async Task<Result<ProjectChangeRequestDto>> CancelAsync(Guid id, string userId, CancellationToken cancellationToken = default)
    {
        var context = await LoadRequestContextAsync(id, null, cancellationToken);
        if (context.Error is not null) return Result<ProjectChangeRequestDto>.Failure(context.Error);
        var request = context.Request!;
        if (IsReadOnly()) return Result<ProjectChangeRequestDto>.Failure(Error.Forbidden("Cannot cancel a Change Request in read-only view."));
        if (request.Status is not (ProjectChangeRequest.Statuses.Draft or ProjectChangeRequest.Statuses.Pending))
            return Result<ProjectChangeRequestDto>.Failure(Error.Conflict("Only Draft or Pending Change Requests can be cancelled."));
        if (request.RequestedBy != context.ActorId && !await HasPermissionAsync(context.ActorId, request.TenantId, ManagePermission, cancellationToken))
            return Result<ProjectChangeRequestDto>.Failure(Error.Forbidden("Only the requester or an authorized manager can cancel this request."));
        if (request.WorkflowInstanceId.HasValue)
        {
            var code = request.Status == ProjectChangeRequest.Statuses.Draft ? ProjectChangeRequestWorkflow.CancelDraft : ProjectChangeRequestWorkflow.CancelPending;
            var transition = await ExecuteTransitionAsync(request, code, null, context.ActorId, cancellationToken);
            if (transition.IsFailure) return Result<ProjectChangeRequestDto>.Failure(transition.Error!);
        }
        else
        {
            request.Status = ProjectChangeRequest.Statuses.Cancelled;
            request.DecisionNotes = "Cancelled before workflow assignment.";
            request.UpdatedAt = DateTime.UtcNow;
            request.UpdatedBy = userId;
            var saved = await SaveWithConcurrencyAsync(cancellationToken);
            if (saved.IsFailure) return Result<ProjectChangeRequestDto>.Failure(saved.Error!);
        }
        await LogAuditAsync(request, "Cancel", context.ActorId, request.DecisionNotes, cancellationToken);
        return await BuildDtoAsync(request, cancellationToken);
    }

    public async Task<Result<ProjectChangeRequestDto>> RemediateLegacyAsync(Guid id, string userId, CancellationToken cancellationToken = default)
    {
        var context = await LoadRequestContextAsync(id, ManagePermission, cancellationToken);
        if (context.Error is not null) return Result<ProjectChangeRequestDto>.Failure(context.Error);
        var request = context.Request!;
        if (IsReadOnly()) return Result<ProjectChangeRequestDto>.Failure(Error.Forbidden("Cannot remediate a Change Request in read-only view."));
        if (request.Status != ProjectChangeRequest.Statuses.Pending || request.WorkflowInstanceId.HasValue
            || await _db.Set<ProjectChangeRequestApproval>().AnyAsync(item => item.ProjectChangeRequestId == request.Id && !item.IsDeleted, cancellationToken))
            return Result<ProjectChangeRequestDto>.Failure(Error.Conflict("Only a legacy Pending request without an approval assignment can be reopened."));
        var selected = await ResolveWorkflowAsync(request.ProjectId, request.TenantId, userId, cancellationToken);
        if (selected.IsFailure || selected.Value is null) return Result<ProjectChangeRequestDto>.Failure(selected.Error ?? Error.Conflict("No workflow is available."));
        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        request.Status = ProjectChangeRequest.Statuses.Draft;
        request.SubmittedAt = null;
        request.UpdatedAt = DateTime.UtcNow;
        request.UpdatedBy = userId;
        await _db.SaveChangesAsync(cancellationToken);
        var instance = await _instances.ProvisionAsync(ProjectChangeRequestWorkflow.EntityType, request.Id,
            selected.Value.WorkflowDefinitionId!.Value, selected.Value.WorkflowVersionId!.Value, BuildWorkflowContext(userId), cancellationToken);
        if (instance.IsFailure)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<ProjectChangeRequestDto>.Failure(instance.Error!);
        }
        request.WorkflowInstanceId = instance.Value!.InstanceId;
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await LogAuditAsync(request, "ReopenLegacyForApprovalSetup", context.ActorId, "No legacy approval was inferred.", cancellationToken);
        return await BuildDtoAsync(request, cancellationToken);
    }

    public async Task<Result<ProjectChangeRequestApprovalDto>> ReassignApprovalAsync(Guid id, Guid assignmentId, ReassignProjectChangeRequestApprovalDto dto, string userId, CancellationToken cancellationToken = default)
    {
        var context = await LoadRequestContextAsync(id, ManagePermission, cancellationToken);
        if (context.Error is not null) return Result<ProjectChangeRequestApprovalDto>.Failure(context.Error);
        var request = context.Request!;
        if (IsReadOnly()) return Result<ProjectChangeRequestApprovalDto>.Failure(Error.Forbidden("Cannot reassign approval in read-only view."));
        if (request.Status != ProjectChangeRequest.Statuses.Pending) return Result<ProjectChangeRequestApprovalDto>.Failure(Error.Conflict("Only a pending approval can be reassigned."));
        if (string.IsNullOrWhiteSpace(dto.Reason) || dto.Reason.Trim().Length > 2000) return Result<ProjectChangeRequestApprovalDto>.Failure(Error.Validation("A reassignment reason is required."));
        var assignment = await _db.Set<ProjectChangeRequestApproval>().FirstOrDefaultAsync(item => item.Id == assignmentId && item.ProjectChangeRequestId == id
            && item.TenantId == request.TenantId && item.Status == ProjectChangeRequestApproval.Statuses.Pending && !item.IsDeleted, cancellationToken);
        if (assignment is null) return Result<ProjectChangeRequestApprovalDto>.Failure(Error.NotFound("Pending approval assignment not found."));
        var options = await BuildApprovalOptionsAsync(context.Project!, request.RequestedBy, cancellationToken);
        if (options.IsFailure) return Result<ProjectChangeRequestApprovalDto>.Failure(options.Error!);
        var replacement = options.Value!.Approvers.FirstOrDefault(item => item.UserId == dto.NewApproverUserId);
        if (replacement is null || replacement.UserId == assignment.AssignedToUserId) return Result<ProjectChangeRequestApprovalDto>.Failure(Error.Validation("Select a different currently eligible project participant."));
        if (await _db.Set<ProjectChangeRequestApproval>().AnyAsync(item => item.ProjectChangeRequestId == id && item.AssignedToUserId == replacement.UserId
            && item.Status == ProjectChangeRequestApproval.Statuses.Pending && item.Id != assignment.Id && !item.IsDeleted, cancellationToken))
            return Result<ProjectChangeRequestApprovalDto>.Failure(Error.Conflict("This user already has a pending assignment."));
        var oldUserId = assignment.AssignedToUserId;
        var oldName = assignment.ApproverNameSnapshot;
        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        _db.Set<ProjectChangeRequestApprovalHistory>().Add(new ProjectChangeRequestApprovalHistory
        {
            TenantId = request.TenantId,
            ProjectChangeRequestApprovalId = assignment.Id,
            ProjectId = request.ProjectId,
            FromApproverUserId = oldUserId,
            FromApproverNameSnapshot = oldName,
            ToApproverUserId = replacement.UserId,
            ToApproverNameSnapshot = replacement.FullName,
            ChangedByUserId = context.ActorId,
            ChangedByNameSnapshot = CurrentUserName(),
            Reason = dto.Reason.Trim(),
            ChangedAt = DateTime.UtcNow,
            CreatedBy = userId
        });
        assignment.AssignedToUserId = replacement.UserId;
        assignment.ApproverNameSnapshot = replacement.FullName;
        assignment.RoleSnapshot = replacement.ProjectRole;
        assignment.IsSponsor = replacement.IsSponsor;
        assignment.UpdatedAt = DateTime.UtcNow;
        assignment.UpdatedBy = userId;
        try { await _db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { await transaction.RollbackAsync(cancellationToken); return Result<ProjectChangeRequestApprovalDto>.Failure(Error.Conflict("The approval changed concurrently. Refresh and retry.")); }
        await transaction.CommitAsync(cancellationToken);
        await LogAuditAsync(request, "ReassignApproval", context.ActorId, new { assignmentId, From = oldUserId, To = replacement.UserId, dto.Reason }, cancellationToken);
        await NotifyUsersAsync(request, context.Project!, [replacement.UserId], cancellationToken);
        var result = await BuildDtoAsync(request, cancellationToken);
        return result.IsSuccess
            ? Result<ProjectChangeRequestApprovalDto>.Success(result.Value!.Approvals.First(item => item.Id == assignmentId))
            : Result<ProjectChangeRequestApprovalDto>.Failure(result.Error!);
    }

    private async Task<Result<ProjectChangeRequestDto>> DecideAsync(Guid id, bool approve, string? comment, string actorString, CancellationToken cancellationToken)
    {
        var context = await LoadRequestContextAsync(id, ApprovePermission, cancellationToken, allowAssignedApproverOutOfScope: true);
        if (context.Error is not null) return Result<ProjectChangeRequestDto>.Failure(context.Error);
        var request = context.Request!;
        if (IsReadOnly()) return Result<ProjectChangeRequestDto>.Failure(Error.Forbidden("Cannot decide an approval in read-only view."));
        var actor = context.ActorId;
        var decisionComment = Normalize(comment);
        if (!approve && string.IsNullOrWhiteSpace(decisionComment)) return Result<ProjectChangeRequestDto>.Failure(Error.Validation("A comment is required when rejecting."));
        if (decisionComment?.Length > 2000) return Result<ProjectChangeRequestDto>.Failure(Error.Validation("Decision comments cannot exceed 2000 characters."));
        if (request.Status != ProjectChangeRequest.Statuses.Pending) return Result<ProjectChangeRequestDto>.Failure(Error.Conflict("Only a pending request can receive a decision."));
        if (request.WorkflowInstanceId is null) return Result<ProjectChangeRequestDto>.Failure(Error.Conflict("This legacy request has no approval workflow; an authorized manager must reopen it for setup."));
        if (!await CanActAsync(request, actor, cancellationToken, allowOutOfScope: true)) return Result<ProjectChangeRequestDto>.Failure(Error.Forbidden("You are not the active assigned approver for this request."));
        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var completes = approve && await WouldCompleteAsync(request, actor, cancellationToken);
        decimal? proposedLifetimeCeiling = null;
        if (completes && request.BudgetChangeAmount.HasValue)
        {
            if (!await HasPermissionAsync(actor, request.TenantId, ApproveFinancialCeilingChangePermission, cancellationToken))
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<ProjectChangeRequestDto>.Failure(Error.Forbidden(
                    $"{ApproveFinancialCeilingChangePermission} is required for the final approval of a Change Request that changes the Project lifetime ceiling."));
            }

            var currentProject = context.Project!;
            await _db.Entry(currentProject).ReloadAsync(cancellationToken);
            if (currentProject.IsDeleted || currentProject.TenantId != request.TenantId)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<ProjectChangeRequestDto>.Failure(Error.NotFound("The Project is no longer available in this Tenant."));
            }
            if (!request.BudgetBeforeChange.HasValue || currentProject.Budget != request.BudgetBeforeChange)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<ProjectChangeRequestDto>.Failure(Error.Conflict(
                    "The approved Project lifetime ceiling changed after this request was submitted. Cancel and resubmit the Change Request against the current ceiling."));
            }

            proposedLifetimeCeiling = request.BudgetBeforeChange.Value + request.BudgetChangeAmount.Value;
            var ceilingError = await ValidateLifetimeCeilingRevisionAsync(currentProject, proposedLifetimeCeiling.Value, cancellationToken);
            if (ceilingError is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<ProjectChangeRequestDto>.Failure(ceilingError);
            }
        }
        var transitionCode = !approve ? ProjectChangeRequestWorkflow.Reject
            : completes ? ProjectChangeRequestWorkflow.Complete : ProjectChangeRequestWorkflow.ApproveStep;
        var transition = await ExecuteTransitionAsync(request, transitionCode, decisionComment, actor, cancellationToken);
        if (transition.IsFailure)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<ProjectChangeRequestDto>.Failure(transition.Error!);
        }
        if (proposedLifetimeCeiling.HasValue)
        {
            var project = context.Project!;
            var previousCeiling = project.Budget;
            project.Budget = proposedLifetimeCeiling.Value;
            project.UpdatedAt = DateTime.UtcNow;
            project.UpdatedBy = actor.ToString("D");
            _db.Set<AuditEntry>().Add(new AuditEntry
            {
                TenantId = project.TenantId,
                ProjectId = project.Id,
                EntityType = "ProjectBudget",
                EntityId = project.Id,
                Action = "ApprovedLifetimeCeilingChanged",
                UserId = actor,
                UserName = _http.HttpContext?.User.FindFirst(ClaimTypes.Name)?.Value
                    ?? _http.HttpContext?.User.FindFirst("name")?.Value,
                Timestamp = DateTime.UtcNow,
                Description = $"Project lifetime ceiling changed through approved Change Request CR-{request.Number}.",
                ChangedFields = "Budget",
                Changes = JsonSerializer.Serialize(new { budget = new { from = previousCeiling, to = proposedLifetimeCeiling.Value } }),
                OriginalValues = JsonSerializer.Serialize(new { projectLifetimeCeiling = previousCeiling, currency = project.Currency }),
                NewValues = JsonSerializer.Serialize(new { projectLifetimeCeiling = proposedLifetimeCeiling.Value, currency = project.Currency }),
                CreatedBy = actor.ToString("D")
            });
            await _db.SaveChangesAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        await LogAuditAsync(request, approve ? "ApproveStep" : "Reject", actor, decisionComment, cancellationToken);
        if (approve && request.Status == ProjectChangeRequest.Statuses.Pending)
            await NotifySequentialNextAsync(request, context.Project!, cancellationToken);
        return await BuildDtoAsync(request, cancellationToken);
    }

    private async Task<Error?> ValidateLifetimeCeilingRevisionAsync(Project project, decimal proposed, CancellationToken cancellationToken)
    {
        if (proposed < 0)
            return Error.Validation("The resulting Project lifetime ceiling cannot be negative.");

        var currency = project.Currency?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3 || currency.Any(character => character is < 'A' or > 'Z'))
            return Error.Validation("The Project must have a valid configured currency before its approved lifetime ceiling can change.");

        var annualBudgets = await _db.Set<ProjectAnnualBudget>().IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.TenantId == project.TenantId && item.ProjectId == project.Id && !item.IsDeleted)
            .ToListAsync(cancellationToken);
        if (annualBudgets.Any(item => !string.Equals(item.Currency.Trim(), currency, StringComparison.OrdinalIgnoreCase)))
            return Error.Conflict("Annual budget records use a different currency from the Project. Reconcile the historical currency records before revising the lifetime ceiling.");

        var approvedAnnualAllocations = annualBudgets
            .Where(item => item.Status == ProjectAnnualBudgetStatuses.Approved)
            .Sum(item => item.NewApprovedFunding + item.ApprovedAdjustments);
        var budgetIds = annualBudgets.Select(item => item.Id).ToArray();
        var pendingAnnualReviews = budgetIds.Length == 0
            ? new List<ApprovalRequest>()
            : await _db.Set<ApprovalRequest>().IgnoreQueryFilters().AsNoTracking()
                .Where(item => item.TenantId == project.TenantId && !item.IsDeleted
                    && item.EntityType == "ProjectAnnualBudget" && budgetIds.Contains(item.EntityId)
                    && (item.Type == "ProjectAnnualBudgetReview" || item.Type == "ProjectAnnualBudgetRevision")
                    && item.Status == PaymentRequestStatuses.Pending)
                .ToListAsync(cancellationToken);

        if (pendingAnnualReviews.Any(approval => !approval.Amount.HasValue || approval.Amount.Value < 0m
            || !string.Equals(approval.Currency?.Trim(), currency, StringComparison.OrdinalIgnoreCase)))
            return Error.Conflict("A pending annual budget review has an invalid amount or currency. Resolve the review before revising the lifetime ceiling.");

        var annualBudgetById = annualBudgets.ToDictionary(item => item.Id);
        var pendingIncreaseReservation = pendingAnnualReviews.Sum(approval =>
        {
            if (!approval.Amount.HasValue || !annualBudgetById.TryGetValue(approval.EntityId, out var budget)) return 0m;
            if (approval.Type == "ProjectAnnualBudgetReview" && budget.Status == ProjectAnnualBudgetStatuses.Draft)
                return approval.Amount.Value;
            if (approval.Type == "ProjectAnnualBudgetRevision" && budget.Status == ProjectAnnualBudgetStatuses.Approved)
                return Math.Max(0m, approval.Amount.Value - budget.NewApprovedFunding - budget.ApprovedAdjustments);
            return 0m;
        });

        var requiredForAnnualAllocations = approvedAnnualAllocations + pendingIncreaseReservation;
        if (proposed < requiredForAnnualAllocations)
            return Error.Validation($"The proposed Project lifetime ceiling of {currency} {proposed:N2} is below approved annual allocations and pending submitted increases ({currency} {requiredForAnnualAllocations:N2}). Revise those annual budgets or submit a higher ceiling revision.");

        var financials = _financialLookup is null
            ? null
            : await _financialLookup.GetAsync(project.Id, currency, cancellationToken);
        if (financials is not null && !string.Equals(financials.Currency.Trim(), currency, StringComparison.OrdinalIgnoreCase))
            return Error.Conflict("The financial summary currency does not match the Project currency. Reconcile the financial records before revising the lifetime ceiling.");
        if (proposed < (financials?.AllocatedBudget ?? 0m))
            return Error.Validation($"The proposed Project lifetime ceiling of {currency} {proposed:N2} is below existing Budget Line allocations ({currency} {financials!.AllocatedBudget:N2}). Revise those allocations before approving this Change Request.");
        var actualAndCommitted = financials?.ActualCost ?? 0m;
        if (proposed < actualAndCommitted)
            return Error.Validation($"The proposed Project lifetime ceiling of {currency} {proposed:N2} is below actual costs ({currency} {actualAndCommitted:N2}). The ceiling cannot be reduced below current financial obligations.");

        return null;
    }

    private async Task<Result<WorkflowTransitionResult>> ExecuteTransitionAsync(ProjectChangeRequest request, string transitionCode,
        string? comment, Guid actor, CancellationToken cancellationToken)
    {
        var execution = BuildWorkflowContext(actor.ToString(), comment);
        var instance = await _instances.GetInstanceAsync(ProjectChangeRequestWorkflow.EntityType, request.Id, execution, cancellationToken);
        if (instance.IsFailure || instance.Value is null)
            return Result<WorkflowTransitionResult>.Failure(instance.Error ?? WorkflowErrors.WorkflowInstanceNotFound(ProjectChangeRequestWorkflow.EntityType, request.Id));
        var metadata = transitionCode is ProjectChangeRequestWorkflow.ApproveStep or ProjectChangeRequestWorkflow.Complete or ProjectChangeRequestWorkflow.Reject
            ? new Dictionary<string, string?> { ["approverUserId"] = actor.ToString() }
            : null;
        return await _workflow.ExecuteTransitionAsync(new WorkflowTransitionRequest(ProjectChangeRequestWorkflow.EntityType, request.Id,
            transitionCode, comment, metadata, instance.Value.CurrentStateCode, instance.Value.RowVersion, execution.CorrelationId), execution, cancellationToken);
    }

    private async Task<Result<ProjectWorkflowSelection?>> ResolveWorkflowAsync(Guid projectId, Guid tenantId, string actor, CancellationToken cancellationToken)
    {
        var context = BuildWorkflowContext(actor);
        if (context.TenantId != tenantId) return Result<ProjectWorkflowSelection?>.Failure(Error.Forbidden("The authenticated tenant does not own this project."));
        var configured = await _projectWorkflows.ResolveForNewRecordAsync(projectId, WorkflowDomains.ChangeRequest,
            ProjectChangeRequestWorkflow.EntityType, context, cancellationToken);
        if (configured.IsFailure) return Result<ProjectWorkflowSelection?>.Failure(configured.Error!);
        if (configured.Value is not null) return configured;
        var standard = await _provisioner.EnsureAsync(tenantId, actor, cancellationToken);
        return standard.IsFailure
            ? Result<ProjectWorkflowSelection?>.Failure(standard.Error!)
            : Result<ProjectWorkflowSelection?>.Success(new ProjectWorkflowSelection(WorkflowDomains.ChangeRequest,
                ProjectChangeRequestWorkflow.EntityType, false, standard.Value!.DefinitionId, standard.Value.VersionId,
                ProjectChangeRequestWorkflow.DefinitionCode, "Draft"));
    }

    private async Task<Result<ProjectChangeRequestApprovalOptionsDto>> BuildApprovalOptionsAsync(Project project, Guid requesterId, CancellationToken cancellationToken)
    {
        var policy = await _db.Set<ProjectWorkflowConfiguration>().AsNoTracking().FirstOrDefaultAsync(item => item.ProjectId == project.Id
            && item.TenantId == project.TenantId && item.Domain == WorkflowDomains.ChangeRequest && !item.IsDeleted, cancellationToken);
        var sponsorRequired = policy?.SponsorApprovalRequired ?? true;
        var sources = new Dictionary<Guid, (string Role, bool Sponsor)>();
        void Add(Guid? id, string? role, bool isSponsor = false)
        {
            if (id is not Guid userId || userId == Guid.Empty) return;
            if (sources.TryGetValue(userId, out var old)) sources[userId] = (old.Role, old.Sponsor || isSponsor);
            else sources[userId] = (string.IsNullOrWhiteSpace(role) ? (isSponsor ? "Project Sponsor" : "Project Participant") : role.Trim(), isSponsor);
        }
        Add(project.SponsorId, "Project Sponsor", true);
        Add(project.ProjectManagerId, "Project Manager");
        var members = await (from member in _db.Set<ProjectMember>().AsNoTracking()
                             join resource in _db.Set<Resource>().AsNoTracking() on member.ResourceId equals resource.Id
                             where member.ProjectId == project.Id && member.TenantId == project.TenantId && !member.IsDeleted && member.Status == "Active"
                                 && resource.TenantId == project.TenantId && !resource.IsDeleted && resource.IsActive && resource.UserId.HasValue
                             select new { UserId = resource.UserId!.Value, member.Role }).ToListAsync(cancellationToken);
        foreach (var item in members) Add(item.UserId, item.Role);
        var stakeholders = await _db.Set<ProjectStakeholder>().AsNoTracking().Where(item => item.ProjectId == project.Id && item.TenantId == project.TenantId
            && !item.IsDeleted && item.Status == ProjectStakeholderStatus.Active && item.UserId.HasValue)
            .Select(item => new { UserId = item.UserId!.Value, item.Role }).ToListAsync(cancellationToken);
        foreach (var item in stakeholders) Add(item.UserId, item.Role);
        var resourceStakeholders = await (from stakeholder in _db.Set<ProjectStakeholder>().AsNoTracking()
                                          join resource in _db.Set<Resource>().AsNoTracking() on stakeholder.ResourceId equals resource.Id
                                          where stakeholder.ProjectId == project.Id && stakeholder.TenantId == project.TenantId && !stakeholder.IsDeleted
                                              && stakeholder.Status == ProjectStakeholderStatus.Active && resource.TenantId == project.TenantId
                                              && !resource.IsDeleted && resource.IsActive && resource.UserId.HasValue
                                          select new { UserId = resource.UserId!.Value, stakeholder.Role }).ToListAsync(cancellationToken);
        foreach (var item in resourceStakeholders) Add(item.UserId, item.Role);

        // Business approvers may be outside the project team. Offer only active users
        // in this tenant who currently hold the effective approval permission; the
        // persisted assignment grants access to this request only, never project access.
        var tenantApproverIds = await _db.Set<User>().AsNoTracking()
            .Where(item => item.TenantId == project.TenantId && item.IsActive && !item.IsDeleted && item.Id != requesterId)
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);
        foreach (var userId in tenantApproverIds)
        {
            if (sources.ContainsKey(userId)) continue;
            if (await HasPermissionAsync(userId, project.TenantId, ApprovePermission, cancellationToken))
                Add(userId, "Business Approver");
        }

        var ids = sources.Keys.Where(item => item != requesterId).ToList();
        var users = ids.Count == 0 ? new Dictionary<Guid, User>() : await _db.Set<User>().AsNoTracking().Where(item => ids.Contains(item.Id)
            && item.TenantId == project.TenantId && item.IsActive && !item.IsDeleted).ToDictionaryAsync(item => item.Id, cancellationToken);
        var candidates = new List<ProjectChangeRequestApproverDto>();
        foreach (var id in ids)
        {
            if (!users.TryGetValue(id, out var user) || !await HasPermissionAsync(id, project.TenantId, ApprovePermission, cancellationToken)) continue;
            var source = sources[id];
            candidates.Add(new ProjectChangeRequestApproverDto(id, user.FullName, source.Role, source.Sponsor, sponsorRequired && source.Sponsor));
        }
        candidates = candidates.OrderByDescending(item => item.IsMandatory).ThenBy(item => item.FullName, StringComparer.OrdinalIgnoreCase).ToList();
        var allowed = new List<string> { ProjectChangeRequestApprovalStrategies.AllRequired };
        if (policy?.AllowAnyOneOptionalApprover == true) allowed.Add(ProjectChangeRequestApprovalStrategies.AnyOneOptional);
        if (policy?.AllowSequentialApprovers ?? true) allowed.Add(ProjectChangeRequestApprovalStrategies.Sequential);
        var policyMessage = sponsorRequired && (project.SponsorId is null || !candidates.Any(item => item.IsSponsor))
            ? "The Project Sponsor must be active, have effective approval permission, and cannot be the requester. Assign another Sponsor before submitting."
            : null;
        return Result<ProjectChangeRequestApprovalOptionsDto>.Success(new(candidates, allowed, project.SponsorId, policyMessage));
    }

    private async Task<Error?> ValidateDraftSelectionAsync(Project project, Guid requesterId, IReadOnlyCollection<Guid>? selectedIds, CancellationToken cancellationToken)
    {
        if (selectedIds is null || selectedIds.Count == 0) return null;
        if (selectedIds.Count != selectedIds.Distinct().Count() || selectedIds.Contains(Guid.Empty)) return Error.Validation("Approver selections must be valid and unique.");
        var options = await BuildApprovalOptionsAsync(project, requesterId, cancellationToken);
        if (options.IsFailure) return options.Error;
        var allowed = options.Value!.Approvers.Select(item => item.UserId).ToHashSet();
        return selectedIds.All(allowed.Contains) ? null : Error.Validation("Only active same-tenant users with current Change Request approval permission can be selected.");
    }

    private async Task<bool> CanActAsync(ProjectChangeRequest request, Guid actor, CancellationToken cancellationToken, bool allowOutOfScope = false)
    {
        if (actor == request.RequestedBy || !await HasPermissionAsync(actor, request.TenantId, ApprovePermission, cancellationToken)) return false;
        var project = await FindProjectAsync(request.ProjectId, request.TenantId, cancellationToken, allowOutOfScope);
        if (project is null) return false;
        // A persisted, currently actionable assignment grants access to this request's
        // bounded review projection even if the approver is outside project membership
        // or Management Scope. Assignment eligibility was validated when it was made.
        if (!allowOutOfScope)
        {
            var options = await BuildApprovalOptionsAsync(project, request.RequestedBy, cancellationToken);
            if (options.IsFailure || !options.Value!.Approvers.Any(item => item.UserId == actor)) return false;
        }
        var assignment = await _db.Set<ProjectChangeRequestApproval>().AsNoTracking().Where(item => item.ProjectChangeRequestId == request.Id
            && item.AssignedToUserId == actor && item.Status == ProjectChangeRequestApproval.Statuses.Pending && !item.IsDeleted)
            .OrderBy(item => item.Sequence).FirstOrDefaultAsync(cancellationToken);
        if (assignment is null) return false;
        if (request.ApprovalStrategy == ProjectChangeRequestApprovalStrategies.Sequential)
            return !await _db.Set<ProjectChangeRequestApproval>().AnyAsync(item => item.ProjectChangeRequestId == request.Id
                && item.Sequence < assignment.Sequence && item.Status != ProjectChangeRequestApproval.Statuses.Approved
                && item.Status != ProjectChangeRequestApproval.Statuses.Satisfied && !item.IsDeleted, cancellationToken);
        if (request.ApprovalStrategy == ProjectChangeRequestApprovalStrategies.AnyOneOptional && !assignment.IsMandatory)
        {
            var mandatoryOpen = await _db.Set<ProjectChangeRequestApproval>().AnyAsync(item => item.ProjectChangeRequestId == request.Id && item.IsMandatory
                && item.Status != ProjectChangeRequestApproval.Statuses.Approved && !item.IsDeleted, cancellationToken);
            var optionalDone = await _db.Set<ProjectChangeRequestApproval>().AnyAsync(item => item.ProjectChangeRequestId == request.Id && !item.IsMandatory
                && item.Status == ProjectChangeRequestApproval.Statuses.Approved && !item.IsDeleted, cancellationToken);
            return !mandatoryOpen && !optionalDone;
        }
        return true;
    }

    private async Task<bool> WouldCompleteAsync(ProjectChangeRequest request, Guid actor, CancellationToken cancellationToken)
    {
        var assignments = await _db.Set<ProjectChangeRequestApproval>().AsNoTracking().Where(item => item.ProjectChangeRequestId == request.Id && !item.IsDeleted).ToListAsync(cancellationToken);
        var acting = assignments.FirstOrDefault(item => item.AssignedToUserId == actor && item.Status == ProjectChangeRequestApproval.Statuses.Pending);
        if (acting is null) return false;
        if (request.ApprovalStrategy == ProjectChangeRequestApprovalStrategies.AnyOneOptional)
            return assignments.Where(item => item.IsMandatory).All(item => item.Status == ProjectChangeRequestApproval.Statuses.Approved || item.Id == acting.Id)
                && (acting.IsMandatory
                    ? assignments.Any(item => !item.IsMandatory && item.Status == ProjectChangeRequestApproval.Statuses.Approved)
                    : assignments.Any(item => !item.IsMandatory && (item.Status == ProjectChangeRequestApproval.Statuses.Approved || item.Id == acting.Id)));
        return assignments.All(item => item.Status == ProjectChangeRequestApproval.Statuses.Approved || item.Id == acting.Id);
    }

    private async Task<(Error? Error, ProjectChangeRequest? Request, Project? Project, Guid ActorId)> LoadRequestContextAsync(
        Guid id, string? permission, CancellationToken cancellationToken, bool allowAssignedApproverOutOfScope = false)
    {
        var tenant = CurrentTenant();
        var actor = CurrentUser();
        if (tenant.IsFailure) return (tenant.Error, null, null, Guid.Empty);
        if (actor.IsFailure) return (actor.Error, null, null, Guid.Empty);
        if (permission is not null && !await HasPermissionAsync(actor.Value, tenant.Value, permission, cancellationToken))
            return (Error.Forbidden($"{permission} permission is required."), null, null, actor.Value);
        var request = await _db.Set<ProjectChangeRequest>().FirstOrDefaultAsync(item => item.Id == id && item.TenantId == tenant.Value && !item.IsDeleted, cancellationToken);
        if (request is null) return (Error.NotFound(), null, null, actor.Value);
        var project = await FindProjectAsync(request.ProjectId, tenant.Value, cancellationToken, allowAssignedApproverOutOfScope);
        if (project is null) return (Error.NotFound(), null, null, actor.Value);
        if (allowAssignedApproverOutOfScope && !await CanActAsync(request, actor.Value, cancellationToken, allowOutOfScope: true))
            return (Error.NotFound("This approval is not assigned to the current user."), null, null, actor.Value);
        return (null, request, project, actor.Value);
    }

    private async Task<Project?> FindProjectAsync(Guid projectId, Guid tenantId, CancellationToken cancellationToken, bool allowOutOfScope = false)
    {
        var project = await _db.Set<Project>().FirstOrDefaultAsync(item => item.Id == projectId && item.TenantId == tenantId && !item.IsDeleted, cancellationToken);
        var scope = _management.Current?.Scope.ProjectIds;
        return project is null || (!allowOutOfScope && scope is { Count: > 0 } && !scope.Contains(projectId)) ? null : project;
    }

    private async Task<bool> HasPermissionAsync(Guid userId, Guid tenantId, string permission, CancellationToken cancellationToken)
        => (await _permissions.GetEffectivePermissionsAsync(userId, tenantId, cancellationToken)).Contains(permission);

    private async Task<bool> IsActiveProjectParticipantAsync(Guid projectId, Guid tenantId, Guid userId, CancellationToken cancellationToken)
    {
        var project = await FindProjectAsync(projectId, tenantId, cancellationToken);
        if (project is null) return false;
        var activeUser = await _db.Set<User>().AnyAsync(item => item.Id == userId && item.TenantId == tenantId && item.IsActive && !item.IsDeleted, cancellationToken);
        if (!activeUser) return false;
        if ((project.SponsorId == userId || project.ProjectManagerId == userId)
            && await _db.Set<User>().AnyAsync(item => item.Id == userId && item.TenantId == tenantId && item.IsActive && !item.IsDeleted, cancellationToken)) return true;
        var member = await (from item in _db.Set<ProjectMember>() join resource in _db.Set<Resource>() on item.ResourceId equals resource.Id
                            where item.ProjectId == projectId && item.TenantId == tenantId && !item.IsDeleted && item.Status == "Active"
                                && resource.TenantId == tenantId && !resource.IsDeleted && resource.IsActive && resource.UserId == userId
                            select item.Id).AnyAsync(cancellationToken);
        if (member) return true;
        var stakeholder = await _db.Set<ProjectStakeholder>().AnyAsync(item => item.ProjectId == projectId && item.TenantId == tenantId
            && !item.IsDeleted && item.Status == ProjectStakeholderStatus.Active && item.UserId == userId, cancellationToken);
        if (stakeholder) return true;
        return await (from item in _db.Set<ProjectStakeholder>() join resource in _db.Set<Resource>() on item.ResourceId equals resource.Id
                      where item.ProjectId == projectId && item.TenantId == tenantId && !item.IsDeleted && item.Status == ProjectStakeholderStatus.Active
                          && resource.TenantId == tenantId && !resource.IsDeleted && resource.IsActive && resource.UserId == userId
                      select item.Id).AnyAsync(cancellationToken);
    }

    private async Task<Result<ProjectChangeRequestApprovalOptionsDto>> LegacyBuildApprovalOptionsAsync(Project project, Guid requester, CancellationToken cancellationToken)
    {
        var policy = await _db.Set<ProjectWorkflowConfiguration>().AsNoTracking().FirstOrDefaultAsync(item => item.ProjectId == project.Id
            && item.TenantId == project.TenantId && item.Domain == WorkflowDomains.ChangeRequest && !item.IsDeleted, cancellationToken);
        var sponsorMandatory = policy?.SponsorApprovalRequired ?? true;
        var sources = new Dictionary<Guid, (string Role, bool Sponsor)>();
        void Add(Guid? id, string? role, bool sponsor = false)
        {
            if (id is not Guid userId || userId == Guid.Empty) return;
            if (sources.TryGetValue(userId, out var current)) sources[userId] = (current.Role, current.Sponsor || sponsor);
            else sources[userId] = (string.IsNullOrWhiteSpace(role) ? "Project Participant" : role.Trim(), sponsor);
        }
        Add(project.SponsorId, "Project Sponsor", true);
        Add(project.ProjectManagerId, "Project Manager");
        var members = await (from member in _db.Set<ProjectMember>().AsNoTracking()
                             join resource in _db.Set<Resource>().AsNoTracking() on member.ResourceId equals resource.Id
                             where member.ProjectId == project.Id && member.TenantId == project.TenantId && !member.IsDeleted && member.Status == "Active"
                                 && resource.TenantId == project.TenantId && !resource.IsDeleted && resource.IsActive && resource.UserId.HasValue
                             select new { UserId = resource.UserId!.Value, member.Role }).ToListAsync(cancellationToken);
        foreach (var item in members) Add(item.UserId, item.Role);
        var stakeholders = await _db.Set<ProjectStakeholder>().AsNoTracking().Where(item => item.ProjectId == project.Id && item.TenantId == project.TenantId
            && !item.IsDeleted && item.Status == ProjectStakeholderStatus.Active && item.UserId.HasValue)
            .Select(item => new { UserId = item.UserId!.Value, item.Role }).ToListAsync(cancellationToken);
        foreach (var item in stakeholders) Add(item.UserId, item.Role);
        var resourceStakeholders = await (from stakeholder in _db.Set<ProjectStakeholder>().AsNoTracking()
                                          join resource in _db.Set<Resource>().AsNoTracking() on stakeholder.ResourceId equals resource.Id
                                          where stakeholder.ProjectId == project.Id && stakeholder.TenantId == project.TenantId && !stakeholder.IsDeleted
                                              && stakeholder.Status == ProjectStakeholderStatus.Active && resource.TenantId == project.TenantId
                                              && !resource.IsDeleted && resource.IsActive && resource.UserId.HasValue
                                          select new { UserId = resource.UserId!.Value, stakeholder.Role }).ToListAsync(cancellationToken);
        foreach (var item in resourceStakeholders) Add(item.UserId, item.Role);
        var ids = sources.Keys.Where(item => item != requester).ToList();
        var users = ids.Count == 0 ? new Dictionary<Guid, User>() : await _db.Set<User>().AsNoTracking().Where(item => ids.Contains(item.Id)
            && item.TenantId == project.TenantId && item.IsActive && !item.IsDeleted).ToDictionaryAsync(item => item.Id, cancellationToken);
        var candidates = new List<ProjectChangeRequestApproverDto>();
        foreach (var id in ids)
        {
            if (!users.TryGetValue(id, out var user) || !await HasPermissionAsync(id, project.TenantId, ApprovePermission, cancellationToken)) continue;
            var source = sources[id];
            candidates.Add(new ProjectChangeRequestApproverDto(id, user.FullName, source.Role, source.Sponsor, sponsorMandatory && source.Sponsor));
        }
        candidates = candidates.OrderByDescending(item => item.IsMandatory).ThenBy(item => item.FullName, StringComparer.OrdinalIgnoreCase).ToList();
        var strategies = new List<string> { ProjectChangeRequestApprovalStrategies.AllRequired };
        if (policy?.AllowAnyOneOptionalApprover == true) strategies.Add(ProjectChangeRequestApprovalStrategies.AnyOneOptional);
        if (policy?.AllowSequentialApprovers ?? true) strategies.Add(ProjectChangeRequestApprovalStrategies.Sequential);
        var issue = sponsorMandatory && (project.SponsorId is null || !candidates.Any(item => item.IsSponsor))
            ? "The Project Sponsor must be active, eligible, and different from the requester. Assign another Sponsor before submission."
            : null;
        return Result<ProjectChangeRequestApprovalOptionsDto>.Success(new(candidates, strategies, project.SponsorId, issue));
    }

    private async Task<Error?> LegacyValidateDraftSelectionAsync(Project project, Guid requester, IReadOnlyCollection<Guid>? selected, CancellationToken cancellationToken)
    {
        if (selected is null || selected.Count == 0) return null;
        if (selected.Count != selected.Distinct().Count() || selected.Contains(Guid.Empty)) return Error.Validation("Approver selections must be unique and valid.");
        var options = await BuildApprovalOptionsAsync(project, requester, cancellationToken);
        if (options.IsFailure) return options.Error;
        var candidates = options.Value!.Approvers.Select(item => item.UserId).ToHashSet();
        return selected.All(candidates.Contains) ? null : Error.Validation("Only current eligible project participants can be selected.");
    }

    private async Task<bool> LegacyCanActAsync(ProjectChangeRequest request, Guid userId, CancellationToken cancellationToken)
    {
        if (userId == request.RequestedBy || !await HasPermissionAsync(userId, request.TenantId, ApprovePermission, cancellationToken)
            || !await IsActiveProjectParticipantAsync(request.ProjectId, request.TenantId, userId, cancellationToken)) return false;
        var assignment = await _db.Set<ProjectChangeRequestApproval>().AsNoTracking().Where(item => item.ProjectChangeRequestId == request.Id
            && item.AssignedToUserId == userId && item.Status == ProjectChangeRequestApproval.Statuses.Pending && !item.IsDeleted)
            .OrderBy(item => item.Sequence).FirstOrDefaultAsync(cancellationToken);
        if (assignment is null) return false;
        if (request.ApprovalStrategy == ProjectChangeRequestApprovalStrategies.Sequential)
            return !await _db.Set<ProjectChangeRequestApproval>().AnyAsync(item => item.ProjectChangeRequestId == request.Id && item.Sequence < assignment.Sequence
                && item.Status != ProjectChangeRequestApproval.Statuses.Approved && item.Status != ProjectChangeRequestApproval.Statuses.Satisfied && !item.IsDeleted, cancellationToken);
        if (request.ApprovalStrategy == ProjectChangeRequestApprovalStrategies.AnyOneOptional && !assignment.IsMandatory)
        {
            var mandatoryOpen = await _db.Set<ProjectChangeRequestApproval>().AnyAsync(item => item.ProjectChangeRequestId == request.Id && item.IsMandatory
                && item.Status != ProjectChangeRequestApproval.Statuses.Approved && !item.IsDeleted, cancellationToken);
            var optionalDone = await _db.Set<ProjectChangeRequestApproval>().AnyAsync(item => item.ProjectChangeRequestId == request.Id && !item.IsMandatory
                && item.Status == ProjectChangeRequestApproval.Statuses.Approved && !item.IsDeleted, cancellationToken);
            return !mandatoryOpen && !optionalDone;
        }
        return true;
    }

    private async Task<bool> LegacyWouldCompleteAsync(ProjectChangeRequest request, Guid userId, CancellationToken cancellationToken)
    {
        var assignments = await _db.Set<ProjectChangeRequestApproval>().AsNoTracking().Where(item => item.ProjectChangeRequestId == request.Id && !item.IsDeleted).ToListAsync(cancellationToken);
        var current = assignments.FirstOrDefault(item => item.AssignedToUserId == userId && item.Status == ProjectChangeRequestApproval.Statuses.Pending);
        if (current is null) return false;
        if (request.ApprovalStrategy == ProjectChangeRequestApprovalStrategies.AnyOneOptional)
            return assignments.Where(item => item.IsMandatory).All(item => item.Status == ProjectChangeRequestApproval.Statuses.Approved || item.Id == current.Id)
                && (current.IsMandatory
                    ? assignments.Any(item => !item.IsMandatory && item.Status == ProjectChangeRequestApproval.Statuses.Approved)
                    : assignments.Any(item => !item.IsMandatory && (item.Status == ProjectChangeRequestApproval.Statuses.Approved || item.Id == current.Id)));
        return assignments.All(item => item.Status == ProjectChangeRequestApproval.Statuses.Approved || item.Id == current.Id);
    }

    private async Task<Result<ProjectChangeRequestDto>> LegacyDecideAsync(Guid id, bool approve, string? comment, string actorText, CancellationToken cancellationToken)
    {
        var context = await LoadRequestContextAsync(id, ApprovePermission, cancellationToken);
        if (context.Error is not null) return Result<ProjectChangeRequestDto>.Failure(context.Error);
        var request = context.Request!;
        if (IsReadOnly()) return Result<ProjectChangeRequestDto>.Failure(Error.Forbidden("Cannot decide an approval in read-only view."));
        if (request.Status != ProjectChangeRequest.Statuses.Pending) return Result<ProjectChangeRequestDto>.Failure(Error.Conflict("Only a Pending request accepts decisions."));
        if (!request.WorkflowInstanceId.HasValue) return Result<ProjectChangeRequestDto>.Failure(Error.Conflict("This legacy request has no assignment. Reopen it for approval setup."));
        if (!await CanActAsync(request, context.ActorId, cancellationToken)) return Result<ProjectChangeRequestDto>.Failure(Error.Forbidden("You are not the active assigned approver."));
        var normalized = Normalize(comment);
        if (!approve && string.IsNullOrWhiteSpace(normalized)) return Result<ProjectChangeRequestDto>.Failure(Error.Validation("A comment is required when rejecting."));
        if (normalized?.Length > 2000) return Result<ProjectChangeRequestDto>.Failure(Error.Validation("Decision comments cannot exceed 2000 characters."));
        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var complete = approve && await WouldCompleteAsync(request, context.ActorId, cancellationToken);
        var transitionCode = !approve ? ProjectChangeRequestWorkflow.Reject : complete ? ProjectChangeRequestWorkflow.Complete : ProjectChangeRequestWorkflow.ApproveStep;
        var transition = await ExecuteTransitionAsync(request, transitionCode, normalized, context.ActorId, cancellationToken);
        if (transition.IsFailure)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<ProjectChangeRequestDto>.Failure(transition.Error!);
        }
        await transaction.CommitAsync(cancellationToken);
        await LogAuditAsync(request, approve ? "ApproveStep" : "Reject", context.ActorId, normalized, cancellationToken);
        if (approve && request.Status == ProjectChangeRequest.Statuses.Pending) await NotifySequentialNextAsync(request, context.Project!, cancellationToken);
        return await BuildDtoAsync(request, cancellationToken);
    }

    private async Task<Result<WorkflowTransitionResult>> LegacyExecuteTransitionAsync(ProjectChangeRequest request, string code, string? comment, Guid actor, CancellationToken cancellationToken)
    {
        var context = BuildWorkflowContext(actor.ToString(), comment);
        var instance = await _instances.GetInstanceAsync(ProjectChangeRequestWorkflow.EntityType, request.Id, context, cancellationToken);
        if (instance.IsFailure || instance.Value is null)
            return Result<WorkflowTransitionResult>.Failure(instance.Error ?? WorkflowErrors.WorkflowInstanceNotFound(ProjectChangeRequestWorkflow.EntityType, request.Id));
        var metadata = code is ProjectChangeRequestWorkflow.ApproveStep or ProjectChangeRequestWorkflow.Complete or ProjectChangeRequestWorkflow.Reject
            ? new Dictionary<string, string?> { ["approverUserId"] = actor.ToString() } : null;
        return await _workflow.ExecuteTransitionAsync(new WorkflowTransitionRequest(ProjectChangeRequestWorkflow.EntityType, request.Id,
            code, comment, metadata, instance.Value.CurrentStateCode, instance.Value.RowVersion, context.CorrelationId), context, cancellationToken);
    }

    private async Task<(Error? Error, ProjectChangeRequest? Request, Project? Project, Guid ActorId)> LegacyLoadRequestContextAsync(Guid id, string? permission, CancellationToken cancellationToken)
    {
        var tenant = CurrentTenant();
        var actor = CurrentUser();
        if (tenant.IsFailure) return (tenant.Error, null, null, Guid.Empty);
        if (actor.IsFailure) return (actor.Error, null, null, Guid.Empty);
        if (permission is not null && !await HasPermissionAsync(actor.Value, tenant.Value, permission, cancellationToken))
            return (Error.Forbidden($"{permission} permission is required."), null, null, actor.Value);
        var request = await _db.Set<ProjectChangeRequest>().FirstOrDefaultAsync(item => item.Id == id && item.TenantId == tenant.Value && !item.IsDeleted, cancellationToken);
        if (request is null) return (Error.NotFound(), null, null, actor.Value);
        var project = await FindProjectAsync(request.ProjectId, tenant.Value, cancellationToken);
        return project is null ? (Error.NotFound(), null, null, actor.Value) : (null, request, project, actor.Value);
    }

    private async Task<Result<ProjectChangeRequestDto>> BuildDtoAsync(ProjectChangeRequest request, CancellationToken cancellationToken)
    {
        var project = await _db.Set<Project>().AsNoTracking().FirstOrDefaultAsync(item => item.Id == request.ProjectId && item.TenantId == request.TenantId, cancellationToken);
        if (project is null) return Result<ProjectChangeRequestDto>.Failure(Error.NotFound());
        var requester = await _db.Set<User>().IgnoreQueryFilters().AsNoTracking().FirstOrDefaultAsync(item => item.Id == request.RequestedBy && item.TenantId == request.TenantId, cancellationToken);
        var reviewer = request.ReviewedBy.HasValue ? await _db.Set<User>().IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == request.ReviewedBy && item.TenantId == request.TenantId, cancellationToken) : null;
        var dto = request.ToDto(project.Name, requester?.FullName ?? request.RequestedBy.ToString(), reviewer?.FullName, project.PublicId);
        if (request.Status == ProjectChangeRequest.Statuses.Draft)
            dto.SelectedApproverUserIds = ParseIds(request.SelectedApproverUserIdsJson).ToArray();
        var assignments = await _db.Set<ProjectChangeRequestApproval>().AsNoTracking().Where(item => item.ProjectChangeRequestId == request.Id
            && item.TenantId == request.TenantId && !item.IsDeleted).OrderBy(item => item.Sequence).ToListAsync(cancellationToken);
        var assignmentIds = assignments.Select(item => item.Id).ToList();
        var history = assignmentIds.Count == 0 ? new List<ProjectChangeRequestApprovalHistory>() : await _db.Set<ProjectChangeRequestApprovalHistory>().AsNoTracking()
            .Where(item => item.TenantId == request.TenantId && assignmentIds.Contains(item.ProjectChangeRequestApprovalId)).OrderBy(item => item.ChangedAt).ToListAsync(cancellationToken);
        dto.Approvals = assignments.Select(item => new ProjectChangeRequestApprovalDto(item.Id, item.AssignedToUserId, item.ApproverNameSnapshot,
            item.OriginalApproverUserId, item.OriginalApproverNameSnapshot, item.RoleSnapshot, item.IsSponsor, item.IsMandatory, item.Sequence, item.Status,
            item.DecidedAt, item.DecisionActorId, item.DecisionComment,
            history.Where(change => change.ProjectChangeRequestApprovalId == item.Id).Select(change => new ProjectChangeRequestApprovalHistoryDto(
                change.FromApproverUserId, change.FromApproverNameSnapshot, change.ToApproverUserId, change.ToApproverNameSnapshot,
                change.ChangedByUserId, change.ChangedByNameSnapshot, change.Reason, change.ChangedAt)).ToList())).ToList();
        if (request.ApprovalStrategy == ProjectChangeRequestApprovalStrategies.AnyOneOptional)
        {
            dto.RequiredApprovalCount = assignments.Count(item => item.IsMandatory) + (assignments.Any(item => !item.IsMandatory) ? 1 : 0);
            dto.ApprovalCount = assignments.Count(item => item.IsMandatory && item.Status == ProjectChangeRequestApproval.Statuses.Approved)
                + (assignments.Any(item => !item.IsMandatory && item.Status == ProjectChangeRequestApproval.Statuses.Approved) ? 1 : 0);
        }
        else
        {
            dto.RequiredApprovalCount = assignments.Count;
            dto.ApprovalCount = assignments.Count(item => item.Status == ProjectChangeRequestApproval.Statuses.Approved);
        }
        var pending = assignments.Where(item => item.Status == ProjectChangeRequestApproval.Statuses.Pending).ToList();
        if (request.Status == ProjectChangeRequest.Statuses.Pending)
        {
            if (request.ApprovalStrategy == ProjectChangeRequestApprovalStrategies.Sequential)
                dto.PendingWith = pending.OrderBy(item => item.Sequence).Take(1).Select(item => item.ApproverNameSnapshot).ToArray();
            else if (request.ApprovalStrategy == ProjectChangeRequestApprovalStrategies.AnyOneOptional)
                dto.PendingWith = pending.Any(item => item.IsMandatory) ? pending.Where(item => item.IsMandatory).Select(item => item.ApproverNameSnapshot).ToArray()
                    : assignments.Any(item => !item.IsMandatory && item.Status == ProjectChangeRequestApproval.Statuses.Approved) ? []
                    : pending.Where(item => !item.IsMandatory).Select(item => item.ApproverNameSnapshot).ToArray();
            else dto.PendingWith = pending.Select(item => item.ApproverNameSnapshot).ToArray();
        }
        dto.RequiresApprovalSetup = request.Status == ProjectChangeRequest.Statuses.Pending && (assignments.Count == 0 || request.WorkflowInstanceId is null);
        return Result<ProjectChangeRequestDto>.Success(dto);
    }

    private async Task NotifyActionableAsync(ProjectChangeRequest request, Project project, CancellationToken cancellationToken)
    {
        var assignments = await _db.Set<ProjectChangeRequestApproval>().AsNoTracking().Where(item => item.ProjectChangeRequestId == request.Id
            && item.Status == ProjectChangeRequestApproval.Statuses.Pending && !item.IsDeleted).ToListAsync(cancellationToken);
        var recipientIds = request.ApprovalStrategy == ProjectChangeRequestApprovalStrategies.Sequential
            ? assignments.OrderBy(item => item.Sequence).Take(1).Select(item => item.AssignedToUserId)
            : assignments.Select(item => item.AssignedToUserId).Distinct();
        await NotifyUsersAsync(request, project, recipientIds, cancellationToken);
    }

    private async Task NotifySequentialNextAsync(ProjectChangeRequest request, Project project, CancellationToken cancellationToken)
    {
        if (request.ApprovalStrategy != ProjectChangeRequestApprovalStrategies.Sequential) return;
        var next = await _db.Set<ProjectChangeRequestApproval>().AsNoTracking().Where(item => item.ProjectChangeRequestId == request.Id
            && item.Status == ProjectChangeRequestApproval.Statuses.Pending && !item.IsDeleted).OrderBy(item => item.Sequence).FirstOrDefaultAsync(cancellationToken);
        if (next is not null) await NotifyUsersAsync(request, project, [next.AssignedToUserId], cancellationToken);
    }

    private async Task NotifyUsersAsync(ProjectChangeRequest request, Project project, IEnumerable<Guid> userIds, CancellationToken cancellationToken)
    {
        var organizationId = _projectOrganizationLookup is null
            ? null
            : await _projectOrganizationLookup.ResolveOrganizationIdAsync(request.TenantId, project.Id, cancellationToken);
        foreach (var userId in userIds.Distinct())
        {
            try
            {
                await _notifications.SendAsync(new Notification
                {
                    TenantId = request.TenantId, OrganizationId = organizationId, UserId = userId,
                    Title = $"Change Request #{request.Number} requires your approval",
                    Body = $"Project: {project.Name}. {request.Reason}",
                    Type = NotificationTypes.ProjectChangeRequest,
                    ReferenceType = "ProjectChangeRequest", ReferenceId = request.Id, SentAt = DateTime.UtcNow
                }, cancellationToken);
            }
            catch { /* In-app delivery is supplementary; the persisted request remains authoritative. */ }
        }
    }

    private Error? ValidateInput(CreateProjectChangeRequestDto dto, Project project)
    {
        if (!Enum.IsDefined(dto.Type)) return Error.Validation("Select a supported Change Request type.");
        if (string.IsNullOrWhiteSpace(dto.Reason) || dto.Reason.Trim().Length > 500) return Error.Validation("A reason of at most 500 characters is required.");
        if (dto.Justification?.Length > 2000 || dto.Impact?.Length > 2000 || dto.Attachments?.Length > 4000) return Error.Validation("Request details exceed the allowed length.");
        // A Budget Change request must carry a non-zero financial impact; a non-budget request must not.
        if (dto.Type == ProjectChangeRequest.ChangeTypes.BudgetChange && !dto.BudgetChangeAmount.HasValue)
            return Error.Validation("A Budget Change request requires a non-zero change amount.");
        if (dto.Type != ProjectChangeRequest.ChangeTypes.BudgetChange && dto.BudgetChangeAmount.HasValue)
            return Error.Validation("Only a Budget Change request can carry a budget impact amount.");
        if (dto.BudgetChangeAmount.HasValue && (dto.BudgetChangeAmount == 0 || decimal.Round(dto.BudgetChangeAmount.Value, 2) != dto.BudgetChangeAmount
            || string.IsNullOrWhiteSpace(dto.Justification) || !project.Budget.HasValue || project.Budget + dto.BudgetChangeAmount < 0))
            return Error.Validation("Budget impact requires a project ceiling, a non-zero amount with at most two decimals, a non-negative resulting budget, and justification.");
        return ProjectChangeRequestApprovalStrategies.IsSupported(dto.ApprovalStrategy) ? null : Error.Validation("Select a supported approval strategy.");
    }

    private async Task<Result> SaveWithConcurrencyAsync(CancellationToken cancellationToken)
    {
        try { await _db.SaveChangesAsync(cancellationToken); return Result.Success(); }
        catch (DbUpdateConcurrencyException) { return Result.Failure(Error.Conflict("The Change Request changed concurrently. Refresh and retry.")); }
    }

    private async Task LogAuditAsync(ProjectChangeRequest request, string action, Guid actor, object? values, CancellationToken cancellationToken)
    {
        await _audit.LogAsync(new AuditEntry { TenantId = request.TenantId, EntityType = "ProjectChangeRequest", EntityId = request.Id,
            Action = action, UserId = actor, NewValues = JsonSerializer.Serialize(values), Timestamp = DateTime.UtcNow, CreatedBy = actor.ToString() }, cancellationToken);
    }

    private WorkflowExecutionContext BuildWorkflowContext(string userId, string? comment = null)
    {
        var principal = _http.HttpContext?.User;
        var tenantId = Guid.TryParse(principal?.FindFirst("tenantId")?.Value, out var id) ? id : Guid.Empty;
        var metadata = principal?.Claims.Where(item => item.Type == "permission").GroupBy(item => item.Value, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, _ => (string?)"true", StringComparer.OrdinalIgnoreCase);
        var readOnly = principal?.FindFirst("readOnly")?.Value == "true" || principal?.FindFirst("viewAs")?.Value == "true" || _management.Current?.EffectiveReadOnly == true;
        return new WorkflowExecutionContext(tenantId, userId, CurrentUserName(), readOnly,
            _http.HttpContext?.Request.Headers["X-Correlation-ID"].FirstOrDefault(), comment, metadata);
    }

    private bool IsReadOnly() => _http.HttpContext?.User.FindFirst("readOnly")?.Value.Equals("true", StringComparison.OrdinalIgnoreCase) == true
        || _http.HttpContext?.User.FindFirst("viewAs")?.Value.Equals("true", StringComparison.OrdinalIgnoreCase) == true
        || _management.Current?.EffectiveReadOnly == true;
    private string CurrentUserName() => _http.HttpContext?.User.FindFirst(ClaimTypes.Name)?.Value ?? _http.HttpContext?.User.FindFirst("name")?.Value ?? "NEXUS User";
    private Result<Guid> CurrentTenant() => Guid.TryParse(_http.HttpContext?.User.FindFirst("tenantId")?.Value, out var id) && id != Guid.Empty
        ? Result<Guid>.Success(id) : Result<Guid>.Failure(Error.Unauthorized("Tenant ID is missing or invalid."));
    private Result<Guid> CurrentUser() => Guid.TryParse(_http.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) && id != Guid.Empty
        ? Result<Guid>.Success(id) : Result<Guid>.Failure(Error.Unauthorized("User ID is missing or invalid."));
    private static string NormalizeStrategy(string? value) => value?.Trim().ToUpperInvariant() switch
    {
        "ANYONEOPTIONAL" or "ANY_ONE_OPTIONAL" or "ANYONE" => ProjectChangeRequestApprovalStrategies.AnyOneOptional,
        "SEQUENTIAL" => ProjectChangeRequestApprovalStrategies.Sequential,
        _ => ProjectChangeRequestApprovalStrategies.AllRequired
    };
    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static List<Guid> ParseIds(string json) { try { return JsonSerializer.Deserialize<List<Guid>>(json) ?? []; } catch (JsonException) { return []; } }
}
