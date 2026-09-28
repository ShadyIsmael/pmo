using System.Text.Json;
using NEXUS.Modules.WorkflowEngine.Application.Contracts;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.WorkflowEngine.Application.Guards;

public sealed class RequirePermissionGuardHandler : IWorkflowGuardHandler
{
    public string GuardType => "RequirePermission";

    public Task<Result> ValidateConfigurationAsync(JsonElement configuration, CancellationToken cancellationToken = default)
    {
        if (configuration.ValueKind != JsonValueKind.Object)
            return Task.FromResult(Result.Failure(WorkflowErrors.InvalidGuardConfiguration(GuardType, "Configuration must be a JSON object.")));

        if (!configuration.TryGetProperty("permission", out var permissionProp) || permissionProp.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(permissionProp.GetString()))
            return Task.FromResult(Result.Failure(WorkflowErrors.InvalidGuardConfiguration(GuardType, "Required property 'permission' (string) is missing or empty.")));

        return Task.FromResult(Result.Success());
    }

    public Task<Result> EvaluateAsync(WorkflowTransitionContext context, JsonElement configuration, CancellationToken cancellationToken = default)
    {
        var permission = configuration.GetProperty("permission").GetString()!;
        var permissions = context.Execution.Metadata?.TryGetValue("permissions", out var perms) == true ? perms : null;

        if (permissions is null)
            return Task.FromResult(Result.Failure(WorkflowErrors.GuardRejected(GuardType, $"Permission '{permission}' not found in user claims.")));

        if (!HasPermission(permissions, permission))
            return Task.FromResult(Result.Failure(WorkflowErrors.GuardRejected(GuardType, $"Required permission '{permission}' not granted.")));

        return Task.FromResult(Result.Success());
    }

    private static bool HasPermission(string? permissionsJson, string requiredPermission)
    {
        if (string.IsNullOrWhiteSpace(permissionsJson))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(permissionsJson);
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var element in doc.RootElement.EnumerateArray())
                {
                    if (element.ValueKind == JsonValueKind.String && string.Equals(element.GetString(), requiredPermission, StringComparison.OrdinalIgnoreCase))
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