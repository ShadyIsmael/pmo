using System.Text.Json;
using NEXUS.Modules.WorkflowEngine.Application.Contracts;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.WorkflowEngine.Application.Guards;

public sealed class RequireFactGuardHandler : IWorkflowGuardHandler
{
    public string GuardType => "RequireFact";

    public Task<Result> ValidateConfigurationAsync(JsonElement configuration, CancellationToken cancellationToken = default)
    {
        if (configuration.ValueKind != JsonValueKind.Object)
            return Task.FromResult(Result.Failure(WorkflowErrors.InvalidGuardConfiguration(GuardType, "Configuration must be a JSON object.")));

        if (!configuration.TryGetProperty("fact", out var factProp) || factProp.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(factProp.GetString()))
            return Task.FromResult(Result.Failure(WorkflowErrors.InvalidGuardConfiguration(GuardType, "Required property 'fact' (string) is missing or empty.")));

        if (configuration.TryGetProperty("expectedValue", out var expectedValueProp))
        {
            if (expectedValueProp.ValueKind != JsonValueKind.String && expectedValueProp.ValueKind != JsonValueKind.Number && expectedValueProp.ValueKind != JsonValueKind.True && expectedValueProp.ValueKind != JsonValueKind.False && expectedValueProp.ValueKind != JsonValueKind.Null)
                return Task.FromResult(Result.Failure(WorkflowErrors.InvalidGuardConfiguration(GuardType, "Property 'expectedValue' must be a string, number, boolean, or null.")));
        }

        if (configuration.TryGetProperty("operator", out var opProp) && opProp.ValueKind == JsonValueKind.String)
        {
            var op = opProp.GetString();
            if (op is not ("eq" or "ne" or "contains" or "gt" or "lt" or "gte" or "lte" or "truthy" or "falsy"))
                return Task.FromResult(Result.Failure(WorkflowErrors.InvalidGuardConfiguration(GuardType, "Property 'operator' must be one of: eq, ne, contains, gt, lt, gte, lte, truthy, falsy.")));
        }

        return Task.FromResult(Result.Success());
    }

    public Task<Result> EvaluateAsync(WorkflowTransitionContext context, JsonElement configuration, CancellationToken cancellationToken = default)
    {
        var factName = configuration.GetProperty("fact").GetString()!;
        var op = configuration.TryGetProperty("operator", out var opProp) && opProp.ValueKind == JsonValueKind.String
            ? opProp.GetString()!
            : "truthy";

        if (!context.Entity.Facts.TryGetValue(factName, out var actualValue) || actualValue is null)
        {
            if (op is "falsy" or "ne")
                return Task.FromResult(Result.Success());

            return Task.FromResult(Result.Failure(WorkflowErrors.GuardRejected(GuardType, $"Required fact '{factName}' is missing or null.")));
        }

        JsonElement? expectedValue = configuration.TryGetProperty("expectedValue", out var expProp) ? expProp : default;

        var result = EvaluateCondition(actualValue, op, expectedValue);
        if (!result)
        {
            var expectedStr = expectedValue?.ToString() ?? "<none>";
            var actualStr = actualValue.ToString() ?? "<null>";
            return Task.FromResult(Result.Failure(WorkflowErrors.GuardRejected(GuardType, $"Fact '{factName}' {op} '{expectedStr}' failed (actual: '{actualStr}').")));
        }

        return Task.FromResult(Result.Success());
    }

    private static bool EvaluateCondition(object actualValue, string op, JsonElement? expectedValue)
    {
        return op switch
        {
            "truthy" => IsTruthy(actualValue),
            "falsy" => !IsTruthy(actualValue),
            "eq" => expectedValue.HasValue && ValuesEqual(actualValue, expectedValue.Value),
            "ne" => !expectedValue.HasValue || !ValuesEqual(actualValue, expectedValue.Value),
            "contains" => expectedValue.HasValue && actualValue.ToString()?.Contains(expectedValue.Value.GetString() ?? "", StringComparison.OrdinalIgnoreCase) == true,
            "gt" => expectedValue.HasValue && CompareValues(actualValue, expectedValue.Value) > 0,
            "lt" => expectedValue.HasValue && CompareValues(actualValue, expectedValue.Value) < 0,
            "gte" => expectedValue.HasValue && CompareValues(actualValue, expectedValue.Value) >= 0,
            "lte" => expectedValue.HasValue && CompareValues(actualValue, expectedValue.Value) <= 0,
            _ => false
        };
    }

    private static bool IsTruthy(object value)
    {
        return value switch
        {
            bool b => b,
            string s => !string.IsNullOrWhiteSpace(s),
            int i => i != 0,
            long l => l != 0,
            double d => d != 0,
            decimal m => m != 0,
            _ => value != null
        };
    }

    private static bool ValuesEqual(object actual, JsonElement expected)
    {
        return expected.ValueKind switch
        {
            JsonValueKind.String => string.Equals(actual.ToString(), expected.GetString(), StringComparison.OrdinalIgnoreCase),
            JsonValueKind.Number => actual is double ad && Math.Abs(ad - expected.GetDouble()) < double.Epsilon
                || actual is int ai && ai == expected.GetInt32()
                || actual is long al && al == expected.GetInt64()
                || actual is decimal adec && adec == (decimal)expected.GetDouble(),
            JsonValueKind.True => actual is bool ab && ab,
            JsonValueKind.False => actual is bool ab && !ab,
            JsonValueKind.Null => actual == null,
            _ => false
        };
    }

    private static int CompareValues(object actual, JsonElement expected)
    {
        return expected.ValueKind switch
        {
            JsonValueKind.Number when actual is IComparable c && expected.TryGetDouble(out var d) => c.CompareTo(d),
            JsonValueKind.Number when actual is IComparable c && expected.TryGetInt32(out var i) => c.CompareTo(i),
            JsonValueKind.Number when actual is IComparable c && expected.TryGetInt64(out var l) => c.CompareTo(l),
            JsonValueKind.String when actual is IComparable c => c.CompareTo(expected.GetString()),
            _ => 0
        };
    }
}