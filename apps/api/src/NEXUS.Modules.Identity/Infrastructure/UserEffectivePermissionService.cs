using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Identity.Application;
using NEXUS.Modules.Identity.Domain;

namespace NEXUS.Modules.Identity.Infrastructure;

public sealed class UserEffectivePermissionService : IUserEffectivePermissionService
{
    private readonly DbContext _context;

    public UserEffectivePermissionService(DbContext context) => _context = context;

    public async Task<IReadOnlySet<string>> GetEffectivePermissionsAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || tenantId == Guid.Empty)
            return new HashSet<string>(StringComparer.Ordinal);

        var activeUserExists = await _context.Set<User>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AnyAsync(user => user.Id == userId && user.TenantId == tenantId && !user.IsDeleted && user.IsActive, cancellationToken);

        if (!activeUserExists)
            return new HashSet<string>(StringComparer.Ordinal);

        // Login and refresh can run before a tenant claim is established, so this query
        // deliberately bypasses ambient filters and repeats every tenant/deletion check.
        var inherited = await (
                from assignment in _context.Set<UserRole>().IgnoreQueryFilters().AsNoTracking()
                where assignment.UserId == userId && assignment.TenantId == tenantId && !assignment.IsDeleted
                      && assignment.Role.TenantId == tenantId && !assignment.Role.IsDeleted
                from permission in assignment.Role.Permissions
                where permission.TenantId == tenantId && !permission.IsDeleted
                select permission.Resource + "." + permission.Action)
            .Distinct()
            .ToListAsync(cancellationToken);

        var overrides = await _context.Set<UserPermissionOverride>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(item => item.UserId == userId && item.TenantId == tenantId && !item.IsDeleted
                           && item.Permission.TenantId == tenantId && !item.Permission.IsDeleted)
            .Select(item => new
            {
                Permission = item.Permission.Resource + "." + item.Permission.Action,
                item.Effect
            })
            .ToListAsync(cancellationToken);

        var effective = new HashSet<string>(inherited, StringComparer.Ordinal);
        foreach (var item in overrides)
        {
            switch (item.Effect)
            {
                case PermissionOverrideEffect.Allow:
                    effective.Add(item.Permission);
                    break;
                case PermissionOverrideEffect.Deny:
                    effective.Remove(item.Permission);
                    break;
            }
        }

        return effective;
    }

    public async Task<IReadOnlySet<string>> GetEffectiveRoleNamesAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || tenantId == Guid.Empty)
            return new HashSet<string>(StringComparer.Ordinal);

        var roles = await (
                from assignment in _context.Set<UserRole>().IgnoreQueryFilters().AsNoTracking()
                where assignment.UserId == userId && assignment.TenantId == tenantId && !assignment.IsDeleted
                      && assignment.Role.TenantId == tenantId && !assignment.Role.IsDeleted
                      && assignment.User.TenantId == tenantId && !assignment.User.IsDeleted && assignment.User.IsActive
                select assignment.Role.Name)
            .Distinct()
            .ToListAsync(cancellationToken);

        return new HashSet<string>(roles, StringComparer.Ordinal);
    }
}
