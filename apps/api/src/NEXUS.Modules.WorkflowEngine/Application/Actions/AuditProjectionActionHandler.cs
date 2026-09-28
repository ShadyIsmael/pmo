using System.Text.Json;
using NEXUS.Modules.WorkflowEngine.Application.Contracts;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.WorkflowEngine.Application.Actions;

public sealed class AuditProjectionActionHandler : IWorkflowActionHandler
{
    public string ActionType => "AuditProjection";

    public Task<Result> ValidateConfigurationAsync(JsonElement configuration, CancellationToken cancellationToken = default)
    {
        if (configuration.ValueKind != JsonValueKind.Object)
            return Task.FromResult(Result.Failure(WorkflowErrors.InvalidActionConfiguration(ActionType, "Configuration must be a JSON object.")));

        return Task.FromResult(Result.Success());
    }

    public Task<Result> ExecuteAsync(WorkflowTransitionContext context, JsonElement configuration, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result.Success());
    }
}