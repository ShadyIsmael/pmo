using System.Text.Json;
using System.Security.Cryptography;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.Identity.Domain;
using NEXUS.Modules.ProjectManagement.Application.DTOs;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Helpers;
using NEXUS.SharedKernel.Management;

namespace NEXUS.Modules.ProjectManagement.Application;

public sealed class ProjectStakeholderService : IProjectStakeholderService
{
    private readonly DbContext _context;
    private readonly IUserReferenceLookup _userLookup;
    private readonly IResourceReferenceLookup _resourceLookup;
    private readonly IAuditService _auditService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IManagementContextAccessor? _managementContext;

    public ProjectStakeholderService(DbContext context, IUserReferenceLookup userLookup, IResourceReferenceLookup resourceLookup, IAuditService auditService, IHttpContextAccessor httpContextAccessor, IManagementContextAccessor? managementContext = null)
    {
        _context = context;
        _userLookup = userLookup;
        _resourceLookup = resourceLookup;
        _auditService = auditService;
        _httpContextAccessor = httpContextAccessor;
        _managementContext = managementContext;
    }

    public async Task<Result<IReadOnlyList<ProjectStakeholderDto>>> GetByProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var accessError = RequireProjectAccess(projectId, "Stakeholders.Read");
        if (accessError is not null)
            return Result<IReadOnlyList<ProjectStakeholderDto>>.Failure(accessError);
        if (!await ProjectExistsAsync(projectId, cancellationToken))
            return Result<IReadOnlyList<ProjectStakeholderDto>>.Failure(Error.NotFound("Project not found"));

        var entities = await _context.Set<ProjectStakeholder>()
            .Where(x => x.ProjectId == projectId)
            .OrderBy(x => x.Name)
            .ThenBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
        return Result<IReadOnlyList<ProjectStakeholderDto>>.Success(entities.Select(ToDto).ToList());
    }

    public async Task<Result<ProjectStakeholderDto>> AddInternalAsync(Guid projectId, AddInternalProjectStakeholderDto dto, string userId, CancellationToken cancellationToken = default)
    {
        var accessError = RequireProjectAccess(projectId, "Stakeholders.Manage", userId, mutation: true);
        if (accessError is not null) return Result<ProjectStakeholderDto>.Failure(accessError);
        if (!await ProjectExistsAsync(projectId, cancellationToken))
            return Result<ProjectStakeholderDto>.Failure(Error.NotFound("Project not found"));
        if (dto.UserId == Guid.Empty)
            return Result<ProjectStakeholderDto>.Failure(Error.Validation("An internal user is required"));

        var reference = await _userLookup.FindAsync(dto.UserId, cancellationToken);
        if (reference is null)
            return Result<ProjectStakeholderDto>.Failure(Error.Validation("The selected user could not be found or is inactive"));
        if (string.IsNullOrWhiteSpace(dto.Role))
            return Result<ProjectStakeholderDto>.Failure(Error.Validation("Relationship is required"));
        var resource = await _resourceLookup.FindByUserIdAsync(dto.UserId, cancellationToken);
        if (resource is null)
            return Result<ProjectStakeholderDto>.Failure(Error.Validation("The selected user must already exist in Resource Management"));

        var duplicate = await _context.Set<ProjectStakeholder>()
            .AnyAsync(x => x.ProjectId == projectId && x.UserId == dto.UserId && x.Status == ProjectStakeholderStatus.Active, cancellationToken);
        if (duplicate)
            return Result<ProjectStakeholderDto>.Failure(Error.Conflict("This user is already an active stakeholder on the project"));

        var entity = new ProjectStakeholder
        {
            TenantId = GetCurrentTenantId(),
            ProjectId = projectId,
            UserId = dto.UserId,
            ResourceId = resource.Id,
            Name = reference.FullName.Trim(),
            Email = reference.Email.Trim(),
            NormalizedEmail = NormalizeEmail(reference.Email),
            Role = dto.Role.Trim(),
            RelationshipType = "Stakeholder",
            Interest = Normalize(dto.Interest),
            Influence = Normalize(dto.Influence),
            Status = ProjectStakeholderStatus.Active,
            RowVersion = NewRowVersion(),
            CreatedBy = userId
        };
        _context.Set<ProjectStakeholder>().Add(entity);
        await SaveAndAuditAsync(entity, "Create", userId, null, cancellationToken);
        return Result<ProjectStakeholderDto>.Success(ToDto(entity));
    }

    public async Task<Result<ProjectStakeholderDto>> AddContactAsync(Guid projectId, AddContactProjectStakeholderDto dto, string userId, CancellationToken cancellationToken = default)
    {
        var accessError = RequireProjectAccess(projectId, "Stakeholders.Manage", userId, mutation: true);
        if (accessError is not null) return Result<ProjectStakeholderDto>.Failure(accessError);
        if (!await ProjectExistsAsync(projectId, cancellationToken))
            return Result<ProjectStakeholderDto>.Failure(Error.NotFound("Project not found"));
        return Result<ProjectStakeholderDto>.Failure(Error.Validation("Project stakeholders must be selected from Resource Management. Create this person in Resource Management first."));
    }

    public async Task<Result<ProjectStakeholderDto>> UpdateAsync(Guid projectId, Guid stakeholderId, UpdateProjectStakeholderDto dto, string userId, CancellationToken cancellationToken = default)
    {
        var accessError = RequireProjectAccess(projectId, "Stakeholders.Manage", userId, mutation: true);
        if (accessError is not null) return Result<ProjectStakeholderDto>.Failure(accessError);
        var entity = await FindAsync(projectId, stakeholderId, cancellationToken);
        if (entity is null) return Result<ProjectStakeholderDto>.Failure(Error.NotFound("Stakeholder not found"));
        var versionError = ApplyConcurrency(entity, dto.RowVersion);
        if (versionError is not null) return Result<ProjectStakeholderDto>.Failure(versionError);
        if (entity.Status == ProjectStakeholderStatus.Revoked)
            return Result<ProjectStakeholderDto>.Failure(Error.Conflict("A revoked stakeholder cannot be edited"));

        if (entity.UserId.HasValue)
        {
            var reference = await _userLookup.FindAsync(entity.UserId.Value, cancellationToken);
            if (reference is null) return Result<ProjectStakeholderDto>.Failure(Error.Validation("The linked user is inactive or unavailable"));
            entity.Name = reference.FullName.Trim();
            entity.Email = reference.Email.Trim();
            entity.NormalizedEmail = NormalizeEmail(reference.Email);
        }
        else
        {
            if (dto.Name is not null)
            {
                if (string.IsNullOrWhiteSpace(dto.Name)) return Result<ProjectStakeholderDto>.Failure(Error.Validation("Name is required"));
                entity.Name = dto.Name.Trim();
            }
            if (dto.Email is not null)
            {
                var normalizedEmail = NormalizeEmail(dto.Email);
                if (!string.IsNullOrWhiteSpace(normalizedEmail) && await _context.Set<ProjectStakeholder>().AnyAsync(x =>
                        x.Id != entity.Id &&
                        x.ProjectId == projectId &&
                        x.Status == ProjectStakeholderStatus.ContactOnly &&
                        x.NormalizedEmail == normalizedEmail, cancellationToken))
                    return Result<ProjectStakeholderDto>.Failure(Error.Conflict("This contact is already an active stakeholder on the project"));

                entity.Email = Normalize(dto.Email);
                entity.NormalizedEmail = normalizedEmail;
            }
        }
        if (dto.Role is not null)
        {
            if (string.IsNullOrWhiteSpace(dto.Role)) return Result<ProjectStakeholderDto>.Failure(Error.Validation("Relationship is required"));
            entity.Role = dto.Role.Trim();
        }
        if (dto.Interest is not null) entity.Interest = Normalize(dto.Interest);
        if (dto.Influence is not null) entity.Influence = Normalize(dto.Influence);
        entity.UpdatedBy = userId;
        entity.RowVersion = NewRowVersion();
        try { await SaveAndAuditAsync(entity, "Update", userId, dto, cancellationToken); }
        catch (DbUpdateConcurrencyException) { return Result<ProjectStakeholderDto>.Failure(Error.Conflict("The stakeholder was changed by another user")); }
        return Result<ProjectStakeholderDto>.Success(ToDto(entity));
    }

    public async Task<Result<ProjectStakeholderDto>> RevokeAsync(Guid projectId, Guid stakeholderId, RevokeProjectStakeholderDto dto, string userId, CancellationToken cancellationToken = default)
    {
        var accessError = RequireProjectAccess(projectId, "Stakeholders.Manage", userId, mutation: true);
        if (accessError is not null) return Result<ProjectStakeholderDto>.Failure(accessError);
        var entity = await FindAsync(projectId, stakeholderId, cancellationToken);
        if (entity is null) return Result<ProjectStakeholderDto>.Failure(Error.NotFound("Stakeholder not found"));
        var versionError = ApplyConcurrency(entity, dto.RowVersion);
        if (versionError is not null) return Result<ProjectStakeholderDto>.Failure(versionError);
        if (entity.Status == ProjectStakeholderStatus.Revoked)
            return Result<ProjectStakeholderDto>.Success(ToDto(entity));
        entity.Status = ProjectStakeholderStatus.Revoked;
        entity.UpdatedBy = userId;
        entity.RowVersion = NewRowVersion();
        try { await SaveAndAuditAsync(entity, "Revoke", userId, null, cancellationToken); }
        catch (DbUpdateConcurrencyException) { return Result<ProjectStakeholderDto>.Failure(Error.Conflict("The stakeholder was changed by another user")); }
        return Result<ProjectStakeholderDto>.Success(ToDto(entity));
    }

    public async Task<Result> RemoveAsync(Guid projectId, Guid stakeholderId, byte[]? rowVersion, string userId, CancellationToken cancellationToken = default)
    {
        var accessError = RequireProjectAccess(projectId, "Stakeholders.Manage", userId, mutation: true);
        if (accessError is not null) return Result.Failure(accessError);
        var entity = await FindAsync(projectId, stakeholderId, cancellationToken);
        if (entity is null) return Result.Failure(Error.NotFound("Stakeholder not found"));
        var versionError = ApplyConcurrency(entity, rowVersion);
        if (versionError is not null) return Result.Failure(versionError);
        entity.IsDeleted = true;
        entity.UpdatedBy = userId;
        entity.RowVersion = NewRowVersion();
        try { await SaveAndAuditAsync(entity, "Delete", userId, null, cancellationToken); }
        catch (DbUpdateConcurrencyException) { return Result.Failure(Error.Conflict("The stakeholder was changed by another user")); }
        return Result.Success();
    }

    private async Task<ProjectStakeholder?> FindAsync(Guid projectId, Guid stakeholderId, CancellationToken cancellationToken) =>
        await _context.Set<ProjectStakeholder>().FirstOrDefaultAsync(x => x.Id == stakeholderId && x.ProjectId == projectId, cancellationToken);

    private async Task<bool> ProjectExistsAsync(Guid projectId, CancellationToken cancellationToken) =>
        await _context.Set<Project>().AnyAsync(x => x.Id == projectId, cancellationToken);

    private async Task SaveAndAuditAsync(ProjectStakeholder entity, string action, string userId, object? values, CancellationToken cancellationToken)
    {
        await _context.SaveChangesAsync(cancellationToken);
        await _auditService.LogAsync(new AuditEntry
        {
            TenantId = entity.TenantId,
            EntityType = "ProjectStakeholder",
            EntityId = entity.Id,
            Action = action,
            UserId = Guid.TryParse(userId, out var parsed) ? parsed : null,
            NewValues = values is null ? JsonSerializer.Serialize(new { entity.Status, entity.Role, entity.UserId }) : JsonSerializer.Serialize(values),
            Timestamp = DateTime.UtcNow,
            CreatedBy = userId
        }, cancellationToken);
    }

    private static ProjectStakeholderDto ToDto(ProjectStakeholder entity) => new()
    {
        Id = entity.Id,
        ProjectId = entity.ProjectId,
        UserId = entity.UserId,
        ResourceId = entity.ResourceId,
        Name = entity.Name,
        Email = entity.Email,
        Role = entity.Role,
        Interest = entity.Interest,
        Influence = entity.Influence,
        Type = entity.UserId.HasValue ? "Internal" : "ContactOnly",
        RelationshipType = entity.RelationshipType,
        Status = entity.Status,
        RowVersion = entity.RowVersion,
        CreatedAt = entity.CreatedAt,
        UpdatedAt = entity.UpdatedAt
    };

    private NEXUS.SharedKernel.Error? ApplyConcurrency(ProjectStakeholder entity, byte[]? rowVersion)
    {
        if (rowVersion is null || rowVersion.Length == 0) return Error.Validation("RowVersion is required");
        // Keep the API supplied token as the original value used by the
        // optimistic concurrency predicate; EF will populate the new token.
        // The entity's current value must remain store-generated.
        _context.Entry(entity).Property(x => x.RowVersion).OriginalValue = rowVersion;
        return null;
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string? NormalizeEmail(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();
    private static byte[] NewRowVersion() => RandomNumberGenerator.GetBytes(8);

    private Guid GetCurrentTenantId()
    {
        var value = _httpContextAccessor.HttpContext?.User.FindFirst("tenantId")?.Value;
        return Guid.TryParse(value, out var tenantId) && tenantId != Guid.Empty ? tenantId : Guid.Empty;
    }

    private NEXUS.SharedKernel.Error? RequireProjectAccess(
        Guid projectId, string permission, string? actorId = null, bool mutation = false)
    {
        var principal = _httpContextAccessor.HttpContext?.User;
        if (principal?.Identity?.IsAuthenticated != true)
            return NEXUS.SharedKernel.Error.Unauthorized("An authenticated user is required.");
        if (!Guid.TryParse(principal.FindFirst("tenantId")?.Value, out var tenantId) || tenantId == Guid.Empty
            || !Guid.TryParse(principal.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userId) || userId == Guid.Empty)
            return NEXUS.SharedKernel.Error.Forbidden("A valid tenant and user identity is required.");
        if (!principal.HasClaim("permission", permission))
            return NEXUS.SharedKernel.Error.Forbidden($"The {permission} permission is required.");
        if (actorId is not null && (!Guid.TryParse(actorId, out var requestedActor) || requestedActor != userId))
            return NEXUS.SharedKernel.Error.Forbidden("The audit actor must match the authenticated user.");

        var management = _managementContext?.Current;
        if (management is null || management.TenantId != tenantId || management.UserId != userId
            || !management.Scope.ProjectIds.Contains(projectId))
            return NEXUS.SharedKernel.Error.Forbidden("The project is outside the current management scope.");
        if (mutation && management.EffectiveReadOnly)
            return NEXUS.SharedKernel.Error.Forbidden("Stakeholder changes are not available in read-only management mode.");
        return null;
    }
}
