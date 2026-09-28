using NEXUS.Modules.WorkItems.Domain;

namespace NEXUS.Modules.WorkItems.Application;

public interface IWorkItemCommentAccess
{
    Task<bool> CanCommentAsync(WorkItem item, Guid userId, CancellationToken cancellationToken = default);
}
