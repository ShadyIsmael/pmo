using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.CostManagement.Domain;
using NEXUS.Modules.WorkflowEngine.Application.Contracts;
using NEXUS.Modules.WorkflowEngine.Domain;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Management;

namespace NEXUS.Modules.CostManagement.Application;

public static class FinanceWorkflow
{
    public const string EntityType = "BudgetRevision";
    public const string DefinitionCode = "FINANCE_BUDGET_REVISION_STANDARD";
}

public sealed class FinanceWorkflowAdapter : IWorkflowEntityAdapter
{
    private readonly DbContext _context;
    private readonly IProjectReferenceLookup _projectLookup;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IManagementContextAccessor _managementContext;

    public FinanceWorkflowAdapter(DbContext context, IProjectReferenceLookup projectLookup, IHttpContextAccessor httpContextAccessor, IManagementContextAccessor managementContext)
    {
        _context = context;
        _projectLookup = projectLookup;
        _httpContextAccessor = httpContextAccessor;
        _managementContext = managementContext;
    }

    public string EntityType => FinanceWorkflow.EntityType;

    public async Task<Result<WorkflowEntitySnapshot>> GetSnapshotAsync(Guid entityId, WorkflowExecutionContext context, CancellationToken cancellationToken = default)
    {
        var revision = await FindAccessibleAsync(entityId, context, cancellationToken);
        return revision is null
            ? Result<WorkflowEntitySnapshot>.Failure(WorkflowErrors.EntityNotAccessible(EntityType, entityId))
            : Result<WorkflowEntitySnapshot>.Success(new WorkflowEntitySnapshot(EntityType, entityId, new Dictionary<string, object?>
            {
                ["StateCode"] = revision.Status.ToString(),
                ["StateNameEn"] = revision.Status.ToString(),
                ["StateNameAr"] = revision.Status.ToString(),
                ["ProjectId"] = revision.ProjectId,
                ["HasJustification"] = !string.IsNullOrWhiteSpace(revision.Justification)
            }));
    }

    public async Task<Result> AuthorizeAsync(WorkflowExecutionContext context, Guid entityId, CancellationToken cancellationToken = default)
    {
        var principal = _httpContextAccessor.HttpContext?.User;
        if (principal?.Identity?.IsAuthenticated != true || !principal.Claims.Any(claim => claim.Type == "permission" && (claim.Value is "Finances.Read" or "Finances.Write")) && !principal.IsInRole("SystemAdministrator"))
            return Result.Failure(Error.Forbidden("Finance access is required."));
        return await FindAccessibleAsync(entityId, context, cancellationToken) is null
            ? Result.Failure(WorkflowErrors.EntityNotAccessible(EntityType, entityId))
            : Result.Success();
    }

    public Task<Result> ValidateTransitionAsync(WorkflowTransitionContext context, CancellationToken cancellationToken = default)
    {
        return Enum.TryParse<BudgetRevision.Statuses>(context.ToState.Code, true, out _)
            ? Task.FromResult(Result.Success())
            : Task.FromResult(Result.Failure(Error.Validation($"Finance status '{context.ToState.Code}' is not supported by Budget Revision.")));
    }

    public async Task<Result> ApplyTransitionAsync(WorkflowTransitionContext context, CancellationToken cancellationToken = default)
    {
        var revision = await FindAccessibleAsync(context.EntityId, context.Execution, cancellationToken, tracked: true);
        if (revision is null) return Result.Failure(WorkflowErrors.EntityNotAccessible(EntityType, context.EntityId));
        if (!Enum.TryParse<BudgetRevision.Statuses>(context.ToState.Code, true, out var status)) return Result.Failure(Error.Validation("Finance transition target is invalid."));
        if (!string.Equals(revision.Status.ToString(), context.FromState.Code, StringComparison.OrdinalIgnoreCase))
            return Result.Failure(WorkflowErrors.WorkflowDomainStateMismatch(context.FromState.Code, revision.Status.ToString()));
        revision.Status = status;
        if (status == BudgetRevision.Statuses.Approved)
        {
            var approverId = ParseUserId(context.Execution.UserId);
            if (approverId != Guid.Empty && approverId == revision.RequestedBy)
                return Result.Failure(Error.Validation("The requester cannot approve their own budget revision."));
            if (revision.Budget is null)
                return Result.Failure(Error.NotFound("Associated budget not found."));
            if (revision.PreviousApprovedAmount != revision.Budget.ApprovedAmount)
                return Result.Failure(Error.Conflict("The budget allocation has changed. Cancel and resubmit the revision."));
            var allocationError = await BudgetAuthority.ValidateAllocationAsync(_context, _projectLookup, revision.Budget.ProjectId, revision.BudgetId, revision.ProposedApprovedAmount, cancellationToken);
            if (allocationError is not null)
                return Result.Failure(allocationError);
            revision.Budget.ApprovedAmount = revision.ProposedApprovedAmount;
            revision.Budget.UpdatedBy = context.Execution.UserId;
            revision.Budget.UpdatedAt = DateTime.UtcNow;
        }
        revision.ReviewedBy = status is BudgetRevision.Statuses.Approved or BudgetRevision.Statuses.Rejected ? ParseUserId(context.Execution.UserId) : revision.ReviewedBy;
        revision.ReviewedAt = status is BudgetRevision.Statuses.Approved or BudgetRevision.Statuses.Rejected ? DateTime.UtcNow : revision.ReviewedAt;
        revision.UpdatedBy = context.Execution.UserId;
        return Result.Success();
    }

    private async Task<BudgetRevision?> FindAccessibleAsync(Guid id, WorkflowExecutionContext context, CancellationToken cancellationToken, bool tracked = false)
    {
        var query = _context.Set<BudgetRevision>().Include(item => item.Budget).AsQueryable();
        if (!tracked) query = query.AsNoTracking();
        var revision = await query.FirstOrDefaultAsync(item => item.Id == id && !item.IsDeleted, cancellationToken);
        if (revision is null || (context.TenantId != Guid.Empty && revision.TenantId != context.TenantId)) return null;
        var projectIds = _managementContext.Current?.Scope.ProjectIds;
        return projectIds is not null && projectIds.Count > 0 && !projectIds.Contains(revision.ProjectId) ? null : revision;
    }

    private static Guid ParseUserId(string value) => Guid.TryParse(value, out var id) ? id : Guid.Empty;
}

public sealed class FinanceWorkflowMetadataProvider : IWorkflowEntityMetadataProvider
{
    public string EntityType => FinanceWorkflow.EntityType;
    public WorkflowEntityTypeMetadata GetMetadata() => new(WorkflowDomains.Finance, EntityType, "Budget Revisions", "مراجعات الميزانية", new[]
    {
        new WorkflowFactMetadata("HasJustification", "Has justification", "له تبرير", "boolean"),
        new WorkflowFactMetadata("ProjectId", "Project", "المشروع", "string")
    });
}

public sealed class FinanceWorkflowConsumerValidator : IWorkflowDefinitionConsumerValidator
{
    public string EntityType => FinanceWorkflow.EntityType;

    public Task<Result> ValidateAsync(WorkflowDefinitionVersion version, CancellationToken cancellationToken = default)
    {
        var supported = Enum.GetNames<BudgetRevision.Statuses>().ToHashSet(StringComparer.OrdinalIgnoreCase);
        var unknown = version.States.Where(item => item.IsActive && !supported.Contains(item.Code)).Select(item => item.Code).ToList();
        if (unknown.Count > 0) return Task.FromResult(Result.Failure(Error.Validation($"Finance workflow contains unsupported Budget Revision status code(s): {string.Join(", ", unknown)}.")));
        var initial = version.States.Count(item => item.IsActive && item.IsInitial);
        return Task.FromResult(initial == 1 ? Result.Success() : Result.Failure(Error.Validation("Finance workflow must contain exactly one active initial state.")));
    }
}
