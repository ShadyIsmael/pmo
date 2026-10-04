using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.WorkflowEngine.Application.Contracts;
using NEXUS.Modules.WorkflowEngine.Domain;
using NEXUS.Modules.WorkItems.Domain;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Helpers;
using NEXUS.SharedKernel.Management;

namespace NEXUS.Modules.WorkItems.Application;

public static class WorkItemWorkflow
{
    public const string EntityType = "WorkItem";
    public const string DefinitionCode = "WORKITEM_STANDARD";
}

public sealed record WorkItemWorkflowProvisioningResult(
    Guid DefinitionId,
    Guid VersionId,
    string InitialStateCode,
    int StateCount,
    int TransitionCount);

public sealed record WorkItemWorkflowOnboardingException(
    Guid WorkItemId,
    string StatusCode,
    string Reason);

public sealed record WorkItemWorkflowOnboardingReport(
    int CreatedInstanceCount,
    IReadOnlyList<WorkItemWorkflowOnboardingException> Exceptions);

public interface IWorkItemWorkflowProvisioningService
{
    Task<Result<WorkItemWorkflowProvisioningResult>> EnsureCurrentTenantAsync(string actor, CancellationToken cancellationToken = default);
    Task<Result<WorkItemWorkflowOnboardingReport>> OnboardCurrentTenantAsync(string actor, CancellationToken cancellationToken = default);
}

public sealed class WorkItemWorkflowProvisioningService : IWorkItemWorkflowProvisioningService
{
    private readonly DbContext _context;
    private readonly IWorkItemStatusReferenceLookup _statusLookup;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public WorkItemWorkflowProvisioningService(
        DbContext context,
        IWorkItemStatusReferenceLookup statusLookup,
        IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _statusLookup = statusLookup;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<Result<WorkItemWorkflowProvisioningResult>> EnsureCurrentTenantAsync(
        string actor,
        CancellationToken cancellationToken = default)
    {
        var tenantId = CurrentTenantId();
        var statuses = (await _statusLookup.GetAsync(false, cancellationToken))
            .OrderBy(status => status.DisplayOrder)
            .ThenBy(status => status.Code, StringComparer.Ordinal)
            .ToList();

        var defaults = statuses.Where(status => status.IsDefault).ToList();
        if (defaults.Count != 1)
            return Result<WorkItemWorkflowProvisioningResult>.Failure(Error.Validation("Exactly one active default Work Item status is required before the workflow can be provisioned."));

        if (statuses.Count == 0)
            return Result<WorkItemWorkflowProvisioningResult>.Failure(Error.Validation("At least one active Work Item status is required before the workflow can be provisioned."));

        var definition = await _context.Set<WorkflowDefinition>()
            .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.Code == WorkItemWorkflow.DefinitionCode && item.EntityType == WorkItemWorkflow.EntityType && !item.IsDeleted, cancellationToken);

        if (definition is not null && definition.ActivePublishedVersionId.HasValue)
        {
            var published = await _context.Set<WorkflowDefinitionVersion>()
                .Include(version => version.States)
                .Include(version => version.Transitions)
                .FirstOrDefaultAsync(version => version.Id == definition.ActivePublishedVersionId.Value && version.LifecycleStatus == WorkflowDefinitionVersionLifecycleStatus.Published && !version.IsDeleted, cancellationToken);

            if (published is not null)
            {
                var initialStates = published.States.Where(state => state.IsInitial && state.IsActive).ToList();
                if (initialStates.Count != 1)
                    return Result<WorkItemWorkflowProvisioningResult>.Failure(Error.Validation("The published Work Item workflow must contain exactly one active initial state."));
                var initial = initialStates[0];

                return Result<WorkItemWorkflowProvisioningResult>.Success(new WorkItemWorkflowProvisioningResult(
                    definition.Id,
                    published.Id,
                    initial.Code,
                    published.States.Count(state => state.IsActive),
                    published.Transitions.Count(transition => transition.IsActive)));
            }
        }

        definition ??= new WorkflowDefinition
        {
            TenantId = tenantId,
            Code = WorkItemWorkflow.DefinitionCode,
            Name = "Standard Work Item Workflow",
            Description = "Compatibility workflow generated from the tenant's active Work Item statuses.",
            Domain = WorkflowDomains.Task,
            EntityType = WorkItemWorkflow.EntityType,
            IsActive = true,
            CreatedBy = actor
        };

        if (definition.Id == Guid.Empty) definition.Id = Guid.NewGuid();

        var existingVersions = await _context.Set<WorkflowDefinitionVersion>()
            .Where(version => version.WorkflowDefinitionId == definition.Id && !version.IsDeleted)
            .Select(version => version.VersionNumber)
            .ToListAsync(cancellationToken);

        var version = new WorkflowDefinitionVersion
        {
            TenantId = tenantId,
            WorkflowDefinitionId = definition.Id,
            VersionNumber = existingVersions.DefaultIfEmpty(0).Max() + 1,
            LifecycleStatus = WorkflowDefinitionVersionLifecycleStatus.Published,
            ChangeLog = "Phase 3 WorkItems compatibility workflow",
            PublishedAt = DateTime.UtcNow,
            PublishedBy = actor,
            CreatedBy = actor
        };

        var stateByCode = new Dictionary<string, WorkflowState>(StringComparer.OrdinalIgnoreCase);
        foreach (var status in statuses)
        {
            var state = new WorkflowState
            {
                TenantId = tenantId,
                WorkflowDefinitionVersionId = version.Id,
                Code = status.Code,
                NameEn = status.NameEn,
                NameAr = status.NameAr,
                DisplayOrder = status.DisplayOrder,
                IsInitial = status.IsDefault,
                // Existing WorkItems allow reopening Done/Cancelled items. The
                // compatibility version therefore keeps every state reopenable.
                IsFinal = false,
                IsActive = true,
                CreatedBy = actor
            };
            stateByCode.Add(state.Code, state);
            version.States.Add(state);
        }

        foreach (var from in statuses)
        {
            foreach (var to in statuses.Where(candidate => !string.Equals(candidate.Code, from.Code, StringComparison.OrdinalIgnoreCase)))
            {
                var transition = new WorkflowTransition
                {
                    TenantId = tenantId,
                    WorkflowDefinitionVersionId = version.Id,
                    Code = BuildTransitionCode(from.Code, to.Code),
                    NameEn = BuildTransitionNameEn(to.NameEn),
                    NameAr = BuildTransitionNameAr(to.NameAr),
                    FromStateId = stateByCode[from.Code].Id,
                    ToStateId = stateByCode[to.Code].Id,
                    DisplayOrder = to.DisplayOrder,
                    IsActive = true,
                    CreatedBy = actor
                };
                transition.Actions.Add(new WorkflowTransitionAction
                {
                    TenantId = tenantId,
                    WorkflowTransitionId = transition.Id,
                    ActionType = "DomainProjection",
                    ConfigurationJson = JsonSerializer.Serialize(new { targetStateCode = to.Code }),
                    ExecutionOrder = 10,
                    IsActive = true,
                    CreatedBy = actor
                });
                transition.Actions.Add(new WorkflowTransitionAction
                {
                    TenantId = tenantId,
                    WorkflowTransitionId = transition.Id,
                    ActionType = "AuditProjection",
                    ConfigurationJson = "{}",
                    ExecutionOrder = 20,
                    IsActive = true,
                    CreatedBy = actor
                });
                version.Transitions.Add(transition);
            }
        }

        if (definition.Versions.All(candidate => candidate.Id != version.Id)) definition.Versions.Add(version);

        // WorkflowDefinitionVersion.WorkflowDefinition and
        // WorkflowDefinition.ActivePublishedVersion form an intentional
        // two-way relationship. EF Core cannot order a single SaveChanges
        // call when both ends are new, so persist the version first and set
        // the published-version pointer in a second phase within one
        // transaction.
        definition.ActivePublishedVersionId = null;
        definition.ActivePublishedVersion = null;

        if (_context.Entry(definition).State == EntityState.Detached)
            _context.Set<WorkflowDefinition>().Add(definition);

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await _context.SaveChangesAsync(cancellationToken);

            definition.ActivePublishedVersionId = version.Id;
            definition.ActivePublishedVersion = version;
            await _context.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Result<WorkItemWorkflowProvisioningResult>.Success(new WorkItemWorkflowProvisioningResult(
            definition.Id,
            version.Id,
            defaults[0].Code,
            statuses.Count,
            version.Transitions.Count));
    }

    public async Task<Result<WorkItemWorkflowOnboardingReport>> OnboardCurrentTenantAsync(
        string actor,
        CancellationToken cancellationToken = default)
    {
        var provisioning = await EnsureCurrentTenantAsync(actor, cancellationToken);
        if (provisioning.IsFailure)
            return Result<WorkItemWorkflowOnboardingReport>.Failure(provisioning.Error!);

        var version = await _context.Set<WorkflowDefinitionVersion>()
            .Include(item => item.States)
            .FirstAsync(item => item.Id == provisioning.Value!.VersionId, cancellationToken);
        var states = version.States.Where(state => state.IsActive).ToDictionary(state => state.Code, StringComparer.OrdinalIgnoreCase);
        var items = await _context.Set<WorkItem>()
            .Where(item => item.TenantId == CurrentTenantId() && !item.IsDeleted)
            .OrderBy(item => item.Id)
            .ToListAsync(cancellationToken);
        var itemIds = items.Select(item => item.Id).ToArray();
        var instances = await _context.Set<WorkflowInstance>()
            .Include(instance => instance.CurrentState)
            .Where(instance => instance.TenantId == CurrentTenantId()
                && instance.EntityType == WorkItemWorkflow.EntityType
                && itemIds.Contains(instance.EntityId)
                && instance.IsActive
                && !instance.IsDeleted)
            .ToListAsync(cancellationToken);
        var duplicateInstanceItemIds = instances
            .GroupBy(instance => instance.EntityId)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet();
        var byItem = instances
            .Where(instance => !duplicateInstanceItemIds.Contains(instance.EntityId))
            .ToDictionary(instance => instance.EntityId);
        var exceptions = new List<WorkItemWorkflowOnboardingException>();
        var created = 0;

        foreach (var item in items)
        {
            if (duplicateInstanceItemIds.Contains(item.Id))
            {
                exceptions.Add(new(item.Id, item.Status, "Multiple active workflow instances exist for this Work Item."));
                continue;
            }

            if (byItem.TryGetValue(item.Id, out var existing))
            {
                if (!string.Equals(existing.CurrentState.Code, item.Status, StringComparison.OrdinalIgnoreCase))
                    exceptions.Add(new(item.Id, item.Status, $"Active workflow state '{existing.CurrentState.Code}' does not match WorkItem.Status '{item.Status}'."));
                continue;
            }

            if (!states.TryGetValue(item.Status, out var state))
            {
                exceptions.Add(new(item.Id, item.Status, "WorkItem.Status does not map to an active published workflow state."));
                continue;
            }

            _context.Set<WorkflowInstance>().Add(new WorkflowInstance
            {
                TenantId = item.TenantId,
                WorkflowDefinitionId = version.WorkflowDefinitionId,
                WorkflowDefinitionVersionId = version.Id,
                EntityType = WorkItemWorkflow.EntityType,
                EntityId = item.Id,
                CurrentStateId = state.Id,
                StartedAt = item.CreatedAt,
                StartedBy = string.IsNullOrWhiteSpace(item.CreatedBy) ? actor : item.CreatedBy,
                IsActive = true,
                CreatedBy = actor
            });
            created++;
        }

        if (created > 0) await _context.SaveChangesAsync(cancellationToken);
        return Result<WorkItemWorkflowOnboardingReport>.Success(new WorkItemWorkflowOnboardingReport(created, exceptions));
    }

    private Guid CurrentTenantId() => Guid.TryParse(_httpContextAccessor.HttpContext?.User.FindFirstValue("tenantId"), out var tenantId)
        ? tenantId
        : TenantConstants.DemoTenantId;

    private static string BuildTransitionCode(string fromCode, string toCode)
    {
        var normalized = $"MOVE_{NormalizeCode(fromCode)}_TO_{NormalizeCode(toCode)}";
        if (normalized.Length <= 100) return normalized;
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)))[..12];
        return $"MOVE_{NormalizeCode(fromCode)[..Math.Min(35, NormalizeCode(fromCode).Length)]}_TO_{hash}";
    }

    private static string NormalizeCode(string value) => new(value.Select(character => char.IsLetterOrDigit(character) ? char.ToUpperInvariant(character) : '_').ToArray());
    private static string BuildTransitionNameEn(string target) => $"Move to {target}";
    private static string BuildTransitionNameAr(string target) => $"النقل إلى {target}";
}

public sealed class WorkItemWorkflowAdapter : IWorkflowEntityAdapter
{
    private readonly DbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IManagementContextAccessor _managementContext;
    private readonly IWorkItemStatusReferenceLookup _statusLookup;

    public WorkItemWorkflowAdapter(
        DbContext context,
        IHttpContextAccessor httpContextAccessor,
        IManagementContextAccessor managementContext,
        IWorkItemStatusReferenceLookup statusLookup)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
        _managementContext = managementContext;
        _statusLookup = statusLookup;
    }

    public string EntityType => WorkItemWorkflow.EntityType;

    public async Task<Result<WorkflowEntitySnapshot>> GetSnapshotAsync(Guid entityId, WorkflowExecutionContext context, CancellationToken cancellationToken = default)
    {
        var item = await FindAccessibleAsync(entityId, context, cancellationToken);
        if (item is null) return Result<WorkflowEntitySnapshot>.Failure(WorkflowErrors.EntityNotAccessible(EntityType, entityId));
        return Result<WorkflowEntitySnapshot>.Success(new WorkflowEntitySnapshot(EntityType, entityId, new Dictionary<string, object?>
        {
            ["StateCode"] = item.Status,
            ["StateNameEn"] = item.Status,
            ["StateNameAr"] = item.Status,
            ["Type"] = item.Type,
            ["HasAssignee"] = item.AssignedResourceId.HasValue || item.AssignedTeamId.HasValue,
            ["Priority"] = item.Priority,
            ["HasDueDate"] = item.DueDate.HasValue || item.TargetResolutionDate.HasValue,
            ["HasResolution"] = !string.IsNullOrWhiteSpace(item.Resolution),
            ["ProjectId"] = item.ProjectId,
            ["IsProjectScoped"] = item.ProjectId.HasValue
        }));
    }

    public async Task<Result> AuthorizeAsync(WorkflowExecutionContext context, Guid entityId, CancellationToken cancellationToken = default)
    {
        if (!HasWorkItemPermission()) return Result.Failure(Error.Forbidden("Work Item access is required."));
        return await FindAccessibleAsync(entityId, context, cancellationToken) is null
            ? Result.Failure(WorkflowErrors.EntityNotAccessible(EntityType, entityId))
            : Result.Success();
    }

    public async Task<Result> ValidateTransitionAsync(WorkflowTransitionContext context, CancellationToken cancellationToken = default)
    {
        var status = await _statusLookup.FindAsync(context.ToState.Code, cancellationToken);
        if (status is null || !status.IsActive)
            return Result.Failure(Error.Conflict($"Work Item status '{context.ToState.Code}' is not active or does not exist."));
        return Result.Success();
    }

    public async Task<Result> ApplyTransitionAsync(WorkflowTransitionContext context, CancellationToken cancellationToken = default)
    {
        var item = await FindAccessibleAsync(context.EntityId, context.Execution, cancellationToken, tracked: true);
        if (item is null) return Result.Failure(WorkflowErrors.EntityNotAccessible(EntityType, context.EntityId));
        if (!string.Equals(item.Status, context.FromState.Code, StringComparison.OrdinalIgnoreCase))
            return Result.Failure(WorkflowErrors.WorkflowDomainStateMismatch(context.FromState.Code, item.Status));

        var oldStatus = item.Status;
        item.ApplyWorkflowStatusProjection(context.ToState.Code, context.Execution.UserId);
        var isDone = string.Equals(context.ToState.Code, "Done", StringComparison.OrdinalIgnoreCase);
        if (string.Equals(item.Type, WorkItemType.Issue.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            item.IdentifiedDate ??= DateTime.UtcNow;
            item.ResolvedDate = isDone ? item.ResolvedDate ?? DateTime.UtcNow : null;
        }
        item.CompletedAt = isDone ? item.CompletedAt ?? DateTime.UtcNow : null;

        _context.Set<AuditEntry>().Add(new AuditEntry
        {
            TenantId = item.TenantId,
            EntityType = EntityType,
            EntityId = item.Id,
            Action = "Transitioned",
            Changes = JsonSerializer.Serialize(new { status = new { from = oldStatus, to = context.ToState.Code }, transition = context.Transition.Code }),
            UserId = Guid.TryParse(context.Execution.UserId, out var userId) ? userId : null,
            UserName = context.Execution.UserName,
            Timestamp = DateTime.UtcNow,
            CreatedBy = context.Execution.UserId
        });
        return Result.Success();
    }

    private async Task<WorkItem?> FindAccessibleAsync(Guid entityId, WorkflowExecutionContext context, CancellationToken cancellationToken, bool tracked = false)
    {
        var query = _context.Set<WorkItem>().AsQueryable();
        if (!tracked) query = query.AsNoTracking();
        var item = await query.FirstOrDefaultAsync(candidate => candidate.Id == entityId && !candidate.IsDeleted, cancellationToken);
        if (item is null) return null;
        if (context.TenantId != Guid.Empty && item.TenantId != context.TenantId) return null;
        var projectIds = _managementContext.Current?.Scope.ProjectIds;
        if (projectIds is not null && (!item.ProjectId.HasValue || !projectIds.Contains(item.ProjectId.Value))) return null;
        return item;
    }

    private bool HasWorkItemPermission()
    {
        var principal = _httpContextAccessor.HttpContext?.User;
        if (principal is null || principal.Identity?.IsAuthenticated != true) return false;
        return principal.HasClaim("permission", "WorkItem.View")
            || principal.HasClaim("permission", "WorkItem.Edit")
            || principal.HasClaim("permission", "WorkItem.Delete")
            || principal.IsInRole("SystemAdministrator");
    }
}

public sealed class WorkItemWorkflowMetadataProvider : IWorkflowEntityMetadataProvider
{
    public string EntityType => WorkItemWorkflow.EntityType;

    public WorkflowEntityTypeMetadata GetMetadata() => new(
        WorkflowDomains.Task,
        WorkItemWorkflow.EntityType,
        "Work Items",
        "عناصر العمل",
        new[]
        {
            new WorkflowFactMetadata("HasAssignee", "Has assignee", "له مسند إليه", "boolean"),
            new WorkflowFactMetadata("Type", "Type", "النوع", "string"),
            new WorkflowFactMetadata("Priority", "Priority", "الأولوية", "string"),
            new WorkflowFactMetadata("HasDueDate", "Has due date", "له موعد استحقاق", "boolean"),
            new WorkflowFactMetadata("HasResolution", "Has resolution", "له حل", "boolean"),
            new WorkflowFactMetadata("IsProjectScoped", "Is project scoped", "مرتبط بمشروع", "boolean")
        });
}

public sealed class WorkItemWorkflowConsumerValidator : IWorkflowDefinitionConsumerValidator
{
    private readonly IWorkItemStatusReferenceLookup _statusLookup;

    public WorkItemWorkflowConsumerValidator(IWorkItemStatusReferenceLookup statusLookup)
    {
        _statusLookup = statusLookup;
    }

    public string EntityType => WorkItemWorkflow.EntityType;

    public async Task<Result> ValidateAsync(WorkflowDefinitionVersion version, CancellationToken cancellationToken = default)
    {
        var statuses = await _statusLookup.GetAsync(true, cancellationToken);
        var byCode = statuses.ToDictionary(status => status.Code, StringComparer.OrdinalIgnoreCase);
        var activeStates = version.States.Where(state => state.IsActive).ToList();
        var missing = activeStates.Where(state => !byCode.ContainsKey(state.Code)).Select(state => state.Code).ToList();
        if (missing.Count > 0)
            return Result.Failure(Error.Validation($"Work Item workflow contains unknown status code(s): {string.Join(", ", missing)}."));

        var inactive = activeStates.Where(state => !byCode[state.Code].IsActive).Select(state => state.Code).ToList();
        if (inactive.Count > 0)
            return Result.Failure(Error.Validation($"Work Item workflow contains inactive status code(s): {string.Join(", ", inactive)}."));

        var initial = activeStates.Where(state => state.IsInitial).ToList();
        if (initial.Count != 1 || !byCode[initial[0].Code].IsActive)
            return Result.Failure(Error.Validation("The Work Item workflow must have exactly one active initial status."));

        return Result.Success();
    }
}
