using System.Security.Claims;
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

namespace NEXUS.Api.Tests.Security;

public sealed class MeetingWorkspaceScopeTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task Workspace_lookup_fails_closed_when_management_context_is_missing()
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("tenantId", TenantId.ToString("D"))],
                authenticationType: "test"))
        };
        var httpContextAccessor = new HttpContextAccessor { HttpContext = httpContext };
        AppDbContext.ConfigureTenantResolution(httpContextAccessor);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        await using var db = new AppDbContext(options);
        var meeting = new Meeting
        {
            TenantId = TenantId,
            ProjectId = Guid.NewGuid(),
            Title = "Scoped meeting",
            Status = MeetingStatuses.Scheduled
        };
        db.Meetings.Add(meeting);
        db.Set<MeetingAgenda>().Add(new MeetingAgenda
        {
            TenantId = TenantId,
            MeetingId = meeting.Id,
            Topic = "Private agenda item"
        });
        await db.SaveChangesAsync();

        var service = new MeetingWorkspaceService(
            db,
            Mock.Of<IAuditService>(),
            Mock.Of<IUserReferenceLookup>(),
            httpContextAccessor,
            new ManagementContextAccessor());

        var result = await service.GetAgendaAsync(meeting.Id);

        Assert.True(result.IsFailure);
    }
}
