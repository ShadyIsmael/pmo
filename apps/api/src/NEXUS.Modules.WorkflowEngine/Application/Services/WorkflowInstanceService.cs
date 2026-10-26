using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.WorkflowEngine.Application.Contracts;
using NEXUS.Modules.WorkflowEngine.Domain;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.WorkflowEngine.Application.Services;

public sealed class WorkflowInstanceService : IWorkflowInstanceService
{
    private readonly DbContext _context;
    private readonly IWorkflowEntityAdapterRegistry _adapterRegistry;

    public WorkflowInstanceService(DbContext context, IWorkflowEntityAdapterRegistry adapterRegistry)
    {
        _context = context;
        _adapterRegistry = adapterRegistry;
    }

    public async Task<Result<WorkflowInstanceSummary>> ProvisionAsync(
        string entityType,
        Guid entityId,
        string workflowDefinitionCode,
        WorkflowExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var adapter = _adapterRegistry.Resolve(entityType);
        if (adapter is null)
            return Result<WorkflowInstanceSummary>.Failure(WorkflowErrors.UnknownEntityType(entityType));

        var authorizeResult = await adapter.AuthorizeAsync(context, entityId, cancellationToken);
        if (authorizeResult.IsFailure)
            return Result<WorkflowInstanceSummary>.Failure(authorizeResult.Error!);

        var definitions = _context.Set<WorkflowDefinition>();
        var versions = _context.Set<WorkflowDefinitionVersion>();
        var instances = _context.Set<WorkflowInstance>();

        var definition = await definitions
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.TenantId == context.TenantId && d.Code == workflowDefinitionCode
                && d.EntityType == entityType && d.IsActive && !d.IsDeleted, cancellationToken);

        if (definition is null)
            return Result<WorkflowInstanceSummary>.Failure(WorkflowErrors.WorkflowNotConfigured(entityType));

        if (definition.ActivePublishedVersionId is null)
            return Result<WorkflowInstanceSummary>.Failure(WorkflowErrors.NoActivePublishedVersion);

        var version = await versions
            .Include(v => v.States)
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == definition.ActivePublishedVersionId.Value
                && v.TenantId == context.TenantId && v.WorkflowDefinitionId == definition.Id, cancellationToken);

        if (version is null)
            return Result<WorkflowInstanceSummary>.Failure(WorkflowErrors.NoActivePublishedVersion);

        if (version.LifecycleStatus == WorkflowDefinitionVersionLifecycleStatus.Draft)
            return Result<WorkflowInstanceSummary>.Failure(WorkflowErrors.DraftCannotExecute);

        if (version.LifecycleStatus == WorkflowDefinitionVersionLifecycleStatus.Retired)
            return Result<WorkflowInstanceSummary>.Failure(WorkflowErrors.RetiredCannotStart);

        var existingInstance = await instances
            .FirstOrDefaultAsync(i => i.TenantId == context.TenantId && i.EntityType == entityType
                && i.EntityId == entityId && i.IsActive && !i.IsDeleted, cancellationToken);

        if (existingInstance is not null)
            return Result<WorkflowInstanceSummary>.Failure(WorkflowErrors.DuplicateInstance);

        var initialState = version.States.FirstOrDefault(s => s.IsInitial && s.IsActive);
        if (initialState is null)
            return Result<WorkflowInstanceSummary>.Failure(WorkflowErrors.NoInitialState);

        var instance = new WorkflowInstance
        {
            TenantId = context.TenantId,
            WorkflowDefinitionId = definition.Id,
            WorkflowDefinitionVersionId = version.Id,
            EntityType = entityType,
            EntityId = entityId,
            CurrentStateId = initialState.Id,
            StartedAt = DateTime.UtcNow,
            StartedBy = context.UserId,
            CreatedBy = context.UserId,
            IsActive = true
        };

        instances.Add(instance);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<WorkflowInstanceSummary>.Success(new WorkflowInstanceSummary(
            instance.Id,
            definition.Id,
            version.Id,
            definition.Code,
            version.VersionNumber,
            entityType,
            entityId,
            initialState.Code,
            initialState.NameEn,
            initialState.NameAr,
            initialState.IsFinal,
            instance.StartedAt,
            instance.StartedBy,
            instance.CompletedAt,
            instance.IsActive,
            instance.RowVersion));
    }

    public async Task<Result<WorkflowInstanceSummary>> ProvisionAsync(
        string entityType,
        Guid entityId,
        Guid workflowDefinitionId,
        Guid workflowVersionId,
        WorkflowExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var adapter = _adapterRegistry.Resolve(entityType);
        if (adapter is null)
            return Result<WorkflowInstanceSummary>.Failure(WorkflowErrors.UnknownEntityType(entityType));

        var authorizeResult = await adapter.AuthorizeAsync(context, entityId, cancellationToken);
        if (authorizeResult.IsFailure)
            return Result<WorkflowInstanceSummary>.Failure(authorizeResult.Error!);

        var definition = await _context.Set<WorkflowDefinition>()
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == workflowDefinitionId && item.TenantId == context.TenantId
                && item.IsActive && !item.IsDeleted, cancellationToken);
        var version = await _context.Set<WorkflowDefinitionVersion>()
            .Include(item => item.States)
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == workflowVersionId && item.TenantId == context.TenantId
                && item.WorkflowDefinitionId == workflowDefinitionId && !item.IsDeleted, cancellationToken);
        if (definition is null || version is null || definition.ActivePublishedVersionId != version.Id)
            return Result<WorkflowInstanceSummary>.Failure(WorkflowErrors.NoActivePublishedVersion);
        if (version.LifecycleStatus == WorkflowDefinitionVersionLifecycleStatus.Draft)
            return Result<WorkflowInstanceSummary>.Failure(WorkflowErrors.DraftCannotExecute);
        if (version.LifecycleStatus == WorkflowDefinitionVersionLifecycleStatus.Retired)
            return Result<WorkflowInstanceSummary>.Failure(WorkflowErrors.RetiredCannotStart);

        var instances = _context.Set<WorkflowInstance>();
        if (await instances.AnyAsync(item => item.TenantId == context.TenantId && item.EntityType == entityType
            && item.EntityId == entityId && item.IsActive && !item.IsDeleted, cancellationToken))
            return Result<WorkflowInstanceSummary>.Failure(WorkflowErrors.DuplicateInstance);
        var initialState = version.States.FirstOrDefault(item => item.IsInitial && item.IsActive && !item.IsDeleted);
        if (initialState is null)
            return Result<WorkflowInstanceSummary>.Failure(WorkflowErrors.NoInitialState);

        var instance = new WorkflowInstance
        {
            TenantId = context.TenantId,
            WorkflowDefinitionId = definition.Id,
            WorkflowDefinitionVersionId = version.Id,
            EntityType = entityType,
            EntityId = entityId,
            CurrentStateId = initialState.Id,
            StartedAt = DateTime.UtcNow,
            StartedBy = context.UserId,
            CreatedBy = context.UserId,
            IsActive = true
        };
        instances.Add(instance);
        await _context.SaveChangesAsync(cancellationToken);
        return Result<WorkflowInstanceSummary>.Success(new WorkflowInstanceSummary(instance.Id, definition.Id, version.Id, definition.Code, version.VersionNumber, entityType, entityId, initialState.Code, initialState.NameEn, initialState.NameAr, initialState.IsFinal, instance.StartedAt, instance.StartedBy, instance.CompletedAt, instance.IsActive, instance.RowVersion));
    }

    public async Task<Result<WorkflowInstanceSummary?>> GetInstanceAsync(
        string entityType,
        Guid entityId,
        WorkflowExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var adapter = _adapterRegistry.Resolve(entityType);
        if (adapter is null)
            return Result<WorkflowInstanceSummary?>.Failure(WorkflowErrors.UnknownEntityType(entityType));

        var authorizeResult = await adapter.AuthorizeAsync(context, entityId, cancellationToken);
        if (authorizeResult.IsFailure)
            return Result<WorkflowInstanceSummary?>.Failure(authorizeResult.Error!);

        var instance = await _context.Set<WorkflowInstance>()
            .Include(i => i.CurrentState)
            .Include(i => i.WorkflowDefinition)
            .Include(i => i.WorkflowDefinitionVersion)
            .AsNoTracking()
            .OrderByDescending(i => i.StartedAt)
            .FirstOrDefaultAsync(i => i.TenantId == context.TenantId && i.EntityType == entityType && i.EntityId == entityId && !i.IsDeleted, cancellationToken);

        if (instance is null)
            return Result<WorkflowInstanceSummary?>.Success(null);

        return Result<WorkflowInstanceSummary?>.Success(new WorkflowInstanceSummary(
            instance.Id,
            instance.WorkflowDefinitionId,
            instance.WorkflowDefinitionVersionId,
            instance.WorkflowDefinition.Code,
            instance.WorkflowDefinitionVersion.VersionNumber,
            entityType,
            entityId,
            instance.CurrentState.Code,
            instance.CurrentState.NameEn,
            instance.CurrentState.NameAr,
            instance.CurrentState.IsFinal,
            instance.StartedAt,
            instance.StartedBy,
            instance.CompletedAt,
            instance.IsActive,
            instance.RowVersion));
    }
}
