using System.Text.Json;
using NEXUS.Modules.WorkflowEngine.Application.Contracts;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.WorkflowEngine.Application.Guards;

public sealed class RequireCommentGuardHandler : IWorkflowGuardHandler
{
    public string GuardType => "RequireComment";

    public Task<Result> ValidateConfigurationAsync(JsonElement configuration, CancellationToken cancellationToken = default)
    {
        if (configuration.ValueKind != JsonValueKind.Object)
            return Task.FromResult(Result.Failure(WorkflowErrors.InvalidGuardConfiguration(GuardType, "Configuration must be a JSON object.")));

        if (configuration.TryGetProperty("minLength", out var minLengthProp))
        {
            if (minLengthProp.ValueKind != JsonValueKind.Number || minLengthProp.GetInt32() < 0)
                return Task.FromResult(Result.Failure(WorkflowErrors.InvalidGuardConfiguration(GuardType, "Property 'minLength' must be a non-negative integer.")));
        }

        return Task.FromResult(Result.Success());
    }

    public Task<Result> EvaluateAsync(WorkflowTransitionContext context, JsonElement configuration, CancellationToken cancellationToken = default)
    {
        var minLength = 1;
        if (configuration.TryGetProperty("minLength", out var minLengthProp) && minLengthProp.ValueKind == JsonValueKind.Number)
            minLength = minLengthProp.GetInt32();

        var comment = context.Execution.Comment;

        if (string.IsNullOrWhiteSpace(comment) || comment.Length < minLength)
            return Task.FromResult(Result.Failure(WorkflowErrors.GuardRejected(GuardType, $"Comment is required (minimum {minLength} characters).")));

        return Task.FromResult(Result.Success());
    }
}