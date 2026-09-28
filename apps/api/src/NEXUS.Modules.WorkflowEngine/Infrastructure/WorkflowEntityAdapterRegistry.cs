using NEXUS.Modules.WorkflowEngine.Application.Contracts;

namespace NEXUS.Modules.WorkflowEngine.Infrastructure;

public sealed class WorkflowEntityAdapterRegistry : IWorkflowEntityAdapterRegistry
{
    private readonly IReadOnlyDictionary<string, IWorkflowEntityAdapter> _adapters;

    public WorkflowEntityAdapterRegistry(IEnumerable<IWorkflowEntityAdapter> adapters)
    {
        _adapters = adapters
            .Where(adapter => !string.IsNullOrWhiteSpace(adapter.EntityType))
            .GroupBy(adapter => adapter.EntityType, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Single(), StringComparer.OrdinalIgnoreCase);
    }

    public IWorkflowEntityAdapter? Resolve(string entityType) =>
        _adapters.TryGetValue(entityType, out var adapter) ? adapter : null;

    public bool IsSupported(string entityType) => Resolve(entityType) is not null;
}
