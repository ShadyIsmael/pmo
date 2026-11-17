using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.Identity.Application;
using NEXUS.Modules.Identity.Domain;
using NEXUS.Modules.Organization.Domain;
using NEXUS.SharedKernel;
using OrganizationEntity = NEXUS.Modules.Organization.Domain.Organization;

namespace NEXUS.Modules.Organization.Application;

public sealed class TenantProvisioningService : ITenantProvisioningService
{
    private readonly DbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuditService _audit;

    public TenantProvisioningService(
        DbContext context,
        IHttpContextAccessor httpContextAccessor,
        IAuditService audit,
        IEnumerable<IOrganizationLifecycleHandler> organizationLifecycleHandlers)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
        _audit = audit;
        _organizationLifecycleHandlers = organizationLifecycleHandlers;
    }

    private readonly IEnumerable<IOrganizationLifecycleHandler> _organizationLifecycleHandlers;

    public async Task<IReadOnlyList<TenantSummary>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await _context.Set<Tenant>().AsNoTracking()
            .OrderBy(tenant => tenant.DisplayName)
            .Select(tenant => new TenantSummary(tenant.Id, tenant.Code, tenant.DisplayName, tenant.Status, tenant.CreatedAt))
            .ToListAsync(cancellationToken);

    public async Task<TenantProvisioningOperationResult> CreateAsync(
        CreateTenantRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = Validate(request, out var normalizedCode, out var displayName, out var organizationName);
        if (validation is not null)
            return new TenantProvisioningOperationResult(TenantProvisioningCode.ValidationFailed, Error: validation);

        var actorId = GetActorId();
        if (actorId is null)
            return new TenantProvisioningOperationResult(TenantProvisioningCode.ValidationFailed, Error: "An authenticated platform actor is required.");

        var tenantId = request.TenantId ?? Guid.NewGuid();
        if (tenantId == Guid.Empty || (request.InitialAdministratorUserId.HasValue && !request.TenantId.HasValue))
            return new TenantProvisioningOperationResult(TenantProvisioningCode.ValidationFailed,
                Error: "A non-empty tenant ID must be supplied when assigning a pre-provisioned initial administrator.");

        if (await _context.Set<Tenant>().IgnoreQueryFilters().AnyAsync(tenant => tenant.Id == tenantId, cancellationToken))
            return new TenantProvisioningOperationResult(TenantProvisioningCode.ValidationFailed, Error: "The tenant ID is already registered.");
        if (await _context.Set<Tenant>().IgnoreQueryFilters().AnyAsync(tenant => tenant.Code == normalizedCode, cancellationToken))
            return new TenantProvisioningOperationResult(TenantProvisioningCode.DuplicateCode, Error: "The tenant code is already in use.");

        var now = DateTime.UtcNow;
        var tenant = new Tenant
        {
            Id = tenantId,
            Code = normalizedCode,
            DisplayName = displayName,
            Status = TenantLifecycleStatus.Provisioning,
            CreatedAt = now,
            CreatedBy = actorId.Value.ToString("D")
        };
        var organization = new OrganizationEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = organizationName,
            IsActive = true,
            CreatedAt = now,
            CreatedBy = actorId.Value.ToString("D")
        };

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        _context.Set<Tenant>().Add(tenant);
        _context.Set<OrganizationEntity>().Add(organization);

        if (request.InitialAdministratorUserId is { } initialAdministratorId)
        {
            var assignResult = await AssignAdminWithinTransactionAsync(
                tenant, initialAdministratorId, actorId.Value, cancellationToken);
            if (assignResult is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return assignResult;
            }

            tenant.Status = TenantLifecycleStatus.Active;
            tenant.UpdatedAt = now;
            tenant.UpdatedBy = actorId.Value.ToString("D");
        }

        var auditResult = await AppendAuditAsync(
            tenant,
            actorId.Value,
            "Created",
            null,
            new { tenant.Id, tenant.Code, tenant.DisplayName, Status = tenant.Status.ToString(), OrganizationId = organization.Id },
            cancellationToken);
        if (auditResult is not null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return auditResult;
        }

        if (request.InitialAdministratorUserId is { } assignedUserId)
        {
            auditResult = await AppendAuditAsync(
                tenant,
                actorId.Value,
                "AdministratorAssigned",
                null,
                new { UserId = assignedUserId, Role = "admin" },
                cancellationToken);
            if (auditResult is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return auditResult;
            }
        }

try
            {
                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                await transaction.RollbackAsync(cancellationToken);
                if (await _context.Set<Tenant>().IgnoreQueryFilters().AnyAsync(item => item.Code == normalizedCode, cancellationToken))
                    return new TenantProvisioningOperationResult(TenantProvisioningCode.DuplicateCode, Error: "The tenant code is already in use.");
                throw;
            }

            // Notify lifecycle handlers (e.g., Pre-Initiation phase provisioning)
            foreach (var handler in _organizationLifecycleHandlers)
            {
                try
                {
                    await handler.HandleOrganizationCreatedAsync(tenantId, organization.Id, actorId.Value, cancellationToken);
                }
                catch
                {
                    // Log but don't fail tenant creation if handler fails
                    // The operation can be retried or recovered later
                }
            }

            return new TenantProvisioningOperationResult(
                TenantProvisioningCode.Created,
                new TenantProvisioningResult(tenant.Id, organization.Id, tenant.Status,
                    request.InitialAdministratorUserId.HasValue ? "admin" : null));
        }

    public async Task<TenantProvisioningOperationResult> AssignAdministratorAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty || userId == Guid.Empty)
            return new TenantProvisioningOperationResult(TenantProvisioningCode.ValidationFailed, Error: "Tenant and user IDs are required.");
        var actorId = GetActorId();
        if (actorId is null)
            return new TenantProvisioningOperationResult(TenantProvisioningCode.ValidationFailed, Error: "An authenticated platform actor is required.");

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        var tenant = await _context.Set<Tenant>().IgnoreQueryFilters()
            .SingleOrDefaultAsync(item => item.Id == tenantId, cancellationToken);
        if (tenant is null)
            return new TenantProvisioningOperationResult(TenantProvisioningCode.TenantNotFound);
        if (tenant.Status == TenantLifecycleStatus.Retired)
            return new TenantProvisioningOperationResult(TenantProvisioningCode.ValidationFailed, Error: "A retired tenant cannot be activated.");

        var assignmentResult = await AssignAdminWithinTransactionAsync(tenant, userId, actorId.Value, cancellationToken);
        if (assignmentResult is not null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return assignmentResult;
        }

        var now = DateTime.UtcNow;
        tenant.Status = TenantLifecycleStatus.Active;
        tenant.UpdatedAt = now;
        tenant.UpdatedBy = actorId.Value.ToString("D");
        var auditResult = await AppendAuditAsync(
            tenant,
            actorId.Value,
            "AdministratorAssigned",
            new { Status = "Provisioning" },
            new { Status = tenant.Status.ToString(), UserId = userId, Role = "admin" },
            cancellationToken);
        if (auditResult is not null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return auditResult;
        }

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new TenantProvisioningOperationResult(TenantProvisioningCode.ConcurrencyConflict,
                Error: "The tenant changed during provisioning. Reload and retry.");
        }

        return new TenantProvisioningOperationResult(
            TenantProvisioningCode.Activated,
            new TenantProvisioningResult(tenant.Id,
                await _context.Set<OrganizationEntity>().IgnoreQueryFilters()
                    .Where(organization => organization.TenantId == tenantId && !organization.IsDeleted)
                    .Select(organization => organization.Id).FirstAsync(cancellationToken),
                tenant.Status,
                "admin"));
    }

    public async Task<TenantProvisioningOperationResult> ChangeLifecycleAsync(
        Guid tenantId,
        TenantLifecycleStatus status,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty || status is not (TenantLifecycleStatus.Suspended or TenantLifecycleStatus.Retired))
            return new TenantProvisioningOperationResult(TenantProvisioningCode.InvalidLifecycleTransition,
                Error: "Platform lifecycle operations may suspend or retire a tenant. Activation requires administrator assignment.");
        var actorId = GetActorId();
        if (actorId is null)
            return new TenantProvisioningOperationResult(TenantProvisioningCode.ValidationFailed, Error: "An authenticated platform actor is required.");

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        var tenant = await _context.Set<Tenant>().SingleOrDefaultAsync(item => item.Id == tenantId, cancellationToken);
        if (tenant is null)
            return new TenantProvisioningOperationResult(TenantProvisioningCode.TenantNotFound);
        if (tenant.Status == TenantLifecycleStatus.Retired || tenant.Status == status)
            return new TenantProvisioningOperationResult(TenantProvisioningCode.InvalidLifecycleTransition,
                Error: "A retired tenant is terminal, and the requested status is already set.");

        var previousStatus = tenant.Status;
        var now = DateTime.UtcNow;
        tenant.Status = status;
        tenant.UpdatedAt = now;
        tenant.UpdatedBy = actorId.Value.ToString("D");
        var auditResult = await AppendAuditAsync(
            tenant,
            actorId.Value,
            "LifecycleChanged",
            new { Status = previousStatus.ToString() },
            new { Status = status.ToString() },
            cancellationToken);
        if (auditResult is not null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return auditResult;
        }

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new TenantProvisioningOperationResult(TenantProvisioningCode.ConcurrencyConflict,
                Error: "The tenant changed during the lifecycle update. Reload and retry.");
        }

        return new TenantProvisioningOperationResult(
            TenantProvisioningCode.StatusChanged,
            new TenantProvisioningResult(tenant.Id,
                await _context.Set<OrganizationEntity>().IgnoreQueryFilters()
                    .Where(organization => organization.TenantId == tenantId && !organization.IsDeleted)
                    .Select(organization => organization.Id).FirstOrDefaultAsync(cancellationToken),
                tenant.Status));
    }

    private async Task<TenantProvisioningOperationResult?> AssignAdminWithinTransactionAsync(
        Tenant tenant,
        Guid userId,
        Guid actorId,
        CancellationToken cancellationToken)
    {
        var user = await _context.Set<User>().IgnoreQueryFilters()
            .SingleOrDefaultAsync(item => item.Id == userId && item.TenantId == tenant.Id && !item.IsDeleted && item.IsActive,
                cancellationToken);
        if (user is null)
            return new TenantProvisioningOperationResult(TenantProvisioningCode.AdministratorNotFound,
                Error: "The initial administrator must be an active, pre-provisioned user belonging to this tenant.");

        var adminRoles = await _context.Set<Role>().IgnoreQueryFilters()
            .Include(role => role.Permissions)
            .Where(role => role.TenantId == tenant.Id
                && !role.IsDeleted
                && (role.RoleKey == Role.TenantAdminRoleKey
                    || role.SystemRoleKey == Role.TenantAdminRoleKey))
            .ToListAsync(cancellationToken);
        if (adminRoles.Count > 1 || adminRoles.Any(role =>
                !ProtectedTenantAdminPolicy.IsProtectedSystemAdminRole(role)))
            return new TenantProvisioningOperationResult(TenantProvisioningCode.AdministratorRoleUnavailable,
                Error: "The tenant has an invalid or duplicate protected Admin role identity.");

        var adminRole = adminRoles.SingleOrDefault();
        var existingPermissions = await _context.Set<Permission>().IgnoreQueryFilters()
            .Where(permission => permission.TenantId == tenant.Id && !permission.IsDeleted)
            .ToListAsync(cancellationToken);
        var duplicatePermissionKeys = existingPermissions
            .GroupBy(permission => $"{permission.Resource}.{permission.Action}", StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();
        if (duplicatePermissionKeys.Length > 0)
            return new TenantProvisioningOperationResult(TenantProvisioningCode.AdministratorRoleUnavailable,
                Error: $"The tenant permission catalog contains duplicate keys: {string.Join(", ", duplicatePermissionKeys)}.");

        var permissionByKey = existingPermissions.ToDictionary(
            permission => $"{permission.Resource}.{permission.Action}", StringComparer.Ordinal);
        foreach (var definition in PermissionCatalog.Entries
                     .Where(entry => entry.IsActive)
                     .OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            if (permissionByKey.ContainsKey(definition.Key)) continue;

            var permission = new Permission
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                Name = definition.Key,
                Resource = definition.Resource,
                Action = definition.Action,
                Description = definition.DescriptionEn,
                CreatedBy = actorId.ToString("D")
            };
            permissionByKey.Add(definition.Key, permission);
            _context.Set<Permission>().Add(permission);
        }

        if (adminRole is null)
        {
            adminRole = new Role
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                Name = "admin",
                Description = "Tenant administrator",
                Category = "System",
                IsSystem = true,
                CreatedBy = actorId.ToString("D"),
                Permissions = []
            };
            adminRole.AssignSystemRoleKey(Role.TenantAdminSystemRoleKey, tenant.Id);
            _context.Set<Role>().Add(adminRole);
        }
        // Existing Admin grants are left untouched. New catalog entries are never
        // added to an existing Admin role merely because they are grantable.

        var existingAssignment = await _context.Set<UserRole>().IgnoreQueryFilters()
            .AnyAsync(assignment => assignment.TenantId == tenant.Id && assignment.UserId == userId
                && assignment.RoleId == adminRole.Id && !assignment.IsDeleted, cancellationToken);
        if (!existingAssignment)
        {
            _context.Set<UserRole>().Add(new UserRole
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                UserId = userId,
                RoleId = adminRole.Id,
                CreatedBy = actorId.ToString("D")
            });
        }

        return null;
    }

    private async Task<TenantProvisioningOperationResult?> AppendAuditAsync(
        Tenant tenant,
        Guid actorId,
        string action,
        object? original,
        object updated,
        CancellationToken cancellationToken)
    {
        var entry = new AuditEntry
        {
            TenantId = tenant.Id,
            EntityType = nameof(Tenant),
            EntityId = tenant.Id,
            Action = action,
            ChangedFields = action == "Created" ? "Code,DisplayName,Status,Organization" : "Administrator,Status",
            OriginalValues = original is null ? null : JsonSerializer.Serialize(original),
            NewValues = JsonSerializer.Serialize(updated),
            UserId = actorId,
            CreatedBy = actorId.ToString("D"),
            Source = AuditSources.User
        };
        var result = await _audit.AppendAsync(entry, cancellationToken);
        return result.IsFailure
            ? new TenantProvisioningOperationResult(TenantProvisioningCode.AuditUnavailable,
                Error: result.Error?.Message ?? "The tenant audit record could not be staged.")
            : null;
    }

    private Guid? GetActorId()
    {
        var principal = _httpContextAccessor.HttpContext?.User;
        var value = principal?.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal?.FindFirstValue("sub");
        return Guid.TryParse(value, out var actorId) && actorId != Guid.Empty ? actorId : null;
    }

    private static string? Validate(
        CreateTenantRequest request,
        out string normalizedCode,
        out string displayName,
        out string organizationName)
    {
        normalizedCode = (request.Code ?? string.Empty).Trim().ToUpperInvariant();
        displayName = (request.DisplayName ?? string.Empty).Trim();
        organizationName = (request.InitialOrganizationName ?? string.Empty).Trim();
        if (normalizedCode.Length is < 2 or > 64
            || normalizedCode.Any(character => !(char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.'))
            || !char.IsAsciiLetterOrDigit(normalizedCode[0]))
            return "Tenant code must start with a letter or number and contain only letters, numbers, dots, underscores, or hyphens.";
        if (displayName.Length is < 2 or > 200) return "Tenant display name must contain 2 to 200 characters.";
        if (organizationName.Length is < 2 or > 200) return "Initial organization name must contain 2 to 200 characters.";
        return null;
    }
}
