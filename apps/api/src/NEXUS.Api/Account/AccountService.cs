using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.Identity.Application;
using NEXUS.Modules.Identity.Domain;
using NEXUS.Modules.Organization.Domain;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.Modules.ResourceManagement.Domain;
using NEXUS.Persistence;
using NEXUS.SharedKernel;

namespace NEXUS.Api.Account;

public sealed class AccountService : IAccountService
{
    private readonly AppDbContext _context;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly LocalPasswordPolicyOptions _passwordPolicy;
    private readonly IAuditService _audit;

    public AccountService(
        AppDbContext context,
        IPasswordHasher<User> passwordHasher,
        IOptions<LocalPasswordPolicyOptions> passwordPolicy,
        IAuditService audit)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _passwordPolicy = passwordPolicy.Value;
        _audit = audit;
    }

    public async Task<Result<AccountResponseDto>> GetAsync(Guid userId, Guid tenantId, Guid currentSessionId, CancellationToken cancellationToken = default)
    {
        var user = await _context.Users.AsNoTracking()
            .Where(item => item.Id == userId && item.TenantId == tenantId && item.IsActive && !item.IsDeleted)
            .Select(item => new
            {
                item.Id, item.TenantId, item.FirstName, item.LastName, item.Email,
                item.AuthenticationSource, item.MustChangePassword, item.PasswordChangedAt, item.LastLoginAt
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (user is null) return Result<AccountResponseDto>.Failure(Error.NotFound("The account is unavailable."));

        var now = DateTime.UtcNow;
        var tenant = await _context.Tenants.AsNoTracking()
            .Where(item => item.Id == tenantId)
            .Select(item => new { item.Id, item.Code, item.DisplayName, item.Status })
            .SingleOrDefaultAsync(cancellationToken);
        var tenantMembership = tenant is null
            ? null
            : new AccountTenantMembershipDto(tenant.Id, tenant.Code, tenant.DisplayName, tenant.Status.ToString());

        var organizationAccess = await (
            from grant in _context.Set<OrganizationSettingsGrant>().AsNoTracking()
            join organization in _context.Organizations.AsNoTracking()
                on new { grant.TenantId, OrganizationId = grant.OrganizationId }
                equals new { organization.TenantId, OrganizationId = organization.Id }
            where grant.TenantId == tenantId && grant.UserId == userId && !grant.IsDeleted
                && !organization.IsDeleted && organization.IsActive
            orderby organization.Name
            select new AccountOrganizationAccessDto(
                organization.Id, organization.Name, organization.Code, grant.CanRead, grant.CanManage))
            .ToListAsync(cancellationToken);

        var resource = await _context.Resources.AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.UserId == userId && item.IsActive && !item.IsDeleted)
            .OrderBy(item => item.CreatedAt)
            .Select(item => new
            {
                item.Id, item.Name, item.EmployeeId, item.JobTitle, item.Email, item.Phone, item.AvatarUrl,
                item.Type, item.Status, item.EmploymentType, item.Seniority, item.DateOfJoining, item.LocationId
            })
            .FirstOrDefaultAsync(cancellationToken);

        AccountProfessionalProfileDto? professional = null;
        var organizationalUnits = new List<AccountOrganizationalUnitMembershipDto>();
        var teams = new List<AccountTeamMembershipDto>();
        var projects = new List<AccountProjectMembershipDto>();
        if (resource is not null)
        {
            var location = resource.LocationId.HasValue
                ? await _context.Locations.AsNoTracking()
                    .Where(item => item.TenantId == tenantId && item.Id == resource.LocationId.Value && !item.IsDeleted)
                    .Select(item => item.Name)
                    .FirstOrDefaultAsync(cancellationToken)
                : null;
            professional = new AccountProfessionalProfileDto(
                resource.Name, resource.EmployeeId, resource.JobTitle, resource.Email, resource.Phone,
                resource.AvatarUrl, resource.Type, resource.Status, resource.EmploymentType,
                resource.Seniority, resource.DateOfJoining, location);

            organizationalUnits = await (
                from membership in _context.ResourceOrganizationalUnits.AsNoTracking()
                join unit in _context.OrganizationalUnits.AsNoTracking()
                    on new { membership.TenantId, UnitId = membership.OrganizationalUnitId }
                    equals new { unit.TenantId, UnitId = unit.Id }
                where membership.TenantId == tenantId && membership.ResourceId == resource.Id
                    && membership.IsActive && !membership.IsDeleted
                    && membership.EffectiveFrom <= now
                    && (!membership.EffectiveTo.HasValue || membership.EffectiveTo > now)
                    && unit.IsActive && !unit.IsDeleted
                orderby membership.IsPrimary descending, unit.SortOrder, unit.Name
                select new AccountOrganizationalUnitMembershipDto(unit.Id, unit.Name, unit.TypeCode, membership.IsPrimary))
                .ToListAsync(cancellationToken);

            teams = await (
                from membership in _context.TeamMembers.AsNoTracking()
                join team in _context.Teams.AsNoTracking()
                    on new { membership.TenantId, membership.TeamId }
                    equals new { team.TenantId, TeamId = team.Id }
                where membership.TenantId == tenantId && membership.ResourceId == resource.Id
                    && membership.IsActive && !membership.IsDeleted
                    && membership.EffectiveFrom <= now
                    && (!membership.EffectiveTo.HasValue || membership.EffectiveTo > now)
                    && team.IsActive && !team.IsDeleted
                orderby team.Name
                select new AccountTeamMembershipDto(team.Id, team.Name, team.Code, membership.EffectiveFrom))
                .ToListAsync(cancellationToken);

            var teamIds = teams.Select(item => item.TeamId).ToArray();
            var projectRows = await _context.ProjectMembers.AsNoTracking()
                .Where(member => member.TenantId == tenantId && !member.IsDeleted
                    && member.Status == "Active"
                    && member.StartDate <= now && member.EndDate >= now
                    && (member.ResourceId == resource.Id || (member.TeamId.HasValue && teamIds.Contains(member.TeamId.Value)))
                    && member.Project.TenantId == tenantId && !member.Project.IsDeleted)
                .Select(member => new
                {
                    member.Id,
                    member.ProjectId,
                    ProjectName = member.Project.Name,
                    ProjectCode = member.Project.Code,
                    ProjectStatus = member.Project.Status,
                    member.ResourceId,
                    member.TeamId,
                    member.Role,
                    member.AllocationPercentage,
                    member.StartDate,
                    member.EndDate
                })
                .OrderBy(item => item.ProjectName)
                .ToListAsync(cancellationToken);

            var memberIds = projectRows.Select(item => item.Id).ToArray();
            var functionalRoles = new List<(Guid ProjectMemberId, AccountProjectRoleDto Dto)>();
            if (memberIds.Length > 0)
            {
                var roleRows = await (
                    from assignment in _context.ProjectMemberRoles.AsNoTracking()
                    join role in _context.Lookups.AsNoTracking()
                        on new { assignment.TenantId, LookupId = assignment.ProjectRoleId }
                        equals new { role.TenantId, LookupId = role.Id }
                    where assignment.TenantId == tenantId && memberIds.Contains(assignment.ProjectMemberId)
                        && !assignment.IsDeleted && role.Category == "ProjectRole" && !role.IsDeleted
                    select new
                    {
                        assignment.ProjectMemberId,
                        RoleId = role.Id,
                        role.Code,
                        role.NameEn,
                        role.NameAr,
                        assignment.IsPrimary
                    })
                    .ToListAsync(cancellationToken);
                functionalRoles = roleRows.Select(item => (item.ProjectMemberId,
                    new AccountProjectRoleDto(item.RoleId, item.Code, item.NameEn, item.NameAr, item.IsPrimary))).ToList();
            }
            var rolesByMembership = functionalRoles.GroupBy(item => item.ProjectMemberId)
                .ToDictionary(group => group.Key, group => (IReadOnlyList<AccountProjectRoleDto>)group.Select(item => item.Dto).ToArray());

            projects = projectRows.Select(row => new AccountProjectMembershipDto(
                row.ProjectId, row.ProjectName, row.ProjectCode, row.ProjectStatus,
                row.ResourceId == resource.Id ? "Direct resource" : "Current team membership",
                row.Role, row.AllocationPercentage, row.StartDate, row.EndDate,
                rolesByMembership.GetValueOrDefault(row.Id, Array.Empty<AccountProjectRoleDto>()))).ToList();
        }

        var sessions = await GetActiveSessionsCoreAsync(userId, tenantId, currentSessionId, now, cancellationToken);
        var profile = new AccountProfileDto(user.Id, user.FirstName, user.LastName,
            $"{user.FirstName} {user.LastName}".Trim(), user.Email, user.AuthenticationSource,
            user.PasswordChangedAt, user.LastLoginAt);
        var security = new AccountSecuritySummaryDto(
            user.AuthenticationSource == AuthenticationSource.Local,
            user.MustChangePassword,
            sessions.Count,
            currentSessionId,
            user.PasswordChangedAt,
            user.LastLoginAt);

        return Result<AccountResponseDto>.Success(new AccountResponseDto(
            profile, professional, tenantMembership, organizationAccess, organizationalUnits, teams, projects, security, sessions));
    }

    public async Task<Result<AccountProfileDto>> UpdateProfileAsync(Guid userId, Guid tenantId, UpdateAccountProfileRequest request, CancellationToken cancellationToken = default)
    {
        var firstName = request.FirstName?.Trim();
        var lastName = request.LastName?.Trim();
        if (string.IsNullOrWhiteSpace(firstName) || firstName.Length > 100)
            return Result<AccountProfileDto>.Failure(Error.Validation("First name is required and must be 100 characters or fewer.", "firstName"));
        if (string.IsNullOrWhiteSpace(lastName) || lastName.Length > 100)
            return Result<AccountProfileDto>.Failure(Error.Validation("Last name is required and must be 100 characters or fewer.", "lastName"));

        var user = await _context.Users
            .SingleOrDefaultAsync(item => item.Id == userId && item.TenantId == tenantId && item.IsActive && !item.IsDeleted, cancellationToken);
        if (user is null) return Result<AccountProfileDto>.Failure(Error.NotFound("The account is unavailable."));

        var changedFields = new List<string>(2);
        if (!string.Equals(user.FirstName, firstName, StringComparison.Ordinal)) { user.FirstName = firstName; changedFields.Add("FirstName"); }
        if (!string.Equals(user.LastName, lastName, StringComparison.Ordinal)) { user.LastName = lastName; changedFields.Add("LastName"); }
        if (changedFields.Count > 0)
        {
            user.UpdatedAt = DateTime.UtcNow;
            user.UpdatedBy = userId.ToString();
            await _audit.AppendAsync(new AuditEntry
            {
                TenantId = tenantId, EntityType = "User", EntityId = userId, Action = "AccountProfileUpdated",
                ChangedFields = string.Join(",", changedFields), Changes = "{\"selfService\":true}",
                UserId = userId, UserName = user.FullName, CreatedBy = userId.ToString()
            }, cancellationToken);
            try { await _context.SaveChangesAsync(cancellationToken); }
            catch (DbUpdateConcurrencyException)
            {
                return Result<AccountProfileDto>.Failure(Error.Conflict("The account changed while you were editing it. Reload and try again."));
            }
        }

        return Result<AccountProfileDto>.Success(new AccountProfileDto(
            user.Id, user.FirstName, user.LastName, user.FullName.Trim(), user.Email,
            user.AuthenticationSource, user.PasswordChangedAt, user.LastLoginAt));
    }

    public async Task<Result<ChangeAccountPasswordResponse>> ChangePasswordAsync(
        Guid userId, Guid tenantId, ChangeAccountPasswordRequest request, string? ipAddress, string? userAgent,
        CancellationToken cancellationToken = default)
    {
        var user = await _context.Users
            .SingleOrDefaultAsync(item => item.Id == userId && item.TenantId == tenantId && item.IsActive && !item.IsDeleted, cancellationToken);
        if (user is null || user.AuthenticationSource != AuthenticationSource.Local || string.IsNullOrEmpty(user.PasswordHash))
            return Result<ChangeAccountPasswordResponse>.Failure(Error.Validation("This account uses organization-managed sign-in. Change its password through your identity provider."));
        if (string.IsNullOrWhiteSpace(request.CurrentPassword) || string.IsNullOrWhiteSpace(request.NewPassword))
            return Result<ChangeAccountPasswordResponse>.Failure(Error.Validation("Current password and new password are required."));
        if (_passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.CurrentPassword) == PasswordVerificationResult.Failed)
            return Result<ChangeAccountPasswordResponse>.Failure(Error.Unauthorized("The current password is incorrect."));
        if (request.NewPassword.Length < _passwordPolicy.MinimumLength || request.NewPassword.Length > _passwordPolicy.MaximumLength)
            return Result<ChangeAccountPasswordResponse>.Failure(Error.Validation($"The new password must be between {_passwordPolicy.MinimumLength} and {_passwordPolicy.MaximumLength} characters."));
        if (string.Equals(request.CurrentPassword, request.NewPassword, StringComparison.Ordinal))
            return Result<ChangeAccountPasswordResponse>.Failure(Error.Validation("Choose a password different from the current password."));

        var now = DateTime.UtcNow;
        user.PasswordHash = _passwordHasher.HashPassword(user, request.NewPassword);
        user.PasswordChangedAt = now;
        user.UpdatedAt = now;
        user.UpdatedBy = userId.ToString();
        var sessions = await _context.UserSessions
            .Where(session => session.TenantId == tenantId && session.UserId == userId && session.RevokedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var session in sessions) session.RevokedAt = now;

        await _audit.AppendAsync(new AuditEntry
        {
            TenantId = tenantId, EntityType = "User", EntityId = userId, Action = "AccountPasswordChanged",
            ChangedFields = "PasswordHash,PasswordChangedAt,SessionsRevoked",
            Changes = "{\"passwordChanged\":true,\"activeSessionsRevoked\":true}",
            UserId = userId, UserName = user.FullName, IpAddress = ipAddress, UserAgent = Limit(userAgent, 500),
            CreatedBy = userId.ToString()
        }, cancellationToken);

        try { await _context.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException)
        {
            return Result<ChangeAccountPasswordResponse>.Failure(Error.Conflict("The account changed concurrently. Sign in again and retry."));
        }
        return Result<ChangeAccountPasswordResponse>.Success(new ChangeAccountPasswordResponse(true));
    }

    public async Task<Result<IReadOnlyList<AccountSessionDto>>> GetSessionsAsync(Guid userId, Guid tenantId, Guid currentSessionId, CancellationToken cancellationToken = default)
    {
        var exists = await _context.Users.AsNoTracking().AnyAsync(user => user.Id == userId && user.TenantId == tenantId && user.IsActive && !user.IsDeleted, cancellationToken);
        if (!exists) return Result<IReadOnlyList<AccountSessionDto>>.Failure(Error.NotFound("The account is unavailable."));
        var sessions = await GetActiveSessionsCoreAsync(userId, tenantId, currentSessionId, DateTime.UtcNow, cancellationToken);
        return Result<IReadOnlyList<AccountSessionDto>>.Success(sessions);
    }

    public async Task<Result<bool>> RevokeSessionAsync(
        Guid userId, Guid tenantId, Guid currentSessionId, Guid sessionId, string? ipAddress, string? userAgent,
        CancellationToken cancellationToken = default)
    {
        var session = await _context.UserSessions
            .SingleOrDefaultAsync(item => item.Id == sessionId && item.UserId == userId && item.TenantId == tenantId, cancellationToken);
        if (session is null) return Result<bool>.Failure(Error.NotFound("The session was not found."));
        if (session.RevokedAt is null)
        {
            session.RevokedAt = DateTime.UtcNow;
            await _audit.AppendAsync(new AuditEntry
            {
                TenantId = tenantId, EntityType = "UserSession", EntityId = session.Id, Action = "AccountSessionRevoked",
                Changes = "{\"sessionRevoked\":true}", UserId = userId, IpAddress = ipAddress,
                UserAgent = Limit(userAgent, 500), CreatedBy = userId.ToString()
            }, cancellationToken);
            try { await _context.SaveChangesAsync(cancellationToken); }
            catch (DbUpdateConcurrencyException)
            {
                return Result<bool>.Failure(Error.Conflict("The session changed while it was being revoked. Reload and try again."));
            }
        }
        return Result<bool>.Success(session.Id == currentSessionId);
    }

    public async Task<Result<RevokeOtherSessionsResponse>> RevokeOtherSessionsAsync(
        Guid userId, Guid tenantId, Guid currentSessionId, string? ipAddress, string? userAgent,
        CancellationToken cancellationToken = default)
    {
        if (currentSessionId == Guid.Empty)
            return Result<RevokeOtherSessionsResponse>.Failure(Error.Unauthorized("The current session could not be verified."));

        var now = DateTime.UtcNow;
        var sessions = await _context.UserSessions
            .Where(session => session.TenantId == tenantId && session.UserId == userId
                && session.Id != currentSessionId && session.RevokedAt == null && session.ExpiresAt > now)
            .ToListAsync(cancellationToken);
        foreach (var session in sessions) session.RevokedAt = now;
        if (sessions.Count > 0)
        {
            await _audit.AppendAsync(new AuditEntry
            {
                TenantId = tenantId, EntityType = "User", EntityId = userId, Action = "AccountOtherSessionsRevoked",
                Changes = "{\"otherSessionsRevoked\":true}", NewValues = $"{{\"count\":{sessions.Count}}}",
                UserId = userId, IpAddress = ipAddress, UserAgent = Limit(userAgent, 500), CreatedBy = userId.ToString()
            }, cancellationToken);
            try { await _context.SaveChangesAsync(cancellationToken); }
            catch (DbUpdateConcurrencyException)
            {
                return Result<RevokeOtherSessionsResponse>.Failure(Error.Conflict("One or more sessions changed concurrently. Reload and try again."));
            }
        }
        return Result<RevokeOtherSessionsResponse>.Success(new RevokeOtherSessionsResponse(sessions.Count));
    }

    private Task<List<AccountSessionDto>> GetActiveSessionsCoreAsync(
        Guid userId, Guid tenantId, Guid currentSessionId, DateTime now, CancellationToken cancellationToken) =>
        _context.UserSessions.AsNoTracking()
            .Where(session => session.TenantId == tenantId && session.UserId == userId
                && session.RevokedAt == null && session.ExpiresAt > now)
            .OrderByDescending(session => session.CreatedAt)
            .Select(session => new AccountSessionDto(
                session.Id, session.CreatedAt, session.ExpiresAt, session.IpAddress,
                session.UserAgent, session.Id == currentSessionId))
            .ToListAsync(cancellationToken);

    private static string? Limit(string? value, int maxLength) =>
        string.IsNullOrWhiteSpace(value) ? null : value[..Math.Min(value.Length, maxLength)];
}
