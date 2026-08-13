using NEXUS.SharedKernel;
using NEXUS.Modules.WorkflowEngine.Domain;

namespace NEXUS.Modules.WorkflowEngine.Application;

public interface IWorkflowService
{
    Task<Result<WorkflowInstance>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<WorkflowInstance>> GetPagedAsync(int page, int pageSize, CancellationToken cancellationToken = default);
}
