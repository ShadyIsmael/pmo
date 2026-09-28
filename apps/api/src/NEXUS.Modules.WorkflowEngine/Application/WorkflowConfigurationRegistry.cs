using NEXUS.Modules.WorkflowEngine.Application.Contracts;
using NEXUS.Modules.WorkflowEngine.Application.Guards;
using NEXUS.Modules.WorkflowEngine.Application.Actions;

namespace NEXUS.Modules.WorkflowEngine.Application;

public sealed class WorkflowConfigurationRegistry : IWorkflowGuardHandlerRegistry, IWorkflowActionHandlerRegistry
{
    private readonly IReadOnlyDictionary<string, IWorkflowGuardHandler> _guards;
    private readonly IReadOnlyDictionary<string, IWorkflowActionHandler> _actions;

    public WorkflowConfigurationRegistry(IEnumerable<IWorkflowGuardHandler> guards, IEnumerable<IWorkflowActionHandler> actions)
    {
        _guards = guards
            .Where(g => !string.IsNullOrWhiteSpace(g.GuardType))
            .GroupBy(g => g.GuardType, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Single(), StringComparer.OrdinalIgnoreCase);

        _actions = actions
            .Where(a => !string.IsNullOrWhiteSpace(a.ActionType))
            .GroupBy(a => a.ActionType, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(a => a.Key, a => a.Single(), StringComparer.OrdinalIgnoreCase);
    }

    public bool IsSupported(string guardType) =>
        !string.IsNullOrWhiteSpace(guardType) && _guards.ContainsKey(guardType);

    public bool IsSupportedAction(string actionType) =>
        !string.IsNullOrWhiteSpace(actionType) && _actions.ContainsKey(actionType);

    public IWorkflowGuardHandler? GetGuardHandler(string guardType) =>
        _guards.TryGetValue(guardType, out var handler) ? handler : null;

    public IWorkflowActionHandler? GetActionHandler(string actionType) =>
        _actions.TryGetValue(actionType, out var handler) ? handler : null;

    bool IWorkflowGuardHandlerRegistry.IsSupported(string guardType) => IsSupported(guardType);

    bool IWorkflowActionHandlerRegistry.IsSupported(string actionType) => IsSupportedAction(actionType);
}
