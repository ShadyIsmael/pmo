using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Moq;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.Identity.Application;
using NEXUS.Modules.Identity.Domain;
using NEXUS.Modules.Identity.Infrastructure;
using NEXUS.SharedKernel;
using Xunit;

namespace NEXUS.Api.Tests.Integration;

public sealed class LocalPasswordLifecycleTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private PermissionTestDbContext _context = null!;
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Mock<IAuditService> _audit = new();
    private readonly PasswordHasher<User> _hasher = new();
    private readonly LocalPasswordPolicyOptions _policy = new() { TemporaryPasswordExpiryHours = 6, MinimumLength = 12, MaximumLength = 64 };

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        var options = new DbContextOptionsBuilder<PermissionTestDbContext>().UseSqlite(_connection).Options;
        _context = new PermissionTestDbContext(options);
        await _context.Database.EnsureCreatedAsync();
        _audit.Setup(service => service.AppendAsync(It.IsAny<AuditEntry>(), It.IsAny<CancellationToken>()))
            .Callback<AuditEntry, CancellationToken>((entry, _) => _context.AuditEntries.Add(entry))
            .ReturnsAsync(Result.Success());
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task Create_local_user_generates_hashed_one_time_temporary_password_and_safe_audit()
    {
        var service = CreateUserService();
        var result = await service.CreateAsync(new CreateUserDto
        {
            Email = "local@example.test",
            FirstName = "Local",
            LastName = "User",
            Roles = []
        }, Guid.NewGuid().ToString());

        Assert.True(result.IsSuccess);
        var temporaryPassword = Assert.IsType<string>(result.Value!.TemporaryPassword);
        var user = await _context.Users.SingleAsync();
        Assert.Equal(AuthenticationSource.Local, user.AuthenticationSource);
        Assert.True(user.MustChangePassword);
        Assert.InRange(user.TemporaryPasswordExpiresAt!.Value, DateTime.UtcNow.AddHours(5.9), DateTime.UtcNow.AddHours(6.1));
        Assert.NotEqual(temporaryPassword, user.PasswordHash);
        Assert.Equal(PasswordVerificationResult.Success, _hasher.VerifyHashedPassword(user, user.PasswordHash!, temporaryPassword));
        Assert.DoesNotContain("temporaryPassword", JsonSerializer.Serialize(result.Value.User), StringComparison.OrdinalIgnoreCase);

        var audit = await _context.AuditEntries.SingleAsync();
        Assert.Equal("LocalUserCreated", audit.Action);
        Assert.DoesNotContain(temporaryPassword, audit.Changes, StringComparison.Ordinal);
        Assert.DoesNotContain(temporaryPassword, audit.NewValues, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Login_with_temporary_password_returns_password_change_state_without_application_permissions()
    {
        var user = AddLocalUser("temporary-login@example.test", "Temporary!234567");
        user.MustChangePassword = true;
        user.TemporaryPasswordExpiresAt = DateTime.UtcNow.AddHours(2);
        user.PasswordChangedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var auth = CreateAuthService();
        var result = await auth.LoginAsync(new LoginCommand(user.Email, "Temporary!234567", _tenantId.ToString()));

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.PasswordChangeRequired);
        Assert.True(result.Value.User.MustChangePassword);
        Assert.Empty(result.Value.User.Permissions);
        Assert.Empty(result.Value.User.Roles);
        Assert.Single(await _context.UserSessions.ToListAsync());
    }

    [Fact]
    public async Task Expired_temporary_password_is_rejected_at_login()
    {
        var user = AddLocalUser("expired@example.test", "Temporary!234567");
        user.MustChangePassword = true;
        user.TemporaryPasswordExpiresAt = DateTime.UtcNow.AddMinutes(-1);
        await _context.SaveChangesAsync();

        var result = await CreateAuthService().LoginAsync(new LoginCommand(user.Email, "Temporary!234567", _tenantId.ToString()));

        Assert.True(result.IsFailure);
        Assert.Equal("TEMPORARY_PASSWORD_EXPIRED", result.Error!.Code);
        Assert.Empty(await _context.UserSessions.ToListAsync());
    }

    [Fact]
    public async Task Password_change_clears_temporary_state_and_revokes_old_sessions()
    {
        var user = AddLocalUser("change@example.test", "Temporary!234567");
        user.MustChangePassword = true;
        user.TemporaryPasswordExpiresAt = DateTime.UtcNow.AddHours(1);
        user.PasswordChangedAt = DateTime.UtcNow.AddMinutes(-5);
        var session = new UserSession { TenantId = _tenantId, UserId = user.Id, Token = "token", RefreshToken = "refresh-hash", ExpiresAt = DateTime.UtcNow.AddDays(2), CreatedBy = "test" };
        _context.UserSessions.Add(session);
        await _context.SaveChangesAsync();

        var changed = await CreateAuthService().ChangeInitialPasswordAsync(user.Id, _tenantId,
            new ChangeInitialPasswordCommand("Temporary!234567", "NewPermanent!234567"));

        Assert.True(changed.IsSuccess);
        var saved = await _context.Users.SingleAsync();
        Assert.False(saved.MustChangePassword);
        Assert.Null(saved.TemporaryPasswordExpiresAt);
        Assert.NotNull(saved.PasswordChangedAt);
        Assert.Equal(PasswordVerificationResult.Success, _hasher.VerifyHashedPassword(saved, saved.PasswordHash!, "NewPermanent!234567"));
        Assert.Equal(DateTime.UtcNow.Date, (await _context.UserSessions.SingleAsync()).RevokedAt!.Value.Date);
        var audit = await _context.AuditEntries.SingleAsync();
        Assert.Equal("FirstPasswordChanged", audit.Action);
        Assert.DoesNotContain("NewPermanent!234567", audit.Changes, StringComparison.Ordinal);
        Assert.DoesNotContain("NewPermanent!234567", audit.NewValues, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Forced_password_state_blocks_other_apis_but_allows_change_and_expiry_is_explicit()
    {
        var user = AddLocalUser("blocked@example.test", "Temporary!234567");
        user.MustChangePassword = true;
        user.PasswordChangedAt = DateTime.UtcNow.AddMinutes(-2);
        user.TemporaryPasswordExpiresAt = DateTime.UtcNow.AddHours(1);
        await _context.SaveChangesAsync();

        var (blocked, body, continued) = await InvokeEnforcementAsync(user, "/api/projects", "GET");
        Assert.Equal(StatusCodes.Status403Forbidden, blocked);
        Assert.False(continued);
        Assert.Equal("PASSWORD_CHANGE_REQUIRED", JsonDocument.Parse(body).RootElement.GetProperty("code").GetString());

        var (allowed, _, changeContinued) = await InvokeEnforcementAsync(user, "/api/auth/change-initial-password", "POST");
        Assert.Equal(StatusCodes.Status200OK, allowed);
        Assert.True(changeContinued);

        user.TemporaryPasswordExpiresAt = DateTime.UtcNow.AddMinutes(-1);
        await _context.SaveChangesAsync();
        var (expiredStatus, expiredBody, expiredContinued) = await InvokeEnforcementAsync(user, "/api/projects", "GET");
        Assert.Equal(StatusCodes.Status403Forbidden, expiredStatus);
        Assert.False(expiredContinued);
        Assert.Equal("TEMPORARY_PASSWORD_EXPIRED", JsonDocument.Parse(expiredBody).RootElement.GetProperty("code").GetString());
    }

    private UserAdministrationService CreateUserService()
    {
        var http = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("tenantId", _tenantId.ToString())], "test"))
            }
        };
        return new UserAdministrationService(_context, _hasher, http, new UserEffectivePermissionService(_context), _audit.Object, Options.Create(_policy));
    }

    private DemoAuthService CreateAuthService()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Secret"] = "Test-Signing-Secret-Key-At-Least-32-Bytes",
            ["Jwt:Issuer"] = "NEXUS",
            ["Jwt:Audience"] = "NEXUS-API"
        }).Build();
        return new DemoAuthService(_context, configuration, _hasher, new UserEffectivePermissionService(_context), _audit.Object, Options.Create(_policy));
    }

    private User AddLocalUser(string email, string password)
    {
        var user = new User
        {
            TenantId = _tenantId,
            Email = email,
            FirstName = "Test",
            LastName = "User",
            AuthenticationSource = AuthenticationSource.Local,
            PasswordHash = "",
            IsActive = true,
            CreatedBy = "test"
        };
        user.PasswordHash = _hasher.HashPassword(user, password);
        _context.Users.Add(user);
        return user;
    }

    private async Task<(int StatusCode, string Body, bool Continued)> InvokeEnforcementAsync(User user, string path, string method)
    {
        var continued = false;
        var middleware = new InitialPasswordEnforcementMiddleware(_ => { continued = true; return Task.CompletedTask; });
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Request.Method = method;
        context.User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim("tenantId", user.TenantId.ToString()),
            new Claim("pwdts", user.PasswordChangedAt?.Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "0")
        ], "test"));
        await using var responseBody = new MemoryStream();
        context.Response.Body = responseBody;
        await middleware.InvokeAsync(context, _context);
        responseBody.Position = 0;
        using var reader = new StreamReader(responseBody);
        return (context.Response.StatusCode, await reader.ReadToEndAsync(), continued);
    }
}
