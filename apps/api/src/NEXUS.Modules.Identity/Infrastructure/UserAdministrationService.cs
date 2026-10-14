using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text.Json;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.Identity.Domain;
using NEXUS.Modules.Identity.Infrastructure;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.Identity.Application;

public class UserAdministrationService : IUserAdministrationService
{
    private static readonly Guid DemoTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly DbContext _context;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IUserEffectivePermissionService _effectivePermissions;
    private readonly IAuditService _auditService;
    private readonly LocalPasswordPolicyOptions _passwordPolicy;

    public UserAdministrationService(DbContext context, IPasswordHasher<User> passwordHasher, IHttpContextAccessor httpContextAccessor, IUserEffectivePermissionService effectivePermissions, IAuditService auditService, IOptions<LocalPasswordPolicyOptions> passwordPolicy)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _httpContextAccessor = httpContextAccessor;
        _effectivePermissions = effectivePermissions;
        _auditService = auditService;
        _passwordPolicy = passwordPolicy.Value;
    }

    public async Task<PagedResult<UserAdminDto>> GetPagedAsync(int page, int pageSize, string? search, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        search = search?.Trim();
        if (search?.Length > 200)
            search = search[..200];

        var query = _context.Set<User>()
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search;
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

    public async Task<Result<CreatedUserDto>> CreateAsync(CreateUserDto dto, string actorId, CancellationToken cancellationToken = default)
    {
        var email = dto.Email?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            return Result<CreatedUserDto>.Failure(Error.Validation("A valid email address is required"));

        var firstName = dto.FirstName?.Trim() ?? string.Empty;
        var lastName = dto.LastName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
            return Result<CreatedUserDto>.Failure(Error.Validation("First name and last name are required"));

        if (dto.AuthenticationSource != AuthenticationSource.Local)
            return Result<CreatedUserDto>.Failure(new Error("AUTHENTICATION_SOURCE_UNAVAILABLE", "External user provisioning must use a configured directory authentication provider.", 400));

        var existing = await _context.Set<User>().FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
        if (existing is not null)
            return Result<CreatedUserDto>.Failure(Error.Conflict("A user with this email already exists"));

        var roles = await ResolveRolesAsync(dto.Roles, cancellationToken);
        if (roles is null)
            return Result<CreatedUserDto>.Failure(Error.Validation("One or more selected roles are unavailable"));
        if (!await CanAssignRolesAsync(roles, actorId, GetCurrentTenantId(), cancellationToken))
            return Result<CreatedUserDto>.Failure(Error.Forbidden("You cannot assign a role with permissions you do not have"));

        var now = DateTime.UtcNow;
        var temporaryPassword = TemporaryPasswordGenerator.Generate();

        var user = new User
        {
            TenantId = GetCurrentTenantId(),
            Email = email,
            AuthenticationSource = AuthenticationSource.Local,
            PasswordHash = string.Empty,
            MustChangePassword = true,
            TemporaryPasswordExpiresAt = now.AddHours(_passwordPolicy.TemporaryPasswordExpiryHours),
            PasswordChangedAt = now,
            FirstName = firstName,
            LastName = lastName,
            JobTitle = dto.JobTitle,
            Phone = dto.Phone,
            IsActive = dto.IsActive,
            CreatedBy = actorId
        };
        user.PasswordHash = _passwordHasher.HashPassword(user, temporaryPassword);

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
        var actorUserId = Guid.TryParse(actorId, out var parsedActorId) ? parsedActorId : Guid.Empty;
        await _auditService.AppendAsync(new AuditEntry
        {
            TenantId = user.TenantId,
            EntityType = "User",
            EntityId = user.Id,
            Action = "LocalUserCreated",
            Changes = JsonSerializer.Serialize(new { AuthenticationSource = user.AuthenticationSource.ToString(), PasswordState = "Temporary" }),
            NewValues = JsonSerializer.Serialize(new { user.Email, AuthenticationSource = user.AuthenticationSource.ToString(), PasswordState = "Temporary" }),
            UserId = actorUserId,
            UserName = actorId,
            CreatedBy = actorId
        }, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<CreatedUserDto>.Success(new CreatedUserDto
        {
            User = MapToDto(user),
            TemporaryPassword = temporaryPassword
        });
    }

    public async Task<Result<TemporaryPasswordResetDto>> ResetPasswordAsync(Guid id, string actorId, CancellationToken cancellationToken = default)
    {
        var tenantId = GetCurrentTenantId();
        var user = await _context.Set<User>()
            .IgnoreQueryFilters()
            .Include(candidate => candidate.UserRoles)
                .ThenInclude(userRole => userRole.Role)
            .FirstOrDefaultAsync(candidate => candidate.Id == id && candidate.TenantId == tenantId && !candidate.IsDeleted, cancellationToken);

        if (user is null)
            return Result<TemporaryPasswordResetDto>.Failure(Error.NotFound("User not found"));
        if (user.AuthenticationSource != AuthenticationSource.Local)
            return Result<TemporaryPasswordResetDto>.Failure(Error.Forbidden("A local password cannot be reset for a directory-authenticated user"));

        var now = DateTime.UtcNow;
        var temporaryPassword = TemporaryPasswordGenerator.Generate();
        user.PasswordHash = _passwordHasher.HashPassword(user, temporaryPassword);
        user.MustChangePassword = true;
        user.TemporaryPasswordExpiresAt = now.AddHours(_passwordPolicy.TemporaryPasswordExpiryHours);
        user.PasswordChangedAt = now;
        user.UpdatedAt = now;
        user.UpdatedBy = actorId;

        var sessions = await _context.Set<UserSession>()
            .IgnoreQueryFilters()
            .Where(session => session.UserId == id && session.TenantId == tenantId && session.RevokedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var session in sessions)
            session.RevokedAt = now;

        var actorUserId = Guid.TryParse(actorId, out var parsedActorId) ? parsedActorId : Guid.Empty;
        await _auditService.AppendAsync(new AuditEntry
        {
            TenantId = tenantId,
            EntityType = "User",
            EntityId = user.Id,
            Action = "TemporaryPasswordReset",
            Changes = JsonSerializer.Serialize(new { PasswordState = "Temporary", MustChangePassword = true, ExpiresAt = user.TemporaryPasswordExpiresAt }),
            OriginalValues = JsonSerializer.Serialize(new { PasswordState = "Unknown" }),
            NewValues = JsonSerializer.Serialize(new { PasswordState = "Temporary", MustChangePassword = true, ExpiresAt = user.TemporaryPasswordExpiresAt }),
            UserId = actorUserId,
            UserName = actorId,
            CreatedBy = actorId
        }, cancellationToken);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<TemporaryPasswordResetDto>.Failure(Error.Conflict("The user changed concurrently. Reload and try again."));
        }

        return Result<TemporaryPasswordResetDto>.Success(new TemporaryPasswordResetDto
        {
            UserId = user.Id,
            TemporaryPassword = temporaryPassword,
            ExpiresAt = user.TemporaryPasswordExpiresAt.Value
        });
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

        var roles = await ResolveRolesAsync(dto.Roles, cancellationToken);
        if (roles is null)
            return Result<UserAdminDto>.Failure(Error.Validation("One or more selected roles are unavailable"));

        var previousRoleIds = user.UserRoles.Select(userRole => userRole.RoleId).ToHashSet();
        var previousRoleNames = user.UserRoles
            .Select(userRole => userRole.Role?.Name ?? string.Empty)
            .Where(name => name.Length > 0)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        var nextRoleIds = roles.Select(role => role.Id).ToHashSet();
        var rolesChanged = !previousRoleIds.SetEquals(nextRoleIds);
        if (rolesChanged && Guid.TryParse(actorId, out var actorGuid) && actorGuid == user.Id)
            return Result<UserAdminDto>.Failure(Error.Forbidden("You cannot change your own role assignments. Ask another administrator to update them."));
        if (rolesChanged && !await CanAssignRolesAsync(roles, actorId, GetCurrentTenantId(), cancellationToken))
            return Result<UserAdminDto>.Failure(Error.Forbidden("You cannot assign a role with permissions you do not have"));

        user.Email = email;
        user.FirstName = firstName;
        user.LastName = lastName;
        user.JobTitle = dto.JobTitle;
        user.Phone = dto.Phone;
        user.IsActive = dto.IsActive;
        user.UpdatedBy = actorId;
        ReplaceRoles(user, roles, actorId);

        if (rolesChanged)
        {
            var actorUserId = Guid.TryParse(actorId, out var parsedActorId) ? parsedActorId : Guid.Empty;
            var actorName = actorUserId == Guid.Empty ? actorId : await _context.Set<User>()
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(actor => actor.Id == actorUserId && actor.TenantId == user.TenantId && !actor.IsDeleted)
                .Select(actor => actor.FirstName + " " + actor.LastName)
                .FirstOrDefaultAsync(cancellationToken) ?? actorId;
            var nextRoles = roles.Select(role => role.Name).OrderBy(name => name, StringComparer.Ordinal).ToArray();
            await _auditService.AppendAsync(new AuditEntry
            {
                TenantId = user.TenantId,
                EntityType = "User",
                EntityId = user.Id,
                Action = "RolesUpdated",
                Changes = JsonSerializer.Serialize(new { Original = previousRoleNames, Current = nextRoles }),
                OriginalValues = JsonSerializer.Serialize(previousRoleNames),
                NewValues = JsonSerializer.Serialize(nextRoles),
                UserId = actorUserId,
                UserName = actorName,
                CreatedBy = actorId
            }, cancellationToken);
        }

        await _context.SaveChangesAsync(cancellationToken);

        var reloaded = await _context.Set<User>()
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == user.Id, cancellationToken);

        return Result<UserAdminDto>.Success(MapToDto(reloaded ?? user));
    }

    private void ReplaceRoles(User user, List<Role> roles, string actorId)
    {
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

    private async Task<List<Role>?> ResolveRolesAsync(List<string> roleNames, CancellationToken cancellationToken)
    {
        if (roleNames is null || roleNames.Count == 0)
            return new List<Role>();

        var names = roleNames.Select(r => r.Trim()).Where(r => !string.IsNullOrEmpty(r)).Distinct().ToList();
        if (names.Count == 0)
            return new List<Role>();

        var roles = await _context.Set<Role>()
            .Include(role => role.Permissions)
            .Where(r => names.Contains(r.Name))
            .ToListAsync(cancellationToken);

        return roles.Count == names.Count ? roles : null;
    }

    private async Task<bool> CanAssignRolesAsync(IReadOnlyCollection<Role> roles, string actorId, Guid tenantId, CancellationToken cancellationToken)
    {
        if (roles.Count == 0)
            return true;
        if (!Guid.TryParse(actorId, out var actorUserId))
            return false;

        var actorPermissions = await _effectivePermissions.GetEffectivePermissionsAsync(actorUserId, tenantId, cancellationToken);
        return roles.SelectMany(role => role.Permissions)
            .Where(permission => permission.TenantId == tenantId && !permission.IsDeleted)
            .All(permission => actorPermissions.Contains($"{permission.Resource}.{permission.Action}"));
    }

    public async Task<Result<IReadOnlyList<UserPermissionAssignmentDto>>> GetUserPermissionsAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var exists = await _context.Set<User>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AnyAsync(user => user.Id == id && user.TenantId == tenantId && !user.IsDeleted, cancellationToken);
        if (!exists)
            return Result<IReadOnlyList<UserPermissionAssignmentDto>>.Failure(Error.NotFound("User not found"));

        return Result<IReadOnlyList<UserPermissionAssignmentDto>>.Success(await BuildPermissionAssignmentsAsync(id, tenantId, cancellationToken));
    }

    public async Task<Result<IReadOnlyList<UserPermissionAssignmentDto>>> UpdateUserPermissionsAsync(Guid id, Guid tenantId, UpdateUserPermissionsDto dto, string actorId, CancellationToken cancellationToken = default)
    {
        if (dto is null || dto.Overrides is null || dto.Overrides.Count > 1000)
            return Result<IReadOnlyList<UserPermissionAssignmentDto>>.Failure(Error.Validation("Permission override list is invalid"));
        if (tenantId == Guid.Empty || !Guid.TryParse(actorId, out var actorUserId))
            return Result<IReadOnlyList<UserPermissionAssignmentDto>>.Failure(Error.Forbidden("A valid authenticated tenant and user are required"));
        if (actorUserId == id)
            return Result<IReadOnlyList<UserPermissionAssignmentDto>>.Failure(Error.Forbidden("You cannot change your own permissions. Ask another administrator to update them."));
        if (dto.Overrides.Any(item => item.Effect is not (PermissionOverrideEffect.Allow or PermissionOverrideEffect.Deny)))
            return Result<IReadOnlyList<UserPermissionAssignmentDto>>.Failure(Error.Validation("Choose Allow or Deny for each direct override; omit inherited permissions"));
        if (dto.Overrides.Select(item => item.PermissionId).Distinct().Count() != dto.Overrides.Count)
            return Result<IReadOnlyList<UserPermissionAssignmentDto>>.Failure(Error.Validation("A permission can only have one direct override"));

        var target = await _context.Set<User>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(user => user.Id == id && user.TenantId == tenantId && !user.IsDeleted, cancellationToken);
        if (target is null)
            return Result<IReadOnlyList<UserPermissionAssignmentDto>>.Failure(Error.NotFound("User not found"));

        var actorPermissions = await _effectivePermissions.GetEffectivePermissionsAsync(actorUserId, tenantId, cancellationToken);
        if (!actorPermissions.Contains("users.write"))
            return Result<IReadOnlyList<UserPermissionAssignmentDto>>.Failure(Error.Forbidden("You do not have permission to manage user access"));

        var catalog = await _context.Set<Permission>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(permission => permission.TenantId == tenantId && !permission.IsDeleted)
            .ToListAsync(cancellationToken);
        var catalogById = catalog.ToDictionary(permission => permission.Id);
        if (dto.Overrides.Any(item => !catalogById.ContainsKey(item.PermissionId)))
            return Result<IReadOnlyList<UserPermissionAssignmentDto>>.Failure(Error.Validation("One or more permissions are not available in this tenant"));
        if (dto.Overrides.Any(item => !actorPermissions.Contains($"{catalogById[item.PermissionId].Resource}.{catalogById[item.PermissionId].Action}")))
            return Result<IReadOnlyList<UserPermissionAssignmentDto>>.Failure(Error.Forbidden("You can only grant or deny permissions within your own effective access"));

        var requested = dto.Overrides.ToDictionary(item => item.PermissionId, item => item.Effect);
        var existingOverrides = await _context.Set<UserPermissionOverride>()
            .IgnoreQueryFilters()
            .Where(item => item.UserId == id && item.TenantId == tenantId)
            .ToListAsync(cancellationToken);
        var original = existingOverrides
            .Where(item => !item.IsDeleted && item.Effect is PermissionOverrideEffect.Allow or PermissionOverrideEffect.Deny)
            .ToDictionary(item => item.PermissionId, item => item.Effect);

        foreach (var item in existingOverrides)
        {
            if (item.IsDeleted && !requested.ContainsKey(item.PermissionId))
                continue;

            var nextEffect = requested.GetValueOrDefault(item.PermissionId, PermissionOverrideEffect.Inherit);
            if (!item.IsDeleted && item.Effect == nextEffect)
                continue;

            item.IsDeleted = false;
            item.Effect = nextEffect;
            item.UpdatedBy = actorId;
            item.UpdatedAt = DateTime.UtcNow;
        }

        foreach (var (permissionId, effect) in requested)
        {
            if (existingOverrides.Any(item => item.PermissionId == permissionId))
                continue;
            _context.Set<UserPermissionOverride>().Add(new UserPermissionOverride
            {
                TenantId = tenantId,
                UserId = id,
                PermissionId = permissionId,
                Effect = effect,
                CreatedBy = actorId
            });
        }

        var normalized = requested
            .Where(pair => pair.Value is PermissionOverrideEffect.Allow or PermissionOverrideEffect.Deny)
            .ToDictionary(pair => pair.Key, pair => pair.Value);
        var changedIds = original.Keys.Union(normalized.Keys)
            .Where(permissionId => original.GetValueOrDefault(permissionId, PermissionOverrideEffect.Inherit)
                                   != normalized.GetValueOrDefault(permissionId, PermissionOverrideEffect.Inherit))
            .ToList();

        if (changedIds.Count == 0)
            return Result<IReadOnlyList<UserPermissionAssignmentDto>>.Success(await BuildPermissionAssignmentsAsync(id, tenantId, cancellationToken));

        var auditChanges = changedIds
            .OrderBy(permissionId => catalogById[permissionId].Resource, StringComparer.Ordinal)
            .ThenBy(permissionId => catalogById[permissionId].Action, StringComparer.Ordinal)
            .Select(permissionId => new
            {
                Permission = $"{catalogById[permissionId].Resource}.{catalogById[permissionId].Action}",
                Previous = original.GetValueOrDefault(permissionId, PermissionOverrideEffect.Inherit).ToString(),
                Current = normalized.GetValueOrDefault(permissionId, PermissionOverrideEffect.Inherit).ToString()
            })
            .ToList();
        var actorName = await _context.Set<User>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(user => user.Id == actorUserId && user.TenantId == tenantId && !user.IsDeleted)
            .Select(user => user.FirstName + " " + user.LastName)
            .FirstOrDefaultAsync(cancellationToken) ?? actorId;

        await _auditService.AppendAsync(new AuditEntry
        {
            TenantId = tenantId,
            EntityType = "User",
            EntityId = id,
            Action = "PermissionsUpdated",
            Changes = JsonSerializer.Serialize(auditChanges),
            OriginalValues = JsonSerializer.Serialize(auditChanges.Select(item => new { item.Permission, Effect = item.Previous })),
            NewValues = JsonSerializer.Serialize(auditChanges.Select(item => new { item.Permission, Effect = item.Current })),
            UserId = actorUserId,
            UserName = actorName,
            CreatedBy = actorId
        }, cancellationToken);

        try
        {
            // Override rows and their audit record are saved together by EF's transaction.
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<IReadOnlyList<UserPermissionAssignmentDto>>.Failure(Error.Conflict("Permissions changed concurrently. Reload the user and try again."));
        }
        catch (DbUpdateException)
        {
            return Result<IReadOnlyList<UserPermissionAssignmentDto>>.Failure(Error.Conflict("Permissions could not be saved because another update was applied. Reload and try again."));
        }

        return Result<IReadOnlyList<UserPermissionAssignmentDto>>.Success(await BuildPermissionAssignmentsAsync(id, tenantId, cancellationToken));
    }

    private async Task<IReadOnlyList<UserPermissionAssignmentDto>> BuildPermissionAssignmentsAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken)
    {
        var inheritedPermissions = await (
                from assignment in _context.Set<UserRole>().IgnoreQueryFilters().AsNoTracking()
                where assignment.UserId == userId && assignment.TenantId == tenantId && !assignment.IsDeleted
                      && assignment.Role.TenantId == tenantId && !assignment.Role.IsDeleted
                from permission in assignment.Role.Permissions
                where permission.TenantId == tenantId && !permission.IsDeleted
                select permission.Resource + "." + permission.Action)
            .Distinct()
            .ToListAsync(cancellationToken);
        var inherited = new HashSet<string>(inheritedPermissions, StringComparer.Ordinal);
        var overrides = await _context.Set<UserPermissionOverride>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(item => item.UserId == userId && item.TenantId == tenantId && !item.IsDeleted
                           && item.Permission.TenantId == tenantId && !item.Permission.IsDeleted)
            .ToDictionaryAsync(item => item.PermissionId, item => item.Effect, cancellationToken);
        var permissions = await _context.Set<Permission>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(permission => permission.TenantId == tenantId && !permission.IsDeleted)
            .OrderBy(permission => permission.Resource)
            .ThenBy(permission => permission.Action)
            .ToListAsync(cancellationToken);

        return permissions.Select(permission =>
        {
            var roleGranted = inherited.Contains($"{permission.Resource}.{permission.Action}");
            var hasDirectOverride = overrides.TryGetValue(permission.Id, out var effect)
                && effect is PermissionOverrideEffect.Allow or PermissionOverrideEffect.Deny;
            var isEffective = hasDirectOverride
                ? effect == PermissionOverrideEffect.Allow
                : roleGranted;
            return new UserPermissionAssignmentDto
            {
                Id = permission.Id,
                Name = permission.Name,
                Resource = permission.Resource,
                Action = permission.Action,
                Description = permission.Description,
                IsRoleGranted = roleGranted,
                OverrideEffect = hasDirectOverride ? effect : null,
                IsEffective = isEffective
            };
        }).ToList();
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
            AuthenticationSource = user.AuthenticationSource,
            MustChangePassword = user.MustChangePassword,
            TemporaryPasswordExpiresAt = user.TemporaryPasswordExpiresAt,
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
