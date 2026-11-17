using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NEXUS.Modules.Notifications.Api;
using NEXUS.Modules.Notifications.Application;
using NEXUS.Modules.Notifications.Domain;
using NEXUS.Modules.Notifications.Infrastructure;
using Xunit;

namespace NEXUS.Api.Tests.Integration;

public sealed class NotificationDeliveryFoundationTests
{
    private static readonly Guid TenantA = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid TenantB = Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly Guid UserA = Guid.Parse("10000000-0000-0000-0000-000000000010");
    private static readonly Guid UserB = Guid.Parse("20000000-0000-0000-0000-000000000020");

    [Fact]
    public async Task SendAsync_PersistsCanonicalNotificationAndPendingPushWithoutCallingProviderInline()
    {
        await using var database = await TestDatabase.CreateAsync();
        var sender = new TestPushSender { IsConfigured = true };
        await using var context = database.CreateContext();
        var service = CreateService(context, sender);
        var preferences = NotificationTypes.Supported.Select(type => new NotificationPreferenceDto(
            type,
            InAppEnabled: type == NotificationTypes.ProjectHandover,
            PushEnabled: type == NotificationTypes.ProjectHandover,
            EmailEnabled: false)).ToArray();
        Assert.True((await service.UpdatePreferencesAsync(TenantA, UserA, new UpdateNotificationPreferencesRequest(preferences))).IsSuccess);
        Assert.True((await service.UpdatePreferencesAsync(TenantA, UserB, new UpdateNotificationPreferencesRequest(
            [new NotificationPreferenceDto("UnapprovedEvent", true, true, false)]))).IsFailure);
        var notification = CreateNotification(TenantA, UserA, NotificationTypes.ProjectHandover);
        await service.SendAsync(notification);

        Assert.Empty(sender.Sent);
        Assert.True(notification.IsInAppVisible);
        var delivery = await context.NotificationDeliveries.SingleAsync();
        Assert.Equal(NotificationDeliveryChannel.Push, delivery.Channel);
        Assert.Equal(NotificationDeliveryStatus.Pending, delivery.Status);
        Assert.Equal(notification.Id, delivery.NotificationId);
        Assert.Equal(1, await service.GetUnreadCountAsync(TenantA, UserA));
    }

    [Fact]
    public async Task SendAsync_EnforcesPushAndInAppPreferencesAndKeepsEmailDeferred()
    {
        await using var database = await TestDatabase.CreateAsync();
        var sender = new TestPushSender { IsConfigured = true };
        await using var context = database.CreateContext();
        context.NotificationPreferences.Add(new NotificationPreference
        {
            TenantId = TenantA,
            UserId = UserA,
            Type = NotificationTypes.ProjectHandover,
            InAppEnabled = false,
            PushEnabled = false,
            EmailEnabled = true,
        });
        await context.SaveChangesAsync();

        var notification = CreateNotification(TenantA, UserA, NotificationTypes.ProjectHandover);
        await CreateService(context, sender).SendAsync(notification);

        Assert.False(notification.IsInAppVisible);
        Assert.Empty(await context.NotificationDeliveries.ToListAsync());
        Assert.Empty(sender.Sent);
    }

    [Fact]
    public async Task SendAsync_StoresNotificationButDoesNotQueueWhenFirebaseIsUnconfigured()
    {
        await using var database = await TestDatabase.CreateAsync();
        var sender = new TestPushSender { IsConfigured = false };
        await using var context = database.CreateContext();
        context.NotificationPreferences.Add(new NotificationPreference
        {
            TenantId = TenantA,
            UserId = UserA,
            Type = NotificationTypes.ProjectHandover,
            PushEnabled = true,
        });
        await context.SaveChangesAsync();

        var notification = CreateNotification(TenantA, UserA, NotificationTypes.ProjectHandover);
        await CreateService(context, sender).SendAsync(notification);

        Assert.True(await context.Notifications.AnyAsync(item => item.Id == notification.Id));
        Assert.Empty(await context.NotificationDeliveries.ToListAsync());
        Assert.Empty(sender.Sent);
    }

    [Fact]
    public async Task GetUnreadCountAsync_IsScopedToTheRequestedTenantAndVisibleNotifications()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var context = database.CreateContext();
        context.Notifications.AddRange(
            CreateNotification(TenantA, UserA, NotificationTypes.ProjectHandover),
            CreateNotification(TenantB, UserA, NotificationTypes.ProjectHandover),
            new Notification { TenantId = TenantA, UserId = UserA, Type = NotificationTypes.ProjectHandover, IsInAppVisible = false });
        await context.SaveChangesAsync();

        var service = CreateService(context, new TestPushSender());
        Assert.Equal(1, await service.GetUnreadCountAsync(TenantA, UserA));
        Assert.Equal(1, await service.GetUnreadCountAsync(TenantB, UserA));
    }

    [Fact]
    public async Task DispatchBatchAsync_RetriesTransientDeviceFailureThenMarksDeliveryDelivered()
    {
        await using var database = await TestDatabase.CreateAsync();
        var sender = new TestPushSender();
        sender.Handler = (_, _, _) => Task.FromResult(sender.Sent.Count == 1
            ? new PushSendResult(false, PushFailureKind.Transient, "Temporary", "Try again.")
            : new PushSendResult(true, ProviderMessageId: "provider-message-2"));
        Guid deliveryId;
        await using (var seed = database.CreateContext())
        {
            var notification = CreateNotification(TenantA, UserA, NotificationTypes.ProjectHandover);
            deliveryId = AddPushDelivery(seed, notification);
            seed.UserDeviceTokens.Add(CreateDevice(TenantA, UserA, "registration-token-one"));
            await seed.SaveChangesAsync();
        }

        await using (var firstRun = database.CreateContext())
        {
            var processor = CreateProcessor(firstRun, sender);
            Assert.Equal(1, await processor.DispatchBatchAsync());
            var delivery = await firstRun.NotificationDeliveries.SingleAsync(item => item.Id == deliveryId);
            Assert.Equal(NotificationDeliveryStatus.RetryScheduled, delivery.Status);
            var attempt = await firstRun.NotificationDeliveryAttempts.SingleAsync();
            Assert.Equal(1, attempt.AttemptCount);
            delivery.NextAttemptAt = DateTime.UtcNow.AddSeconds(-1);
            attempt.NextAttemptAt = DateTime.UtcNow.AddSeconds(-1);
            await firstRun.SaveChangesAsync();
        }

        await using (var secondRun = database.CreateContext())
        {
            Assert.Equal(1, await CreateProcessor(secondRun, sender).DispatchBatchAsync());
            var delivery = await secondRun.NotificationDeliveries.SingleAsync(item => item.Id == deliveryId);
            var attempt = await secondRun.NotificationDeliveryAttempts.SingleAsync();
            Assert.Equal(NotificationDeliveryStatus.Delivered, delivery.Status);
            Assert.Equal(NotificationAttemptStatus.Delivered, attempt.Status);
            Assert.Equal(2, attempt.AttemptCount);
            Assert.Equal("provider-message-2", attempt.ProviderMessageId);
        }
        Assert.Equal(2, sender.Sent.Count);
    }

    [Fact]
    public async Task DispatchBatchAsync_PermanentAndUnregisteredFailuresAreNotRetriedAndUnregisteredDeviceIsDeactivated()
    {
        await using var database = await TestDatabase.CreateAsync();
        var sender = new TestPushSender
        {
            Handler = (_, _, _) => Task.FromResult(new PushSendResult(false, PushFailureKind.Unregistered, "UnregisteredToken", "Device expired.")),
        };
        Guid deliveryId;
        Guid deviceId;
        await using (var seed = database.CreateContext())
        {
            var notification = CreateNotification(TenantA, UserA, NotificationTypes.ProjectHandover);
            deliveryId = AddPushDelivery(seed, notification);
            var device = CreateDevice(TenantA, UserA, "old-registration-token");
            deviceId = device.Id;
            seed.UserDeviceTokens.Add(device);
            await seed.SaveChangesAsync();
        }

        await using var context = database.CreateContext();
        await CreateProcessor(context, sender).DispatchBatchAsync();
        var delivery = await context.NotificationDeliveries.SingleAsync(item => item.Id == deliveryId);
        var deviceRow = await context.UserDeviceTokens.SingleAsync(item => item.Id == deviceId);
        var attempt = await context.NotificationDeliveryAttempts.SingleAsync();
        Assert.Equal(NotificationDeliveryStatus.FailedPermanent, delivery.Status);
        Assert.Equal(NotificationAttemptStatus.FailedPermanent, attempt.Status);
        Assert.False(deviceRow.IsActive);
        Assert.Equal(1, attempt.AttemptCount);
        Assert.Single(sender.Sent);
    }

    [Fact]
    public async Task DispatchBatchAsync_PermanentFailureDoesNotRetryOrDeactivateAValidDevice()
    {
        await using var database = await TestDatabase.CreateAsync();
        var sender = new TestPushSender
        {
            Handler = (_, _, _) => Task.FromResult(new PushSendResult(false, PushFailureKind.Permanent, "ProviderConfiguration", "The provider rejected the request.")),
        };
        Guid deliveryId;
        Guid deviceId;
        await using (var seed = database.CreateContext())
        {
            var notification = CreateNotification(TenantA, UserA, NotificationTypes.ProjectHandover);
            deliveryId = AddPushDelivery(seed, notification);
            var device = CreateDevice(TenantA, UserA, "valid-device-token");
            deviceId = device.Id;
            seed.UserDeviceTokens.Add(device);
            await seed.SaveChangesAsync();
        }

        await using var context = database.CreateContext();
        await CreateProcessor(context, sender).DispatchBatchAsync();
        var attempt = await context.NotificationDeliveryAttempts.SingleAsync();
        Assert.Equal(NotificationDeliveryStatus.FailedPermanent, (await context.NotificationDeliveries.SingleAsync(item => item.Id == deliveryId)).Status);
        Assert.Equal(NotificationAttemptStatus.FailedPermanent, attempt.Status);
        Assert.Equal(1, attempt.AttemptCount);
        Assert.True((await context.UserDeviceTokens.SingleAsync(item => item.Id == deviceId)).IsActive);
        Assert.Single(sender.Sent);
    }

    [Fact]
    public async Task DispatchBatchAsync_StopsRetryingAtConfiguredMaximum()
    {
        await using var database = await TestDatabase.CreateAsync();
        var options = CreateOptions();
        options.MaxAttempts = 1;
        var sender = new TestPushSender
        {
            Handler = (_, _, _) => Task.FromResult(new PushSendResult(false, PushFailureKind.Transient, "Unavailable", "Try later.")),
        };
        Guid deliveryId;
        await using (var seed = database.CreateContext())
        {
            var notification = CreateNotification(TenantA, UserA, NotificationTypes.ProjectHandover);
            var delivery = CreateDelivery(notification);
            delivery.MaxAttempts = options.MaxAttempts;
            deliveryId = delivery.Id;
            seed.Notifications.Add(notification);
            seed.NotificationDeliveries.Add(delivery);
            seed.UserDeviceTokens.Add(CreateDevice(TenantA, UserA, "max-attempt-token"));
            await seed.SaveChangesAsync();
        }

        await using var context = database.CreateContext();
        await CreateProcessor(context, sender, options).DispatchBatchAsync();
        var deliveryResult = await context.NotificationDeliveries.SingleAsync(item => item.Id == deliveryId);
        var attempt = await context.NotificationDeliveryAttempts.SingleAsync();
        Assert.Equal(NotificationDeliveryStatus.FailedPermanent, deliveryResult.Status);
        Assert.Equal("MaxAttempts", deliveryResult.FailureCategory);
        Assert.Equal(NotificationAttemptStatus.FailedPermanent, attempt.Status);
        Assert.Equal(1, attempt.AttemptCount);
        Assert.Single(sender.Sent);
    }

    [Fact]
    public async Task DispatchBatchAsync_NeverSendsTenantANotificationToTenantBDevice()
    {
        await using var database = await TestDatabase.CreateAsync();
        var sender = new TestPushSender();
        Guid deliveryId;
        await using (var seed = database.CreateContext())
        {
            var notification = CreateNotification(TenantA, UserA, NotificationTypes.ProjectHandover);
            deliveryId = AddPushDelivery(seed, notification);
            seed.UserDeviceTokens.Add(CreateDevice(TenantB, UserA, "other-tenant-token"));
            await seed.SaveChangesAsync();
        }

        await using var context = database.CreateContext();
        await CreateProcessor(context, sender).DispatchBatchAsync();

        Assert.Empty(sender.Sent);
        Assert.Equal(NotificationDeliveryStatus.FailedPermanent, (await context.NotificationDeliveries.SingleAsync(item => item.Id == deliveryId)).Status);
    }

    [Fact]
    public async Task DispatchBatchAsync_RecoversExpiredProcessingLease()
    {
        await using var database = await TestDatabase.CreateAsync();
        var sender = new TestPushSender();
        var options = CreateOptions();
        Guid deliveryId;
        await using (var seed = database.CreateContext())
        {
            var notification = CreateNotification(TenantA, UserA, NotificationTypes.ProjectHandover);
            var delivery = CreateDelivery(notification);
            delivery.Status = NotificationDeliveryStatus.Processing;
            delivery.ProcessingStartedAt = DateTime.UtcNow - options.ProcessingLease - TimeSpan.FromSeconds(1);
            seed.Notifications.Add(notification);
            seed.NotificationDeliveries.Add(delivery);
            deliveryId = delivery.Id;
            seed.UserDeviceTokens.Add(CreateDevice(TenantA, UserA, "recoverable-token"));
            await seed.SaveChangesAsync();
        }

        await using var context = database.CreateContext();
        Assert.Equal(1, await CreateProcessor(context, sender, options).DispatchBatchAsync());
        Assert.Equal(NotificationDeliveryStatus.Delivered, (await context.NotificationDeliveries.SingleAsync(item => item.Id == deliveryId)).Status);
        Assert.Single(sender.Sent);
    }

    [Fact]
    public async Task ConcurrentProcessors_ClaimOneDeliveryAndSendOnlyOnce()
    {
        await using var database = await TestDatabase.CreateAsync();
        var enteredProvider = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseProvider = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sender = new TestPushSender
        {
            Handler = async (_, _, cancellationToken) =>
            {
                enteredProvider.TrySetResult();
                await releaseProvider.Task.WaitAsync(cancellationToken);
                return new PushSendResult(true);
            },
        };
        await using (var seed = database.CreateContext())
        {
            var notification = CreateNotification(TenantA, UserA, NotificationTypes.ProjectHandover);
            AddPushDelivery(seed, notification);
            seed.UserDeviceTokens.Add(CreateDevice(TenantA, UserA, "concurrent-token"));
            await seed.SaveChangesAsync();
        }

        await using var firstContext = database.CreateContext();
        await using var secondContext = database.CreateContext();
        var firstRun = CreateProcessor(firstContext, sender).DispatchBatchAsync();
        await enteredProvider.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var secondRun = await CreateProcessor(secondContext, sender).DispatchBatchAsync();
        releaseProvider.TrySetResult();
        await firstRun;

        Assert.Equal(0, secondRun);
        Assert.Single(sender.Sent);
    }

    [Fact]
    public async Task DeviceRegistration_IsIdempotentAndSupportsMultipleDevicesPerUser()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var service = new DeviceTokenService(context);
        var first = await service.RegisterAsync(TenantA, UserA, new RegisterDeviceTokenRequest("registration-token-a", "web", "Browser A"));
        var same = await service.RegisterAsync(TenantA, UserA, new RegisterDeviceTokenRequest("registration-token-a", "web", "Browser A updated"));
        var second = await service.RegisterAsync(TenantA, UserA, new RegisterDeviceTokenRequest("registration-token-b", "web", "Browser B"));

        Assert.Equal(first.Id, same.Id);
        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(2, await context.UserDeviceTokens.CountAsync(item => item.TenantId == TenantA && item.UserId == UserA && item.IsActive));
        Assert.Equal("Browser A updated", (await context.UserDeviceTokens.SingleAsync(item => item.Id == first.Id)).DeviceName);
    }

    [Fact]
    public async Task DeviceRegistration_RejectsCrossTenantOwnershipWithoutMutatingPriorOwner()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var service = new DeviceTokenService(context);
        var previous = await service.RegisterAsync(TenantA, UserA, new RegisterDeviceTokenRequest("shared-registration-token", "web"));
        await Assert.ThrowsAsync<DeviceTokenOwnershipConflictException>(async () =>
        {
            _ = await service.RegisterAsync(TenantB, UserB, new RegisterDeviceTokenRequest("shared-registration-token", "web"));
        });

        var rows = await context.UserDeviceTokens.ToListAsync();
        var owner = Assert.Single(rows);
        Assert.Equal(previous.Id, owner.Id);
        Assert.Equal((TenantA, UserA, true), (owner.TenantId, owner.UserId, owner.IsActive));
    }

    [Fact]
    public async Task DeviceRegistration_RejectsSameTenantDifferentUserWithoutMutatingPriorOwner()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var service = new DeviceTokenService(context);
        var previous = await service.RegisterAsync(TenantA, UserA, new RegisterDeviceTokenRequest("shared-user-token", "web"));

        await Assert.ThrowsAsync<DeviceTokenOwnershipConflictException>(async () =>
        {
            _ = await service.RegisterAsync(TenantA, UserB, new RegisterDeviceTokenRequest("shared-user-token", "web"));
        });

        var rows = await context.UserDeviceTokens.ToListAsync();
        var owner = Assert.Single(rows);
        Assert.Equal(previous.Id, owner.Id);
        Assert.Equal((TenantA, UserA, true), (owner.TenantId, owner.UserId, owner.IsActive));
    }

    [Fact]
    public async Task DeviceRegistration_OwnershipConflictDoesNotDeactivateCallersPreviousDevice()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var service = new DeviceTokenService(context);
        var callerDevice = await service.RegisterAsync(TenantB, UserB, new RegisterDeviceTokenRequest("caller-device-token", "web"));
        var otherOwnerDevice = await service.RegisterAsync(TenantA, UserA, new RegisterDeviceTokenRequest("other-owner-token", "web"));

        await Assert.ThrowsAsync<DeviceTokenOwnershipConflictException>(async () =>
        {
            _ = await service.RegisterAsync(TenantB, UserB, new RegisterDeviceTokenRequest(
                "other-owner-token", "web", PreviousDeviceId: callerDevice.Id));
        });

        var rows = await context.UserDeviceTokens.OrderBy(item => item.TenantId).ToListAsync();
        Assert.Equal(2, rows.Count);
        Assert.All(rows, item => Assert.True(item.IsActive));
        Assert.Equal((TenantA, UserA), (rows[0].TenantId, rows[0].UserId));
        Assert.Equal((TenantB, UserB), (rows[1].TenantId, rows[1].UserId));
        Assert.Equal(otherOwnerDevice.Id, rows[0].Id);
        Assert.Equal(callerDevice.Id, rows[1].Id);
    }

    [Fact]
    public async Task RegisterDeviceEndpoint_ReturnsConflictWhenTokenBelongsToAnotherOwner()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var service = new DeviceTokenService(context);
        await service.RegisterAsync(TenantA, UserA, new RegisterDeviceTokenRequest("endpoint-shared-token", "web"));
        var controller = new NotificationController(CreateService(context, new TestPushSender()), service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(
                    [
                        new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, UserB.ToString()),
                        new System.Security.Claims.Claim("tenantId", TenantB.ToString()),
                    ], "test")),
                },
            },
        };

        var response = await controller.RegisterDevice(new RegisterDeviceTokenRequest("endpoint-shared-token", "web"), CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(response);
        Assert.Equal("deviceTokenOwnershipConflict", conflict.Value!.GetType().GetProperty("code")!.GetValue(conflict.Value));
        var owner = Assert.Single(await context.UserDeviceTokens.ToListAsync());
        Assert.Equal((TenantA, UserA, true), (owner.TenantId, owner.UserId, owner.IsActive));
    }

    [Fact]
    public async Task DeviceRegistration_RefreshDeactivatesPreviousDeviceAndRegistersNewToken()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var service = new DeviceTokenService(context);
        var previous = await service.RegisterAsync(TenantA, UserA, new RegisterDeviceTokenRequest("old-registration-token", "web"));
        var current = await service.RegisterAsync(TenantA, UserA, new RegisterDeviceTokenRequest("new-registration-token", "web", PreviousDeviceId: previous.Id));

        Assert.NotEqual(previous.Id, current.Id);
        Assert.False((await context.UserDeviceTokens.SingleAsync(item => item.Id == previous.Id)).IsActive);
        Assert.True((await context.UserDeviceTokens.SingleAsync(item => item.Id == current.Id)).IsActive);
    }

    [Fact]
    public async Task RegisterDeviceEndpoint_RejectsRequestsWithoutAuthenticatedIdentity()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var context = database.CreateContext();
        var controller = new NotificationController(CreateService(context, new TestPushSender()), new DeviceTokenService(context))
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };

        var response = await controller.RegisterDevice(new RegisterDeviceTokenRequest("registration-token", "web"), CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(response);
        Assert.Empty(await context.UserDeviceTokens.ToListAsync());
    }

    private static NotificationService CreateService(DbContext context, TestPushSender sender) => new(context, sender, Options.Create(CreateOptions()));

    private static NotificationDeliveryProcessor CreateProcessor(DbContext context, TestPushSender sender, NotificationDeliveryOptions? options = null) =>
        new(context, sender, Options.Create(options ?? CreateOptions()));

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

    private static Notification CreateNotification(Guid tenantId, Guid userId, string type) => new()
    {
        TenantId = tenantId,
        UserId = userId,
        Type = type,
        Title = "A handover update",
        Body = "Review the handover in NEXUS.",
        CreatedBy = "test",
    };

    private static UserDeviceToken CreateDevice(Guid tenantId, Guid userId, string token) => new()
    {
        TenantId = tenantId,
        UserId = userId,
        Token = token,
        TokenHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.Unicode.GetBytes(token))),
        Platform = "web",
        CreatedBy = "test",
    };

    private static Guid AddPushDelivery(NotificationTestDbContext context, Notification notification)
    {
        var delivery = CreateDelivery(notification);
        context.Notifications.Add(notification);
        context.NotificationDeliveries.Add(delivery);
        return delivery.Id;
    }

    private static NotificationDelivery CreateDelivery(Notification notification) => new()
    {
        TenantId = notification.TenantId,
        NotificationId = notification.Id,
        Channel = NotificationDeliveryChannel.Push,
        Status = NotificationDeliveryStatus.Pending,
        MaxAttempts = 3,
        NextAttemptAt = DateTime.UtcNow,
        CreatedBy = "test",
    };

    private sealed class TestPushSender : IPushNotificationSender
    {
        public bool IsConfigured { get; set; }
        public Func<Notification, UserDeviceToken, CancellationToken, Task<PushSendResult>> Handler { get; set; } =
            (_, _, _) => Task.FromResult(new PushSendResult(true, ProviderMessageId: "provider-message"));
        public List<Guid> Sent { get; } = [];

        public async Task<PushSendResult> SendAsync(Notification notification, UserDeviceToken device, CancellationToken cancellationToken = default)
        {
            Sent.Add(device.Id);
            return await Handler(notification, device, cancellationToken);
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
            var connectionString = $"Data Source=notifications_{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
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
        }
    }
}
