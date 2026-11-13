using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NEXUS.Modules.Identity.Domain;
using NEXUS.Modules.Meetings.Application;
using NEXUS.Modules.Meetings.Domain;
using NEXUS.Modules.Notifications.Application;
using NEXUS.Modules.Notifications.Domain;
using NEXUS.Modules.Notifications.Infrastructure;
using NEXUS.Modules.ProjectManagement.Domain;
using NEXUS.Persistence;
using NEXUS.Persistence.Management;
using NEXUS.Modules.Identity.Infrastructure;
using NEXUS.SharedKernel;
using Xunit;

namespace NEXUS.Api.Tests.Integration;

public sealed class MeetingNotificationPublisherTests : IDisposable
{
    private static readonly Guid TenantId = Guid.Parse("9B011E01-0D97-4E86-85DD-565548E39101");

    public MeetingNotificationPublisherTests() => AppDbContext.ConfigureTenantResolution(new HttpContextAccessor());

    [Fact]
    public async Task Invitation_and_started_notifications_target_only_active_invited_system_users_and_are_idempotent()
    {
        using var tenantScope = TrustedTenantExecutionContext.Enter(TenantId);
        await using var database = await TestDatabase.CreateAsync();
        await using var db = database.CreateContext();
        var project = await AddProjectAsync(db);
        var actorId = await AddUserAsync(db, "actor", hasView: false, hasProjectAccess: false);
        var activeInviteeId = await AddUserAsync(db, "invitee", hasView: false, hasProjectAccess: false);
        var declinedId = await AddUserAsync(db, "declined", hasView: false, hasProjectAccess: false);
        var inactiveId = await AddUserAsync(db, "inactive", hasView: false, hasProjectAccess: false, isActive: false);
        var unsupportedResponseId = await AddUserAsync(db, "unsupported-response", hasView: false, hasProjectAccess: false);
        var organizer = await db.Users.IgnoreQueryFilters().SingleAsync(user => user.Id == actorId);
        organizer.FirstName = "Alex";
        organizer.LastName = "Organizer";

        var meeting = NewMeeting(project.Id, MeetingStatuses.Scheduled, actorId);
        meeting.Title = "Quarterly\r\ndelivery review";
        var actor = NewInvitee(meeting, actorId, MeetingInvitationResponseStatuses.Accepted);
        var invitee = NewInvitee(meeting, activeInviteeId, MeetingInvitationResponseStatuses.Pending);
        var declined = NewInvitee(meeting, declinedId, MeetingInvitationResponseStatuses.Declined);
        var inactive = NewInvitee(meeting, inactiveId, MeetingInvitationResponseStatuses.Accepted);
        var unsupportedResponse = NewInvitee(meeting, unsupportedResponseId, "Cancelled");
        var guest = NewInvitee(meeting, null, MeetingInvitationResponseStatuses.Accepted);
        guest.Email = "guest@example.test";
        guest.Name = "External guest";
        db.Meetings.Add(meeting);
        db.MeetingParticipants.AddRange(actor, invitee, declined, inactive, unsupportedResponse, guest);
        await db.SaveChangesAsync();

        var publisher = CreatePublisher(db, out var pushSender);
        Assert.True((await publisher.QueueInvitationAsync(meeting, invitee, actorId)).IsSuccess);
        Assert.True((await publisher.QueueInvitationAsync(meeting, invitee, actorId)).IsSuccess);
        Assert.True((await publisher.QueueInvitationAsync(meeting, unsupportedResponse, actorId)).IsSuccess);

        meeting.Status = MeetingStatuses.InProgress;
        await db.SaveChangesAsync();
        Assert.True((await publisher.QueueStartedAsync(meeting, actorId)).IsSuccess);
        Assert.Single(await db.Notifications.AsNoTracking().ToListAsync());

        // A start notification must wait until the actual-start timestamp has been
        // persisted; an in-memory timestamp alone does not represent the domain event.
        meeting.ActualStartTime = DateTime.UtcNow;
        Assert.True((await publisher.QueueStartedAsync(meeting, actorId)).IsSuccess);
        Assert.Single(await db.Notifications.AsNoTracking().ToListAsync());

        await db.SaveChangesAsync();
        Assert.True((await publisher.QueueStartedAsync(meeting, actorId)).IsSuccess);
        Assert.True((await publisher.QueueStartedAsync(meeting, actorId)).IsSuccess);

        var notifications = await db.Notifications.AsNoTracking().ToListAsync();
        Assert.Equal(2, notifications.Count);
        Assert.All(notifications, notification =>
        {
            Assert.Equal(activeInviteeId, notification.UserId);
            Assert.Equal("Meeting", notification.ReferenceType);
            Assert.Equal(meeting.PublicId, notification.ReferenceId);
            Assert.NotEqual(meeting.Id, notification.ReferenceId);
            Assert.Contains("\n", notification.Title);
            Assert.Contains("\u062F", notification.Title);
            Assert.Contains("\n\n", notification.Body);
        });
        var invitation = Assert.Single(notifications.Where(item => item.Type == NotificationTypes.MeetingInvitation));
        Assert.Equal("Meeting Invitation\n\u062F\u0639\u0648\u0629 \u0644\u062D\u0636\u0648\u0631 \u0627\u062C\u062A\u0645\u0627\u0639", invitation.Title);
        Assert.Contains("Alex Organizer", invitation.Body);
        Assert.DoesNotContain("\r", invitation.Body);
        var started = Assert.Single(notifications.Where(item => item.Type == NotificationTypes.MeetingStarted));
        Assert.Equal("Meeting Started\n\u0628\u062F\u0623 \u0627\u0644\u0627\u062C\u062A\u0645\u0627\u0639", started.Title);
        Assert.Equal("The meeting \"Quarterly delivery review\" has started. You can open the meeting to follow its agenda and updates.\n\n\u0628\u062F\u0623 \u0627\u0644\u0627\u062C\u062A\u0645\u0627\u0639 \u00ABQuarterly delivery review\u00BB. \u064A\u0645\u0643\u0646\u0643 \u0641\u062A\u062D \u0627\u0644\u0627\u062C\u062A\u0645\u0627\u0639 \u0644\u0645\u062A\u0627\u0628\u0639\u0629 \u062C\u062F\u0648\u0644 \u0627\u0644\u0623\u0639\u0645\u0627\u0644 \u0648\u0627\u0644\u0645\u0633\u062A\u062C\u062F\u0627\u062A.", started.Body);
        Assert.Empty(pushSender.Sent);
    }

    [Fact]
    public async Task Reminder_is_limited_to_an_active_invitee_and_deduplicated_by_scheduled_event()
    {
        using var tenantScope = TrustedTenantExecutionContext.Enter(TenantId);
        await using var database = await TestDatabase.CreateAsync();
        await using var db = database.CreateContext();
        var project = await AddProjectAsync(db);
        var recipientId = await AddUserAsync(db, "reminder", hasView: false, hasProjectAccess: false);
        var declinedId = await AddUserAsync(db, "declined-reminder", hasView: false, hasProjectAccess: false);
        var meeting = NewMeeting(project.Id, MeetingStatuses.Scheduled);
        var recipient = NewInvitee(meeting, recipientId, MeetingInvitationResponseStatuses.Accepted);
        var declined = NewInvitee(meeting, declinedId, MeetingInvitationResponseStatuses.Declined);
        db.Meetings.Add(meeting);
        db.MeetingParticipants.AddRange(recipient, declined);
        await db.SaveChangesAsync();

        var publisher = CreatePublisher(db, out _);
        var scheduled = meeting.StartTime.AddMinutes(-15);
        Assert.True((await publisher.QueueReminderAsync(meeting, recipient, scheduled)).IsSuccess);
        Assert.True((await publisher.QueueReminderAsync(meeting, recipient, scheduled)).IsSuccess);
        Assert.True((await publisher.QueueReminderAsync(meeting, declined, scheduled)).IsSuccess);
        Assert.True((await publisher.QueueReminderAsync(meeting, recipient, scheduled.AddMinutes(-5))).IsSuccess);

        var notifications = await db.Notifications.AsNoTracking().OrderBy(item => item.ScheduledAt).ToListAsync();
        Assert.Equal(2, notifications.Count);
        Assert.All(notifications, notification =>
        {
            Assert.Equal(recipientId, notification.UserId);
            Assert.Equal(NotificationTypes.MeetingReminder, notification.Type);
            Assert.Equal(meeting.PublicId, notification.ReferenceId);
            Assert.Equal("Meeting", notification.ReferenceType);
            Assert.Contains("\n", notification.Title);
        });
        Assert.All(notifications, notification => Assert.Equal("Upcoming Meeting\n\u062A\u0630\u0643\u064A\u0631 \u0628\u0627\u062C\u062A\u0645\u0627\u0639", notification.Title));
        Assert.Equal(new[] { scheduled, scheduled.AddMinutes(-5) }.OrderBy(value => value), notifications.Select(item => item.ScheduledAt!.Value).OrderBy(value => value));
    }

    [Fact]
    public async Task Publication_comments_and_replies_use_effective_view_and_current_project_scope_without_disclosing_comment_text()
    {
        using var tenantScope = TrustedTenantExecutionContext.Enter(TenantId);
        await using var database = await TestDatabase.CreateAsync();
        await using var db = database.CreateContext();
        var project = await AddProjectAsync(db);
        var actorId = await AddUserAsync(db, "minutes-actor", hasView: true, hasProjectAccess: true);
        var eligibleId = await AddUserAsync(db, "eligible", hasView: true, hasProjectAccess: true);
        var deniedId = await AddUserAsync(db, "denied", hasView: true, hasProjectAccess: true, denyMeetingView: true);
        var outOfScopeId = await AddUserAsync(db, "out-of-scope", hasView: true, hasProjectAccess: false);
        var noViewId = await AddUserAsync(db, "no-view", hasView: false, hasProjectAccess: true);
        var inactiveId = await AddUserAsync(db, "inactive-viewer", hasView: true, hasProjectAccess: true, isActive: false);
        var declinedId = await AddUserAsync(db, "declined-viewer", hasView: true, hasProjectAccess: true);
        var unsupportedResponseId = await AddUserAsync(db, "unsupported-viewer", hasView: true, hasProjectAccess: true);

        var meeting = NewMeeting(project.Id, MeetingStatuses.Completed, actorId);
        var participants = new[]
        {
            NewInvitee(meeting, actorId, MeetingInvitationResponseStatuses.Accepted),
            NewInvitee(meeting, eligibleId, MeetingInvitationResponseStatuses.Accepted),
            NewInvitee(meeting, deniedId, MeetingInvitationResponseStatuses.Accepted),
            NewInvitee(meeting, outOfScopeId, MeetingInvitationResponseStatuses.Accepted),
            NewInvitee(meeting, noViewId, MeetingInvitationResponseStatuses.Accepted),
            NewInvitee(meeting, inactiveId, MeetingInvitationResponseStatuses.Accepted),
            NewInvitee(meeting, declinedId, MeetingInvitationResponseStatuses.Declined),
            NewInvitee(meeting, unsupportedResponseId, "Cancelled"),
        };
        var minutes = new MeetingMinutes
        {
            TenantId = TenantId,
            MeetingId = meeting.Id,
            State = MeetingMinutesStates.Final,
            Summary = "Published summary",
            CreatedBy = "test",
        };
        var comment = new MeetingMinutesComment
        {
            TenantId = TenantId,
            MeetingMinutes = minutes,
            MeetingMinutesId = minutes.Id,
            CommentText = "PRIVATE_TOP_LEVEL_COMMENT_TEXT",
            CreatedBy = actorId.ToString("D"),
        };
        var reply = new MeetingMinutesComment
        {
            TenantId = TenantId,
            MeetingMinutes = minutes,
            MeetingMinutesId = minutes.Id,
            ParentCommentId = comment.Id,
            ParentComment = comment,
            CommentText = "PRIVATE_REPLY_COMMENT_TEXT",
            CreatedBy = actorId.ToString("D"),
        };
        db.Meetings.Add(meeting);
        db.MeetingParticipants.AddRange(participants);
        db.MeetingMinutes.Add(minutes);
        db.MeetingMinutesComments.AddRange(comment, reply);
        db.NotificationPreferences.Add(new NotificationPreference
        {
            TenantId = TenantId,
            UserId = eligibleId,
            Type = NotificationTypes.MeetingMinutesPublished,
            InAppEnabled = true,
            PushEnabled = true,
            EmailEnabled = false,
        });
        await db.SaveChangesAsync();

        var publisher = CreatePublisher(db, out var pushSender);
        Assert.True((await publisher.QueueMinutesPublishedAsync(meeting, minutes, actorId)).IsSuccess);
        Assert.True((await publisher.QueueMinutesPublishedAsync(meeting, minutes, actorId)).IsSuccess);
        Assert.True((await publisher.QueueMinutesCommentAsync(meeting, minutes, comment, actorId)).IsSuccess);
        Assert.True((await publisher.QueueMinutesCommentAsync(meeting, minutes, comment, actorId)).IsSuccess);
        Assert.True((await publisher.QueueMinutesCommentAsync(meeting, minutes, reply, actorId)).IsSuccess);
        Assert.True((await publisher.QueueMinutesCommentAsync(meeting, minutes, reply, actorId)).IsSuccess);

        var notifications = await db.Notifications.AsNoTracking().ToListAsync();
        Assert.Equal(3, notifications.Count);
        Assert.All(notifications, notification =>
        {
            Assert.Equal(eligibleId, notification.UserId);
            Assert.Equal("Meeting", notification.ReferenceType);
            Assert.Equal(meeting.PublicId, notification.ReferenceId);
            Assert.NotEqual(meeting.Id, notification.ReferenceId);
            Assert.Contains("\n", notification.Title);
            Assert.DoesNotContain("PRIVATE_TOP_LEVEL_COMMENT_TEXT", notification.Title + notification.Body);
            Assert.DoesNotContain("PRIVATE_REPLY_COMMENT_TEXT", notification.Title + notification.Body);
        });

        var published = Assert.Single(notifications.Where(item => item.Type == NotificationTypes.MeetingMinutesPublished));
        Assert.Null(published.ReferencePublicId);
        Assert.Equal("Meeting Minutes Available\n\u0645\u062D\u0636\u0631 \u0627\u0644\u0627\u062C\u062A\u0645\u0627\u0639 \u0645\u062A\u0627\u062D", published.Title);
        Assert.Equal("The minutes for meeting \"Quarterly delivery review\" have been published and are available for review.\n\n\u062A\u0645 \u0646\u0634\u0631 \u0645\u062D\u0636\u0631 \u0627\u0644\u0627\u062C\u062A\u0645\u0627\u0639 \u00ABQuarterly delivery review\u00BB \u0648\u0623\u0635\u0628\u062D \u0645\u062A\u0627\u062D\u064B\u0627 \u0644\u0644\u0645\u0631\u0627\u062C\u0639\u0629.", published.Body);
        var topLevel = Assert.Single(notifications.Where(item => item.Type == NotificationTypes.MeetingMinutesComment));
        Assert.Equal("New Comment on Meeting Minutes\n\u062A\u0639\u0644\u064A\u0642 \u062C\u062F\u064A\u062F \u0639\u0644\u0649 \u0645\u062D\u0636\u0631 \u0627\u0644\u0627\u062C\u062A\u0645\u0627\u0639", topLevel.Title);
        Assert.Equal(comment.PublicId, topLevel.ReferencePublicId);
        Assert.Equal("A new comment was added to the minutes of meeting \"Quarterly delivery review\".\n\n\u062A\u0645\u062A \u0625\u0636\u0627\u0641\u0629 \u062A\u0639\u0644\u064A\u0642 \u062C\u062F\u064A\u062F \u0639\u0644\u0649 \u0645\u062D\u0636\u0631 \u0627\u0644\u0627\u062C\u062A\u0645\u0627\u0639 \u00ABQuarterly delivery review\u00BB.", topLevel.Body);
        var replyNotice = Assert.Single(notifications.Where(item => item.Type == NotificationTypes.MeetingMinutesCommentReply));
        Assert.Equal("New Reply on Meeting Minutes\n\u0631\u062F \u062C\u062F\u064A\u062F \u0639\u0644\u0649 \u062A\u0639\u0644\u064A\u0642 \u0628\u0645\u062D\u0636\u0631 \u0627\u0644\u0627\u062C\u062A\u0645\u0627\u0639", replyNotice.Title);
        Assert.Equal(reply.PublicId, replyNotice.ReferencePublicId);
        Assert.Equal("A reply was added to a comment on the minutes of meeting \"Quarterly delivery review\".\n\n\u062A\u0645\u062A \u0625\u0636\u0627\u0641\u0629 \u0631\u062F \u0639\u0644\u0649 \u062A\u0639\u0644\u064A\u0642 \u0628\u0645\u062D\u0636\u0631 \u0627\u0644\u0627\u062C\u062A\u0645\u0627\u0639 \u00ABQuarterly delivery review\u00BB.", replyNotice.Body);

        var delivery = Assert.Single(await db.NotificationDeliveries.AsNoTracking().ToListAsync());
        Assert.Equal(published.Id, delivery.NotificationId);
        Assert.Equal(NotificationDeliveryChannel.Push, delivery.Channel);
        Assert.Equal(NotificationDeliveryStatus.Pending, delivery.Status);
        Assert.Empty(pushSender.Sent);
    }

    [Fact]
    public async Task Draft_minutes_do_not_emit_publication_or_comment_notifications()
    {
        using var tenantScope = TrustedTenantExecutionContext.Enter(TenantId);
        await using var database = await TestDatabase.CreateAsync();
        await using var db = database.CreateContext();
        var project = await AddProjectAsync(db);
        var actorId = await AddUserAsync(db, "draft-actor", hasView: true, hasProjectAccess: true);
        var attendeeId = await AddUserAsync(db, "draft-attendee", hasView: true, hasProjectAccess: true);
        var meeting = NewMeeting(project.Id, MeetingStatuses.Completed, actorId);
        var minutes = new MeetingMinutes { TenantId = TenantId, MeetingId = meeting.Id, State = MeetingMinutesStates.Draft, CreatedBy = "test" };
        var comment = new MeetingMinutesComment
        {
            TenantId = TenantId,
            MeetingMinutes = minutes,
            MeetingMinutesId = minutes.Id,
            CommentText = "DRAFT_COMMENT_TEXT",
            CreatedBy = actorId.ToString("D"),
        };
        db.Meetings.Add(meeting);
        db.MeetingParticipants.Add(NewInvitee(meeting, actorId, MeetingInvitationResponseStatuses.Accepted));
        db.MeetingParticipants.Add(NewInvitee(meeting, attendeeId, MeetingInvitationResponseStatuses.Accepted));
        db.MeetingMinutes.Add(minutes);
        db.MeetingMinutesComments.Add(comment);
        await db.SaveChangesAsync();

        var publisher = CreatePublisher(db, out _);
        Assert.True((await publisher.QueueMinutesPublishedAsync(meeting, minutes, actorId)).IsSuccess);
        Assert.True((await publisher.QueueMinutesCommentAsync(meeting, minutes, comment, actorId)).IsSuccess);
        Assert.Empty(await db.Notifications.ToListAsync());
    }

    [Fact]
    public async Task Projectless_completed_meeting_notifies_attendees_with_effective_meeting_view()
    {
        using var tenantScope = TrustedTenantExecutionContext.Enter(TenantId);
        await using var database = await TestDatabase.CreateAsync();
        await using var db = database.CreateContext();
        var actorId = await AddUserAsync(db, "projectless-actor", hasView: true, hasProjectAccess: false);
        var attendeeId = await AddUserAsync(db, "projectless-attendee", hasView: true, hasProjectAccess: false);
        var meeting = NewMeeting(Guid.Empty, MeetingStatuses.Completed, actorId);
        meeting.ProjectId = null;
        var minutes = new MeetingMinutes
        {
            TenantId = TenantId,
            MeetingId = meeting.Id,
            State = MeetingMinutesStates.Final,
            CreatedBy = "test",
        };
        db.Meetings.Add(meeting);
        db.MeetingParticipants.Add(NewInvitee(meeting, actorId, MeetingInvitationResponseStatuses.Accepted));
        db.MeetingParticipants.Add(NewInvitee(meeting, attendeeId, MeetingInvitationResponseStatuses.Accepted));
        db.MeetingMinutes.Add(minutes);
        await db.SaveChangesAsync();

        var publisher = CreatePublisher(db, out _);
        Assert.True((await publisher.QueueMinutesPublishedAsync(meeting, minutes, actorId)).IsSuccess);
        var notification = Assert.Single(await db.Notifications.AsNoTracking().ToListAsync());
        Assert.Equal(attendeeId, notification.UserId);
        Assert.Equal(meeting.PublicId, notification.ReferenceId);
    }

    [Fact]
    public async Task Notification_enqueue_remains_inside_caller_transaction()
    {
        using var tenantScope = TrustedTenantExecutionContext.Enter(TenantId);
        await using var database = await TestDatabase.CreateAsync();
        Guid projectId;
        Guid actorId;
        Guid inviteeId;
        Meeting meeting;
        MeetingParticipant invitee;
        await using (var seed = database.CreateContext())
        {
            var project = await AddProjectAsync(seed);
            projectId = project.Id;
            actorId = await AddUserAsync(seed, "transaction-actor", hasView: false, hasProjectAccess: false);
            inviteeId = await AddUserAsync(seed, "transaction-invitee", hasView: false, hasProjectAccess: false);
            meeting = NewMeeting(projectId, MeetingStatuses.Scheduled, actorId);
            invitee = NewInvitee(meeting, inviteeId, MeetingInvitationResponseStatuses.Accepted);
            seed.Meetings.Add(meeting);
            seed.MeetingParticipants.Add(invitee);
            await seed.SaveChangesAsync();
        }

        await using (var transactionContext = database.CreateContext())
        {
            await using var transaction = await transactionContext.Database.BeginTransactionAsync();
            var publisher = CreatePublisher(transactionContext, out _);
            Assert.True((await publisher.QueueInvitationAsync(meeting, invitee, actorId)).IsSuccess);
            Assert.Single(await transactionContext.Notifications.ToListAsync());
            await transaction.RollbackAsync();
        }

        await using var verify = database.CreateContext();
        Assert.Empty(await verify.Notifications.IgnoreQueryFilters().ToListAsync());
    }

    private static MeetingNotificationPublisher CreatePublisher(AppDbContext context, out TestPushSender pushSender)
    {
        pushSender = new TestPushSender();
        var effectivePermissions = new UserEffectivePermissionService(context);
        var managementScope = new ManagementScopeResolver(context, effectivePermissions, new NEXUS.Persistence.ProjectOrganizationLookup(context));
        var notificationService = new NotificationService(context, pushSender, Options.Create(new NotificationDeliveryOptions()));
        return new MeetingNotificationPublisher(context, notificationService, effectivePermissions, managementScope);
    }

    private static async Task<Project> AddProjectAsync(AppDbContext context)
    {
        var project = new Project { TenantId = TenantId, Name = "Publisher Test Project", Code = $"P-{Guid.NewGuid():N}" };
        context.Projects.Add(project);
        await context.SaveChangesAsync();
        return project;
    }

    private static async Task<Guid> AddUserAsync(
        AppDbContext context,
        string key,
        bool hasView,
        bool hasProjectAccess,
        bool denyMeetingView = false,
        bool isActive = true)
    {
        var user = new User
        {
            TenantId = TenantId,
            Email = $"{key}-{Guid.NewGuid():N}@example.test",
            FirstName = key,
            LastName = "User",
            IsActive = isActive,
        };
        context.Users.Add(user);

        Permission? viewPermission = null;
        if (hasView || denyMeetingView)
        {
            viewPermission = new Permission
            {
                TenantId = TenantId,
                Name = MeetingPermissions.View,
                Resource = "Meeting",
                Action = "View",
            };
            context.Permissions.Add(viewPermission);
        }

        if (hasView)
        {
            var role = new Role { TenantId = TenantId, Name = $"{key} test role" };
            role.Permissions.Add(viewPermission!);
            var assignment = new UserRole { TenantId = TenantId, User = user, Role = role };
            user.UserRoles.Add(assignment);
            context.Roles.Add(role);
            context.UserRoles.Add(assignment);
        }

        if (denyMeetingView)
        {
            context.UserPermissionOverrides.Add(new UserPermissionOverride
            {
                TenantId = TenantId,
                UserId = user.Id,
                PermissionId = viewPermission!.Id,
                Effect = PermissionOverrideEffect.Deny,
                CreatedBy = "test",
            });
        }

        if (hasProjectAccess)
        {
            var projectId = await context.Projects.Select(project => project.Id).FirstAsync();
            context.ProjectManagerAssignments.Add(new ProjectManagerAssignment
            {
                TenantId = TenantId,
                ProjectId = projectId,
                ProjectManagerId = user.Id,
                StartDate = DateTime.UtcNow.Date.AddDays(-1),
                IsCurrent = true,
            });
        }

        await context.SaveChangesAsync();
        return user.Id;
    }

    private static Meeting NewMeeting(Guid projectId, string status, Guid? organizerId = null) => new()
    {
        TenantId = TenantId,
        PublicId = Guid.NewGuid(),
        ProjectId = projectId,
        Title = "Quarterly delivery review",
        StartTime = DateTime.UtcNow.AddMinutes(30),
        EndTime = DateTime.UtcNow.AddHours(1),
        Status = status,
        OrganizerId = organizerId,
    };

    private static MeetingParticipant NewInvitee(Meeting meeting, Guid? userId, string responseStatus) => new()
    {
        TenantId = TenantId,
        MeetingId = meeting.Id,
        UserId = userId,
        Email = userId.HasValue ? $"{userId.Value:N}@example.test" : string.Empty,
        Name = userId?.ToString("D") ?? "External attendee",
        InvitationResponseStatus = responseStatus,
        CreatedBy = "test",
    };

    public void Dispose() => AppDbContext.ConfigureTenantResolution(new HttpContextAccessor());

    private sealed class TestPushSender : IPushNotificationSender
    {
        public bool IsConfigured => true;
        public List<Guid> Sent { get; } = [];
        public Task<PushSendResult> SendAsync(Notification notification, UserDeviceToken device, CancellationToken cancellationToken = default)
        {
            Sent.Add(device.Id);
            return Task.FromResult(new PushSendResult(true));
        }
    }

    private sealed class TestDatabase(SqliteConnection connection, DbContextOptions<AppDbContext> options) : IAsyncDisposable
    {
        public AppDbContext CreateContext() => new(options);

        public static async Task<TestDatabase> CreateAsync()
        {
            var connection = new SqliteConnection($"Data Source=meeting_notifications_{Guid.NewGuid():N};Mode=Memory;Cache=Shared");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
            await using (var context = new AppDbContext(options))
                await context.Database.EnsureCreatedAsync();
            return new TestDatabase(connection, options);
        }

        public ValueTask DisposeAsync() => connection.DisposeAsync();
    }
}
