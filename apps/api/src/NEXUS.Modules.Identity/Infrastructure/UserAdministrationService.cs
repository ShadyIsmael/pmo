using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Identity.Domain;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.Identity.Application;

public class UserAdministrationService : IUserAdministrationService
{
    private static readonly Guid DemoTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly DbContext _context;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public UserAdministrationService(DbContext context, IPasswordHasher<User> passwordHasher, IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<PagedResult<UserAdminDto>> GetPagedAsync(int page, int pageSize, string? search, CancellationToken cancellationToken = default)
    {
        var query = _context.Set<User>()
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(u =>
                u.FirstName.Contains(term) ||
                u.LastName.Contains(term) ||
                (u.FirstName + " " + u.LastName).Contains(term) ||
                u.Email.Contains(term) ||
                (u.JobTitle != null && u.JobTitle.Contains(term)));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(u => u.FirstName)
            .ThenBy(u => u.LastName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var dtos = items.Select(MapToDto).ToList();
        return new PagedResult<UserAdminDto>(dtos, page, pageSize, total);
    }

    public async Task<IReadOnlyList<RoleDto>> GetRolesAsync(CancellationToken cancellationToken = default)
    {
        var roles = await _context.Set<Role>()
            .AsNoTracking()
            .OrderBy(r => r.Category)
            .ThenBy(r => r.Name)
            .ToListAsync(cancellationToken);

        return roles.Select(r => new RoleDto
        {
            Id = r.Id,
            Name = r.Name,
            Description = r.Description,
            Category = r.Category
        }).ToList();
    }

    public async Task<Result<UserAdminDto>> CreateAsync(CreateUserDto dto, string actorId, CancellationToken cancellationToken = default)
    {
        var email = dto.Email?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            return Result<UserAdminDto>.Failure(Error.Validation("A valid email address is required"));

        var firstName = dto.FirstName?.Trim() ?? string.Empty;
        var lastName = dto.LastName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
            return Result<UserAdminDto>.Failure(Error.Validation("First name and last name are required"));

        if (string.IsNullOrWhiteSpace(dto.Password) || dto.Password.Length < 6)
            return Result<UserAdminDto>.Failure(Error.Validation("Password must be at least 6 characters"));

        var existing = await _context.Set<User>().FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
        if (existing is not null)
            return Result<UserAdminDto>.Failure(Error.Conflict("A user with this email already exists"));

        var roles = await ResolveRolesAsync(dto.Roles, cancellationToken);

        var user = new User
        {
            TenantId = GetCurrentTenantId(),
            Email = email,
            PasswordHash = _passwordHasher.HashPassword(new User(), dto.Password),
            FirstName = firstName,
            LastName = lastName,
            JobTitle = dto.JobTitle,
            Phone = dto.Phone,
            IsActive = dto.IsActive,
            CreatedBy = actorId
        };

        foreach (var role in roles)
        {
            user.UserRoles.Add(new UserRole
            {
                TenantId = user.TenantId,
                RoleId = role.Id,
                CreatedBy = actorId
            });
        }

        _context.Set<User>().Add(user);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<UserAdminDto>.Success(MapToDto(user));
    }

    public async Task<Result<UserAdminDto>> UpdateAsync(Guid id, UpdateUserDto dto, string actorId, CancellationToken cancellationToken = default)
    {
        var user = await _context.Set<User>()
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

        if (user is null)
            return Result<UserAdminDto>.Failure(Error.NotFound("User not found"));

        var email = dto.Email?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            return Result<UserAdminDto>.Failure(Error.Validation("A valid email address is required"));

        var firstName = dto.FirstName?.Trim() ?? string.Empty;
        var lastName = dto.LastName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
            return Result<UserAdminDto>.Failure(Error.Validation("First name and last name are required"));

        var emailOwner = await _context.Set<User>().FirstOrDefaultAsync(u => u.Email == email && u.Id != id, cancellationToken);
        if (emailOwner is not null)
            return Result<UserAdminDto>.Failure(Error.Conflict("A user with this email already exists"));

        user.Email = email;
        user.FirstName = firstName;
        user.LastName = lastName;
        user.JobTitle = dto.JobTitle;
        user.Phone = dto.Phone;
        user.IsActive = dto.IsActive;
        user.UpdatedBy = actorId;

        if (!string.IsNullOrWhiteSpace(dto.Password))
        {
            if (dto.Password.Length < 6)
                return Result<UserAdminDto>.Failure(Error.Validation("Password must be at least 6 characters"));
            user.PasswordHash = _passwordHasher.HashPassword(user, dto.Password);
        }

        await ReplaceRolesAsync(user, dto.Roles, actorId, cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        var reloaded = await _context.Set<User>()
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == user.Id, cancellationToken);

        return Result<UserAdminDto>.Success(MapToDto(reloaded ?? user));
    }

    private async Task ReplaceRolesAsync(User user, List<string> roleNames, string actorId, CancellationToken cancellationToken)
    {
        var roles = await ResolveRolesAsync(roleNames, cancellationToken);
        var targetRoleIds = roles.Select(r => r.Id).ToHashSet();

        var toRemove = user.UserRoles.Where(ur => !targetRoleIds.Contains(ur.RoleId)).ToList();
        foreach (var userRole in toRemove)
        {
            _context.Set<UserRole>().Remove(userRole);
            user.UserRoles.Remove(userRole);
        }

        var existingRoleIds = user.UserRoles.Select(ur => ur.RoleId).ToHashSet();
        foreach (var role in roles)
        {
            if (existingRoleIds.Contains(role.Id)) continue;
            var userRole = new UserRole
            {
                TenantId = user.TenantId,
                UserId = user.Id,
                RoleId = role.Id,
                CreatedBy = actorId
            };
            _context.Set<UserRole>().Add(userRole);
        }
    }

    private async Task<List<Role>> ResolveRolesAsync(List<string> roleNames, CancellationToken cancellationToken)
    {
        if (roleNames is null || roleNames.Count == 0)
            return new List<Role>();

        var names = roleNames.Select(r => r.Trim()).Where(r => !string.IsNullOrEmpty(r)).Distinct().ToList();
        if (names.Count == 0)
            return new List<Role>();

        var roles = await _context.Set<Role>()
            .Where(r => names.Contains(r.Name))
            .ToListAsync(cancellationToken);

        return roles;
    }

    private static UserAdminDto MapToDto(User user)
    {
        return new UserAdminDto
        {
            Id = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            FullName = user.FullName,
            JobTitle = user.JobTitle,
            Phone = user.Phone,
            IsActive = user.IsActive,
            Roles = user.UserRoles.Select(ur => ur.Role?.Name ?? string.Empty).OrderBy(name => name).ToList(),
            CreatedAt = user.CreatedAt,
            LastLoginAt = user.LastLoginAt
        };
    }

    private Guid GetCurrentTenantId()
    {
        var tenantId = _httpContextAccessor.HttpContext?.User.FindFirst("tenantId")?.Value;
        if (!string.IsNullOrWhiteSpace(tenantId) && Guid.TryParse(tenantId, out var parsed))
            return parsed;

        return DemoTenantId;
    }
}
