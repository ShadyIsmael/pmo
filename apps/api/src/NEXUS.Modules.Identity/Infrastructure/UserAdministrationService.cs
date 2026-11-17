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
    private readonly IRoleAssignmentAuthorizationService _roleAssignmentAuthorization;
    private readonly IUserOrganizationDirectory _organizationDirectory;
    private readonly IUserOrganizationMembershipIntegrity _organizationIntegrity;
    private readonly IPermissionProtectionPolicy _permissionProtection;

    public UserAdministrationService(
        DbContext context,
        IPasswordHasher<User> passwordHasher,
        IHttpContextAccessor httpContextAccessor,
        IUserEffectivePermissionService effectivePermissions,
        IAuditService auditService,
        IRoleAssignmentAuthorizationService roleAssignmentAuthorization,
        IOptions<LocalPasswordPolicyOptions> passwordPolicy,
        IUserOrganizationDirectory organizationDirectory,
        IUserOrganizationMembershipIntegrity organizationIntegrity,
        IPermissionProtectionPolicy? permissionProtection = null)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _httpContextAccessor = httpContextAccessor;
        _effectivePermissions = effectivePermissions;
        _auditService = auditService;
        _roleAssignmentAuthorization = roleAssignmentAuthorization;
        _passwordPolicy = passwordPolicy.Value;
        _organizationDirectory = organizationDirectory;
        _organizationIntegrity = organizationIntegrity;
        _permissionProtection = permissionProtection ?? PermissionProtectionPolicy.Empty;
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
                && userRole.Role.TenantId == tenantId))
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

        var organizationIds = items.SelectMany(user => user.UserRoles
                .Where(userRole => userRole.Role?.OrganizationId is not null)
                .Select(userRole => userRole.Role!.OrganizationId!.Value))
            .Distinct()
            .ToArray();
        var organizationNames = await LoadOrganizationNamesAsync(organizationIds, tenantId, cancellationToken);
        var canRemoveByOrganization = await LoadRoleRemovalPermissionsAsync(
            actorId, tenantId, organizationIds, cancellationToken);
        var canRemoveTenantRoles = await CanRemoveTenantRolesAsync(actorId, tenantId, cancellationToken);
        var dtos = items.Select(user => MapToDto(user, organizationNames, canRemoveByOrganization, canRemoveTenantRoles, actorId)).ToList();
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

        // Keep the authoritative permission and Role checks in the same tenant
        // mutation transaction as persistence. Role and permission mutations use
        // this guard too, so none can invalidate the assignment after validation.
        await using var tenantAdminMutation = await TenantAdminMutationGuard.AcquireAsync(_context, tenantId, cancellationToken);
        var actorPermissions = await _effectivePermissions.GetEffectivePermissionsAsync(actorUserId, tenantId, cancellationToken);
        if (!actorPermissions.Contains("Users.ManageAccounts"))
            return Result<CreatedUserDto>.Failure(Error.Forbidden("Users.ManageAccounts is required to create a user account."));
        if (!dto.IsActive && !actorPermissions.Contains("Users.SetStatus"))
            return Result<CreatedUserDto>.Failure(Error.Forbidden("Users.SetStatus is required to create an inactive user account."));

        var requestedRoleIds = NormalizeRoleIds(dto.RoleIds);
        if (requestedRoleIds.Error is not null)
            return Result<CreatedUserDto>.Failure(requestedRoleIds.Error);
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

        var roleAuthorization = await _roleAssignmentAuthorization.ResolveAndAuthorizeAsync(
            requestedRoleIds.Ids, actorUserId, tenantId, targetUserId: null, cancellationToken);
        if (roleAuthorization.IsFailure)
            return Result<CreatedUserDto>.Failure(roleAuthorization.Error!);
        var roles = roleAuthorization.Value!.ToList();
        var organizationIds = roles.Where(role => role.OrganizationId.HasValue)
            .Select(role => role.OrganizationId!.Value).Distinct().ToArray();
        var organizationNames = await LoadOrganizationNamesAsync(organizationIds, tenantId, cancellationToken);

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
        var roleAudit = MapRoleAuditSnapshots(roles, organizationNames);
        var organizationAudit = MapDerivedOrganizationSnapshots(roles, organizationNames);
        await _auditService.AppendAsync(new AuditEntry
        {
            TenantId = user.TenantId,
            EntityType = "User",
            EntityId = user.Id,
            Action = "LocalUserCreated",
            Changes = JsonSerializer.Serialize(new { AuthenticationSource = user.AuthenticationSource.ToString(), PasswordState = "Temporary", Roles = roleAudit, Organizations = organizationAudit }),
            NewValues = JsonSerializer.Serialize(new { user.Email, AuthenticationSource = user.AuthenticationSource.ToString(), PasswordState = "Temporary", Roles = roleAudit, Organizations = organizationAudit }),
            UserId = actorUserId,
            UserName = actorId,
            CreatedBy = actorId
        }, cancellationToken);
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            await tenantAdminMutation.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await tenantAdminMutation.RollbackAsync(cancellationToken);
            _context.ChangeTracker.Clear();
            return Result<CreatedUserDto>.Failure(Error.Conflict(
                "The user or Role assignments changed concurrently. Reload the form and try again."));
        }
        catch (DbUpdateException)
        {
            await tenantAdminMutation.RollbackAsync(cancellationToken);
            _context.ChangeTracker.Clear();
            return Result<CreatedUserDto>.Failure(Error.Conflict(
                "The user could not be created because the email or Role state changed concurrently. Reload and try again."));
        }

        var createdUser = await _context.Set<User>()
            .IgnoreQueryFilters()
            .Include(created => created.UserRoles.Where(userRole => userRole.TenantId == tenantId && !userRole.IsDeleted
                && userRole.Role.TenantId == tenantId))
                .ThenInclude(userRole => userRole.Role)
            .AsNoTracking()
            .FirstAsync(created => created.Id == user.Id && created.TenantId == tenantId, cancellationToken);
        var createdOrganizationIds = createdUser.UserRoles
            .Where(userRole => userRole.Role?.OrganizationId is not null)
            .Select(userRole => userRole.Role!.OrganizationId!.Value)
            .Distinct()
            .ToArray();
        var createdOrganizationNames = await LoadOrganizationNamesAsync(createdOrganizationIds, tenantId, cancellationToken);
        var canRemoveByOrganization = await LoadRoleRemovalPermissionsAsync(actorUserId, tenantId,
            createdOrganizationIds, cancellationToken);
        var canRemoveTenantRoles = await CanRemoveTenantRolesAsync(actorUserId, tenantId, cancellationToken);
        return Result<CreatedUserDto>.Success(new CreatedUserDto
        {
            User = MapToDto(createdUser, createdOrganizationNames, canRemoveByOrganization, canRemoveTenantRoles, actorUserId),
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

        await using var tenantAdminMutation = await TenantAdminMutationGuard.AcquireAsync(_context, tenantId, cancellationToken);
        var actorPermissions = await _effectivePermissions.GetEffectivePermissionsAsync(actorUserId, tenantId, cancellationToken);
        var canManageAccounts = actorPermissions.Contains("Users.ManageAccounts");
        var canAssignRoles = actorPermissions.Contains("Users.AssignRoles");
        var canSetUserStatus = actorPermissions.Contains("Users.SetStatus");
        if (!canManageAccounts && !canAssignRoles && !canSetUserStatus)
            return Result<UserAdminDto>.Failure(Error.Forbidden("A matching user account, role assignment, or status permission is required."));

        var hadActiveTenantAdmin = await tenantAdminMutation.HasActiveTenantAdminAsync(tenantId, cancellationToken);

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

        var currentAssignments = user.UserRoles
            .Where(userRole => userRole.TenantId == tenantId && !userRole.IsDeleted)
            .ToList();
        if (currentAssignments.Any(userRole => userRole.Role is null || userRole.Role.TenantId != tenantId))
            return Result<UserAdminDto>.Failure(new Error(
                RoleAssignmentContract.OutOfScope,
                "The user's existing Role assignments could not be verified in this Tenant. No changes were saved.",
                409,
                "roleIds"));

        var currentRoles = currentAssignments.Select(userRole => userRole.Role!).ToList();
        var previousRoleIds = currentAssignments.Select(userRole => userRole.RoleId).ToHashSet();
        var normalizedRoleIds = NormalizeRoleIds(dto.RoleIds ?? previousRoleIds.ToList());
        if (normalizedRoleIds.Error is not null)
            return Result<UserAdminDto>.Failure(normalizedRoleIds.Error);

        var nextRoleIds = normalizedRoleIds.Ids.ToHashSet();
        var rolesChanged = !previousRoleIds.SetEquals(nextRoleIds);
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

        var rolesToRemove = rolesChanged
            ? currentRoles.Where(role => !nextRoleIds.Contains(role.Id)).ToList()
            : new List<Role>();
        var rolesToAddIds = rolesChanged
            ? nextRoleIds.Except(previousRoleIds).ToArray()
            : Array.Empty<Guid>();
        var rolesToAdd = new List<Role>();
        if (rolesToAddIds.Length > 0)
        {
            var roleAuthorization = await _roleAssignmentAuthorization.ResolveAndAuthorizeAsync(
                rolesToAddIds, actorUserId, tenantId, id, cancellationToken);
            if (roleAuthorization.IsFailure)
                return Result<UserAdminDto>.Failure(roleAuthorization.Error!);
            rolesToAdd = roleAuthorization.Value!.ToList();
        }

        if (rolesToRemove.Count > 0)
        {
            var removalError = await ValidateRoleRemovalsAsync(rolesToRemove, actorUserId, tenantId, cancellationToken);
            if (removalError is not null)
                return Result<UserAdminDto>.Failure(removalError);
        }

        var nextRoles = currentRoles.Where(role => nextRoleIds.Contains(role.Id))
            .Concat(rolesToAdd)
            .DistinctBy(role => role.Id)
            .ToList();
        if (nextRoles.Count != nextRoleIds.Count)
            return Result<UserAdminDto>.Failure(new Error(
                RoleAssignmentContract.InvalidRoleIds,
                "One or more requested Role IDs could not be resolved. Existing assignments were preserved.",
                400,
                "roleIds"));

        var roleOrganizationNames = new Dictionary<Guid, string>();
        if (rolesChanged)
        {
            var organizationIds = currentRoles.Concat(rolesToAdd)
                .Where(role => role.OrganizationId.HasValue)
                .Select(role => role.OrganizationId!.Value)
                .Distinct()
                .ToArray();
            roleOrganizationNames = new Dictionary<Guid, string>(
                await LoadOrganizationNamesAsync(organizationIds, tenantId, cancellationToken));

            var previousOrganizationIds = currentRoles
                .Where(role => role.OrganizationId.HasValue)
                .Select(role => role.OrganizationId!.Value)
                .ToHashSet();
            var nextOrganizationIds = nextRoles
                .Where(role => role.OrganizationId.HasValue)
                .Select(role => role.OrganizationId!.Value)
                .ToHashSet();
            var organizationsLosingMembership = previousOrganizationIds.Except(nextOrganizationIds).ToArray();
            var blockedOrganizationIds = await _organizationIntegrity.GetOrganizationsBlockingRemovalAsync(
                tenantId, id, organizationsLosingMembership, cancellationToken);
            if (blockedOrganizationIds.Count > 0)
            {
                var blockedNames = blockedOrganizationIds
                    .Where(roleOrganizationNames.ContainsKey)
                    .Select(organizationId => roleOrganizationNames[organizationId])
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .ToArray();
                var blockedSummary = blockedNames.Length > 0
                    ? string.Join(", ", blockedNames)
                    : "one or more Organizations";
                return Result<UserAdminDto>.Failure(new Error(
                    RoleAssignmentContract.MembershipDependencyConflict,
                    $"Removing the user's last Role in {blockedSummary} would remove Organization membership required by protected settings. Keep or assign another Role in each affected Organization.",
                    409,
                    "roleIds",
                    new Dictionary<string, object?>
                    {
                        ["organizationIds"] = blockedOrganizationIds,
                        ["organizationNames"] = blockedNames
                    }));
            }
        }

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
            ReplaceRoles(user, currentAssignments, nextRoles, actorId);

        if (rolesChanged)
        {
            var actorName = await _context.Set<User>()
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(actor => actor.Id == actorUserId && actor.TenantId == user.TenantId && !actor.IsDeleted)
                .Select(actor => actor.FirstName + " " + actor.LastName)
                .FirstOrDefaultAsync(cancellationToken) ?? actorId;
            var originalRoleAudit = MapRoleAuditSnapshots(currentRoles, roleOrganizationNames);
            var nextRoleAudit = MapRoleAuditSnapshots(nextRoles, roleOrganizationNames);
            var addedRoleAudit = MapRoleAuditSnapshots(rolesToAdd, roleOrganizationNames);
            var removedRoleAudit = MapRoleAuditSnapshots(rolesToRemove, roleOrganizationNames);
            var derivedOrganizations = MapDerivedOrganizationSnapshots(nextRoles, roleOrganizationNames);
            await _auditService.AppendAsync(new AuditEntry
            {
                TenantId = user.TenantId,
                EntityType = "User",
                EntityId = user.Id,
                Action = "RolesUpdated",
                Changes = JsonSerializer.Serialize(new { Added = addedRoleAudit, Removed = removedRoleAudit, Original = originalRoleAudit, Current = nextRoleAudit, Organizations = derivedOrganizations }),
                OriginalValues = JsonSerializer.Serialize(originalRoleAudit),
                NewValues = JsonSerializer.Serialize(new { Roles = nextRoleAudit, Organizations = derivedOrganizations }),
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

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await tenantAdminMutation.RollbackAsync(cancellationToken);
            _context.ChangeTracker.Clear();
            return Result<UserAdminDto>.Failure(Error.Conflict(
                "The user or Role assignments changed concurrently. Reload the user and try again."));
        }
        if (hadActiveTenantAdmin
            && !await tenantAdminMutation.HasActiveTenantAdminAsync(tenantId, cancellationToken))
        {
            await tenantAdminMutation.RollbackAsync(cancellationToken);
            _context.ChangeTracker.Clear();
            await _auditService.AppendAsync(new AuditEntry
            {
                TenantId = tenantId,
                EntityType = "User",
                EntityId = id,
                Action = "TenantAdminMutationRejected",
                Changes = JsonSerializer.Serialize(new { Rejected = true, Reason = "Last active Tenant Admin assignment would be removed" }),
                NewValues = JsonSerializer.Serialize(new { Rejected = true, dto.IsActive, dto.RoleIds }),
                UserId = actorUserId,
                UserName = actorId,
                CreatedBy = actorId
            }, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            return Result<UserAdminDto>.Failure(Error.Conflict("This change would remove the last active Tenant Admin assignment."));
        }
        await tenantAdminMutation.CommitAsync(cancellationToken);

        var reloaded = await _context.Set<User>()
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == user.Id, cancellationToken);

        var resultUser = reloaded ?? user;
        var organizationIdsForResult = resultUser.UserRoles
            .Where(userRole => userRole.Role?.OrganizationId is not null)
            .Select(userRole => userRole.Role!.OrganizationId!.Value)
            .Distinct()
            .ToArray();
        var resultOrganizationNames = await LoadOrganizationNamesAsync(organizationIdsForResult, tenantId, cancellationToken);
        var resultCanRemoveByOrganization = await LoadRoleRemovalPermissionsAsync(actorUserId, tenantId,
            organizationIdsForResult, cancellationToken);
        var canRemoveTenantRoles = await CanRemoveTenantRolesAsync(actorUserId, tenantId, cancellationToken);
        return Result<UserAdminDto>.Success(MapToDto(resultUser, resultOrganizationNames,
            resultCanRemoveByOrganization, canRemoveTenantRoles, actorUserId));
    }

    private void ReplaceRoles(
        User user,
        IReadOnlyCollection<UserRole> currentAssignments,
        IReadOnlyCollection<Role> roles,
        string actorId)
    {
        var targetRoleIds = roles.Select(r => r.Id).ToHashSet();

        // Remove only the active, same-Tenant assignments that were loaded and
        // authorized above. Never clear unrelated or unverifiable associations.
        var toRemove = currentAssignments.Where(ur => !targetRoleIds.Contains(ur.RoleId)).ToList();
        foreach (var userRole in toRemove)
        {
            _context.Set<UserRole>().Remove(userRole);
            user.UserRoles.Remove(userRole);
        }

        var existingRoleIds = user.UserRoles
            .Where(userRole => !userRole.IsDeleted && userRole.TenantId == user.TenantId)
            .Select(userRole => userRole.RoleId)
            .ToHashSet();
        foreach (var role in roles)
        {
            if (existingRoleIds.Contains(role.Id)) continue;
            var deletedAssignment = user.UserRoles.FirstOrDefault(userRole =>
                userRole.RoleId == role.Id && userRole.TenantId == user.TenantId && userRole.IsDeleted);
            if (deletedAssignment is not null)
            {
                deletedAssignment.IsDeleted = false;
                deletedAssignment.UpdatedBy = actorId;
                deletedAssignment.UpdatedAt = DateTime.UtcNow;
                existingRoleIds.Add(role.Id);
                continue;
            }

            var userRole = new UserRole
            {
                TenantId = user.TenantId,
                UserId = user.Id,
                RoleId = role.Id,
                CreatedBy = actorId
            };
            _context.Set<UserRole>().Add(userRole);
            existingRoleIds.Add(role.Id);
        }
    }

    private async Task<Error?> ValidateRoleRemovalsAsync(
        IReadOnlyCollection<Role> roles,
        Guid actorUserId,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var unresolvedOwnership = roles.Where(role =>
                role.IsLegacyOrganizationOwnershipUnresolved
                || role.OrganizationId == Guid.Empty)
            .ToArray();
        if (unresolvedOwnership.Length > 0)
            return new Error(
                RoleAssignmentContract.OutOfScope,
                "This Role has no verified Organization owner and cannot be removed through User Administration. Reconcile its ownership first.",
                409,
                "roleIds",
                new Dictionary<string, object?> { ["roleIds"] = unresolvedOwnership.Select(role => role.Id).ToArray() });

        var protectedOrUnowned = roles.Where(role =>
                ProtectedTenantAdminPolicy.HasReservedAdministratorIdentity(role))
            .ToArray();
        if (protectedOrUnowned.Length > 0)
            return new Error(
                RoleAssignmentContract.ProtectedRole,
                "Protected administrator Roles cannot be removed through ordinary User Administration.",
                403,
                "roleIds",
                new Dictionary<string, object?> { ["roleIds"] = protectedOrUnowned.Select(role => role.Id).ToArray() });

        foreach (var group in roles.GroupBy(role => role.OrganizationId))
        {
            var organizationPermissions = group.Key is Guid organizationId
                ? await _effectivePermissions.GetEffectivePermissionsForOrganizationAsync(
                    actorUserId, tenantId, organizationId, cancellationToken)
                : await _effectivePermissions.GetEffectivePermissionsAsync(actorUserId, tenantId, cancellationToken);
            if (!organizationPermissions.Contains("Users.AssignRoles"))
                return new Error(
                    RoleAssignmentContract.OutOfScope,
                    "Users.AssignRoles is not effective in every Role scope you are changing.",
                    403,
                    "roleIds",
                    group.Key is Guid scopedOrganizationId
                        ? new Dictionary<string, object?> { ["organizationId"] = scopedOrganizationId }
                        : null);
        }

        return null;
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
        var hadActiveTenantAdmin = await tenantAdminMutation.HasActiveTenantAdminAsync(tenantId, cancellationToken);

        var target = await _context.Set<User>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(user => user.Id == id && user.TenantId == tenantId && !user.IsDeleted, cancellationToken);
        if (target is null)
            return Result<IReadOnlyList<UserPermissionAssignmentDto>>.Failure(Error.NotFound("User not found"));

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
                || !definition.IsActive)
            .ToHashSet();

        foreach (var (permissionId, effect) in requested)
        {
            var permission = catalogById[permissionId];
            var key = $"{permission.Resource}.{permission.Action}";
            if (permission.IsDeleted
                || !PermissionCatalog.ByKey.TryGetValue(key, out var definition)
                || !definition.IsActive)
            {
                return Result<IReadOnlyList<UserPermissionAssignmentDto>>.Failure(Error.Validation(
                    $"Permission {key} is inactive or uncatalogued and cannot receive a direct override."));
            }

            if (effect == PermissionOverrideEffect.Allow && !_permissionProtection.IsGrantable(definition))
            {
                return Result<IReadOnlyList<UserPermissionAssignmentDto>>.Failure(Error.Forbidden(
                    $"Permission {key} is protected by Product Owner policy and cannot be granted through a direct override."));
            }

            if (effect == PermissionOverrideEffect.Allow && definition.Scope != PermissionScope.Tenant)
                return Result<IReadOnlyList<UserPermissionAssignmentDto>>.Failure(Error.Validation(
                    $"Permission {key} requires a resource-scoped Role grant and cannot be assigned as a Tenant-wide direct override."));
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
            if (hadActiveTenantAdmin
                && !await tenantAdminMutation.HasActiveTenantAdminAsync(tenantId, cancellationToken))
            {
                await tenantAdminMutation.RollbackAsync(cancellationToken);
                _context.ChangeTracker.Clear();
                await _auditService.AppendAsync(new AuditEntry
                {
                    TenantId = tenantId,
                    EntityType = "UserPermissionOverride",
                    EntityId = id,
                    Action = "TenantAdminMutationRejected",
                    Changes = JsonSerializer.Serialize(new { Rejected = true, Reason = "Last active Tenant Admin assignment would be removed" }),
                    NewValues = JsonSerializer.Serialize(new { Rejected = true, PermissionIds = dto.Overrides.Select(item => item.PermissionId).ToArray() }),
                    UserId = actorUserId,
                    UserName = actorId,
                    CreatedBy = actorId
                }, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
                return Result<IReadOnlyList<UserPermissionAssignmentDto>>.Failure(Error.Conflict("This change would remove the last active Tenant Admin assignment."));
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
        var rolePermissionSources = await (
                from assignment in _context.Set<UserRole>().IgnoreQueryFilters().AsNoTracking()
                where assignment.UserId == userId && assignment.TenantId == tenantId && !assignment.IsDeleted
                      && assignment.Role.TenantId == tenantId && !assignment.Role.IsDeleted
                from permission in assignment.Role.Permissions
                where permission.TenantId == tenantId && !permission.IsDeleted
                select new { RoleName = assignment.Role.Name, Key = permission.Resource + "." + permission.Action })
            .ToListAsync(cancellationToken);
        var inherited = new HashSet<string>(rolePermissionSources.Select(source => source.Key), StringComparer.Ordinal);
        var roleGrantSourcesByKey = rolePermissionSources
            .GroupBy(source => source.Key, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<string>)group
                    .Select(source => source.RoleName)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(name => name, StringComparer.Ordinal)
                    .ToArray(),
                StringComparer.Ordinal);
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
                IsAssignable = catalogEntry is not null && _permissionProtection.IsGrantable(catalogEntry),
                // This endpoint is returned only after UserPermissions.Manage is
                // verified. The user may always add a restrictive Deny; the API
                // separately limits Allow to grantable Tenant-scoped permissions.
                CanDelegate = true,
                Scope = catalogEntry?.Scope.ToString() ?? nameof(PermissionScope.Tenant),
                RoleGrantSources = roleGrantSourcesByKey.TryGetValue(key, out var sources)
                    ? sources
                    : (IReadOnlyList<string>)Array.Empty<string>(),
                IsRoleGranted = roleGranted,
                OverrideEffect = hasDirectOverride ? effect : null,
                IsEffective = isEffective
            };
        }).ToList();
    }

    private static UserAdminDto MapToDto(
        User user,
        IReadOnlyDictionary<Guid, string>? organizationNames,
        IReadOnlyDictionary<Guid, bool> canRemoveByOrganization,
        bool canRemoveTenantRoles,
        Guid actorUserId)
    {
        organizationNames ??= new Dictionary<Guid, string>();
        var roles = user.UserRoles
            .Where(userRole => !userRole.IsDeleted && userRole.Role is not null
                && userRole.Role.TenantId == user.TenantId)
            .Select(userRole =>
            {
                var role = userRole.Role!;
                var organizationId = role.OrganizationId;
                var isProtected = ProtectedTenantAdminPolicy.HasReservedAdministratorIdentity(role);
                var isUnresolved = role.IsLegacyOrganizationOwnershipUnresolved;
                var organizationName = organizationId is Guid id && organizationNames.TryGetValue(id, out var name)
                    ? name
                    : null;
                return new UserAdminRoleDto
                {
                    Id = role.Id,
                    Name = role.Name,
                    OrganizationId = organizationId,
                    OrganizationName = organizationName,
                    Scope = role.IsLegacyOrganizationOwnershipUnresolved ? "Unresolved"
                        : organizationId.HasValue ? "Organization" : "Tenant",
                    IsOrganizationOwned = organizationId.HasValue,
                    IsLegacyOrganizationOwnershipUnresolved = role.IsLegacyOrganizationOwnershipUnresolved,
                    IsOrganizationOwnershipUnresolved = isUnresolved,
                    IsProtected = isProtected,
                    CanRemove = user.Id != actorUserId && !isProtected && !isUnresolved
                        && (organizationId is Guid orgId
                            ? canRemoveByOrganization.TryGetValue(orgId, out var canRemove) && canRemove
                            : canRemoveTenantRoles)
                };
            })
            .OrderBy(role => role.OrganizationName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(role => role.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(role => role.Id)
            .ToList();
        var organizations = roles
            .Where(role => role.IsOrganizationOwned && role.OrganizationId.HasValue)
            .GroupBy(role => role.OrganizationId!.Value)
            .Select(group => new UserOrganizationDto
            {
                Id = group.Key,
                Name = group.Select(role => role.OrganizationName).FirstOrDefault(name => !string.IsNullOrWhiteSpace(name))
            })
            .OrderBy(organization => organization.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(organization => organization.Id)
            .ToList();

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
            Roles = roles,
            Organizations = organizations,
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

    private static (IReadOnlyList<Guid> Ids, Error? Error) NormalizeRoleIds(List<Guid>? roleIds)
    {
        if (roleIds is null || roleIds.Count == 0)
            return (Array.Empty<Guid>(), null);
        if (roleIds.Any(roleId => roleId == Guid.Empty))
            return (Array.Empty<Guid>(), new Error(
                RoleAssignmentContract.InvalidRoleIds,
                "Role IDs must be valid.",
                400,
                "roleIds"));

        return (roleIds.Distinct().ToArray(), null);
    }

    private async Task<IReadOnlyDictionary<Guid, bool>> LoadRoleRemovalPermissionsAsync(
        Guid actorUserId,
        Guid tenantId,
        IEnumerable<Guid> organizationIds,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<Guid, bool>();
        foreach (var organizationId in organizationIds.Where(id => id != Guid.Empty).Distinct())
        {
            var organizationPermissions = await _effectivePermissions.GetEffectivePermissionsForOrganizationAsync(
                actorUserId, tenantId, organizationId, cancellationToken);
            result[organizationId] = organizationPermissions.Contains("Users.AssignRoles");
        }

        return result;
    }

    private async Task<bool> CanRemoveTenantRolesAsync(
        Guid actorUserId,
        Guid tenantId,
        CancellationToken cancellationToken) =>
        (await _effectivePermissions.GetEffectivePermissionsAsync(actorUserId, tenantId, cancellationToken))
            .Contains("Users.AssignRoles");

    private static RoleAuditSnapshot[] MapRoleAuditSnapshots(
        IEnumerable<Role> roles,
        IReadOnlyDictionary<Guid, string> organizationNames) =>
        roles.Select(role => new RoleAuditSnapshot(
                role.Id,
                role.Name,
                role.OrganizationId,
                role.OrganizationId is Guid organizationId && organizationNames.TryGetValue(organizationId, out var name) ? name : null,
                role.IsLegacyOrganizationOwnershipUnresolved ? "Unresolved"
                    : role.OrganizationId.HasValue ? "Organization" : "Tenant",
                ProtectedTenantAdminPolicy.HasReservedAdministratorIdentity(role),
                role.IsLegacyOrganizationOwnershipUnresolved))
            .OrderBy(role => role.OrganizationName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(role => role.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(role => role.RoleId)
            .ToArray();

    private static UserOrganizationDto[] MapDerivedOrganizationSnapshots(
        IEnumerable<Role> roles,
        IReadOnlyDictionary<Guid, string> organizationNames) =>
        roles.Where(role => role.OrganizationId.HasValue)
            .GroupBy(role => role.OrganizationId!.Value)
            .Select(group => new UserOrganizationDto
            {
                Id = group.Key,
                Name = organizationNames.TryGetValue(group.Key, out var name) ? name : null
            })
            .OrderBy(organization => organization.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(organization => organization.Id)
            .ToArray();

    private sealed record RoleAuditSnapshot(
        Guid RoleId,
        string Name,
        Guid? OrganizationId,
        string? OrganizationName,
        string Scope,
        bool IsProtected,
        bool IsLegacyOrganizationOwnershipUnresolved);

    private async Task<IReadOnlyDictionary<Guid, string>> LoadOrganizationNamesAsync(
        IEnumerable<Guid> organizationIds,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        return await _organizationDirectory.GetActiveNamesAsync(tenantId, organizationIds, cancellationToken);
    }

    private async Task<string> LoadActorNameAsync(Guid actorUserId, Guid tenantId, string fallback, CancellationToken cancellationToken)
    {
        return await _context.Set<User>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(actor => actor.Id == actorUserId && actor.TenantId == tenantId && !actor.IsDeleted)
            .Select(actor => actor.FirstName + " " + actor.LastName)
            .FirstOrDefaultAsync(cancellationToken) ?? fallback;
    }
}
