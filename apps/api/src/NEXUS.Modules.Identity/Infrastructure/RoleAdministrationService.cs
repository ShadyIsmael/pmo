using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.Identity.Application;
using NEXUS.Modules.Identity.Domain;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.Identity.Infrastructure;

public sealed class RoleAdministrationService : IRoleAdministrationService
{
    private const string ManageUsersPermission = "users.write";
    private const int MaximumPermissionsPerRole = 1000;
    private readonly DbContext _context;
    private readonly IUserEffectivePermissionService _effectivePermissions;
    private readonly IAuditService _auditService;

    public RoleAdministrationService(
        DbContext context,
        IUserEffectivePermissionService effectivePermissions,
        IAuditService auditService)
    {
        _context = context;
        _effectivePermissions = effectivePermissions;
        _auditService = auditService;
    }

    public async Task<Result<IReadOnlyList<RoleSummaryDto>>> GetManageableRolesAsync(
        Guid actorId,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var actorPermissions = await GetActorPermissionsAsync(actorId, tenantId, cancellationToken);
        if (actorPermissions is null)
            return Result<IReadOnlyList<RoleSummaryDto>>.Failure(Error.Forbidden("You do not have permission to manage roles"));

        var roles = await _context.Set<Role>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(role => role.TenantId == tenantId && !role.IsDeleted)
            .Include(role => role.Permissions)
            .Include(role => role.UserRoles.Where(assignment =>
                assignment.TenantId == tenantId && !assignment.IsDeleted
                && assignment.User.TenantId == tenantId && !assignment.User.IsDeleted))
            .OrderBy(role => role.Category)
            .ThenBy(role => role.Name)
            .ToListAsync(cancellationToken);

        var manageableRoles = roles
            .Where(role => CanManageRole(role, tenantId, actorPermissions))
            .Select(role => MapSummary(role, role.UserRoles.Count))
            .ToList();

        return Result<IReadOnlyList<RoleSummaryDto>>.Success(manageableRoles);
    }

    public async Task<Result<IReadOnlyList<PermissionCatalogItemDto>>> GetAssignablePermissionsAsync(
        Guid actorId,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var actorPermissions = await GetActorPermissionsAsync(actorId, tenantId, cancellationToken);
        if (actorPermissions is null)
            return Result<IReadOnlyList<PermissionCatalogItemDto>>.Failure(Error.Forbidden("You do not have permission to manage roles"));

        var permissions = await _context.Set<Permission>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(permission => permission.TenantId == tenantId && !permission.IsDeleted)
            .OrderBy(permission => permission.Resource)
            .ThenBy(permission => permission.Action)
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<PermissionCatalogItemDto>>.Success(
            permissions
                .Where(permission => actorPermissions.Contains(PermissionKey(permission)))
                .Select(MapPermission)
                .ToList());
    }

    public async Task<Result<RoleDetailDto>> GetByIdAsync(
        Guid id,
        Guid actorId,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var actorPermissions = await GetActorPermissionsAsync(actorId, tenantId, cancellationToken);
        if (actorPermissions is null)
            return Result<RoleDetailDto>.Failure(Error.Forbidden("You do not have permission to manage roles"));

        var role = await LoadRoleAsync(id, tenantId, tracked: false, cancellationToken);
        if (role is null)
            return Result<RoleDetailDto>.Failure(Error.NotFound("Role not found"));
        if (!CanManageRole(role, tenantId, actorPermissions))
            return Result<RoleDetailDto>.Failure(Error.Forbidden("You cannot manage a role with permissions outside your own effective access"));

        return Result<RoleDetailDto>.Success(MapDetail(role, await GetAssignedUserCountAsync(id, tenantId, cancellationToken)));
    }

    public async Task<Result<RoleDetailDto>> CreateAsync(
        SaveRoleDto dto,
        Guid actorId,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var validationError = ValidateInput(dto);
        if (validationError is not null)
            return Result<RoleDetailDto>.Failure(validationError);

        var actorPermissions = await GetActorPermissionsAsync(actorId, tenantId, cancellationToken);
        if (actorPermissions is null)
            return Result<RoleDetailDto>.Failure(Error.Forbidden("You do not have permission to manage roles"));

        var name = dto.Name.Trim();
        if (await RoleNameExistsAsync(name, tenantId, null, cancellationToken))
            return Result<RoleDetailDto>.Failure(Error.Conflict("A role with this name already exists"));

        var permissionsResult = await ResolvePermissionsAsync(dto.PermissionIds, tenantId, actorPermissions, cancellationToken);
        if (permissionsResult.IsFailure)
            return Result<RoleDetailDto>.Failure(permissionsResult.Error!);

        var role = new Role
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name,
            Description = NormalizeOptional(dto.Description),
            Category = NormalizeOptional(dto.Category),
            IsSystem = false,
            CreatedBy = actorId.ToString("D"),
            Permissions = permissionsResult.Value!
        };

        _context.Set<Role>().Add(role);
        await AppendAuditAsync(
            role.Id,
            "Created",
            null,
            new { role.Name, role.Description, role.Category, Permissions = role.Permissions.Select(PermissionKey).OrderBy(key => key).ToArray() },
            actorId,
            tenantId,
            cancellationToken);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Result<RoleDetailDto>.Failure(Error.Conflict("The role could not be saved because its name is already in use or another update conflicted"));
        }

        return Result<RoleDetailDto>.Success(MapDetail(role, 0));
    }

    public async Task<Result<RoleDetailDto>> UpdateAsync(
        Guid id,
        SaveRoleDto dto,
        Guid actorId,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var validationError = ValidateInput(dto);
        if (validationError is not null)
            return Result<RoleDetailDto>.Failure(validationError);

        var actorPermissions = await GetActorPermissionsAsync(actorId, tenantId, cancellationToken);
        if (actorPermissions is null)
            return Result<RoleDetailDto>.Failure(Error.Forbidden("You do not have permission to manage roles"));

        var role = await LoadRoleAsync(id, tenantId, tracked: true, cancellationToken);
        if (role is null)
            return Result<RoleDetailDto>.Failure(Error.NotFound("Role not found"));
        if (!CanManageRole(role, tenantId, actorPermissions))
            return Result<RoleDetailDto>.Failure(Error.Forbidden("You cannot manage a role with permissions outside your own effective access"));

        var name = dto.Name.Trim();
        if (role.IsSystem && !string.Equals(role.Name, name, StringComparison.Ordinal))
            return Result<RoleDetailDto>.Failure(Error.Validation("System role names cannot be changed"));
        if (await RoleNameExistsAsync(name, tenantId, id, cancellationToken))
            return Result<RoleDetailDto>.Failure(Error.Conflict("A role with this name already exists"));

        var permissionsResult = await ResolvePermissionsAsync(dto.PermissionIds, tenantId, actorPermissions, cancellationToken);
        if (permissionsResult.IsFailure)
            return Result<RoleDetailDto>.Failure(permissionsResult.Error!);

        var nextPermissions = permissionsResult.Value!;
        if (!await ActorRetainsRoleManagementAsync(actorId, tenantId, role.Id, nextPermissions, cancellationToken))
            return Result<RoleDetailDto>.Failure(Error.Conflict("This change would remove your role-management access. Keep at least one effective users.write permission assigned to your account."));

        var oldPermissions = role.Permissions.Select(PermissionKey).OrderBy(key => key, StringComparer.Ordinal).ToArray();
        var newPermissions = nextPermissions.Select(PermissionKey).OrderBy(key => key, StringComparer.Ordinal).ToArray();
        var description = NormalizeOptional(dto.Description);
        var category = NormalizeOptional(dto.Category);
        var hasChanges = !string.Equals(role.Name, name, StringComparison.Ordinal)
            || !string.Equals(role.Description, description, StringComparison.Ordinal)
            || !string.Equals(role.Category, category, StringComparison.Ordinal)
            || !oldPermissions.SequenceEqual(newPermissions, StringComparer.Ordinal);

        if (!hasChanges)
            return Result<RoleDetailDto>.Success(MapDetail(role, role.UserRoles.Count));

        var originalValues = new { role.Name, role.Description, role.Category, Permissions = oldPermissions };
        role.Name = name;
        role.Description = description;
        role.Category = category;
        role.UpdatedBy = actorId.ToString("D");
        role.Permissions.Clear();
        foreach (var permission in nextPermissions)
            role.Permissions.Add(permission);

        await AppendAuditAsync(
            role.Id,
            "Updated",
            originalValues,
            new { role.Name, role.Description, role.Category, Permissions = newPermissions },
            actorId,
            tenantId,
            cancellationToken);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<RoleDetailDto>.Failure(Error.Conflict("This role changed while you were editing it. Reload the role and try again."));
        }
        catch (DbUpdateException)
        {
            return Result<RoleDetailDto>.Failure(Error.Conflict("The role could not be saved because its name is already in use or another update conflicted"));
        }

        return Result<RoleDetailDto>.Success(MapDetail(role, role.UserRoles.Count));
    }

    public async Task<Result> DeleteAsync(
        Guid id,
        Guid actorId,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var actorPermissions = await GetActorPermissionsAsync(actorId, tenantId, cancellationToken);
        if (actorPermissions is null)
            return Result.Failure(Error.Forbidden("You do not have permission to manage roles"));

        var role = await LoadRoleAsync(id, tenantId, tracked: true, cancellationToken);
        if (role is null)
            return Result.Failure(Error.NotFound("Role not found"));
        if (!CanManageRole(role, tenantId, actorPermissions))
            return Result.Failure(Error.Forbidden("You cannot manage a role with permissions outside your own effective access"));
        if (role.IsSystem)
            return Result.Failure(Error.Forbidden("System roles cannot be deleted"));

        var assignedUsers = await GetAssignedUserCountAsync(id, tenantId, cancellationToken);
        if (assignedUsers > 0)
            return Result.Failure(Error.Conflict("This role is assigned to users. Remove those assignments before deleting it."));

        var originalValues = new
        {
            role.Name,
            role.Description,
            role.Category,
            Permissions = role.Permissions.Select(PermissionKey).OrderBy(key => key, StringComparer.Ordinal).ToArray()
        };
        role.IsDeleted = true;
        role.UpdatedBy = actorId.ToString("D");
        role.Permissions.Clear();
        await AppendAuditAsync(
            role.Id,
            "Deleted",
            originalValues,
            new { IsDeleted = true },
            actorId,
            tenantId,
            cancellationToken);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure(Error.Conflict("This role changed while you were deleting it. Reload and try again."));
        }
        catch (DbUpdateException)
        {
            return Result.Failure(Error.Conflict("The role could not be deleted because another update conflicted"));
        }

        return Result.Success();
    }

    private async Task<IReadOnlySet<string>?> GetActorPermissionsAsync(
        Guid actorId,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        if (actorId == Guid.Empty || tenantId == Guid.Empty)
            return null;

        var permissions = await _effectivePermissions.GetEffectivePermissionsAsync(actorId, tenantId, cancellationToken);
        return permissions.Contains(ManageUsersPermission) ? permissions : null;
    }

    private async Task<Role?> LoadRoleAsync(Guid id, Guid tenantId, bool tracked, CancellationToken cancellationToken)
    {
        IQueryable<Role> query = _context.Set<Role>()
            .IgnoreQueryFilters()
            .Where(role => role.Id == id && role.TenantId == tenantId && !role.IsDeleted)
            .Include(role => role.Permissions)
            .Include(role => role.UserRoles.Where(assignment =>
                assignment.TenantId == tenantId && !assignment.IsDeleted
                && assignment.User.TenantId == tenantId && !assignment.User.IsDeleted));

        if (!tracked)
            query = query.AsNoTracking();

        return await query.FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<Result<List<Permission>>> ResolvePermissionsAsync(
        List<Guid>? permissionIds,
        Guid tenantId,
        IReadOnlySet<string> actorPermissions,
        CancellationToken cancellationToken)
    {
        if (permissionIds is null || permissionIds.Count > MaximumPermissionsPerRole)
            return Result<List<Permission>>.Failure(Error.Validation("The permission list is invalid"));
        if (permissionIds.Distinct().Count() != permissionIds.Count)
            return Result<List<Permission>>.Failure(Error.Validation("A permission can only be selected once"));

        var permissions = await _context.Set<Permission>()
            .IgnoreQueryFilters()
            .Where(permission => permissionIds.Contains(permission.Id)
                && permission.TenantId == tenantId && !permission.IsDeleted)
            .ToListAsync(cancellationToken);

        if (permissions.Count != permissionIds.Count)
            return Result<List<Permission>>.Failure(Error.Validation("One or more permissions are not available in this tenant"));
        if (permissions.Any(permission => !actorPermissions.Contains(PermissionKey(permission))))
            return Result<List<Permission>>.Failure(Error.Forbidden("You can only assign permissions within your own effective access"));

        return Result<List<Permission>>.Success(permissions);
    }

    private async Task<bool> RoleNameExistsAsync(string name, Guid tenantId, Guid? exceptRoleId, CancellationToken cancellationToken)
    {
        var normalizedName = name.ToUpperInvariant();
        return await _context.Set<Role>()
            .IgnoreQueryFilters()
            .AnyAsync(role => role.TenantId == tenantId && !role.IsDeleted
                && role.Id != (exceptRoleId ?? Guid.Empty)
                && role.Name.ToUpper() == normalizedName, cancellationToken);
    }

    private async Task<bool> ActorRetainsRoleManagementAsync(
        Guid actorId,
        Guid tenantId,
        Guid changedRoleId,
        IReadOnlyCollection<Permission> replacementPermissions,
        CancellationToken cancellationToken)
    {
        var assignments = await _context.Set<UserRole>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(assignment => assignment.UserId == actorId && assignment.TenantId == tenantId && !assignment.IsDeleted
                && assignment.Role.TenantId == tenantId && !assignment.Role.IsDeleted)
            .Include(assignment => assignment.Role)
                .ThenInclude(role => role.Permissions)
            .ToListAsync(cancellationToken);

        var effective = assignments
            .SelectMany(assignment => assignment.RoleId == changedRoleId
                ? replacementPermissions.AsEnumerable()
                : assignment.Role.Permissions.AsEnumerable())
            .Where(permission => permission.TenantId == tenantId && !permission.IsDeleted)
            .Select(PermissionKey)
            .ToHashSet(StringComparer.Ordinal);

        var overrides = await _context.Set<UserPermissionOverride>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(item => item.UserId == actorId && item.TenantId == tenantId && !item.IsDeleted
                && item.Permission.TenantId == tenantId && !item.Permission.IsDeleted)
            .Select(item => new { Permission = PermissionKey(item.Permission), item.Effect })
            .ToListAsync(cancellationToken);

        foreach (var item in overrides)
        {
            if (item.Effect == PermissionOverrideEffect.Allow)
                effective.Add(item.Permission);
            else if (item.Effect == PermissionOverrideEffect.Deny)
                effective.Remove(item.Permission);
        }

        return effective.Contains(ManageUsersPermission);
    }

    private async Task<int> GetAssignedUserCountAsync(Guid roleId, Guid tenantId, CancellationToken cancellationToken) =>
        await _context.Set<UserRole>()
            .IgnoreQueryFilters()
            .CountAsync(assignment => assignment.RoleId == roleId && assignment.TenantId == tenantId
                && !assignment.IsDeleted && assignment.User.TenantId == tenantId && !assignment.User.IsDeleted,
                cancellationToken);

    private async Task AppendAuditAsync(
        Guid roleId,
        string action,
        object? originalValues,
        object? newValues,
        Guid actorId,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var actorName = await _context.Set<User>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(user => user.Id == actorId && user.TenantId == tenantId && !user.IsDeleted)
            .Select(user => user.FirstName + " " + user.LastName)
            .FirstOrDefaultAsync(cancellationToken);
        var changes = new { Original = originalValues, Current = newValues };

        await _auditService.AppendAsync(new AuditEntry
        {
            TenantId = tenantId,
            EntityType = "Role",
            EntityId = roleId,
            Action = action,
            Changes = JsonSerializer.Serialize(changes),
            OriginalValues = originalValues is null ? null : JsonSerializer.Serialize(originalValues),
            NewValues = newValues is null ? null : JsonSerializer.Serialize(newValues),
            UserId = actorId,
            UserName = actorName ?? actorId.ToString("D"),
            CreatedBy = actorId.ToString("D")
        }, cancellationToken);
    }

    private static Error? ValidateInput(SaveRoleDto? dto)
    {
        if (dto is null || dto.PermissionIds is null)
            return Error.Validation("Role details and permission selections are required");
        if (string.IsNullOrWhiteSpace(dto.Name) || dto.Name.Trim().Length > 100)
            return Error.Validation("Role name is required and must not exceed 100 characters");
        if (dto.Description?.Trim().Length > 500)
            return Error.Validation("Role description must not exceed 500 characters");
        if (dto.Category?.Trim().Length > 100)
            return Error.Validation("Role category must not exceed 100 characters");
        return null;
    }

    private static bool CanManageRole(Role role, Guid tenantId, IReadOnlySet<string> actorPermissions) =>
        role.Permissions.All(permission => permission.TenantId == tenantId && !permission.IsDeleted
            && actorPermissions.Contains(PermissionKey(permission)));

    private static RoleSummaryDto MapSummary(Role role, int assignedUserCount) => new()
    {
        Id = role.Id,
        Name = role.Name,
        Description = role.Description,
        Category = role.Category,
        IsSystem = role.IsSystem,
        PermissionCount = role.Permissions.Count(permission => !permission.IsDeleted),
        AssignedUserCount = assignedUserCount
    };

    private static RoleDetailDto MapDetail(Role role, int assignedUserCount) => new()
    {
        Id = role.Id,
        Name = role.Name,
        Description = role.Description,
        Category = role.Category,
        IsSystem = role.IsSystem,
        PermissionCount = role.Permissions.Count(permission => !permission.IsDeleted),
        AssignedUserCount = assignedUserCount,
        Permissions = role.Permissions
            .Where(permission => !permission.IsDeleted)
            .OrderBy(permission => permission.Resource, StringComparer.Ordinal)
            .ThenBy(permission => permission.Action, StringComparer.Ordinal)
            .Select(MapPermission)
            .ToList()
    };

    private static PermissionCatalogItemDto MapPermission(Permission permission) => new()
    {
        Id = permission.Id,
        Name = permission.Name,
        Resource = permission.Resource,
        Action = permission.Action,
        Description = permission.Description
    };

    private static string PermissionKey(Permission permission) => $"{permission.Resource}.{permission.Action}";

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
