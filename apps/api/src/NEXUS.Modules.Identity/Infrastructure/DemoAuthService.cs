using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using NEXUS.Modules.Identity.Application;
using NEXUS.Modules.Identity.Domain;
using NEXUS.SharedKernel;

namespace NEXUS.Modules.Identity.Infrastructure;

public class DemoAuthService : IAuthenticationService
{
    private readonly DbContext _context;
    private readonly IConfiguration _configuration;
    private readonly IPasswordHasher<User> _passwordHasher;

    public DemoAuthService(DbContext context, IConfiguration configuration, IPasswordHasher<User> passwordHasher)
    {
        _context = context;
        _configuration = configuration;
        _passwordHasher = passwordHasher;
    }

    public async Task<Result<LoginResponseDto>> LoginAsync(LoginCommand command, CancellationToken cancellationToken = default)
    {
        var user = await GetUserWithRolesByEmailAsync(command.Email, cancellationToken);

        if (user is null)
            return Result<LoginResponseDto>.Failure(Error.Unauthorized("Invalid email or password"));

        var verification = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, command.Password);
        if (verification == PasswordVerificationResult.Failed)
            return Result<LoginResponseDto>.Failure(Error.Unauthorized("Invalid email or password"));

        if (!user.IsActive)
            return Result<LoginResponseDto>.Failure(Error.Forbidden("Account is deactivated"));

        user.LastLoginAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        return await IssueSessionAsync(user, cancellationToken);
    }

    public async Task<Result<LoginResponseDto>> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            return Result<LoginResponseDto>.Failure(Error.Unauthorized("Refresh token is required"));

        var tokenHash = HashToken(refreshToken);
        var session = await _context.Set<UserSession>()
            .FirstOrDefaultAsync(s => s.RefreshToken == tokenHash && s.UserId != Guid.Empty, cancellationToken);

        if (session is null || session.RevokedAt is not null || session.ExpiresAt <= DateTime.UtcNow)
            return Result<LoginResponseDto>.Failure(Error.Unauthorized("Invalid or expired refresh token"));

        var user = await GetUserWithRolesByIdAsync(session.UserId, cancellationToken);
        if (user is null || !user.IsActive)
            return Result<LoginResponseDto>.Failure(Error.Unauthorized("User account is not available"));

        session.RevokedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        return await IssueSessionAsync(user, cancellationToken);
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

    public async Task<Result<CurrentUserDto>> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await GetUserWithRolesByIdAsync(userId, cancellationToken);

        if (user is null)
            return Result<CurrentUserDto>.Failure(Error.NotFound("User not found"));

        return Result<CurrentUserDto>.Success(MapToCurrentUser(user));
    }

    public async Task<Result<CurrentUserDto>> SwitchRoleAsync(Guid userId, string roleName, CancellationToken cancellationToken = default)
    {
        return await GetCurrentUserAsync(userId, cancellationToken);
    }

    private async Task<Result<LoginResponseDto>> IssueSessionAsync(User user, CancellationToken cancellationToken)
    {
        var (token, expiresAt) = GenerateJwtToken(user);

        var refreshToken = GenerateRefreshToken();
        var session = new UserSession
        {
            Id = Guid.NewGuid(),
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
            User = MapToCurrentUser(user)
        });
    }

    private async Task<User?> GetUserWithRolesByEmailAsync(string email, CancellationToken cancellationToken)
    {
        return await _context.Set<User>()
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                    .ThenInclude(r => r.Permissions)
            .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
    }

    private async Task<User?> GetUserWithRolesByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _context.Set<User>()
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                .ThenInclude(r => r.Permissions)
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
    }

    private static CurrentUserDto MapToCurrentUser(User user)
    {
        var roleDtos = user.UserRoles.Select(ur => new RoleDto
        {
            Id = ur.Role.Id,
            Name = ur.Role.Name,
            Description = ur.Role.Description,
            Category = ur.Role.Category
        }).ToList();

        var permissions = user.UserRoles
            .SelectMany(ur => ur.Role.Permissions)
            .Select(p => $"{p.Resource}.{p.Action}")
            .Distinct()
            .ToList();

        return new CurrentUserDto
        {
            Id = user.Id,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            JobTitle = user.JobTitle,
            Phone = user.Phone,
            AvatarUrl = user.AvatarUrl,
            TenantId = user.TenantId.ToString(),
            Roles = roleDtos,
            Permissions = permissions
        };
    }

    private (string token, DateTime expiresAt) GenerateJwtToken(User user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
            _configuration["Jwt:Secret"] ?? "NEXUS-Demo-Secret-Key-Minimum-16-Chars!"));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiresAt = DateTime.UtcNow.AddHours(GetAccessTokenLifetimeHours());

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Name, user.FullName),
            new("tenantId", user.TenantId.ToString())
        };

        foreach (var role in user.UserRoles.Select(ur => ur.Role.Name))
            claims.Add(new Claim(ClaimTypes.Role, role));

        foreach (var permission in user.UserRoles
            .SelectMany(ur => ur.Role.Permissions)
            .Select(p => $"{p.Resource}.{p.Action}")
            .Distinct())
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
}
