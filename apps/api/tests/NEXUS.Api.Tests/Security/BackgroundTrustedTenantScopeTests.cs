using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using NEXUS.Api.Tests.Testing;
using NEXUS.Modules.CostManagement.Application;
using NEXUS.Modules.CostManagement.Domain;
using NEXUS.Modules.Identity.Application;
using NEXUS.Modules.Notifications.Application;
using NEXUS.Modules.Notifications.Domain;
using NEXUS.Modules.Notifications.Infrastructure;
using NEXUS.Persistence;
using NEXUS.SharedKernel;
using Xunit;

namespace NEXUS.Api.Tests.Security;

[Collection("Static tenant resolution")]
public sealed class BackgroundTrustedTenantScopeTests : IDisposable
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly AppDbContext _db;

    public BackgroundTrustedTenantScopeTests()
    {
        _connection.Open();
        AppDbContext.ConfigureTenantResolution(new HttpContextAccessor());
        _db = new SqliteTestAppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options);
        _db.Database.EnsureCreated();
    }

    [Fact]
    public async Task Payment_due_worker_persists_dispatch_and_notification_inside_matching_trusted_scope()
    {
        var obligation = new PaymentObligation
        {
            TenantId = _tenantId,
            ProjectId = Guid.NewGuid(),
            ProjectName = "Tenant project",
            PaymentReference = "PAY-001",
            Purpose = "Milestone payment",
            Description = "Due payment for worker scope test",
            Amount = 125m,
            Currency = "USD",
            DueDate = new DateTime(2026, 11, 7, 0, 0, 0, DateTimeKind.Utc),
            CreatedBy = _userId.ToString("D")
        };
        using (TrustedTenantExecutionContext.Enter(_tenantId))
        {
            _db.Set<PaymentObligation>().Add(obligation);
            await _db.SaveChangesAsync();
        }

        var permissions = new Mock<IUserEffectivePermissionService>();
        permissions.Setup(service => service.GetEffectivePermissionsAsync(
                _userId, _tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>(StringComparer.Ordinal)
            {
                "ProjectPaymentRequest.View",
                "ProjectSettlement.View"
            });
        var notificationService = new Mock<INotificationService>();
        notificationService.Setup(service => service.SendAsync(
                It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .Returns((Notification notification, CancellationToken cancellationToken) =>
                PersistNotificationAsync(notification, cancellationToken));

        var worker = new PaymentDueNotificationService(
            _db,
            notificationService.Object,
            permissions.Object,
            Mock.Of<IProjectMilestoneReferenceLookup>());

        var createdCount = await worker.SendDueNotificationsAsync(
            new DateTime(2026, 11, 7, 12, 0, 0, DateTimeKind.Utc));

        Assert.Equal(1, createdCount);
        var dispatch = await _db.Set<PaymentNotificationDispatch>().IgnoreQueryFilters().AsNoTracking().SingleAsync();
        var notificationRow = await _db.Set<Notification>().IgnoreQueryFilters().AsNoTracking().SingleAsync();
        Assert.Equal(_tenantId, dispatch.TenantId);
        Assert.Equal(_tenantId, notificationRow.TenantId);
        Assert.Equal(obligation.Id, dispatch.PaymentObligationId);
        Assert.Equal(dispatch.NotificationId, notificationRow.Id);
        notificationService.Verify(service => service.SendAsync(
            It.IsAny<Notification>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Notification_delivery_worker_persists_delivery_state_inside_matching_trusted_scope()
    {
        var notification = new Notification
        {
            TenantId = _tenantId,
            UserId = _userId,
            Type = NotificationTypes.ProjectHandover,
            Title = "Scope test",
            Body = "Notification delivery tenant scope test",
            CreatedBy = "test"
        };
        var delivery = new NotificationDelivery
        {
            TenantId = _tenantId,
            NotificationId = notification.Id,
            Channel = NotificationDeliveryChannel.Push,
            Status = NotificationDeliveryStatus.Pending,
            MaxAttempts = 3,
            NextAttemptAt = DateTime.UtcNow.AddMinutes(-1),
            CreatedBy = "test"
        };
        using (TrustedTenantExecutionContext.Enter(_tenantId))
        {
            _db.AddRange(notification, delivery);
            await _db.SaveChangesAsync();
        }

        var sender = new Mock<IPushNotificationSender>();
        sender.SetupGet(service => service.IsConfigured).Returns(true);
        var worker = new NotificationDeliveryProcessor(
            _db,
            sender.Object,
            Options.Create(new NotificationDeliveryOptions()));

        var processedCount = await worker.DispatchBatchAsync();

        Assert.Equal(1, processedCount);
        var persisted = await _db.Set<NotificationDelivery>().IgnoreQueryFilters().AsNoTracking().SingleAsync();
        Assert.Equal(_tenantId, persisted.TenantId);
        Assert.Equal(NotificationDeliveryStatus.FailedPermanent, persisted.Status);
        Assert.Equal("NoActiveDevices", persisted.FailureCategory);
        sender.Verify(service => service.SendAsync(
            It.IsAny<Notification>(), It.IsAny<UserDeviceToken>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private async Task<Result> PersistNotificationAsync(Notification notification, CancellationToken cancellationToken)
    {
        _db.Set<Notification>().Add(notification);
        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public void Dispose()
    {
        AppDbContext.ConfigureTenantResolution(new HttpContextAccessor());
        _db.Dispose();
        _connection.Dispose();
    }
}
