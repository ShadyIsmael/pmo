using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using NEXUS.Api.Management;
using NEXUS.Modules.Audit.Application;
using NEXUS.Modules.Meetings.Application;
using NEXUS.Modules.Meetings.Domain;
using NEXUS.Persistence;
using NEXUS.SharedKernel;
using NEXUS.SharedKernel.Management;
using Xunit;

namespace NEXUS.Api.Tests.Unit;

public sealed class MeetingWorkItemScopeTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task Adding_out_of_scope_project_work_item_to_meeting_is_rejected()
    {
        var projectAId = Guid.NewGuid();
        var projectBId = Guid.NewGuid();
        var meeting = new Meeting { TenantId = TenantId, ProjectId = projectAId, Title = "Project A meeting" };
        var minutes = new MeetingMinutes { TenantId = TenantId, MeetingId = meeting.Id };
        var workItemId = Guid.NewGuid();
        await using var db = CreateDb(meeting, minutes);
        var workItems = new Mock<IWorkItemReferenceLookup>();
        workItems.Setup(lookup => lookup.FindAsync(workItemId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkItemReference(workItemId, "Out-of-scope task", "Task", "Open", projectBId, null, null));
        var service = CreateService(db, workItems.Object, new HashSet<Guid> { projectAId });

        var result = await service.AddWorkItemLinkAsync(meeting.Id, new() { WorkItemId = workItemId }, "user");

        Assert.True(result.IsFailure);
        Assert.Empty(await db.Set<MeetingMinutesWorkItemLink>().ToListAsync());
    }

    [Fact]
    public async Task Existing_link_does_not_reveal_out_of_scope_work_item_details()
    {
        var projectAId = Guid.NewGuid();
        var projectBId = Guid.NewGuid();
        var meeting = new Meeting { TenantId = TenantId, ProjectId = projectAId, Title = "Project A meeting" };
        var minutes = new MeetingMinutes { TenantId = TenantId, MeetingId = meeting.Id };
        var workItemId = Guid.NewGuid();
        var link = new MeetingMinutesWorkItemLink { TenantId = TenantId, MeetingMinutesId = minutes.Id, WorkItemId = workItemId };
        await using var db = CreateDb(meeting, minutes, link);
        var workItems = new Mock<IWorkItemReferenceLookup>();
        workItems.Setup(lookup => lookup.GetManyAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, WorkItemReference>
            {
                [workItemId] = new(workItemId, "Out-of-scope task", "Task", "Open", projectBId, null, null)
            });
        var service = CreateService(db, workItems.Object, new HashSet<Guid> { projectAId });

        var result = await service.GetWorkItemLinksAsync(meeting.Id);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!);
    }

    private static AppDbContext CreateDb(Meeting meeting, MeetingMinutes minutes, MeetingMinutesWorkItemLink? link = null)
    {
        AppDbContext.ConfigureTenantResolution(new HttpContextAccessor());
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var db = new AppDbContext(options);
        db.Meetings.Add(meeting);
        db.MeetingMinutes.Add(minutes);
        if (link is not null) db.Set<MeetingMinutesWorkItemLink>().Add(link);
        db.SaveChanges();
        return db;
    }

    private static MeetingService CreateService(AppDbContext db, IWorkItemReferenceLookup workItems, IReadOnlySet<Guid> scopedProjectIds)
    {
        var management = new ManagementContextAccessor();
        management.Set(new ManagementContext(
            Guid.NewGuid(), TenantId, ManagementPerspective.ProjectManager, ManagementViewMode.Native,
            null, null, null,
            new ManagementScope(new HashSet<Guid>(), new HashSet<Guid>(), scopedProjectIds)));

        return new MeetingService(
            db,
            Mock.Of<IAuditService>(),
            Mock.Of<IProjectReferenceLookup>(),
            Mock.Of<IUserReferenceLookup>(),
            workItems,
            Mock.Of<IHttpContextAccessor>(),
            Mock.Of<IMeetingMinutesAttachmentStorage>(),
            Mock.Of<IMeetingMinutesAttachmentPolicy>(),
            management);
    }
}
