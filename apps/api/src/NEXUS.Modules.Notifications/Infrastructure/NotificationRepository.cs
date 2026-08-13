using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Notifications.Domain;

namespace NEXUS.Modules.Notifications.Infrastructure;

public class NotificationRepository
{
    private readonly DbContext _context;

    public NotificationRepository(DbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<Notification>> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.Set<Notification>()
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.SentAt)
            .ToListAsync(cancellationToken);
    }
}
