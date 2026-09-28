using System.Text.Json;
using NEXUS.Modules.WorkflowEngine.Application.Contracts;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.WorkflowEngine.Application;

public sealed class WorkflowDefinitionValidator
{
    private readonly IWorkflowGuardHandlerRegistry _guards;
    private readonly IWorkflowActionHandlerRegistry _actions;

    public WorkflowDefinitionValidator(IWorkflowGuardHandlerRegistry guards, IWorkflowActionHandlerRegistry actions)
    {
        _guards = guards;
        _actions = actions;
    }

    public Result Validate(string workflowData)
    {
        if (string.IsNullOrWhiteSpace(workflowData))
            return Result.Failure(Error.Validation("Workflow definition JSON is required."));

        try
        {
            using var document = JsonDocument.Parse(workflowData);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return Result.Failure(Error.Validation("Workflow definition must be a JSON object."));

            var root = document.RootElement;
            if (!TryGetArray(root, "states", out var states))
                return Result.Failure(Error.Validation("Workflow definition must contain a states array."));
            if (!TryGetArray(root, "transitions", out var transitions))
                return Result.Failure(Error.Validation("Workflow definition must contain a transitions array."));

            var errors = new List<string>();
            var stateCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var finalStateCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var initialCount = 0;

            foreach (var state in states.EnumerateArray())
            {
                if (state.ValueKind != JsonValueKind.Object)
                {
                    errors.Add("Each state must be a JSON object.");
                    continue;
                }

                var code = GetString(state, "code");
                if (string.IsNullOrWhiteSpace(code)) errors.Add("Every state must have a code.");
                else if (!stateCodes.Add(code)) errors.Add($"Duplicate state code '{code}'.");

                var isActive = GetBoolean(state, "isActive", true);
                var isInitial = GetBoolean(state, "isInitial", false);
                var isFinal = GetBoolean(state, "isFinal", false);
                if (isActive && isInitial) initialCount++;
                if (isActive && isFinal && !string.IsNullOrWhiteSpace(code)) finalStateCodes.Add(code);
            }

            if (stateCodes.Count == 0) errors.Add("At least one state is required.");
            if (initialCount != 1) errors.Add("Exactly one active initial state is required.");

            var transitionCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var activeEdges = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var transition in transitions.EnumerateArray())
            {
                if (transition.ValueKind != JsonValueKind.Object)
                {
                    errors.Add("Each transition must be a JSON object.");
                    continue;
                }

                var code = GetString(transition, "code");
                if (string.IsNullOrWhiteSpace(code)) errors.Add("Every transition must have a code.");
                else if (!transitionCodes.Add(code)) errors.Add($"Duplicate transition code '{code}'.");

                if (!GetBoolean(transition, "isActive", true)) continue;
                var from = GetString(transition, "fromStateCode", "fromState", "from");
                var to = GetString(transition, "toStateCode", "toState", "to");
                if (string.IsNullOrWhiteSpace(from) || !stateCodes.Contains(from)) errors.Add($"Transition '{code}' references an unknown source state.");
                if (string.IsNullOrWhiteSpace(to) || !stateCodes.Contains(to)) errors.Add($"Transition '{code}' references an unknown target state.");
                if (!string.IsNullOrWhiteSpace(from) && !string.IsNullOrWhiteSpace(to))
                {
                    if (string.Equals(from, to, StringComparison.OrdinalIgnoreCase)) errors.Add($"Transition '{code}' cannot transition to the same state.");
                    if (!activeEdges.Add($"{from}\u001f{to}")) errors.Add($"Duplicate active transition from '{from}' to '{to}'.");
                    if (finalStateCodes.Contains(from)) errors.Add($"Final state '{from}' cannot have outgoing transitions.");
                }

                ValidateRules(transition, "guards", "guardType", _guards.IsSupported, errors, code, "guard");
                ValidateRules(transition, "actions", "actionType", _actions.IsSupported, errors, code, "action");
            }

            return errors.Count == 0
                ? Result.Success()
                : Result.Failure(Error.Validation(string.Join(" ", errors)));
        }
        catch (JsonException ex)
        {
            return Result.Failure(Error.Validation($"Workflow definition JSON is invalid: {ex.Message}"));
        }
    }

    private static void ValidateRules(JsonElement transition, string property, string typeProperty, Func<string, bool> isSupported, List<string> errors, string? transitionCode, string label)
    {
        if (!TryGetArray(transition, property, out var rules)) return;
        foreach (var rule in rules.EnumerateArray())
        {
            if (rule.ValueKind != JsonValueKind.Object)
            {
                errors.Add($"Each {label} on transition '{transitionCode}' must be a JSON object.");
                continue;
            }

            var type = GetString(rule, typeProperty, "type");
            if (string.IsNullOrWhiteSpace(type) || !isSupported(type)) errors.Add($"Unsupported {label} type '{type}' on transition '{transitionCode}'.");
            if (TryGetProperty(rule, "configuration", out var config))
            {
                if (config.ValueKind != JsonValueKind.Object) errors.Add($"{label} configuration on transition '{transitionCode}' must be a JSON object.");
            }
            else if (TryGetProperty(rule, "configurationJson", out var configJson))
            {
                if (configJson.ValueKind != JsonValueKind.String || !TryParseObject(configJson.GetString(), out _)) errors.Add($"{label} configurationJson on transition '{transitionCode}' must contain a valid JSON object.");
            }
        }
    }

    private static bool TryGetArray(JsonElement element, string name, out JsonElement value) => TryGetProperty(element, name, out value) && value.ValueKind == JsonValueKind.Array;
    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase)) { value = property.Value; return true; }
        value = default;
        return false;
    }
    private static string? GetString(JsonElement element, params string[] names)
    {
        foreach (var name in names)
            if (TryGetProperty(element, name, out var value) && value.ValueKind == JsonValueKind.String) return value.GetString();
        return null;
    }
    private static bool GetBoolean(JsonElement element, string name, bool fallback)
    {
        return TryGetProperty(element, name, out var value) && (value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False) ? value.GetBoolean() : fallback;
    }
    private static bool TryParseObject(string? json, out JsonDocument? document)
    {
        document = null;
        if (string.IsNullOrWhiteSpace(json)) return false;
        try { document = JsonDocument.Parse(json); return document.RootElement.ValueKind == JsonValueKind.Object; }
        catch (JsonException) { return false; }
    }
}
