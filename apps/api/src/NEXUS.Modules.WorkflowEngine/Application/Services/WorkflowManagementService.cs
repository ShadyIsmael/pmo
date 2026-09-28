using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.WorkflowEngine.Application.Contracts;
using NEXUS.Modules.WorkflowEngine.Domain;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.WorkflowEngine.Application.Services;

public interface IWorkflowManagementService
{
    Task<Result<IReadOnlyList<WorkflowDefinitionListItemDto>>> ListDefinitionsAsync(string? search, bool? isActive, string? entityType, CancellationToken cancellationToken = default);
    Task<Result<WorkflowDefinitionDetailsDto>> GetDefinitionAsync(Guid definitionId, CancellationToken cancellationToken = default);
    Task<Result<WorkflowVersionDetailsDto>> GetVersionAsync(Guid versionId, CancellationToken cancellationToken = default);
    Task<Result<WorkflowManagementMetadataDto>> GetMetadataAsync(CancellationToken cancellationToken = default);
    Task<Result<WorkflowDefinitionDetailsDto>> CreateDefinitionAsync(CreateWorkflowDefinitionRequest request, string actor, CancellationToken cancellationToken = default);
    Task<Result<WorkflowDefinitionDetailsDto>> UpdateDefinitionAsync(Guid definitionId, UpdateWorkflowDefinitionRequest request, string actor, CancellationToken cancellationToken = default);
    Task<Result<WorkflowVersionDetailsDto>> CreateDraftAsync(Guid definitionId, CreateDraftRequest request, string actor, CancellationToken cancellationToken = default);
    Task<Result> DeleteDraftAsync(Guid versionId, byte[]? rowVersion, string actor, CancellationToken cancellationToken = default);
    Task<Result<WorkflowStateDto>> CreateStateAsync(Guid versionId, SaveWorkflowStateRequest request, string actor, CancellationToken cancellationToken = default);
    Task<Result<WorkflowStateDto>> UpdateStateAsync(Guid stateId, SaveWorkflowStateRequest request, string actor, CancellationToken cancellationToken = default);
    Task<Result> DeleteStateAsync(Guid stateId, byte[]? rowVersion, string actor, CancellationToken cancellationToken = default);
    Task<Result<WorkflowTransitionDto>> CreateTransitionAsync(Guid versionId, SaveWorkflowTransitionRequest request, string actor, CancellationToken cancellationToken = default);
    Task<Result<WorkflowTransitionDto>> UpdateTransitionAsync(Guid transitionId, SaveWorkflowTransitionRequest request, string actor, CancellationToken cancellationToken = default);
    Task<Result> DeleteTransitionAsync(Guid transitionId, byte[]? rowVersion, string actor, CancellationToken cancellationToken = default);
    Task<Result<WorkflowGuardDto>> CreateGuardAsync(Guid transitionId, SaveWorkflowGuardRequest request, string actor, CancellationToken cancellationToken = default);
    Task<Result<WorkflowGuardDto>> UpdateGuardAsync(Guid guardId, SaveWorkflowGuardRequest request, string actor, CancellationToken cancellationToken = default);
    Task<Result> DeleteGuardAsync(Guid guardId, byte[]? rowVersion, string actor, CancellationToken cancellationToken = default);
    Task<Result<WorkflowActionDto>> CreateActionAsync(Guid transitionId, SaveWorkflowActionRequest request, string actor, CancellationToken cancellationToken = default);
    Task<Result<WorkflowActionDto>> UpdateActionAsync(Guid actionId, SaveWorkflowActionRequest request, string actor, CancellationToken cancellationToken = default);
    Task<Result> DeleteActionAsync(Guid actionId, byte[]? rowVersion, string actor, CancellationToken cancellationToken = default);
    Task<Result<WorkflowValidationReport>> ValidateAsync(Guid versionId, CancellationToken cancellationToken = default);
    Task<Result<WorkflowVersionDetailsDto>> PublishAsync(Guid versionId, PublishWorkflowRequest request, string actor, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<WorkflowPreviewStateDto>>> PreviewAsync(Guid versionId, CancellationToken cancellationToken = default);
}

public sealed class WorkflowManagementService : IWorkflowManagementService
{
    private readonly DbContext _context;
    private readonly WorkflowDefinitionValidator _validator;
    private readonly IWorkflowEntityAdapterRegistry _adapterRegistry;
    private readonly IEnumerable<IWorkflowEntityMetadataProvider> _metadataProviders;
    private readonly IEnumerable<IWorkflowDefinitionConsumerValidator> _consumerValidators;
    private readonly IEnumerable<IWorkflowGuardHandler> _guardHandlers;
    private readonly IEnumerable<IWorkflowActionHandler> _actionHandlers;
    private readonly IAuditService _auditService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public WorkflowManagementService(
        DbContext context,
        WorkflowDefinitionValidator validator,
        IWorkflowEntityAdapterRegistry adapterRegistry,
        IEnumerable<IWorkflowEntityMetadataProvider> metadataProviders,
        IEnumerable<IWorkflowDefinitionConsumerValidator> consumerValidators,
        IEnumerable<IWorkflowGuardHandler> guardHandlers,
        IEnumerable<IWorkflowActionHandler> actionHandlers,
        IAuditService auditService,
        IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _validator = validator;
        _adapterRegistry = adapterRegistry;
        _metadataProviders = metadataProviders;
        _consumerValidators = consumerValidators;
        _guardHandlers = guardHandlers;
        _actionHandlers = actionHandlers;
        _auditService = auditService;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<Result<IReadOnlyList<WorkflowDefinitionListItemDto>>> ListDefinitionsAsync(string? search, bool? isActive, string? entityType, CancellationToken cancellationToken = default)
    {
        var query = _context.Set<WorkflowDefinition>().Include(item => item.Versions).AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(item => item.Name.Contains(term) || item.Code.Contains(term));
        }
        if (isActive.HasValue) query = query.Where(item => item.IsActive == isActive.Value);
        if (!string.IsNullOrWhiteSpace(entityType)) query = query.Where(item => item.EntityType == entityType);

        var definitions = await query.OrderBy(item => item.Name).ToListAsync(cancellationToken);
        var items = definitions.Select(definition =>
        {
            var published = definition.Versions.FirstOrDefault(version => version.Id == definition.ActivePublishedVersionId);
            return new WorkflowDefinitionListItemDto(
                definition.Id,
                definition.Name,
                definition.Code,
                definition.EntityType,
                definition.IsActive,
                definition.ActivePublishedVersionId,
                published?.VersionNumber,
                published?.LifecycleStatus.ToString(),
                definition.Versions.Count,
                definition.UpdatedAt,
                definition.RowVersion);
        }).ToList();
        return Result<IReadOnlyList<WorkflowDefinitionListItemDto>>.Success(items);
    }

    public async Task<Result<WorkflowDefinitionDetailsDto>> GetDefinitionAsync(Guid definitionId, CancellationToken cancellationToken = default)
    {
        var definition = await _context.Set<WorkflowDefinition>()
            .Include(item => item.Versions)
                .ThenInclude(version => version.States)
            .Include(item => item.Versions)
                .ThenInclude(version => version.Transitions)
                    .ThenInclude(transition => transition.Guards)
            .Include(item => item.Versions)
                .ThenInclude(version => version.Transitions)
                    .ThenInclude(transition => transition.Actions)
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == definitionId, cancellationToken);
        return definition is null
            ? Result<WorkflowDefinitionDetailsDto>.Failure(Error.NotFound("Workflow definition not found."))
            : Result<WorkflowDefinitionDetailsDto>.Success(ToDefinitionDto(definition));
    }

    public async Task<Result<WorkflowVersionDetailsDto>> GetVersionAsync(Guid versionId, CancellationToken cancellationToken = default)
    {
        var version = await LoadVersionAsync(versionId, cancellationToken);
        return version is null
            ? Result<WorkflowVersionDetailsDto>.Failure(Error.NotFound("Workflow version not found."))
            : Result<WorkflowVersionDetailsDto>.Success(ToVersionDto(version));
    }

    public Task<Result<WorkflowManagementMetadataDto>> GetMetadataAsync(CancellationToken cancellationToken = default)
    {
        var entityTypes = _metadataProviders
            .Where(provider => _adapterRegistry.IsSupported(provider.EntityType))
            .Select(provider => provider.GetMetadata())
            .OrderBy(item => item.EntityType)
            .ToList();
        var guardTypes = _guardHandlers.Select(handler => handler.GuardType).OrderBy(item => item).ToList();
        var actionTypes = _actionHandlers.Select(handler => handler.ActionType).OrderBy(item => item).ToList();
        return Task.FromResult(Result<WorkflowManagementMetadataDto>.Success(new WorkflowManagementMetadataDto(entityTypes, guardTypes, actionTypes)));
    }

    public async Task<Result<WorkflowDefinitionDetailsDto>> CreateDefinitionAsync(CreateWorkflowDefinitionRequest request, string actor, CancellationToken cancellationToken = default)
    {
        var writable = EnsureWritable();
        if (writable.IsFailure) return Result<WorkflowDefinitionDetailsDto>.Failure(writable.Error!);
        var name = request.Name.Trim();
        var code = request.Code.Trim();
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(request.EntityType))
            return Result<WorkflowDefinitionDetailsDto>.Failure(Error.Validation("Name, code, and entity type are required."));
        var entityType = request.EntityType.Trim();
        if (!_adapterRegistry.IsSupported(entityType))
            return Result<WorkflowDefinitionDetailsDto>.Failure(WorkflowErrors.UnknownEntityType(request.EntityType));
        if (await _context.Set<WorkflowDefinition>().AnyAsync(item => item.Code == code, cancellationToken))
            return Result<WorkflowDefinitionDetailsDto>.Failure(Error.Conflict($"Workflow code '{code}' already exists."));
        if (await _context.Set<WorkflowDefinition>().AnyAsync(item => item.EntityType == entityType && item.IsActive && !item.IsDeleted, cancellationToken))
            return Result<WorkflowDefinitionDetailsDto>.Failure(Error.Conflict($"An active workflow definition already exists for entity type '{entityType}'."));

        var definition = new WorkflowDefinition
        {
            TenantId = CurrentTenantId(),
            Name = name,
            Code = code,
            Description = Normalize(request.Description),
            EntityType = entityType,
            IsActive = true,
            CreatedBy = actor
        };
        var version = NewVersion(definition, 1, request.Description, actor);
        definition.Versions.Add(version);
        _context.Set<WorkflowDefinition>().Add(definition);
        AddAudit(definition.Id, "Created", actor, new { definition.Name, definition.Code, definition.EntityType });
        await _context.SaveChangesAsync(cancellationToken);
        return Result<WorkflowDefinitionDetailsDto>.Success(ToDefinitionDto(definition));
    }

    public async Task<Result<WorkflowDefinitionDetailsDto>> UpdateDefinitionAsync(Guid definitionId, UpdateWorkflowDefinitionRequest request, string actor, CancellationToken cancellationToken = default)
    {
        var writable = EnsureWritable();
        if (writable.IsFailure) return Result<WorkflowDefinitionDetailsDto>.Failure(writable.Error!);
        var definition = await _context.Set<WorkflowDefinition>().FirstOrDefaultAsync(item => item.Id == definitionId, cancellationToken);
        if (definition is null) return Result<WorkflowDefinitionDetailsDto>.Failure(Error.NotFound("Workflow definition not found."));
        if (request.IsActive && !definition.IsActive && await _context.Set<WorkflowDefinition>().AnyAsync(item => item.Id != definitionId && item.EntityType == definition.EntityType && item.IsActive && !item.IsDeleted, cancellationToken))
            return Result<WorkflowDefinitionDetailsDto>.Failure(Error.Conflict($"An active workflow definition already exists for entity type '{definition.EntityType}'."));
        ApplyRowVersion(definition, request.RowVersion);
        definition.Name = request.Name.Trim();
        definition.Description = Normalize(request.Description);
        definition.IsActive = request.IsActive;
        definition.UpdatedBy = actor;
        AddAudit(definition.Id, request.IsActive ? "Updated" : "Deactivated", actor, new { definition.Name, definition.IsActive });
        try { await _context.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { return Result<WorkflowDefinitionDetailsDto>.Failure(Error.Conflict("Workflow definition changed. Refresh and retry.")); }
        return await GetDefinitionAsync(definitionId, cancellationToken);
    }

    public async Task<Result<WorkflowVersionDetailsDto>> CreateDraftAsync(Guid definitionId, CreateDraftRequest request, string actor, CancellationToken cancellationToken = default)
    {
        var writable = EnsureWritable();
        if (writable.IsFailure) return Result<WorkflowVersionDetailsDto>.Failure(writable.Error!);
        var definition = await _context.Set<WorkflowDefinition>().FirstOrDefaultAsync(item => item.Id == definitionId, cancellationToken);
        if (definition is null) return Result<WorkflowVersionDetailsDto>.Failure(Error.NotFound("Workflow definition not found."));
        var existingDraft = await _context.Set<WorkflowDefinitionVersion>().FirstOrDefaultAsync(item => item.WorkflowDefinitionId == definitionId && item.LifecycleStatus == WorkflowDefinitionVersionLifecycleStatus.Draft, cancellationToken);
        if (existingDraft is not null) return Result<WorkflowVersionDetailsDto>.Failure(WorkflowErrors.DraftAlreadyExists(existingDraft.VersionNumber));

        var source = definition.ActivePublishedVersionId.HasValue
            ? await LoadVersionAsync(definition.ActivePublishedVersionId.Value, cancellationToken)
            : null;
        var nextNumber = (await _context.Set<WorkflowDefinitionVersion>().Where(item => item.WorkflowDefinitionId == definitionId).Select(item => (int?)item.VersionNumber).MaxAsync(cancellationToken) ?? 0) + 1;
        var draft = NewVersion(definition, nextNumber, request.ChangeLog ?? "Draft created", actor);
        if (source is not null)
        {
            var stateMap = new Dictionary<Guid, WorkflowState>();
            foreach (var sourceState in source.States.OrderBy(item => item.DisplayOrder))
            {
                var state = new WorkflowState
                {
                    TenantId = definition.TenantId,
                    WorkflowDefinitionVersionId = draft.Id,
                    Code = sourceState.Code,
                    NameEn = sourceState.NameEn,
                    NameAr = sourceState.NameAr,
                    Description = sourceState.Description,
                    DisplayOrder = sourceState.DisplayOrder,
                    IsInitial = sourceState.IsInitial,
                    IsFinal = sourceState.IsFinal,
                    IsActive = sourceState.IsActive,
                    CreatedBy = actor
                };
                stateMap[sourceState.Id] = state;
                draft.States.Add(state);
            }
            foreach (var sourceTransition in source.Transitions.OrderBy(item => item.DisplayOrder))
            {
                if (!stateMap.TryGetValue(sourceTransition.FromStateId, out var from) || !stateMap.TryGetValue(sourceTransition.ToStateId, out var to)) continue;
                var transition = new WorkflowTransition
                {
                    TenantId = definition.TenantId,
                    WorkflowDefinitionVersionId = draft.Id,
                    Code = sourceTransition.Code,
                    NameEn = sourceTransition.NameEn,
                    NameAr = sourceTransition.NameAr,
                    FromStateId = from.Id,
                    ToStateId = to.Id,
                    DisplayOrder = sourceTransition.DisplayOrder,
                    IsActive = sourceTransition.IsActive,
                    CreatedBy = actor
                };
                foreach (var guard in sourceTransition.Guards)
                    transition.Guards.Add(new WorkflowTransitionGuard { TenantId = definition.TenantId, WorkflowTransitionId = transition.Id, GuardType = guard.GuardType, ConfigurationJson = guard.ConfigurationJson, ExecutionOrder = guard.ExecutionOrder, IsActive = guard.IsActive, CreatedBy = actor });
                foreach (var action in sourceTransition.Actions)
                    transition.Actions.Add(new WorkflowTransitionAction { TenantId = definition.TenantId, WorkflowTransitionId = transition.Id, ActionType = action.ActionType, ConfigurationJson = action.ConfigurationJson, ExecutionOrder = action.ExecutionOrder, IsActive = action.IsActive, CreatedBy = actor });
                draft.Transitions.Add(transition);
            }
        }
        definition.Versions.Add(draft);
        AddAudit(definition.Id, "DraftCreated", actor, new { version = draft.VersionNumber, clonedFrom = source?.VersionNumber });
        await _context.SaveChangesAsync(cancellationToken);
        var loaded = await LoadVersionAsync(draft.Id, cancellationToken);
        return Result<WorkflowVersionDetailsDto>.Success(ToVersionDto(loaded!));
    }

    public async Task<Result> DeleteDraftAsync(Guid versionId, byte[]? rowVersion, string actor, CancellationToken cancellationToken = default)
    {
        var writable = EnsureWritable();
        if (writable.IsFailure) return writable;
        var version = await _context.Set<WorkflowDefinitionVersion>().FirstOrDefaultAsync(item => item.Id == versionId, cancellationToken);
        if (version is null) return Result.Failure(Error.NotFound("Workflow version not found."));
        if (version.LifecycleStatus != WorkflowDefinitionVersionLifecycleStatus.Draft) return Result.Failure(Error.Conflict("Only Draft workflow versions can be deleted."));
        if (await _context.Set<WorkflowInstance>().AnyAsync(item => item.WorkflowDefinitionVersionId == versionId, cancellationToken)) return Result.Failure(Error.Conflict("This draft is already referenced by workflow instances."));
        ApplyRowVersion(version, rowVersion);
        version.IsDeleted = true;
        version.UpdatedBy = actor;
        AddAudit(version.Id, "DraftDeleted", actor, new { version = version.VersionNumber });
        try { await _context.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { return Result.Failure(Error.Conflict("Workflow version changed. Refresh and retry.")); }
        return Result.Success();
    }

    public async Task<Result<WorkflowStateDto>> CreateStateAsync(Guid versionId, SaveWorkflowStateRequest request, string actor, CancellationToken cancellationToken = default)
    {
        var versionResult = await DraftVersionAsync(versionId, cancellationToken);
        if (versionResult.IsFailure) return Result<WorkflowStateDto>.Failure(versionResult.Error!);
        var version = versionResult.Value!;
        var validation = ValidateStateRequest(request.Code, request.NameEn, request.NameAr);
        if (validation is not null) return Result<WorkflowStateDto>.Failure(validation);
        if (version.States.Any(item => item.Code.Equals(request.Code.Trim(), StringComparison.OrdinalIgnoreCase))) return Result<WorkflowStateDto>.Failure(Error.Conflict("State code must be unique within the draft."));
        if (request.IsInitial) foreach (var state in version.States) state.IsInitial = false;
        var stateEntity = new WorkflowState { TenantId = version.TenantId, WorkflowDefinitionVersionId = version.Id, Code = request.Code.Trim(), NameEn = request.NameEn.Trim(), NameAr = request.NameAr.Trim(), Description = Normalize(request.Description), DisplayOrder = request.DisplayOrder, IsInitial = request.IsInitial, IsFinal = request.IsFinal, IsActive = request.IsActive, CreatedBy = actor };
        version.States.Add(stateEntity);
        AddAudit(version.Id, "StateAdded", actor, new { stateEntity.Code });
        await _context.SaveChangesAsync(cancellationToken);
        return Result<WorkflowStateDto>.Success(ToStateDto(stateEntity));
    }

    public async Task<Result<WorkflowStateDto>> UpdateStateAsync(Guid stateId, SaveWorkflowStateRequest request, string actor, CancellationToken cancellationToken = default)
    {
        var state = await _context.Set<WorkflowState>().FirstOrDefaultAsync(item => item.Id == stateId, cancellationToken);
        if (state is null) return Result<WorkflowStateDto>.Failure(Error.NotFound("Workflow state not found."));
        var draft = await DraftVersionAsync(state.WorkflowDefinitionVersionId, cancellationToken);
        if (draft.IsFailure) return Result<WorkflowStateDto>.Failure(draft.Error!);
        var validation = ValidateStateRequest(request.Code, request.NameEn, request.NameAr);
        if (validation is not null) return Result<WorkflowStateDto>.Failure(validation);
        if (await _context.Set<WorkflowState>().AnyAsync(item => item.WorkflowDefinitionVersionId == state.WorkflowDefinitionVersionId && item.Id != state.Id && item.Code == request.Code.Trim(), cancellationToken)) return Result<WorkflowStateDto>.Failure(Error.Conflict("State code must be unique within the draft."));
        ApplyRowVersion(state, request.RowVersion);
        if (request.IsInitial)
        {
            foreach (var sibling in draft.Value!.States.Where(item => item.Id != state.Id)) sibling.IsInitial = false;
        }
        state.Code = request.Code.Trim(); state.NameEn = request.NameEn.Trim(); state.NameAr = request.NameAr.Trim(); state.Description = Normalize(request.Description); state.DisplayOrder = request.DisplayOrder; state.IsInitial = request.IsInitial; state.IsFinal = request.IsFinal; state.IsActive = request.IsActive; state.UpdatedBy = actor;
        AddAudit(state.Id, "StateUpdated", actor, new { state.Code });
        try { await _context.SaveChangesAsync(cancellationToken); } catch (DbUpdateConcurrencyException) { return Result<WorkflowStateDto>.Failure(Error.Conflict("Workflow state changed. Refresh and retry.")); }
        return Result<WorkflowStateDto>.Success(ToStateDto(state));
    }

    public async Task<Result> DeleteStateAsync(Guid stateId, byte[]? rowVersion, string actor, CancellationToken cancellationToken = default)
    {
        var state = await _context.Set<WorkflowState>().FirstOrDefaultAsync(item => item.Id == stateId, cancellationToken);
        if (state is null) return Result.Failure(Error.NotFound("Workflow state not found."));
        var draft = await DraftVersionAsync(state.WorkflowDefinitionVersionId, cancellationToken);
        if (draft.IsFailure) return Result.Failure(draft.Error!);
        var references = await _context.Set<WorkflowTransition>().CountAsync(item => item.WorkflowDefinitionVersionId == state.WorkflowDefinitionVersionId && !item.IsDeleted && (item.FromStateId == stateId || item.ToStateId == stateId), cancellationToken);
        if (references > 0) return Result.Failure(Error.Conflict($"Cannot remove state. {references} transition(s) currently reference this state."));
        ApplyRowVersion(state, rowVersion); state.IsDeleted = true; state.UpdatedBy = actor;
        AddAudit(state.Id, "StateRemoved", actor, new { state.Code });
        try { await _context.SaveChangesAsync(cancellationToken); } catch (DbUpdateConcurrencyException) { return Result.Failure(Error.Conflict("Workflow state changed. Refresh and retry.")); }
        return Result.Success();
    }

    public async Task<Result<WorkflowTransitionDto>> CreateTransitionAsync(Guid versionId, SaveWorkflowTransitionRequest request, string actor, CancellationToken cancellationToken = default)
    {
        var versionResult = await DraftVersionAsync(versionId, cancellationToken);
        if (versionResult.IsFailure) return Result<WorkflowTransitionDto>.Failure(versionResult.Error!);
        var version = versionResult.Value!;
        var validation = await ValidateTransitionShapeAsync(version, request, null, cancellationToken);
        if (validation is not null) return Result<WorkflowTransitionDto>.Failure(validation);
        var transition = new WorkflowTransition { TenantId = version.TenantId, WorkflowDefinitionVersionId = version.Id, Code = request.Code.Trim(), NameEn = request.NameEn.Trim(), NameAr = request.NameAr.Trim(), FromStateId = request.FromStateId, ToStateId = request.ToStateId, DisplayOrder = request.DisplayOrder, IsActive = request.IsActive, CreatedBy = actor };
        version.Transitions.Add(transition);
        AddAudit(version.Id, "TransitionAdded", actor, new { transition.Code });
        await _context.SaveChangesAsync(cancellationToken);
        return Result<WorkflowTransitionDto>.Success(ToTransitionDto(transition, version.States));
    }

    public async Task<Result<WorkflowTransitionDto>> UpdateTransitionAsync(Guid transitionId, SaveWorkflowTransitionRequest request, string actor, CancellationToken cancellationToken = default)
    {
        var transition = await _context.Set<WorkflowTransition>().Include(item => item.FromState).Include(item => item.ToState).FirstOrDefaultAsync(item => item.Id == transitionId, cancellationToken);
        if (transition is null) return Result<WorkflowTransitionDto>.Failure(Error.NotFound("Workflow transition not found."));
        var versionResult = await DraftVersionAsync(transition.WorkflowDefinitionVersionId, cancellationToken);
        if (versionResult.IsFailure) return Result<WorkflowTransitionDto>.Failure(versionResult.Error!);
        var version = versionResult.Value!;
        var validation = await ValidateTransitionShapeAsync(version, request, transition.Id, cancellationToken);
        if (validation is not null) return Result<WorkflowTransitionDto>.Failure(validation);
        ApplyRowVersion(transition, request.RowVersion);
        transition.Code = request.Code.Trim(); transition.NameEn = request.NameEn.Trim(); transition.NameAr = request.NameAr.Trim(); transition.FromStateId = request.FromStateId; transition.ToStateId = request.ToStateId; transition.DisplayOrder = request.DisplayOrder; transition.IsActive = request.IsActive; transition.UpdatedBy = actor;
        AddAudit(transition.Id, "TransitionUpdated", actor, new { transition.Code });
        try { await _context.SaveChangesAsync(cancellationToken); } catch (DbUpdateConcurrencyException) { return Result<WorkflowTransitionDto>.Failure(Error.Conflict("Workflow transition changed. Refresh and retry.")); }
        await _context.Entry(transition).Reference(item => item.FromState).LoadAsync(cancellationToken); await _context.Entry(transition).Reference(item => item.ToState).LoadAsync(cancellationToken);
        return Result<WorkflowTransitionDto>.Success(ToTransitionDto(transition, version.States));
    }

    public async Task<Result> DeleteTransitionAsync(Guid transitionId, byte[]? rowVersion, string actor, CancellationToken cancellationToken = default)
    {
        var transition = await _context.Set<WorkflowTransition>().FirstOrDefaultAsync(item => item.Id == transitionId, cancellationToken);
        if (transition is null) return Result.Failure(Error.NotFound("Workflow transition not found."));
        var draft = await DraftVersionAsync(transition.WorkflowDefinitionVersionId, cancellationToken); if (draft.IsFailure) return Result.Failure(draft.Error!);
        ApplyRowVersion(transition, rowVersion); transition.IsDeleted = true; transition.UpdatedBy = actor;
        AddAudit(transition.Id, "TransitionRemoved", actor, new { transition.Code });
        try { await _context.SaveChangesAsync(cancellationToken); } catch (DbUpdateConcurrencyException) { return Result.Failure(Error.Conflict("Workflow transition changed. Refresh and retry.")); }
        return Result.Success();
    }

    public async Task<Result<WorkflowGuardDto>> CreateGuardAsync(Guid transitionId, SaveWorkflowGuardRequest request, string actor, CancellationToken cancellationToken = default)
    {
        var transition = await LoadTransitionAsync(transitionId, cancellationToken); if (transition is null) return Result<WorkflowGuardDto>.Failure(Error.NotFound("Workflow transition not found."));
        var draft = await DraftVersionAsync(transition.WorkflowDefinitionVersionId, cancellationToken); if (draft.IsFailure) return Result<WorkflowGuardDto>.Failure(draft.Error!);
        var config = await ValidateRuleAsync(request.GuardType, request.Configuration, true, cancellationToken); if (config.IsFailure) return Result<WorkflowGuardDto>.Failure(config.Error!);
        var guard = new WorkflowTransitionGuard { TenantId = transition.TenantId, WorkflowTransitionId = transition.Id, GuardType = request.GuardType.Trim(), ConfigurationJson = config.Value!, ExecutionOrder = request.ExecutionOrder, IsActive = request.IsActive, CreatedBy = actor };
        transition.Guards.Add(guard); AddAudit(guard.Id, "GuardAdded", actor, new { guard.GuardType, transitionId }); await _context.SaveChangesAsync(cancellationToken); return Result<WorkflowGuardDto>.Success(ToGuardDto(guard));
    }

    public async Task<Result<WorkflowGuardDto>> UpdateGuardAsync(Guid guardId, SaveWorkflowGuardRequest request, string actor, CancellationToken cancellationToken = default)
    {
        var guard = await _context.Set<WorkflowTransitionGuard>().FirstOrDefaultAsync(item => item.Id == guardId, cancellationToken); if (guard is null) return Result<WorkflowGuardDto>.Failure(Error.NotFound("Workflow guard not found."));
        var transition = await LoadTransitionAsync(guard.WorkflowTransitionId, cancellationToken); if (transition is null) return Result<WorkflowGuardDto>.Failure(Error.NotFound("Workflow transition not found."));
        var draft = await DraftVersionAsync(transition.WorkflowDefinitionVersionId, cancellationToken); if (draft.IsFailure) return Result<WorkflowGuardDto>.Failure(draft.Error!);
        var config = await ValidateRuleAsync(request.GuardType, request.Configuration, true, cancellationToken); if (config.IsFailure) return Result<WorkflowGuardDto>.Failure(config.Error!);
        ApplyRowVersion(guard, request.RowVersion); guard.GuardType = request.GuardType.Trim(); guard.ConfigurationJson = config.Value!; guard.ExecutionOrder = request.ExecutionOrder; guard.IsActive = request.IsActive; guard.UpdatedBy = actor; AddAudit(guard.Id, "GuardUpdated", actor, new { guard.GuardType });
        try { await _context.SaveChangesAsync(cancellationToken); } catch (DbUpdateConcurrencyException) { return Result<WorkflowGuardDto>.Failure(Error.Conflict("Workflow guard changed. Refresh and retry.")); }
        return Result<WorkflowGuardDto>.Success(ToGuardDto(guard));
    }

    public async Task<Result> DeleteGuardAsync(Guid guardId, byte[]? rowVersion, string actor, CancellationToken cancellationToken = default)
    {
        var guard = await _context.Set<WorkflowTransitionGuard>().FirstOrDefaultAsync(item => item.Id == guardId, cancellationToken); if (guard is null) return Result.Failure(Error.NotFound("Workflow guard not found."));
        var transition = await LoadTransitionAsync(guard.WorkflowTransitionId, cancellationToken); if (transition is null) return Result.Failure(Error.NotFound("Workflow transition not found."));
        var draft = await DraftVersionAsync(transition.WorkflowDefinitionVersionId, cancellationToken); if (draft.IsFailure) return Result.Failure(draft.Error!);
        ApplyRowVersion(guard, rowVersion); guard.IsDeleted = true; guard.UpdatedBy = actor; AddAudit(guard.Id, "GuardRemoved", actor, new { guard.GuardType });
        try { await _context.SaveChangesAsync(cancellationToken); } catch (DbUpdateConcurrencyException) { return Result.Failure(Error.Conflict("Workflow guard changed. Refresh and retry.")); } return Result.Success();
    }

    public async Task<Result<WorkflowActionDto>> CreateActionAsync(Guid transitionId, SaveWorkflowActionRequest request, string actor, CancellationToken cancellationToken = default)
    {
        var transition = await LoadTransitionAsync(transitionId, cancellationToken); if (transition is null) return Result<WorkflowActionDto>.Failure(Error.NotFound("Workflow transition not found."));
        var draft = await DraftVersionAsync(transition.WorkflowDefinitionVersionId, cancellationToken); if (draft.IsFailure) return Result<WorkflowActionDto>.Failure(draft.Error!);
        var config = await ValidateRuleAsync(request.ActionType, request.Configuration, false, cancellationToken); if (config.IsFailure) return Result<WorkflowActionDto>.Failure(config.Error!);
        var action = new WorkflowTransitionAction { TenantId = transition.TenantId, WorkflowTransitionId = transition.Id, ActionType = request.ActionType.Trim(), ConfigurationJson = config.Value!, ExecutionOrder = request.ExecutionOrder, IsActive = request.IsActive, CreatedBy = actor };
        transition.Actions.Add(action); AddAudit(action.Id, "ActionAdded", actor, new { action.ActionType, transitionId }); await _context.SaveChangesAsync(cancellationToken); return Result<WorkflowActionDto>.Success(ToActionDto(action));
    }

    public async Task<Result<WorkflowActionDto>> UpdateActionAsync(Guid actionId, SaveWorkflowActionRequest request, string actor, CancellationToken cancellationToken = default)
    {
        var action = await _context.Set<WorkflowTransitionAction>().FirstOrDefaultAsync(item => item.Id == actionId, cancellationToken); if (action is null) return Result<WorkflowActionDto>.Failure(Error.NotFound("Workflow action not found."));
        var transition = await LoadTransitionAsync(action.WorkflowTransitionId, cancellationToken); if (transition is null) return Result<WorkflowActionDto>.Failure(Error.NotFound("Workflow transition not found."));
        var draft = await DraftVersionAsync(transition.WorkflowDefinitionVersionId, cancellationToken); if (draft.IsFailure) return Result<WorkflowActionDto>.Failure(draft.Error!);
        var config = await ValidateRuleAsync(request.ActionType, request.Configuration, false, cancellationToken); if (config.IsFailure) return Result<WorkflowActionDto>.Failure(config.Error!);
        ApplyRowVersion(action, request.RowVersion); action.ActionType = request.ActionType.Trim(); action.ConfigurationJson = config.Value!; action.ExecutionOrder = request.ExecutionOrder; action.IsActive = request.IsActive; action.UpdatedBy = actor; AddAudit(action.Id, "ActionUpdated", actor, new { action.ActionType });
        try { await _context.SaveChangesAsync(cancellationToken); } catch (DbUpdateConcurrencyException) { return Result<WorkflowActionDto>.Failure(Error.Conflict("Workflow action changed. Refresh and retry.")); } return Result<WorkflowActionDto>.Success(ToActionDto(action));
    }

    public async Task<Result> DeleteActionAsync(Guid actionId, byte[]? rowVersion, string actor, CancellationToken cancellationToken = default)
    {
        var action = await _context.Set<WorkflowTransitionAction>().FirstOrDefaultAsync(item => item.Id == actionId, cancellationToken); if (action is null) return Result.Failure(Error.NotFound("Workflow action not found."));
        var transition = await LoadTransitionAsync(action.WorkflowTransitionId, cancellationToken); if (transition is null) return Result.Failure(Error.NotFound("Workflow transition not found."));
        var draft = await DraftVersionAsync(transition.WorkflowDefinitionVersionId, cancellationToken); if (draft.IsFailure) return Result.Failure(draft.Error!);
        ApplyRowVersion(action, rowVersion); action.IsDeleted = true; action.UpdatedBy = actor; AddAudit(action.Id, "ActionRemoved", actor, new { action.ActionType });
        try { await _context.SaveChangesAsync(cancellationToken); } catch (DbUpdateConcurrencyException) { return Result.Failure(Error.Conflict("Workflow action changed. Refresh and retry.")); } return Result.Success();
    }

    public async Task<Result<WorkflowValidationReport>> ValidateAsync(Guid versionId, CancellationToken cancellationToken = default)
    {
        var version = await LoadVersionAsync(versionId, cancellationToken); if (version is null) return Result<WorkflowValidationReport>.Failure(Error.NotFound("Workflow version not found."));
        var report = await ValidateVersionAsync(version, cancellationToken);
        return Result<WorkflowValidationReport>.Success(report);
    }

    public async Task<Result<WorkflowVersionDetailsDto>> PublishAsync(Guid versionId, PublishWorkflowRequest request, string actor, CancellationToken cancellationToken = default)
    {
        var writable = EnsurePublishable(); if (writable.IsFailure) return Result<WorkflowVersionDetailsDto>.Failure(writable.Error!);
        var version = await LoadVersionAsync(versionId, cancellationToken); if (version is null) return Result<WorkflowVersionDetailsDto>.Failure(Error.NotFound("Workflow version not found."));
        if (version.LifecycleStatus != WorkflowDefinitionVersionLifecycleStatus.Draft) return Result<WorkflowVersionDetailsDto>.Failure(Error.Conflict("Only Draft workflow versions can be published."));
        var validation = await ValidateVersionAsync(version, cancellationToken);
        if (!validation.IsValid) return Result<WorkflowVersionDetailsDto>.Failure(WorkflowErrors.DefinitionValidationFailed(string.Join(" ", validation.Errors)));
        ApplyRowVersion(version, request.RowVersion);
        var definition = await _context.Set<WorkflowDefinition>().FirstOrDefaultAsync(item => item.Id == version.WorkflowDefinitionId, cancellationToken);
        if (definition is null) return Result<WorkflowVersionDetailsDto>.Failure(Error.NotFound("Workflow definition not found."));
        if (!definition.IsActive) return Result<WorkflowVersionDetailsDto>.Failure(Error.Conflict("An inactive workflow definition cannot be published."));
        await using var transaction = _context.Database.ProviderName?.Contains("Relational", StringComparison.OrdinalIgnoreCase) == true ? await _context.Database.BeginTransactionAsync(cancellationToken) : null;
        try
        {
            var previousPublished = await _context.Set<WorkflowDefinitionVersion>().Where(item => item.WorkflowDefinitionId == definition.Id && item.LifecycleStatus == WorkflowDefinitionVersionLifecycleStatus.Published && item.Id != version.Id).ToListAsync(cancellationToken);
            foreach (var previous in previousPublished)
            {
                previous.LifecycleStatus = WorkflowDefinitionVersionLifecycleStatus.Retired;
                previous.UpdatedBy = actor;
                AddAudit(previous.Id, "Retired", actor, new { replacedBy = version.VersionNumber });
            }
            version.LifecycleStatus = WorkflowDefinitionVersionLifecycleStatus.Published;
            version.PublishedAt = DateTime.UtcNow;
            version.PublishedBy = actor;
            version.UpdatedBy = actor;
            definition.ActivePublishedVersionId = version.Id;
            definition.UpdatedBy = actor;
            AddAudit(version.Id, "Published", actor, new { version = version.VersionNumber, definitionId = definition.Id });
            await _context.SaveChangesAsync(cancellationToken);
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            if (transaction is not null) await transaction.RollbackAsync(cancellationToken);
            return Result<WorkflowVersionDetailsDto>.Failure(Error.Conflict("Workflow version changed. Refresh and retry."));
        }
        catch
        {
            if (transaction is not null) await transaction.RollbackAsync(cancellationToken);
            throw;
        }
        var published = await LoadVersionAsync(versionId, cancellationToken);
        return Result<WorkflowVersionDetailsDto>.Success(ToVersionDto(published!));
    }

    public async Task<Result<IReadOnlyList<WorkflowPreviewStateDto>>> PreviewAsync(Guid versionId, CancellationToken cancellationToken = default)
    {
        var version = await LoadVersionAsync(versionId, cancellationToken); if (version is null) return Result<IReadOnlyList<WorkflowPreviewStateDto>>.Failure(Error.NotFound("Workflow version not found."));
        var preview = version.States.Where(state => !state.IsDeleted).OrderBy(state => state.DisplayOrder).Select(state => new WorkflowPreviewStateDto(state.Code, state.NameEn, state.NameAr, state.IsInitial, state.IsFinal, version.Transitions.Where(transition => !transition.IsDeleted && transition.IsActive && transition.FromStateId == state.Id).OrderBy(transition => transition.DisplayOrder).Select(transition => new WorkflowPreviewTransitionDto(transition.Code, transition.NameEn, transition.NameAr, transition.ToState.Code, transition.ToState.NameEn, transition.ToState.NameAr)).ToList())).ToList();
        return Result<IReadOnlyList<WorkflowPreviewStateDto>>.Success(preview);
    }

    private async Task<WorkflowValidationReport> ValidateVersionAsync(WorkflowDefinitionVersion version, CancellationToken cancellationToken)
    {
        var errors = new List<string>();
        var generic = _validator.Validate(JsonSerializer.Serialize(new
        {
            states = version.States.Where(state => !state.IsDeleted).Select(state => new { state.Code, state.IsInitial, state.IsFinal, state.IsActive }),
            transitions = version.Transitions.Where(transition => !transition.IsDeleted).Select(transition => new
            {
                transition.Code,
                transition.FromStateId,
                transition.ToStateId,
                fromStateCode = version.States.FirstOrDefault(state => state.Id == transition.FromStateId)?.Code,
                toStateCode = version.States.FirstOrDefault(state => state.Id == transition.ToStateId)?.Code,
                transition.IsActive,
                guards = transition.Guards.Where(guard => !guard.IsDeleted).Select(guard => new { type = guard.GuardType, configurationJson = guard.ConfigurationJson }),
                actions = transition.Actions.Where(action => !action.IsDeleted).Select(action => new { type = action.ActionType, configurationJson = action.ConfigurationJson })
            })
        }));
        if (generic.IsFailure) errors.Add(generic.Error?.Message ?? "Workflow definition is invalid.");
        foreach (var guard in version.Transitions.SelectMany(item => item.Guards).Where(item => !item.IsDeleted && item.IsActive))
        {
            var handler = _guardHandlers.FirstOrDefault(item => item.GuardType.Equals(guard.GuardType, StringComparison.OrdinalIgnoreCase));
            if (handler is null) { errors.Add($"Unknown guard type '{guard.GuardType}'."); continue; }
            var result = await ValidateConfigurationAsync(handler, guard.ConfigurationJson, cancellationToken);
            if (result.IsFailure) errors.Add(result.Error?.Message ?? $"Invalid configuration for guard '{guard.GuardType}'.");
        }
        foreach (var action in version.Transitions.SelectMany(item => item.Actions).Where(item => !item.IsDeleted && item.IsActive))
        {
            var handler = _actionHandlers.FirstOrDefault(item => item.ActionType.Equals(action.ActionType, StringComparison.OrdinalIgnoreCase));
            if (handler is null) { errors.Add($"Unknown action type '{action.ActionType}'."); continue; }
            var result = await ValidateConfigurationAsync(handler, action.ConfigurationJson, cancellationToken);
            if (result.IsFailure) errors.Add(result.Error?.Message ?? $"Invalid configuration for action '{action.ActionType}'.");
        }
        var consumer = _consumerValidators.FirstOrDefault(item => item.EntityType.Equals(version.WorkflowDefinition.EntityType, StringComparison.OrdinalIgnoreCase));
        if (consumer is not null)
        {
            var result = await consumer.ValidateAsync(version, cancellationToken);
            if (result.IsFailure) errors.Add(WorkflowErrors.ConsumerValidationFailed(result.Error?.Message ?? "The workflow consumer rejected this definition.").Message);
        }
        return new WorkflowValidationReport(errors.Count == 0, errors, Array.Empty<string>());
    }

    private async Task<Result<string>> ValidateRuleAsync(string type, Dictionary<string, object?> configuration, bool guard, CancellationToken cancellationToken)
    {
        var normalized = type.Trim();
        if (guard)
        {
            var handler = _guardHandlers.FirstOrDefault(item => item.GuardType.Equals(normalized, StringComparison.OrdinalIgnoreCase));
            if (handler is null) return Result<string>.Failure(WorkflowErrors.UnknownGuardType(normalized));
            var json = JsonSerializer.Serialize(configuration ?? new Dictionary<string, object?>());
            var document = JsonDocument.Parse(json);
            var result = await handler.ValidateConfigurationAsync(document.RootElement, cancellationToken);
            return result.IsSuccess ? Result<string>.Success(json) : Result<string>.Failure(result.Error!);
        }
        else
        {
            var handler = _actionHandlers.FirstOrDefault(item => item.ActionType.Equals(normalized, StringComparison.OrdinalIgnoreCase));
            if (handler is null) return Result<string>.Failure(WorkflowErrors.UnknownActionType(normalized));
            var json = JsonSerializer.Serialize(configuration ?? new Dictionary<string, object?>());
            var document = JsonDocument.Parse(json);
            var result = await handler.ValidateConfigurationAsync(document.RootElement, cancellationToken);
            return result.IsSuccess ? Result<string>.Success(json) : Result<string>.Failure(result.Error!);
        }
    }

    private static async Task<Result> ValidateConfigurationAsync(IWorkflowGuardHandler handler, string json, CancellationToken cancellationToken)
    {
        try { using var document = JsonDocument.Parse(json); return await handler.ValidateConfigurationAsync(document.RootElement, cancellationToken); }
        catch (JsonException ex) { return Result.Failure(Error.Validation(ex.Message)); }
    }

    private static async Task<Result> ValidateConfigurationAsync(IWorkflowActionHandler handler, string json, CancellationToken cancellationToken)
    {
        try { using var document = JsonDocument.Parse(json); return await handler.ValidateConfigurationAsync(document.RootElement, cancellationToken); }
        catch (JsonException ex) { return Result.Failure(Error.Validation(ex.Message)); }
    }

    private async Task<Error?> ValidateTransitionShapeAsync(WorkflowDefinitionVersion version, SaveWorkflowTransitionRequest request, Guid? currentId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.NameEn) || string.IsNullOrWhiteSpace(request.NameAr)) return Error.Validation("Transition code and localized names are required.");
        if (request.FromStateId == request.ToStateId) return Error.Validation("A transition cannot target the same state.");
        var from = version.States.FirstOrDefault(state => state.Id == request.FromStateId && !state.IsDeleted);
        var to = version.States.FirstOrDefault(state => state.Id == request.ToStateId && !state.IsDeleted);
        if (from is null || to is null) return Error.Validation("Both transition states must belong to the same draft version.");
        if (from.IsFinal && request.IsActive) return Error.Validation("A final state cannot have outgoing transitions.");
        if (await _context.Set<WorkflowTransition>().AnyAsync(item => item.WorkflowDefinitionVersionId == version.Id && item.Id != currentId && item.Code == request.Code.Trim(), cancellationToken)) return Error.Conflict("Transition code must be unique within the draft.");
        if (request.IsActive && await _context.Set<WorkflowTransition>().AnyAsync(item => item.WorkflowDefinitionVersionId == version.Id && item.Id != currentId && item.IsActive && item.FromStateId == request.FromStateId && item.ToStateId == request.ToStateId, cancellationToken)) return Error.Conflict("An active transition between these states already exists.");
        return null;
    }

    private async Task<Result<WorkflowDefinitionVersion>> DraftVersionAsync(Guid versionId, CancellationToken cancellationToken)
    {
        var version = await LoadVersionAsync(versionId, cancellationToken);
        if (version is null) return Result<WorkflowDefinitionVersion>.Failure(Error.NotFound("Workflow version not found."));
        if (version.LifecycleStatus != WorkflowDefinitionVersionLifecycleStatus.Draft) return Result<WorkflowDefinitionVersion>.Failure(WorkflowErrors.PublishedVersionImmutable);
        return Result<WorkflowDefinitionVersion>.Success(version);
    }

    private Task<WorkflowTransition?> LoadTransitionAsync(Guid transitionId, CancellationToken cancellationToken) => _context.Set<WorkflowTransition>().Include(item => item.Guards).Include(item => item.Actions).FirstOrDefaultAsync(item => item.Id == transitionId, cancellationToken);

    private Task<WorkflowDefinitionVersion?> LoadVersionAsync(Guid versionId, CancellationToken cancellationToken) => _context.Set<WorkflowDefinitionVersion>().Include(item => item.WorkflowDefinition).Include(item => item.States).Include(item => item.Transitions).ThenInclude(item => item.FromState).Include(item => item.Transitions).ThenInclude(item => item.ToState).Include(item => item.Transitions).ThenInclude(item => item.Guards).Include(item => item.Transitions).ThenInclude(item => item.Actions).FirstOrDefaultAsync(item => item.Id == versionId, cancellationToken);

    private WorkflowDefinitionVersion NewVersion(WorkflowDefinition definition, int number, string? changeLog, string actor) => new() { TenantId = definition.TenantId, WorkflowDefinitionId = definition.Id, VersionNumber = number, LifecycleStatus = WorkflowDefinitionVersionLifecycleStatus.Draft, ChangeLog = Normalize(changeLog), CreatedBy = actor };

    private Result EnsureWritable() => IsReadOnly() ? Result.Failure(WorkflowErrors.ReadOnlyViewAs) : Result.Success();
    private Result EnsurePublishable() => IsReadOnly() ? Result.Failure(WorkflowErrors.ReadOnlyViewAs) : Result.Success();
    private bool IsReadOnly() => _httpContextAccessor.HttpContext?.User.FindFirstValue("readOnly") == "true" || _httpContextAccessor.HttpContext?.User.FindFirstValue("viewAs") == "true";
    private Guid CurrentTenantId() => Guid.TryParse(_httpContextAccessor.HttpContext?.User.FindFirstValue("tenantId"), out var id) ? id : Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static Error? ValidateStateRequest(string code, string nameEn, string nameAr) => string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(nameEn) || string.IsNullOrWhiteSpace(nameAr) ? Error.Validation("State code and localized names are required.") : null;
    private void ApplyRowVersion(object entity, byte[]? rowVersion) { if (rowVersion is { Length: > 0 }) _context.Entry(entity).Property(nameof(BaseEntity.RowVersion)).OriginalValue = rowVersion; }
    private void AddAudit(Guid entityId, string action, string actor, object changes) => _context.Set<AuditEntry>().Add(new AuditEntry { TenantId = CurrentTenantId(), EntityType = "WorkflowManagement", EntityId = entityId, Action = action, Changes = JsonSerializer.Serialize(changes), UserId = Guid.TryParse(actor, out var id) ? id : null, UserName = _httpContextAccessor.HttpContext?.User.FindFirstValue("name"), Timestamp = DateTime.UtcNow, CreatedBy = actor });

    private static WorkflowDefinitionDetailsDto ToDefinitionDto(WorkflowDefinition definition) => new(definition.Id, definition.Name, definition.Code, definition.Description, definition.EntityType, definition.IsActive, definition.ActivePublishedVersionId, definition.CreatedAt, definition.CreatedBy, definition.UpdatedAt, definition.UpdatedBy, definition.RowVersion, definition.Versions.Where(item => !item.IsDeleted).OrderByDescending(item => item.VersionNumber).Select(ToVersionListDto).ToList());
    private static WorkflowVersionListItemDto ToVersionListDto(WorkflowDefinitionVersion version) => new(version.Id, version.VersionNumber, version.LifecycleStatus, version.ChangeLog, version.CreatedAt, version.CreatedBy, version.PublishedAt, version.PublishedBy, version.States.Count(item => !item.IsDeleted), version.Transitions.Count(item => !item.IsDeleted), version.Transitions.SelectMany(item => item.Guards).Count(item => !item.IsDeleted), version.Transitions.SelectMany(item => item.Actions).Count(item => !item.IsDeleted), version.RowVersion);
    private static WorkflowVersionDetailsDto ToVersionDto(WorkflowDefinitionVersion version) => new(version.Id, version.WorkflowDefinitionId, version.VersionNumber, version.LifecycleStatus, version.ChangeLog, version.CreatedAt, version.CreatedBy, version.PublishedAt, version.PublishedBy, version.RowVersion, version.States.Where(item => !item.IsDeleted).OrderBy(item => item.DisplayOrder).Select(ToStateDto).ToList(), version.Transitions.Where(item => !item.IsDeleted).OrderBy(item => item.DisplayOrder).Select(item => ToTransitionDto(item, version.States)).ToList());
    private static WorkflowStateDto ToStateDto(WorkflowState state) => new(state.Id, state.Code, state.NameEn, state.NameAr, state.Description, state.DisplayOrder, state.IsInitial, state.IsFinal, state.IsActive, state.RowVersion);
    private static WorkflowTransitionDto ToTransitionDto(WorkflowTransition transition, IEnumerable<WorkflowState> states) { var from = states.FirstOrDefault(item => item.Id == transition.FromStateId); var to = states.FirstOrDefault(item => item.Id == transition.ToStateId); return new WorkflowTransitionDto(transition.Id, transition.Code, transition.NameEn, transition.NameAr, transition.FromStateId, transition.ToStateId, from?.Code ?? string.Empty, to?.Code ?? string.Empty, transition.DisplayOrder, transition.IsActive, transition.Guards.Where(item => !item.IsDeleted).OrderBy(item => item.ExecutionOrder).Select(ToGuardDto).ToList(), transition.Actions.Where(item => !item.IsDeleted).OrderBy(item => item.ExecutionOrder).Select(ToActionDto).ToList(), transition.RowVersion); }
    private static WorkflowGuardDto ToGuardDto(WorkflowTransitionGuard guard) => new(guard.Id, guard.GuardType, guard.ConfigurationJson, guard.ExecutionOrder, guard.IsActive, guard.RowVersion);
    private static WorkflowActionDto ToActionDto(WorkflowTransitionAction action) => new(action.Id, action.ActionType, action.ConfigurationJson, action.ExecutionOrder, action.IsActive, action.RowVersion);
}
