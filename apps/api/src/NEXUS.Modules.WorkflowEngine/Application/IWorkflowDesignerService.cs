using NEXUS.SharedKernel;

namespace NEXUS.Modules.WorkflowEngine.Application;

public interface IWorkflowDesignerService
{
    Task<Result> ValidateWorkflowAsync(string workflowData, CancellationToken cancellationToken = default);
}
