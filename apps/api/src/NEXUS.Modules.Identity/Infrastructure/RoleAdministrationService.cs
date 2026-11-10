using System.Text.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.Identity.Application;
using NEXUS.Modules.Identity.Domain;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.Identity.Infrastructure;

public sealed class RoleAdministrationService : IRoleAdministrationService
{
    private const string CreateRolePermission = "Roles.Create";
    private const string EditRoleDetailsPermission = "Roles.EditDetails";
    private const string ConfigureRolePermissionsPermission = "Roles.ConfigurePermissions";
    private const string DeleteRolePermission = "Roles.Delete";
    private const int MaximumPermissionsPerRole = 1000;
    private readonly DbContext _context;
    private readonly IUserEffectivePermissionService _effectivePermissions;
    private readonly IAuditService _auditService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public RoleAdministrationService(
        DbContext context,
        IUserEffectivePermissionService effectivePermissions,
        IAuditService auditService,
        IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _effectivePermissions = effectivePermissions;
        _auditService = auditService;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<Result<IReadOnlyList<RoleSummaryDto>>> GetManageableRolesAsync(
        Guid actorId,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var actorPermissions = await GetActorPermissionsAsync(actorId, tenantId, cancellationToken, requireManage: false);
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

        var canManageRoles = HasRoleManagementCapability(actorPermissions);
        var bootstrapDelegableKeys = canManageRoles
            ? await BootstrapPermissionDelegationPolicy.GetDelegableKeysAsync(_context, actorId, tenantId, actorPermissions, cancellationToken)
            : EmptyPermissionKeys;
        var manageableRoles = roles
            .Where(role => !canManageRoles || CanManageRole(role, tenantId, actorPermissions, bootstrapDelegableKeys))
            .Select(role => MapSummary(role, role.UserRoles.Count, tenantId))
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
        if (!CanConfigureRolePermissions(actorPermissions))
            return Result<IReadOnlyList<PermissionCatalogItemDto>>.Failure(Error.Forbidden("Roles.ConfigurePermissions is required."));
        var bootstrapDelegableKeys = await BootstrapPermissionDelegationPolicy.GetDelegableKeysAsync(
            _context, actorId, tenantId, actorPermissions, cancellationToken);

        var permissions = await _context.Set<Permission>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(permission => permission.TenantId == tenantId && !permission.IsDeleted)
            .OrderBy(permission => permission.Resource)
            .ThenBy(permission => permission.Action)
            .ToListAsync(cancellationToken);

        // Preserve the actor-effective/bootstrap capability for system-role recipients,
        // while exposing the broader custom-role delegation capability separately.
        var actorDelegableKeys = actorPermissions.Concat(bootstrapDelegableKeys)
            .ToHashSet(StringComparer.Ordinal);
        var catalogItems = new List<PermissionCatalogItemDto>();
        foreach (var permission in permissions)
        {
            var key = PermissionKey(permission);
            if (!PermissionCatalog.ByKey.TryGetValue(key, out var definition)
                || !definition.IsActive
                || definition.Scope is PermissionScope.Platform or PermissionScope.CrossTenant or PermissionScope.SystemOwner)
                continue;

            var canDelegate = definition.IsAssignable
                && actorDelegableKeys.Contains(key)
                && definition.RequiredPermissionKeys.All(actorDelegableKeys.Contains);
            var canDelegateToCustomRole = definition.IsAssignable
                && (IsAdministrativePermission(definition)
                    ? actorPermissions.Contains(key)
                        && definition.RequiredPermissionKeys.All(actorPermissions.Contains)
                    : IsRoleAssignableScope(definition.Scope));
            catalogItems.Add(MapPermission(permission, canDelegate, canDelegateToCustomRole));
        }

        return Result<IReadOnlyList<PermissionCatalogItemDto>>.Success(catalogItems);
    }

    public async Task<Result<RoleDetailDto>> GetByIdAsync(
        Guid id,
        Guid actorId,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var actorPermissions = await GetActorPermissionsAsync(actorId, tenantId, cancellationToken, requireManage: false);
        if (actorPermissions is null)
            return Result<RoleDetailDto>.Failure(Error.Forbidden("You do not have permission to manage roles"));

        var role = await LoadRoleAsync(id, tenantId, tracked: false, cancellationToken);
        if (role is null)
            return Result<RoleDetailDto>.Failure(Error.NotFound("Role not found"));
        var bootstrapDelegableKeys = await BootstrapPermissionDelegationPolicy.GetDelegableKeysAsync(_context, actorId, tenantId, actorPermissions, cancellationToken);
        if (HasRoleManagementCapability(actorPermissions) && !CanManageRole(role, tenantId, actorPermissions, bootstrapDelegableKeys))
            return Result<RoleDetailDto>.Failure(Error.Forbidden("You cannot manage a role with permissions outside your own effective access"));

        return Result<RoleDetailDto>.Success(MapDetail(role, await GetAssignedUserCountAsync(id, tenantId, cancellationToken), tenantId));
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
        var category = NormalizeOptional(dto.Category);
        if (!RoleCategories.IsSelectable(category))
            return Result<RoleDetailDto>.Failure(Error.Validation(
                $"A custom role category must be one of: {string.Join(", ", RoleCategories.Selectable)}. System is reserved for protected system roles."));
        if (!CanPerformRoleOperation(actorPermissions, CreateRolePermission))
            return Result<RoleDetailDto>.Failure(Error.Forbidden("Roles.Create is required."));
        if (dto.PermissionIds is { Count: > 0 }
            && !CanConfigureRolePermissions(actorPermissions))
            return Result<RoleDetailDto>.Failure(Error.Forbidden("Roles.ConfigurePermissions is required to assign permissions while creating a role."));
        var bootstrapDelegableKeys = await BootstrapPermissionDelegationPolicy.GetDelegableKeysAsync(_context, actorId, tenantId, actorPermissions, cancellationToken);

        var name = dto.Name.Trim();
        if (string.Equals(name, "admin", StringComparison.OrdinalIgnoreCase))
            return Result<RoleDetailDto>.Failure(Error.Conflict("The protected system Admin role name is reserved."));
        if (await RoleNameExistsAsync(name, tenantId, null, cancellationToken))
            return Result<RoleDetailDto>.Failure(Error.Conflict("A role with this name already exists"));

        var permissionsResult = await ResolvePermissionsAsync(
            dto.PermissionIds, tenantId, actorPermissions, bootstrapDelegableKeys,
            allowOrdinaryDelegation: true,
            allowProtectedSystemAdminBaseline: false,
            cancellationToken);
        if (permissionsResult.IsFailure)
            return Result<RoleDetailDto>.Failure(permissionsResult.Error!);

        var role = new Role
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name,
            Description = NormalizeOptional(dto.Description),
            Category = category,
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

        return Result<RoleDetailDto>.Success(MapDetail(role, 0, tenantId));
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
        var bootstrapDelegableKeys = await BootstrapPermissionDelegationPolicy.GetDelegableKeysAsync(_context, actorId, tenantId, actorPermissions, cancellationToken);

        await using var tenantAdminMutation = await TenantAdminMutationGuard.AcquireAsync(_context, tenantId, cancellationToken);
        var hadEffectiveTenantAdmin = await tenantAdminMutation.HasEffectiveTenantAdminAsync(_effectivePermissions, tenantId, cancellationToken);

        var role = await LoadRoleAsync(id, tenantId, tracked: true, cancellationToken);
        if (role is null)
            return Result<RoleDetailDto>.Failure(Error.NotFound("Role not found"));
        if (!CanManageRole(role, tenantId, actorPermissions, bootstrapDelegableKeys))
            return Result<RoleDetailDto>.Failure(Error.Forbidden("You cannot manage a role with permissions outside your own effective access"));

        var category = NormalizeOptional(dto.Category);
        var categoryChanged = !string.Equals(role.Category, category, StringComparison.Ordinal);
        if (role.IsSystem && categoryChanged)
            return Result<RoleDetailDto>.Failure(Error.Forbidden("System role categories are protected and cannot be changed through role administration."));
        if (!role.IsSystem && !RoleCategories.IsSelectable(category)
            && !string.Equals(role.Category, category, StringComparison.Ordinal))
            return Result<RoleDetailDto>.Failure(Error.Validation(
                $"A custom role category must be one of: {string.Join(", ", RoleCategories.Selectable)}. System is reserved for protected system roles."));
        // Category is descriptive metadata. It does not constrain permission
        // grants or require existing grants to be removed when a category changes.

        var name = dto.Name.Trim();
        if (role.IsSystem && !string.Equals(role.Name, name, StringComparison.Ordinal))
            return Result<RoleDetailDto>.Failure(Error.Validation("System role names cannot be changed"));
        if (!ProtectedTenantAdminPolicy.IsProtectedSystemAdminRole(role)
            && string.Equals(name, "admin", StringComparison.OrdinalIgnoreCase))
            return Result<RoleDetailDto>.Failure(Error.Conflict("The protected system Admin role name is reserved."));
        if (await RoleNameExistsAsync(name, tenantId, id, cancellationToken))
            return Result<RoleDetailDto>.Failure(Error.Conflict("A role with this name already exists"));

        var oldPermissions = role.Permissions.Select(PermissionKey).OrderBy(key => key, StringComparer.Ordinal).ToArray();
        var permissionsResult = await ResolvePermissionsAsync(
            dto.PermissionIds, tenantId, actorPermissions, bootstrapDelegableKeys,
            allowOrdinaryDelegation: !role.IsSystem,
            allowProtectedSystemAdminBaseline: ProtectedTenantAdminPolicy.IsProtectedSystemAdminRole(role),
            cancellationToken);
        if (permissionsResult.IsFailure)
            return Result<RoleDetailDto>.Failure(permissionsResult.Error!);

        var nextPermissions = permissionsResult.Value!;
        var actorIsAssignedToRole = role.UserRoles.Any(assignment =>
            assignment.UserId == actorId && assignment.TenantId == tenantId && !assignment.IsDeleted);
        if (!role.IsSystem && actorIsAssignedToRole)
        {
            var effectiveAfterRoleUpdate = await GetActorEffectivePermissionsWithRoleReplacementAsync(
                actorId, tenantId, role.Id, nextPermissions, cancellationToken);
            if (effectiveAfterRoleUpdate.Except(actorPermissions, StringComparer.Ordinal).Any())
                return Result<RoleDetailDto>.Failure(Error.Forbidden(
                    "A role assigned to you cannot add permissions outside your current effective access."));
        }
        if (ProtectedTenantAdminPolicy.IsProtectedSystemAdminRole(role))
        {
            var selectedKeys = nextPermissions.Select(PermissionKey).ToHashSet(StringComparer.Ordinal);
            var missingMandatoryKeys = ProtectedTenantAdminPolicy.MandatoryPermissionKeys
                .Where(key => !selectedKeys.Contains(key))
                .OrderBy(key => key, StringComparer.Ordinal)
                .ToArray();
            if (missingMandatoryKeys.Length > 0)
            {
                await AppendAuditAsync(
                    role.Id,
                    "ProtectedAdminBaselineMutationRejected",
                    new { Permissions = role.Permissions.Select(PermissionKey).OrderBy(key => key, StringComparer.Ordinal).ToArray() },
                    new { RejectedRemoval = missingMandatoryKeys },
                    actorId,
                    tenantId,
                    cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
                await tenantAdminMutation.CommitAsync(cancellationToken);
                return Result<RoleDetailDto>.Failure(Error.Conflict("The protected system Admin role must retain every mandatory tenant Administration and Portfolio/Program permission."));
            }
        }
        var oldPermissionKeys = oldPermissions.ToHashSet(StringComparer.Ordinal);
        if (role.IsSystem && nextPermissions
                .Select(PermissionKey)
                .Any(key => BootstrapPermissionDelegationPolicy.Keys.Contains(key)
                    && !oldPermissionKeys.Contains(key)
                    && !(ProtectedTenantAdminPolicy.IsProtectedSystemAdminRole(role)
                        && BootstrapPermissionDelegationPolicy.CanAddToProtectedTenantAdminRole(key))))
            return Result<RoleDetailDto>.Failure(Error.Forbidden(
                "Bootstrap-delegable permissions can be added only to custom roles, except the explicitly configured Annual Budget permissions on the protected tenant Admin role."));
        if (!await ActorRetainsRoleManagementAsync(actorId, tenantId, role.Id, nextPermissions, cancellationToken))
            return Result<RoleDetailDto>.Failure(Error.Conflict("This change would remove your role-management access. Keep an effective Roles.Create, Roles.EditDetails, Roles.ConfigurePermissions, or Roles.Delete permission assigned to your account."));

        var newPermissions = nextPermissions.Select(PermissionKey).OrderBy(key => key, StringComparer.Ordinal).ToArray();
        var description = NormalizeOptional(dto.Description);
        var detailsChanged = !string.Equals(role.Name, name, StringComparison.Ordinal)
            || !string.Equals(role.Description, description, StringComparison.Ordinal)
            || !string.Equals(role.Category, category, StringComparison.Ordinal);
        var permissionsChanged = !oldPermissions.SequenceEqual(newPermissions, StringComparer.Ordinal);
        if (detailsChanged && !CanPerformRoleOperation(actorPermissions, EditRoleDetailsPermission))
            return Result<RoleDetailDto>.Failure(Error.Forbidden("Roles.EditDetails is required to change role details."));
        if (permissionsChanged && !CanConfigureRolePermissions(actorPermissions))
            return Result<RoleDetailDto>.Failure(Error.Forbidden("Roles.ConfigurePermissions is required to change role permissions."));
        var hasChanges = detailsChanged || permissionsChanged;

        if (!hasChanges)
            return Result<RoleDetailDto>.Success(MapDetail(role, role.UserRoles.Count, tenantId));

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
            if (hadEffectiveTenantAdmin
                && !await tenantAdminMutation.HasEffectiveTenantAdminAsync(_effectivePermissions, tenantId, cancellationToken))
            {
                await tenantAdminMutation.RollbackAsync(cancellationToken);
                _context.ChangeTracker.Clear();
                await AppendAuditAsync(
                    role.Id,
                    "TenantAdminMutationRejected",
                    originalValues,
                    new { Rejected = true, Reason = "Last effective tenant administrator would be removed" },
                    actorId,
                    tenantId,
                    cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
                return Result<RoleDetailDto>.Failure(Error.Conflict("This change would remove the last effective tenant administrator."));
            }
            await tenantAdminMutation.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<RoleDetailDto>.Failure(Error.Conflict("This role changed while you were editing it. Reload the role and try again."));
        }
        catch (DbUpdateException)
        {
            return Result<RoleDetailDto>.Failure(Error.Conflict("The role could not be saved because its name is already in use or another update conflicted"));
        }

        return Result<RoleDetailDto>.Success(MapDetail(role, role.UserRoles.Count, tenantId));
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
        if (!actorPermissions.Contains(DeleteRolePermission))
            return Result.Failure(Error.Forbidden("Roles.Delete is required."));
        var bootstrapDelegableKeys = await BootstrapPermissionDelegationPolicy.GetDelegableKeysAsync(_context, actorId, tenantId, actorPermissions, cancellationToken);

        var role = await LoadRoleAsync(id, tenantId, tracked: true, cancellationToken);
        if (role is null)
            return Result.Failure(Error.NotFound("Role not found"));
        if (!CanManageRole(role, tenantId, actorPermissions, bootstrapDelegableKeys))
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
        CancellationToken cancellationToken,
        bool requireManage = true)
    {
        if (actorId == Guid.Empty || tenantId == Guid.Empty || !IsCurrentActorAndTenant(actorId, tenantId))
            return null;

        var permissions = await _effectivePermissions.GetEffectivePermissionsAsync(actorId, tenantId, cancellationToken);
        var isAuthorized = requireManage
            ? HasRoleManagementCapability(permissions)
            : HasRoleViewCapability(permissions);
        return isAuthorized ? permissions : null;
    }

    private bool IsCurrentActorAndTenant(Guid actorId, Guid tenantId)
    {
        var principal = _httpContextAccessor.HttpContext?.User;
        return principal?.Identity?.IsAuthenticated == true
            && Guid.TryParse(principal.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var currentActorId)
            && currentActorId == actorId
            && Guid.TryParse(principal.FindFirst("tenantId")?.Value, out var currentTenantId)
            && currentTenantId == tenantId;
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
        IReadOnlySet<string> bootstrapDelegableKeys,
        bool allowOrdinaryDelegation,
        bool allowProtectedSystemAdminBaseline,
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
        var selectedKeys = permissions.Select(PermissionKey).ToHashSet(StringComparer.Ordinal);
        foreach (var permission in permissions)
        {
            var key = PermissionKey(permission);
            if (!PermissionCatalog.ByKey.TryGetValue(key, out var definition)
                || !definition.IsActive
                || !definition.IsAssignable
                || definition.Scope is PermissionScope.Platform or PermissionScope.CrossTenant or PermissionScope.SystemOwner)
                return Result<List<Permission>>.Failure(Error.Validation(
                    $"Permission {key} is inactive, uncatalogued, non-assignable, or reserved for platform-wide administration."));

            var isAdministrativePermission = IsAdministrativePermission(definition);
            var canDelegateAdministrative = isAdministrativePermission && actorPermissions.Contains(key);
            var canDelegateOrdinary = allowOrdinaryDelegation
                && !isAdministrativePermission
                && IsRoleAssignableScope(definition.Scope);
            var canDelegateActorOwnedOrdinary = !allowOrdinaryDelegation
                && !isAdministrativePermission
                && IsRoleAssignableScope(definition.Scope)
                && actorPermissions.Contains(key);
            var canUseBootstrapDelegation = !isAdministrativePermission
                && bootstrapDelegableKeys.Contains(key)
                && (!allowProtectedSystemAdminBaseline
                    || BootstrapPermissionDelegationPolicy.CanAddToProtectedTenantAdminRole(key));
            if (!canDelegateAdministrative && !canDelegateOrdinary
                && !canDelegateActorOwnedOrdinary && !canUseBootstrapDelegation)
                return Result<List<Permission>>.Failure(Error.Forbidden(
                    isAdministrativePermission
                        ? "Administrative permissions can only be assigned within your own effective access."
                        : "This permission is not available for role delegation."));

            var missingDependencies = definition.RequiredPermissionKeys
                .Where(requiredKey => !selectedKeys.Contains(requiredKey))
                .ToArray();
            if (missingDependencies.Length > 0)
                return Result<List<Permission>>.Failure(Error.Validation(
                    $"Permission {key} requires its screen View permission: {string.Join(", ", missingDependencies)}"));
        }

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
        var effective = await GetActorEffectivePermissionsWithRoleReplacementAsync(
            actorId, tenantId, changedRoleId, replacementPermissions, cancellationToken);
        return HasRoleManagementCapability(effective);
    }

    private async Task<IReadOnlySet<string>> GetActorEffectivePermissionsWithRoleReplacementAsync(
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

        return PermissionCatalog.EnforceDependencies(effective);
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

    private static readonly IReadOnlySet<string> EmptyPermissionKeys = new HashSet<string>(StringComparer.Ordinal);

    private static bool CanManageRole(
        Role role,
        Guid tenantId,
        IReadOnlySet<string> actorPermissions,
        IReadOnlySet<string> bootstrapDelegableKeys) =>
        role.Permissions.All(permission =>
        {
            if (permission.TenantId != tenantId || permission.IsDeleted)
                return false;

            var key = PermissionKey(permission);
            if (!PermissionCatalog.ByKey.TryGetValue(key, out var definition)
                || !definition.IsActive
                || !definition.IsAssignable
                || !IsRoleAssignableScope(definition.Scope))
                return false;

            if (IsAdministrativePermission(definition))
                return actorPermissions.Contains(key);

            if (!role.IsSystem && CanConfigureRolePermissions(actorPermissions))
                return true;

            return actorPermissions.Contains(key) || bootstrapDelegableKeys.Contains(key);
        });

    private static bool IsAdministrativePermission(PermissionCatalogEntry definition) =>
        string.Equals(definition.ModuleId, "administration", StringComparison.OrdinalIgnoreCase);

    private static bool IsRoleAssignableScope(PermissionScope scope) =>
        scope is PermissionScope.Tenant
            or PermissionScope.Organization
            or PermissionScope.Portfolio
            or PermissionScope.Program
            or PermissionScope.Project;

    private static RoleSummaryDto MapSummary(Role role, int assignedUserCount, Guid tenantId) => new()
    {
        Id = role.Id,
        Name = role.Name,
        Description = role.Description,
        Category = role.Category,
        IsSystem = role.IsSystem,
        IsProtectedSystemAdmin = ProtectedTenantAdminPolicy.IsProtectedSystemAdminRole(role),
        PermissionCount = role.Permissions.Count(permission => permission.TenantId == tenantId && IsActiveCatalogPermission(permission)),
        AssignedUserCount = assignedUserCount
    };

    private static RoleDetailDto MapDetail(Role role, int assignedUserCount, Guid tenantId) => new()
    {
        Id = role.Id,
        Name = role.Name,
        Description = role.Description,
        Category = role.Category,
        IsSystem = role.IsSystem,
        IsProtectedSystemAdmin = ProtectedTenantAdminPolicy.IsProtectedSystemAdminRole(role),
        PermissionCount = role.Permissions.Count(permission => permission.TenantId == tenantId && IsActiveCatalogPermission(permission)),
        AssignedUserCount = assignedUserCount,
        Permissions = role.Permissions
            .Where(permission => permission.TenantId == tenantId && IsActiveCatalogPermission(permission))
            .OrderBy(permission => permission.Resource, StringComparer.Ordinal)
            .ThenBy(permission => permission.Action, StringComparer.Ordinal)
            .Select(permission => MapPermission(permission))
            .ToList()
    };

    private static PermissionCatalogItemDto MapPermission(
        Permission permission,
        bool? canDelegate = null,
        bool? canDelegateToCustomRole = null)
    {
        var key = PermissionKey(permission);
        if (!PermissionCatalog.ByKey.TryGetValue(key, out var definition) || !definition.IsActive)
            throw new InvalidOperationException($"Role response attempted to expose uncatalogued or inactive permission '{key}'.");

        return new PermissionCatalogItemDto
        {
            Id = permission.Id,
            Name = definition.Key,
            Resource = permission.Resource,
            Action = permission.Action,
            Description = permission.Description,
            ModuleId = definition.ModuleId,
            ModuleNameEn = definition.ModuleNameEn,
            ModuleNameAr = definition.ModuleNameAr,
            ScreenId = definition.ScreenId,
            ScreenNameEn = definition.ScreenNameEn,
            ScreenNameAr = definition.ScreenNameAr,
            Scope = definition.Scope.ToString(),
            Level = definition.Level.ToString(),
            LabelEn = definition.LabelEn,
            LabelAr = definition.LabelAr,
            DescriptionEn = definition.DescriptionEn,
            DescriptionAr = definition.DescriptionAr,
            Sensitivity = definition.Sensitivity,
            IsSensitive = definition.IsSensitive,
            RequiredPermissionKeys = definition.RequiredPermissionKeys,            AllowedCategories = definition.AllowedCategories,
            IsActive = definition.IsActive,
            IsAssignable = definition.IsAssignable,
            CanDelegate = canDelegate,
            CanDelegateToCustomRole = canDelegateToCustomRole,
            IsMandatoryForSystemAdmin = ProtectedTenantAdminPolicy.MandatoryPermissionKeys.Contains(key)
        };
    }
    private static string PermissionKey(Permission permission) => $"{permission.Resource}.{permission.Action}";

    private static bool IsActiveCatalogPermission(Permission permission) =>
        !permission.IsDeleted
        && PermissionCatalog.ByKey.TryGetValue(PermissionKey(permission), out var definition)
        && definition.IsActive;

    private static bool CanPerformRoleOperation(IReadOnlySet<string> permissions, string operationPermission) =>
        permissions.Contains(operationPermission);

    // Granting permission sets is explicit through Roles.ConfigurePermissions.
    // Custom-role delegation covers ordinary scoped capabilities; Administration
    // module capabilities still require the actor's own effective permission.
    private static bool CanConfigureRolePermissions(IReadOnlySet<string> permissions) =>
        CanPerformRoleOperation(permissions, ConfigureRolePermissionsPermission);

    private static bool HasRoleManagementCapability(IReadOnlySet<string> permissions) =>
        CanPerformRoleOperation(permissions, CreateRolePermission)
        || CanPerformRoleOperation(permissions, EditRoleDetailsPermission)
        || CanConfigureRolePermissions(permissions)
        || permissions.Contains(DeleteRolePermission);

    private static bool HasRoleViewCapability(IReadOnlySet<string> permissions) =>
        permissions.Contains("Roles.View");

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
