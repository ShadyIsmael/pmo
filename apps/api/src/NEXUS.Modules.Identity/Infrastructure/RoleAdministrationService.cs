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
    private readonly IUserOrganizationDirectory? _organizationDirectory;
    private readonly IPermissionProtectionPolicy _permissionProtection;

    public RoleAdministrationService(
        DbContext context,
        IUserEffectivePermissionService effectivePermissions,
        IAuditService auditService,
        IHttpContextAccessor httpContextAccessor,
        IUserOrganizationDirectory? organizationDirectory = null,
        IPermissionProtectionPolicy? permissionProtection = null)
    {
        _context = context;
        _effectivePermissions = effectivePermissions;
        _auditService = auditService;
        _httpContextAccessor = httpContextAccessor;
        _organizationDirectory = organizationDirectory;
        _permissionProtection = permissionProtection ?? PermissionProtectionPolicy.Empty;
    }

    public async Task<Result<IReadOnlyList<RoleSummaryDto>>> GetAssignableRolesAsync(
        Guid actorId,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        if (!IsCurrentActorAndTenant(actorId, tenantId))
            return Result<IReadOnlyList<RoleSummaryDto>>.Failure(Error.Forbidden("A verified Tenant actor is required."));
        var roles = await _context.Set<Role>().IgnoreQueryFilters().AsNoTracking()
            .Where(role => role.TenantId == tenantId && !role.IsDeleted)
            .Include(role => role.Permissions)
            .OrderBy(role => role.Name)
            .ThenBy(role => role.Id)
            .ToListAsync(cancellationToken);
        var organizationIds = roles.Where(role => role.OrganizationId.HasValue)
            .Select(role => role.OrganizationId!.Value).Distinct().ToArray();
        if (organizationIds.Length > 0 && _organizationDirectory is null)
            return Result<IReadOnlyList<RoleSummaryDto>>.Failure(new Error(
                "ORGANIZATION_LOOKUP_UNAVAILABLE", "Organization lookup is unavailable.", StatusCodes.Status503ServiceUnavailable));
        var organizationNames = organizationIds.Length == 0
            ? new Dictionary<Guid, string>()
            : await _organizationDirectory!.GetActiveNamesAsync(tenantId, organizationIds, cancellationToken);
        var result = new List<RoleSummaryDto>();

        foreach (var role in roles)
        {
            if (role.IsLegacyOrganizationOwnershipUnresolved
                || role.OrganizationId is Guid organizationId && !organizationNames.ContainsKey(organizationId))
                continue;

            var actorPermissions = role.OrganizationId is Guid scopedOrganizationId
                ? await _effectivePermissions.GetEffectivePermissionsForOrganizationAsync(actorId, tenantId, scopedOrganizationId, cancellationToken)
                : await _effectivePermissions.GetEffectivePermissionsAsync(actorId, tenantId, cancellationToken);
            if (!actorPermissions.Contains("Users.AssignRoles"))
                continue;
            var failure = RoleAssignmentAuthorizationPolicy.Evaluate(
                [role], actorId, tenantId, targetUserId: null, actorPermissions, _permissionProtection);
            if (failure is not null)
                continue;

            organizationNames.TryGetValue(role.OrganizationId ?? Guid.Empty, out var organizationName);
            result.Add(MapSummary(role, 0, tenantId, organizationName, isAssignable: true));
        }

        return Result<IReadOnlyList<RoleSummaryDto>>.Success(result);
    }

    public async Task<Result<IReadOnlyList<RoleOrganizationOptionDto>>> GetManagedRoleOrganizationsAsync(
        Guid actorId,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        if (!IsCurrentActorAndTenant(actorId, tenantId))
            return Result<IReadOnlyList<RoleOrganizationOptionDto>>.Failure(Error.Forbidden("A verified Tenant actor is required."));
        if (_organizationDirectory is null)
            return Result<IReadOnlyList<RoleOrganizationOptionDto>>.Failure(new Error(
                "ORGANIZATION_LOOKUP_UNAVAILABLE", "Organization lookup is unavailable.", StatusCodes.Status503ServiceUnavailable));

        var organizations = await _organizationDirectory.GetAssignableOptionsAsync(
            tenantId, authorizedOrganizationIds: null, cancellationToken);
        var managed = new List<RoleOrganizationOptionDto>();
        foreach (var organization in organizations)
        {
            var permissions = await _effectivePermissions.GetEffectivePermissionsForOrganizationAsync(
                actorId, tenantId, organization.Id, cancellationToken);
            if (HasRoleViewCapability(permissions) || HasRoleManagementCapability(permissions))
                managed.Add(new RoleOrganizationOptionDto { Id = organization.Id, Name = organization.Name });
        }

        return Result<IReadOnlyList<RoleOrganizationOptionDto>>.Success(managed);
    }

    public async Task<Result<IReadOnlyList<RoleSummaryDto>>> GetManageableRolesAsync(
        Guid actorId,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        if (!IsCurrentActorAndTenant(actorId, tenantId))
            return Result<IReadOnlyList<RoleSummaryDto>>.Failure(Error.Forbidden("A verified Tenant actor is required."));

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

        var organizationNames = _organizationDirectory is null
            ? new Dictionary<Guid, string>()
            : await _organizationDirectory.GetActiveNamesAsync(
                tenantId, roles.Where(role => role.OrganizationId.HasValue).Select(role => role.OrganizationId!.Value), cancellationToken);
        var manageableRoles = new List<RoleSummaryDto>();
        foreach (var role in roles)
        {
            var actorPermissions = await GetEffectivePermissionsForRoleAsync(actorId, tenantId, role, cancellationToken);
            var canManageRoles = HasRoleManagementCapability(actorPermissions);
            if (!HasRoleViewCapability(actorPermissions) && !canManageRoles)
                continue;
            var canManageRole = canManageRoles && CanManageRole(role, tenantId, actorPermissions);
            if (canManageRoles && !canManageRole)
                continue;
            organizationNames.TryGetValue(role.OrganizationId ?? Guid.Empty, out var organizationName);
            manageableRoles.Add(MapSummary(
                role, role.UserRoles.Count, tenantId, organizationName,
                actorPermissions: actorPermissions, canManageRole: canManageRole));
        }

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
        var permissions = await _context.Set<Permission>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(permission => permission.TenantId == tenantId && !permission.IsDeleted)
            .OrderBy(permission => permission.Resource)
            .ThenBy(permission => permission.Action)
            .ToListAsync(cancellationToken);

        var catalogItems = new List<PermissionCatalogItemDto>();
        foreach (var permission in permissions)
        {
            var key = PermissionKey(permission);
            if (!PermissionCatalog.ByKey.TryGetValue(key, out var definition)
                || !definition.IsActive
                || definition.Scope is PermissionScope.Platform or PermissionScope.CrossTenant or PermissionScope.SystemOwner)
                continue;

            var canDelegate = _permissionProtection.IsGrantable(definition)
                && IsRoleAssignableScope(definition.Scope);
            var canDelegateToCustomRole = canDelegate;
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
        if (!IsCurrentActorAndTenant(actorId, tenantId))
            return Result<RoleDetailDto>.Failure(Error.Forbidden("A verified Tenant actor is required."));

        var role = await LoadRoleAsync(id, tenantId, tracked: false, cancellationToken);
        if (role is null)
            return Result<RoleDetailDto>.Failure(Error.NotFound("Role not found"));
        var actorPermissions = await GetEffectivePermissionsForRoleAsync(actorId, tenantId, role, cancellationToken);
        if (!HasRoleViewCapability(actorPermissions) && !HasRoleManagementCapability(actorPermissions))
            return Result<RoleDetailDto>.Failure(Error.Forbidden("You do not have permission to view this role"));
        var canManageRole = HasRoleManagementCapability(actorPermissions)
            && CanManageRole(role, tenantId, actorPermissions);
        if (HasRoleManagementCapability(actorPermissions) && !canManageRole)
            return Result<RoleDetailDto>.Failure(Error.Forbidden("You cannot manage a role with permissions outside your own effective access"));

        return Result<RoleDetailDto>.Success(MapDetail(
            role, await GetAssignedUserCountAsync(id, tenantId, cancellationToken), tenantId,
            await GetOrganizationNameAsync(role.OrganizationId, tenantId, cancellationToken),
            actorPermissions, canManageRole));
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

        if (!IsCurrentActorAndTenant(actorId, tenantId))
            return Result<RoleDetailDto>.Failure(Error.Forbidden("A verified Tenant actor is required."));
        if (dto.OrganizationId == Guid.Empty)
            return Result<RoleDetailDto>.Failure(Error.Validation("A valid Organization is required when the Role is Organization-owned."));
        var organizationId = dto.OrganizationId;
        string? organizationName = null;
        if (organizationId.HasValue)
        {
            if (_organizationDirectory is null)
                return Result<RoleDetailDto>.Failure(new Error(
                    "ORGANIZATION_LOOKUP_UNAVAILABLE", "Organization lookup is unavailable.", StatusCodes.Status503ServiceUnavailable));
            var organizationNames = await _organizationDirectory.GetActiveNamesAsync(tenantId, [organizationId.Value], cancellationToken);
            if (!organizationNames.TryGetValue(organizationId.Value, out organizationName))
                return Result<RoleDetailDto>.Failure(Error.NotFound("The requested Organization is unavailable."));
        }

        var actorPermissions = await GetEffectivePermissionsForOrganizationScopeAsync(
            actorId, tenantId, organizationId, cancellationToken);
        var category = NormalizeOptional(dto.Category);
        if (!RoleCategories.IsSelectable(category))
            return Result<RoleDetailDto>.Failure(Error.Validation(
                $"A custom role category must be one of: {string.Join(", ", RoleCategories.Selectable)}. System is reserved for protected system roles."));
        if (!CanPerformRoleOperation(actorPermissions, CreateRolePermission))
            return Result<RoleDetailDto>.Failure(Error.Forbidden("Roles.Create is required."));

        // Revalidate the actor's scope-specific grant while holding the same
        // tenant mutation transaction used for role and user access changes.
        await using var tenantMutation = await TenantAdminMutationGuard.AcquireAsync(_context, tenantId, cancellationToken);
        actorPermissions = await GetEffectivePermissionsForOrganizationScopeAsync(
            actorId, tenantId, organizationId, cancellationToken);
        if (!CanPerformRoleOperation(actorPermissions, CreateRolePermission))
            return Result<RoleDetailDto>.Failure(Error.Forbidden("Roles.Create is no longer effective in this Role scope."));
        if (dto.PermissionIds is { Count: > 0 }
            && !CanConfigureRolePermissions(actorPermissions))
            return Result<RoleDetailDto>.Failure(Error.Forbidden("Roles.ConfigurePermissions is required to assign permissions while creating a role."));
        var name = dto.Name.Trim();
        if (string.Equals(name, "admin", StringComparison.OrdinalIgnoreCase))
            return Result<RoleDetailDto>.Failure(Error.Conflict("The protected system Admin role name is reserved."));
        if (await RoleNameExistsAsync(name, tenantId, organizationId, null, cancellationToken))
            return Result<RoleDetailDto>.Failure(Error.Conflict("A role with this name already exists"));

        var permissionsResult = await ResolvePermissionsAsync(
            dto.PermissionIds, tenantId, organizationOwned: organizationId.HasValue, cancellationToken);
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
        if (organizationId is Guid ownerOrganizationId)
            role.AssignOrganization(ownerOrganizationId);

        _context.Set<Role>().Add(role);
        await AppendAuditAsync(
            role.Id,
            "Created",
            null,
            new { role.Name, role.Description, role.Category, role.OrganizationId, Permissions = role.Permissions.Select(PermissionKey).OrderBy(key => key).ToArray() },
            actorId,
            tenantId,
            cancellationToken);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            await tenantMutation.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Result<RoleDetailDto>.Failure(Error.Conflict("The role could not be saved because its name is already in use or another update conflicted"));
        }

        return Result<RoleDetailDto>.Success(MapDetail(
            role, 0, tenantId, organizationName, actorPermissions,
            CanManageRole(role, tenantId, actorPermissions)));
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

        if (!IsCurrentActorAndTenant(actorId, tenantId))
            return Result<RoleDetailDto>.Failure(Error.Forbidden("A verified Tenant actor is required."));
        var role = await LoadRoleAsync(id, tenantId, tracked: false, cancellationToken);
        if (role is null)
            return Result<RoleDetailDto>.Failure(Error.NotFound("Role not found"));
        var actorPermissions = await GetEffectivePermissionsForRoleAsync(actorId, tenantId, role, cancellationToken);
        if (!HasRoleManagementCapability(actorPermissions))
            return Result<RoleDetailDto>.Failure(Error.Forbidden("You do not have permission to manage roles"));
        await using var tenantAdminMutation = await TenantAdminMutationGuard.AcquireAsync(_context, tenantId, cancellationToken);
        var hadActiveTenantAdmin = await tenantAdminMutation.HasActiveTenantAdminAsync(tenantId, cancellationToken);

        role = await LoadRoleAsync(id, tenantId, tracked: true, cancellationToken);
        if (role is null)
            return Result<RoleDetailDto>.Failure(Error.NotFound("Role not found"));
        if (dto.OrganizationId.HasValue && dto.OrganizationId != role.OrganizationId)
            return Result<RoleDetailDto>.Failure(Error.Conflict("A Role's Organization is immutable once it has been created."));
        var roleOrganizationName = await GetOrganizationNameAsync(role.OrganizationId, tenantId, cancellationToken);
        actorPermissions = await GetEffectivePermissionsForRoleAsync(actorId, tenantId, role, cancellationToken);
        if (!HasRoleManagementCapability(actorPermissions))
            return Result<RoleDetailDto>.Failure(Error.Forbidden("Your role management permission is no longer effective."));
        if (!CanManageRole(role, tenantId, actorPermissions))
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
        if (await RoleNameExistsAsync(name, tenantId, role.OrganizationId, id, cancellationToken))
            return Result<RoleDetailDto>.Failure(Error.Conflict("A role with this name already exists"));

        var oldPermissions = role.Permissions.Select(PermissionKey).OrderBy(key => key, StringComparer.Ordinal).ToArray();
        var permissionsResult = await ResolvePermissionsAsync(
            dto.PermissionIds, tenantId, organizationOwned: role.OrganizationId.HasValue, cancellationToken);
        if (permissionsResult.IsFailure)
            return Result<RoleDetailDto>.Failure(permissionsResult.Error!);

        var nextPermissions = permissionsResult.Value!;
        var actorIsAssignedToRole = role.UserRoles.Any(assignment =>
            assignment.UserId == actorId && assignment.TenantId == tenantId && !assignment.IsDeleted);
        if (!role.IsSystem && actorIsAssignedToRole)
        {
            var effectiveAfterRoleUpdate = await GetActorEffectivePermissionsWithRoleReplacementAsync(
                actorId, tenantId, role.Id, role.OrganizationId, nextPermissions, cancellationToken);
            if (effectiveAfterRoleUpdate.Except(actorPermissions, StringComparer.Ordinal).Any())
                return Result<RoleDetailDto>.Failure(Error.Forbidden(
                    "A role assigned to you cannot add permissions outside your current effective access."));
        }
        if (!await ActorRetainsRoleManagementAsync(actorId, tenantId, role.Id, role.OrganizationId, nextPermissions, cancellationToken))
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
            return Result<RoleDetailDto>.Success(MapDetail(
                role, role.UserRoles.Count, tenantId, roleOrganizationName, actorPermissions, canManageRole: true));

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
            if (hadActiveTenantAdmin
                && !await tenantAdminMutation.HasActiveTenantAdminAsync(tenantId, cancellationToken))
            {
                await tenantAdminMutation.RollbackAsync(cancellationToken);
                _context.ChangeTracker.Clear();
                await AppendAuditAsync(
                    role.Id,
                    "TenantAdminMutationRejected",
                    originalValues,
                    new { Rejected = true, Reason = "Last active Tenant Admin assignment would be removed" },
                    actorId,
                    tenantId,
                    cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
                return Result<RoleDetailDto>.Failure(Error.Conflict("This change would remove the last active Tenant Admin assignment."));
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

        return Result<RoleDetailDto>.Success(MapDetail(
            role, role.UserRoles.Count, tenantId, roleOrganizationName, actorPermissions, canManageRole: true));
    }

    public async Task<Result> DeleteAsync(
        Guid id,
        Guid actorId,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        if (!IsCurrentActorAndTenant(actorId, tenantId))
            return Result.Failure(Error.Forbidden("A verified Tenant actor is required."));
        var role = await LoadRoleAsync(id, tenantId, tracked: true, cancellationToken);
        if (role is null)
            return Result.Failure(Error.NotFound("Role not found"));
        var actorPermissions = await GetEffectivePermissionsForRoleAsync(actorId, tenantId, role, cancellationToken);
        if (!actorPermissions.Contains(DeleteRolePermission))
            return Result.Failure(Error.Forbidden("Roles.Delete is required in this Organization."));
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

    private async Task<IReadOnlySet<string>> GetEffectivePermissionsForRoleAsync(
        Guid actorId,
        Guid tenantId,
        Role role,
        CancellationToken cancellationToken)
    {
        if (role.IsLegacyOrganizationOwnershipUnresolved)
            return EmptyPermissionKeys;
        if (role.OrganizationId is not Guid organizationId)
            return await _effectivePermissions.GetEffectivePermissionsAsync(actorId, tenantId, cancellationToken);
        if (_organizationDirectory is null
            || (await _organizationDirectory.GetActiveNamesAsync(tenantId, [organizationId], cancellationToken)).Count != 1)
            return EmptyPermissionKeys;
        return await _effectivePermissions.GetEffectivePermissionsForOrganizationAsync(
            actorId, tenantId, organizationId, cancellationToken);
    }

    private Task<IReadOnlySet<string>> GetEffectivePermissionsForOrganizationScopeAsync(
        Guid actorId,
        Guid tenantId,
        Guid? organizationId,
        CancellationToken cancellationToken) => organizationId is Guid scopedOrganizationId
            ? _effectivePermissions.GetEffectivePermissionsForOrganizationAsync(actorId, tenantId, scopedOrganizationId, cancellationToken)
            : _effectivePermissions.GetEffectivePermissionsAsync(actorId, tenantId, cancellationToken);

    private async Task<string?> GetOrganizationNameAsync(
        Guid? organizationId,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        if (organizationId is not Guid id || _organizationDirectory is null)
            return null;
        var names = await _organizationDirectory.GetActiveNamesAsync(tenantId, [id], cancellationToken);
        return names.TryGetValue(id, out var name) ? name : null;
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
        bool organizationOwned,
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
                || definition.Scope is PermissionScope.Platform or PermissionScope.CrossTenant or PermissionScope.SystemOwner)
                return Result<List<Permission>>.Failure(Error.Validation(
                    $"Permission {key} is inactive, uncatalogued, or reserved for platform-wide administration."));
            if (!_permissionProtection.IsGrantable(definition))
                return Result<List<Permission>>.Failure(Error.Validation(
                    $"Permission {key} is protected by Product Owner policy and cannot be assigned."));
            if (organizationOwned && !OrganizationRolePermissionPolicy.CanGrant(definition, _permissionProtection))
                return Result<List<Permission>>.Failure(Error.Validation(
                    $"Permission {key} is outside the grantable scope for an Organization-owned Role."));
            if (!IsRoleAssignableScope(definition.Scope))
                return Result<List<Permission>>.Failure(Error.Validation(
                    $"Permission {key} cannot be assigned to a Tenant- or Organization-owned Role."));

            var missingDependencies = definition.RequiredPermissionKeys
                .Where(requiredKey => !selectedKeys.Contains(requiredKey))
                .ToArray();
            if (missingDependencies.Length > 0)
                return Result<List<Permission>>.Failure(Error.Validation(
                    $"Permission {key} requires its screen View permission: {string.Join(", ", missingDependencies)}"));
        }

        return Result<List<Permission>>.Success(permissions);
    }

    private async Task<bool> RoleNameExistsAsync(string name, Guid tenantId, Guid? organizationId, Guid? exceptRoleId, CancellationToken cancellationToken)
    {
        var normalizedName = name.ToUpperInvariant();
        return await _context.Set<Role>()
            .IgnoreQueryFilters()
            .AnyAsync(role => role.TenantId == tenantId && !role.IsDeleted
                && role.Id != (exceptRoleId ?? Guid.Empty)
                && role.OrganizationId == organizationId
                && role.Name.ToUpper() == normalizedName, cancellationToken);
    }

    private async Task<bool> ActorRetainsRoleManagementAsync(
        Guid actorId,
        Guid tenantId,
        Guid changedRoleId,
        Guid? organizationId,
        IReadOnlyCollection<Permission> replacementPermissions,
        CancellationToken cancellationToken)
    {
        var effective = await GetActorEffectivePermissionsWithRoleReplacementAsync(
            actorId, tenantId, changedRoleId, organizationId, replacementPermissions, cancellationToken);
        return HasRoleManagementCapability(effective);
    }

    private async Task<IReadOnlySet<string>> GetActorEffectivePermissionsWithRoleReplacementAsync(
        Guid actorId,
        Guid tenantId,
        Guid changedRoleId,
        Guid? organizationId,
        IReadOnlyCollection<Permission> replacementPermissions,
        CancellationToken cancellationToken)
    {
        var assignments = await _context.Set<UserRole>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(assignment => assignment.UserId == actorId && assignment.TenantId == tenantId && !assignment.IsDeleted
                && assignment.User.TenantId == tenantId && !assignment.User.IsDeleted && assignment.User.IsActive
                && assignment.Role.TenantId == tenantId && !assignment.Role.IsDeleted
                && !assignment.Role.IsLegacyOrganizationOwnershipUnresolved
                && (assignment.Role.OrganizationId == null || assignment.Role.OrganizationId == organizationId))
            .Include(assignment => assignment.Role)
                .ThenInclude(role => role.Permissions)
            .ToListAsync(cancellationToken);

        var inherited = assignments
            .SelectMany(assignment => assignment.RoleId == changedRoleId
                ? replacementPermissions.AsEnumerable()
                : assignment.Role.Permissions.AsEnumerable())
            .Where(permission => permission.TenantId == tenantId && !permission.IsDeleted)
            .Select(PermissionKey)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var overrides = await _context.Set<UserPermissionOverride>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(item => item.UserId == actorId && item.TenantId == tenantId && !item.IsDeleted
                && item.Permission.TenantId == tenantId && !item.Permission.IsDeleted)
            .Select(item => new { Permission = PermissionKey(item.Permission), item.Effect })
            .ToListAsync(cancellationToken);

        var effectiveOverrides = overrides.Where(item =>
            item.Effect == PermissionOverrideEffect.Deny
            || (PermissionCatalog.ByKey.TryGetValue(item.Permission, out var definition)
                && definition.Scope == PermissionScope.Tenant));
        return PermissionCatalog.EnforceDependencies(EffectivePermissionResolver.Resolve(
            inherited,
            effectiveOverrides.Select(item => (item.Permission, item.Effect))));
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

    private bool CanManageRole(
        Role role,
        Guid tenantId,
        IReadOnlySet<string> actorPermissions) =>
        role.Permissions.All(permission =>
        {
            if (permission.TenantId != tenantId || permission.IsDeleted)
                return false;

            var key = PermissionKey(permission);
            if (!PermissionCatalog.ByKey.TryGetValue(key, out var definition)
                || !definition.IsActive
                || !IsRoleAssignableScope(definition.Scope))
                return false;
            return _permissionProtection.IsGrantable(definition)
                || actorPermissions.Contains(key);
        });

    private static bool IsRoleAssignableScope(PermissionScope scope) =>
        scope is PermissionScope.Tenant
            or PermissionScope.Organization
            or PermissionScope.Portfolio
            or PermissionScope.Program
            or PermissionScope.Project;

    private static RoleSummaryDto MapSummary(
        Role role,
        int assignedUserCount,
        Guid tenantId,
        string? organizationName = null,
        bool isAssignable = false,
        IReadOnlySet<string>? actorPermissions = null,
        bool canManageRole = false) => new()
    {
        Id = role.Id,
        OrganizationId = role.OrganizationId,
        OrganizationName = organizationName,
        IsOrganizationOwned = role.OrganizationId.HasValue,
        IsLegacyOrganizationOwnershipUnresolved = role.IsLegacyOrganizationOwnershipUnresolved,
        Scope = role.IsLegacyOrganizationOwnershipUnresolved ? "Unresolved" : role.OrganizationId.HasValue ? "Organization" : "Tenant",
        RoleKey = role.RoleKey,
        Name = role.Name,
        Description = role.Description,
        Category = role.Category,
        IsSystem = role.IsSystem,
        IsProtected = ProtectedTenantAdminPolicy.IsProtectedSystemAdminRole(role)
            || ProtectedTenantAdminPolicy.IsOrganizationAdminSystemRole(role),
        IsProtectedSystemAdmin = ProtectedTenantAdminPolicy.IsProtectedSystemAdminRole(role),
        IsAssignable = isAssignable,
        CanEditDetails = canManageRole && actorPermissions?.Contains(EditRoleDetailsPermission) == true,
        CanManagePermissions = canManageRole && actorPermissions is not null && CanConfigureRolePermissions(actorPermissions),
        CanDelete = canManageRole && !role.IsSystem && actorPermissions?.Contains(DeleteRolePermission) == true,
        PermissionCount = role.Permissions.Count(permission => permission.TenantId == tenantId && IsActiveCatalogPermission(permission)),
        AssignedUserCount = assignedUserCount
    };

    private RoleDetailDto MapDetail(
        Role role,
        int assignedUserCount,
        Guid tenantId,
        string? organizationName = null,
        IReadOnlySet<string>? actorPermissions = null,
        bool canManageRole = false) => new()
    {
        Id = role.Id,
        OrganizationId = role.OrganizationId,
        OrganizationName = organizationName,
        IsOrganizationOwned = role.OrganizationId.HasValue,
        IsLegacyOrganizationOwnershipUnresolved = role.IsLegacyOrganizationOwnershipUnresolved,
        Scope = role.IsLegacyOrganizationOwnershipUnresolved ? "Unresolved" : role.OrganizationId.HasValue ? "Organization" : "Tenant",
        RoleKey = role.RoleKey,
        Name = role.Name,
        Description = role.Description,
        Category = role.Category,
        IsSystem = role.IsSystem,
        IsProtected = ProtectedTenantAdminPolicy.IsProtectedSystemAdminRole(role)
            || ProtectedTenantAdminPolicy.IsOrganizationAdminSystemRole(role),
        IsProtectedSystemAdmin = ProtectedTenantAdminPolicy.IsProtectedSystemAdminRole(role),
        PermissionCount = role.Permissions.Count(permission => permission.TenantId == tenantId && IsActiveCatalogPermission(permission)),
        AssignedUserCount = assignedUserCount,
        CanEditDetails = canManageRole && actorPermissions?.Contains(EditRoleDetailsPermission) == true,
        CanManagePermissions = canManageRole && actorPermissions is not null && CanConfigureRolePermissions(actorPermissions),
        CanDelete = canManageRole && !role.IsSystem && actorPermissions?.Contains(DeleteRolePermission) == true,
        Permissions = role.Permissions
            .Where(permission => permission.TenantId == tenantId && IsActiveCatalogPermission(permission))
            .OrderBy(permission => permission.Resource, StringComparer.Ordinal)
            .ThenBy(permission => permission.Action, StringComparer.Ordinal)
            .Select(permission => MapPermission(permission))
            .ToList()
    };

    private PermissionCatalogItemDto MapPermission(
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
            IsAssignable = _permissionProtection.IsGrantable(definition),
            CanGrantToOrganizationRole = OrganizationRolePermissionPolicy.CanGrant(definition, _permissionProtection),
            CanDelegate = canDelegate,
            CanDelegateToCustomRole = canDelegateToCustomRole
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
    // Per-permission actor possession and module/category restrictions do not
    // override that authenticated operation authority.
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
