using System.Text.Json;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.WorkflowEngine.Application.Contracts;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.WorkflowEngine.Application.Actions;

public sealed class AuditProjectionActionHandler : IWorkflowActionHandler
{
    private readonly IAuditService _auditService;

    public AuditProjectionActionHandler(IAuditService auditService)
    {
        _auditService = auditService;
    }

    public string ActionType => "AuditProjection";

    public Task<Result> ValidateConfigurationAsync(JsonElement configuration, CancellationToken cancellationToken = default)
    {
        if (configuration.ValueKind != JsonValueKind.Object)
            return Task.FromResult(Result.Failure(WorkflowErrors.InvalidActionConfiguration(ActionType, "Configuration must be a JSON object.")));

        return Task.FromResult(Result.Success());
    }

    public async Task<Result> ExecuteAsync(WorkflowTransitionContext context, JsonElement configuration, CancellationToken cancellationToken = default)
    {
        Guid? userId = Guid.TryParse(context.Execution.UserId, out var uid) ? uid : null;

        var entry = new AuditEntry
        {
            TenantId = context.Execution.TenantId,
            EntityType = context.Entity.EntityType,
            EntityId = context.EntityId,
            Action = "Transitioned",
            Changes = JsonSerializer.Serialize(new
            {
                workflow = new
                {
                    definition = context.Transition.WorkflowDefinitionVersion.WorkflowDefinition.Code,
                    version = context.Transition.WorkflowDefinitionVersion.VersionNumber
                },
                entityType = context.Entity.EntityType,
                entityId = context.EntityId,
                transitionCode = context.Transition.Code,
                fromState = context.FromState.Code,
                toState = context.ToState.Code,
                executor = context.Execution.UserId,
                correlationId = context.Execution.CorrelationId
            }),
            UserId = userId,
            UserName = context.Execution.UserName,
            Timestamp = DateTime.UtcNow,
            CreatedBy = context.Execution.UserId,
            CorrelationId = context.Execution.CorrelationId
        };

        return await _auditService.AppendAsync(entry, cancellationToken);
    }
}
