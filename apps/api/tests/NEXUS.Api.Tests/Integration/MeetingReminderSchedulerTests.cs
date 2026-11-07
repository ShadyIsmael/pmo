using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NEXUS.Modules.Identity.Domain;
using NEXUS.Modules.Meetings.Application;
using NEXUS.Modules.Meetings.Domain;
using NEXUS.Persistence;
using NEXUS.SharedKernel;
using Xunit;

namespace NEXUS.Api.Tests.Integration;

public sealed class MeetingReminderSchedulerTests
{
    private static readonly Guid TenantId = Guid.Parse("30000000-0000-0000-0000-000000000001");
    private static readonly Guid OtherTenantId = Guid.Parse("30000000-0000-0000-0000-000000000002");
    private static readonly DateTime FixedNowUtc = new(2026, 11, 5, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task RunOnce_QueuesOnlyCurrentActiveInvitedUsers_AndDoesNotRepeatOccurrence()
    {
        await using var database = await TestDatabase.CreateAsync();
        var organizerId = Guid.NewGuid();
        var invitedId = Guid.NewGuid();
        var declinedId = Guid.NewGuid();
        var removedId = Guid.NewGuid();
        var inactiveId = Guid.NewGuid();
        var foreignTenantId = Guid.NewGuid();
        var meeting = CreateMeeting(organizerId, FixedNowUtc.AddMinutes(10));
        var participants = new[]
        {
            CreateParticipant(meeting, invitedId, MeetingInvitationResponseStatuses.Declined),
            CreateParticipant(meeting, declinedId, MeetingInvitationResponseStatuses.Declined),
            CreateParticipant(meeting, removedId, MeetingInvitationResponseStatuses.Accepted, deleted: true),
            CreateParticipant(meeting, inactiveId, MeetingInvitationResponseStatuses.Pending),
            CreateParticipant(meeting, foreignTenantId, MeetingInvitationResponseStatuses.Tentative)
        };
        participants[^1].TenantId = OtherTenantId;

        database.Context.Add(meeting);
        database.Context.AddRange(participants);
        database.Context.AddRange(
            CreateUser(invitedId, TenantId),
            CreateUser(declinedId, TenantId),
            CreateUser(removedId, TenantId),
            CreateUser(inactiveId, TenantId, active: false),
            CreateUser(foreignTenantId, OtherTenantId),
            CreateUser(organizerId, TenantId));
        await database.Context.SaveChangesAsync();

        // An occurrence can have a dispatch before a participant becomes an active invitee.
        var dueAtUtc = Utc(meeting.StartTime).AddMinutes(-meeting.ReminderOffsetMinutes!.Value);
        database.Context.Set<MeetingReminderDispatch>().Add(CreateDispatch(meeting, dueAtUtc));
        await database.Context.SaveChangesAsync();
        participants[0].InvitationResponseStatus = MeetingInvitationResponseStatuses.Accepted;

        // Recurrence occurrences have separate Meeting identities and dispatches.
        var nextOccurrence = CreateMeeting(Guid.NewGuid(), meeting.StartTime.AddMinutes(20));
        nextOccurrence.ReminderOffsetMinutes = MeetingReminderOffsets.OneHour;
        nextOccurrence.Recurrence = meeting.Recurrence;
        var nextInviteeId = Guid.NewGuid();
        database.Context.Add(nextOccurrence);
        database.Context.Add(CreateParticipant(nextOccurrence, nextInviteeId, MeetingInvitationResponseStatuses.Pending));
        database.Context.Add(CreateUser(nextInviteeId, TenantId));
        await database.Context.SaveChangesAsync();

        var publisher = new RecordingPublisher();
        await RunSchedulerOnceAsync(database, publisher, async () =>
        {
            await WaitUntilAsync(async () =>
            {
                await using var context = database.CreateContext();
                var dispatches = await context.Set<MeetingReminderDispatch>().IgnoreQueryFilters().ToListAsync();
                return dispatches.Count == 2
                    && dispatches.All(dispatch => dispatch.Status == MeetingReminderDispatchStatuses.Delivered);
            });
        });

        var calls = publisher.Calls;
        Assert.Equal(2, calls.Count);
        var firstCall = Assert.Single(calls.Where(call => call.Meeting.Id == meeting.Id));
        Assert.Equal(invitedId, firstCall.Recipient.UserId);
        Assert.Equal(dueAtUtc, firstCall.ScheduledAtUtc);
        Assert.Equal(DateTimeKind.Utc, firstCall.ScheduledAtUtc.Kind);
        var nextCall = Assert.Single(calls.Where(call => call.Meeting.Id == nextOccurrence.Id));
        Assert.Equal(nextInviteeId, nextCall.Recipient.UserId);
        Assert.Equal(Utc(nextOccurrence.StartTime).AddMinutes(-MeetingReminderOffsets.OneHour), nextCall.ScheduledAtUtc);

        await using (var verifyContext = database.CreateContext())
        {
            var dispatches = await verifyContext.Set<MeetingReminderDispatch>().IgnoreQueryFilters().ToListAsync();
            Assert.All(dispatches, dispatch => Assert.Equal(MeetingReminderDispatchStatuses.Delivered, dispatch.Status));
            Assert.All(dispatches, dispatch => Assert.False(dispatch.IsDeleted));
        }

        // A second poll/restart must observe the durable terminal dispatch and not queue again.
        await RunSchedulerOnceAsync(database, publisher, async () =>
        {
            await Task.Delay(100);
        });
        Assert.Equal(2, publisher.Calls.Count);
    }

    [Fact]
    public async Task RunOnce_IncludesOrganizerOnlyWhenOrganizerIsAnActiveInvitedParticipant()
    {
        await using var database = await TestDatabase.CreateAsync();
        var organizerId = Guid.NewGuid();
        var meeting = CreateMeeting(organizerId, FixedNowUtc.AddMinutes(10));
        var organizerInvite = CreateParticipant(meeting, organizerId, MeetingInvitationResponseStatuses.Pending);
        database.Context.Add(meeting);
        database.Context.Add(organizerInvite);
        database.Context.Add(CreateUser(organizerId, TenantId));
        await database.Context.SaveChangesAsync();

        var publisher = new RecordingPublisher();
        await RunSchedulerOnceAsync(database, publisher, async () =>
        {
            await publisher.FirstCall.WaitAsync(TimeSpan.FromSeconds(5));
        });

        var call = Assert.Single(publisher.Calls);
        Assert.Equal(organizerId, call.Recipient.UserId);
    }

    [Theory]
    [InlineData(MeetingStatuses.InProgress)]
    [InlineData(MeetingStatuses.Cancelled)]
    public async Task RunOnce_SuppressesPendingReminderAfterMeetingStartsOrIsCancelled(string status)
    {
        await using var database = await TestDatabase.CreateAsync();
        var meeting = CreateMeeting(Guid.NewGuid(), FixedNowUtc.AddMinutes(10));
        meeting.Status = status;
        database.Context.Add(meeting);
        database.Context.Set<MeetingReminderDispatch>().Add(CreateDispatch(
            meeting,
            Utc(meeting.StartTime).AddMinutes(-meeting.ReminderOffsetMinutes!.Value)));
        await database.Context.SaveChangesAsync();

        var publisher = new RecordingPublisher();
        await RunSchedulerOnceAsync(database, publisher, async () =>
        {
            await WaitUntilAsync(async () =>
            {
                await using var context = database.CreateContext();
                var dispatch = await context.Set<MeetingReminderDispatch>().IgnoreQueryFilters().SingleAsync();
                return dispatch.IsDeleted;
            });
        });

        Assert.Empty(publisher.Calls);
    }

    [Fact]
    public async Task RunOnce_RollsBackFailedQueueAndRetriesPendingDispatch()
    {
        await using var database = await TestDatabase.CreateAsync();
        var userId = Guid.NewGuid();
        var meeting = CreateMeeting(Guid.NewGuid(), FixedNowUtc.AddMinutes(10));
        database.Context.Add(meeting);
        database.Context.Add(CreateParticipant(meeting, userId, MeetingInvitationResponseStatuses.Accepted));
        database.Context.Add(CreateUser(userId, TenantId));
        database.Context.Set<MeetingReminderDispatch>().Add(CreateDispatch(
            meeting,
            Utc(meeting.StartTime).AddMinutes(-meeting.ReminderOffsetMinutes!.Value)));
        await database.Context.SaveChangesAsync();

        var publisher = new RecordingPublisher(failuresBeforeSuccess: 1);
        await RunSchedulerOnceAsync(database, publisher, async () =>
        {
            await publisher.FirstCall.WaitAsync(TimeSpan.FromSeconds(5));
            await WaitUntilAsync(async () =>
            {
                await using var context = database.CreateContext();
                var dispatch = await context.Set<MeetingReminderDispatch>().IgnoreQueryFilters().SingleAsync();
                return dispatch.Status == MeetingReminderDispatchStatuses.Pending;
            });
        });

        await using (var verifyContext = database.CreateContext())
        {
            var dispatch = await verifyContext.Set<MeetingReminderDispatch>().IgnoreQueryFilters().SingleAsync();
            Assert.Equal(MeetingReminderDispatchStatuses.Pending, dispatch.Status);
        }

        await RunSchedulerOnceAsync(database, publisher, async () =>
        {
            await WaitUntilAsync(async () =>
            {
                await using var context = database.CreateContext();
                var dispatch = await context.Set<MeetingReminderDispatch>().IgnoreQueryFilters().SingleAsync();
                return dispatch.Status == MeetingReminderDispatchStatuses.Delivered;
            });
        });

        Assert.Equal(2, publisher.Calls.Count);
        await using var finalContext = database.CreateContext();
        var finalDispatch = await finalContext.Set<MeetingReminderDispatch>().IgnoreQueryFilters().SingleAsync();
        Assert.Equal(MeetingReminderDispatchStatuses.Delivered, finalDispatch.Status);
    }

    private static Meeting CreateMeeting(Guid organizerId, DateTime startTimeUtc) => new()
    {
        TenantId = TenantId,
        Title = "Reminder scheduler test",
        StartTime = startTimeUtc,
        EndTime = startTimeUtc.AddMinutes(30),
        ReminderOffsetMinutes = MeetingReminderOffsets.FifteenMinutes,
        OrganizerId = organizerId,
        Status = MeetingStatuses.Scheduled,
        Recurrence = "Weekly",
        CreatedBy = "test"
    };

    private static MeetingParticipant CreateParticipant(
        Meeting meeting,
        Guid? userId,
        string? invitationStatus,
        bool deleted = false) => new()
    {
        TenantId = meeting.TenantId,
        MeetingId = meeting.Id,
        UserId = userId,
        Email = userId.HasValue ? $"{userId:N}@example.test" : "external@example.test",
        InvitationResponseStatus = invitationStatus,
        IsDeleted = deleted,
        CreatedBy = "test"
    };

    private static User CreateUser(Guid id, Guid tenantId, bool active = true) => new()
    {
        Id = id,
        TenantId = tenantId,
        Email = $"{id:N}@example.test",
        FirstName = "Meeting",
        LastName = "Invitee",
        IsActive = active,
        CreatedBy = "test"
    };

    private static MeetingReminderDispatch CreateDispatch(Meeting meeting, DateTime scheduledAtUtc) => new()
    {
        TenantId = meeting.TenantId,
        MeetingId = meeting.Id,
        ScheduledAtUtc = scheduledAtUtc,
        Status = MeetingReminderDispatchStatuses.Pending,
        CreatedBy = "test"
    };

    private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private static async Task RunSchedulerOnceAsync(
        TestDatabase database,
        RecordingPublisher publisher,
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
            new FixedTimeProvider(FixedNowUtc));

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

        Assert.True(await condition(), "The reminder scheduler did not reach the expected state.");
    }

    private sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
    {
        private readonly DateTimeOffset _utcNow = new(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc));

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }

    private sealed class RecordingPublisher(int failuresBeforeSuccess = 0) : IMeetingNotificationPublisher
    {
        private readonly List<ReminderCall> _calls = [];
        private readonly TaskCompletionSource _firstCall = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _remainingFailures = failuresBeforeSuccess;

        public IReadOnlyList<ReminderCall> Calls
        {
            get
            {
                lock (_calls) return _calls.ToArray();
            }
        }

        public Task FirstCall => _firstCall.Task;

        public Task<Result> QueueReminderAsync(
            Meeting meeting,
            MeetingParticipant recipient,
            DateTime scheduledAtUtc,
            CancellationToken cancellationToken = default)
        {
            lock (_calls) _calls.Add(new ReminderCall(meeting, recipient, scheduledAtUtc));
            _firstCall.TrySetResult();
            if (Interlocked.Decrement(ref _remainingFailures) >= 0)
                return Task.FromResult(Result.Failure(Error.Validation("Expected test publisher failure.")));
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

    private sealed record ReminderCall(Meeting Meeting, MeetingParticipant Recipient, DateTime ScheduledAtUtc);

    private sealed class TestDatabase : IAsyncDisposable
    {
        private readonly SqliteConnection _keepAlive;
        private readonly DbContextOptions<AppDbContext> _options;

        private TestDatabase(SqliteConnection keepAlive, DbContextOptions<AppDbContext> options, AppDbContext context)
        {
            _keepAlive = keepAlive;
            _options = options;
            Context = context;
        }

        public AppDbContext Context { get; }

        public AppDbContext CreateContext() => new(_options);

        public static async Task<TestDatabase> CreateAsync()
        {
            var connectionString = $"Data Source=meeting_reminders_{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
            var keepAlive = new SqliteConnection(connectionString);
            await keepAlive.OpenAsync();
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connectionString).Options;
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
