using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.RiskManagement.Domain;
using NEXUS.Modules.WorkflowEngine.Application.Contracts;
using NEXUS.Modules.WorkflowEngine.Application.Services;
using NEXUS.Modules.WorkflowEngine.Domain;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Management;

namespace NEXUS.Modules.RiskManagement.Application;

public static class RiskWorkflow
{
    public const string EntityType = "Risk";
    public const string DefinitionCode = "RISK_STANDARD";
}

public sealed class RiskWorkflowAdapter : IWorkflowEntityAdapter
{
    private readonly DbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IManagementContextAccessor _managementContext;

    public RiskWorkflowAdapter(DbContext context, IHttpContextAccessor httpContextAccessor, IManagementContextAccessor managementContext)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
        _managementContext = managementContext;
    }

    public string EntityType => RiskWorkflow.EntityType;

    public async Task<Result<WorkflowEntitySnapshot>> GetSnapshotAsync(Guid entityId, WorkflowExecutionContext context, CancellationToken cancellationToken = default)
    {
        var risk = await FindAccessibleAsync(entityId, context, cancellationToken);
        return risk is null
            ? Result<WorkflowEntitySnapshot>.Failure(WorkflowErrors.EntityNotAccessible(EntityType, entityId))
            : Result<WorkflowEntitySnapshot>.Success(new WorkflowEntitySnapshot(EntityType, entityId, new Dictionary<string, object?>
            {
                ["StateCode"] = risk.Status.ToString(),
                ["StateNameEn"] = risk.Status.ToString(),
                ["StateNameAr"] = risk.Status.ToString(),
                ["HasOwner"] = risk.OwnerResourceId.HasValue,
                ["RiskLevel"] = risk.RiskLevel.ToString(),
                ["ProjectId"] = risk.ProjectId
            }));
    }

    public async Task<Result> AuthorizeAsync(WorkflowExecutionContext context, Guid entityId, CancellationToken cancellationToken = default)
    {
        var principal = _httpContextAccessor.HttpContext?.User;
        if (principal?.Identity?.IsAuthenticated != true || !principal.Claims.Any(claim => claim.Type == "permission" && (claim.Value is "Risk.View" or "Risk.Edit" or "Risk.Mitigate")) && !principal.IsInRole("SystemAdministrator"))
            return Result.Failure(Error.Forbidden("Risk access is required."));
        return await FindAccessibleAsync(entityId, context, cancellationToken) is null
            ? Result.Failure(WorkflowErrors.EntityNotAccessible(EntityType, entityId))
            : Result.Success();
    }

    public Task<Result> ValidateTransitionAsync(WorkflowTransitionContext context, CancellationToken cancellationToken = default)
    {
        return Enum.TryParse<RiskStatus>(context.ToState.Code, true, out _)
            ? Task.FromResult(Result.Success())
            : Task.FromResult(Result.Failure(Error.Validation($"Risk status '{context.ToState.Code}' is not supported by NEXUS.")));
    }

    public async Task<Result> ApplyTransitionAsync(WorkflowTransitionContext context, CancellationToken cancellationToken = default)
    {
        var risk = await FindAccessibleAsync(context.EntityId, context.Execution, cancellationToken, tracked: true);
        if (risk is null) return Result.Failure(WorkflowErrors.EntityNotAccessible(EntityType, context.EntityId));
        if (!Enum.TryParse<RiskStatus>(context.ToState.Code, true, out var status)) return Result.Failure(Error.Validation("Risk transition target is invalid."));
        if (!string.Equals(risk.Status.ToString(), context.FromState.Code, StringComparison.OrdinalIgnoreCase))
            return Result.Failure(WorkflowErrors.WorkflowDomainStateMismatch(context.FromState.Code, risk.Status.ToString()));
        risk.Status = status;
        risk.ClosedDate = status == RiskStatus.Closed ? risk.ClosedDate ?? DateTime.UtcNow : null;
        risk.UpdatedBy = context.Execution.UserId;
        return Result.Success();
    }

    private async Task<Risk?> FindAccessibleAsync(Guid id, WorkflowExecutionContext context, CancellationToken cancellationToken, bool tracked = false)
    {
        var query = _context.Set<Risk>().AsQueryable();
        if (!tracked) query = query.AsNoTracking();
        var risk = await query.FirstOrDefaultAsync(item => item.Id == id && !item.IsDeleted, cancellationToken);
        if (risk is null || (context.TenantId != Guid.Empty && risk.TenantId != context.TenantId)) return null;
        var projectIds = _managementContext.Current?.Scope.ProjectIds;
        return projectIds is not null && projectIds.Count > 0 && !projectIds.Contains(risk.ProjectId) ? null : risk;
    }
}

public sealed class RiskWorkflowMetadataProvider : IWorkflowEntityMetadataProvider
{
    public string EntityType => RiskWorkflow.EntityType;
    public WorkflowEntityTypeMetadata GetMetadata() => new(WorkflowDomains.Risk, EntityType, "Risks", "المخاطر", new[]
    {
        new WorkflowFactMetadata("HasOwner", "Has owner", "له مالك", "boolean"),
        new WorkflowFactMetadata("RiskLevel", "Risk level", "مستوى المخاطر", "string"),
        new WorkflowFactMetadata("ProjectId", "Project", "المشروع", "string")
    });
}

public sealed class RiskWorkflowConsumerValidator : IWorkflowDefinitionConsumerValidator
{
    public string EntityType => RiskWorkflow.EntityType;

    public Task<Result> ValidateAsync(WorkflowDefinitionVersion version, CancellationToken cancellationToken = default)
    {
        var supported = Enum.GetNames<RiskStatus>().ToHashSet(StringComparer.OrdinalIgnoreCase);
        var unknown = version.States.Where(item => item.IsActive && !supported.Contains(item.Code)).Select(item => item.Code).ToList();
        if (unknown.Count > 0) return Task.FromResult(Result.Failure(Error.Validation($"Risk workflow contains unsupported status code(s): {string.Join(", ", unknown)}.")));
        var initial = version.States.Count(item => item.IsActive && item.IsInitial);
        return Task.FromResult(initial == 1 ? Result.Success() : Result.Failure(Error.Validation("Risk workflow must contain exactly one active initial state.")));
    }
}
