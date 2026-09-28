using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Identity.Domain;
using NEXUS.Modules.WorkItems.Application;

namespace NEXUS.Persistence;

public sealed class WorkItemCommentAuthorLookup : IWorkItemCommentAuthorLookup
{
    private readonly DbContext _context;

    public WorkItemCommentAuthorLookup(DbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyDictionary<string, string>> GetNamesAsync(IEnumerable<string> userIds, CancellationToken cancellationToken = default)
    {
        var ids = userIds.Where(id => Guid.TryParse(id, out _)).Select(Guid.Parse).Distinct().ToList();
        var users = await _context.Set<User>().Where(user => ids.Contains(user.Id)).Select(user => new { user.Id, user.FirstName, user.LastName, user.Email }).ToListAsync(cancellationToken);
        return users.ToDictionary(user => user.Id.ToString(), user => string.Join(" ", new[] { user.FirstName, user.LastName }.Where(value => !string.IsNullOrWhiteSpace(value))).Trim() is { Length: > 0 } name ? name : user.Email ?? user.Id.ToString());
    }
}
