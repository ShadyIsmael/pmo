using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Audit.Domain;
using NEXUS.Modules.Meetings.Application;
using NEXUS.Modules.Meetings.Application.DTOs;
using NEXUS.Modules.Meetings.Domain;
using NEXUS.Api.Tests.Testing;
using NEXUS.Persistence;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Management;
using Xunit;

namespace NEXUS.Api.Tests.Integration;

public sealed class MeetingMinutesServiceRegressionTests : IDisposable
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _otherTenantId = Guid.NewGuid();
    private readonly Guid _actorId = Guid.NewGuid();
    private readonly Guid _projectId = Guid.NewGuid();
    private readonly Guid _otherProjectId = Guid.NewGuid();
    private readonly Mock<IAuditService> _audit = new();
    private readonly Mock<IProjectReferenceLookup> _projects = new();
    private readonly Mock<IUserReferenceLookup> _users = new();
    private readonly IHttpContextAccessor _http = new HttpContextAccessor();

    public MeetingMinutesServiceRegressionTests()
    {
        AppDbContext.ConfigureTenantResolution(_http);
        _audit.Setup(service => service.LogAsync(It.IsAny<AuditEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        _users.Setup(service => service.GetManyAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, UserReference>());
        _projects.Setup(service => service.FindAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid projectId, CancellationToken _) =>
                projectId == _projectId ? new ProjectReference(projectId, "Test project") : null);
    }

    [Fact]
    public async Task Completed_scoped_meeting_without_minutes_returns_successful_null()
    {
        using var tenantScope = TrustedTenantExecutionContext.Enter(_tenantId);
        var meeting = NewMeeting(_tenantId, _projectId, MeetingStatuses.Completed);
        await using var db = CreateDb(meeting);
        var service = CreateService(db, _projectId);

        var result = await service.GetMinutesAsync(meeting.Id);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task Public_id_detail_resolution_provides_internal_id_for_minutes_read()
    {
        using var tenantScope = TrustedTenantExecutionContext.Enter(_tenantId);
        var meeting = NewMeeting(_tenantId, _projectId, MeetingStatuses.Completed);
        meeting.Id = Guid.Parse("AA12F90D-44FA-4A16-BCB1-D1F6D8F5806F");
        meeting.PublicId = Guid.Parse("2025488D-AE03-480D-936D-CB5F7C9C4A74");
        await using var db = CreateDb(meeting);
        var service = CreateService(db, _projectId);

        var detail = await service.GetByPublicIdAsync(meeting.PublicId);

        Assert.True(detail.IsSuccess);
        Assert.Equal(meeting.Id, detail.Value!.Id);
        Assert.Equal(meeting.PublicId, detail.Value.PublicId);

        var minutes = await service.GetMinutesAsync(detail.Value.Id);

        Assert.True(minutes.IsSuccess);
        Assert.Null(minutes.Value);
    }

    [Fact]
    public async Task Public_detail_lookup_does_not_accept_internal_id_as_public_id()
    {
        using var tenantScope = TrustedTenantExecutionContext.Enter(_tenantId);
        var meeting = NewMeeting(_tenantId, _projectId, MeetingStatuses.Completed);
        meeting.Id = Guid.Parse("AA12F90D-44FA-4A16-BCB1-D1F6D8F5806F");
        meeting.PublicId = Guid.Parse("2025488D-AE03-480D-936D-CB5F7C9C4A74");
        await using var db = CreateDb(meeting);
        var service = CreateService(db, _projectId);

        var result = await service.GetByPublicIdAsync(meeting.Id);

        AssertNotFound(result);
    }

    [Fact]
    public async Task Public_detail_lookup_keeps_missing_foreign_and_out_of_scope_meetings_non_disclosing()
    {
        using var tenantScope = TrustedTenantExecutionContext.Enter(_tenantId);
        var foreignMeeting = NewMeeting(_otherTenantId, _projectId, MeetingStatuses.Completed);
        var outOfScopeMeeting = NewMeeting(_tenantId, _otherProjectId, MeetingStatuses.Completed);
        await using var db = CreateDb(foreignMeeting, outOfScopeMeeting);
        var service = CreateService(db, _projectId);

        var foreignResult = await service.GetByPublicIdAsync(foreignMeeting.PublicId);
        var outOfScopeResult = await service.GetByPublicIdAsync(outOfScopeMeeting.PublicId);
        var missingResult = await service.GetByPublicIdAsync(Guid.NewGuid());

        AssertNotFound(foreignResult);
        AssertNotFound(outOfScopeResult);
        AssertNotFound(missingResult);
    }

    [Fact]
    public async Task Public_detail_lookup_allows_in_scope_organizer_and_eligible_attendee()
    {
        using var tenantScope = TrustedTenantExecutionContext.Enter(_tenantId);
        var organizedMeeting = NewMeeting(_tenantId, null, MeetingStatuses.Completed);
        organizedMeeting.OrganizerId = _actorId;
        var attendeeMeeting = NewMeeting(_tenantId, null, MeetingStatuses.Completed);
        var participant = new MeetingParticipant
        {
            TenantId = _tenantId,
            MeetingId = attendeeMeeting.Id,
            UserId = _actorId,
            Name = "Actor",
            Email = "actor@example.test",
        };
        await using var db = CreateDb(organizedMeeting, attendeeMeeting, participant);
        var service = CreateService(db, _projectId);

        var organized = await service.GetByPublicIdAsync(organizedMeeting.PublicId);
        var attendee = await service.GetByPublicIdAsync(attendeeMeeting.PublicId);

        Assert.True(organized.IsSuccess);
        Assert.Equal(organizedMeeting.Id, organized.Value!.Id);
        Assert.True(attendee.IsSuccess);
        Assert.Equal(attendeeMeeting.Id, attendee.Value!.Id);
    }

    [Fact]
    public async Task Missing_foreign_and_out_of_management_scope_meetings_remain_not_found()
    {
        using var tenantScope = TrustedTenantExecutionContext.Enter(_tenantId);
        var foreignMeeting = NewMeeting(_otherTenantId, _projectId, MeetingStatuses.Completed);
        var outOfScopeMeeting = NewMeeting(_tenantId, _otherProjectId, MeetingStatuses.Completed);
        await using var db = CreateDb(foreignMeeting, outOfScopeMeeting);
        var service = CreateService(db, _projectId);

        var foreignResult = await service.GetMinutesAsync(foreignMeeting.Id);
        var outOfScopeResult = await service.GetMinutesAsync(outOfScopeMeeting.Id);
        var nonexistentResult = await service.GetMinutesAsync(Guid.NewGuid());

        AssertNotFound(foreignResult);
        AssertNotFound(outOfScopeResult);
        AssertNotFound(nonexistentResult);
    }

    [Fact]
    public async Task Minutes_are_unavailable_until_completed()
    {
        using var tenantScope = TrustedTenantExecutionContext.Enter(_tenantId);
        var meeting = NewMeeting(_tenantId, _projectId, MeetingStatuses.InProgress);
        await using var db = CreateDb(meeting);
        var service = CreateService(db, _projectId);

        var read = await service.GetMinutesAsync(meeting.Id);
        var create = await service.CreateMinutesAsync(meeting.Id, new CreateMeetingMinutesRequest { Summary = "Too early" }, _actorId.ToString());

        Assert.True(read.IsFailure);
        Assert.Equal(404, read.Error?.StatusCode);
        Assert.True(create.IsFailure);
        Assert.Equal(409, create.Error?.StatusCode);
        Assert.Empty(await db.MeetingMinutes.ToListAsync());
    }

    [Fact]
    public async Task Repeated_draft_initialization_returns_existing_minutes_without_overwriting_or_duplicating()
    {
        using var tenantScope = TrustedTenantExecutionContext.Enter(_tenantId);
        var meeting = NewMeeting(_tenantId, _projectId, MeetingStatuses.Completed);
        await using var db = CreateDb(meeting);
        var service = CreateService(db, _projectId);

        var first = await service.CreateMinutesAsync(meeting.Id, new CreateMeetingMinutesRequest
        {
            Summary = "Original summary",
            DiscussionNotes = "Original notes",
            Decisions = "Original decision",
            Outcomes = "Original outcome",
        }, _actorId.ToString());
        var retry = await service.CreateMinutesAsync(meeting.Id, new CreateMeetingMinutesRequest
        {
            Summary = "Replacement summary",
            DiscussionNotes = "Replacement notes",
            Decisions = "Replacement decision",
            Outcomes = "Replacement outcome",
        }, _actorId.ToString());

        Assert.True(first.IsSuccess);
        Assert.True(retry.IsSuccess);
        Assert.Equal(first.Value!.Id, retry.Value!.Id);
        Assert.Equal("Original summary", retry.Value.Summary);
        Assert.Equal("Original notes", retry.Value.DiscussionNotes);
        Assert.Equal("Original decision", retry.Value.Decisions);
        Assert.Equal("Original outcome", retry.Value.Outcomes);
        Assert.Equal(MeetingMinutesStates.Draft, retry.Value.State);
        Assert.Single(await db.MeetingMinutes.ToListAsync());
        _audit.Verify(service => service.LogAsync(It.IsAny<AuditEntry>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Draft_creation_retry_does_not_replace_published_minutes()
    {
        using var tenantScope = TrustedTenantExecutionContext.Enter(_tenantId);
        var meeting = NewMeeting(_tenantId, _projectId, MeetingStatuses.Completed);
        var minutes = new MeetingMinutes
        {
            TenantId = _tenantId,
            MeetingId = meeting.Id,
            Summary = "Published summary",
            DiscussionNotes = "Published notes",
            Decisions = "Published decision",
            Outcomes = "Published outcome",
            State = MeetingMinutesStates.Final,
        };
        await using var db = CreateDb(meeting, minutes);
        var service = CreateService(db, _projectId);

        var result = await service.CreateMinutesAsync(meeting.Id, new CreateMeetingMinutesRequest { Summary = "Do not replace" }, _actorId.ToString());

        Assert.True(result.IsSuccess);
        Assert.Equal(minutes.Id, result.Value!.Id);
        Assert.Equal(MeetingMinutesStates.Final, result.Value.State);
        Assert.Equal("Published summary", result.Value.Summary);
        Assert.Equal("Published notes", result.Value.DiscussionNotes);
        Assert.Equal("Published decision", result.Value.Decisions);
        Assert.Equal("Published outcome", result.Value.Outcomes);
        Assert.Single(await db.MeetingMinutes.ToListAsync());
        _audit.Verify(service => service.LogAsync(It.IsAny<AuditEntry>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Scheduled_to_in_progress_to_completed_persists_actual_timestamps_and_preserves_meeting_content()
    {
        using var tenantScope = TrustedTenantExecutionContext.Enter(_tenantId);
        var meeting = NewMeeting(_tenantId, _projectId, MeetingStatuses.Scheduled);
        var agenda = new MeetingAgenda { TenantId = _tenantId, MeetingId = meeting.Id, Topic = "Review delivery", SortOrder = 0 };
        var participant = new MeetingParticipant { TenantId = _tenantId, MeetingId = meeting.Id, Name = "Attendee", Email = "attendee@example.test", AttendanceStatus = "Present", InvitationResponseStatus = "Accepted" };
        var action = new MeetingAction { TenantId = _tenantId, MeetingId = meeting.Id, Action = "Send summary", Status = MeetingActionStatuses.Open, AgendaItemId = agenda.Id };
        await using var db = CreateDb(meeting, agenda, participant, action);
        var service = CreateService(db, _projectId);

        var started = await service.StartAsync(meeting.Id, new MeetingLifecycleRequest(), _actorId.ToString());
        var ended = await service.EndAsync(meeting.Id, new MeetingLifecycleRequest(), _actorId.ToString());

        Assert.True(started.IsSuccess);
        Assert.Equal(MeetingStatuses.InProgress, started.Value!.Status);
        Assert.NotNull(started.Value.ActualStartTime);
        Assert.True(ended.IsSuccess);
        Assert.Equal(MeetingStatuses.Completed, ended.Value!.Status);
        Assert.NotNull(ended.Value.ActualStartTime);
        Assert.NotNull(ended.Value.ActualEndTime);
        Assert.True(ended.Value.ActualEndTime!.Value >= ended.Value.ActualStartTime!.Value);

        var minutesImmediatelyAfterEnd = await service.GetMinutesAsync(meeting.Id);
        Assert.True(minutesImmediatelyAfterEnd.IsSuccess);
        Assert.Null(minutesImmediatelyAfterEnd.Value);

        var persisted = await db.Meetings.SingleAsync(item => item.Id == meeting.Id);
        Assert.Equal(MeetingStatuses.Completed, persisted.Status);
        Assert.Equal(ended.Value.ActualStartTime, persisted.ActualStartTime);
        Assert.Equal(ended.Value.ActualEndTime, persisted.ActualEndTime);
        Assert.Equal("Review delivery", (await db.MeetingAgendas.SingleAsync()).Topic);
        Assert.Equal("Present", (await db.MeetingParticipants.SingleAsync()).AttendanceStatus);
        Assert.Equal("Accepted", (await db.MeetingParticipants.SingleAsync()).InvitationResponseStatus);
        Assert.Equal("Send summary", (await db.MeetingActions.SingleAsync()).Action);
    }

    private AppDbContext CreateDb(params object[] entities)
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);
        db.Database.EnsureCreated();
        foreach (var tenantGroup in entities.OfType<ITenantEntity>().GroupBy(entity => entity.TenantId))
        {
            using var tenantScope = TrustedTenantExecutionContext.Enter(tenantGroup.Key);
            db.AddRange(tenantGroup.Cast<object>());
            db.SaveChanges();
        }
        return db;
    }

    private MeetingService CreateService(AppDbContext db, Guid projectId)
    {
        var management = new TestManagementContextAccessor(new ManagementContext(
            _actorId,
            _tenantId,
            ManagementPerspective.ProjectManager,
            ManagementViewMode.Native,
            null,
            null,
            projectId,
            new ManagementScope(new HashSet<Guid>(), new HashSet<Guid>(), new HashSet<Guid> { projectId })));

        return new MeetingService(
            db,
            _audit.Object,
            _projects.Object,
            _users.Object,
            Mock.Of<IWorkItemReferenceLookup>(),
            _http,
            Mock.Of<IMeetingMinutesAttachmentStorage>(),
            Mock.Of<IMeetingMinutesAttachmentPolicy>(),
            management,
            new NoOpMeetingNotificationPublisher());
    }

    private static Meeting NewMeeting(Guid tenantId, Guid? projectId, string status) => new()
    {
        TenantId = tenantId,
        ProjectId = projectId,
        Title = "Minutes regression meeting",
        StartTime = DateTime.UtcNow.AddHours(-1),
        EndTime = DateTime.UtcNow.AddHours(1),
        Status = status,
    };

    private static void AssertNotFound<T>(Result<T> result)
    {
        Assert.True(result.IsFailure);
        Assert.Equal(404, result.Error?.StatusCode);
    }

    public void Dispose() => AppDbContext.ConfigureTenantResolution(new HttpContextAccessor());

    private sealed class TestManagementContextAccessor(ManagementContext context) : IManagementContextAccessor
    {
        public ManagementContext? Current { get; private set; } = context;
        public void Set(ManagementContext value) => Current = value;
    }
}
