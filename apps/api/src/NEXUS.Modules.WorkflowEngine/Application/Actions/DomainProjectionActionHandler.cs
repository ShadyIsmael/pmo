using System.Text.Json;
using NEXUS.Modules.WorkflowEngine.Application.Contracts;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.WorkflowEngine.Application.Actions;

public sealed class DomainProjectionActionHandler : IWorkflowActionHandler
{
    public string ActionType => "DomainProjection";

    public Task<Result> ValidateConfigurationAsync(JsonElement configuration, CancellationToken cancellationToken = default)
    {
        if (configuration.ValueKind != JsonValueKind.Object)
            return Task.FromResult(Result.Failure(WorkflowErrors.InvalidActionConfiguration(ActionType, "Configuration must be a JSON object.")));

        if (configuration.TryGetProperty("targetStateCode", out var prop))
        {
            if (prop.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(prop.GetString()))
                return Task.FromResult(Result.Failure(WorkflowErrors.InvalidActionConfiguration(ActionType, "Property 'targetStateCode' must be a non-empty string.")));
        }

        return Task.FromResult(Result.Success());
    }

    public Task<Result> ExecuteAsync(WorkflowTransitionContext context, JsonElement configuration, CancellationToken cancellationToken = default)
    {
        var result = context.EntityAdapter.ApplyTransitionAsync(context, cancellationToken);
        return result;
    }
}