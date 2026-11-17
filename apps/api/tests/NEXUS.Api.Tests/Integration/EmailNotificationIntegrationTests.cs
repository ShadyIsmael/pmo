using System.Net;
using System.Net.Sockets;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Identity.Application;
using NEXUS.Modules.Identity.Domain;
using NEXUS.Modules.Notifications.Application;
using NEXUS.Modules.Notifications.Domain;
using NEXUS.Modules.Notifications.Infrastructure;
using Xunit;

namespace NEXUS.Api.Tests.Integration;

public sealed class EmailNotificationIntegrationTests
{
    private static readonly Guid TenantA = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid TenantB = Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly Guid OrganizationA = Guid.Parse("10000000-0000-0000-0000-000000000101");
    private static readonly Guid UserA = Guid.Parse("10000000-0000-0000-0000-000000000010");

    [Fact]
    public void DestinationPolicy_AllowsPublicUsernamePasswordSmtpButRejectsPrivateTargetsByDefault()
    {
        var policy = CreatePolicy();

        Assert.True(policy.IsDestinationAllowed(TenantA, null, "smtp.example.test", IPAddress.Parse("8.8.8.8"), SmtpAuthenticationMode.UsernamePassword));
        Assert.False(policy.IsDestinationAllowed(TenantA, null, "smtp.internal.test", IPAddress.Parse("10.4.5.6"), SmtpAuthenticationMode.UsernamePassword));
        Assert.False(policy.IsDestinationAllowed(TenantA, null, "localhost", IPAddress.Loopback, SmtpAuthenticationMode.UsernamePassword));
    }

    [Fact]
    public void DestinationPolicy_PrivateRelayRequiresExactHostAndMatchingTenantNetwork()
    {
        var policy = CreatePolicy(new SmtpDestinationAllowlistRule
        {
            TenantId = TenantA,
            PrivateRelayHosts = ["mail.internal.example.test"],
            PrivateRelayNetworks = ["10.40.0.0/16"],
        });

        Assert.True(policy.IsDestinationAllowed(TenantA, null, "mail.internal.example.test", IPAddress.Parse("10.40.2.12"), SmtpAuthenticationMode.UsernamePassword));
        Assert.False(policy.IsDestinationAllowed(TenantA, null, "other.internal.example.test", IPAddress.Parse("10.40.2.12"), SmtpAuthenticationMode.UsernamePassword));
        Assert.False(policy.IsDestinationAllowed(TenantA, null, "mail.internal.example.test", IPAddress.Parse("10.41.2.12"), SmtpAuthenticationMode.UsernamePassword));
        Assert.False(policy.IsDestinationAllowed(TenantB, null, "mail.internal.example.test", IPAddress.Parse("10.40.2.12"), SmtpAuthenticationMode.UsernamePassword));
    }

    [Fact]
    public void DestinationPolicy_MostSpecificScopeAndTrustedRelayAuthorizationAreIsolated()
    {
        var policy = CreatePolicy(
            new SmtpDestinationAllowlistRule
            {
                PrivateRelayHosts = ["mail.internal.example.test"],
                PrivateRelayNetworks = ["10.0.0.0/8"],
                TrustedRelayHosts = ["mail.internal.example.test"],
                TrustedRelayNetworks = ["10.0.0.0/8"],
            },
            new SmtpDestinationAllowlistRule
            {
                TenantId = TenantA,
                OrganizationId = OrganizationA,
                PrivateRelayHosts = ["other.internal.example.test"],
                PrivateRelayNetworks = ["10.40.0.0/16"],
            });

        // An Organization rule exists, so a broader global rule cannot authorize a different host.
        Assert.False(policy.IsDestinationAllowed(TenantA, OrganizationA, "mail.internal.example.test", IPAddress.Parse("10.4.5.6"), SmtpAuthenticationMode.UsernamePassword));
        Assert.True(policy.IsDestinationAllowed(TenantA, null, "mail.internal.example.test", IPAddress.Parse("10.4.5.6"), SmtpAuthenticationMode.TrustedRelay));
        Assert.True(policy.IsDestinationAllowed(TenantB, null, "mail.internal.example.test", IPAddress.Parse("10.4.5.6"), SmtpAuthenticationMode.TrustedRelay));
        Assert.False(policy.IsDestinationAllowed(TenantA, null, "mail.internal.example.test", IPAddress.Parse("8.8.8.8"), SmtpAuthenticationMode.TrustedRelay));
        Assert.True(policy.IsTrustedRelayHostAuthorized(TenantA, null, "MAIL.INTERNAL.EXAMPLE.TEST."));
        Assert.False(policy.IsTrustedRelayHostAuthorized(TenantA, OrganizationA, "mail.internal.example.test"));

        var tenantOnly = CreatePolicy(new SmtpDestinationAllowlistRule
        {
            TenantId = TenantA,
            TrustedRelayHosts = ["mail.internal.example.test"],
            TrustedRelayNetworks = ["10.4.0.0/16"],
        });
        Assert.True(tenantOnly.IsDestinationAllowed(TenantA, null, "mail.internal.example.test", IPAddress.Parse("10.4.5.6"), SmtpAuthenticationMode.TrustedRelay));
        Assert.False(tenantOnly.IsDestinationAllowed(TenantB, null, "mail.internal.example.test", IPAddress.Parse("10.4.5.6"), SmtpAuthenticationMode.TrustedRelay));
    }

    [Fact]
    public void DestinationPolicy_RejectsOAuthMalformedHostAndInvalidScopePairs()
    {
        var policy = CreatePolicy();

        Assert.False(policy.IsDestinationAllowed(TenantA, null, "smtp.example.test", IPAddress.Parse("8.8.8.8"), SmtpAuthenticationMode.OAuth2));
        Assert.False(policy.IsDestinationAllowed(TenantA, null, "http://169.254.169.254/latest/meta-data", IPAddress.Parse("8.8.8.8"), SmtpAuthenticationMode.UsernamePassword));
        Assert.False(policy.IsDestinationAllowed(null, OrganizationA, "smtp.example.test", IPAddress.Parse("8.8.8.8"), SmtpAuthenticationMode.UsernamePassword));
    }

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("169.254.169.254")]
    [InlineData("192.0.2.1")]
    [InlineData("240.0.0.1")]
    [InlineData("::1")]
    [InlineData("fe80::1")]
    [InlineData("2001:db8::1")]
    [InlineData("ff02::1")]
    public void DestinationPolicy_BroadDeploymentCidrsCannotAllowLoopbackLinkLocalOrReservedTargets(string addressText)
    {
        var policy = CreatePolicy(new SmtpDestinationAllowlistRule
        {
            PrivateRelayHosts = ["broad.mail.example.test"],
            PrivateRelayNetworks = ["0.0.0.0/0", "::/0"],
            TrustedRelayHosts = ["broad.mail.example.test"],
            TrustedRelayNetworks = ["0.0.0.0/0", "::/0"],
        });
        var address = IPAddress.Parse(addressText);

        Assert.False(policy.IsDestinationAllowed(TenantA, null, "broad.mail.example.test", address, SmtpAuthenticationMode.UsernamePassword));
        Assert.False(policy.IsDestinationAllowed(TenantA, null, "broad.mail.example.test", address, SmtpAuthenticationMode.TrustedRelay));
    }

    [Fact]
    public async Task SmtpTransport_RequiresStartTlsAndSendsNoCredentialsOrMailToPlaintextServer()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var serverTask = ServeSmtpWithoutStartTlsAsync(listener);
        var profile = new SmtpTransportProfile(
            Enabled: true,
            Host: "127.0.0.1",
            Port: port,
            SecurityMode: SmtpSecurityMode.StartTlsRequired,
            AuthenticationMode: SmtpAuthenticationMode.UsernamePassword,
            UserName: "test-user",
            Password: "test-password-must-not-be-sent",
            FromAddress: "noreply@example.test",
            FromName: "FOPX PM",
            ReplyToAddress: null);
        var transport = new SmtpEmailNotificationTransport(new LoopbackTestDestinationPolicy());

        var result = await transport.TestConnectionAsync(profile, TenantA, null);
        var commands = await serverTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.False(result.Succeeded);
        Assert.True(
            result.FailureCode == "TlsRequired",
            $"The local SMTP server observed EHLO: {commands.Any(command => command.StartsWith("EHLO ", StringComparison.OrdinalIgnoreCase))}; commands: {commands.Count}.");
        Assert.Contains(commands, command => command.StartsWith("EHLO ", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(commands, command => command.StartsWith("AUTH ", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(commands, command => command.StartsWith("MAIL FROM:", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(commands, command => command.Contains("test-user", StringComparison.Ordinal));
        Assert.DoesNotContain(commands, command => command.Contains("test-password-must-not-be-sent", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SettingsView_UsesEffectiveAuthorizationAndDoesNotGrantAccessFromAdminRole()
    {
        var principal = Principal(
            new Claim(ClaimTypes.Role, "Admin"));
        var store = new Mock<IEmailNotificationProfileStore>(MockBehavior.Strict);
        var service = CreateSettingsService(principal, store.Object, new TestOrganizationAccess(true));

        var exception = await Assert.ThrowsAsync<EmailNotificationSettingsException>(
            () => service.GetSettingsAsync(EmailNotificationProfileScope.Tenant, null));

        Assert.Equal(403, exception.StatusCode);
        store.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task SettingsView_ActiveDenyWinsAndOrganizationSettingsGrantIsRequired()
    {
        var deniedPrincipal = Principal(
            new Claim("permission", "EmailNotifications.View"),
            new Claim("deny", "EmailNotifications.View"));
        var deniedStore = new Mock<IEmailNotificationProfileStore>(MockBehavior.Strict);
        var deniedService = CreateSettingsService(deniedPrincipal, deniedStore.Object, new TestOrganizationAccess(true));

        var denied = await Assert.ThrowsAsync<EmailNotificationSettingsException>(() =>
            deniedService.GetSettingsAsync(EmailNotificationProfileScope.Tenant, null));

        Assert.Equal(403, denied.StatusCode);
        deniedStore.VerifyNoOtherCalls();

        var allowedPrincipal = Principal(new Claim("permission", "EmailNotifications.View"));
        var organizationStore = new Mock<IEmailNotificationProfileStore>(MockBehavior.Strict);
        var organizationService = CreateSettingsService(allowedPrincipal, organizationStore.Object, new TestOrganizationAccess(false));
        var organizationDenied = await Assert.ThrowsAsync<EmailNotificationSettingsException>(() =>
            organizationService.GetSettingsAsync(EmailNotificationProfileScope.Organization, OrganizationA));

        Assert.Equal(403, organizationDenied.StatusCode);
        organizationStore.VerifyNoOtherCalls();
    }

    [Fact]
    public void ProfileSettingsResponse_DoesNotSerializePlaintextPassword()
    {
        const string secret = "smtp-secret-never-return-this";
        var response = new EmailNotificationSettingsResponse(
            EmailNotificationProfileScope.Tenant,
            TenantA,
            PersistenceAvailable: true,
            HasOverride: true,
            OverrideProfile: new EmailNotificationProfileView(
                Enabled: true,
                Host: "smtp.example.test",
                Port: 587,
                SecurityMode: SmtpSecurityMode.StartTlsRequired,
                AuthenticationMode: SmtpAuthenticationMode.UsernamePassword,
                UserName: "mailer",
                HasPassword: true,
                FromAddress: "noreply@example.test",
                FromName: "FOPX PM",
                ReplyToAddress: null,
                Version: "AQID"),
            EffectiveProfile: null,
            EffectiveSourceScope: EmailNotificationProfileScope.Tenant,
            CanManage: true,
            CanTest: true,
            CanViewLogs: true,
            TrustedRelayAvailableForEffectiveHost: false);

        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.DoesNotContain(secret, json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"password\"", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"hasPassword\":true", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendIdempotentAsync_QueuesOneOrganizationScopedEmailIntentAndPreservesInAppNotification()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var sender = new TestEmailSender { IsAvailable = true };
        var push = new TestPushSender();
        var service = new NotificationService(context, push, Options.Create(CreateOptions()), sender);
        context.NotificationPreferences.Add(new NotificationPreference
        {
            TenantId = TenantA,
            UserId = UserA,
            Type = NotificationTypes.ProjectHandover,
            EmailEnabled = true,
            CreatedBy = "test",
        });
        await context.SaveChangesAsync();
        var notification = CreateNotification(TenantA, UserA, NotificationTypes.ProjectHandover, OrganizationA);

        var first = await service.SendIdempotentAsync(notification, allowPush: false, allowEmail: true);
        var retry = await service.SendIdempotentAsync(notification, allowPush: false, allowEmail: true);

        Assert.True(first.IsSuccess);
        Assert.True(first.Value);
        Assert.True(retry.IsSuccess);
        Assert.False(retry.Value);
        Assert.True(notification.IsInAppVisible);
        var resolvedScope = Assert.Single(sender.ConfigurationScopes);
        Assert.Equal(TenantA, resolvedScope.TenantId);
        Assert.Equal((Guid?)OrganizationA, resolvedScope.OrganizationId);
        Assert.Single(await context.Notifications.ToListAsync());
        var delivery = await context.NotificationDeliveries.SingleAsync();
        Assert.Equal(NotificationDeliveryChannel.Email, delivery.Channel);
        Assert.Equal(NotificationDeliveryStatus.Pending, delivery.Status);
        Assert.Equal((Guid?)OrganizationA, (await context.Notifications.SingleAsync()).OrganizationId);
    }

    [Fact]
    public async Task SendAsync_ProfileUnavailableDoesNotBlockInAppNotificationPersistence()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var context = database.CreateContext();
        context.NotificationPreferences.Add(new NotificationPreference
        {
            TenantId = TenantA,
            UserId = UserA,
            Type = NotificationTypes.ProjectHandover,
            InAppEnabled = true,
            EmailEnabled = true,
            CreatedBy = "test",
        });
        await context.SaveChangesAsync();
        var sender = new TestEmailSender { IsAvailable = false };
        var notification = CreateNotification(TenantA, UserA, NotificationTypes.ProjectHandover, OrganizationA);

        await new NotificationService(context, new TestPushSender(), Options.Create(CreateOptions()), sender).SendAsync(notification);

        Assert.True(notification.IsInAppVisible);
        Assert.True(await context.Notifications.AnyAsync(item => item.Id == notification.Id));
        Assert.Empty(await context.NotificationDeliveries.ToListAsync());
        Assert.Equal((TenantA, (Guid?)OrganizationA), Assert.Single(sender.ConfigurationScopes));
    }

    [Fact]
    public async Task DeliveryProcessor_MarksProviderAcceptanceAsAcceptedWithoutClaimingDeliveryReceipt()
    {
        await using var database = await TestDatabase.CreateAsync();
        var sender = new TestEmailSender { IsAvailable = true, Handler = (_, _, _) => Task.FromResult(new EmailSendResult(true)) };
        Guid deliveryId;
        await using (var seed = database.CreateContext())
        {
            deliveryId = await SeedEmailDeliveryAsync(seed);
        }

        await using var context = database.CreateContext();
        Assert.Equal(1, await CreateProcessor(context, sender).DispatchBatchAsync());

        var delivery = await context.NotificationDeliveries.SingleAsync(item => item.Id == deliveryId);
        Assert.Equal(NotificationDeliveryStatus.Accepted, delivery.Status);
        Assert.NotNull(delivery.AcceptedAt);
        Assert.Null(delivery.DeliveredAt);
        Assert.Single(sender.Sent);
    }

    [Fact]
    public async Task DeliveryProcessor_RetriesTransientSmtpFailureThenRecordsAcceptance()
    {
        await using var database = await TestDatabase.CreateAsync();
        var sendCount = 0;
        var sender = new TestEmailSender
        {
            IsAvailable = true,
            Handler = (_, _, _) => Task.FromResult(++sendCount == 1
                ? new EmailSendResult(false, EmailFailureKind.Transient, "Temporary", "Try again.")
                : new EmailSendResult(true)),
        };
        Guid deliveryId;
        await using (var seed = database.CreateContext())
            deliveryId = await SeedEmailDeliveryAsync(seed);

        await using (var firstRun = database.CreateContext())
        {
            Assert.Equal(1, await CreateProcessor(firstRun, sender).DispatchBatchAsync());
            var delivery = await firstRun.NotificationDeliveries.SingleAsync(item => item.Id == deliveryId);
            Assert.Equal(NotificationDeliveryStatus.RetryScheduled, delivery.Status);
            Assert.Equal(1, delivery.AttemptCount);
            delivery.NextAttemptAt = DateTime.UtcNow.AddSeconds(-1);
            await firstRun.SaveChangesAsync();
        }

        await using var secondRun = database.CreateContext();
        Assert.Equal(1, await CreateProcessor(secondRun, sender).DispatchBatchAsync());
        var accepted = await secondRun.NotificationDeliveries.SingleAsync(item => item.Id == deliveryId);
        Assert.Equal(NotificationDeliveryStatus.Accepted, accepted.Status);
        Assert.Equal(2, accepted.AttemptCount);
        Assert.NotNull(accepted.AcceptedAt);
        Assert.Null(accepted.DeliveredAt);
        Assert.Equal(2, sender.Sent.Count);
    }

    private static SmtpDestinationPolicy CreatePolicy(params SmtpDestinationAllowlistRule[] rules) =>
        new(Options.Create(new SmtpDestinationPolicyOptions { Rules = [.. rules] }));

    private static async Task<IReadOnlyList<string>> ServeSmtpWithoutStartTlsAsync(TcpListener listener)
    {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(4));
        using var client = await listener.AcceptTcpClientAsync(cancellation.Token);
        await using var stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.ASCII, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);
        await using var writer = new StreamWriter(stream, Encoding.ASCII, bufferSize: 1024, leaveOpen: true)
        {
            NewLine = "\r\n",
            AutoFlush = true,
        };
        var commands = new List<string>();
        await writer.WriteLineAsync("220 localhost ESMTP ready");

        try
        {
            while (await reader.ReadLineAsync(cancellation.Token) is { } command)
            {
                commands.Add(command);
                if (command.StartsWith("EHLO ", StringComparison.OrdinalIgnoreCase))
                {
                    // Deliberately omit STARTTLS from the advertised extension list.
                    await writer.WriteAsync("250-localhost\r\n250 SIZE 1000000\r\n");
                }
                else if (command.Equals("QUIT", StringComparison.OrdinalIgnoreCase))
                {
                    await writer.WriteLineAsync("221 closing connection");
                    break;
                }
                else
                {
                    await writer.WriteLineAsync("500 command not expected");
                }
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // A client that fails closed may close the socket without sending QUIT.
        }

        return commands;
    }

    private static ClaimsPrincipal Principal(params Claim[] extraClaims)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, UserA.ToString()),
            new("tenantId", TenantA.ToString()),
        };
        claims.AddRange(extraClaims);
        return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "Test"));
    }

    private static EmailNotificationSettingsService CreateSettingsService(
        ClaimsPrincipal principal,
        IEmailNotificationProfileStore store,
        IEmailNotificationOrganizationScopeAccess organizationAccess)
    {
        var httpContext = new DefaultHttpContext { User = principal };
        var httpContextAccessor = new HttpContextAccessor { HttpContext = httpContext };
        return new EmailNotificationSettingsService(
            store,
            organizationAccess,
            CreatePolicy(),
            Mock.Of<IEmailNotificationTestOperations>(),
            Mock.Of<IUserRepository>(),
            new ClaimAuthorizationService(),
            httpContextAccessor,
            Mock.Of<IAuditService>());
    }

    private static NotificationDeliveryProcessor CreateProcessor(DbContext context, TestEmailSender sender) =>
        new(context, new TestPushSender(), Options.Create(CreateOptions()), sender);

    private static NotificationDeliveryOptions CreateOptions() => new()
    {
        BatchSize = 10,
        MaxAttempts = 3,
        InitialRetryDelay = TimeSpan.FromSeconds(1),
        MaxRetryDelay = TimeSpan.FromSeconds(5),
        PollInterval = TimeSpan.FromSeconds(5),
        ProcessingLease = TimeSpan.FromSeconds(20),
        SendTimeout = TimeSpan.FromSeconds(10),
    };

    private static Notification CreateNotification(Guid tenantId, Guid userId, string type, Guid? organizationId = null) => new()
    {
        TenantId = tenantId,
        OrganizationId = organizationId,
        UserId = userId,
        Type = type,
        Title = "A handover update",
        Body = "Review the handover in FOPX PM.",
        CreatedBy = "test",
    };

    private static async Task<Guid> SeedEmailDeliveryAsync(NotificationTestDbContext context)
    {
        context.Users.Add(new User
        {
            TenantId = TenantA,
            Id = UserA,
            Email = "recipient@example.test",
            IsActive = true,
            CreatedBy = "test",
        });
        var notification = CreateNotification(TenantA, UserA, NotificationTypes.ProjectHandover, OrganizationA);
        var delivery = new NotificationDelivery
        {
            TenantId = TenantA,
            NotificationId = notification.Id,
            Channel = NotificationDeliveryChannel.Email,
            Status = NotificationDeliveryStatus.Pending,
            MaxAttempts = 3,
            NextAttemptAt = DateTime.UtcNow,
            CreatedBy = "test",
        };
        context.Notifications.Add(notification);
        context.NotificationDeliveries.Add(delivery);
        context.NotificationPreferences.Add(new NotificationPreference
        {
            TenantId = TenantA,
            UserId = UserA,
            Type = NotificationTypes.ProjectHandover,
            EmailEnabled = true,
            CreatedBy = "test",
        });
        await context.SaveChangesAsync();
        return delivery.Id;
    }

    private sealed class ClaimAuthorizationService : Microsoft.AspNetCore.Authorization.IAuthorizationService
    {
        public Task<AuthorizationResult> AuthorizeAsync(
            ClaimsPrincipal user,
            object? resource,
            IEnumerable<IAuthorizationRequirement> requirements) =>
            Task.FromResult(AuthorizationResult.Failed());

        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object? resource, string policyName)
        {
            var allowed = user.HasClaim("permission", policyName) && !user.HasClaim("deny", policyName);
            return Task.FromResult(allowed ? AuthorizationResult.Success() : AuthorizationResult.Failed());
        }
    }

    private sealed class TestOrganizationAccess(bool allowed) : IEmailNotificationOrganizationScopeAccess
    {
        public Task<bool> HasAccessAsync(Guid organizationId, bool manage, CancellationToken cancellationToken = default) =>
            Task.FromResult(allowed);

        public Task<bool> HasScopedOrganizationAdminAuthorityAsync(
            Guid organizationId, bool manage, CancellationToken cancellationToken = default) =>
            HasAccessAsync(organizationId, manage, cancellationToken);
    }

    private sealed class LoopbackTestDestinationPolicy : ISmtpDestinationPolicy
    {
        public bool IsDestinationAllowed(
            Guid? tenantId,
            Guid? organizationId,
            string normalizedHost,
            IPAddress resolvedAddress,
            SmtpAuthenticationMode authenticationMode) =>
            tenantId == TenantA
            && organizationId is null
            && normalizedHost == "127.0.0.1"
            && IPAddress.IsLoopback(resolvedAddress)
            && authenticationMode == SmtpAuthenticationMode.UsernamePassword;

        public bool IsTrustedRelayHostAuthorized(Guid? tenantId, Guid? organizationId, string host) => false;
    }

    private sealed class TestPushSender : IPushNotificationSender
    {
        public bool IsConfigured => false;
        public Task<PushSendResult> SendAsync(Notification notification, UserDeviceToken device, CancellationToken cancellationToken = default) =>
            Task.FromResult(new PushSendResult(false, PushFailureKind.Permanent, "NotConfigured", "Push is not used in this test."));
    }

    private sealed class TestEmailSender : IEmailNotificationSender
    {
        public bool IsAvailable { get; init; }
        public Func<Notification, string, CancellationToken, Task<EmailSendResult>> Handler { get; init; } =
            (_, _, _) => Task.FromResult(new EmailSendResult(true));
        public List<(Guid TenantId, Guid? OrganizationId)> ConfigurationScopes { get; } = [];
        public List<Guid> Sent { get; } = [];

        public Task<bool> IsConfiguredAsync(Guid tenantId, Guid? organizationId, CancellationToken cancellationToken = default)
        {
            ConfigurationScopes.Add((tenantId, organizationId));
            return Task.FromResult(IsAvailable);
        }

        public async Task<EmailSendResult> SendAsync(Notification notification, string recipientEmail, CancellationToken cancellationToken = default)
        {
            Sent.Add(notification.Id);
            return await Handler(notification, recipientEmail, cancellationToken);
        }
    }

    private sealed class TestDatabase : IAsyncDisposable
    {
        private readonly SqliteConnection _keepAlive;
        private readonly DbContextOptions<NotificationTestDbContext> _options;

        private TestDatabase(SqliteConnection keepAlive, DbContextOptions<NotificationTestDbContext> options)
        {
            _keepAlive = keepAlive;
            _options = options;
        }

        public NotificationTestDbContext CreateContext() => new(_options);

        public static async Task<TestDatabase> CreateAsync()
        {
            var connectionString = $"Data Source=email_notifications_{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
            var keepAlive = new SqliteConnection(connectionString);
            await keepAlive.OpenAsync();
            var options = new DbContextOptionsBuilder<NotificationTestDbContext>().UseSqlite(connectionString).Options;
            await using (var context = new NotificationTestDbContext(options))
                await context.Database.EnsureCreatedAsync();
            return new TestDatabase(keepAlive, options);
        }

        public ValueTask DisposeAsync() => _keepAlive.DisposeAsync();
    }

    private sealed class NotificationTestDbContext(DbContextOptions<NotificationTestDbContext> options) : DbContext(options)
    {
        public DbSet<Notification> Notifications => Set<Notification>();
        public DbSet<NotificationDelivery> NotificationDeliveries => Set<NotificationDelivery>();
        public DbSet<NotificationDeliveryAttempt> NotificationDeliveryAttempts => Set<NotificationDeliveryAttempt>();
        public DbSet<UserDeviceToken> UserDeviceTokens => Set<UserDeviceToken>();
        public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();
        public DbSet<User> Users => Set<User>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Notification>().HasKey(item => item.Id);
            modelBuilder.Entity<NotificationDelivery>().HasKey(item => item.Id);
            modelBuilder.Entity<NotificationDelivery>().HasIndex(item => new { item.TenantId, item.NotificationId, item.Channel }).IsUnique();
            modelBuilder.Entity<NotificationDeliveryAttempt>().HasKey(item => item.Id);
            modelBuilder.Entity<NotificationDeliveryAttempt>().HasIndex(item => new { item.TenantId, item.NotificationDeliveryId, item.DeviceTokenId }).IsUnique();
            modelBuilder.Entity<UserDeviceToken>().HasKey(item => item.Id);
            modelBuilder.Entity<UserDeviceToken>().HasIndex(item => item.TokenHash).IsUnique().HasFilter("[IsActive] = 1");
            modelBuilder.Entity<NotificationPreference>().HasKey(item => item.Id);
            modelBuilder.Entity<NotificationPreference>().HasIndex(item => new { item.TenantId, item.UserId, item.Type }).IsUnique().HasFilter("[IsDeleted] = 0");
            modelBuilder.Entity<User>().HasKey(item => item.Id);
            modelBuilder.Entity<User>().Ignore(item => item.UserRoles);
            modelBuilder.Entity<User>().Ignore(item => item.Sessions);
            modelBuilder.Entity<User>().Ignore(item => item.PermissionOverrides);
        }
    }
}
