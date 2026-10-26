using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Identity.Application;
using NEXUS.Modules.Identity.Domain;

namespace NEXUS.Modules.Identity.Infrastructure;

public sealed class UserSessionValidator : IUserSessionValidator
{
    private readonly DbContext _context;

    public UserSessionValidator(DbContext context) => _context = context;

    public Task<bool> IsActiveAsync(Guid sessionId, Guid userId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        if (sessionId == Guid.Empty || userId == Guid.Empty || tenantId == Guid.Empty)
            return Task.FromResult(false);

        var now = DateTime.UtcNow;
        return _context.Set<UserSession>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AnyAsync(session => session.Id == sessionId
                && session.UserId == userId
                && session.TenantId == tenantId
                && session.RevokedAt == null
                && session.ExpiresAt > now
                && !session.User.IsDeleted
                && session.User.IsActive
                && session.User.TenantId == tenantId,
                cancellationToken);
    }
}
