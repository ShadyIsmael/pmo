using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.Identity.Domain;
using NEXUS.Modules.Meetings.Application;
using NEXUS.Modules.Meetings.Application.DTOs;
using NEXUS.Modules.Meetings.Domain;
using NEXUS.Persistence;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Management;
using Xunit;

namespace NEXUS.Api.Tests.Integration;

public sealed class MeetingNotificationLifecycleAndRescheduleTests : IDisposable
{
    private static readonly Guid TenantId = Guid.Parse("38000000-0000-0000-0000-000000000001");
    private readonly Guid _actorId = Guid.NewGuid();
    private readonly Guid _inviteeId = Guid.NewGuid();
    private readonly Guid _secondInviteeId = Guid.NewGuid();
    private readonly HttpContextAccessor _http = new();
    private readonly Mock<IAuditService> _audit = new();

    public MeetingNotificationLifecycleAndRescheduleTests()
    {
        AppDbContext.ConfigureTenantResolution(_http);
        _audit.Setup(service => service.LogAsync(It.IsAny<AuditEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
    }

    [Fact]
    public async Task Create_and_add_attendee_queue_only_new_system_user_invitations_and_edit_does_not_resend()
    {
        using var tenantScope = TrustedTenantExecutionContext.Enter(TenantId);
        await using var db = CreateInMemoryDb();
        var publisher = new RecordingMeetingPublisher();
        var service = CreateService(db, publisher);
        var start = DateTime.UtcNow.AddDays(2);
        var request = NewMeetingRequest(start,
        [
            new MeetingAttendeeInput { UserId = _inviteeId },
            new MeetingAttendeeInput { UserId = _actorId },
            new MeetingAttendeeInput { Email = "external@example.test", DisplayName = "External guest" },
        ]);

        var created = await service.CreateAsync(request, _actorId.ToString());

        Assert.True(created.IsSuccess);
        var invitation = Assert.Single(publisher.Invitations);
        Assert.Equal(_inviteeId, invitation.Invitee.UserId);
        Assert.Equal(_actorId, invitation.ActorId);
        Assert.Equal(TenantId, invitation.Meeting.TenantId);
        Assert.Equal(MeetingStatuses.Scheduled, invitation.Meeting.Status);

        var edited = await service.UpdateAsync(created.Value!.Id, new UpdateMeetingRequest
        {
            Title = "Edited title",
            ProjectId = null,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            OrganizerId = _actorId,
            Attendees = request.Attendees,
            ReminderOffsetMinutes = request.ReminderOffsetMinutes,
        }, _actorId.ToString());

        Assert.True(edited.IsSuccess);
        Assert.Equal("Edited title", edited.Value!.Title);
        Assert.Single(publisher.Invitations);

        var added = await service.AddAttendeeAsync(edited.Value.Id,
            new MeetingAttendeeInput { UserId = _secondInviteeId }, _actorId.ToString());

        Assert.True(added.IsSuccess);
        Assert.Equal(2, publisher.Invitations.Count);
        Assert.Equal(_secondInviteeId, publisher.Invitations[1].Invitee.UserId);
    }

    [Fact]
    public async Task Start_operation_persists_actual_start_before_queueing_once_and_repeat_start_does_not_resend()
    {
        using var tenantScope = TrustedTenantExecutionContext.Enter(TenantId);
        await using var db = CreateInMemoryDb();
        var meeting = new Meeting
        {
            TenantId = TenantId,
            OrganizerId = _actorId,
            Title = "Start notification integration",
            StartTime = DateTime.UtcNow.AddMinutes(10),
            EndTime = DateTime.UtcNow.AddMinutes(40),
            Status = MeetingStatuses.Scheduled,
        };
        db.Meetings.Add(meeting);
        await db.SaveChangesAsync();

        var publisher = new RecordingMeetingPublisher();
        var service = CreateService(db, publisher);
        var started = await service.StartAsync(meeting.Id, new MeetingLifecycleRequest(), _actorId.ToString());

        Assert.True(started.IsSuccess);
        Assert.Equal(MeetingStatuses.InProgress, started.Value!.Status);
        Assert.NotNull(started.Value.ActualStartTime);
        var eventCall = Assert.Single(publisher.Started);
        Assert.Equal(meeting.Id, eventCall.MeetingId);
        Assert.Equal(_actorId, eventCall.ActorId);
        Assert.NotNull(eventCall.ActualStartTime);

        var repeated = await service.StartAsync(meeting.Id, new MeetingLifecycleRequest(), _actorId.ToString());

        Assert.True(repeated.IsSuccess);
        Assert.Single(publisher.Started);
    }

    [Fact]
    public async Task Finalizing_draft_minutes_queues_publication_once_and_repeated_finalize_does_not_resend()
    {
        using var tenantScope = TrustedTenantExecutionContext.Enter(TenantId);
        await using var db = CreateInMemoryDb();
        var meeting = NewCompletedMeeting();
        var minutes = new MeetingMinutes
        {
            TenantId = TenantId,
            MeetingId = meeting.Id,
            Summary = "Reviewed summary",
            State = MeetingMinutesStates.Draft,
        };
        db.AddRange(meeting, minutes);
        await db.SaveChangesAsync();

        var publisher = new RecordingMeetingPublisher();
        var service = CreateService(db, publisher);
        var finalized = await service.FinalizeMinutesAsync(meeting.Id, new FinalizeMeetingMinutesRequest(), _actorId.ToString());
        var repeated = await service.FinalizeMinutesAsync(meeting.Id, new FinalizeMeetingMinutesRequest(), _actorId.ToString());

        Assert.True(finalized.IsSuccess);
        Assert.True(repeated.IsSuccess);
        Assert.Equal(MeetingMinutesStates.Final, finalized.Value!.State);
        Assert.Equal(finalized.Value.Id, repeated.Value!.Id);
        var publication = Assert.Single(publisher.Publications);
        Assert.Equal(meeting.Id, publication.MeetingId);
        Assert.Equal(minutes.Id, publication.MinutesId);
        Assert.Equal(MeetingMinutesStates.Final, publication.State);
        Assert.Equal(_actorId, publication.ActorId);
    }

    [Fact]
    public async Task Top_level_comment_and_reply_on_persisted_final_minutes_queue_with_correct_parent_and_actor()
    {
        using var tenantScope = TrustedTenantExecutionContext.Enter(TenantId);
        await using var db = CreateInMemoryDb();
        var meeting = NewCompletedMeeting();
        var minutes = new MeetingMinutes
        {
            TenantId = TenantId,
            MeetingId = meeting.Id,
            Summary = "Published summary",
            State = MeetingMinutesStates.Final,
        };
        db.AddRange(meeting, minutes);
        await db.SaveChangesAsync();

        var publisher = new RecordingMeetingPublisher();
        var service = CreateService(db, publisher);
        var topLevel = await service.AddCommentAsync(meeting.Id,
            new CreateMeetingMinutesCommentRequest { CommentText = "Top-level comment" }, _actorId.ToString());
        Assert.True(topLevel.IsSuccess);
        var reply = await service.AddCommentAsync(meeting.Id,
            new CreateMeetingMinutesCommentRequest { CommentText = "Reply", ParentCommentId = topLevel.Value!.Id },
            _actorId.ToString());

        Assert.True(reply.IsSuccess);
        Assert.Equal(2, publisher.Comments.Count);
        var topLevelEvent = publisher.Comments[0];
        Assert.Equal(minutes.Id, topLevelEvent.MinutesId);
        Assert.Equal(topLevel.Value!.Id, topLevelEvent.CommentId);
        Assert.NotEqual(Guid.Empty, topLevelEvent.CommentPublicId);
        Assert.Null(topLevelEvent.ParentCommentId);
        Assert.Equal(_actorId, topLevelEvent.ActorId);
        Assert.Equal(_actorId.ToString(), topLevelEvent.CreatedBy);

        var replyEvent = publisher.Comments[1];
        Assert.Equal(minutes.Id, replyEvent.MinutesId);
        Assert.Equal(reply.Value!.Id, replyEvent.CommentId);
        Assert.NotEqual(Guid.Empty, replyEvent.CommentPublicId);
        Assert.Equal(topLevel.Value.Id, replyEvent.ParentCommentId);
        Assert.Equal(_actorId, replyEvent.ActorId);
        Assert.Equal(_actorId.ToString(), replyEvent.CreatedBy);
        Assert.Equal(2, await db.MeetingMinutesComments.CountAsync());
    }

    [Fact]
    public async Task Scheduler_reschedules_after_start_and_lead_time_change_then_delivers_at_revised_due_instant()
    {
        using var tenantScope = TrustedTenantExecutionContext.Enter(TenantId);
        await using var database = await SqliteTestDatabase.CreateAsync();
        var fixedNow = new DateTime(2026, 11, 5, 12, 0, 0, DateTimeKind.Utc);
        var meeting = NewScheduledMeeting(fixedNow.AddMinutes(60), MeetingReminderOffsets.FifteenMinutes);
        var recipientId = Guid.NewGuid();
        var participant = NewParticipant(meeting, recipientId, MeetingInvitationResponseStatuses.Accepted);
        database.Context.Add(meeting);
        database.Context.Add(participant);
        database.Context.Add(NewUser(recipientId));
        var staleDispatch = new MeetingReminderDispatch
        {
            TenantId = TenantId,
            MeetingId = meeting.Id,
            ScheduledAtUtc = meeting.StartTime.AddMinutes(-MeetingReminderOffsets.FifteenMinutes),
            Status = MeetingReminderDispatchStatuses.Pending,
            CreatedBy = "test",
        };
        database.Context.Add(staleDispatch);
        await database.Context.SaveChangesAsync();

        meeting.StartTime = fixedNow.AddMinutes(40);
        meeting.EndTime = meeting.StartTime.AddMinutes(30);
        meeting.ReminderOffsetMinutes = MeetingReminderOffsets.ThirtyMinutes;
        await database.Context.SaveChangesAsync();

        var publisher = new RecordingReminderPublisher();
        await RunSchedulerOnceAsync(database, publisher, fixedNow, async () =>
        {
            await WaitUntilAsync(async () =>
            {
                await using var context = database.CreateContext();
                return await context.Set<MeetingReminderDispatch>().IgnoreQueryFilters()
                    .Where(item => item.Id == staleDispatch.Id).Select(item => item.IsDeleted).SingleAsync();
            });
        });

        Assert.Empty(publisher.Calls);
        await using (var verifyBeforeDue = database.CreateContext())
        {
            var savedStaleDispatch = await verifyBeforeDue.Set<MeetingReminderDispatch>().IgnoreQueryFilters().SingleAsync();
            Assert.True(savedStaleDispatch.IsDeleted);
        }

        var revisedDue = fixedNow.AddMinutes(10);
        await RunSchedulerOnceAsync(database, publisher, revisedDue, async () =>
        {
            await WaitUntilAsync(async () =>
            {
                await using var context = database.CreateContext();
                return await context.Set<MeetingReminderDispatch>().IgnoreQueryFilters()
                    .AnyAsync(item => !item.IsDeleted && item.Status == MeetingReminderDispatchStatuses.Delivered);
            });
        });

        var call = Assert.Single(publisher.Calls);
        Assert.Equal(meeting.Id, call.Meeting.Id);
        Assert.Equal(recipientId, call.Recipient.UserId);
        Assert.Equal(revisedDue, call.ScheduledAtUtc);
        await using var verifyAfterDue = database.CreateContext();
        var dispatches = await verifyAfterDue.Set<MeetingReminderDispatch>().IgnoreQueryFilters().OrderBy(item => item.Id).ToListAsync();
        Assert.Equal(2, dispatches.Count);
        Assert.Contains(dispatches, item => item.IsDeleted);
        Assert.Contains(dispatches, item => !item.IsDeleted && item.Status == MeetingReminderDispatchStatuses.Delivered
            && item.ScheduledAtUtc == revisedDue);
    }

    [Theory]
    [InlineData("cancelled")]
    [InlineData("declined")]
    public async Task Scheduler_suppresses_pending_reminder_after_meeting_cancellation_or_invitee_decline(string change)
    {
        using var tenantScope = TrustedTenantExecutionContext.Enter(TenantId);
        await using var database = await SqliteTestDatabase.CreateAsync();
        var fixedNow = new DateTime(2026, 11, 5, 12, 0, 0, DateTimeKind.Utc);
        var meeting = NewScheduledMeeting(fixedNow.AddMinutes(10), MeetingReminderOffsets.FifteenMinutes);
        var recipientId = Guid.NewGuid();
        var participant = NewParticipant(meeting, recipientId, MeetingInvitationResponseStatuses.Accepted);
        database.Context.Add(meeting);
        database.Context.Add(participant);
        database.Context.Add(NewUser(recipientId));
        var pending = new MeetingReminderDispatch
        {
            TenantId = TenantId,
            MeetingId = meeting.Id,
            ScheduledAtUtc = fixedNow.AddMinutes(-5),
            Status = MeetingReminderDispatchStatuses.Pending,
            CreatedBy = "test",
        };
        database.Context.Add(pending);
        await database.Context.SaveChangesAsync();
        if (change == "cancelled") meeting.Status = MeetingStatuses.Cancelled;
        else participant.InvitationResponseStatus = MeetingInvitationResponseStatuses.Declined;
        await database.Context.SaveChangesAsync();

        var publisher = new RecordingReminderPublisher();
        await RunSchedulerOnceAsync(database, publisher, fixedNow, async () =>
        {
            await WaitUntilAsync(async () =>
            {
                await using var context = database.CreateContext();
                var dispatch = await context.Set<MeetingReminderDispatch>().IgnoreQueryFilters().SingleAsync();
                return change == "cancelled"
                    ? dispatch.IsDeleted
                    : dispatch.Status == MeetingReminderDispatchStatuses.NoRecipients;
            });
        });

        Assert.Empty(publisher.Calls);
        await using var verify = database.CreateContext();
        var finalDispatch = await verify.Set<MeetingReminderDispatch>().IgnoreQueryFilters().SingleAsync();
        if (change == "cancelled") Assert.True(finalDispatch.IsDeleted);
        else Assert.Equal(MeetingReminderDispatchStatuses.NoRecipients, finalDispatch.Status);
    }

    private AppDbContext CreateInMemoryDb()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("tenantId", TenantId.ToString()),
            new Claim(ClaimTypes.NameIdentifier, _actorId.ToString()),
            new Claim(ClaimTypes.Name, "Meeting lifecycle test"),
        ], "test"));
        _http.HttpContext = httpContext;

        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);
        db.Database.EnsureCreated();
        return db;
    }

    private MeetingService CreateService(AppDbContext db, RecordingMeetingPublisher publisher)
    {
        var directory = new[]
        {
            new UserReference(_actorId, "Meeting Actor", $"{_actorId:N}@example.test"),
            new UserReference(_inviteeId, "Primary Invitee", $"{_inviteeId:N}@example.test"),
            new UserReference(_secondInviteeId, "Second Invitee", $"{_secondInviteeId:N}@example.test"),
        }.ToDictionary(user => user.Id);
        var users = new Mock<IUserReferenceLookup>();
        users.Setup(lookup => lookup.FindAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => directory.GetValueOrDefault(id));
        users.Setup(lookup => lookup.GetManyAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<Guid> ids, CancellationToken _) => (IReadOnlyDictionary<Guid, UserReference>)ids
                .Distinct()
                .Where(directory.ContainsKey)
                .ToDictionary(id => id, id => directory[id]));
        users.Setup(lookup => lookup.FindByEmailsAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<string> _, CancellationToken _) => (IReadOnlyDictionary<string, UserReference>)new Dictionary<string, UserReference>(StringComparer.OrdinalIgnoreCase));

        var management = new TestManagementContextAccessor(new ManagementContext(
            _actorId,
            TenantId,
            ManagementPerspective.ProjectManager,
            ManagementViewMode.Native,
            null,
            null,
            null,
            ManagementScope.Empty));

        return new MeetingService(
            db,
            _audit.Object,
            Mock.Of<IProjectReferenceLookup>(),
            users.Object,
            Mock.Of<IWorkItemReferenceLookup>(),
            _http,
            Mock.Of<IMeetingMinutesAttachmentStorage>(),
            Mock.Of<IMeetingMinutesAttachmentPolicy>(),
            management,
            publisher);
    }

    private CreateMeetingRequest NewMeetingRequest(DateTime start, IReadOnlyList<MeetingAttendeeInput> attendees) => new()
    {
        Title = "Invitation lifecycle test",
        StartTime = start,
        EndTime = start.AddMinutes(30),
        OrganizerId = _actorId,
        Attendees = attendees,
        ReminderOffsetMinutes = MeetingReminderOffsets.ThirtyMinutes,
    };

    private Meeting NewCompletedMeeting() => new()
    {
        TenantId = TenantId,
        OrganizerId = _actorId,
        Title = "Completed Meeting Minutes notification test",
        StartTime = DateTime.UtcNow.AddHours(-2),
        EndTime = DateTime.UtcNow.AddHours(-1),
        ActualStartTime = DateTime.UtcNow.AddHours(-2),
        ActualEndTime = DateTime.UtcNow.AddHours(-1),
        Status = MeetingStatuses.Completed,
        CreatedBy = "test",
    };

    private static Meeting NewScheduledMeeting(DateTime start, int reminderOffset) => new()
    {
        TenantId = TenantId,
        Title = "Reminder reschedule test",
        StartTime = start,
        EndTime = start.AddMinutes(30),
        ReminderOffsetMinutes = reminderOffset,
        Status = MeetingStatuses.Scheduled,
        CreatedBy = "test",
    };

    private static MeetingParticipant NewParticipant(Meeting meeting, Guid userId, string invitationStatus) => new()
    {
        TenantId = meeting.TenantId,
        MeetingId = meeting.Id,
        UserId = userId,
        Email = $"{userId:N}@example.test",
        InvitationResponseStatus = invitationStatus,
        CreatedBy = "test",
    };

    private static User NewUser(Guid userId) => new()
    {
        Id = userId,
        TenantId = TenantId,
        Email = $"{userId:N}@example.test",
        FirstName = "Meeting",
        LastName = "Invitee",
        IsActive = true,
        CreatedBy = "test",
    };

    private static async Task RunSchedulerOnceAsync(
        SqliteTestDatabase database,
        RecordingReminderPublisher publisher,
        DateTime nowUtc,
        Func<Task> waitForOutcome)
    {
        var services = new ServiceCollection()
            .AddLogging()
            .AddScoped(_ => database.CreateContext())
            .AddScoped<IMeetingNotificationPublisher>(_ => publisher)
            .BuildServiceProvider();
        var scheduler = new MeetingReminderScheduler(
            services.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<MeetingReminderScheduler>.Instance,
            new FixedTimeProvider(nowUtc));

        await scheduler.StartAsync(CancellationToken.None);
        try
        {
            await waitForOutcome();
        }
        finally
        {
            await scheduler.StopAsync(CancellationToken.None);
            scheduler.Dispose();
            await services.DisposeAsync();
        }
    }

    private static async Task WaitUntilAsync(Func<Task<bool>> condition)
    {
        var timeout = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < timeout)
        {
            if (await condition()) return;
            await Task.Delay(20);
        }

        Assert.True(await condition(), "The meeting notification operation did not reach the expected state.");
    }

    public void Dispose() => AppDbContext.ConfigureTenantResolution(new HttpContextAccessor());

    private sealed class RecordingMeetingPublisher : IMeetingNotificationPublisher
    {
        public List<InvitationCall> Invitations { get; } = [];
        public List<StartedCall> Started { get; } = [];
        public List<PublicationCall> Publications { get; } = [];
        public List<CommentCall> Comments { get; } = [];

        public Task<Result> QueueInvitationAsync(Meeting meeting, MeetingParticipant invitee, Guid actorId, CancellationToken cancellationToken = default)
        {
            Invitations.Add(new InvitationCall(meeting, invitee, actorId));
            return Task.FromResult(Result.Success());
        }

        public Task<Result> QueueReminderAsync(Meeting meeting, MeetingParticipant recipient, DateTime scheduledAtUtc, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success());

        public Task<Result> QueueStartedAsync(Meeting meeting, Guid actorId, CancellationToken cancellationToken = default)
        {
            Started.Add(new StartedCall(meeting.Id, meeting.ActualStartTime, actorId));
            return Task.FromResult(Result.Success());
        }

        public Task<Result> QueueMinutesPublishedAsync(Meeting meeting, MeetingMinutes minutes, Guid actorId, CancellationToken cancellationToken = default)
        {
            Publications.Add(new PublicationCall(meeting.Id, minutes.Id, minutes.State, actorId));
            return Task.FromResult(Result.Success());
        }

        public Task<Result> QueueMinutesCommentAsync(Meeting meeting, MeetingMinutes minutes, MeetingMinutesComment comment, Guid actorId, CancellationToken cancellationToken = default)
        {
            Comments.Add(new CommentCall(minutes.Id, comment.Id, comment.PublicId, comment.ParentCommentId, comment.CreatedBy, actorId));
            return Task.FromResult(Result.Success());
        }
    }

    private sealed class RecordingReminderPublisher : IMeetingNotificationPublisher
    {
        public List<ReminderCall> Calls { get; } = [];

        public Task<Result> QueueReminderAsync(Meeting meeting, MeetingParticipant recipient, DateTime scheduledAtUtc, CancellationToken cancellationToken = default)
        {
            Calls.Add(new ReminderCall(meeting, recipient, scheduledAtUtc));
            return Task.FromResult(Result.Success());
        }

        public Task<Result> QueueInvitationAsync(Meeting meeting, MeetingParticipant invitee, Guid actorId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result> QueueStartedAsync(Meeting meeting, Guid actorId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result> QueueMinutesPublishedAsync(Meeting meeting, MeetingMinutes minutes, Guid actorId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result> QueueMinutesCommentAsync(Meeting meeting, MeetingMinutes minutes, MeetingMinutesComment comment, Guid actorId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
    {
        private readonly DateTimeOffset _utcNow = new(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc));
        public override DateTimeOffset GetUtcNow() => _utcNow;
    }

    private sealed record InvitationCall(Meeting Meeting, MeetingParticipant Invitee, Guid ActorId);
    private sealed record StartedCall(Guid MeetingId, DateTime? ActualStartTime, Guid ActorId);
    private sealed record PublicationCall(Guid MeetingId, Guid MinutesId, string State, Guid ActorId);
    private sealed record CommentCall(Guid MinutesId, Guid CommentId, Guid CommentPublicId, Guid? ParentCommentId, string CreatedBy, Guid ActorId);
    private sealed record ReminderCall(Meeting Meeting, MeetingParticipant Recipient, DateTime ScheduledAtUtc);

    private sealed class TestManagementContextAccessor(ManagementContext context) : IManagementContextAccessor
    {
        public ManagementContext? Current { get; private set; } = context;
        public void Set(ManagementContext value) => Current = value;
    }

    private sealed class SqliteTestDatabase : IAsyncDisposable
    {
        private readonly SqliteConnection _keepAlive;
        private readonly string _connectionString;

        private SqliteTestDatabase(SqliteConnection keepAlive, AppDbContext context)
        {
            _keepAlive = keepAlive;
            Context = context;
            _connectionString = keepAlive.ConnectionString;
        }

        public AppDbContext Context { get; }

        public AppDbContext CreateContext()
        {
            return new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connectionString).Options);
        }

        public static async Task<SqliteTestDatabase> CreateAsync()
        {
            AppDbContext.ConfigureTenantResolution(new HttpContextAccessor());
            var connectionString = $"Data Source=meeting_lifecycle_{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
            var keepAlive = new SqliteConnection(connectionString);
            await keepAlive.OpenAsync();
            var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connectionString).Options);
            await context.Database.EnsureCreatedAsync();
            return new SqliteTestDatabase(keepAlive, context);
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await _keepAlive.DisposeAsync();
        }
    }
}
