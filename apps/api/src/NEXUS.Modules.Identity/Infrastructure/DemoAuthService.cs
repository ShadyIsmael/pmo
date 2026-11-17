using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.Identity.Application;
using NEXUS.Modules.Identity.Domain;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Helpers;
using NEXUS.SharedKernel.Security;

namespace NEXUS.Modules.Identity.Infrastructure;

public class DemoAuthService : IAuthenticationService
{
    private readonly DbContext _context;
    private readonly IConfiguration _configuration;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IUserEffectivePermissionService _effectivePermissions;
    private readonly IAuditService _auditService;
    private readonly IOptionsMonitor<LocalPasswordPolicyOptions> _passwordPolicy;
    private readonly IHostEnvironment _environment;
    private readonly IUserOrganizationDirectory? _organizationDirectory;

    public DemoAuthService(
        DbContext context,
        IConfiguration configuration,
        IPasswordHasher<User> passwordHasher,
        IUserEffectivePermissionService effectivePermissions,
        IAuditService auditService,
        IOptionsMonitor<LocalPasswordPolicyOptions> passwordPolicy,
        IHostEnvironment environment,
        IUserOrganizationDirectory? organizationDirectory = null)
    {
        _context = context;
        _configuration = configuration;
        _passwordHasher = passwordHasher;
        _effectivePermissions = effectivePermissions;
        _auditService = auditService;
        _passwordPolicy = passwordPolicy;
        _environment = environment;
        _organizationDirectory = organizationDirectory;
    }

    public async Task<Result<LoginResponseDto>> LoginAsync(LoginCommand command, CancellationToken cancellationToken = default)
    {
        LoginUserSnapshot? user;
        if (string.IsNullOrWhiteSpace(command.TenantId))
        {
            var matchingAccounts = await _context.Set<User>()
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(candidate => candidate.Email == command.Email && !candidate.IsDeleted)
                .Select(candidate => new { candidate.Id, candidate.TenantId })
                .Take(2)
                .ToListAsync(cancellationToken);

            if (matchingAccounts.Count != 1 || matchingAccounts[0].TenantId == Guid.Empty)
                return Result<LoginResponseDto>.Failure(Error.Unauthorized("Invalid email or password"));

            user = await GetLoginUserByEmailAsync(command.Email, matchingAccounts[0].TenantId, cancellationToken);
        }
        else
        {
            if (!Guid.TryParse(command.TenantId, out var tenantId) || tenantId == Guid.Empty)
                return Result<LoginResponseDto>.Failure(Error.Unauthorized("Invalid email or password"));

            user = await GetLoginUserByEmailAsync(command.Email, tenantId, cancellationToken);
        }

        if (user is null)
            return Result<LoginResponseDto>.Failure(Error.Unauthorized("Invalid email or password"));

        if (user.AuthenticationSource != AuthenticationSource.Local || string.IsNullOrEmpty(user.PasswordHash))
            return Result<LoginResponseDto>.Failure(Error.Unauthorized("Invalid email or password"));

        var verificationUser = user.ToPasswordVerificationUser();
        var verification = _passwordHasher.VerifyHashedPassword(verificationUser, user.PasswordHash, command.Password);
        if (verification == PasswordVerificationResult.Failed)
            return Result<LoginResponseDto>.Failure(Error.Unauthorized("Invalid email or password"));

        if (!user.IsActive)
            return Result<LoginResponseDto>.Failure(Error.Forbidden("Account is deactivated"));

        var bypassInitialPasswordPolicy = IsDevelopmentAdminPasswordBypassEnabled(user.TenantId, user.Email, user.AuthenticationSource);
        if (user.MustChangePassword && !bypassInitialPasswordPolicy
            && (!user.TemporaryPasswordExpiresAt.HasValue || user.TemporaryPasswordExpiresAt <= DateTime.UtcNow))
            return Result<LoginResponseDto>.Failure(new Error("TEMPORARY_PASSWORD_EXPIRED", "The temporary password has expired. Contact an administrator to generate a new one.", 401));

        if (user.MustChangePassword && !bypassInitialPasswordPolicy)
        {
            user.Permissions.Clear();
            user.Roles.Clear();
        }
        else if (bypassInitialPasswordPolicy)
        {
            // This is a detached login snapshot; persisted account state stays intact.
            user.MustChangePassword = false;
        }

        // Session creation is authoritative. LastLoginAt is best-effort metadata and
        // must not turn a valid concurrent login into a failed request.
        Result<LoginResponseDto> loginResult;
        using (PreAuthenticationTenantExecutionContext.EnterAfterCredentialValidation(user.TenantId))
        {
            loginResult = await IssueSessionAsync(user, cancellationToken);
            await TryUpdateLastLoginAsync(user.Id, cancellationToken);
        }

        return loginResult;
    }

    public async Task<Result<LoginResponseDto>> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            return Result<LoginResponseDto>.Failure(Error.Unauthorized("Refresh token is required"));

        var tokenHash = HashToken(refreshToken);
        var session = await _context.Set<UserSession>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.RefreshToken == tokenHash
                && s.UserId != Guid.Empty
                && s.TenantId != Guid.Empty
                && !s.IsDeleted, cancellationToken);

        if (session is null || session.RevokedAt is not null || session.ExpiresAt <= DateTime.UtcNow)
            return Result<LoginResponseDto>.Failure(Error.Unauthorized("Invalid or expired refresh token"));

        var user = await GetUserWithRolesByIdAsync(session.UserId, session.TenantId, cancellationToken);
        if (user is null || !user.IsActive || user.TenantId != session.TenantId)
            return Result<LoginResponseDto>.Failure(Error.Unauthorized("User account is not available"));

        if (user.AuthenticationSource != AuthenticationSource.Local || string.IsNullOrEmpty(user.PasswordHash))
            return Result<LoginResponseDto>.Failure(Error.Unauthorized("User account is not available for local authentication"));

        using var tenantScope = PreAuthenticationTenantExecutionContext.EnterAfterCredentialValidation(user.TenantId);
        var bypassInitialPasswordPolicy = IsDevelopmentAdminPasswordBypassEnabled(user.TenantId, user.Email, user.AuthenticationSource);
        if (user.MustChangePassword && !bypassInitialPasswordPolicy
            && (!user.TemporaryPasswordExpiresAt.HasValue || user.TemporaryPasswordExpiresAt <= DateTime.UtcNow))
        {
            session.RevokedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
            return Result<LoginResponseDto>.Failure(new Error("TEMPORARY_PASSWORD_EXPIRED", "The temporary password has expired. Contact an administrator to generate a new one.", 401));
        }

        session.RevokedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        return await IssueSessionAsync(user, cancellationToken);
    }

    public async Task<Result<ChangeInitialPasswordResponseDto>> ChangeInitialPasswordAsync(Guid userId, Guid tenantId, ChangeInitialPasswordCommand command, CancellationToken cancellationToken = default)
    {
        var user = await _context.Set<User>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(candidate => candidate.Id == userId && candidate.TenantId == tenantId && !candidate.IsDeleted, cancellationToken);

        if (user is null || !user.IsActive || user.AuthenticationSource != AuthenticationSource.Local || string.IsNullOrEmpty(user.PasswordHash))
            return Result<ChangeInitialPasswordResponseDto>.Failure(Error.Unauthorized("The local account is not available"));
        if (!user.MustChangePassword)
            return Result<ChangeInitialPasswordResponseDto>.Failure(Error.Conflict("No initial password change is required"));

        var now = DateTime.UtcNow;
        if (!user.TemporaryPasswordExpiresAt.HasValue || user.TemporaryPasswordExpiresAt <= now)
            return Result<ChangeInitialPasswordResponseDto>.Failure(new Error("TEMPORARY_PASSWORD_EXPIRED", "The temporary password has expired. Contact an administrator to generate a new one.", 401));

        var verification = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, command.CurrentPassword ?? string.Empty);
        if (verification == PasswordVerificationResult.Failed)
            return Result<ChangeInitialPasswordResponseDto>.Failure(Error.Unauthorized("The current password is incorrect"));

        var newPassword = command.NewPassword ?? string.Empty;
        var passwordPolicy = _passwordPolicy.CurrentValue;
        if (newPassword.Length < passwordPolicy.MinimumLength || newPassword.Length > passwordPolicy.MaximumLength)
            return Result<ChangeInitialPasswordResponseDto>.Failure(Error.Validation($"The new password must be between {passwordPolicy.MinimumLength} and {passwordPolicy.MaximumLength} characters."));
        if (string.Equals(command.CurrentPassword, newPassword, StringComparison.Ordinal))
            return Result<ChangeInitialPasswordResponseDto>.Failure(Error.Validation("The new password must be different from the temporary password."));

        user.PasswordHash = _passwordHasher.HashPassword(user, newPassword);
        user.MustChangePassword = false;
        user.TemporaryPasswordExpiresAt = null;
        user.PasswordChangedAt = now;
        user.UpdatedAt = now;
        user.UpdatedBy = userId.ToString();

        var sessions = await _context.Set<UserSession>()
            .IgnoreQueryFilters()
            .Where(session => session.UserId == userId && session.TenantId == tenantId && session.RevokedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var session in sessions)
            session.RevokedAt = now;

        await _auditService.AppendAsync(new AuditEntry
        {
            TenantId = tenantId,
            EntityType = "User",
            EntityId = user.Id,
            Action = "FirstPasswordChanged",
            Changes = "{\"PasswordState\":\"Permanent\",\"MustChangePassword\":false}",
            OriginalValues = "{\"PasswordState\":\"Temporary\",\"MustChangePassword\":true}",
            NewValues = "{\"PasswordState\":\"Permanent\",\"MustChangePassword\":false}",
            UserId = user.Id,
            UserName = user.FullName,
            CreatedBy = user.Id.ToString()
        }, cancellationToken);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result<ChangeInitialPasswordResponseDto>.Failure(Error.Conflict("The account changed concurrently. Sign in again and try once more."));
        }

        return Result<ChangeInitialPasswordResponseDto>.Success(new ChangeInitialPasswordResponseDto());
    }

    public async Task<Result> LogoutAsync(Guid userId, string? refreshToken, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(refreshToken))
        {
            var tokenHash = HashToken(refreshToken);
            var session = await _context.Set<UserSession>()
                .FirstOrDefaultAsync(s => s.RefreshToken == tokenHash && s.UserId == userId, cancellationToken);

            if (session is not null)
            {
                session.RevokedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync(cancellationToken);
            }

            return Result.Success();
        }

        var activeSessions = await _context.Set<UserSession>()
            .Where(s => s.UserId == userId && s.RevokedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var session in activeSessions)
            session.RevokedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> LogoutCookieSessionAsync(string? refreshToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken)) return Result.Success();

        var tokenHash = HashToken(refreshToken);
        var session = await _context.Set<UserSession>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(candidate => candidate.RefreshToken == tokenHash
                && candidate.TenantId != Guid.Empty
                && !candidate.IsDeleted, cancellationToken);
        if (session is not null && session.RevokedAt is null)
        {
            using var tenantScope = PreAuthenticationTenantExecutionContext.EnterAfterCredentialValidation(session.TenantId);
            session.RevokedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }

        return Result.Success();
    }

    public async Task<Result<CurrentUserDto>> GetCurrentUserAsync(Guid userId, Guid? tenantId = null, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || !tenantId.HasValue || tenantId.Value == Guid.Empty)
            return Result<CurrentUserDto>.Failure(Error.Unauthorized("A valid authenticated user and tenant are required."));

        var user = await GetUserWithRolesByIdAsync(userId, tenantId.Value, cancellationToken);

        if (user is null)
            return Result<CurrentUserDto>.Failure(Error.NotFound("User not found"));

        // Derive the user's organizations from active role assignments.
        // Organization membership is determined by the OrganizationId of assigned roles.
        // Each role carries its authoritative OrganizationId; a user's organizations are the
        // set of Organizations for which they have at least one active, assigned role.
        var assignedOrganizationIds = await _context.Set<UserRole>()
            .AsNoTracking()
            .Where(ur => ur.UserId == userId && ur.TenantId == tenantId.Value && !ur.IsDeleted
                && ur.User.TenantId == tenantId.Value && ur.User.IsActive && !ur.User.IsDeleted
                && ur.Role.TenantId == tenantId.Value && !ur.Role.IsDeleted
                && ur.Role.OrganizationId.HasValue)
            .Select(ur => ur.Role.OrganizationId.GetValueOrDefault())
            .Distinct()
            .ToListAsync(cancellationToken);

        var userOrganizations = assignedOrganizationIds;
        if (assignedOrganizationIds.Count > 0)
        {
            if (_organizationDirectory is null)
            {
                userOrganizations = new List<Guid>();
            }
            else
            {
                var activeOrganizations = await _organizationDirectory.GetActiveNamesAsync(
                    tenantId.Value, assignedOrganizationIds, cancellationToken);
                userOrganizations = assignedOrganizationIds
                    .Where(activeOrganizations.ContainsKey)
                    .ToList();
            }
        }

        // Compute effective permissions per organization and union them together.
        // This ensures that organization-owned role grants become effective when the
        // user's session has the corresponding organization context.
        var effectivePermissions = new HashSet<string>(StringComparer.Ordinal);

        if (userOrganizations.Count > 0)
        {
            foreach (var organizationId in userOrganizations)
            {
                var orgPermissions = await _effectivePermissions.GetEffectivePermissionsForOrganizationAsync(
                    userId, tenantId.Value, organizationId, cancellationToken);
                foreach (var perm in orgPermissions)
                    effectivePermissions.Add(perm);
            }
        }
        else
        {
            // Fall back to tenant-scoped permissions when the user has no organization membership.
            var tenantPermissions = await _effectivePermissions.GetEffectivePermissionsAsync(
                userId, tenantId.Value, cancellationToken);
            foreach (var perm in tenantPermissions)
                effectivePermissions.Add(perm);
        }

        var currentUser = MapToCurrentUser(user);
        currentUser.Permissions = effectivePermissions
            .OrderBy(permission => permission, StringComparer.Ordinal)
            .ToList();
        var bypassInitialPasswordPolicy = IsDevelopmentAdminPasswordBypassEnabled(user.TenantId, user.Email, user.AuthenticationSource);
        var mustChangePassword = user.MustChangePassword && !bypassInitialPasswordPolicy;
        currentUser.MustChangePassword = mustChangePassword;
        if (mustChangePassword)
        {
            currentUser.Permissions.Clear();
            currentUser.Roles.Clear();
        }
        return Result<CurrentUserDto>.Success(currentUser);
    }

    public async Task<Result<CurrentUserDto>> SwitchRoleAsync(Guid userId, Guid tenantId, string roleName, CancellationToken cancellationToken = default)
    {
        return await GetCurrentUserAsync(userId, tenantId, cancellationToken);
    }

    private Task<Result<LoginResponseDto>> IssueSessionAsync(User user, CancellationToken cancellationToken)
    {
        return IssueSessionWithCurrentPermissionsAsync(user, cancellationToken);
    }

    private async Task<Result<LoginResponseDto>> IssueSessionWithCurrentPermissionsAsync(User user, CancellationToken cancellationToken)
    {
        var snapshot = CreateLoginUserSnapshot(user);
        var bypassInitialPasswordPolicy = IsDevelopmentAdminPasswordBypassEnabled(user.TenantId, user.Email, user.AuthenticationSource);
        snapshot.MustChangePassword = snapshot.MustChangePassword && !bypassInitialPasswordPolicy;
        if (snapshot.MustChangePassword)
        {
            snapshot.Permissions = new List<string>();
            snapshot.Roles = new List<RoleDto>();
        }
        else
        {
            snapshot.Permissions = (await _effectivePermissions.GetEffectivePermissionsAsync(user.Id, user.TenantId, cancellationToken))
                .OrderBy(permission => permission, StringComparer.Ordinal)
                .ToList();
        }
        return await IssueSessionAsync(snapshot, cancellationToken);
    }

    private async Task<Result<LoginResponseDto>> IssueSessionAsync(LoginUserSnapshot user, CancellationToken cancellationToken)
    {
        var sessionId = Guid.NewGuid();
        var (token, expiresAt) = GenerateJwtToken(user, sessionId);

        var refreshToken = GenerateRefreshToken();
        var session = new UserSession
        {
            Id = sessionId,
            TenantId = user.TenantId,
            UserId = user.Id,
            Token = token,
            RefreshToken = HashToken(refreshToken),
            ExpiresAt = DateTime.UtcNow.AddDays(GetRefreshTokenLifetimeDays()),
            CreatedBy = "System"
        };

        _context.Set<UserSession>().Add(session);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<LoginResponseDto>.Success(new LoginResponseDto
        {
            Token = token,
            RefreshToken = refreshToken,
            ExpiresAt = expiresAt,
            User = MapToCurrentUser(user),
            PasswordChangeRequired = user.MustChangePassword
        });
    }

    private async Task TryUpdateLastLoginAsync(Guid userId, CancellationToken cancellationToken)
    {
        try
        {
            await _context.Set<User>()
                .Where(user => user.Id == userId)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(user => user.LastLoginAt, DateTime.UtcNow),
                    cancellationToken);
        }
        catch (DbUpdateException)
        {
            // LastLoginAt is best-effort audit metadata. The session has already been
            // committed, so a metadata update failure must not fail authentication.
        }
    }

    private async Task<LoginUserSnapshot?> GetLoginUserByEmailAsync(string email, Guid tenantId, CancellationToken cancellationToken)
    {
        var user = await _context.Set<User>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(user => user.Email == email && user.TenantId == tenantId && !user.IsDeleted)
            .Select(user => new
            {
                Id = user.Id,
                TenantId = user.TenantId,
                Email = user.Email,
                PasswordHash = user.PasswordHash,
                FirstName = user.FirstName,
                LastName = user.LastName,
                IsActive = user.IsActive,
                AuthenticationSource = user.AuthenticationSource,
                MustChangePassword = user.MustChangePassword,
                TemporaryPasswordExpiresAt = user.TemporaryPasswordExpiresAt,
                PasswordChangedAt = user.PasswordChangedAt
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (user is null)
            return null;

        var authorization = await _context.Set<UserRole>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(userRole => userRole.UserId == user.Id && userRole.TenantId == tenantId && !userRole.IsDeleted
                               && userRole.Role.TenantId == tenantId && !userRole.Role.IsDeleted)
            .Select(userRole => new LoginRoleProjection
            {
                Role = new RoleDto
                {
                    Id = userRole.Role.Id,
                    RoleKey = userRole.Role.RoleKey,
                    Name = userRole.Role.Name,
                    Description = userRole.Role.Description,
                    Category = userRole.Role.Category,
                    IsSystem = userRole.Role.IsSystem,
                    IsProtected = userRole.Role.IsSystem
                        && userRole.Role.RoleKey == Role.TenantAdminRoleKey
                        && userRole.Role.SystemRoleKey == Role.TenantAdminRoleKey,
                    IsAssignable = !userRole.Role.IsSystem
                        && userRole.Role.SystemRoleKey == null
                        && userRole.Role.RoleKey != Role.TenantAdminRoleKey
                        && userRole.Role.RoleKey != Role.OrganizationAdminRoleKey
                },
                Permissions = userRole.Role.Permissions
                    .Where(permission => permission.TenantId == tenantId && !permission.IsDeleted)
                    .Select(permission => permission.Resource + "." + permission.Action)
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        var snapshot = new LoginUserSnapshot
        {
            Id = user.Id,
            TenantId = user.TenantId,
            Email = user.Email,
            PasswordHash = user.PasswordHash,
            FirstName = user.FirstName,
            LastName = user.LastName,
            IsActive = user.IsActive,
            AuthenticationSource = user.AuthenticationSource,
            MustChangePassword = user.MustChangePassword,
            TemporaryPasswordExpiresAt = user.TemporaryPasswordExpiresAt,
            PasswordChangedAt = user.PasswordChangedAt,
            Roles = authorization.Select(item => item.Role).ToList(),
            Permissions = authorization
                .SelectMany(item => item.Permissions)
                .Distinct()
                .ToList()
        };
        snapshot.Permissions = (await _effectivePermissions.GetEffectivePermissionsAsync(user.Id, tenantId, cancellationToken))
            .OrderBy(permission => permission, StringComparer.Ordinal)
            .ToList();
        return snapshot;
    }

    private async Task<User?> GetUserWithRolesByIdAsync(Guid id, Guid tenantId, CancellationToken cancellationToken)
    {
        return await _context.Set<User>()
            .IgnoreQueryFilters()
            .Include(u => u.UserRoles.Where(userRole => userRole.TenantId == tenantId && !userRole.IsDeleted && userRole.Role.TenantId == tenantId && !userRole.Role.IsDeleted))
                .ThenInclude(ur => ur.Role)
                .ThenInclude(role => role.Permissions.Where(permission => permission.TenantId == tenantId && !permission.IsDeleted))
            .FirstOrDefaultAsync(u => u.Id == id && u.TenantId == tenantId && !u.IsDeleted, cancellationToken);
    }

    private bool IsDevelopmentAdminPasswordBypassEnabled(
        Guid tenantId,
        string email,
        AuthenticationSource authenticationSource) =>
        DevelopmentAdminTemporaryPasswordBypass.IsEnabled(
            tenantId,
            email,
            authenticationSource,
            _passwordPolicy.CurrentValue,
            _environment);

    private static LoginUserSnapshot CreateLoginUserSnapshot(User user)
    {
        return new LoginUserSnapshot
        {
            Id = user.Id,
            TenantId = user.TenantId,
            Email = user.Email,
            PasswordHash = user.PasswordHash,
            FirstName = user.FirstName,
            LastName = user.LastName,
            IsActive = user.IsActive,
            AuthenticationSource = user.AuthenticationSource,
            MustChangePassword = user.MustChangePassword,
            TemporaryPasswordExpiresAt = user.TemporaryPasswordExpiresAt,
            PasswordChangedAt = user.PasswordChangedAt,
            Roles = user.UserRoles.Select(userRole => new RoleDto
            {
                Id = userRole.Role.Id,
                RoleKey = userRole.Role.RoleKey,
                Name = userRole.Role.Name,
                Description = userRole.Role.Description,
                Category = userRole.Role.Category,
                IsSystem = userRole.Role.IsSystem,
                IsProtected = ProtectedTenantAdminPolicy.IsProtectedSystemAdminRole(userRole.Role),
                IsAssignable = RoleAssignmentAuthorizationPolicy.IsGenerallyAssignable(userRole.Role)
            }).ToList(),
            Permissions = user.UserRoles
                .SelectMany(userRole => userRole.Role.Permissions)
                .Select(permission => permission.Resource + "." + permission.Action)
                .Distinct()
                .ToList()
        };
    }

    private static CurrentUserDto MapToCurrentUser(User user)
    {
        var roleDtos = user.UserRoles.Select(userRole => new RoleDto
        {
            Id = userRole.Role.Id,
            RoleKey = userRole.Role.RoleKey,
            Name = userRole.Role.Name,
            Description = userRole.Role.Description,
            Category = userRole.Role.Category,
            IsSystem = userRole.Role.IsSystem,
            IsProtected = ProtectedTenantAdminPolicy.IsProtectedSystemAdminRole(userRole.Role),
            IsAssignable = RoleAssignmentAuthorizationPolicy.IsGenerallyAssignable(userRole.Role)
        }).ToList();

        var permissions = user.UserRoles
            .SelectMany(userRole => userRole.Role.Permissions)
            .Select(permission => $"{permission.Resource}.{permission.Action}")
            .Distinct()
            .ToList();

        return new CurrentUserDto
        {
            Id = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            TenantId = user.TenantId.ToString(),
            AuthenticationSource = user.AuthenticationSource,
            MustChangePassword = user.MustChangePassword,
            TemporaryPasswordExpiresAt = user.TemporaryPasswordExpiresAt,
            Roles = roleDtos,
            Permissions = permissions
        };
    }

    private static CurrentUserDto MapToCurrentUser(LoginUserSnapshot user)
    {
        return new CurrentUserDto
        {
            Id = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            TenantId = user.TenantId.ToString(),
            AuthenticationSource = user.AuthenticationSource,
            MustChangePassword = user.MustChangePassword,
            TemporaryPasswordExpiresAt = user.TemporaryPasswordExpiresAt,
            Roles = user.Roles,
            Permissions = user.Permissions
        };
    }

    private (string token, DateTime expiresAt) GenerateJwtToken(LoginUserSnapshot user, Guid sessionId)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
            JwtSigningKey.Require(_configuration[JwtSigningKey.ConfigurationKey])));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiresAt = DateTime.UtcNow.AddHours(GetAccessTokenLifetimeHours());

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Name, $"{user.FirstName} {user.LastName}"),
            new("tenantId", user.TenantId.ToString()),
            new("sid", sessionId.ToString()),
            new("pwdts", user.PasswordChangedAt?.Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "0")
        };

        foreach (var role in user.Roles.Select(role => role.Name))
            claims.Add(new Claim(ClaimTypes.Role, role));

        foreach (var permission in user.Permissions)
            claims.Add(new Claim("permission", permission));

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"] ?? "NEXUS",
            audience: _configuration["Jwt:Audience"] ?? "NEXUS-API",
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials
        );

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }

    private static string GenerateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }

    private int GetAccessTokenLifetimeHours()
    {
        return int.TryParse(_configuration["Jwt:ExpiryHours"], out var hours) && hours > 0 ? hours : 8;
    }

    private int GetRefreshTokenLifetimeDays()
    {
        return int.TryParse(_configuration["Jwt:RefreshTokenLifetimeDays"], out var days) && days > 0 ? days : 7;
    }

    private sealed class LoginUserSnapshot
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public string Email { get; set; } = string.Empty;
        public string? PasswordHash { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public AuthenticationSource AuthenticationSource { get; set; }
        public bool MustChangePassword { get; set; }
        public DateTime? TemporaryPasswordExpiresAt { get; set; }
        public DateTime? PasswordChangedAt { get; set; }
        public List<RoleDto> Roles { get; set; } = new();
        public List<string> Permissions { get; set; } = new();

        public User ToPasswordVerificationUser()
        {
            return new User
            {
                Id = Id,
                TenantId = TenantId,
                Email = Email,
                PasswordHash = PasswordHash,
                FirstName = FirstName,
                LastName = LastName,
                IsActive = IsActive
            };
        }
    }

    private sealed class LoginRoleProjection
    {
        public RoleDto Role { get; set; } = new();
        public List<string> Permissions { get; set; } = new();
    }
}
