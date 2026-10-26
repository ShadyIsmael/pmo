using System.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.Organization.Domain;
using NEXUS.Modules.ResourceManagement.Application.DTOs;
using NEXUS.Modules.ResourceManagement.Application.Mapping;
using NEXUS.Modules.ResourceManagement.Domain;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Helpers;

namespace NEXUS.Modules.ResourceManagement.Application;

/// <summary>
/// Team master operations. A Team is an independent organizational entity, never a
/// Resource type. Membership lives in <see cref="TeamMember"/> and is effective-dated:
/// leaving a Team closes the row with EffectiveTo instead of deleting it, so past
/// work ownership stays attributable.
/// </summary>
public class TeamService : ITeamService
{
    private const string CodePrefix = "TEAM-";
    private const int CodeDigits = 6;
    private const string CodeSequenceEntityType = "Team";
    private const int CodeGenerationAttempts = 8;

    private readonly DbContext _context;
    private readonly IAuditService _auditService;
    private readonly IHttpContextAccessor? _httpContextAccessor;

    public TeamService(DbContext context, IAuditService auditService, IHttpContextAccessor? httpContextAccessor = null)
    {
        _context = context;
        _auditService = auditService;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<PagedResult<TeamDto>> GetPagedAsync(int page, int pageSize, string? search = null, Guid? departmentId = null, bool? isActive = null, CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = _context.Set<Team>().Where(t => !t.IsDeleted);

        if (isActive.HasValue)
            query = query.Where(t => t.IsActive == isActive.Value);

        if (departmentId.HasValue)
            query = query.Where(t => t.DepartmentId == departmentId.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.ToLower();
            query = query.Where(t => t.Name.ToLower().Contains(term) ||
                (t.Code != null && t.Code.ToLower().Contains(term)) ||
                (t.Description != null && t.Description.ToLower().Contains(term)));
        }

        var total = await query.CountAsync(cancellationToken);
        var entities = await query.OrderBy(t => t.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = await MapAllAsync(entities, includeMembers: false, cancellationToken);
        return new PagedResult<TeamDto>(items, page, pageSize, total);
    }

    public async Task<Result<TeamDto>> GetByIdAsync(Guid id, bool includeMembers = true, CancellationToken cancellationToken = default)
    {
        var team = await TeamQuery().FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (team is null)
            return Result<TeamDto>.Failure(Error.NotFound("Team not found"));

        var dto = team.ToDto();
        await EnrichAsync(dto, includeMembers, cancellationToken);
        return Result<TeamDto>.Success(dto);
    }

    public async Task<Result<TeamDto>> CreateAsync(CreateTeamDto dto, string userId, CancellationToken cancellationToken = default)
    {
        var validation = ValidateCreate(dto);
        if (validation is not null)
            return Result<TeamDto>.Failure(validation);

        var referenceError = await ValidateReferencesAsync(dto.DepartmentId, dto.TeamLeadId, cancellationToken);
        if (referenceError is not null)
            return Result<TeamDto>.Failure(referenceError);

        var memberError = await ValidateResourcesAsync(dto.MemberResourceIds, cancellationToken);
        if (memberError is not null)
            return Result<TeamDto>.Failure(memberError);

        var team = dto.ToEntity();
        team.TenantId = CurrentTenantId();
        team.CreatedBy = userId;
        team.RowVersion = NewRowVersion();

        var persisted = false;
        for (var attempt = 1; attempt <= CodeGenerationAttempts; attempt++)
        {
            Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? transaction = null;
            try
            {
                // Serializable protects the counter row/range in SQL Server. The unique
                // sequence key plus the bounded retry below also handles first-use races
                // when two requests create the first Team for a tenant at the same time.
                if (_context.Database.IsRelational())
                    transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

                var nextValue = await AllocateNextCodeValueAsync(team.TenantId, cancellationToken);
                team.Code = FormatCode(nextValue);
                _context.Set<Team>().Add(team);

                AddInitialMembers(team, dto.MemberResourceIds, userId);
                await _context.SaveChangesAsync(cancellationToken);

                if (transaction is not null)
                    await transaction.CommitAsync(cancellationToken);

                persisted = true;
                break;
            }
            catch (Exception ex) when (IsRetryableCodeAllocationFailure(ex))
            {
                await RollbackSilentlyAsync(transaction);
                DetachPendingCreate(team);
                if (attempt < CodeGenerationAttempts)
                    await Task.Delay(TimeSpan.FromMilliseconds(25 * attempt), cancellationToken);
            }
            finally
            {
                if (transaction is not null)
                    await transaction.DisposeAsync();
            }
        }

        if (!persisted)
            return Result<TeamDto>.Failure(new Error(
                "CODE_ALLOCATION_UNAVAILABLE",
                "A unique Team code could not be allocated at this time.",
                503));

        await LogAsync("Team", team.Id, "Create", dto, userId, cancellationToken);

        var resultDto = team.ToDto();
        await EnrichAsync(resultDto, includeMembers: true, cancellationToken);
        return Result<TeamDto>.Success(resultDto);
    }

    public async Task<Result<TeamDto>> UpdateAsync(Guid id, UpdateTeamDto dto, string userId, CancellationToken cancellationToken = default)
    {
        var team = await TeamQuery().FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (team is null)
            return Result<TeamDto>.Failure(Error.NotFound("Team not found"));

        var validation = ValidateUpdate(dto);
        if (validation is not null)
            return Result<TeamDto>.Failure(validation);

        var referenceError = await ValidateReferencesAsync(dto.DepartmentId ?? team.DepartmentId, dto.TeamLeadId ?? team.TeamLeadId, cancellationToken);
        if (referenceError is not null)
            return Result<TeamDto>.Failure(referenceError);

        team.ApplyUpdate(dto);
        team.UpdatedBy = userId;
        team.RowVersion = NewRowVersion();
        await _context.SaveChangesAsync(cancellationToken);
        await LogAsync("Team", team.Id, "Update", dto, userId, cancellationToken);

        var resultDto = team.ToDto();
        await EnrichAsync(resultDto, includeMembers: true, cancellationToken);
        return Result<TeamDto>.Success(resultDto);
    }

    public async Task<Result<TeamDto>> DeactivateAsync(Guid id, string userId, CancellationToken cancellationToken = default)
    {
        var team = await TeamQuery().FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (team is null)
            return Result<TeamDto>.Failure(Error.NotFound("Team not found"));

        team.IsActive = false;
        team.UpdatedBy = userId;
        team.RowVersion = NewRowVersion();
        await _context.SaveChangesAsync(cancellationToken);
        await LogAsync("Team", team.Id, "Deactivate", null, userId, cancellationToken);

        var dto = team.ToDto();
        await EnrichAsync(dto, includeMembers: true, cancellationToken);
        return Result<TeamDto>.Success(dto);
    }

    public async Task<Result<TeamDto>> ReactivateAsync(Guid id, string userId, CancellationToken cancellationToken = default)
    {
        var team = await TeamQuery().FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (team is null)
            return Result<TeamDto>.Failure(Error.NotFound("Team not found"));

        team.IsActive = true;
        team.UpdatedBy = userId;
        team.RowVersion = NewRowVersion();
        await _context.SaveChangesAsync(cancellationToken);
        await LogAsync("Team", team.Id, "Reactivate", null, userId, cancellationToken);

        var dto = team.ToDto();
        await EnrichAsync(dto, includeMembers: true, cancellationToken);
        return Result<TeamDto>.Success(dto);
    }

    public async Task<Result> DeleteAsync(Guid id, string userId, CancellationToken cancellationToken = default)
    {
        var team = await TeamQuery().FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (team is null)
            return Result.Failure(Error.NotFound("Team not found"));

        // Only *current* memberships block deletion. Closed historical rows are not
        // an active reference, and they are soft-deleted below rather than removed so
        // past work ownership stays attributable.
        var currentMembers = await _context.Set<TeamMember>()
            .Where(m => m.TeamId == id && !m.IsDeleted && m.IsActive && m.EffectiveTo == null)
            .CountAsync(cancellationToken);
        if (currentMembers > 0)
            return Result.Failure(Error.Conflict(
                $"This team cannot be deleted because it still has {currentMembers} current member{(currentMembers == 1 ? "" : "s")}. Remove the members first, or deactivate the team instead."));

        var historicalMembers = await _context.Set<TeamMember>()
            .Where(m => m.TeamId == id && !m.IsDeleted)
            .ToListAsync(cancellationToken);
        foreach (var member in historicalMembers)
        {
            member.IsDeleted = true;
            member.UpdatedBy = userId;
            member.RowVersion = NewRowVersion();
        }

        team.IsDeleted = true;
        team.UpdatedBy = userId;
        team.RowVersion = NewRowVersion();
        await _context.SaveChangesAsync(cancellationToken);
        await LogAsync("Team", team.Id, "Delete", null, userId, cancellationToken);
        return Result.Success();
    }

    public async Task<IReadOnlyList<TeamLookupDto>> GetLookupAsync(bool activeOnly = true, CancellationToken cancellationToken = default)
    {
        var query = _context.Set<Team>().AsNoTracking().Where(t => !t.IsDeleted);
        if (activeOnly)
            query = query.Where(t => t.IsActive);

        return await query.OrderBy(t => t.Name)
            .Select(t => new TeamLookupDto { Id = t.Id, Name = t.Name, Code = t.Code, IsActive = t.IsActive })
            .ToListAsync(cancellationToken);
    }

    public async Task<Result<IReadOnlyList<TeamMemberDto>>> GetMembersAsync(Guid teamId, bool currentOnly = true, CancellationToken cancellationToken = default)
    {
        if (!await TeamQuery().AnyAsync(t => t.Id == teamId, cancellationToken))
            return Result<IReadOnlyList<TeamMemberDto>>.Failure(Error.NotFound("Team not found"));

        return Result<IReadOnlyList<TeamMemberDto>>.Success(await LoadMembersAsync(teamId, currentOnly, cancellationToken));
    }

    public async Task<Result<IReadOnlyList<TeamMemberDto>>> AddMembersAsync(Guid teamId, AddTeamMembersDto dto, string userId, CancellationToken cancellationToken = default)
    {
        var team = await TeamQuery().FirstOrDefaultAsync(t => t.Id == teamId, cancellationToken);
        if (team is null)
            return Result<IReadOnlyList<TeamMemberDto>>.Failure(Error.NotFound("Team not found"));
        if (!team.IsActive)
            return Result<IReadOnlyList<TeamMemberDto>>.Failure(Error.Validation("Members cannot be added to an inactive team"));

        var requested = dto.ResourceIds.Distinct().ToList();
        if (requested.Count == 0)
            return Result<IReadOnlyList<TeamMemberDto>>.Failure(Error.Validation("At least one resource is required"));

        var resourceError = await ValidateResourcesAsync(requested, cancellationToken);
        if (resourceError is not null)
            return Result<IReadOnlyList<TeamMemberDto>>.Failure(resourceError);

        var existing = await CurrentMemberIdsAsync(teamId, cancellationToken);
        var toAdd = requested.Where(id => !existing.Contains(id)).ToList();
        if (toAdd.Count == 0)
            return Result<IReadOnlyList<TeamMemberDto>>.Failure(Error.Conflict("All of the selected resources are already members of this team"));

        var from = dto.EffectiveFrom ?? DateTime.UtcNow;
        foreach (var resourceId in toAdd)
        {
            _context.Set<TeamMember>().Add(new TeamMember
            {
                TeamId = teamId,
                ResourceId = resourceId,
                EffectiveFrom = from,
                IsActive = true,
                TenantId = team.TenantId,
                CreatedBy = userId,
                RowVersion = NewRowVersion()
            });
        }

        await _context.SaveChangesAsync(cancellationToken);
        await LogAsync("Team", teamId, "AddMembers", new { resourceIds = toAdd, from }, userId, cancellationToken);
        return Result<IReadOnlyList<TeamMemberDto>>.Success(await LoadMembersAsync(teamId, currentOnly: true, cancellationToken));
    }

    public async Task<Result<IReadOnlyList<TeamMemberDto>>> SetMembersAsync(Guid teamId, SetTeamMembersDto dto, string userId, CancellationToken cancellationToken = default)
    {
        var team = await TeamQuery().FirstOrDefaultAsync(t => t.Id == teamId, cancellationToken);
        if (team is null)
            return Result<IReadOnlyList<TeamMemberDto>>.Failure(Error.NotFound("Team not found"));
        if (!team.IsActive)
            return Result<IReadOnlyList<TeamMemberDto>>.Failure(Error.Validation("Members cannot be changed on an inactive team"));

        var requested = dto.ResourceIds.Distinct().ToList();
        var resourceError = await ValidateResourcesAsync(requested, cancellationToken);
        if (resourceError is not null)
            return Result<IReadOnlyList<TeamMemberDto>>.Failure(resourceError);

        var from = dto.EffectiveFrom ?? DateTime.UtcNow;
        var current = await _context.Set<TeamMember>()
            .Where(m => m.TeamId == teamId && !m.IsDeleted && m.IsActive && m.EffectiveTo == null)
            .ToListAsync(cancellationToken);

        var requestedSet = requested.ToHashSet();
        var now = DateTime.UtcNow;
        var toAdd = requestedSet.Except(current.Select(m => m.ResourceId)).ToList();
        var toClose = current.Where(m => !requestedSet.Contains(m.ResourceId)).ToList();

        foreach (var resourceId in toAdd)
        {
            _context.Set<TeamMember>().Add(new TeamMember
            {
                TeamId = teamId,
                ResourceId = resourceId,
                EffectiveFrom = from,
                IsActive = true,
                TenantId = team.TenantId,
                CreatedBy = userId,
                RowVersion = NewRowVersion()
            });
        }

        foreach (var member in toClose)
        {
            member.EffectiveTo = now;
            member.IsActive = false;
            member.UpdatedBy = userId;
            member.RowVersion = NewRowVersion();
        }

        await _context.SaveChangesAsync(cancellationToken);
        await LogAsync("Team", teamId, "SetMembers", new { requested, from, added = toAdd.Count, closed = toClose.Count }, userId, cancellationToken);
        return Result<IReadOnlyList<TeamMemberDto>>.Success(await LoadMembersAsync(teamId, currentOnly: true, cancellationToken));
    }

    public async Task<Result<IReadOnlyList<TeamMemberDto>>> RemoveMemberAsync(Guid teamId, Guid resourceId, RemoveTeamMemberDto dto, string userId, CancellationToken cancellationToken = default)
    {
        var team = await TeamQuery().FirstOrDefaultAsync(t => t.Id == teamId, cancellationToken);
        if (team is null)
            return Result<IReadOnlyList<TeamMemberDto>>.Failure(Error.NotFound("Team not found"));

        var member = await _context.Set<TeamMember>()
            .FirstOrDefaultAsync(m => m.TeamId == teamId && m.ResourceId == resourceId && !m.IsDeleted && m.IsActive && m.EffectiveTo == null, cancellationToken);
        if (member is null)
            return Result<IReadOnlyList<TeamMemberDto>>.Failure(Error.NotFound("This resource is not a current member of the team"));

        // Close, never hard-delete: historical work ownership must stay attributable.
        member.EffectiveTo = dto.EffectiveTo ?? DateTime.UtcNow;
        member.IsActive = false;
        member.UpdatedBy = userId;
        member.RowVersion = NewRowVersion();
        await _context.SaveChangesAsync(cancellationToken);
        await LogAsync("Team", teamId, "RemoveMember", new { resourceId, member.EffectiveTo }, userId, cancellationToken);
        return Result<IReadOnlyList<TeamMemberDto>>.Success(await LoadMembersAsync(teamId, currentOnly: true, cancellationToken));
    }

    private IQueryable<Team> TeamQuery() =>
        _context.Set<Team>()
            .Include(t => t.Department)
            .Where(t => !t.IsDeleted);

    private async Task<HashSet<Guid>> CurrentMemberIdsAsync(Guid teamId, CancellationToken cancellationToken) =>
        await _context.Set<TeamMember>()
            .Where(m => m.TeamId == teamId && !m.IsDeleted && m.IsActive && m.EffectiveTo == null)
            .Select(m => m.ResourceId)
            .ToHashSetAsync(cancellationToken);

    private async Task<List<TeamMemberDto>> LoadMembersAsync(Guid teamId, bool currentOnly, CancellationToken cancellationToken)
    {
        var query = _context.Set<TeamMember>().AsNoTracking().Where(m => m.TeamId == teamId && !m.IsDeleted);
        if (currentOnly)
            query = query.Where(m => m.IsActive && m.EffectiveTo == null);

        var members = await query.OrderBy(m => m.EffectiveFrom).ToListAsync(cancellationToken);
        if (members.Count == 0)
            return new List<TeamMemberDto>();

        var resourceIds = members.Select(m => m.ResourceId).Distinct().ToList();
        var resources = await _context.Set<Resource>().AsNoTracking()
            .Where(r => resourceIds.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, cancellationToken);

        return members.Select(m =>
        {
            var dto = m.ToDto();
            if (resources.TryGetValue(m.ResourceId, out var resource))
            {
                dto.ResourceName = resource.Name;
                dto.ResourceType = ResourceTypeConstants.ToApiCode(resource.Type);
            }
            return dto;
        }).ToList();
    }

    private async Task<List<TeamDto>> MapAllAsync(List<Team> entities, bool includeMembers, CancellationToken cancellationToken)
    {
        var dtos = entities.Select(t => t.ToDto()).ToList();
        foreach (var dto in dtos)
            await EnrichAsync(dto, includeMembers, cancellationToken);
        return dtos;
    }

    private async Task EnrichAsync(TeamDto dto, bool includeMembers, CancellationToken cancellationToken)
    {
        if (dto.TeamLeadId.HasValue && dto.TeamLeadName is null)
        {
            var lead = await _context.Set<Resource>().AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == dto.TeamLeadId.Value, cancellationToken);
            dto.TeamLeadName = lead?.Name;
        }

        var memberCount = await _context.Set<TeamMember>()
            .CountAsync(m => m.TeamId == dto.Id && !m.IsDeleted && m.IsActive && m.EffectiveTo == null, cancellationToken);
        dto.MemberCount = memberCount;

        if (includeMembers && memberCount > 0)
            dto.Members = await LoadMembersAsync(dto.Id, currentOnly: true, cancellationToken);
    }

    private static Error? ValidateCreate(CreateTeamDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            return Error.Validation("Team name is required");
        if (dto.Name.Trim().Length > 200)
            return Error.Validation("Team name must be 200 characters or fewer");
        if (dto.Description is { Length: > 1000 })
            return Error.Validation("Team description must be 1000 characters or fewer");
        if (dto.EmailAlias is { Length: > 256 })
            return Error.Validation("Team email alias must be 256 characters or fewer");
        return null;
    }

    private static Error? ValidateUpdate(UpdateTeamDto dto)
    {
        if (dto.Name is not null && string.IsNullOrWhiteSpace(dto.Name))
            return Error.Validation("Team name is required");
        if (dto.Name is { Length: > 200 })
            return Error.Validation("Team name must be 200 characters or fewer");
        if (dto.Description is { Length: > 1000 })
            return Error.Validation("Team description must be 1000 characters or fewer");
        if (dto.EmailAlias is { Length: > 256 })
            return Error.Validation("Team email alias must be 256 characters or fewer");
        return null;
    }

    /// <summary>
    /// The optional Department and Team Lead must exist in the current tenant.
    /// A team lead is a Resource, never a User, so lead badges stay meaningful.
    /// </summary>
    private async Task<Error?> ValidateReferencesAsync(Guid? departmentId, Guid? teamLeadId, CancellationToken cancellationToken)
    {
        if (departmentId.HasValue)
        {
            var exists = await _context.Set<Department>().AnyAsync(d => d.Id == departmentId.Value && !d.IsDeleted, cancellationToken);
            if (!exists)
                return Error.Validation("Department was not found");
        }

        if (teamLeadId.HasValue)
        {
            var exists = await _context.Set<Resource>().AnyAsync(r => r.Id == teamLeadId.Value && !r.IsDeleted, cancellationToken);
            if (!exists)
                return Error.Validation("Team lead must be an existing resource");
        }

        return null;
    }

    private async Task<Error?> ValidateResourcesAsync(IReadOnlyList<Guid> resourceIds, CancellationToken cancellationToken)
    {
        var ids = resourceIds.Where(id => id != Guid.Empty).Distinct().ToList();
        if (ids.Count == 0)
            return null;

        var found = await _context.Set<Resource>()
            .Where(r => ids.Contains(r.Id) && !r.IsDeleted)
            .Select(r => r.Id)
            .ToListAsync(cancellationToken);

        if (found.Count != ids.Count)
            return Error.Validation("One or more selected resources do not exist");

        return null;
    }

    private async Task<int> AllocateNextCodeValueAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var sequence = await _context.Set<EntityCodeSequence>()
            .SingleOrDefaultAsync(item => item.TenantId == tenantId && item.EntityType == CodeSequenceEntityType, cancellationToken);

        if (sequence is null)
        {
            // This path is only a bootstrap fallback for databases created before the
            // sequence migration and for brand-new tenants. Once the row exists, every
            // allocation is an update of the tenant's counter row, never a MAX scan.
            var maxExistingValue = await FindExistingMaxCodeValueAsync(tenantId, cancellationToken);
            sequence = new EntityCodeSequence
            {
                TenantId = tenantId,
                EntityType = CodeSequenceEntityType,
                NextValue = checked(maxExistingValue + 1)
            };
            _context.Set<EntityCodeSequence>().Add(sequence);
        }

        var allocatedValue = sequence.NextValue;
        sequence.NextValue = checked(allocatedValue + 1);
        return allocatedValue;
    }

    private async Task<int> FindExistingMaxCodeValueAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var codes = await _context.Set<Team>()
            .Where(t => t.TenantId == tenantId && t.Code != null && t.Code.StartsWith(CodePrefix))
            .Select(t => t.Code!)
            .ToListAsync(cancellationToken);

        var max = 0;
        foreach (var code in codes)
        {
            var suffix = code[CodePrefix.Length..];
            if (int.TryParse(suffix, out var value) && value > max)
                max = value;
        }

        return max;
    }

    private static string FormatCode(int value) => $"{CodePrefix}{value.ToString($"D{CodeDigits}")}";

    private void AddInitialMembers(Team team, IReadOnlyList<Guid> resourceIds, string userId)
    {
        var now = DateTime.UtcNow;
        foreach (var resourceId in resourceIds.Distinct())
        {
            _context.Set<TeamMember>().Add(new TeamMember
            {
                TeamId = team.Id,
                ResourceId = resourceId,
                EffectiveFrom = now,
                IsActive = true,
                TenantId = team.TenantId,
                CreatedBy = userId,
                RowVersion = NewRowVersion()
            });
        }
    }

    private void DetachPendingCreate(Team team)
    {
        foreach (var entry in _context.ChangeTracker.Entries().Where(entry =>
                     entry.Entity is Team pendingTeam && pendingTeam.Id == team.Id ||
                     entry.Entity is TeamMember pendingMember && pendingMember.TeamId == team.Id ||
                     entry.Entity is EntityCodeSequence pendingSequence &&
                     pendingSequence.TenantId == team.TenantId &&
                     pendingSequence.EntityType == CodeSequenceEntityType).ToList())
        {
            entry.State = EntityState.Detached;
        }
    }

    private static async Task RollbackSilentlyAsync(Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? transaction)
    {
        if (transaction is null)
            return;

        try
        {
            await transaction.RollbackAsync(CancellationToken.None);
        }
        catch
        {
            // Disposing an unfinished transaction still rolls it back. Preserve the
            // original allocation exception for the retry decision.
        }
    }

    private static bool IsRetryableCodeAllocationFailure(Exception exception)
    {
        var message = exception.ToString();
        return message.Contains("EntityCodeSequences", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("IX_Teams_TenantId_Code", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("Teams.TenantId, Teams.Code", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("database is locked", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("database table is locked", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("deadlock", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("timeout", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("1205", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Teams use a varbinary RowVersion (matching the pre-existing Teams column),
    /// so the version is stamped by the application rather than by the store.
    /// </summary>
    private static byte[] NewRowVersion() => System.Security.Cryptography.RandomNumberGenerator.GetBytes(8);

    private Guid CurrentTenantId() =>
        Guid.TryParse(_httpContextAccessor?.HttpContext?.User.FindFirstValue("tenantId"), out var tenantId)
            ? tenantId
            : TenantConstants.DemoTenantId;

    private async Task LogAsync(string entityType, Guid entityId, string action, object? payload, string userId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(userId, out var actorId))
            actorId = Guid.Empty;

        await _auditService.LogAsync(new AuditEntry
        {
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            NewValues = payload is null ? null : System.Text.Json.JsonSerializer.Serialize(payload),
            UserId = actorId,
            Timestamp = DateTime.UtcNow,
            CreatedBy = userId
        }, cancellationToken);
    }
}
