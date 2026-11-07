using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using NEXUS.Api.Notifications;
using NEXUS.Modules.Identity.Application;
using NEXUS.Modules.Identity.Domain;
using NEXUS.Modules.Notifications.Application;
using NEXUS.Modules.Notifications.Domain;
using NEXUS.Modules.Organization.Application.Settings;
using NEXUS.Modules.Organization.Domain;
using NEXUS.Modules.Portfolio.Domain;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.Modules.ResourceManagement.Domain;
using NEXUS.Modules.RiskManagement.Domain;
using NEXUS.Persistence;
using NEXUS.SharedKernel.Management;
using Xunit;

namespace NEXUS.Api.Tests.Integration;

public sealed class RiskActionFollowUpProcessorTests
{
    private static readonly Guid TenantId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly DateTime FollowUpDate = new(2026, 11, 2, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTimeOffset DueAtUtc = new(2026, 11, 2, 8, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("en", "Risk Action Follow-up Due")]
    [InlineData("ar", "\u062d\u0627\u0646 \u0645\u0648\u0639\u062f \u0645\u062a\u0627\u0628\u0639\u0629 \u0625\u062c\u0631\u0627\u0621 \u0627\u0644\u062e\u0637\u0631")]
    public async Task ProcessDueAsync_UsesCurrentDistinctOwnerAndCreatesStableLocalizedPublicReference(
        string language,
        string expectedTitle)
    {
        await using var database = await TestDatabase.CreateAsync();
        var ownerId = Guid.Parse("10000000-0000-0000-0000-000000000010");
        var scenario = await AddScenarioAsync(database.Context, ownerId, ownerId);
        var processor = CreateProcessor(database.Context, language: language);

        var firstPass = await processor.ProcessDueAsync();
        var retryPass = await processor.ProcessDueAsync();

        var notification = await database.Context.Notifications.IgnoreQueryFilters().SingleAsync();
        Assert.Equal(1, firstPass);
        Assert.Equal(0, retryPass);
        Assert.Equal(ownerId, notification.UserId);
        Assert.Equal(NotificationTypes.RiskActionFollowUpDue, notification.Type);
        Assert.Equal(expectedTitle, notification.Title);
        Assert.Contains(scenario.Action.Title, notification.Body!);
        Assert.Contains(scenario.Project.Name, notification.Body!);
        Assert.Contains(scenario.Risk.Title, notification.Body!);
        Assert.Equal("RiskActionFollowUpDue", notification.ReferenceType);
        Assert.Equal((Guid?)scenario.Action.PublicId, notification.ReferenceId);
        Assert.NotEqual((Guid?)scenario.Action.Id, notification.ReferenceId);
        Assert.Equal(DueAtUtc.UtcDateTime, notification.ScheduledAt!.Value);
        Assert.False(notification.IsRead);
        Assert.True(notification.IsInAppVisible);
        Assert.Equal(1, await CreateNotificationService(database.Context).GetUnreadCountAsync(TenantId, ownerId));

        var occurrence = string.Join('|',
            TenantId.ToString("N"),
            NotificationTypes.RiskActionFollowUpDue,
            scenario.Action.Id.ToString("N"),
            "2026-11-02",
            ownerId.ToString("N"));
        var expectedId = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(occurrence)).AsSpan(0, 16));
        Assert.Equal(expectedId, notification.Id);
        Assert.Equal(1, await database.Context.Notifications.IgnoreQueryFilters().CountAsync());

        if (language == "ar")
        {
            Assert.Contains("\u0627\u0644\u0645\u0634\u0631\u0648\u0639", notification.Body!);
            Assert.Contains("\u0627\u0644\u062e\u0637\u0631", notification.Body!);
        }
        else
        {
            Assert.Contains("Project:", notification.Body!);
            Assert.Contains("Risk:", notification.Body!);
        }
    }

    [Fact]
    public async Task ProcessDueAsync_UsesInheritedOrganizationCalendarAndHonorsProjectOverride()
    {
        await using var database = await TestDatabase.CreateAsync();
        var ownerId = Guid.Parse("10000000-0000-0000-0000-000000000051");
        var scenario = await AddDirectOwnerScenarioAsync(database.Context, ownerId, ownerId);
        var projectCalendarId = scenario.Project.WorkingCalendarId!.Value;
        var organization = new Organization
        {
            TenantId = TenantId,
            Name = "Calendar Organization",
            Code = "CAL-ORG",
            CreatedBy = "test"
        };
        var portfolio = new Portfolio
        {
            TenantId = TenantId,
            OrganizationId = organization.Id,
            Name = "Calendar Portfolio",
            Currency = "USD",
            CreatedBy = "test"
        };
        var organizationCalendar = CreateCalendar(TimeSpan.FromHours(11));
        var tuesdayRule = organizationCalendar.DayRules.Single(rule => rule.DayOfWeek == DayOfWeek.Tuesday);
        tuesdayRule.IsWorkingDay = true;
        tuesdayRule.StartTime = TimeSpan.FromHours(9.5);
        tuesdayRule.EndTime = TimeSpan.FromHours(17.5);
        organizationCalendar.Holidays.Add(new Holiday
        {
            TenantId = TenantId,
            Name = "Organization holiday",
            Date = new DateTime(2026, 11, 2),
            IsRecurring = false,
            CreatedBy = "test"
        });
        scenario.Project.PortfolioId = portfolio.Id;
        scenario.Project.WorkingCalendarId = null;
        database.Context.AddRange(organization, portfolio, organizationCalendar);
        await database.Context.SaveChangesAsync();

        // No Organization override exists. The Organization's effective setting inherits the Tenant value.
        var inheritedProcessor = CreateProcessor(
            database.Context,
            now: new DateTimeOffset(2026, 11, 4, 0, 0, 0, TimeSpan.Zero),
            tenantCalendarId: organizationCalendar.Id);
        Assert.Equal(1, await inheritedProcessor.ProcessDueAsync());
        var inheritedNotification = await database.Context.Notifications.IgnoreQueryFilters().SingleAsync();
        Assert.Equal(new DateTime(2026, 11, 3, 9, 30, 0, DateTimeKind.Utc), inheritedNotification.ScheduledAt);

        database.Context.Notifications.Remove(inheritedNotification);
        await database.Context.SaveChangesAsync();
        var organizationOverrideProcessor = CreateProcessor(
            database.Context,
            now: new DateTimeOffset(2026, 11, 4, 0, 0, 0, TimeSpan.Zero),
            tenantCalendarId: projectCalendarId,
            organizationCalendarId: organizationCalendar.Id,
            organizationTimeZoneId: "Eastern Standard Time");

        Assert.Equal(1, await organizationOverrideProcessor.ProcessDueAsync());
        var organizationOverrideNotification = await database.Context.Notifications.IgnoreQueryFilters().SingleAsync();
        Assert.Equal(new DateTime(2026, 11, 3, 14, 30, 0, DateTimeKind.Utc), organizationOverrideNotification.ScheduledAt);

        database.Context.Notifications.Remove(organizationOverrideNotification);
        await database.Context.SaveChangesAsync();
        scenario.Project.WorkingCalendarId = projectCalendarId;
        await database.Context.SaveChangesAsync();
        var projectOverrideProcessor = CreateProcessor(
            database.Context,
            now: new DateTimeOffset(2026, 11, 4, 0, 0, 0, TimeSpan.Zero),
            tenantCalendarId: projectCalendarId,
            organizationCalendarId: organizationCalendar.Id,
            organizationTimeZoneId: "Eastern Standard Time");

        Assert.Equal(1, await projectOverrideProcessor.ProcessDueAsync());
        var projectOverrideNotification = await database.Context.Notifications.IgnoreQueryFilters().SingleAsync();
        Assert.Equal(new DateTime(2026, 11, 2, 13, 0, 0, DateTimeKind.Utc), projectOverrideNotification.ScheduledAt);
    }

    [Fact]
    public async Task ProcessDueAsync_DoesNotUseUnconfiguredTenantDefaultCalendar()
    {
        await using var database = await TestDatabase.CreateAsync();
        var ownerId = Guid.Parse("10000000-0000-0000-0000-000000000052");
        var scenario = await AddDirectOwnerScenarioAsync(database.Context, ownerId, ownerId);
        scenario.Project.WorkingCalendarId = null;
        await database.Context.SaveChangesAsync();

        var processed = await CreateProcessor(database.Context).ProcessDueAsync();

        Assert.Equal(0, processed);
        Assert.Empty(await database.Context.Notifications.IgnoreQueryFilters().ToListAsync());
    }

    [Fact]
    public async Task ProcessDueAsync_UsesOwnerChangesMadeBeforeDelivery()
    {
        await using var database = await TestDatabase.CreateAsync();
        var riskOwnerId = Guid.Parse("10000000-0000-0000-0000-000000000011");
        var previousActionOwnerId = Guid.Parse("10000000-0000-0000-0000-000000000012");
        var currentActionOwnerId = Guid.Parse("10000000-0000-0000-0000-000000000013");
        var scenario = await AddScenarioAsync(database.Context, previousActionOwnerId, riskOwnerId, currentActionOwnerId);

        scenario.Action.OwnerResourceId = scenario.ResourcesByUser[currentActionOwnerId].Id;
        await database.Context.SaveChangesAsync();

        var processor = CreateProcessor(database.Context);
        var processed = await processor.ProcessDueAsync();
        var deliveredTo = await database.Context.Notifications.IgnoreQueryFilters()
            .Select(notification => notification.UserId!.Value)
            .OrderBy(userId => userId)
            .ToArrayAsync();

        Assert.Equal(2, processed);
        Assert.Equal(new[] { riskOwnerId, currentActionOwnerId }.Order().ToArray(), deliveredTo);
        Assert.DoesNotContain(previousActionOwnerId, deliveredTo);
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 2)]
    public async Task ProcessDueAsync_UsesDistinctDirectOwnerUserIdsWithoutResourceRows(
        bool differentOwners,
        int expectedNotificationCount)
    {
        await using var database = await TestDatabase.CreateAsync();
        var actionOwnerId = Guid.Parse("10000000-0000-0000-0000-000000000041");
        var riskOwnerId = differentOwners
            ? Guid.Parse("10000000-0000-0000-0000-000000000042")
            : actionOwnerId;
        await AddDirectOwnerScenarioAsync(database.Context, actionOwnerId, riskOwnerId);

        Assert.Empty(await database.Context.Resources.IgnoreQueryFilters().ToListAsync());
        var processed = await CreateProcessor(database.Context).ProcessDueAsync();
        var recipientIds = await database.Context.Notifications.IgnoreQueryFilters()
            .Select(notification => notification.UserId!.Value)
            .OrderBy(userId => userId)
            .ToArrayAsync();

        Assert.Equal(expectedNotificationCount, processed);
        Assert.Equal(expectedNotificationCount, recipientIds.Length);
        Assert.Equal(new[] { actionOwnerId, riskOwnerId }.Distinct().Order().ToArray(), recipientIds);
        Assert.Empty(await database.Context.Resources.IgnoreQueryFilters().ToListAsync());
    }

    [Fact]
    public async Task ProcessDueAsync_RechecksDirectOwnersAndChangedFollowUpDateBeforeDelivery()
    {
        await using var database = await TestDatabase.CreateAsync();
        var previousActionOwnerId = Guid.Parse("10000000-0000-0000-0000-000000000043");
        var currentActionOwnerId = Guid.Parse("10000000-0000-0000-0000-000000000044");
        var riskOwnerId = Guid.Parse("10000000-0000-0000-0000-000000000045");
        var scenario = await AddDirectOwnerScenarioAsync(
            database.Context,
            previousActionOwnerId,
            riskOwnerId,
            nextFollowUpDate: new DateTime(2026, 11, 3, 0, 0, 0, DateTimeKind.Utc));
        var processor = CreateProcessor(database.Context);

        Assert.Equal(0, await processor.ProcessDueAsync());
        Assert.Empty(await database.Context.Notifications.IgnoreQueryFilters().ToListAsync());

        database.Context.Users.Add(new User
        {
            Id = currentActionOwnerId,
            TenantId = TenantId,
            Email = $"{currentActionOwnerId:N}@example.test",
            FirstName = "Current",
            LastName = "Owner",
            IsActive = true,
            CreatedBy = "test"
        });
        scenario.Action.OwnerUserId = currentActionOwnerId;
        scenario.Action.NextFollowUpDate = FollowUpDate;
        await database.Context.SaveChangesAsync();

        var processed = await processor.ProcessDueAsync();
        var notifications = await database.Context.Notifications.IgnoreQueryFilters().ToListAsync();
        var recipientIds = notifications.Select(notification => notification.UserId!.Value).OrderBy(id => id).ToArray();

        Assert.Equal(2, processed);
        Assert.Equal(new[] { currentActionOwnerId, riskOwnerId }.Order().ToArray(), recipientIds);
        Assert.DoesNotContain(previousActionOwnerId, recipientIds);
        Assert.All(notifications, notification => Assert.Equal(DueAtUtc.UtcDateTime, notification.ScheduledAt!.Value));
        Assert.Empty(await database.Context.Resources.IgnoreQueryFilters().ToListAsync());
    }

    [Fact]
    public async Task ProcessDueAsync_ConcurrentWorkersPersistOnlyOneNotificationForAnOccurrence()
    {
        await using var database = await TestDatabase.CreateAsync();
        var ownerId = Guid.Parse("10000000-0000-0000-0000-000000000015");
        await AddScenarioAsync(database.Context, ownerId, ownerId);

        await using var firstContext = database.CreateContext();
        await using var secondContext = database.CreateContext();
        var firstService = CreateNotificationService(firstContext);
        var secondService = CreateNotificationService(secondContext);
        var reachedEligibilityBarrier = 0;
        var releaseWorkers = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        async Task<IReadOnlyDictionary<Guid, NotificationChannelEligibility>> ResolveEligibilityTogetherAsync(
            NotificationService service,
            Guid tenantId,
            IReadOnlyCollection<Guid> userIds,
            string type,
            CancellationToken cancellationToken)
        {
            var result = await service.GetChannelEligibilityAsync(tenantId, userIds, type, cancellationToken);
            if (Interlocked.Increment(ref reachedEligibilityBarrier) == 2)
                releaseWorkers.TrySetResult(true);
            await releaseWorkers.Task.WaitAsync(cancellationToken);
            return result;
        }

        var firstNotifications = CreateSynchronizedNotificationService(firstService, ResolveEligibilityTogetherAsync);
        var secondNotifications = CreateSynchronizedNotificationService(secondService, ResolveEligibilityTogetherAsync);
        var firstProcessor = CreateProcessor(firstContext, notificationService: firstNotifications);
        var secondProcessor = CreateProcessor(secondContext, notificationService: secondNotifications);

        var results = await Task.WhenAll(
            firstProcessor.ProcessDueAsync(),
            secondProcessor.ProcessDueAsync());

        Assert.Equal(1, results.Sum());
        var stored = await database.Context.Notifications.IgnoreQueryFilters().ToListAsync();
        Assert.Single(stored);
        Assert.Equal(ownerId, stored[0].UserId);
    }

    [Fact]
    public async Task ProcessDueAsync_RecoversMissedOccurrenceAfterRestartWithoutBackdating()
    {
        await using var database = await TestDatabase.CreateAsync();
        var ownerId = Guid.Parse("10000000-0000-0000-0000-000000000016");
        await AddScenarioAsync(database.Context, ownerId, ownerId);
        var recoveredAt = DueAtUtc.AddDays(4);

        await using (var stoppedContext = database.CreateContext())
        {
            var beforeDue = await CreateProcessor(stoppedContext, now: DueAtUtc.AddMinutes(-1)).ProcessDueAsync();
            Assert.Equal(0, beforeDue);
        }

        await using (var restartedContext = database.CreateContext())
        {
            var recovered = await CreateProcessor(restartedContext, now: recoveredAt).ProcessDueAsync();
            Assert.Equal(1, recovered);
        }

        var notification = await database.Context.Notifications.IgnoreQueryFilters().SingleAsync();
        Assert.Equal(DueAtUtc.UtcDateTime, notification.ScheduledAt!.Value);
        Assert.Equal(recoveredAt.UtcDateTime, notification.SentAt);

        await using (var retryContext = database.CreateContext())
        {
            var retry = await CreateProcessor(retryContext, now: recoveredAt).ProcessDueAsync();
            Assert.Equal(0, retry);
        }
        Assert.Single(await database.Context.Notifications.IgnoreQueryFilters().ToListAsync());
    }

    [Fact]
    public async Task ProcessDueAsync_DoesNotNotifyOwnerFromAnotherTenant()
    {
        await using var database = await TestDatabase.CreateAsync();
        var actionOwnerId = Guid.Parse("10000000-0000-0000-0000-000000000017");
        var foreignRiskOwnerId = Guid.Parse("20000000-0000-0000-0000-000000000018");
        await AddDirectOwnerScenarioAsync(database.Context, actionOwnerId, foreignRiskOwnerId);

        var foreignOwner = await database.Context.Users.IgnoreQueryFilters()
            .SingleAsync(user => user.Id == foreignRiskOwnerId);
        foreignOwner.TenantId = Guid.Parse("20000000-0000-0000-0000-000000000001");
        await database.Context.SaveChangesAsync();

        var processed = await CreateProcessor(database.Context).ProcessDueAsync();
        var recipients = await database.Context.Notifications.IgnoreQueryFilters()
            .Select(notification => notification.UserId!.Value)
            .ToArrayAsync();

        Assert.Equal(1, processed);
        Assert.Equal(new[] { actionOwnerId }, recipients);
        Assert.DoesNotContain(foreignRiskOwnerId, recipients);
    }

    [Theory]
    [InlineData(RiskActionStatus.Completed, RiskStatus.FollowUp)]
    [InlineData(RiskActionStatus.Cancelled, RiskStatus.FollowUp)]
    [InlineData(RiskActionStatus.Planned, RiskStatus.Closed)]
    public async Task ProcessDueAsync_SuppressesCompletedCancelledOrClosedRecords(
        RiskActionStatus actionStatus,
        RiskStatus riskStatus)
    {
        await using var database = await TestDatabase.CreateAsync();
        var ownerId = Guid.Parse("10000000-0000-0000-0000-000000000020");
        await AddScenarioAsync(database.Context, ownerId, ownerId, actionStatus: actionStatus, riskStatus: riskStatus);

        var processed = await CreateProcessor(database.Context).ProcessDueAsync();

        Assert.Equal(0, processed);
        Assert.Empty(await database.Context.Notifications.IgnoreQueryFilters().ToListAsync());
    }

    [Theory]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public async Task ProcessDueAsync_SuppressesInactiveUnauthorizedOrOutOfScopeOwners(
        bool ownerIsActive,
        bool hasRiskView,
        bool canAccessProject)
    {
        await using var database = await TestDatabase.CreateAsync();
        var ownerId = Guid.Parse("10000000-0000-0000-0000-000000000030");
        await AddScenarioAsync(database.Context, ownerId, ownerId, ownerIsActive: ownerIsActive);

        var processed = await CreateProcessor(
            database.Context,
            hasRiskView: hasRiskView,
            canAccessProject: canAccessProject).ProcessDueAsync();

        Assert.Equal(0, processed);
        Assert.Empty(await database.Context.Notifications.IgnoreQueryFilters().ToListAsync());
    }

    private static RiskActionFollowUpProcessor CreateProcessor(
        AppDbContext context,
        string language = "en",
        bool hasRiskView = true,
        bool canAccessProject = true,
        INotificationService? notificationService = null,
        DateTimeOffset? now = null,
        Guid? tenantCalendarId = null,
        Guid? organizationCalendarId = null,
        string? organizationTimeZoneId = null)
    {
        var notifications = notificationService ?? CreateNotificationService(context);
        var permissionService = new Mock<IUserEffectivePermissionService>();
        permissionService
            .Setup(service => service.GetEffectivePermissionsAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(hasRiskView
                ? (IReadOnlySet<string>)new HashSet<string>(["Risk.View"], StringComparer.Ordinal)
                : new HashSet<string>(StringComparer.Ordinal));

        var scopeResolver = new Mock<IManagementScopeResolver>();
        scopeResolver
            .Setup(resolver => resolver.CanAccessProjectAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(canAccessProject);

        var settingsRepository = new Mock<ISettingsRepository>();
        settingsRepository.SetupGet(repository => repository.IsAvailable).Returns(true);
        settingsRepository
            .Setup(repository => repository.ReadAsync(It.IsAny<SettingsScopeIdentity>(), It.IsAny<CancellationToken>()))
            .Returns((SettingsScopeIdentity identity, CancellationToken _) =>
                Task.FromResult<IReadOnlyDictionary<string, StoredConfigurationValue>>(
                    SettingsFor(identity, language, tenantCalendarId, organizationCalendarId, organizationTimeZoneId)));

        return new RiskActionFollowUpProcessor(
            context,
            notifications,
            permissionService.Object,
            scopeResolver.Object,
            settingsRepository.Object,
            new SettingDefinitionCatalog(),
            new EffectiveConfigurationResolver(),
            NullLogger<RiskActionFollowUpProcessor>.Instance,
            new FixedTimeProvider(now ?? DueAtUtc));
    }

    private static INotificationService CreateSynchronizedNotificationService(
        NotificationService inner,
        Func<NotificationService, Guid, IReadOnlyCollection<Guid>, string, CancellationToken,
            Task<IReadOnlyDictionary<Guid, NotificationChannelEligibility>>> resolveEligibility)
    {
        var service = new Mock<INotificationService>();
        service
            .Setup(notificationService => notificationService.GetChannelEligibilityAsync(
                It.IsAny<Guid>(), It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((Guid tenantId, IReadOnlyCollection<Guid> userIds, string type, CancellationToken cancellationToken) =>
                resolveEligibility(inner, tenantId, userIds, type, cancellationToken));
        service
            .Setup(notificationService => notificationService.SendIdempotentAsync(
                It.IsAny<Notification>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Returns((Notification notification, bool allowPush, bool allowEmail, CancellationToken cancellationToken) =>
                inner.SendIdempotentAsync(notification, allowPush, allowEmail, cancellationToken));
        return service.Object;
    }

    private static IReadOnlyDictionary<string, StoredConfigurationValue> SettingsFor(
        SettingsScopeIdentity identity,
        string language,
        Guid? tenantCalendarId,
        Guid? organizationCalendarId,
        string? organizationTimeZoneId)
    {
        var values = new Dictionary<string, StoredConfigurationValue>(StringComparer.Ordinal);
        if (identity.Scope == SettingScope.Tenant)
        {
            if (language != "en")
                values["regional.defaultLanguage"] = new(
                    Guid.NewGuid(),
                    "regional.defaultLanguage",
                    JsonSerializer.SerializeToElement(language),
                    "test-version");
            if (tenantCalendarId.HasValue)
                values["workingCalendar.defaultCalendarId"] = new(
                    Guid.NewGuid(),
                    "workingCalendar.defaultCalendarId",
                    JsonSerializer.SerializeToElement(tenantCalendarId.Value.ToString("D")),
                    "test-version");
        }
        else if (identity.Scope == SettingScope.Organization && organizationCalendarId.HasValue)
        {
            values["workingCalendar.defaultCalendarId"] = new(
                Guid.NewGuid(),
                "workingCalendar.defaultCalendarId",
                JsonSerializer.SerializeToElement(organizationCalendarId.Value.ToString("D")),
                "test-version");
        }

        if (identity.Scope == SettingScope.Organization && organizationTimeZoneId is not null)
        {
            values["regional.defaultTimeZone"] = new(
                Guid.NewGuid(),
                "regional.defaultTimeZone",
                JsonSerializer.SerializeToElement(organizationTimeZoneId),
                "test-version");
        }

        return values;
    }

    private static NotificationService CreateNotificationService(AppDbContext context)
    {
        var pushSender = new Mock<IPushNotificationSender>();
        pushSender.SetupGet(sender => sender.IsConfigured).Returns(false);
        return new NotificationService(context, pushSender.Object, Options.Create(new NotificationDeliveryOptions()));
    }

    private static async Task<Scenario> AddScenarioAsync(
        AppDbContext context,
        Guid actionOwnerUserId,
        Guid riskOwnerUserId,
        Guid? additionalActionOwnerUserId = null,
        RiskActionStatus actionStatus = RiskActionStatus.Planned,
        RiskStatus riskStatus = RiskStatus.FollowUp,
        bool ownerIsActive = true)
    {
        var userIds = new[] { actionOwnerUserId, riskOwnerUserId, additionalActionOwnerUserId }
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToArray();
        var users = userIds.Select(userId => new User
        {
            Id = userId,
            TenantId = TenantId,
            Email = $"{userId:N}@example.test",
            FirstName = "Risk",
            LastName = "Owner",
            IsActive = ownerIsActive,
            CreatedBy = "test"
        }).ToArray();
        var resourcesByUser = userIds.ToDictionary(userId => userId, userId => new Resource
        {
            TenantId = TenantId,
            UserId = userId,
            Name = $"Resource {userId:N}",
            Type = "Internal",
            Status = "Active",
            IsActive = true,
            CreatedBy = "test"
        });
        var calendar = CreateCalendar();
        var project = new Project
        {
            TenantId = TenantId,
            Name = "Project Atlas",
            Code = "PRJ-ATLAS",
            WorkingCalendarId = calendar.Id,
            CreatedBy = "test"
        };
        var risk = new Risk
        {
            TenantId = TenantId,
            ProjectId = project.Id,
            Title = "Supplier delay",
            Description = "Potential delay in supplier delivery.",
            Status = riskStatus,
            OwnerResourceId = resourcesByUser[riskOwnerUserId].Id,
            CreatedBy = "test"
        };
        var action = new RiskAction
        {
            TenantId = TenantId,
            RiskId = risk.Id,
            Title = "Confirm supplier delivery",
            Description = "Contact the supplier.",
            OwnerResourceId = resourcesByUser[actionOwnerUserId].Id,
            NextFollowUpDate = FollowUpDate,
            Status = actionStatus,
            CreatedBy = "test"
        };

        context.AddRange(users);
        context.AddRange(resourcesByUser.Values);
        context.AddRange(calendar);
        context.AddRange(project, risk, action);
        await context.SaveChangesAsync();

        return new Scenario(project, risk, action, resourcesByUser);
    }

    private static async Task<Scenario> AddDirectOwnerScenarioAsync(
        AppDbContext context,
        Guid actionOwnerUserId,
        Guid riskOwnerUserId,
        DateTime? nextFollowUpDate = null,
        RiskActionStatus actionStatus = RiskActionStatus.Planned,
        RiskStatus riskStatus = RiskStatus.FollowUp)
    {
        var users = new[] { actionOwnerUserId, riskOwnerUserId }
            .Distinct()
            .Select(userId => new User
            {
                Id = userId,
                TenantId = TenantId,
                Email = $"{userId:N}@example.test",
                FirstName = "Direct",
                LastName = "Owner",
                IsActive = true,
                CreatedBy = "test"
            })
            .ToArray();
        var calendar = CreateCalendar();
        var project = new Project
        {
            TenantId = TenantId,
            Name = "Project Atlas",
            Code = "PRJ-ATLAS",
            WorkingCalendarId = calendar.Id,
            CreatedBy = "test"
        };
        var risk = new Risk
        {
            TenantId = TenantId,
            ProjectId = project.Id,
            Title = "Supplier delay",
            Description = "Potential delay in supplier delivery.",
            Status = riskStatus,
            OwnerUserId = riskOwnerUserId,
            CreatedBy = "test"
        };
        var action = new RiskAction
        {
            TenantId = TenantId,
            RiskId = risk.Id,
            Title = "Confirm supplier delivery",
            Description = "Contact the supplier.",
            OwnerUserId = actionOwnerUserId,
            NextFollowUpDate = nextFollowUpDate ?? FollowUpDate,
            Status = actionStatus,
            CreatedBy = "test"
        };

        context.AddRange(users);
        context.AddRange(calendar);
        context.AddRange(project, risk, action);
        await context.SaveChangesAsync();

        return new Scenario(project, risk, action, new Dictionary<Guid, Resource>());
    }

    private static WorkingCalendar CreateCalendar(TimeSpan? mondayStart = null)
    {
        var calendar = new WorkingCalendar
        {
            TenantId = TenantId,
            Name = "Follow-up test calendar",
            IsDefault = true,
            CreatedBy = "test"
        };

        foreach (var day in Enum.GetValues<DayOfWeek>())
        {
            var working = day == DayOfWeek.Monday;
            var start = mondayStart ?? TimeSpan.FromHours(8);
            calendar.DayRules.Add(new WorkingCalendarDayRule
            {
                TenantId = TenantId,
                WorkingCalendarId = calendar.Id,
                DayOfWeek = day,
                IsWorkingDay = working,
                StartTime = working ? start : null,
                EndTime = working ? start.Add(TimeSpan.FromHours(8)) : null,
                CreatedBy = "test"
            });
        }

        return calendar;
    }

    private sealed record Scenario(
        Project Project,
        Risk Risk,
        RiskAction Action,
        IReadOnlyDictionary<Guid, Resource> ResourcesByUser);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class TestDatabase : IAsyncDisposable
    {
        private readonly SqliteConnection _keepAlive;
        private readonly DbContextOptions<AppDbContext> _options;

        private TestDatabase(
            SqliteConnection keepAlive,
            DbContextOptions<AppDbContext> options,
            AppDbContext context)
        {
            _keepAlive = keepAlive;
            _options = options;
            Context = context;
        }

        public AppDbContext Context { get; }

        public AppDbContext CreateContext() => new(_options);

        public static async Task<TestDatabase> CreateAsync()
        {
            var connectionString = $"Data Source=risk_followup_{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
            var keepAlive = new SqliteConnection(connectionString);
            await keepAlive.OpenAsync();
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connectionString)
                .Options;
            var context = new AppDbContext(options);
            await context.Database.EnsureCreatedAsync();
            return new TestDatabase(keepAlive, options, context);
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await _keepAlive.DisposeAsync();
        }
    }
}
