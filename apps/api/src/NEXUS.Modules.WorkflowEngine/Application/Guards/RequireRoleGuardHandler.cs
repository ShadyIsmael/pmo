using System.Text.Json;
using NEXUS.Modules.WorkflowEngine.Application.Contracts;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.WorkflowEngine.Application.Guards;

public sealed class RequireRoleGuardHandler : IWorkflowGuardHandler
{
    public string GuardType => "RequireRole";

    public Task<Result> ValidateConfigurationAsync(JsonElement configuration, CancellationToken cancellationToken = default)
    {
        if (configuration.ValueKind != JsonValueKind.Object)
            return Task.FromResult(Result.Failure(WorkflowErrors.InvalidGuardConfiguration(GuardType, "Configuration must be a JSON object.")));

        if (!configuration.TryGetProperty("role", out var roleProp) || roleProp.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(roleProp.GetString()))
            return Task.FromResult(Result.Failure(WorkflowErrors.InvalidGuardConfiguration(GuardType, "Required property 'role' (string) is missing or empty.")));

        return Task.FromResult(Result.Success());
    }

    public Task<Result> EvaluateAsync(WorkflowTransitionContext context, JsonElement configuration, CancellationToken cancellationToken = default)
    {
        var role = configuration.GetProperty("role").GetString()!;
        var roles = context.Execution.Metadata?.TryGetValue("roles", out var rolesJson) == true ? rolesJson : null;

        if (roles is null)
            return Task.FromResult(Result.Failure(WorkflowErrors.GuardRejected(GuardType, $"Role '{role}' not found in user claims.")));

        if (!HasRole(roles, role))
            return Task.FromResult(Result.Failure(WorkflowErrors.GuardRejected(GuardType, $"Required role '{role}' not granted.")));

        return Task.FromResult(Result.Success());
    }

    private static bool HasRole(string? rolesJson, string requiredRole)
    {
        if (string.IsNullOrWhiteSpace(rolesJson))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(rolesJson);
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var element in doc.RootElement.EnumerateArray())
                {
                    if (element.ValueKind == JsonValueKind.String && string.Equals(element.GetString(), requiredRole, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
        }
        catch (JsonException)
        {
            return false;
        }
        return false;
    }
}