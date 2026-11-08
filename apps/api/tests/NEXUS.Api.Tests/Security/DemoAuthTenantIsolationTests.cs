using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Moq;
using NEXUS.Api.Tests.Testing;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Identity.Application;
using NEXUS.Modules.Identity.Domain;
using NEXUS.Modules.Identity.Infrastructure;
using NEXUS.Persistence;
using NEXUS.SharedKernel.Helpers;
using Xunit;

namespace NEXUS.Api.Tests.Security;

[Collection("Static tenant resolution")]
public sealed class DemoAuthTenantIsolationTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly AppDbContext _db;
    private readonly HttpContextAccessor _requestContext = new();
    private readonly Guid _demoUserId = Guid.NewGuid();
    private readonly Guid _tenantA = Guid.NewGuid();
    private readonly Guid _tenantAUserId = Guid.NewGuid();
    private readonly Mock<IPasswordHasher<User>> _passwordHasher = new();
    private readonly Mock<IUserEffectivePermissionService> _effectivePermissions = new();
    private readonly DemoAuthService _service;

    public DemoAuthTenantIsolationTests()
    {
        _connection.Open();
        _requestContext.HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("tenantId", TenantConstants.DemoTenantId.ToString()), new Claim(ClaimTypes.NameIdentifier, _demoUserId.ToString())],
                authenticationType: "test"))
        };
        AppDbContext.ConfigureTenantResolution(_requestContext);
        _db = new AmbiguousAccountSqliteTestAppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options);
        _db.Database.EnsureCreated();
        _db.Users.AddRange(
            NewUser(_demoUserId, TenantConstants.DemoTenantId, "demo@example.test"),
            NewUser(_tenantAUserId, _tenantA, "tenant-a@example.test"));
        _db.SaveChanges();
        SetAnonymousLoginContext();

        _effectivePermissions
            .Setup(service => service.GetEffectivePermissionsAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>(StringComparer.Ordinal));
        _effectivePermissions
            .Setup(service => service.GetEffectiveRoleNamesAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>(StringComparer.Ordinal));

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = new string('S', 48),
                ["Jwt:Issuer"] = "NEXUS",
                ["Jwt:Audience"] = "NEXUS-API"
            })
            .Build();
        var passwordPolicy = new Mock<IOptionsMonitor<LocalPasswordPolicyOptions>>();
        passwordPolicy.SetupGet(options => options.CurrentValue).Returns(new LocalPasswordPolicyOptions());
        var environment = new Mock<IWebHostEnvironment>();
        environment.SetupGet(host => host.EnvironmentName).Returns("Development");

        _service = new DemoAuthService(
            _db,
            configuration,
            _passwordHasher.Object,
            _effectivePermissions.Object,
            Mock.Of<IAuditService>(),
            passwordPolicy.Object,
            environment.Object);
    }

    [Theory]
    [InlineData("not-a-tenant-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task Login_with_malformed_tenant_fails_closed(string tenantId)
    {
        var result = await _service.LoginAsync(new LoginCommand("demo@example.test", "dummy-password", tenantId));

        Assert.True(result.IsFailure);
        Assert.Equal(401, result.Error?.StatusCode);
        Assert.Equal("Invalid email or password", result.Error?.Message);
        _passwordHasher.Verify(
            hasher => hasher.VerifyHashedPassword(It.IsAny<User>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
        Assert.Empty(await _db.UserSessions.IgnoreQueryFilters().ToListAsync());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Login_without_or_blank_tenant_allows_a_unique_email_and_binds_token_to_persisted_tenant(string? tenantId)
    {
        _passwordHasher
            .Setup(hasher => hasher.VerifyHashedPassword(It.IsAny<User>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(PasswordVerificationResult.Success);

        var result = await _service.LoginAsync(new LoginCommand("demo@example.test", "dummy-password", tenantId));

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(TenantConstants.DemoTenantId.ToString(), result.Value!.User.TenantId);
        var token = new JwtSecurityTokenHandler().ReadJwtToken(result.Value.Token);
        Assert.Contains(token.Claims, claim => claim.Type == "tenantId" && claim.Value == TenantConstants.DemoTenantId.ToString());
        Assert.Single(await _db.UserSessions.IgnoreQueryFilters().Where(session => session.UserId == _demoUserId).ToListAsync());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Login_without_or_blank_tenant_rejects_duplicate_email_across_tenants(string? tenantId)
    {
        SetAuthenticatedTenant(_tenantA);
        _db.Users.Add(NewUser(Guid.NewGuid(), _tenantA, "demo@example.test"));
        await _db.SaveChangesAsync();
        SetAnonymousLoginContext();

        var result = await _service.LoginAsync(new LoginCommand("demo@example.test", "dummy-password", tenantId));

        Assert.True(result.IsFailure);
        Assert.Equal(401, result.Error?.StatusCode);
        Assert.Equal("Invalid email or password", result.Error?.Message);
        _passwordHasher.Verify(
            hasher => hasher.VerifyHashedPassword(It.IsAny<User>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
        Assert.Empty(await _db.UserSessions.IgnoreQueryFilters().ToListAsync());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task Current_user_lookup_without_a_tenant_fails_closed(string? tenantId)
    {
        var requestedTenant = Guid.TryParse(tenantId, out var parsed) ? parsed : (Guid?)null;

        var result = await _service.GetCurrentUserAsync(_demoUserId, requestedTenant);

        Assert.True(result.IsFailure);
        Assert.Equal(401, result.Error?.StatusCode);
    }

    [Fact]
    public async Task Current_user_lookup_with_the_matching_tenant_still_succeeds()
    {
        var result = await _service.GetCurrentUserAsync(_tenantAUserId, _tenantA);

        Assert.True(result.IsSuccess);
        Assert.Equal(_tenantAUserId, result.Value!.Id);
        Assert.Equal(_tenantA.ToString(), result.Value.TenantId);
    }

    [Fact]
    public async Task Refresh_and_cookie_logout_revoke_sessions_in_the_verified_tenant()
    {
        _passwordHasher
            .Setup(hasher => hasher.VerifyHashedPassword(It.IsAny<User>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(PasswordVerificationResult.Success);

        var login = await _service.LoginAsync(new LoginCommand("demo@example.test", "dummy-password", TenantConstants.DemoTenantId.ToString()));
        Assert.True(login.IsSuccess, login.Error?.Message);

        var refresh = await _service.RefreshAsync(login.Value!.RefreshToken!);
        Assert.True(refresh.IsSuccess, refresh.Error?.Message);
        var sessionsAfterRefresh = await _db.UserSessions.IgnoreQueryFilters()
            .Where(session => session.UserId == _demoUserId)
            .ToListAsync();
        Assert.Equal(2, sessionsAfterRefresh.Count);
        var originalSessionId = Guid.Parse(new JwtSecurityTokenHandler().ReadJwtToken(login.Value.Token)
            .Claims.Single(claim => claim.Type == "sid").Value);
        var rotatedSessionId = Guid.Parse(new JwtSecurityTokenHandler().ReadJwtToken(refresh.Value!.Token)
            .Claims.Single(claim => claim.Type == "sid").Value);
        Assert.NotNull(sessionsAfterRefresh.Single(session => session.Id == originalSessionId).RevokedAt);
        Assert.Null(sessionsAfterRefresh.Single(session => session.Id == rotatedSessionId).RevokedAt);

        var logout = await _service.LogoutCookieSessionAsync(refresh.Value!.RefreshToken);
        Assert.True(logout.IsSuccess, logout.Error?.Message);
        Assert.All(
            await _db.UserSessions.IgnoreQueryFilters().Where(session => session.UserId == _demoUserId).ToListAsync(),
            session => Assert.NotNull(session.RevokedAt));
    }

    private void SetAnonymousLoginContext()
    {
        var context = new DefaultHttpContext();
        context.SetEndpoint(new Endpoint(
            null,
            new EndpointMetadataCollection(new AllowAnonymousAttribute()),
            "POST /api/auth/login"));
        _requestContext.HttpContext = context;
    }

    private void SetAuthenticatedTenant(Guid tenantId)
    {
        _requestContext.HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("tenantId", tenantId.ToString("D"))],
                authenticationType: "test"))
        };
    }

    private static User NewUser(Guid id, Guid tenantId, string email) => new()
    {
        Id = id,
        TenantId = tenantId,
        Email = email,
        PasswordHash = "hashed-fixture",
        FirstName = "Test",
        LastName = "User",
        CreatedBy = "tenant-boundary-test"
    };

    /// <summary>
    /// Keeps the production tenant filters and SQLite provider adaptations, but
    /// allows the fixture to model an ambiguous legacy account set. The login
    /// service must reject multiple tenant matches before verifying a password.
    /// </summary>
    private sealed class AmbiguousAccountSqliteTestAppDbContext(DbContextOptions<AppDbContext> options)
        : SqliteTestAppDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<User>().HasIndex(user => user.Email).IsUnique(false);
        }
    }

    public void Dispose()
    {
        AppDbContext.ConfigureTenantResolution(new HttpContextAccessor());
        _db.Dispose();
        _connection.Dispose();
    }
}
