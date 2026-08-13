using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Identity.Application;
using NEXUS.Modules.Identity.Domain;

namespace NEXUS.Modules.Identity.Infrastructure;

public class UserRepository : IUserRepository
{
    private readonly DbContext _context;

    public UserRepository(DbContext context)
    {
        _context = context;
    }

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Set<User>().FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return await _context.Set<User>().FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
    }

    public async Task<User?> GetWithRolesAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Set<User>()
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                    .ThenInclude(r => r.Permissions)
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Set<User>().ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<User>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Set<User>()
            .Where(u => u.IsActive)
            .OrderBy(u => u.FirstName)
            .ThenBy(u => u.LastName)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<User>> GetByRoleNameAsync(string roleName, CancellationToken cancellationToken = default)
    {
        return await _context.Set<User>()
            .Where(u => u.IsActive && u.UserRoles.Any(ur => ur.Role.Name == roleName))
            .OrderBy(u => u.FirstName)
            .ThenBy(u => u.LastName)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        await _context.Set<User>().AddAsync(user, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(User user, CancellationToken cancellationToken = default)
    {
        _context.Set<User>().Update(user);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(User user, CancellationToken cancellationToken = default)
    {
        _context.Set<User>().Remove(user);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
