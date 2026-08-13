using NEXUS.SharedKernel;

namespace NEXUS.Modules.WorkflowEngine.Application;

public class WorkflowDesignerService : IWorkflowDesignerService
{
    public Task<Result> ValidateWorkflowAsync(string workflowData, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result.Success());
    }
}
