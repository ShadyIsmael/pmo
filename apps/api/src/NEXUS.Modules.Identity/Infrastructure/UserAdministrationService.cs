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

    public async Task<Result<PagedResult<UserAdminDto>>> GetPagedAsync(int page, int pageSize, string? search, CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentIdentity(null, null, out var actorId, out var tenantId))
            return Result<PagedResult<UserAdminDto>>.Failure(Error.Forbidden("A valid authenticated user and tenant are required."));
        var actorPermissions = await _effectivePermissions.GetEffectivePermissionsAsync(actorId, tenantId, cancellationToken);
        if (!actorPermissions.Contains("Users.View"))
            return Result<PagedResult<UserAdminDto>>.Failure(Error.Forbidden("Users.View is required to list tenant users."));

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        search = search?.Trim();
        if (search?.Length > 200)
            search = search[..200];

        var query = _context.Set<User>()
            .IgnoreQueryFilters()
            .Where(user => user.TenantId == tenantId && !user.IsDeleted)
            .Include(u => u.UserRoles.Where(userRole => userRole.TenantId == tenantId && !userRole.IsDeleted
                && userRole.Role.TenantId == tenantId && !userRole.Role.IsDeleted))
                .ThenInclude(ur => ur.Role)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search;
            query = query.Where(u =>
                u.FirstName.Contains(term) ||
                u.LastName.Contains(term) ||
                (u.FirstName + " " + u.LastName).Contains(term) ||
                u.Email.Contains(term));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(u => u.FirstName)
            .ThenBy(u => u.LastName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var dtos = items.Select(MapToDto).ToList();
        return Result<PagedResult<UserAdminDto>>.Success(new PagedResult<UserAdminDto>(dtos, page, pageSize, total));
    }

    public async Task<Result<IReadOnlyList<RoleDto>>> GetRolesAsync(CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentIdentity(null, null, out var actorId, out var tenantId))
            return Result<IReadOnlyList<RoleDto>>.Failure(Error.Forbidden("A valid authenticated user and tenant are required."));
        var actorPermissions = await _effectivePermissions.GetEffectivePermissionsAsync(actorId, tenantId, cancellationToken);
        if (!actorPermissions.Contains("Roles.View"))
            return Result<IReadOnlyList<RoleDto>>.Failure(Error.Forbidden("Roles.View is required to list tenant roles."));

        var roles = await _context.Set<Role>()
            .IgnoreQueryFilters()
            .Where(role => role.TenantId == tenantId && !role.IsDeleted)
            .AsNoTracking()
            .OrderBy(r => r.Category)
            .ThenBy(r => r.Name)
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<RoleDto>>.Success(roles.Select(r => new RoleDto
        {
            Id = r.Id,
            Name = r.Name,
            Description = r.Description,
            Category = r.Category
        }).ToList());
    }

    public async Task<Result<CreatedUserDto>> CreateAsync(CreateUserDto dto, string actorId, CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentIdentity(actorId, null, out var actorUserId, out var tenantId))
            return Result<CreatedUserDto>.Failure(Error.Forbidden("A valid authenticated user and tenant are required."));
        var actorPermissions = await _effectivePermissions.GetEffectivePermissionsAsync(actorUserId, tenantId, cancellationToken);
        if (!actorPermissions.Contains("Users.ManageAccounts"))
            return Result<CreatedUserDto>.Failure(Error.Forbidden("Users.ManageAccounts is required to create a user account."));
        if (!dto.IsActive && !actorPermissions.Contains("Users.SetStatus"))
            return Result<CreatedUserDto>.Failure(Error.Forbidden("Users.SetStatus is required to create an inactive user account."));
        if (dto.Roles?.Any() == true && !actorPermissions.Contains("Users.AssignRoles"))
            return Result<CreatedUserDto>.Failure(Error.Forbidden("Users.AssignRoles is required to assign roles while creating a user."));

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

        var roles = await ResolveRolesAsync(dto.Roles, tenantId, cancellationToken);
        if (roles is null)
            return Result<CreatedUserDto>.Failure(Error.Validation("One or more selected roles are unavailable"));
        if (!await CanAssignRolesAsync(roles, actorUserId, tenantId, targetUserId: null, cancellationToken))
            return Result<CreatedUserDto>.Failure(Error.Forbidden("You cannot assign a role with permissions you do not have"));

        var now = DateTime.UtcNow;
        var temporaryPassword = TemporaryPasswordGenerator.Generate();

        var user = new User
        {
            TenantId = tenantId,
            Email = email,
            AuthenticationSource = AuthenticationSource.Local,
            PasswordHash = string.Empty,
            MustChangePassword = true,
            TemporaryPasswordExpiresAt = now.AddHours(_passwordPolicy.TemporaryPasswordExpiryHours),
            PasswordChangedAt = now,
            FirstName = firstName,
            LastName = lastName,
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
        if (!TryGetCurrentIdentity(actorId, null, out var actorUserId, out var tenantId))
            return Result<TemporaryPasswordResetDto>.Failure(Error.Forbidden("A valid authenticated user and tenant are required."));
        var actorPermissions = await _effectivePermissions.GetEffectivePermissionsAsync(actorUserId, tenantId, cancellationToken);
        if (!actorPermissions.Contains("Users.ResetPassword"))
            return Result<TemporaryPasswordResetDto>.Failure(Error.Forbidden("Users.ResetPassword is required."));
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
        if (!TryGetCurrentIdentity(actorId, null, out var actorUserId, out var tenantId))
            return Result<UserAdminDto>.Failure(Error.Forbidden("A valid authenticated user and tenant are required."));
        var actorPermissions = await _effectivePermissions.GetEffectivePermissionsAsync(actorUserId, tenantId, cancellationToken);
        var canManageAccounts = actorPermissions.Contains("Users.ManageAccounts");
        var canAssignRoles = actorPermissions.Contains("Users.AssignRoles");
        var canSetUserStatus = actorPermissions.Contains("Users.SetStatus");
        if (!canManageAccounts && !canAssignRoles && !canSetUserStatus)
            return Result<UserAdminDto>.Failure(Error.Forbidden("A matching user account, role assignment, or status permission is required."));

        await using var tenantAdminMutation = await TenantAdminMutationGuard.AcquireAsync(_context, tenantId, cancellationToken);
        var hadEffectiveTenantAdmin = await tenantAdminMutation.HasEffectiveTenantAdminAsync(_effectivePermissions, tenantId, cancellationToken);

        var user = await _context.Set<User>()
            .IgnoreQueryFilters()
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == id && u.TenantId == tenantId && !u.IsDeleted, cancellationToken);

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

        var roles = await ResolveRolesAsync(dto.Roles, tenantId, cancellationToken);
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
        if (rolesChanged && !actorPermissions.Contains("Users.AssignRoles"))
            return Result<UserAdminDto>.Failure(Error.Forbidden("Users.AssignRoles is required to change user role assignments."));
        var statusChanged = user.IsActive != dto.IsActive;
        if (statusChanged && !canSetUserStatus)
            return Result<UserAdminDto>.Failure(Error.Forbidden("Users.SetStatus is required to activate or deactivate a user account."));
        if (statusChanged && actorUserId == user.Id)
            return Result<UserAdminDto>.Failure(Error.Forbidden("You cannot change the status of your own account."));
        var accountFieldsChanged = !string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(user.FirstName, firstName, StringComparison.Ordinal)
            || !string.Equals(user.LastName, lastName, StringComparison.Ordinal);
        if (accountFieldsChanged && !canManageAccounts)
            return Result<UserAdminDto>.Failure(Error.Forbidden("Users.ManageAccounts is required to change account profile details."));
        if (rolesChanged && actorUserId == user.Id)
            return Result<UserAdminDto>.Failure(Error.Forbidden("You cannot change your own role assignments. Ask another administrator to update them."));
        if (rolesChanged && !await CanAssignRolesAsync(roles, actorUserId, tenantId, id, cancellationToken))
            return Result<UserAdminDto>.Failure(Error.Forbidden("You cannot assign a role with permissions you do not have"));

        if (canManageAccounts)
        {
            user.Email = email;
            user.FirstName = firstName;
            user.LastName = lastName;
        }
        if (canSetUserStatus)
            user.IsActive = dto.IsActive;
        user.UpdatedBy = actorId;
        if (rolesChanged)
            ReplaceRoles(user, roles, actorId);

        if (rolesChanged)
        {
            var actorName = await _context.Set<User>()
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

        if (statusChanged)
        {
            await _auditService.AppendAsync(new AuditEntry
            {
                TenantId = user.TenantId,
                EntityType = "User",
                EntityId = user.Id,
                Action = dto.IsActive ? "UserActivated" : "UserDeactivated",
                Changes = JsonSerializer.Serialize(new { Original = !dto.IsActive, Current = dto.IsActive }),
                OriginalValues = JsonSerializer.Serialize(!dto.IsActive),
                NewValues = JsonSerializer.Serialize(dto.IsActive),
                UserId = actorUserId,
                UserName = actorId,
                CreatedBy = actorId
            }, cancellationToken);
        }

        await _context.SaveChangesAsync(cancellationToken);
        if (hadEffectiveTenantAdmin
            && !await tenantAdminMutation.HasEffectiveTenantAdminAsync(_effectivePermissions, tenantId, cancellationToken))
        {
            await tenantAdminMutation.RollbackAsync(cancellationToken);
            _context.ChangeTracker.Clear();
            await _auditService.AppendAsync(new AuditEntry
            {
                TenantId = tenantId,
                EntityType = "User",
                EntityId = id,
                Action = "TenantAdminMutationRejected",
                Changes = JsonSerializer.Serialize(new { Rejected = true, Reason = "Last effective tenant administrator would be removed" }),
                NewValues = JsonSerializer.Serialize(new { Rejected = true, dto.IsActive, dto.Roles }),
                UserId = actorUserId,
                UserName = actorId,
                CreatedBy = actorId
            }, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            return Result<UserAdminDto>.Failure(Error.Conflict("This change would remove the last effective tenant administrator."));
        }
        await tenantAdminMutation.CommitAsync(cancellationToken);

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

    private async Task<List<Role>?> ResolveRolesAsync(List<string>? roleNames, Guid tenantId, CancellationToken cancellationToken)
    {
        if (roleNames is null || roleNames.Count == 0)
            return new List<Role>();

        var names = roleNames.Select(r => r.Trim()).Where(r => !string.IsNullOrEmpty(r)).Distinct().ToList();
        if (names.Count == 0)
            return new List<Role>();

        var roles = await _context.Set<Role>()
            .IgnoreQueryFilters()
            .Include(role => role.Permissions)
            .Where(r => names.Contains(r.Name) && r.TenantId == tenantId && !r.IsDeleted)
            .ToListAsync(cancellationToken);

        return roles.Count == names.Count ? roles : null;
    }

    private async Task<bool> CanAssignRolesAsync(
        IReadOnlyCollection<Role> roles,
        Guid actorUserId,
        Guid tenantId,
        Guid? targetUserId,
        CancellationToken cancellationToken)
    {
        if (roles.Count == 0)
            return true;
        if (actorUserId == Guid.Empty || tenantId == Guid.Empty)
            return false;

        var actorPermissions = await _effectivePermissions.GetEffectivePermissionsAsync(actorUserId, tenantId, cancellationToken);
        var bootstrapDelegableKeys = await BootstrapPermissionDelegationPolicy.GetDelegableKeysAsync(
            _context, actorUserId, tenantId, actorPermissions, cancellationToken);
        if (roles.Any(role => role.TenantId != tenantId || role.IsDeleted))
            return false;

        foreach (var permission in roles.SelectMany(role => role.Permissions))
        {
            if (permission.TenantId != tenantId || permission.IsDeleted)
                return false;

            var key = $"{permission.Resource}.{permission.Action}";
            if (actorPermissions.Contains(key))
                continue;
            if (!bootstrapDelegableKeys.Contains(key))
                return false;

            // Bootstrap delegation creates an exception for a different user. It
            // cannot be used to amplify the acting administrator's own access.
            if (targetUserId == actorUserId)
                return false;
        }

        return true;
    }

    public async Task<Result<IReadOnlyList<UserPermissionAssignmentDto>>> GetUserPermissionsAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentIdentity(null, tenantId, out var actorUserId, out var currentTenantId))
            return Result<IReadOnlyList<UserPermissionAssignmentDto>>.Failure(Error.Forbidden("A valid authenticated user and matching tenant are required."));
        var actorPermissions = await _effectivePermissions.GetEffectivePermissionsAsync(actorUserId, currentTenantId, cancellationToken);
        if (!actorPermissions.Contains("UserPermissions.Manage"))
            return Result<IReadOnlyList<UserPermissionAssignmentDto>>.Failure(Error.Forbidden("UserPermissions.Manage is required to view direct permission overrides."));
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
        if (!TryGetCurrentIdentity(actorId, tenantId, out var actorUserId, out var currentTenantId))
            return Result<IReadOnlyList<UserPermissionAssignmentDto>>.Failure(Error.Forbidden("A valid authenticated user and matching tenant are required."));
        var actorPermissions = await _effectivePermissions.GetEffectivePermissionsAsync(actorUserId, currentTenantId, cancellationToken);
        if (!actorPermissions.Contains("UserPermissions.Manage"))
            return Result<IReadOnlyList<UserPermissionAssignmentDto>>.Failure(Error.Forbidden("UserPermissions.Manage is required to change direct permission overrides."));
        if (dto is null || dto.Overrides is null || dto.Overrides.Count > 1000)
            return Result<IReadOnlyList<UserPermissionAssignmentDto>>.Failure(Error.Validation("Permission override list is invalid"));
        if (actorUserId == id)
            return Result<IReadOnlyList<UserPermissionAssignmentDto>>.Failure(Error.Forbidden("You cannot change your own permissions. Ask another administrator to update them."));
        if (dto.Overrides.Any(item => item.Effect is not (PermissionOverrideEffect.Allow or PermissionOverrideEffect.Deny)))
            return Result<IReadOnlyList<UserPermissionAssignmentDto>>.Failure(Error.Validation("Choose Allow or Deny for each direct override; omit inherited permissions"));
        if (dto.Overrides.Select(item => item.PermissionId).Distinct().Count() != dto.Overrides.Count)
            return Result<IReadOnlyList<UserPermissionAssignmentDto>>.Failure(Error.Validation("A permission can only have one direct override"));

        await using var tenantAdminMutation = await TenantAdminMutationGuard.AcquireAsync(_context, tenantId, cancellationToken);
        var hadEffectiveTenantAdmin = await tenantAdminMutation.HasEffectiveTenantAdminAsync(_effectivePermissions, tenantId, cancellationToken);

        var target = await _context.Set<User>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(user => user.Id == id && user.TenantId == tenantId && !user.IsDeleted, cancellationToken);
        if (target is null)
            return Result<IReadOnlyList<UserPermissionAssignmentDto>>.Failure(Error.NotFound("User not found"));

        var targetHasProtectedAdminRole = await _context.Set<UserRole>()
            .IgnoreQueryFilters()
            .AnyAsync(assignment => assignment.UserId == id && assignment.TenantId == tenantId && !assignment.IsDeleted
                && assignment.Role.TenantId == tenantId && !assignment.Role.IsDeleted
                && assignment.Role.IsSystem
                && assignment.Role.SystemRoleKey == Role.TenantAdminSystemRoleKey,
                cancellationToken);

        var catalog = await _context.Set<Permission>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(permission => permission.TenantId == tenantId)
            .ToListAsync(cancellationToken);
        var catalogById = catalog.ToDictionary(permission => permission.Id);
        if (dto.Overrides.Any(item => !catalogById.ContainsKey(item.PermissionId)))
            return Result<IReadOnlyList<UserPermissionAssignmentDto>>.Failure(Error.Validation("One or more permissions are not available in this tenant"));
        var existingOverrides = await _context.Set<UserPermissionOverride>()
            .IgnoreQueryFilters()
            .Where(item => item.UserId == id && item.TenantId == tenantId)
            .ToListAsync(cancellationToken);
        var original = existingOverrides
            .Where(item => !item.IsDeleted && item.Effect is PermissionOverrideEffect.Allow or PermissionOverrideEffect.Deny)
            .ToDictionary(item => item.PermissionId, item => item.Effect);
        var requested = dto.Overrides.ToDictionary(item => item.PermissionId, item => item.Effect);
        var inactiveOriginalIds = original.Keys
            .Where(permissionId => !catalogById.TryGetValue(permissionId, out var permission)
                || permission.IsDeleted
                || !PermissionCatalog.ByKey.TryGetValue(
                    $"{permission.Resource}.{permission.Action}", out var definition)
                || !definition.IsActive
                || !definition.IsAssignable)
            .ToHashSet();

        foreach (var (permissionId, effect) in requested)
        {
            var permission = catalogById[permissionId];
            var key = $"{permission.Resource}.{permission.Action}";
            var isAssignable = !permission.IsDeleted && PermissionCatalog.ByKey.TryGetValue(key, out var definition)
                && definition.IsActive && definition.IsAssignable;
            if (!isAssignable)
            {
                return Result<IReadOnlyList<UserPermissionAssignmentDto>>.Failure(Error.Validation(
                    $"Permission {key} is inactive or uncatalogued and cannot receive a direct override."));
            }

            if (!actorPermissions.Contains(key))
                return Result<IReadOnlyList<UserPermissionAssignmentDto>>.Failure(Error.Forbidden("You can only grant or deny permissions within your own effective access"));

            if (targetHasProtectedAdminRole && effect == PermissionOverrideEffect.Deny
                && ProtectedTenantAdminPolicy.MandatoryPermissionKeys.Contains(key))
                return Result<IReadOnlyList<UserPermissionAssignmentDto>>.Failure(Error.Conflict(
                    $"Protected tenant Admin permission {key} cannot be denied."));
        }

        foreach (var item in existingOverrides)
        {
            // Retire stale overrides when this user's permission set is updated. No
            // legacy or inactive override is carried forward by ordinary edits.
            if (!item.IsDeleted && inactiveOriginalIds.Contains(item.PermissionId))
            {
                item.IsDeleted = true;
                item.Effect = PermissionOverrideEffect.Inherit;
                item.UpdatedBy = actorId;
                item.UpdatedAt = DateTime.UtcNow;
                continue;
            }
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

        var normalized = original
            .Where(pair => requested.ContainsKey(pair.Key) && !inactiveOriginalIds.Contains(pair.Key))
            .ToDictionary(pair => pair.Key, pair => pair.Value);
        foreach (var (permissionId, effect) in requested)
            normalized[permissionId] = effect;
        var changedIds = original.Keys.Union(normalized.Keys)
            .Where(permissionId => original.GetValueOrDefault(permissionId, PermissionOverrideEffect.Inherit)
                                   != normalized.GetValueOrDefault(permissionId, PermissionOverrideEffect.Inherit))
            .ToList();

        if (changedIds.Count == 0)
        {
            await tenantAdminMutation.CommitAsync(cancellationToken);
            return Result<IReadOnlyList<UserPermissionAssignmentDto>>.Success(await BuildPermissionAssignmentsAsync(id, tenantId, cancellationToken));
        }

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
            if (hadEffectiveTenantAdmin
                && !await tenantAdminMutation.HasEffectiveTenantAdminAsync(_effectivePermissions, tenantId, cancellationToken))
            {
                await tenantAdminMutation.RollbackAsync(cancellationToken);
                _context.ChangeTracker.Clear();
                await _auditService.AppendAsync(new AuditEntry
                {
                    TenantId = tenantId,
                    EntityType = "UserPermissionOverride",
                    EntityId = id,
                    Action = "TenantAdminMutationRejected",
                    Changes = JsonSerializer.Serialize(new { Rejected = true, Reason = "Last effective tenant administrator would be removed" }),
                    NewValues = JsonSerializer.Serialize(new { Rejected = true, PermissionIds = dto.Overrides.Select(item => item.PermissionId).ToArray() }),
                    UserId = actorUserId,
                    UserName = actorId,
                    CreatedBy = actorId
                }, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
                return Result<IReadOnlyList<UserPermissionAssignmentDto>>.Failure(Error.Conflict("This change would remove the last effective tenant administrator."));
            }
            await tenantAdminMutation.CommitAsync(cancellationToken);
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

        var activePermissions = permissions
            .Where(permission => !permission.IsDeleted
                && PermissionCatalog.ByKey.TryGetValue($"{permission.Resource}.{permission.Action}", out var definition)
                && definition.IsActive)
            .ToArray();
        var hasProtectedAdminRole = await _context.Set<UserRole>()
            .IgnoreQueryFilters()
            .AnyAsync(assignment => assignment.UserId == userId && assignment.TenantId == tenantId && !assignment.IsDeleted
                && assignment.Role.TenantId == tenantId && !assignment.Role.IsDeleted
                && assignment.Role.IsSystem
                && assignment.Role.SystemRoleKey == Role.TenantAdminSystemRoleKey,
                cancellationToken);
        var effectivePermissions = await _effectivePermissions.GetEffectivePermissionsAsync(userId, tenantId, cancellationToken);

        return activePermissions.Select(permission =>
        {
            var key = $"{permission.Resource}.{permission.Action}";
            var roleGranted = inherited.Contains(key);
            var hasDirectOverride = overrides.TryGetValue(permission.Id, out var effect)
                && effect is PermissionOverrideEffect.Allow or PermissionOverrideEffect.Deny;
            var isEffective = effectivePermissions.Contains(key);
            PermissionCatalog.ByKey.TryGetValue(key, out var catalogEntry);
            return new UserPermissionAssignmentDto
            {
                Id = permission.Id,
                Name = permission.Name,
                Resource = permission.Resource,
                Action = permission.Action,
                Description = permission.Description,
                ScreenNameEn = catalogEntry?.ScreenNameEn ?? permission.Resource,
                ScreenNameAr = catalogEntry?.ScreenNameAr ?? permission.Resource,
                LabelEn = catalogEntry?.LabelEn ?? permission.Action,
                LabelAr = catalogEntry?.LabelAr ?? permission.Action,
                IsActive = catalogEntry?.IsActive == true,
                IsAssignable = catalogEntry?.IsAssignable == true,
                IsRoleGranted = roleGranted,
                OverrideEffect = hasDirectOverride ? effect : null,
                IsEffective = isEffective,
                IsProtectedAdminBaseline = hasProtectedAdminRole && ProtectedTenantAdminPolicy.MandatoryPermissionKeys.Contains(key)
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
            IsActive = user.IsActive,
            AuthenticationSource = user.AuthenticationSource,
            MustChangePassword = user.MustChangePassword,
            TemporaryPasswordExpiresAt = user.TemporaryPasswordExpiresAt,
            Roles = user.UserRoles.Select(ur => ur.Role?.Name ?? string.Empty).OrderBy(name => name).ToList(),
            CreatedAt = user.CreatedAt,
            LastLoginAt = user.LastLoginAt
        };
    }

    private bool TryGetCurrentIdentity(string? expectedActorId, Guid? expectedTenantId, out Guid actorId, out Guid tenantId)
    {
        actorId = Guid.Empty;
        tenantId = Guid.Empty;
        var principal = _httpContextAccessor.HttpContext?.User;
        if (principal?.Identity?.IsAuthenticated != true
            || !Guid.TryParse(principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out actorId)
            || actorId == Guid.Empty
            || !Guid.TryParse(principal.FindFirst("tenantId")?.Value, out tenantId)
            || tenantId == Guid.Empty)
        {
            return false;
        }

        if (expectedTenantId.HasValue && expectedTenantId.Value != tenantId)
            return false;
        if (expectedActorId is not null
            && (!Guid.TryParse(expectedActorId, out var suppliedActorId) || suppliedActorId != actorId))
        {
            return false;
        }

        return true;
    }
}
