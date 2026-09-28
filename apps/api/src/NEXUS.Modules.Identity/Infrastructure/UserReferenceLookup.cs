using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Identity.Domain;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.Identity.Infrastructure;

public sealed class UserReferenceLookup : IUserReferenceLookup
{
    private readonly DbContext _context;

    public UserReferenceLookup(DbContext context) => _context = context;

    public Task<UserReference?> FindAsync(Guid userId, CancellationToken cancellationToken = default) =>
        _context.Set<User>()
            .Where(user => user.Id == userId && user.IsActive)
            .Select(user => new UserReference(user.Id, user.FirstName + " " + user.LastName, user.Email))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, UserReference>> GetManyAsync(IEnumerable<Guid> userIds, CancellationToken cancellationToken = default)
    {
        var ids = userIds.Where(id => id != Guid.Empty).Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<Guid, UserReference>();

        return await _context.Set<User>()
            .Where(user => ids.Contains(user.Id) && user.IsActive)
            .Select(user => new UserReference(user.Id, user.FirstName + " " + user.LastName, user.Email))
            .ToDictionaryAsync(user => user.Id, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, UserReference>> FindByEmailsAsync(IEnumerable<string> emails, CancellationToken cancellationToken = default)
    {
        var candidates = emails
            .Where(email => !string.IsNullOrWhiteSpace(email))
            .Select(email => email.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (candidates.Count == 0) return new Dictionary<string, UserReference>(StringComparer.OrdinalIgnoreCase);

        var users = await _context.Set<User>()
            .Where(user => user.IsActive && candidates.Contains(user.Email))
            .Select(user => new UserReference(user.Id, user.FirstName + " " + user.LastName, user.Email))
            .ToListAsync(cancellationToken);
        return users.ToDictionary(user => user.Email, StringComparer.OrdinalIgnoreCase);
    }
}
