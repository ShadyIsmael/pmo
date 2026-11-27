using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.Identity.Application;
using NEXUS.Modules.Identity.Domain;
using NEXUS.Modules.Identity.Infrastructure;
using NEXUS.Api.Development;
using NEXUS.Persistence;
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
        var actor = new User
        {
            TenantId = _tenantId,
            Email = "admin@example.test",
            FirstName = "Admin",
            LastName = "Actor",
            CreatedBy = "test"
        };
        var role = new Role { TenantId = _tenantId, Name = "admin", IsSystem = true, CreatedBy = "System" };
        role.AssignSystemRoleKey(Role.TenantAdminSystemRoleKey, _tenantId);
        var usersView = new Permission { TenantId = _tenantId, Name = "Users.View", Resource = "Users", Action = "View", CreatedBy = "test" };
        var manageAccounts = new Permission { TenantId = _tenantId, Name = "Users.ManageAccounts", Resource = "Users", Action = "ManageAccounts", CreatedBy = "test" };
        role.Permissions.Add(usersView);
        role.Permissions.Add(manageAccounts);
        actor.UserRoles.Add(new UserRole { TenantId = _tenantId, Role = role, CreatedBy = "test" });
        _context.Users.Add(actor);
        _context.Roles.Add(role);
        await _context.SaveChangesAsync();

        var service = CreateUserService(actor.Id);
        var result = await service.CreateAsync(new CreateUserDto
        {
            Email = "local@example.test",
            FirstName = "Local",
            LastName = "User",
            Roles = []
        }, actor.Id.ToString());

        Assert.True(result.IsSuccess);
        var temporaryPassword = Assert.IsType<string>(result.Value!.TemporaryPassword);
        var user = await _context.Users.SingleAsync(candidate => candidate.Email == "local@example.test");
        Assert.Equal(AuthenticationSource.Local, user.AuthenticationSource);
        Assert.True(user.MustChangePassword);
        Assert.InRange(user.TemporaryPasswordExpiresAt!.Value, DateTime.UtcNow.AddHours(5.9), DateTime.UtcNow.AddHours(6.1));
        Assert.NotEqual(temporaryPassword, user.PasswordHash);
        Assert.Equal(PasswordVerificationResult.Success, _hasher.VerifyHashedPassword(user, user.PasswordHash!, temporaryPassword));
        Assert.DoesNotContain(temporaryPassword, JsonSerializer.Serialize(result.Value.User), StringComparison.Ordinal);

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
        var session = await _context.UserSessions.SingleAsync();
        var accessToken = new JwtSecurityTokenHandler().ReadJwtToken(result.Value.Token);
        Assert.Equal(session.Id.ToString(), accessToken.Claims.Single(claim => claim.Type == "sid").Value);
    }

    [Fact]
    public async Task Login_without_an_existing_session_creates_a_new_session()
    {
        var user = AddLocalUser("new-session-login@example.test", "Permanent!234567");
        await _context.SaveChangesAsync();
        Assert.Empty(await _context.UserSessions.ToListAsync());

        var result = await CreateAuthService().LoginAsync(
            new LoginCommand(user.Email, "Permanent!234567", _tenantId.ToString()));

        Assert.True(result.IsSuccess);
        Assert.Single(await _context.UserSessions.ToListAsync());
    }

    [Fact]
    public async Task Initial_password_middleware_skips_explicitly_anonymous_endpoints()
    {
        var continued = false;
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                new Claim("tenantId", _tenantId.ToString())
            ], "test"))
        };
        context.SetEndpoint(new Endpoint(
            null,
            new EndpointMetadataCollection(new AllowAnonymousAttribute()),
            "POST /api/auth/login"));

        var middleware = new InitialPasswordEnforcementMiddleware(_ =>
        {
            continued = true;
            return Task.CompletedTask;
        });
        await middleware.InvokeAsync(
            context,
            _context,
            CreateEnvironment("Development"),
            CreatePasswordPolicyMonitor(() => _policy));

        Assert.True(continued);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
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
    public async Task Bootstrap_temporary_password_with_missing_expiry_is_rejected_at_login()
    {
        var temporaryPassword = GenerateTestPassword();
        var user = AddLocalUser("expired-bootstrap@example.test", temporaryPassword);
        user.MustChangePassword = true;
        user.TemporaryPasswordExpiresAt = null;
        await _context.SaveChangesAsync();

        var result = await CreateAuthService().LoginAsync(
            new LoginCommand(user.Email, temporaryPassword, _tenantId.ToString()));

        Assert.True(result.IsFailure);
        Assert.Equal("TEMPORARY_PASSWORD_EXPIRED", result.Error!.Code);
        Assert.Empty(await _context.UserSessions.ToListAsync());
    }

    [Fact]
    public async Task Development_admin_bypass_is_scoped_runtime_only_and_reverts_when_disabled()
    {
        Assert.False(new LocalPasswordPolicyOptions().DevelopmentAdminTemporaryPasswordBypassEnabled);

        var password = GenerateTestPassword();
        var role = new Role
        {
            TenantId = SeededDevelopmentAdmin.TenantId,
            Name = "admin",
            IsSystem = true,
            CreatedBy = "test"
        };
        role.AssignSystemRoleKey(Role.TenantAdminSystemRoleKey, SeededDevelopmentAdmin.TenantId);
        var existingPermission = new Permission
        {
            TenantId = SeededDevelopmentAdmin.TenantId,
            Name = "Project.View",
            Resource = "Project",
            Action = "View",
            CreatedBy = "test"
        };
        role.Permissions.Add(existingPermission);
        var admin = new User
        {
            Id = SeededDevelopmentAdmin.UserId,
            TenantId = SeededDevelopmentAdmin.TenantId,
            Email = SeededDevelopmentAdmin.Email,
            FirstName = "Alex",
            LastName = "Admin",
            AuthenticationSource = AuthenticationSource.Local,
            PasswordHash = string.Empty,
            MustChangePassword = true,
            TemporaryPasswordExpiresAt = DateTime.UtcNow.AddMinutes(-2),
            PasswordChangedAt = DateTime.UtcNow,
            IsActive = true,
            CreatedBy = "test"
        };
        admin.PasswordHash = _hasher.HashPassword(admin, password);
        admin.UserRoles.Add(new UserRole { TenantId = SeededDevelopmentAdmin.TenantId, Role = role, CreatedBy = "test" });

        var otherPassword = GenerateTestPassword();
        var otherUser = AddLocalUser("other@example.test", otherPassword);
        otherUser.TenantId = SeededDevelopmentAdmin.TenantId;
        otherUser.MustChangePassword = true;
        otherUser.TemporaryPasswordExpiresAt = DateTime.UtcNow.AddMinutes(-2);
        otherUser.PasswordChangedAt = DateTime.UtcNow;

        _context.Roles.Add(role);
        _context.Permissions.Add(existingPermission);
        _context.Users.Add(admin);
        await _context.SaveChangesAsync();
        var persistedExpiry = admin.TemporaryPasswordExpiresAt;
        var persistedHash = admin.PasswordHash;
        var policy = new LocalPasswordPolicyOptions
        {
            TemporaryPasswordExpiryHours = 6,
            MinimumLength = 12,
            MaximumLength = 64,
            DevelopmentAdminTemporaryPasswordBypassEnabled = true
        };
        var optionsMonitor = CreatePasswordPolicyMonitor(() => policy);
        var developmentEnvironment = CreateEnvironment(Environments.Development);
        var auth = CreateAuthService(policy, developmentEnvironment, optionsMonitor);

        var wrongPasswordLogin = await auth.LoginAsync(new LoginCommand(
            SeededDevelopmentAdmin.Email,
            "incorrect-password",
            SeededDevelopmentAdmin.TenantId.ToString()));
        Assert.True(wrongPasswordLogin.IsFailure);

        var adminLogin = await auth.LoginAsync(new LoginCommand(
            SeededDevelopmentAdmin.Email,
            password,
            SeededDevelopmentAdmin.TenantId.ToString()));

        Assert.True(adminLogin.IsSuccess);
        Assert.False(adminLogin.Value!.PasswordChangeRequired);
        Assert.False(adminLogin.Value.User.MustChangePassword);
        Assert.Contains(adminLogin.Value.User.Roles, item => item.Name == "admin");
        Assert.Contains("Project.View", adminLogin.Value.User.Permissions);
        Assert.Single(await _context.UserRoles.ToListAsync());
        Assert.Single(await _context.Permissions.ToListAsync());
        Assert.True(admin.MustChangePassword);
        Assert.Equal(persistedExpiry, admin.TemporaryPasswordExpiresAt);
        Assert.Equal(persistedHash, admin.PasswordHash);

        var refreshedLogin = await auth.RefreshAsync(adminLogin.Value.RefreshToken);
        Assert.True(refreshedLogin.IsSuccess);
        Assert.False(refreshedLogin.Value!.PasswordChangeRequired);
        Assert.False(refreshedLogin.Value.User.MustChangePassword);

        var currentUser = await auth.GetCurrentUserAsync(admin.Id, admin.TenantId);
        Assert.True(currentUser.IsSuccess);
        Assert.False(currentUser.Value!.MustChangePassword);
        Assert.Contains(currentUser.Value.Roles, item => item.Name == "admin");

        var (allowedStatus, _, continued) = await InvokeEnforcementAsync(
            admin,
            "/api/projects",
            "GET",
            policy,
            Environments.Development);
        Assert.Equal(StatusCodes.Status200OK, allowedStatus);
        Assert.True(continued);

        var otherLogin = await auth.LoginAsync(new LoginCommand(
            otherUser.Email,
            otherPassword,
            SeededDevelopmentAdmin.TenantId.ToString()));
        Assert.True(otherLogin.IsFailure);
        Assert.Equal("TEMPORARY_PASSWORD_EXPIRED", otherLogin.Error!.Code);

        policy.DevelopmentAdminTemporaryPasswordBypassEnabled = false;
        var disabledLogin = await auth.LoginAsync(new LoginCommand(
            SeededDevelopmentAdmin.Email,
            password,
            SeededDevelopmentAdmin.TenantId.ToString()));
        Assert.True(disabledLogin.IsFailure);
        Assert.Equal("TEMPORARY_PASSWORD_EXPIRED", disabledLogin.Error!.Code);

        var (blockedStatus, blockedBody, blockedContinued) = await InvokeEnforcementAsync(
            admin,
            "/api/projects",
            "GET",
            policy,
            Environments.Development);
        Assert.Equal(StatusCodes.Status403Forbidden, blockedStatus);
        Assert.False(blockedContinued);
        Assert.Equal("TEMPORARY_PASSWORD_EXPIRED", JsonDocument.Parse(blockedBody).RootElement.GetProperty("code").GetString());
        Assert.True(admin.MustChangePassword);
        Assert.Equal(persistedExpiry, admin.TemporaryPasswordExpiresAt);
        Assert.Equal(persistedHash, admin.PasswordHash);
    }

    [Fact]
    public async Task Development_admin_bypass_setting_is_ignored_in_production()
    {
        var password = GenerateTestPassword();
        var admin = new User
        {
            Id = SeededDevelopmentAdmin.UserId,
            TenantId = SeededDevelopmentAdmin.TenantId,
            Email = SeededDevelopmentAdmin.Email,
            AuthenticationSource = AuthenticationSource.Local,
            IsActive = true,
            MustChangePassword = true,
            TemporaryPasswordExpiresAt = null,
            PasswordChangedAt = DateTime.UtcNow,
            CreatedBy = "test"
        };
        admin.PasswordHash = _hasher.HashPassword(admin, password);
        _context.Users.Add(admin);
        await _context.SaveChangesAsync();

        var policy = new LocalPasswordPolicyOptions { DevelopmentAdminTemporaryPasswordBypassEnabled = true };
        var productionAuth = CreateAuthService(policy, CreateEnvironment(Environments.Production));
        var result = await productionAuth.LoginAsync(new LoginCommand(
            SeededDevelopmentAdmin.Email,
            password,
            SeededDevelopmentAdmin.TenantId.ToString()));

        Assert.True(result.IsFailure);
        Assert.Equal("TEMPORARY_PASSWORD_EXPIRED", result.Error!.Code);
        Assert.Empty(await _context.UserSessions.ToListAsync());

        var (statusCode, body, continued) = await InvokeEnforcementAsync(
            admin,
            "/api/projects",
            "GET",
            policy,
            Environments.Production);
        Assert.Equal(StatusCodes.Status403Forbidden, statusCode);
        Assert.False(continued);
        Assert.Equal("TEMPORARY_PASSWORD_EXPIRED", JsonDocument.Parse(body).RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Development_admin_recovery_sets_hashed_expiring_temporary_password_and_verifies_first_login_flow()
    {
        var temporaryPassword = GenerateTestPassword();
        var permanentPassword = GenerateTestPassword();
        var adminRole = new Role { TenantId = SeedDataProvider.TenantId, Name = "admin", IsSystem = true, CreatedBy = "test" };
        adminRole.AssignSystemRoleKey(Role.TenantAdminSystemRoleKey, SeedDataProvider.TenantId);
        var admin = new User
        {
            Id = SeededDevelopmentAdmin.UserId,
            TenantId = SeedDataProvider.TenantId,
            Email = "admin@nexus.com",
            FirstName = "Alex",
            LastName = "Admin",
            PasswordHash = _hasher.HashPassword(new User(), GenerateTestPassword()),
            MustChangePassword = true,
            TemporaryPasswordExpiresAt = DateTime.UtcNow.AddMinutes(-1),
            IsActive = true,
            CreatedBy = "test"
        };
        admin.UserRoles.Add(new UserRole { TenantId = SeedDataProvider.TenantId, Role = adminRole, CreatedBy = "test" });
        _context.Roles.Add(adminRole);
        _context.Users.Add(admin);
        var existingSession = new UserSession
        {
            TenantId = SeedDataProvider.TenantId,
            UserId = admin.Id,
            Token = "old-session-token",
            RefreshToken = "old-refresh-hash",
            ExpiresAt = DateTime.UtcNow.AddDays(1),
            CreatedBy = "test"
        };
        _context.UserSessions.Add(existingSession);
        await _context.SaveChangesAsync();

        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(host => host.EnvironmentName).Returns(Environments.Development);
        var recovery = new DevelopmentAdminPasswordRecoveryService(
            _context,
            _hasher,
            Options.Create(_policy),
            environment.Object);

        var expiry = await recovery.RecoverTemporaryPasswordAsync(temporaryPassword);

        var savedAdmin = await _context.Users.SingleAsync(user => user.Email == "admin@nexus.com");
        Assert.True(savedAdmin.MustChangePassword);
        Assert.InRange(expiry, DateTime.UtcNow.AddHours(5.9), DateTime.UtcNow.AddHours(6.1));
        Assert.Equal(expiry, savedAdmin.TemporaryPasswordExpiresAt);
        Assert.Equal(PasswordVerificationResult.Success,
            _hasher.VerifyHashedPassword(savedAdmin, savedAdmin.PasswordHash!, temporaryPassword));
        Assert.Equal(PasswordVerificationResult.Failed,
            _hasher.VerifyHashedPassword(savedAdmin, savedAdmin.PasswordHash!, "not-the-recovered-password"));
        Assert.NotNull((await _context.UserSessions.SingleAsync()).RevokedAt);
        Assert.Equal(adminRole.Id, (await _context.UserRoles.SingleAsync()).RoleId);
        var recoveryAudit = await _context.AuditEntries.SingleAsync();
        Assert.Equal("DevelopmentAdminTemporaryPasswordRecovered", recoveryAudit.Action);
        Assert.DoesNotContain(temporaryPassword, recoveryAudit.Changes, StringComparison.Ordinal);
        Assert.DoesNotContain(temporaryPassword, recoveryAudit.NewValues, StringComparison.Ordinal);

        var auth = CreateAuthService();
        var temporaryLogin = await auth.LoginAsync(new LoginCommand(
            "admin@nexus.com", temporaryPassword, SeedDataProvider.TenantId.ToString()));
        Assert.True(temporaryLogin.IsSuccess);
        Assert.True(temporaryLogin.Value!.PasswordChangeRequired);
        Assert.True(temporaryLogin.Value.User.MustChangePassword);
        Assert.Empty(temporaryLogin.Value.User.Permissions);
        Assert.Empty(temporaryLogin.Value.User.Roles);

        var rejectedShortPermanentPassword = await auth.ChangeInitialPasswordAsync(
            admin.Id,
            SeedDataProvider.TenantId,
            new ChangeInitialPasswordCommand(temporaryPassword, "short"));
        Assert.True(rejectedShortPermanentPassword.IsFailure);
        Assert.True((await _context.Users.SingleAsync(user => user.Id == admin.Id)).MustChangePassword);

        var rejectedReusedPassword = await auth.ChangeInitialPasswordAsync(
            admin.Id,
            SeedDataProvider.TenantId,
            new ChangeInitialPasswordCommand(temporaryPassword, temporaryPassword));
        Assert.True(rejectedReusedPassword.IsFailure);
        Assert.True((await _context.Users.SingleAsync(user => user.Id == admin.Id)).MustChangePassword);

        var changed = await auth.ChangeInitialPasswordAsync(
            admin.Id,
            SeedDataProvider.TenantId,
            new ChangeInitialPasswordCommand(temporaryPassword, permanentPassword));
        Assert.True(changed.IsSuccess);

        var permanentLogin = await auth.LoginAsync(new LoginCommand(
            "admin@nexus.com", permanentPassword, SeedDataProvider.TenantId.ToString()));
        Assert.True(permanentLogin.IsSuccess);
        Assert.False(permanentLogin.Value!.PasswordChangeRequired);
        Assert.False(permanentLogin.Value.User.MustChangePassword);
        Assert.Null(permanentLogin.Value.User.TemporaryPasswordExpiresAt);
    }

    [Fact]
    public async Task Development_admin_recovery_rejects_noncanonical_user_id_without_changing_the_account()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            TenantId = SeededDevelopmentAdmin.TenantId,
            Email = SeededDevelopmentAdmin.Email,
            FirstName = "Alex",
            LastName = "Admin",
            AuthenticationSource = AuthenticationSource.Local,
            PasswordHash = _hasher.HashPassword(new User(), GenerateTestPassword()),
            IsActive = true,
            CreatedBy = "test"
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        var originalHash = user.PasswordHash;
        var recovery = CreateAdminRecoveryService(Environments.Development);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => recovery.RecoverTemporaryPasswordAsync(GenerateTestPassword()));

        Assert.Contains("canonical Development seeded Admin", error.Message, StringComparison.Ordinal);
        Assert.Equal(originalHash, user.PasswordHash);
        Assert.Empty(await _context.AuditEntries.ToListAsync());
    }

    [Fact]
    public async Task Development_admin_recovery_rejects_noncanonical_email_without_changing_the_account()
    {
        var user = new User
        {
            Id = SeededDevelopmentAdmin.UserId,
            TenantId = SeededDevelopmentAdmin.TenantId,
            Email = "admin-alias@nexus.com",
            FirstName = "Alex",
            LastName = "Admin",
            AuthenticationSource = AuthenticationSource.Local,
            PasswordHash = _hasher.HashPassword(new User(), GenerateTestPassword()),
            IsActive = true,
            CreatedBy = "test"
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        var originalHash = user.PasswordHash;
        var recovery = CreateAdminRecoveryService(Environments.Development);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => recovery.RecoverTemporaryPasswordAsync(GenerateTestPassword()));

        Assert.Contains("canonical Development seeded Admin email", error.Message, StringComparison.Ordinal);
        Assert.Equal(originalHash, user.PasswordHash);
        Assert.Empty(await _context.AuditEntries.ToListAsync());
    }

    [Fact]
    public async Task Development_admin_recovery_is_rejected_outside_development_before_database_access()
    {
        var recovery = CreateAdminRecoveryService(Environments.Production);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => recovery.RecoverTemporaryPasswordAsync(GenerateTestPassword()));

        Assert.Contains("only in the Development environment", error.Message, StringComparison.Ordinal);
        Assert.Empty(await _context.Users.ToListAsync());
        Assert.Empty(await _context.AuditEntries.ToListAsync());
    }

    [Fact]
    public async Task Development_admin_recovery_enforces_temporary_password_minimum_length()
    {
        var recovery = CreateAdminRecoveryService(Environments.Development);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => recovery.RecoverTemporaryPasswordAsync("short"));

        Assert.Contains("between 12 and 64 characters", error.Message, StringComparison.Ordinal);
        Assert.Empty(await _context.Users.ToListAsync());
        Assert.Empty(await _context.AuditEntries.ToListAsync());
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

        var relogin = await CreateAuthService().LoginAsync(
            new LoginCommand("change@example.test", "NewPermanent!234567", _tenantId.ToString()));
        Assert.True(relogin.IsSuccess);
        Assert.False(relogin.Value!.PasswordChangeRequired);
        Assert.False(relogin.Value.User.MustChangePassword);
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

    private UserAdministrationService CreateUserService(Guid actorId)
    {
        var http = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, actorId.ToString()),
                    new Claim("tenantId", _tenantId.ToString())
                ], "test"))
            }
        };
        return new UserAdministrationService(_context, _hasher, http, new UserEffectivePermissionService(_context), _audit.Object, Options.Create(_policy));
    }

    private DemoAuthService CreateAuthService(
        LocalPasswordPolicyOptions? policy = null,
        IHostEnvironment? environment = null,
        IOptionsMonitor<LocalPasswordPolicyOptions>? optionsMonitor = null)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Secret"] = "Test-Signing-Secret-Key-At-Least-32-Bytes",
            ["Jwt:Issuer"] = "NEXUS",
            ["Jwt:Audience"] = "NEXUS-API"
        }).Build();
        var selectedPolicy = policy ?? _policy;
        return new DemoAuthService(
            _context,
            configuration,
            _hasher,
            new UserEffectivePermissionService(_context),
            _audit.Object,
            optionsMonitor ?? CreatePasswordPolicyMonitor(() => selectedPolicy),
            environment ?? CreateEnvironment(Environments.Development));
    }

    private DevelopmentAdminPasswordRecoveryService CreateAdminRecoveryService(string environmentName)
    {
        return new DevelopmentAdminPasswordRecoveryService(
            _context,
            _hasher,
            Options.Create(_policy),
            CreateEnvironment(environmentName));
    }

    private static IOptionsMonitor<LocalPasswordPolicyOptions> CreatePasswordPolicyMonitor(Func<LocalPasswordPolicyOptions> currentValue)
    {
        var monitor = new Mock<IOptionsMonitor<LocalPasswordPolicyOptions>>();
        monitor.SetupGet(options => options.CurrentValue).Returns(() => currentValue());
        return monitor.Object;
    }

    private static IHostEnvironment CreateEnvironment(string environmentName)
    {
        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(host => host.EnvironmentName).Returns(environmentName);
        return environment.Object;
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

    private static string GenerateTestPassword() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    private async Task<(int StatusCode, string Body, bool Continued)> InvokeEnforcementAsync(
        User user,
        string path,
        string method,
        LocalPasswordPolicyOptions? policy = null,
        string environmentName = "Development")
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
        await middleware.InvokeAsync(
            context,
            _context,
            CreateEnvironment(environmentName),
            CreatePasswordPolicyMonitor(() => policy ?? _policy));
        responseBody.Position = 0;
        using var reader = new StreamReader(responseBody);
        return (context.Response.StatusCode, await reader.ReadToEndAsync(), continued);
    }
}
