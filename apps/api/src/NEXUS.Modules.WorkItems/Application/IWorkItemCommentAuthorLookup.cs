namespace NEXUS.Modules.WorkItems.Application;

public interface IWorkItemCommentAuthorLookup
{
    Task<IReadOnlyDictionary<string, string>> GetNamesAsync(IEnumerable<string> userIds, CancellationToken cancellationToken = default);
}
