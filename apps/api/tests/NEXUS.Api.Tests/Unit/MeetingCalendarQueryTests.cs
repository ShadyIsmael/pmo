using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using NEXUS.Api.Management;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Meetings.Application;
using NEXUS.Modules.Meetings.Domain;
using NEXUS.Api.Tests.Testing;
using NEXUS.Persistence;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Management;
using Xunit;

namespace NEXUS.Api.Tests.Unit;

public sealed class MeetingCalendarQueryTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid UserId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public async Task List_filters_to_the_requested_authorized_projects()
    {
        var projectA = Guid.NewGuid();
        var projectB = Guid.NewGuid();
        var projectC = Guid.NewGuid();
        await using var db = CreateDb(
            Meeting(projectA, "Project A review", Utc(2026, 10, 15, 9), Utc(2026, 10, 15, 10)),
            Meeting(projectB, "Project B review", Utc(2026, 10, 15, 11), Utc(2026, 10, 15, 12)),
            Meeting(projectC, "Project C review", Utc(2026, 10, 15, 13), Utc(2026, 10, 15, 14)));
        var service = CreateService(db, new HashSet<Guid> { projectA, projectB });

        var single = await service.GetPagedAsync(1, 20, projectIds: [projectA]);
        var both = await service.GetPagedAsync(1, 20, projectIds: [projectA, projectB]);
        var unfiltered = await service.GetPagedAsync(1, 20);

        Assert.Single(single.Items);
        Assert.Equal("Project A review", single.Items[0].Title);
        Assert.Equal(2, both.TotalCount);
        Assert.Equal(2, unfiltered.TotalCount);
        Assert.DoesNotContain(unfiltered.Items, item => item.Title == "Project C review");
    }

    [Fact]
    public async Task Requesting_an_out_of_scope_project_returns_no_meetings()
    {
        var projectA = Guid.NewGuid();
        var projectC = Guid.NewGuid();
        await using var db = CreateDb(
            Meeting(projectA, "Project A review", Utc(2026, 10, 15, 9), Utc(2026, 10, 15, 10)),
            Meeting(projectC, "Project C review", Utc(2026, 10, 15, 13), Utc(2026, 10, 15, 14)));
        var service = CreateService(db, new HashSet<Guid> { projectA });

        var result = await service.GetPagedAsync(1, 20, projectIds: [projectC]);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
    }

    [Fact]
    public async Task Calendar_returns_meetings_that_overlap_the_visible_range()
    {
        var projectA = Guid.NewGuid();
        await using var db = CreateDb(
            Meeting(projectA, "Overnight", Utc(2026, 10, 14, 23), Utc(2026, 10, 15, 1)),
            Meeting(projectA, "Inside", Utc(2026, 10, 15, 9), Utc(2026, 10, 15, 10)),
            Meeting(projectA, "Next day", Utc(2026, 10, 16, 9), Utc(2026, 10, 16, 10)),
            Meeting(projectA, "Ends at range start", Utc(2026, 10, 14, 22), Utc(2026, 10, 15, 0)));
        var service = CreateService(db, new HashSet<Guid> { projectA });

        var result = await service.GetCalendarAsync(Utc(2026, 10, 15, 0), Utc(2026, 10, 16, 0));

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.TotalCount);
        Assert.False(result.Value.IsTruncated);
        Assert.Equal(new[] { "Overnight", "Inside" }, result.Value.Items.Select(item => item.Title));
        Assert.All(result.Value.Items, item =>
        {
            Assert.Equal(DateTimeKind.Utc, item.StartTime.Kind);
            Assert.Equal(DateTimeKind.Utc, item.EndTime.Kind);
        });
    }

    [Fact]
    public async Task Calendar_applies_project_status_and_search_filters()
    {
        var projectA = Guid.NewGuid();
        var projectB = Guid.NewGuid();
        await using var db = CreateDb(
            Meeting(projectA, "Planning review", Utc(2026, 10, 15, 9), Utc(2026, 10, 15, 10), "Scheduled"),
            Meeting(projectA, "Planning retro", Utc(2026, 10, 15, 11), Utc(2026, 10, 15, 12), "Completed"),
            Meeting(projectB, "Planning review", Utc(2026, 10, 15, 13), Utc(2026, 10, 15, 14), "Scheduled"));
        var service = CreateService(db, new HashSet<Guid> { projectA, projectB });

        var result = await service.GetCalendarAsync(
            Utc(2026, 10, 15, 0), Utc(2026, 10, 16, 0),
            projectIds: [projectA], status: "Scheduled", search: "Planning");

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!.Items);
        Assert.Equal("Planning review", result.Value.Items[0].Title);
        Assert.Equal(projectA, result.Value.Items[0].ProjectId);
    }

    [Theory]
    [InlineData("2026-10-16T00:00:00Z", "2026-10-15T00:00:00Z")]
    [InlineData("2026-10-15T00:00:00Z", "2026-10-15T00:00:00Z")]
    public async Task Calendar_rejects_an_empty_or_reversed_range(string from, string to)
    {
        await using var db = CreateDb();
        var service = CreateService(db, new HashSet<Guid>());

        var result = await service.GetCalendarAsync(DateTime.Parse(from).ToUniversalTime(), DateTime.Parse(to).ToUniversalTime());

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task Calendar_rejects_ranges_longer_than_366_days()
    {
        await using var db = CreateDb();
        var service = CreateService(db, new HashSet<Guid>());

        var result = await service.GetCalendarAsync(Utc(2026, 1, 1, 0), Utc(2027, 1, 3, 0));

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task Accessible_project_options_are_searchable_by_name_or_code()
    {
        var projectA = Guid.NewGuid();
        var projectB = Guid.NewGuid();
        var projects = new[]
        {
            new ProjectReference(projectA, "Digital Rollout", PublicId: Guid.NewGuid(), Code: "DIG-1"),
            new ProjectReference(projectB, "Core Modernization", PublicId: Guid.NewGuid(), Code: "COR-1"),
        };
        var lookup = new Mock<IProjectReferenceLookup>();
        lookup.Setup(item => item.GetFinancialReferencesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(projects);
        await using var db = CreateDb();
        var service = CreateService(db, new HashSet<Guid> { projectA, projectB }, lookup.Object);

        var byName = await service.GetAccessibleProjectsAsync("roll");
        var byCode = await service.GetAccessibleProjectsAsync("cor-");
        var all = await service.GetAccessibleProjectsAsync();

        Assert.Single(byName);
        Assert.Equal("Digital Rollout", byName[0].Name);
        Assert.Equal("DIG-1", byName[0].Code);
        Assert.Single(byCode);
        Assert.Equal("Core Modernization", byCode[0].Name);
        Assert.Equal(new[] { "Core Modernization", "Digital Rollout" }, all.Select(item => item.Name));
    }

    private static Meeting Meeting(Guid projectId, string title, DateTime start, DateTime end, string status = "Scheduled") =>
        new()
        {
            TenantId = TenantId,
            ProjectId = projectId,
            Title = title,
            StartTime = start,
            EndTime = end,
            Status = status
        };

    private static DateTime Utc(int year, int month, int day, int hour) =>
        new(year, month, day, hour, 0, 0, DateTimeKind.Utc);

    private static AppDbContext CreateDb(params Meeting[] meetings)
    {
        AppDbContext.ConfigureTenantResolution(new HttpContextAccessor());
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var db = new AppDbContext(options);
        db.Meetings.AddRange(meetings);
        db.SaveChanges();
        return db;
    }

    private static MeetingService CreateService(
        AppDbContext db,
        IReadOnlySet<Guid> scopedProjectIds,
        IProjectReferenceLookup? projectLookup = null)
    {
        var management = new ManagementContextAccessor();
        management.Set(new ManagementContext(
            UserId, TenantId, ManagementPerspective.ProjectManager, ManagementViewMode.Native,
            null, null, null,
            new ManagementScope(new HashSet<Guid>(), new HashSet<Guid>(), scopedProjectIds)));

        var projects = projectLookup ?? Mock.Of<IProjectReferenceLookup>();
        if (projectLookup is null)
        {
            Mock.Get(projects)
                .Setup(item => item.GetReferencesAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IEnumerable<Guid> ids, CancellationToken _) =>
                    ids.Distinct().ToDictionary(
                        id => id,
                        id => new ProjectReference(id, $"Project {id.ToString("N")[..4]}", PublicId: Guid.NewGuid(), Code: "CODE")));
        }

        var users = new Mock<IUserReferenceLookup>();
        users.Setup(item => item.GetManyAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, UserReference>());

        return new MeetingService(
            db,
            Mock.Of<IAuditService>(),
            projects,
            users.Object,
            Mock.Of<IWorkItemReferenceLookup>(),
            Mock.Of<IHttpContextAccessor>(),
            Mock.Of<IMeetingMinutesAttachmentStorage>(),
            Mock.Of<IMeetingMinutesAttachmentPolicy>(),
            management,
            new NoOpMeetingNotificationPublisher());
    }
}
