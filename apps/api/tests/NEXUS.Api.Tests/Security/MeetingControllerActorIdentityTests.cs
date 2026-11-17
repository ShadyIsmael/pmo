using NEXUS.SharedKernel;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using NEXUS.Modules.Meetings.Api;
using NEXUS.Modules.Meetings.Application;
using NEXUS.Modules.Meetings.Application.DTOs;
using NEXUS.SharedKernel.Management;
using Xunit;

namespace NEXUS.Api.Tests.Security;

public sealed class MeetingControllerActorIdentityTests
{
    [Fact]
    public async Task Create_AuthenticatedTenantOnlyPrincipalIsRejectedBeforeServiceCall()
    {
        var meetings = new Mock<IMeetingService>(MockBehavior.Strict);
        var workspace = new Mock<IMeetingWorkspaceService>(MockBehavior.Strict);
        var controller = new MeetingController(meetings.Object, workspace.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim("tenantId", Guid.NewGuid().ToString()),
                        new Claim("permission", MeetingPermissions.Create)
                    ], "test"))
                }
            }
        };

        var response = await controller.Create(new CreateMeetingRequest(), CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(response);
        meetings.VerifyNoOtherCalls();
        workspace.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task CreateSeries_AuthenticatedTenantOnlyPrincipalIsRejectedBeforeServiceCall()
    {
        var meetings = new Mock<IMeetingService>(MockBehavior.Strict);
        var workspace = new Mock<IMeetingWorkspaceService>(MockBehavior.Strict);
        var controller = new MeetingController(meetings.Object, workspace.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim("tenantId", Guid.NewGuid().ToString()),
                        new Claim("permission", MeetingPermissions.Create)
                    ], "test"))
                }
            }
        };

        var response = await controller.CreateSeries(new CreateMeetingSeriesRequest(), CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(response);
        meetings.VerifyNoOtherCalls();
        workspace.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task MeetingService_CreateRejectsManagementContextForDifferentAuthenticatedActorBeforePersistence()
    {
        var actorId = Guid.NewGuid();
        var otherActorId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim("tenantId", tenantId.ToString()),
                new Claim(ClaimTypes.NameIdentifier, actorId.ToString())
            ], "test"))
        };
        var management = new Mock<IManagementContextAccessor>(MockBehavior.Strict);
        management.Setup(accessor => accessor.Current).Returns(new ManagementContext(
            otherActorId,
            tenantId,
            ManagementPerspective.ProjectManager,
            ManagementViewMode.Native,
            null,
            null,
            null,
            ManagementScope.Empty));
        using var db = new Microsoft.EntityFrameworkCore.DbContext(
            new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<Microsoft.EntityFrameworkCore.DbContext>().Options);
        var service = new MeetingService(
            db,
            Mock.Of<NEXUS.Modules.Audit.Application.IAuditService>(),
            Mock.Of<NEXUS.SharedKernel.IProjectReferenceLookup>(),
            Mock.Of<IUserReferenceLookup>(),
            Mock.Of<NEXUS.SharedKernel.IWorkItemReferenceLookup>(),
            new HttpContextAccessor { HttpContext = httpContext },
            Mock.Of<IMeetingMinutesAttachmentStorage>(),
            Mock.Of<IMeetingMinutesAttachmentPolicy>(),
            management.Object,
            Mock.Of<IMeetingNotificationPublisher>());

        var result = await service.CreateAsync(new CreateMeetingRequest(), actorId.ToString());

        Assert.True(result.IsFailure);
        Assert.Equal(401, result.Error?.StatusCode);
        management.Verify(accessor => accessor.Current, Times.AtLeastOnce);
        management.VerifyNoOtherCalls();
    }
}
